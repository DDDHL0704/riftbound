using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class InsightRepeatSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var branch in new[] { "once", "twice", "all", "burnout", "counter", "eclipse", "abandon" })
            await RunBranch(source, Path.Combine(output, branch), branch);
    }

    private static async Task RunBranch(NpgsqlDataSource source, string output, string branch)
    {
        Directory.CreateDirectory(output);
        var room = "insight-repeat-" + Guid.NewGuid().ToString("N");
        var echoes = branch is "twice" or "abandon" ? 2 : 1;
        var cardNo = branch == "eclipse" ? "UNL-063/219" : branch == "abandon" ? "UNL-131/219" : "VEN·056";
        var mana = branch == "eclipse" ? 3 : branch == "abandon" ? 2 : 7;
        var count = branch == "burnout" ? 3 : 12;
        var cards = new Dictionary<string, CardObjectState> {
            ["SPELL"] = new("SPELL", cardNo: cardNo, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["COUNTER"] = new("COUNTER", cardNo: "OGN·064/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) };
        foreach (var id in Enumerable.Range(1, count).Select(i => "D" + i))
            cards[id] = new(id, cardNo: "OGN·048/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        if (branch == "eclipse")
        {
            cards["U"] = new("U", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", power: 10, tags: [CardObjectTags.UnitCard]);
            cards["V"] = cards["U"] with { ObjectId = "V", Power = 9 };
        }
        if (branch == "abandon")
            foreach (var id in new[] { "TARGET1", "TARGET2" })
                cards[id] = new(id, cardNo: "OGN·048/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]);
        CardBehaviorRegistry.TryGetByCardNo("OGN·048/298", out var draw);
        IReadOnlyList<string> grants = [];
        for (var i = 0; i < echoes; i++) grants = EchoCostRules.AddBaseCostGrant(grants, "P1");
        var initial = new MatchState(room, 0, 1, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main,
            timingState: branch == "abandon" ? TimingStates.NeutralClosed : TimingStates.NeutralOpen,
            priorityPlayerId: branch == "abandon" ? "P1" : null,
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"], MainDeck = Enumerable.Range(1, count).Select(i => "D" + i).ToArray() },
                ["P2"] = PlayerZones.Empty with { Hand = ["COUNTER"], Base = branch == "eclipse" ? ["U", "V"] : [] } }, cardObjects: cards,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(40, 20), ["P2"] = new(40, 20) },
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = branch == "burnout" ? 7 : 0 }, untilEndOfTurnEffects: grants,
            stackItems: branch == "abandon" ? [new("S1", "P2", "TARGET1", draw.EffectKind, draw.CardNo),
                new("S2", "P2", "TARGET2", draw.EffectKind, draw.CardNo)] : []);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var submitted = 0; long sequence = 0; var rejects = 0;
        var checks = new List<object>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("repeat-" + player), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try
        {
            await Submit("P1", new PlayCardCommand("SPELL", cardNo, branch == "eclipse" ? ["U"] : branch == "abandon" ? ["S1"] : [],
                OptionalCosts: echoes == 1 ? ["ECHO"] : ["ECHO", "ECHO:GRANTED:2"],
                RepeatChoices: branch == "eclipse" ? [new("", ["V"])] : branch == "abandon" ? [new("", ["S2"]), new("", ["S1"])] : null));
            Check(current.RunePools["P1"].Mana == 40 - mana * (echoes + 1), "Every selected additional cost is paid once.");
            if (branch == "counter")
            {
                var parent = current.StackItems.Single().StackItemId;
                await Submit("P1", new PassPriorityCommand());
                await Submit("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [parent], OptionalCosts: []));
            }
            await Top();
            while (current.PendingCardChoice is { } choice)
            {
                Check(ids.Add(choice.ChoiceId), "Different executions require different choice identities.");
                foreach (var id in choice.ContextObjectIds)
                    Check(!JsonSerializer.Serialize(session.PromptFor("P2"), json).Contains("\"" + id + "\"", StringComparison.Ordinal), "Opponent prompt leaked a private deck identity.");
                var execution = current.StackItems.Last().CompletedDeckExecutions;
                Check(choice.DeckContext!.ExecutionIndex == execution && current.CardPlays.Resolved.Count == 0,
                    "Selection is bound to the current execution; source completion must wait.");
                if (choice.ChoiceWindow == "INSIGHT" && execution == 1)
                {
                    await RejectMutation("execution-index", "jsonb_set(payload,'{pendingCardChoice,deckContext,executionIndex}','0'::jsonb)");
                    await RejectMutation("completed-count", "jsonb_set(payload,'{stackItems,0,completedDeckExecutions}','2'::jsonb)");
                    await RejectMutation("earlier-choice", "jsonb_set(payload,'{pendingCardChoice,choiceId}',to_jsonb(replace(payload #>> '{pendingCardChoice,choiceId}',':1','')))");
                }
                await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    choice.ChoiceWindow == "INSIGHT_ORDER" ? choice.ContextObjectIds.Reverse().ToArray()
                    : branch == "all" && execution == 0 || branch == "abandon" && execution < 2 || branch == "eclipse" && execution == 1
                        ? choice.ContextObjectIds : []));
            }
            var expected = branch switch {
                "twice" => new[] { "D5", "D4", "D7", "D6", "D9", "D8" },
                "all" => ["D6", "D7", "D12", "D11"], "burnout" => ["D3", "D2", "D1"],
                "counter" or "eclipse" or "abandon" => [], _ => ["D5", "D4", "D7", "D6"] };
            Check(current.PlayerZones["P1"].Hand.SequenceEqual(expected) && current.StackItems.Count == 0,
                "Independent expected hand/order and final chain completion mismatch.");
            Check(current.CardPlays.Finalized.Count(p => p.Source.ObjectId == "SPELL") == 1
                && current.CardPlays.Resolved.Count(p => p.Source.ObjectId == "SPELL") == (branch == "counter" ? 0 : 1), "Repeats must not invent additional plays.");
            if (branch == "eclipse") Check(current.CardObjects["U"].Power == 6 && current.CardObjects["V"].Power == 5
                && current.PlayerZones["P1"].MainDeck.SequenceEqual(Enumerable.Range(2, 11).Select(i => "D" + i).Append("D1")), "Independent repeated unit targets and recycle order mismatch.");
            if (branch == "abandon") Check(current.PlayerZones["P2"].Hand.SequenceEqual(new[] { "COUNTER", "TARGET1", "TARGET2" })
                && current.PlayerZones["P1"].MainDeck.SequenceEqual(Enumerable.Range(3, 10).Select(i => "D" + i).Concat(["D1", "D2"])), "Each counter must use its own declared target; a later missing target still performs Insight.");
            Check(current.Status == (branch == "burnout" ? MatchStatuses.Finished : MatchStatuses.InProgress), "Winning burnout must stop subsequent execution.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_INSIGHT_ECHO_ORDER_DRAW_COLD_REPLAY_TAMPER_PASS", room, branch, echoes,
                submitted, events = sequence, coldRecoveries = checks.Count, structuralTamperRejections = rejects,
                checks, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Explicit repeated-Insight fixture; not visible UI, a normal opening, all targeted Insight consumers or whole-card certification."
            }, json));
            Console.WriteLine($"POSTGRES_INSIGHT_REPEAT_PASS: {branch}, {submitted} commands, {sequence} events, {checks.Count} cold replays, {rejects} structural rejects");
        }
        finally
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
            cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }

        async Task Top()
        { for (var i = 0; i < 2; i++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand()); }
        async Task Submit(string actor, GameCommand command)
        {
            var intent = "repeat-" + submitted; var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var result = await session.SubmitAsync(actor, intent, command, raw, default);
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual SQL match.");
            Check(frame.IsConsistent && frame.Commands.Count == submitted && frame.Events.Count == sequence
                && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), string.Join("; ", frame.ValidationErrors));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, current, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors)); session = MatchSession.Restore(frame, engine, journal, players);
            Check((await session.SubmitAsync(actor, intent, command, raw, default)).Accepted, "Cold duplicate must remain idempotent.");
            Check((await store.LoadAsync(room, default))!.Commands.Count == submitted, "Duplicate appended SQL history.");
            checks.Add(new { current.Tick, sequence, execution = current.PendingCardChoice?.DeckContext?.ExecutionIndex, replay.IsMatch });
        }
        async Task RejectMutation(string name, string expression)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            try
            {
                await using var forge = new NpgsqlCommand("update state_snapshots set payload=" + expression + " where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); Check(await forge.ExecuteNonQueryAsync() == 1, "Change exactly the latest explicit fixture snapshot.");
                var bad = await store.LoadAsync(room, default);
                Check(bad is { IsConsistent: false } && bad.ValidationErrors.Any(e => e.Contains("Insight", StringComparison.OrdinalIgnoreCase)), name + ": " + string.Join("; ", bad?.ValidationErrors ?? [])); rejects++;
            }
            finally
            {
                await using var restore = new NpgsqlCommand("update state_snapshots set payload=@saved::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                restore.Parameters.AddWithValue("room", room); restore.Parameters.AddWithValue("saved", saved); await restore.ExecuteNonQueryAsync();
            }
            var clean = await store.LoadAsync(room, default);
            Check(clean?.IsConsistent == true && MatchStateHasher.Hash(clean.AuthoritativeState!) == MatchStateHasher.Hash(current), "Original SQL state must recover after mutation.");
        }
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
