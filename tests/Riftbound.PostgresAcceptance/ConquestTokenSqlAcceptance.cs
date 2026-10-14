using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class ConquestTokenSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var card in new[] { "UNL-222/219", "SFD·069/221" })
            foreach (var reaction in new[] { false, true })
                await RunBranch(source, Path.Combine(output, (card.StartsWith("UNL") ? "primary" : "alternate") + (reaction ? "-reaction" : "-plain")), card, reaction);
    }
    private static async Task RunBranch(NpgsqlDataSource source, string output, string card, bool reaction)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-conquest-token-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var zones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Base = ["UNIT"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = ["BF"], Hand = reaction ? ["COUNTER"] : [] } };
        var cards = new Dictionary<string, CardObjectState> {
            ["UNIT"] = new("UNIT", cardNo: card, power: 2, damage: reaction ? 1 : 0, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
            ["BF"] = new("BF", cardNo: "OGN·296/298", ownerId: "P2", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) };
        var locations = new Dictionary<string, ObjectLocationState> { ["UNIT"] = new("P1", "BASE"), ["BF"] = new("P2", "BATTLEFIELD", "BF") };
        if (reaction) { cards["COUNTER"] = new("COUNTER", cardNo: "OGN·133/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]); locations["COUNTER"] = new("P2", "HAND"); }
        var initial = new MatchState(room, tick: 0, seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, activePlayerId: "P1", turnPlayerId: "P1", turnNumber: 5,
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = new(20,20) }, playerZones: zones, cardObjects: cards, objectLocations: locations);
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            await Submit("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await Submit("P1", new PassFocusCommand()); await Submit("P2", new PassFocusCommand());
            Check(GoldCount() == 0 && current.StackItems.Count == 1, "Conquest must not create Gold before responses.");
            await RejectTamperedTokenSource();
            if (reaction) {
                await Submit("P1", new PassPriorityCommand());
                await Submit("P2", new PlayCardCommand("COUNTER", "OGN·133/298", [], OptionalCosts: []));
                for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                Check(current.PlayerZones["P1"].Graveyard.Contains("UNIT") && GoldCount() == 0 && current.StackItems.Count == 1, "Real reaction removes source while its captured skill survives.");
            }
            for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            Check(current.StackItems.Count == 0 && current.TriggerQueue.Count == 0 && GoldCount() == 1, "Gold resolves once after both players pass.");
            Check(current.CardObjects[current.PlayerZones["P1"].Base.Single(id => current.CardObjects[id].Tags.Contains("金币"))].IsExhausted, "Gold must enter dormant.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_CONQUEST_TOKEN_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, tokenSourceTamperRejected = true, card, reaction, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_CONQUEST_TOKEN_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        int GoldCount() => current.CardObjects.Values.Count(c => c.Tags.Contains(CardObjectTags.EquipmentCard) && c.Tags.Contains("金币"));
        async Task RejectTamperedTokenSource() {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            var index = Array.FindIndex(current.StackItems.ToArray(), i => i.ConquestToken is not null);
            try {
                await using var forge = new NpgsqlCommand($"update state_snapshots set payload=jsonb_set(payload,'{{stackItems,{index},conquestToken,sourceGeneration}}','99'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
                var bad = await store.LoadAsync(room, default);
                Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("conquest token", StringComparison.Ordinal)), "Actual SQL forged captured conquest-token generation must fail independent recovery validation.");
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
            var result = await session.SubmitAsync(actor, "conquest-token-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
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
