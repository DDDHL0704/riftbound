using System.IO.Compression;
using System.Text.Json;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class SealedSeriesSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] HistoryTables = ["command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh sealed SQL output required.");
        Directory.CreateDirectory(output);
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        await Run("OGN", 1);
        await Run("SFD", 3);

        async Task Run(string setCode, int bestOf)
        {
            var folder = Path.Combine(output, $"{setCode}-best-of-{bestOf}");
            Directory.CreateDirectory(folder);
            var room = "sealed-series-sql-" + Guid.NewGuid().ToString("N");
            var engine = new CoreRuleEngine();
            var journal = new CapturingJournal(new PostgresMatchJournal(source));
            var players = new PostgresMatchPlayerStore(source);
            var recovery = new PostgresMatchRecoveryStore(source);
            var results = new PostgresMatchResultStore(source);
            var adjudications = new MatchTournamentAdjudicationService(results);
            IMatchSession session = new MatchSession(MatchState.Create(room) with
            {
                Seed = 424242, SessionOptions = MatchSessionOptions.ForSealed(setCode, bestOf)
            }, engine, journal, players);
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            var coldCount = 0;
            var nextInput = 0;
            string? originalPools = null;
            var gameReplays = new List<object>();
            MatchState State() => journal.Entries[^1].AuthoritativeState;

            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken;
            var firstPool = MatchStateHasher.HashValue(session.SnapshotFor("alice").SealedPool);
            await Cold("first-seat");
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken;
            Check(firstPool == MatchStateHasher.HashValue(session.SnapshotFor("alice").SealedPool), "Second seat changed the first player's pool.");
            originalPools = MatchStateHasher.HashValue(State().PlayerSealedPools);
            await Cold("both-seats-before-order");

            var losers = bestOf == 1 ? new[] { "bob" } : new[] { "bob", "alice", "bob" };
            for (var game = 1; game <= losers.Length; game++)
            {
                var series = session.SnapshotFor("alice").Series!;
                var chooser = series.PlayOrderChooserPlayerId!;
                Check(game == 1 || chooser == losers[game - 2], "The previous loser must choose the next play order.");
                await Act(chooser, new ChoosePlayOrderCommand(game % 2 == 1));
                foreach (var player in new[] { "alice", "bob" })
                {
                    var deck = Deck(State().PlayerSealedPools![player], catalog, bestOf,
                        withLegend: game > 1, withChampion: game == 3, useRealBattlefield: game == 1);
                    await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
                    await Cold($"game-{game}-{player}-secret-submission");
                    Check(State().SealedBattlefieldHistory?.Count == game - 1 || game == 1 && State().SealedBattlefieldHistory is null,
                        "A private submission consumed or revealed a battlefield before both players were ready.");
                }
                await Act("alice", new ReadyCommand());
                await Cold($"game-{game}-one-ready");
                await Act("bob", new ReadyCommand());
                Check(State().Phase == MatchPhases.Mulligan && State().SealedBattlefieldHistory!.Count == game,
                    "Both ready inputs must reveal exactly one battlefield per player and enter core 110.");
                await Cold($"game-{game}-battlefield-reveal");
                var first = State().ActivePlayerId;
                var second = State().OpeningSecondActionPlayerId!;
                await Act(first, new MulliganCommand([]));
                await Act(second, new MulliganCommand([]));
                await Cold($"game-{game}-playing");

                if (game < losers.Length)
                {
                    await Act(losers[game - 1], new SurrenderCommand());
                    Check(await results.GetMatchResultAsync(room, default) is null, "An unfinished series wrote public match history.");
                    await VerifyGame(game);
                    await Cold($"game-{game}-intergame-wait");
                    continue;
                }

                // Fail the actual result INSERT after the authoritative ending
                // has committed. The application must repair only the missing
                // result, using the original accepted command after cold restore.
                var finalId = "final-result-original-input";
                var constraint = "qa_sealed_result_" + Guid.NewGuid().ToString("N");
                await Execute($"alter table match_results add constraint {constraint} check (match_id <> '{room}') not valid");
                var failed = false;
                try
                {
                    try { await Finish(finalId); }
                    catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.CheckViolation) { failed = true; }
                }
                finally { await Execute($"alter table match_results drop constraint {constraint}"); }
                Check(failed && State().Status == MatchStatuses.Finished, "Real result SQL failure must retain the committed final game.");
                Check(await results.GetMatchResultAsync(room, default) is null, "Failed result transaction left a partial result.");
                var failedRows = await Rows(["matches", "match_players", .. HistoryTables, "match_results", "match_result_players"]);
                Check(failedRows["match_results"].Length == 0 && failedRows["match_result_players"].Length == 0,
                    "Failed final-result transaction left public player-result rows.");
                await Save("original-nine-tables-after-result-rollback.json.gz", failedRows);
                await VerifyGame(game);
                await Cold("final-state-before-result-repair");
                var history = await Rows(HistoryTables);
                var retries = await Task.WhenAll(Finish(finalId), Finish(finalId));
                Check(retries.All(retried => retried.Accepted && retried.Events.Count == 0),
                    "Concurrent cold retries must return the restored receipt without another ending.");
                Check(MatchStateHasher.HashValue(history) == MatchStateHasher.HashValue(await Rows(HistoryTables)),
                    "Repairing the public result rewrote original authoritative history.");
                var recorded = await results.GetMatchResultAsync(room, default) ?? throw new InvalidOperationException("Final public result missing after retry.");
                Check(recorded.WinnerPlayerId == "alice" && recorded.Players.Single(p => p.PlayerId == "alice").Score == (bestOf == 1 ? 1 : 2)
                    && recorded.Players.Single(p => p.PlayerId == "bob").Score == (bestOf == 1 ? 0 : 1), "Public result contains core points instead of series wins.");
                var beforeDuplicate = await Rows(["matches", "match_results", "match_result_players"]);
                await Task.WhenAll(Enumerable.Range(1, 8).Select(index => new PostgresMatchResultStore(source)
                    .RecordFinalMatchResultAsync(recorded with { FinishedAtUtc = recorded.FinishedAtUtc.AddSeconds(index) }, default).AsTask()));
                var conflicting = false;
                try
                {
                    await new PostgresMatchResultStore(source).RecordFinalMatchResultAsync(recorded with
                    {
                        Players = recorded.Players.Select(player => player with { Score = player.Score + 1 }).ToArray()
                    }, default);
                }
                catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { conflicting = true; }
                Check(conflicting, "A conflicting final score must be rejected without overwriting the original SQL result.");
                await Finish(finalId);
                Check(MatchStateHasher.HashValue(beforeDuplicate) == MatchStateHasher.HashValue(await Rows(["matches", "match_results", "match_result_players"])),
                    "Duplicate result retry changed the original result or finish time.");
                Check((await results.ListMatchResultsForPlayerAsync("alice", 100, default)).Count(r => r.RoomId == room) == 1,
                    "A series must appear once in public match history.");
                await Cold("completed-result");
                await Save("original-nine-tables-final.json.gz", await Rows(["matches", "match_players", .. HistoryTables, "match_results", "match_result_players"]));
                await Save("receipt.json.gz", new
                {
                    status = "PASSED_REAL_SEALED_SQL_SERIES_RECOVERY_AND_FINAL_RESULT_REPAIR", room, setCode, bestOf,
                    originalPools, coldRecoveries = coldCount, gameReplays, commands = journal.Entries.Count, tick = State().Tick,
                    actualRuleInputs = RuleInputIdentity.Current, result = recorded,
                    realResultCheckViolation = failed, originalHistoryRetained = true, concurrentFinalAdmission = true,
                    completeScoringGameCoverage = false
                });

                async Task<ResolutionResult> Finish(string id) => bestOf == 3
                    ? await adjudications.ApplyGameLossAsync((IMatchTournamentAdjudicator)session, id, new(game, losers[game - 1]), default)
                    : await Act(losers[game - 1], new SurrenderCommand(), id);
            }

            async Task<ResolutionResult> Act(string player, GameCommand command, string? intentId = null)
            {
                var id = intentId ?? "sealed-sql-input-" + ++nextInput;
                var raw = JsonSerializer.SerializeToElement(command, command.GetType(), Json);
                var result = command switch
                {
                    SubmitDeckCommand deck => await session.SubmitDeckAsync(player, id, deck, raw, default),
                    ReadyCommand => await session.ReadyAsync(player, id, raw, default),
                    _ => await session.SubmitAsync(player, id, command, raw, default)
                };
                Check(result.Accepted, result.ErrorMessage ?? "Sealed SQL input was rejected.");
                await MatchResultRecords.RecordFinishedSeriesAsync(results, result, default);
                return result;
            }

            async Task VerifyGame(int number)
            {
                var frame = await Load();
                Check(frame.AuthoritativeState!.LastCompletedSeriesGame?.GameNumber == number, "The SQL frame lost the original completed core game.");
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(frame.ReplayInitialState!, frame.Commands,
                    frame.AuthoritativeState, engine, default, frame.Events);
                Check(replay.IsMatch, string.Join("; ", replay.Errors));
                var receipt = new { gameNumber = number, replay.ReplayedStateHash, replay.ExpectedStateHash,
                    coreHash = MatchStateHasher.Hash(frame.AuthoritativeState.LastCompletedSeriesGame!.State) };
                gameReplays.Add(receipt);
                await Save($"game-{number}-original-terminal-replay.json.gz", new { receipt, frame });
            }

            async Task Cold(string label)
            {
                var frame = await Load();
                var history = await Rows(HistoryTables);
                await Save($"cold-{++coldCount:D2}-{label}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(engine, journal, recovery, players).GetOrCreateAsync(room, default);
                foreach (var player in tokens.Keys.ToArray())
                {
                    tokens[player] = (await session.ReconnectPlayerAsync(player, tokens[player], default)).ReconnectToken;
                    Check(MatchStateHasher.HashValue(session.SnapshotFor(player)) == MatchStateHasher.HashValue(frame.PlayerViews[player].Snapshot)
                        && MatchStateHasher.HashValue(session.PromptFor(player)) == MatchStateHasher.HashValue(frame.PlayerViews[player].Prompt),
                        "Real SQL cold reconnect changed a private snapshot or action prompt.");
                }
                Check(MatchStateHasher.HashValue(history) == MatchStateHasher.HashValue(await Rows(HistoryTables)), "Cold recovery rewrote original history tables.");
            }

            async Task<MatchRecoveryFrame> Load()
            {
                var frame = await recovery.LoadAsync(room, default) ?? throw new InvalidOperationException("Sealed SQL recovery frame missing.");
                Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
                Check(frame.OpeningSeed == 424242 && frame.ReplayInitialState!.Seats.Count == 0
                    && frame.ReplayInitialState.PlayerSealedPools is null && frame.ReplayInitialState.SessionOptions == MatchSessionOptions.ForSealed(setCode, bestOf),
                    "Original empty-room replay input, private seed or room options changed.");
                Check(originalPools is null || originalPools == MatchStateHasher.HashValue(frame.AuthoritativeState!.PlayerSealedPools), "SQL recovery changed a sealed pool.");
                foreach (var (player, view) in frame.PlayerViews)
                    Check(MatchStateHasher.HashValue(view.Snapshot.SealedPool) == MatchStateHasher.HashValue(SealedRoomPools.ViewFor(frame.AuthoritativeState!, player)),
                        "A private SQL view contains another player's pool.");
                Check(frame.SpectatorReplayFrame!.SpectatorSnapshot.SealedPool is null, "SQL spectator replay exposed a private pool.");
                return frame;
            }

            async Task<Dictionary<string, string[]>> Rows(IEnumerable<string> tables)
            {
                var value = new Dictionary<string, string[]>(StringComparer.Ordinal);
                await using var connection = await source.OpenConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await new NpgsqlCommand("set transaction read only", connection, transaction).ExecuteNonQueryAsync();
                foreach (var table in tables)
                {
                    var rows = new List<string>();
                    await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection, transaction);
                    query.Parameters.AddWithValue("room", room);
                    await using var reader = await query.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
                    value.Add(table, rows.ToArray());
                }
                await transaction.CommitAsync();
                return value;
            }

            async Task Execute(string sql)
            {
                await using var connection = await source.OpenConnectionAsync();
                await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();
            }

            async Task Save(string name, object value)
            {
                await using var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
                await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
                await JsonSerializer.SerializeAsync(gzip, value, value.GetType(), Json);
            }
        }
    }

    private static OfficialDecklist Deck(SealedPoolGeneration pool, OfficialCardCatalog catalog, int bestOf,
        bool withLegend, bool withChampion, bool useRealBattlefield)
    {
        var cards = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        var owned = pool.Pool.Keys.Select(no => cards[no]).OrderBy(card => card.CardNo, StringComparer.Ordinal).ToArray();
        var traits = new[] { RuneTrait.Red, RuneTrait.Green, RuneTrait.Blue, RuneTrait.Orange, RuneTrait.Purple, RuneTrait.Yellow };
        for (var a = 0; a < traits.Length; a++)
        for (var b = a + 1; b < traits.Length; b++)
        for (var c = b + 1; c < traits.Length; c++)
        {
            var allowed = new[] { traits[a], traits[b], traits[c] };
            bool Fits(OfficialCard card) => card.CardColorList.Select(RuneTrait.Normalize).All(trait => trait == "colorless" || allowed.Contains(trait));
            var legend = withLegend ? owned.FirstOrDefault(card => card.CardCategoryName == "传奇" && Fits(card)) : null;
            var champion = withChampion ? owned.FirstOrDefault(card => card.CardCategoryName == "英雄单位" && Fits(card)) : null;
            if (withLegend && legend is null || withChampion && champion is null) continue;
            var main = owned.Where(card => Fits(card) && card.CardCategoryName is "单位" or "英雄单位" or "法术" or "专属法术" or "装备")
                .SelectMany(card => Enumerable.Repeat(card.CardNo, pool.Pool[card.CardNo])).ToList();
            if (main.Count < 25) continue;
            if (champion is not null) { main.Remove(champion.CardNo); main.Insert(0, champion.CardNo); }
            var rune = catalog.Cards.First(card => SealedDeckRules.IsBasicRune(card) && Fits(card));
            var count = bestOf == 3 ? 1 : 3;
            var field = useRealBattlefield ? owned.FirstOrDefault(card => card.CardCategoryName == "战场" && Fits(card))?.CardNo : null;
            var battlefields = Enumerable.Repeat(SealedDeckRules.BlankBattlefieldCardNo, count).ToArray();
            if (field is not null) battlefields[0] = field;
            var deck = new OfficialDecklist(legend?.CardNo ?? "", champion?.CardNo ?? "", main.Take(25).ToArray(), Enumerable.Repeat(rune.CardNo, 12).ToArray(), battlefields);
            Check(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.Sealed, new(pool.Pool, bestOf)).IsValid,
                "SQL fixture must submit an unmodified pool's legal 25-card configuration.");
            return deck;
        }
        throw new InvalidOperationException("The actual server-generated six packs must support this fixture's legal role configuration.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class CapturingJournal(IMatchRoomSeatJournal inner) : IMatchRoomSeatJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            await inner.RecordAsync(entry, cancellationToken);
            Entries.Add(entry);
        }
        public async ValueTask RecordRoomSeatAsync(MatchJournalEntry entry, string reconnectTokenHash, CancellationToken cancellationToken)
        {
            await inner.RecordRoomSeatAsync(entry, reconnectTokenHash, cancellationToken);
            Entries.Add(entry);
        }
    }
}
