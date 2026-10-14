using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class PrintedPredictSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-printed-predict-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var initial = new MatchState(room, tick: 0, turnNumber: 1, activePlayerId: "P1",
            seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, seed: 424242,
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(10, 10), ["P2"] = new(10, 10) },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["MECH"], Base = ["SEER"], MainDeck = ["D1", "D2"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["MECH"] = new("MECH", cardNo: "OGN·100/298", power: 3, manaCost: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard, "预知"]),
                ["SEER"] = new("SEER", cardNo: "OGN·100/298", power: 3, manaCost: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
                ["D1"] = new("D1", cardNo: "UNL-007/219", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
                ["D2"] = new("D2", cardNo: "OGN·048/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["MECH"] = new("P1", "HAND"), ["SEER"] = new("P1", "BASE"), ["D1"] = new("P1", "MAIN_DECK"), ["D2"] = new("P1", "MAIN_DECK") });
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            await Submit("P1", new PlayCardCommand("MECH", "OGN·100/298", [], OptionalCosts: []));
            Check(current.TriggerQueue.Count == 2, "Each captured keyword must queue separately.");
            Check(current.TriggerQueue.Select(t => t.InsightContext!.Kind).Order().SequenceEqual(["GRANTED_PREDICT", "PRINTED_PREDICT"]), "Printed and granted instances must remain distinct.");
            Check(current.TriggerQueue.Single(t => t.InsightContext!.Kind == "PRINTED_PREDICT").InsightContext!.SourceGeneration == 1, "Printed trigger must bind the entered incarnation.");
            await RejectTamperedPrintedOrigin();
            await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: current.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
            for (var instance = 0; instance < 2; instance++) {
                for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                var choice = current.PendingCardChoice!;
                Check(choice.LegalObjectIds.SequenceEqual([instance == 0 ? "D1" : "D2"]), "A later instance must observe the current top.");
                await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, instance == 0 ? ["D1"] : []));
            }
            Check(current.StackItems.Count == 0 && current.PlayerZones["P1"].MainDeck.SequenceEqual(["D2", "D1"]), "Independent recycling/retaining result mismatch.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_PRINTED_PREDICT_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, printedOriginTamperRejected = true, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_PRINTED_PREDICT_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        async Task RejectTamperedPrintedOrigin() {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            var index = Array.FindIndex(current.TriggerQueue.ToArray(), t => t.InsightContext?.Kind == "PRINTED_PREDICT");
            try {
                await using var forge = new NpgsqlCommand($"update state_snapshots set payload=jsonb_set(payload,'{{triggerQueue,{index},insightContext,printedPredict,faceRevision}}','99'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
                var bad = await store.LoadAsync(room, default);
                Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("Insight", StringComparison.Ordinal)), "Actual SQL forged printed face revision must fail independent recovery validation.");
            } finally {
                await using var restore = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                restore.Parameters.AddWithValue("payload", saved); restore.Parameters.AddWithValue("room", room); await restore.ExecuteNonQueryAsync();
            }
            var recovered = await store.LoadAsync(room, default); Check(recovered?.IsConsistent == true, "SQL restoration after mutation must be consistent.");
        }
        async Task Submit(string actor, GameCommand command) {
            var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var result = await session.SubmitAsync(actor, "printed-predict-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL frame.");
            Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            Check(frame.Commands.Count == submitted && frame.Events.Count == sequence && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "SQL history/state mismatch.");
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(frame, engine, journal, players); checks.Add(new { current.Tick, sequence, stack = current.StackItems.Count, replay.IsMatch });
        }
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
