using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class AttachedRecycleLifecycleSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var card in new[] { "OGN·235/298", "SFD·237/221", "SFD·237*/221" })
            foreach (var reverse in new[] { false, true })
                await RunBranch(source, Path.Combine(output, card.StartsWith("OGN") ? "primary" : card.Contains("*") ? "promo" : "alternate", reverse ? "reverse" : "forward"), card, reverse);
    }
    private static async Task RunBranch(NpgsqlDataSource source, string output, string card, bool reverse)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-attached-recycle-lifecycle-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var zones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Base = ["ALLY", "SONG"], Hand = ["C", "LOOK", "LEAVE"], MainDeck = ["D1", "D2"] },
            ["P2"] = PlayerZones.Empty with { Base = ["TARGET"] } };
        var cards = new Dictionary<string, CardObjectState> {
            ["SONG"] = new("SONG", cardNo: "SFD·059/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]),
            ["TARGET"] = new("TARGET", cardNo: card, power: 6, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
            ["ALLY"] = new("ALLY", cardNo: "OGN·096/298", power: 1, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
            ["C"] = new("C", cardNo: "UNL-200/219", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["LOOK"] = new("LOOK", cardNo: "UNL-063/219", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["LEAVE"] = new("LEAVE", cardNo: "OGN·104/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["D1"] = new("D1", cardNo: "OGN·004/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["D2"] = new("D2", cardNo: "OGN·004/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) };
        var locations = zones.SelectMany(p => new[] { ("BASE", p.Value.Base), ("HAND", p.Value.Hand), ("MAIN_DECK", p.Value.MainDeck) }
            .SelectMany(z => z.Item2.Select(id => new KeyValuePair<string, ObjectLocationState>(id, new(p.Key, z.Item1))))).ToDictionary(p => p.Key, p => p.Value);
        var initial = new MatchState(room, tick: 0, seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, activePlayerId: "P1", turnPlayerId: "P1", turnNumber: 5,
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20,20), ["P2"] = RunePool.Empty }, playerZones: zones, cardObjects: cards, objectLocations: locations);
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0; var tamperRejects = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            await Submit("P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"], OptionalCosts: []));
            for (var pass = 0; pass < 4; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            Check(current.CardObjects["C-TOKEN-001"].CardNo == card, "Actual image must copy the official face.");
            await Submit("P1", new AssembleEquipmentCommand("SONG", "C-TOKEN-001", ["ASSEMBLE_GREEN"]));
            for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit("P1", new PlayCardCommand("LOOK", "UNL-063/219", ["TARGET"], OptionalCosts: []));
            for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            var insight = current.PendingCardChoice!;
            await Submit("P1", new ChooseCardsCommand(insight.ChoiceId, insight.ChoiceWindow, ["D1"]));
            Check(current.TriggerQueue.Count == 2, "Original and attached instance must queue separately.");
            var ordered = current.TriggerQueue.Select(t => t.TriggerId).ToArray(); if (reverse) Array.Reverse(ordered);
            await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: ordered));
            for (var targetIndex = 0; targetIndex < 2; targetIndex++) {
                var target = current.PendingCardChoice!;
                await Submit("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ALLY"]));
            }
            await Submit("P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"], OptionalCosts: []));
            for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            Check(!current.CardObjects.ContainsKey("C-TOKEN-001") && current.StackItems.Count == 2
                && current.StackItems.All(item => item.FieldContext!.SourceCeasedToExist), "Returned image must cease while both captured skills survive.");
            await RejectTamperedBoonSource();
            for (var pass = 0; pass < 4; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            Check(current.StackItems.Count == 0 && current.CardObjects["ALLY"].Tags.Contains(CardObjectTags.Boon), "Surviving captured skill must grant ally its boon.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_ATTACHED_RECYCLE_COLD_AND_FULL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, boonSourceTamperRejected = true, tamperRejects, card, reverse, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_ATTACHED_RECYCLE_LIFECYCLE_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        async Task RejectTamperedBoonSource() {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            var index = Array.FindIndex(current.StackItems.ToArray(), i => i.FieldContext?.Origin is not null);
            var mutations = new List<(string Name, string Expression, string? Face)> {
                ("generation", $"jsonb_set(payload,'{{stackItems,{index},fieldContext,recycledBoon,ceasedSource,source,generation}}','99'::jsonb)", null) };
            foreach (var face in new[] { "OGN·235/298", "SFD·237/221", "SFD·237*/221" }.Where(face => face != card))
                mutations.Add(("consistent-printing-substitution", $"jsonb_set(jsonb_set(payload,'{{stackItems,{index},cardNo}}',to_jsonb(@face::text)),'{{stackItems,{index},fieldContext,cardNo}}',to_jsonb(@face::text))", face));
            foreach (var field in new[] { "cardNo", "faceRevision", "capturedTick" })
                mutations.Add(("missing-" + field, $"payload #- '{{stackItems,{index},fieldContext,recycledBoon,ceasedSource,{field}}}'", null));
            foreach (var field in new[] { "revision", "capturedTick" })
                mutations.Add(("forged-origin-" + field, $"jsonb_set(payload,'{{stackItems,{index},fieldContext,origin,{field}}}','99999'::jsonb)", null));
            foreach (var mutation in mutations) {
                try {
                    await using var forge = new NpgsqlCommand($"update state_snapshots set payload={mutation.Expression} where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                    forge.Parameters.AddWithValue("room", room);
                    if (mutation.Face is not null) forge.Parameters.AddWithValue("face", mutation.Face);
                    await forge.ExecuteNonQueryAsync();
                    var bad = await store.LoadAsync(room, default);
                    Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("field trigger", StringComparison.Ordinal)), "Actual SQL captured source mutation must fail independent recovery: " + mutation.Name);
                    tamperRejects++;
                } finally {
                    await using var restore = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                    restore.Parameters.AddWithValue("payload", saved); restore.Parameters.AddWithValue("room", room); await restore.ExecuteNonQueryAsync();
                }
                var recovered = await store.LoadAsync(room, default); Check(recovered?.IsConsistent == true, "SQL restoration after mutation must be consistent.");
            }
        }
        async Task Submit(string actor, GameCommand command) {
            var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var result = await session.SubmitAsync(actor, "recycle-boon-lifecycle-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
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
