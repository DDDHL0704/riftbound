using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class GrantedPredictSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-predict-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var initial = new MatchState(room, tick: 0, turnNumber: 1, activePlayerId: "P1",
            seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, seed: 424242,
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(10, 10), ["P2"] = new(10, 10) },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["MECH"], Base = ["SEER"], MainDeck = ["D1", "D2"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["MECH"] = new("MECH", cardNo: "SFD·065/221", power: 2, manaCost: 2, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard, "约德尔人", "机械"]),
                ["SEER"] = new("SEER", cardNo: "OGN·100/298", power: 3, manaCost: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
                ["D1"] = new("D1", cardNo: "UNL-007/219", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
                ["D2"] = new("D2", cardNo: "OGN·048/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["MECH"] = new("P1", "HAND"), ["SEER"] = new("P1", "BASE"), ["D1"] = new("P1", "MAIN_DECK"), ["D2"] = new("P1", "MAIN_DECK") });
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default, initial.Seed);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            await Submit("P1", new PlayCardCommand("MECH", "SFD·065/221", [], OptionalCosts: []));
            Check(current.TriggerQueue.Count == 2, "Each captured keyword must queue separately.");
            Check(current.TriggerQueue.Single(t => t.InsightContext!.PredictGrant!.Source.ObjectId == "MECH").InsightContext!.PredictGrant!.Source.Generation == 1, "Self grant must bind the entered incarnation.");
            await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: current.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
            for (var instance = 0; instance < 2; instance++) {
                for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                var choice = current.PendingCardChoice!;
                Check(choice.LegalObjectIds.SequenceEqual([instance == 0 ? "D1" : "D2"]), "A later instance must observe the current top.");
                await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, instance == 0 ? ["D1"] : []));
            }
            Check(current.StackItems.Count == 0 && current.PlayerZones["P1"].MainDeck.SequenceEqual(["D2", "D1"]), "Independent recycling/retaining result mismatch.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_GRANTED_PREDICT_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_GRANTED_PREDICT_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        async Task Submit(string actor, GameCommand command) {
            var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var result = await session.SubmitAsync(actor, "predict-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
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
