using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class SivirOptionalSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var card in new[] { "SFD·203/221", "SFD·250/221" })
            foreach (var pay in new[] { false, true })
                await RunBranch(source, Path.Combine(output, (card.Contains("203") ? "primary" : "alternate") + (pay ? "-pay" : "-decline")), card, pay);
    }
    private static async Task RunBranch(NpgsqlDataSource source, string output, string card, bool pay)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-sivir-optional-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var initial = new MatchState(room, tick: 0, turnNumber: 1, activePlayerId: "P1",
            seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, seed: 424242,
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(10, 10), ["P2"] = new(10, 10) },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { LegendZone = ["SIVIR"], Base = ["RUNE"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["SIVIR"] = new("SIVIR", cardNo: card, ownerId: "P1", controllerId: "P1"),
                ["RUNE"] = new("RUNE", cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:blue"]) },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["SIVIR"] = new("P1", "LEGEND_ZONE"), ["RUNE"] = new("P1", "BASE") });
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default, initial.Seed);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            await Submit("P1", new RecycleRuneCommand("RUNE"));
            var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Missing optional cost window.");
            Check(choice.ChoiceWindow == "TRIGGER_COST_CONFIRMATION" && !current.CardObjects["SIVIR"].IsExhausted,
                "Rune recycling must not pay the optional exhaustion automatically.");
            Check(current.PlayerZones["P1"].Base.Count == 0 && current.PlayerZones["P1"].RuneDeck.Contains("RUNE"), "Rune movement mismatch.");
            await RejectTamperedSivirSource();
            await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["SIVIR"] : []));
            Check(current.CardObjects["SIVIR"].IsExhausted == pay && current.PlayerZones["P1"].Base.Count == 0,
                "Payment must happen before the response window and token creation.");
            if (pay) for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            Check(current.StackItems.Count == 0 && current.TriggerQueue.Count == 0 && current.PlayerZones["P1"].Base.Count == (pay ? 1 : 0), "Optional gold resolution mismatch.");
            if (pay) Check(current.CardObjects[current.PlayerZones["P1"].Base.Single()].IsExhausted, "Gold must enter dormant.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_SIVIR_OPTIONAL_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, sivirSourceTamperRejected = true, card, pay, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_SIVIR_OPTIONAL_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        async Task RejectTamperedSivirSource() {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            try {
                await using var forge = new NpgsqlCommand("update state_snapshots set payload=jsonb_set(payload,'{stackItems,0,sivirRuneRecycle,sourceGeneration}','99'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
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
            var result = await session.SubmitAsync(actor, "sivir-optional-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
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
