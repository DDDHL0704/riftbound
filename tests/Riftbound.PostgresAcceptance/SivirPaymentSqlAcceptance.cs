using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class SivirPaymentSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var deferred in new[] { false, true })
            foreach (var pay in new[] { false, true })
                await RunBranch(source, Path.Combine(output, (deferred ? "deferred" : "inline") + (pay ? "-pay" : "-decline")), deferred, pay);
    }
    private static async Task RunBranch(NpgsqlDataSource source, string output, bool deferred, bool pay)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-sivir-payment-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var zones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { LegendZone = ["SIVIR"], Base = deferred ? ["RUNE", "UNIT"] : ["RUNE"], Hand = deferred ? [] : ["CARD"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = deferred ? ["BF"] : ["BF", "T"], Base = deferred ? ["ENEMY"] : [] } };
        var cards = new Dictionary<string, CardObjectState> {
            ["SIVIR"] = new("SIVIR", cardNo: "SFD·203/221", ownerId: "P1", controllerId: "P1"),
            ["RUNE"] = new("RUNE", cardNo: "UNL-R03", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:blue"]),
            ["BF"] = new("BF", cardNo: deferred ? "OGN·296/298" : "OGN·282/298", ownerId: "P2", controllerId: "P2", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) };
        cards["BF"] = cards["BF"] with { ControllerId = deferred ? null : "P2" };
        var locations = new Dictionary<string, ObjectLocationState> {
            ["SIVIR"] = new("P1", "LEGEND_ZONE"), ["RUNE"] = new("P1", "BASE"), ["BF"] = new("P2", "BATTLEFIELD", "BF") };
        if (deferred) {
            cards["UNIT"] = new("UNIT", cardNo: "SFD·116/221", power: 5, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
            cards["ENEMY"] = new("ENEMY", cardNo: "OGN·096/298", power: 20, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]);
            locations["UNIT"] = new("P1", "BASE"); locations["ENEMY"] = new("P2", "BASE");
        } else {
            cards["CARD"] = new("CARD", cardNo: "OGS·003/024", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
            cards["T"] = new("T", cardNo: "UNL-104/219", power: 5, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]);
            locations["CARD"] = new("P1", "HAND"); locations["T"] = new("P2", "BATTLEFIELD", "BF");
        }
        var initial = new MatchState(room, tick: 0, turnNumber: 1, activePlayerId: "P1",
            seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, seed: 424242,
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = deferred ? RunePool.Empty : new(2, 0), ["P2"] = RunePool.Empty },
            playerZones: zones, cardObjects: cards, objectLocations: locations);
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default, initial.Seed);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            if (deferred) {
                await Submit("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
                await Submit("P1", new PassFocusCommand()); await Submit("P2", new PassFocusCommand());
                var target = current.PendingCardChoice ?? throw new InvalidOperationException("Missing conquest target.");
                await Submit("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ENEMY"]));
                var parent = current.PendingPayment ?? throw new InvalidOperationException("Missing parent Ward payment.");
                await Submit("P1", new RecycleRuneCommand("RUNE"));
                Check(current.PendingPayment?.PaymentId == parent.PaymentId && current.PendingCardChoice is null && current.TriggerQueue.Count == 1, "Rune trigger must wait for parent payment.");
                await Submit("P1", new PayCostCommand(parent.PaymentId, parent.PaymentWindow, ["PAY"]));
            } else await Submit("P1", new PlayCardCommand("CARD", "OGS·003/024", ["T"], OptionalCosts: ["RECYCLE_RUNE:RUNE"]));
            var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Missing optional Sivir cost.");
            Check(choice.ChoiceWindow == "TRIGGER_COST_CONFIRMATION" && !current.CardObjects["SIVIR"].IsExhausted && current.CardObjects[deferred ? "ENEMY" : "T"].Damage == 0, "Payment must finish before optional cost and all response effects.");
            await RejectTamperedSivirSource();
            await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["SIVIR"] : []));
            if (pay) for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            Check(current.StackItems.Count == 0 && current.TriggerQueue.Count == 0 && current.CardObjects[deferred ? "ENEMY" : "T"].Damage == (deferred ? 5 : 2), "Original parent must still resolve after the optional gold trigger.");
            Check(current.CardObjects["SIVIR"].IsExhausted == pay && current.PlayerZones["P1"].Base.Count == (pay ? 1 : 0), "Optional gold count mismatch.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_SIVIR_PAYMENT_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, sivirSourceTamperRejected = true, deferred, pay, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_SIVIR_PAYMENT_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        async Task RejectTamperedSivirSource() {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            var index = Array.FindIndex(current.StackItems.ToArray(), i => i.SivirRuneRecycle is not null);
            try {
                await using var forge = new NpgsqlCommand($"update state_snapshots set payload=jsonb_set(payload,'{{stackItems,{index},sivirRuneRecycle,sourceGeneration}}','99'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
                var bad = await store.LoadAsync(room, default);
                Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("Sivir", StringComparison.Ordinal)), "Actual SQL forged captured Sivir generation must fail independent recovery validation.");
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
            var result = await session.SubmitAsync(actor, "sivir-payment-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
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
