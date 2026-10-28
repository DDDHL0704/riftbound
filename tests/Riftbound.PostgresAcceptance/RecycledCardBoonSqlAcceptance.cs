using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class RecycledCardBoonSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var card in new[] { "OGN·235/298", "SFD·237/221", "SFD·237*/221" })
            foreach (var hook in new[] { false, true })
                await RunBranch(source, Path.Combine(output, (card.StartsWith("OGN") ? "primary" : card.Contains("*") ? "promo" : "alternate") + (hook ? "-hook" : "-predict")), card, hook);
    }
    private static async Task RunBranch(NpgsqlDataSource source, string output, string card, bool hook)
    {
        Directory.CreateDirectory(output);
        var room = "postgres-recycle-boon-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var zones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Base = hook ? ["GEAR", "HOST", "KARMA"] : [], Hand = hook ? [] : ["MECH"], MainDeck = ["TOP", "REST"] },
            ["P2"] = PlayerZones.Empty };
        var cards = new Dictionary<string, CardObjectState> {
            ["TOP"] = new("TOP", cardNo: hook ? "OGN·096/298" : "OGN·004/298", power: 1, manaCost: 2, ownerId: "P1", controllerId: "P1", tags: hook ? [CardObjectTags.UnitCard] : [CardObjectTags.SpellCard]),
            ["REST"] = new("REST", cardNo: "SFD·042/221", manaCost: 2, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) };
        var locations = new Dictionary<string, ObjectLocationState> { ["TOP"] = new("P1", "MAIN_DECK"), ["REST"] = new("P1", "MAIN_DECK") };
        var sourceId = hook ? "KARMA" : "MECH";
        cards[sourceId] = new(sourceId, cardNo: card, power: 6, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
        locations[sourceId] = new("P1", hook ? "BASE" : "HAND");
        if (hook) {
            cards["GEAR"] = new("GEAR", cardNo: "OGN·242/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]);
            cards["HOST"] = new("HOST", cardNo: "OGN·096/298", power: 1, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
            locations["GEAR"] = new("P1", "BASE"); locations["HOST"] = new("P1", "BASE");
        }
        var initial = new MatchState(room, tick: 0, seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, activePlayerId: "P1", turnPlayerId: "P1", turnNumber: 5,
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20,20), ["P2"] = RunePool.Empty }, playerZones: zones, cardObjects: cards, objectLocations: locations);
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try {
            if (hook) {
                var ability = P4ActivatedAbilityCatalog.GetAll().Single(a => a.SourceCardNo == "OGN·242/298" && a.Kind == ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest);
                await Submit("P1", new ActivateAbilityCommand("GEAR", ability.AbilityId, ["HOST"], OptionalCosts: []));
                for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                var deck = current.PendingCardChoice!;
                await Submit("P1", new ChooseCardsCommand(deck.ChoiceId, deck.ChoiceWindow, ["TOP"]));
                Check(current.PendingEffectPlay is not null && current.TriggerQueue.Count == 2, "Both earned triggers must wait for the original permanent confirmation.");
                Check(current.TriggerQueue.Count(t => t.SourceObjectId == "KARMA" && t.FieldContext?.Kind == "RECYCLE_BOON") == 1,
                    "Exactly one recycled-card Boon trigger must retain the original source.");
                Check(current.TriggerQueue.Count(t => t.SourceObjectId == "HOST" && t.LastBreathDraw is not null) == 1,
                    "The destroyed host's separately earned draw trigger must also be retained.");
                await Submit("P1", new PlayCardCommand("TOP", "OGN·096/298", [], OptionalCosts: []));
                Check(current.PlayerZones["P1"].Base.Contains("TOP"), "New permanent must enter before boon target confirmation.");
            } else {
                await Submit("P1", new PlayCardCommand("MECH", card, [], OptionalCosts: []));
                for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                var deck = current.PendingCardChoice!;
                await Submit("P1", new ChooseCardsCommand(deck.ChoiceId, deck.ChoiceWindow, ["TOP"]));
            }
            var target = hook ? "TOP" : "MECH"; var choice = current.PendingCardChoice!;
            Check(choice.ChoiceWindow == "TRIGGER_CONFIRMATION" && choice.LegalObjectIds.Contains(target) && !current.CardObjects[target].Tags.Contains(CardObjectTags.Boon), "Target selection must precede separate boon responses.");
            await RejectTamperedBoonSource();
            await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
            for (var pass = 0; pass < 2; pass++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            if (hook) {
                Check(current.CardObjects[target].Tags.Contains(CardObjectTags.Boon), "Boon must resolve before the host's earned draw.");
                Check(current.StackItems.Count == 1 && current.StackItems[0].SourceObjectId == "HOST" && current.StackItems[0].LastBreathDraw is not null,
                    "The host's earned draw must remain on the stack after Boon resolves.");
                var handBeforeDraw = current.PlayerZones["P1"].Hand.ToArray();
                var deckBeforeDraw = current.PlayerZones["P1"].MainDeck.ToArray();
                Check(deckBeforeDraw.SequenceEqual(new[] { "REST" }), "The real recycled card must remain available for the earned draw.");
                await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                Check(current.PlayerZones["P1"].Hand.SequenceEqual(handBeforeDraw) && current.PlayerZones["P1"].MainDeck.SequenceEqual(deckBeforeDraw),
                    "The first pass must not resolve the earned draw.");
                await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                Check(current.PlayerZones["P1"].Hand.SequenceEqual(handBeforeDraw.Append("REST")) && current.PlayerZones["P1"].MainDeck.Count == 0
                    && current.ObjectLocations["REST"].Zone == "HAND", "The second pass must draw the original recycled card exactly once.");
            }
            Check(current.StackItems.Count == 0 && current.TriggerQueue.Count == 0 && current.CardObjects[target].Tags.Contains(CardObjectTags.Boon), "Boon must resolve once after both players pass.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_RECYCLED_CARD_BOON_COLD_AND_EXPLICIT_INITIAL_REPLAY_PASS", room, submitted, events = sequence,
                coldRecoveries = checks.Count, boonSourceTamperRejected = true, card, hook, checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Isolated explicit rules fixture with actual SQL commands/events/states/views and retained initial-state replay." }, json));
            Console.WriteLine($"POSTGRES_RECYCLED_CARD_BOON_PASS: {submitted} commands, {sequence} events, {checks.Count} cold recoveries and explicit-initial action/event replays");
        } finally {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
        async Task RejectTamperedBoonSource() {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            var index = Array.FindIndex(current.StackItems.ToArray(), i => i.FieldContext?.Kind == "RECYCLE_BOON");
            try {
                await using var forge = new NpgsqlCommand($"update state_snapshots set payload=jsonb_set(payload,'{{stackItems,{index},fieldContext,sourceGeneration}}','99'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
                var bad = await store.LoadAsync(room, default);
                Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("field trigger", StringComparison.Ordinal)), "Actual SQL forged captured recycling-boon generation must fail independent recovery validation.");
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
            var result = await session.SubmitAsync(actor, "recycle-boon-sql-" + submitted, command, JsonSerializer.SerializeToElement(fields), default);
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL frame.");
            if (hook) await File.WriteAllTextAsync(Path.Combine(output, $"prefix-{submitted:D2}-recovered-sql-frame.json"), JsonSerializer.Serialize(frame, json));
            Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            Check(frame.Commands.Count == submitted && frame.Events.Count == sequence && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "SQL history/state mismatch.");
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(frame, engine, journal, players); checks.Add(new { current.Tick, sequence, stack = current.StackItems.Count, replay.IsMatch });
        }
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
