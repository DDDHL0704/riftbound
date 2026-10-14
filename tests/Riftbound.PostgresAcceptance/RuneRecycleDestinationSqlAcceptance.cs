using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class RuneRecycleDestinationSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var runeCount in new[] { 1, 2 })
            await RunBranch(source, Path.Combine(output, runeCount == 1 ? "single" : "multiple"), runeCount);
    }
    private static async Task RunBranch(NpgsqlDataSource source, string output, int runeCount)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-rune-destination-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var initial = new MatchState(room, tick: 0, turnNumber: 1, activePlayerId: "P1",
            seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, seed: 424242,
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(10, 10), ["P2"] = new(10, 10) },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { LegendZone = ["SIVIR"], Base = ["RUNE"], RuneDeck = ["BOTTOM"], MainDeck = ["D1"], Battlefields = ["THUNDER", "ATTACKER"] }, ["P2"] = PlayerZones.Empty with { Battlefields = ["DEFENDER"] } },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["SIVIR"] = new("SIVIR", cardNo: "SFD·203/221", ownerId: "P1", controllerId: "P1"),
                ["RUNE"] = new("RUNE", cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1", isExhausted: true, tags: [CardObjectTags.RuneCard, "COLOR:blue"]),
                ["BOTTOM"] = new("BOTTOM", cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:blue"]),
                ["D1"] = new("D1", cardNo: "UNL-007/219", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
                ["THUNDER"] = new("THUNDER", cardNo: "OGN·287/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                ["ATTACKER"] = new("ATTACKER", cardNo: "SFD·125/221", power: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
                ["DEFENDER"] = new("DEFENDER", cardNo: "SFD·125/221", power: 1, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["SIVIR"] = new("P1", "LEGEND_ZONE"), ["RUNE"] = new("P1", "BASE"), ["BOTTOM"] = new("P1", "RUNE_DECK"), ["D1"] = new("P1", "MAIN_DECK"),
                ["THUNDER"] = new("P1", "BATTLEFIELD"), ["ATTACKER"] = new("P1", "BATTLEFIELD", "THUNDER"), ["DEFENDER"] = new("P2", "BATTLEFIELD", "THUNDER") })
            with { UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("THUNDER")] };
        if (runeCount == 2) initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = ["RUNE", "RUNE2"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["RUNE2"] = initial.CardObjects["RUNE"] with { ObjectId = "RUNE2" } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["RUNE2"] = new("P1", "BASE") } };
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            await Submit("P1", new DeclareBattleCommand("THUNDER", ["ATTACKER"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"], BattlefieldTargetObjectIds: []));
            Check(current.PlayerZones["P1"].MainDeck.SequenceEqual(["D1"]) && current.PlayerZones["P1"].RuneDeck.SequenceEqual(["BOTTOM", "RUNE"]), "Preserve main deck and return rune to rune-deck bottom.");
            Check(current.ObjectLocations["RUNE"].Zone == "RUNE_DECK" && !current.CardObjects["RUNE"].IsExhausted, "Returned rune location/readiness mismatch.");
            Check(current.RunePools["P1"] == initial.RunePools["P1"], "Effect recycling must not create resource power.");
            var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Missing independent Sivir optional cost.");
            await RejectTamperedRuneDestination();
            await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            Check(current.StackItems.Count == 0 && current.TriggerQueue.Count == 0 && !current.CardObjects["SIVIR"].IsExhausted, "Declining observer must preserve the completed rune return.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_RUNE_DESTINATION_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, runeDestinationTamperRejected = true, runeCount, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_RUNE_DESTINATION_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        async Task RejectTamperedRuneDestination() {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            try {
                await using var forge = new NpgsqlCommand("update state_snapshots set payload=jsonb_set(payload,'{objectLocations,RUNE,zone}','\"MAIN_DECK\"'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
                var bad = await store.LoadAsync(room, default);
                Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("RUNE", StringComparison.Ordinal)), "Actual SQL forged recycled rune location must fail independent recovery validation.");
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
            var result = await session.SubmitAsync(actor, "rune-destination-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
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
