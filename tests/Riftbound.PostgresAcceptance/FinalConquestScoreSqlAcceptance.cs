using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class FinalConquestScoreSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh final-conquest SQL evidence is required."); Directory.CreateDirectory(output);
        foreach (var branch in new[] { "draw-hold", "win", "burnout", "brush-decline", "brush-return", "combat", "reject-forged-score" })
            await Run(branch);

        async Task Run(string branch)
        {
            var folder = Path.Combine(output, branch); Directory.CreateDirectory(folder);
            var room = "final-conquest-score-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); MatchState state = null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var scenario = "final-conquest-" + (branch.StartsWith("brush", StringComparison.Ordinal) ? "brush"
                : branch is "draw-hold" or "reject-forged-score" ? "draw" : branch);
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The declared development setup must be an original recorded command."); await Cold();
            var original = (await store.LoadAsync(room, default))!;
            Check(original.OpeningSeed != 0 && original.ReplayInitialState is { Tick: 0, Status: "EMPTY", Seats.Count: 0 }
                && original.Commands.Count == 3 && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT"),
                "Both original atomic seats, private seed, explicit development input and EMPTY origin are retained.");
            if (branch == "combat") Check(state.CardObjects["UNIT"].Power > state.CardObjects["DEFENDER"].Power,
                "The actual printed attacker must survive this finite combat.");
            await Submit("alice", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await Submit("alice", new PassFocusCommand()); await Submit("bob", new PassFocusCommand());
            if (branch == "combat") await Submit("alice", new DeclareBattleCommand("BF", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
            Check(state.CardObjects["BF"].ControllerId == "alice"
                && state.UntilEndOfTurnEffects.Contains(BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "alice")),
                "Every legal conquest captures physical control and its own once-per-turn scoring action.");
            Check(state.PlayerScores["alice"] == (branch == "win" ? 8 : 7)
                && state.PlayerScores["bob"] == (branch == "burnout" ? 1 : 0), "Independent literal scores, including real burnout.");
            Check(state.Status == (branch == "win" ? MatchStatuses.Finished : MatchStatuses.InProgress)
                && state.WinnerPlayerId == (branch == "win" ? "alice" : null), "Only the eligible final conquest wins.");
            var allEvents = journal.Entries.SelectMany(e => e.Events).ToArray();
            Check(allEvents.Count(e => e.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW") == (branch == "win" ? 0 : 1),
                "The exact original point replacement occurs once, separately from actual score gain.");
            Check(state.PlayerZones["alice"].Hand.Count == (branch == "win" ? 0 : 1)
                && state.PlayerZones["alice"].MainDeck.Count == (branch == "win" ? 3 : 2), "Actual private draw and physical deck counts.");
            if (branch == "burnout") Check(state.RngCursor == 1 && state.PlayerZones["alice"].Graveyard.Count == 0
                && allEvents.Count(e => e.Kind == "BURNOUT_APPLIED") == 1, "The ordinary recycle, RNG and opponent point survive cold recovery.");
            if (branch == "combat") Check(state.PlayerZones["bob"].Graveyard.Contains("DEFENDER"), "The actual defeated printed defender remains in its owner's graveyard.");
            if (branch.StartsWith("brush", StringComparison.Ordinal))
            {
                var hand = state.PlayerZones["alice"].Hand.ToArray(); var choice = state.PendingCardChoice!;
                Check(choice.ChoiceWindow == "TRIGGER_OPTIONAL_CONFIRMATION" && choice.PlayerId == "alice"
                    && choice.LegalObjectIds.SequenceEqual(["BF"]), "A replaced point still offers the real Brush scoring trigger.");
                await Submit("alice", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, branch == "brush-return" ? ["BF"] : []));
                if (branch == "brush-return") { await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
                Check(state.CardObjects["BF"].CardNo == (branch == "brush-return" ? "OGN·282/298" : "UNL·T03")
                    && state.PlayerZones["alice"].Hand.SequenceEqual(hand) && state.PlayerScores["alice"] == 7,
                    "Accept/decline preserves the physical location, exact draw and score; the restored card does not retroactively conquer.");
            }
            if (branch == "draw-hold")
            {
                await Submit("alice", new EndTurnCommand()); await Submit("bob", new EndTurnCommand());
                Check(state.PlayerScores["alice"] == 8 && state.PlayerScores["bob"] == 0
                    && state.Status == MatchStatuses.Finished && state.WinnerPlayerId == "alice"
                    && journal.Entries.SelectMany(e => e.Events).Any(e => e.Kind == "SCORE_GAINED"
                        && Equals(e.Payload.GetValueOrDefault("reason"), "BATTLEFIELD_HELD_SCORE")),
                    "An actual later normal hold supplies the unrestricted winning point.");
            }
            var final = (await store.LoadAsync(room, default))!;
            Check(final.OpeningSeed == original.OpeningSeed
                && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(final.ReplayInitialState, Json), JsonSerializer.SerializeToNode(original.ReplayInitialState, Json))
                && final.Commands.Take(original.Commands.Count).Select(MatchStateHasher.HashValue).SequenceEqual(original.Commands.Select(MatchStateHasher.HashValue)),
                "No original setup, accepted history or private opening is rewritten.");
            await Save("original-final-frame.json.gz", final); await SaveRows("original-final-seven-tables.json.gz");
            if (branch == "reject-forged-score") await CorruptAndRefuse();
            Check(cold == journal.Entries.Count, "Every accepted original SQL input has a real complete cold recovery.");
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_SQL_FINAL_CONQUEST_SCORE_SCOPED", room, branch, scenario, originalAcceptedInputs = journal.Entries.Count,
                coldRecoveries = cold, deliberateCoordinatedForgeryRejected = branch == "reject-forged-score",
                originalPrivateSeed = original.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current,
                controlledPositionNotOrdinaryOpening = true, noAcceptedHistoryRewrite = true, wholeCardOrNativeVisibleCertification = false }, Json));
            Console.WriteLine($"FINAL_CONQUEST_SCORE_SQL_PASS {branch} inputs={journal.Entries.Count} cold={cold}");

            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var result = await session.SubmitAsync(actor, prefix, command, JsonSerializer.SerializeToElement(raw, Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "An original final-conquest intent was rejected."); await Cold();
            }
            async Task Cold()
            {
                var frame = (await store.LoadAsync(room, default))!;
                Check(frame.IsConsistent && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current,
                    "Actual SQL rows and loaded inputs must agree: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0,
                    "The entire original prefix must independently replay before every cold restore.");
                await Save($"prefix-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray()) tokens[actor] = (await session.ReconnectPlayerAsync(actor, tokens[actor], default)).ReconnectToken;
                state = frame.AuthoritativeState!; cold++;
            }
            async Task CorruptAndRefuse()
            {
                var forged = state with { PlayerScores = new Dictionary<string, int>(state.PlayerScores) { ["alice"] = 8 },
                    WinnerPlayerId = "alice", Status = MatchStatuses.Finished,
                    UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Append(BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "alice")).ToArray() };
                await using (var connection = await source.OpenConnectionAsync())
                await using (var transaction = await connection.BeginTransactionAsync())
                {
                    await Write("state_snapshots", "state_tick", null, forged);
                    foreach (var v in ResolutionResult.BuildSnapshots(forged)) await Write("snapshots", "snapshot_tick", v.Key, v.Value);
                    foreach (var v in ResolutionResult.BuildPrompts(forged)) await Write("action_prompts", "prompt_tick", v.Key, v.Value);
                    await transaction.CommitAsync();
                    async Task Write(string table, string tick, string? actor, object value)
                    {
                        await using var command = new NpgsqlCommand($"update {table} set payload=@payload where match_id=@room and {tick}=@tick" + (actor is null ? "" : " and player_id=@actor"), connection, transaction);
                        command.Parameters.AddWithValue("room", room); command.Parameters.AddWithValue("tick", state.Tick);
                        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(value, value.GetType(), Json));
                        if (actor is not null) command.Parameters.AddWithValue("actor", actor);
                        Check(await command.ExecuteNonQueryAsync() == 1, "Only one actual new room's latest original view may be deliberately corrupted.");
                    }
                }
                var corrupt = (await store.LoadAsync(room, default))!; await Save("actual-corrupt-frame.json.gz", corrupt); await SaveRows("actual-corrupt-seven-tables.json.gz");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Coordinated score, marker and private-view forgery must fail against the unchanged original action log.");
                var refused = false;
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException e) when (e.Code == ErrorCodes.RecoveryInconsistent) { refused = true; }
                Check(refused, "A fresh registry must refuse this actual corrupted database room.");
                await File.WriteAllTextAsync(Path.Combine(folder, "actual-refusal-errors.json"), JsonSerializer.Serialize(errors, Json));
            }
            async Task Save(string name, object value)
            {
                await using var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
                await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, value, value.GetType(), Json);
            }
            async Task SaveRows(string name)
            {
                var rows = new Dictionary<string, JsonElement>(); await using var connection = await source.OpenConnectionAsync();
                foreach (var table in Tables)
                {
                    await using var command = new NpgsqlCommand($"select coalesce(json_agg(t order by row_to_json(t)::text),'[]'::json)::text from (select * from {table} where match_id=@room)t", connection);
                    command.Parameters.AddWithValue("room", room); using var document = JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!); rows[table] = document.RootElement.Clone();
                }
                await Save(name, rows);
            }
        }
    }

    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private sealed class OriginalJournal(PostgresMatchJournal inner, string folder) : IMatchRoomSeatJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        private async Task Save(MatchJournalEntry entry)
        {
            await using var file = new FileStream(Path.Combine(folder, $"entry-{Entries.Count + 1:D2}-original-before-persist.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, entry, Json);
        }
        public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { await Save(entry); await inner.RecordAsync(entry, token); Entries.Add(entry); }
        public async ValueTask RecordRoomSeatAsync(MatchJournalEntry entry, string hash, CancellationToken token) { await Save(entry); await inner.RecordRoomSeatAsync(entry, hash, token); Entries.Add(entry); }
    }
}
