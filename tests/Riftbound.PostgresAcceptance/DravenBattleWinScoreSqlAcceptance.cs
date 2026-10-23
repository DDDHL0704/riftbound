using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class DravenBattleWinScoreSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private const string Effect = "CAPTURED_DRAVEN_FIRST_BATTLE_WIN_SCORE";
    private static string Marker(DravenBattleWinScoreContext c) => $"DRAVEN_FIRST_BATTLE_WIN:{c.TurnNumber}:{c.Source.Generation}:{c.Source.ObjectId}";

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh Draven score SQL evidence is required."); Directory.CreateDirectory(output);
        foreach (var branch in new[] { "draven-keep", "draven-return", "draven-winning", "draven-winning-return",
            "draven-alt-keep", "draven-alt-return", "draven-alt-winning", "draven-alt-winning-return",
            "forge-generation", "forge-printing", "forge-result-tick" }) await Run(branch);

        async Task Run(string branch)
        {
            var folder = Path.Combine(output, branch); Directory.CreateDirectory(folder);
            var room = "draven-first-battle-win-score-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); MatchState state = null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var winning = branch.Contains("-winning", StringComparison.Ordinal);
            var returned = branch.EndsWith("-return", StringComparison.Ordinal);
            var alternate = branch.StartsWith("draven-alt", StringComparison.Ordinal);
            var forge = branch.StartsWith("forge-", StringComparison.Ordinal);
            var scenario = "battle-result-draven" + (alternate ? "-alt" : "") + (winning ? "-winning" : "");
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-draven-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The controlled position is an original accepted command."); await Cold();
            var original = (await store.LoadAsync(room, default))!;
            Check(original.OpeningSeed != 0 && original.ReplayInitialState is { Tick: 0, Status: "EMPTY", Seats.Count: 0 }
                && original.Commands.Count == 3 && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT"),
                "Original seats, private opening and controlled seed all remain in the action log.");
            await Submit("alice", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
            for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++) await Submit(state.FocusPlayerId, new PassFocusCommand());
            await Submit("alice", new DeclareBattleCommand("FIELD", ["SOURCE"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
            var earned = state.StackItems.Single().DravenBattleWinScore!;
            Check(earned is not null && state.PendingBattleResult is { Stage: "RESULT_TRIGGERS", WinnerPlayerId: "alice" }
                && state.CardObjects["FIELD"].ControllerId == "bob" && state.CardObjects["SOURCE"].IsAttacking
                && state.PlayerScores["alice"] == (winning ? 7 : 0) && state.UntilEndOfTurnEffects.Contains(Marker(earned)),
                "The first occurrence is captured before its score, control and battle end, with a real response window.");
            if (forge)
            {
                await Save("original-final-frame.json.gz", (await store.LoadAsync(room, default))!);
                await SaveRows("original-final-seven-tables.json.gz"); await CorruptAndRefuse();
            }
            else
            {
                if (returned)
                {
                    await Submit("alice", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"])); await Top();
                    Check(state.PlayerZones["alice"].Hand.Contains("SOURCE")
                        && state.CardObjects["SOURCE"].ObjectGeneration > earned!.Source.Generation,
                        "A legal response returns the source as a new private incarnation.");
                    Check(state.StackItems.Single().DravenBattleWinScore == earned, "The earned skill retains its original source and controller.");
                }
                for (var i = 0; state.Status == MatchStatuses.InProgress && state.PriorityPlayerId is not null; i++)
                { Check(i < 8, "The original score continuation must finish."); await Submit(state.PriorityPlayerId, new PassPriorityCommand()); }
                var events = journal.Entries.SelectMany(e => e.Events).ToArray();
                Check(state.PlayerScores["alice"] == (winning ? 8 : returned ? 1 : 2)
                    && state.WinnerPlayerId == (winning ? "alice" : null) && state.UntilEndOfTurnEffects.Contains(Marker(earned!))
                    && events.Count(e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), Effect)) == 1
                    && !events.Any(e => e.Kind == "CARD_DRAWN"), "Exactly one earned first-occurrence point, independently of source departure.");
                if (winning)
                {
                    Check(state.PendingBattleResult is not null && state.CardObjects["FIELD"].ControllerId == "bob"
                        && !events.Any(e => e.Kind is "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED"),
                        "Normal victory finishes the score parent before control or battle close.");
                    var count = journal.Entries.Count;
                    var raw = JsonSerializer.SerializeToElement(new { cmdType = "END_TURN", promptId = session.PromptFor("alice").PromptId, snapshotTick = state.Tick });
                    var refused = false;
                    try { await session.SubmitAsync("alice", "post-finish-refusal", new EndTurnCommand(), raw, default); }
                    catch (MatchSessionException error) when (error.Code == ErrorCodes.MatchFinished) { refused = true; }
                    Check(refused && journal.Entries.Count == count, "Finished-session rejection never writes a new accepted or rejected history row.");
                }
                else Check(state.PendingBattleResult is null && events.Count(e => e.Kind == "BATTLE_CLOSED") == 1
                    && events.Count(e => e.Kind == "BATTLEFIELD_CONQUERED") == (returned ? 0 : 1),
                    "Control follows the earned score and uses actual surviving occupancy.");
                var final = (await store.LoadAsync(room, default))!;
                Check(final.OpeningSeed == original.OpeningSeed && final.Commands.Take(original.Commands.Count)
                    .Select(MatchStateHasher.HashValue).SequenceEqual(original.Commands.Select(MatchStateHasher.HashValue)), "Original histories and seeds are unchanged.");
                await Save("original-final-frame.json.gz", final); await SaveRows("original-final-seven-tables.json.gz");
            }
            Check(cold == journal.Entries.Count, "Every accepted prefix independently replayed and cold restored.");
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_SQL_DRAVEN_FIRST_BATTLE_WIN_SCORE_SCOPED", room, branch, scenario,
                originalAcceptedInputs = journal.Entries.Count, coldRecoveries = cold,
                structurallyConsistentOriginalJournalForgeryRejected = forge, actualRuleInputs = RuleInputIdentity.Current,
                originalPrivateSeed = original.OpeningSeed, controlledPositionNotOrdinaryOpening = true,
                postFinishRejectedWithoutJournalChange = winning, noAcceptedHistoryRewrite = true, wholeCardCertification = false }, Json));
            Console.WriteLine($"DRAVEN_FIRST_WIN_SCORE_SQL_PASS {branch} inputs={journal.Entries.Count} cold={cold}");

            async Task Top()
            {
                var id = state.StackItems.Last().StackItemId;
                for (var i = 0; state.StackItems.Any(s => s.StackItemId == id); i++)
                { Check(i < 4, "A reaction parent must finish in this bounded position."); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
            }
            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var result = await session.SubmitAsync(actor, prefix, command, JsonSerializer.SerializeToElement(raw, Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "An original battle-result-continuation intent was rejected."); await Cold();
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
                var item = state.StackItems.Single(); var context = item.DravenBattleWinScore!; var continuation = state.PendingBattleResult!;
                if (branch == "forge-generation") context = context with { Source = context.Source with { Generation = context.Source.Generation + 1 } };
                else if (branch == "forge-printing") { context = context with { CardNo = "SFD·148a/221" }; item = item with { CardNo = context.CardNo }; }
                else { context = context with { ResultTick = context.ResultTick - 1 }; continuation = continuation with { ResultTick = context.ResultTick }; }
                var forged = state with { PendingBattleResult = continuation, StackItems = [item with { DravenBattleWinScore = context }],
                    UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Select(x => x == Marker(state.StackItems.Single().DravenBattleWinScore!) ? Marker(context) : x).ToArray() };
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
                        Check(await command.ExecuteNonQueryAsync() == 1, "Only this new room's one latest view is deliberately corrupted.");
                    }
                }
                var corrupt = (await store.LoadAsync(room, default))!; await Save("actual-corrupt-frame.json.gz", corrupt); await SaveRows("actual-corrupt-seven-tables.json.gz");
                Check(corrupt.IsConsistent, "Coordinated projected views must pass structural checks before original-history rejection: " + string.Join("; ", corrupt.ValidationErrors));
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Any(e => e.Contains("original", StringComparison.Ordinal)), "Original trigger birth or result event must reject coordinated history forgery.");
                var refused = false;
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException e) when (e.Code == ErrorCodes.RecoveryInconsistent) { refused = true; }
                Check(refused, "A new registry must refuse this actual corrupted SQL room.");
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
