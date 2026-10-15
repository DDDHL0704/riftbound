using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class PatchedPorobotSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var branch in new[] { "below", "threshold", "gold", "return" })
            await RunBranch(source, Path.Combine(output, branch), branch);
    }

    private static async Task RunBranch(NpgsqlDataSource source, string output, string branch)
    {
        Directory.CreateDirectory(output);
        var room = "porobot-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var initial = Initial(room, branch);
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("audit-" + player), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try
        {
            await Submit("P1", new PlayCardCommand("UNIT", "VEN·058", [], OptionalCosts: []));
            Check(current.CardObjects["UNIT"].IsExhausted && current.CardObjects["UNIT"].Power == 2
                && current.CardObjects["UNIT"].Tags.Contains(CardObjectTags.EquipmentCard), "Real dual-type unit entry must be exhausted.");
            if (branch == "below") Check(current.StackItems.Count == 0, "Two other equipment cannot meet the threshold.");
            else
            {
                Check(current.StackItems.Single().SourcePlayDraw?.OtherEquipment.Count == 3, "Expected exactly three captured other equipment.");
                await RejectMutation("missing-context", "payload #- '{stackItems,0,sourcePlayDraw}'", "source play draw");
                await RejectMutation("missing-witness", "jsonb_set(payload, '{stackItems,0,sourcePlayDraw,otherEquipment}', (payload #> '{stackItems,0,sourcePlayDraw,otherEquipment}') - 2)", "source play draw");
                await RejectMutation("wrong-printing", "jsonb_set(payload, '{stackItems,0,sourcePlayDraw,cardNo}', to_jsonb('OGN·096/298'::text))", "source play draw");
                if (branch == "gold")
                {
                    await Submit("P1", new ActivateAbilityCommand("E2", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, [], OptionalCosts: []));
                    Check(!current.CardObjects.ContainsKey("E2"), "Actual counted Gold must cease after its resource skill.");
                }
                if (branch == "return")
                {
                    await Submit("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"], OptionalCosts: []));
                    for (var i = 0; i < 2; i++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                    Check(current.PlayerZones["P1"].Hand.Contains("UNIT") && current.StackItems.Single().SourcePlayDraw is not null,
                        "The real source return must leave its captured ability independent.");
                }
                for (var i = 0; i < 2; i++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                Check(current.PlayerZones["P1"].Hand.Contains("D0"), "Captured skill must draw the original top card.");
            }
            Check(current.StackItems.Count == 0 && current.CardPlays.Finalized.Single(e => e.Source.ObjectId == "UNIT").Types == 3
                && current.CardPlays.Resolved.Single(e => e.Source.ObjectId == "UNIT").Types == 3, "One play must retain both types.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_POROBOT_CONDITIONAL_DRAW_COLD_AND_FULL_REPLAY_PASS", branch, room, submitted,
                events = sequence, coldRecoveries = checks.Count, structuralTamperRejections = branch == "below" ? 0 : 3,
                finalized = current.CardPlays.Finalized, resolved = current.CardPlays.Resolved, checks,
                finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Explicit official dual-type fixture with actual SQL, original-initial full replay, source return and consumed counted Gold. Whole-history causal forgery and whole-card certification remain separate."
            }, json));
            Console.WriteLine($"POSTGRES_POROBOT_PASS: {branch}, {submitted} commands, {sequence} events, {checks.Count} cold replays");
        }
        finally
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
            cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }

        async Task Submit(string actor, GameCommand command)
        {
            var id = "porobot-" + submitted; var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var result = await session.SubmitAsync(actor, id, command, raw, default);
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
            Check((await session.SubmitAsync(actor, id, command, raw, default)).Accepted, "Immediate duplicate must be idempotent.");
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL frame.");
            Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            Check(frame.Commands.Count == submitted && frame.Events.Count == sequence
                && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "SQL history/state mismatch.");
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(frame, engine, journal, players);
            Check((await session.SubmitAsync(actor, id, command, raw, default)).Accepted, "Cold duplicate must remain idempotent.");
            var duplicate = await store.LoadAsync(room, default);
            Check(duplicate?.Commands.Count == submitted && duplicate.Events.Count == sequence, "Duplicates must not append SQL rows.");
            checks.Add(new { current.Tick, sequence, finalized = current.CardPlays.Finalized.Count,
                resolved = current.CardPlays.Resolved.Count, replay.IsMatch });
        }

        async Task RejectMutation(string name, string expression, string diagnostic)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            try
            {
                await using var forge = new NpgsqlCommand("update state_snapshots set payload=" + expression
                    + " where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
                var bad = await store.LoadAsync(room, default);
                Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains(diagnostic, StringComparison.OrdinalIgnoreCase)),
                    name + ": " + string.Join("; ", bad?.ValidationErrors ?? []));
            }
            finally
            {
                await using var restore = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                restore.Parameters.AddWithValue("room", room); restore.Parameters.AddWithValue("payload", saved); await restore.ExecuteNonQueryAsync();
            }
            Check((await store.LoadAsync(room, default))?.IsConsistent == true, "Restored SQL snapshot must be consistent.");
        }
    }

    private static MatchState Initial(string room, string branch)
    {
        var equipment = branch == "below" ? 2 : 3;
        var own = Enumerable.Range(0, equipment).Select(i => "E" + i).ToArray();
        var hand = branch == "return" ? new[] { "UNIT", "RETURN" } : new[] { "UNIT" };
        var zones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Hand = hand, Base = own, MainDeck = ["D0", "D1", "D2"] },
            ["P2"] = PlayerZones.Empty with { MainDeck = ["O0", "O1"] } };
        var cards = new Dictionary<string, CardObjectState> {
            ["UNIT"] = new("UNIT", cardNo: "VEN·058", power: 2, manaCost: 2, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.UnitCard, CardObjectTags.EquipmentCard, "机械", "魄罗"]) };
        foreach (var id in own) cards[id] = new(id, cardNo: "OGN·072/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]);
        if (branch == "gold") {
            Check(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory), "Official Gold factory required.");
            cards["E2"] = factory.CreateObject("E2", "P1", "P1") with { IsExhausted = false };
        }
        if (branch == "return") cards["RETURN"] = new("RETURN", cardNo: "OGN·104/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        foreach (var (player, z) in zones)
            foreach (var id in z.MainDeck) cards[id] = new(id, cardNo: "OGN·048/298", ownerId: player, controllerId: player, tags: [CardObjectTags.SpellCard]);
        return new MatchState(room, 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) });
    }
    private static void Check(bool value, string? message) { if (!value) throw new InvalidOperationException(message); }
}
