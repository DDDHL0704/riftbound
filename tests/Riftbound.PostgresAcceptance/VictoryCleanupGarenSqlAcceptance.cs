using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class VictoryCleanupGarenSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh victory-cleanup-garen SQL evidence is required."); Directory.CreateDirectory(output);
        foreach (var branch in new[] { "garen-one", "garen-four", "healthy-low", "healthy-win", "wounded-low", "wounded-win", "reject-forged-mundo" })
            await Run(branch);

        async Task Run(string branch)
        {
            var folder = Path.Combine(output, branch); Directory.CreateDirectory(folder);
            var room = "victory-cleanup-garen-completion-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); MatchState state = null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var scenario = "victory-cleanup-" + (branch == "reject-forged-mundo" ? "wounded-win" : branch);
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The declared development setup must be an original recorded command."); await Cold();
            var original = (await store.LoadAsync(room, default))!;
            Check(original.OpeningSeed != 0 && original.ReplayInitialState is { Tick: 0, Status: "EMPTY", Seats.Count: 0 }
                && original.Commands.Count == 3 && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT"),
                "Both original atomic seats, private seed, explicit development input and EMPTY origin are retained.");
            var garen = branch.StartsWith("garen-", StringComparison.Ordinal);
            if (garen)
            {
                var movingCount = branch == "garen-four" ? 4 : 1;
                var units = new[] { "U1", "U2", "U3", "U4" };
                var moving = units.Take(movingCount).ToArray();
                await Submit("alice", new MoveUnitCommand(moving[0], "BASE", "BATTLEFIELD:TARGET", [], moving));
                await Submit("alice", new PassFocusCommand()); await Submit("bob", new PassFocusCommand());
                var expectedDraw = movingCount == 4 ? 2 : 0;
                Check(state.CardObjects["TARGET"].ControllerId == "alice" && state.PlayerScores["alice"] == 1
                    && state.PlayerScores["bob"] == 0 && state.Status == MatchStatuses.InProgress && state.WinnerPlayerId is null,
                    "Both exact Garen branches must actually conquer and gain one ordinary point.");
                Check(state.PlayerZones["alice"].Hand.Count == expectedDraw && state.PlayerZones["alice"].MainDeck.Count == 2 - expectedDraw
                    && units.Count(id => state.ObjectLocations[id].BattlefieldObjectId == "TARGET") == movingCount
                    && units.Count(id => state.ObjectLocations[id].BattlefieldObjectId == "OTHER") == 4 - movingCount,
                    "Garen counts only the actually conquered battlefield, independently of the other owned and controlled units.");
                var events = journal.Entries.SelectMany(e => e.Events).ToArray();
                var triggers = events.Where(e => e.Kind == "LEGEND_TRIGGER_RESOLVED"
                    && Equals(e.Payload.GetValueOrDefault("trigger"), "BATTLEFIELD_CONQUERED_DRAW_TWO")).ToArray();
                Check(events.Where(e => e.Kind == "CARD_DRAWN").Sum(e => Convert.ToInt32(e.Payload["count"])) == expectedDraw
                    && triggers.Length == (movingCount == 4 ? 1 : 0)
                    && (triggers.Length == 0 || Equals(triggers[0].Payload["battlefieldId"], "TARGET")
                        && Equals(triggers[0].Payload["controlledBattlefieldUnitCount"], 4)),
                    "Actual draw events and the trigger's count use the same exact battlefield.");
                Check(events.All(e => e.Kind is not ("BURNOUT_APPLIED" or "MATCH_WON")), "Garen controls do not rely on burnout or victory.");
            }
            else
            {
                var wounded = branch.StartsWith("wounded-", StringComparison.Ordinal) || branch == "reject-forged-mundo";
                var wins = !branch.EndsWith("-low", StringComparison.Ordinal);
                Check(state.CardObjects["MUNDO"].Power == 6 && state.CardObjects["MUNDO"].Damage == 0 && Might() == 9,
                    "Mundo starts with actual printed6 and three original graveyard cards, undamaged and alive.");
                if (wounded)
                {
                    await Submit("alice", new PlayCardCommand("DAMAGE", "OGS·022/024", ["MUNDO"])); await Top();
                    Check(state.CardObjects["MUNDO"].Damage == 8 && Might() == 10 && state.PlayerZones["alice"].Base.Contains("MUNDO")
                        && state.PlayerZones["alice"].Graveyard.Contains("DAMAGE"),
                        "Original real printed damage is legally survived before the draw; no seeded damage or accepted prefix rewrite.");
                }
                await Submit("alice", new PlayCardCommand("DRAW", "OGN·047/298", [])); await Top();
                var alive = !wounded || wins;
                Check(state.PlayerZones["alice"].Base.Contains("MUNDO") == alive
                    && state.PlayerZones["alice"].Graveyard.Contains("MUNDO") == !alive,
                    "Normal winning cleanup precedes lethal damage; the nonwinning wounded control still dies.");
                Check(state.PlayerScores["bob"] == (wins ? 8 : 7) && state.WinnerPlayerId == (wins ? "bob" : null)
                    && state.Status == (wins ? MatchStatuses.Finished : MatchStatuses.InProgress)
                    && state.PlayerZones["alice"].Base.Contains("RUNE") && state.CardObjects["RUNE"].IsExhausted,
                    "The original draw and rune call finish before ordinary victory or lethal cleanup.");
                var events = journal.Entries.SelectMany(e => e.Events).ToArray(); var kinds = events.Select(e => e.Kind).ToList();
                Check(kinds.Count(k => k == "BURNOUT_APPLIED") == 1 && kinds.Count(k => k == "CARD_DRAWN") == 1
                    && kinds.Count(k => k == "RUNES_CALLED") == 1 && kinds.IndexOf("CARD_DRAWN") < kinds.IndexOf("RUNES_CALLED")
                    && kinds.Count(k => k == "MATCH_WON") == (wins ? 1 : 0), "Actual single draw, rune call and victory events have independent exact counts.");
                if (wins) Check(kinds.IndexOf("RUNES_CALLED") < kinds.IndexOf("MATCH_WON"), "The rest of the first-burnout parent completes before the win.");
                var deaths = events.Where(e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "MUNDO")).ToArray();
                Check(deaths.Length == (alive ? 0 : 1), "No premature destruction event may survive in a winning original history.");
            }
            int Might() => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["alice"], Json)
                .GetProperty("players").GetProperty("alice").GetProperty("objects").GetProperty("MUNDO").GetProperty("effectivePower").GetInt32();
            async Task Top()
            {
                var top = state.StackItems.Last().StackItemId;
                for (var step = 0; step < 8 && state.StackItems.Any(i => i.StackItemId == top); step++)
                    await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
                Check(!state.StackItems.Any(i => i.StackItemId == top), "The finite original top parent must complete or suspend into its required hand choice.");
            }
            var final = (await store.LoadAsync(room, default))!;
            Check(final.OpeningSeed == original.OpeningSeed
                && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(final.ReplayInitialState, Json), JsonSerializer.SerializeToNode(original.ReplayInitialState, Json))
                && final.Commands.Take(original.Commands.Count).Select(MatchStateHasher.HashValue).SequenceEqual(original.Commands.Select(MatchStateHasher.HashValue)),
                "No original setup, accepted history or private opening is rewritten.");
            await Save("original-final-frame.json.gz", final); await SaveRows("original-final-seven-tables.json.gz");
            if (branch == "reject-forged-mundo") await CorruptAndRefuse();
            Check(cold == journal.Entries.Count, "Every accepted original SQL input has a real complete cold recovery.");
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_SQL_VICTORY_CLEANUP_GAREN_SCOPED", room, branch, scenario, originalAcceptedInputs = journal.Entries.Count,
                coldRecoveries = cold, deliberateCoordinatedForgeryRejected = branch == "reject-forged-mundo",
                originalPrivateSeed = original.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current,
                controlledPositionNotOrdinaryOpening = true, noAcceptedHistoryRewrite = true, wholeCardOrNativeVisibleCertification = false }, Json));
            Console.WriteLine($"VICTORY_CLEANUP_GAREN_SQL_PASS {branch} inputs={journal.Entries.Count} cold={cold}");

            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var result = await session.SubmitAsync(actor, prefix, command, JsonSerializer.SerializeToElement(raw, Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "An original victory-cleanup-garen intent was rejected."); await Cold();
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
                Check(state.PlayerZones["alice"].Base.Contains("MUNDO"), "Forge only the fresh winning original after the correct surviving-unit assertion.");
                var forged = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["alice"] = state.PlayerZones["alice"] with {
                        Base = state.PlayerZones["alice"].Base.Where(id => id != "MUNDO").ToArray(),
                        Graveyard = state.PlayerZones["alice"].Graveyard.Append("MUNDO").ToArray() } },
                    ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                        ["MUNDO"] = new("alice", "GRAVEYARD") } };
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
                Check(errors.Count > 0, "Coordinated premature-death, physical-location and private-view forgery must fail against the unchanged original action log.");
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
