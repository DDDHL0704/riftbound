using System.IO.Compression;
using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class ClairvoyanceSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var branch in new[] { "keep", "some", "all", "burnout", "control" })
            await RunBranch(source, Path.Combine(output, branch), branch);
    }

    private static async Task RunBranch(NpgsqlDataSource source, string output, string branch)
    {
        Directory.CreateDirectory(output);
        var room = "clairvoyance-" + Guid.NewGuid().ToString("N");
        var initial = Initial(room, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var submitted = 0; long sequence = 0; var checks = new List<object>(); var tamper = 0;
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("clair-" + player), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try
        {
            await Submit("P1", new PlayCardCommand("SPELL", "VEN·056", [], OptionalCosts: []));
            if (branch == "control")
            {
                var parent = current.StackItems.Single().StackItemId;
                await Submit("P1", new PassPriorityCommand());
                await Submit("P2", new PlayCardCommand("REVERSAL", "OGN·080/298", [parent], OptionalCosts: []));
                for (var i = 0; i < 2; i++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                Check(current.PendingRuleChoice is null && current.StackItems.Single().ControllerId == "P2"
                    && current.StackItems.Single().TargetObjectIds.Count == 0,
                    "An untargeted spell has no new targets to choose; control must finish without an empty decision.");
            }
            for (var i = 0; i < 2; i++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            var actor = branch == "control" ? "P2" : "P1";
            var choice = current.PendingCardChoice!;
            Check(choice.PlayerId == actor && choice.MaxCount == (branch == "control" ? 2 : branch == "burnout" ? 1 : 5), "Counted Insight/controller mismatch.");
            Check(current.PlayerZones[actor].Hand.Count == 0, "No card may be drawn before completing Insight.");
            foreach (var id in choice.ContextObjectIds)
                Check(!JsonSerializer.Serialize(session.PromptFor(actor == "P1" ? "P2" : "P1"), json).Contains("\"" + id + "\"", StringComparison.Ordinal), "Opponent prompt leaked a private card.");
            await RejectMutation("wrong-count", "jsonb_set(payload,'{pendingCardChoice,maxCount}','" + (choice.MaxCount + 1) + "'::jsonb)");
            await RejectMutation("wrong-player", "jsonb_set(payload,'{pendingCardChoice,playerId}','\"" + (actor == "P1" ? "P2" : "P1") + "\"'::jsonb)");
            await RejectMutation("already-completed", "jsonb_set(payload,'{stackItems,0,insightCompleted}','true'::jsonb)");
            await Submit(actor, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                branch == "all" ? ["D1", "D2", "D3", "D4", "D5"] : branch == "some" ? ["D2", "D4"] : []));
            if (current.PendingCardChoice is { } order)
            {
                Check(current.PlayerZones[actor].Hand.Count == 0, "Ordering is part of Insight; draw must still wait.");
                await Submit(actor, new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow,
                    branch == "control" ? ["E2", "E1"] : branch == "keep" ? ["D5", "D4", "D3", "D2", "D1"] : ["D5", "D3", "D1"]));
            }
            var expected = branch switch { "keep" => new[] { "D5", "D4" }, "some" => ["D5", "D3"], "all" => ["D6", "D7"], "control" => ["E2", "E1"], _ => ["D1"] };
            Check(current.PendingCardChoice is null && current.StackItems.Count == 0 && current.PlayerZones[actor].Hand.SequenceEqual(expected), "Both draws must follow the selected top order.");
            Check(current.Status == (branch == "burnout" ? MatchStatuses.Finished : MatchStatuses.InProgress), "Post-Insight draw burnout/winner mismatch.");
            Check(current.CardPlays.Resolved.Single(e => e.Source.ObjectId == "SPELL").PlayerId == actor
                && current.CardPlays.Finalized.Single(e => e.Source.ObjectId == "SPELL").PlayerId == "P1", "History must preserve finalizer versus resolver.");
            var original = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Original successful SQL frame required before fixture cleanup.");
            await using (var file = new FileStream(Path.Combine(output, "complete-original-frame.json.gz"), FileMode.CreateNew))
            await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize))
                await JsonSerializer.SerializeAsync(zip, original, json);
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_CLAIRVOYANCE_PRIVATE_ORDER_DRAW_COLD_REPLAY_TAMPER_PASS", room, branch,
                submitted, events = sequence, coldRecoveries = checks.Count, structuralTamperRejections = tamper, checks,
                matchStatus = current.Status, current.WinnerPlayerId, drawn = current.PlayerZones[actor].Hand,
                finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Explicit rules fixture: actual SQL/private projections/command and event replay, counted Insight before post-choice draw, owner/controller and winning burnout. Not a normal full opening or whole-card certification."
            }, json));
            Console.WriteLine($"POSTGRES_CLAIRVOYANCE_PASS: {branch}, {submitted} commands, {sequence} events, {checks.Count} cold replays, {tamper} structural rejects");
        }
        finally
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
            cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }

        async Task Submit(string actor, GameCommand command)
        {
            var intent = "clair-" + submitted; var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var result = await session.SubmitAsync(actor, intent, command, raw, default);
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual SQL match.");
            Check(frame.IsConsistent && frame.Commands.Count == submitted && frame.Events.Count == sequence
                && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "SQL state/log mismatch: " + string.Join("; ", frame.ValidationErrors));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, current, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(frame, engine, journal, players);
            Check((await session.SubmitAsync(actor, intent, command, raw, default)).Accepted, "Cold duplicate must be idempotent.");
            Check((await store.LoadAsync(room, default))!.Commands.Count == submitted, "Duplicate must not append SQL rows.");
            checks.Add(new { current.Tick, sequence, hand = current.PlayerZones[actor].Hand.Count, pending = current.PendingCardChoice?.ChoiceWindow, replay.IsMatch });
        }
        async Task RejectMutation(string name, string expression)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            try
            {
                await using var forge = new NpgsqlCommand("update state_snapshots set payload=" + expression + " where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); Check(await forge.ExecuteNonQueryAsync() == 1, "Only one latest fixture snapshot is changed.");
                var bad = await store.LoadAsync(room, default);
                Check(bad is { IsConsistent: false } && bad.ValidationErrors.Any(e => e.Contains("Insight", StringComparison.OrdinalIgnoreCase)), name + ": " + string.Join("; ", bad?.ValidationErrors ?? [])); tamper++;
            }
            finally
            {
                await using var restore = new NpgsqlCommand("update state_snapshots set payload=@saved::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                restore.Parameters.AddWithValue("room", room); restore.Parameters.AddWithValue("saved", saved); await restore.ExecuteNonQueryAsync();
            }
            var clean = await store.LoadAsync(room, default);
            Check(clean?.IsConsistent == true && MatchStateHasher.Hash(clean.AuthoritativeState!) == MatchStateHasher.Hash(current), "Exact original snapshot must recover after mutation.");
        }
    }

    private static MatchState Initial(string room, string branch)
    {
        var count = branch == "burnout" ? 1 : 7;
        var cards = new Dictionary<string, CardObjectState> {
            ["SPELL"] = new("SPELL", cardNo: "VEN·056", manaCost: 7, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) };
        foreach (var id in Enumerable.Range(1, count).Select(i => "D" + i)) cards[id] = new(id, cardNo: "OGN·048/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        foreach (var id in new[] { "E1", "E2" }) cards[id] = new(id, cardNo: "OGN·048/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]);
        if (branch == "control") cards["REVERSAL"] = new("REVERSAL", cardNo: "OGN·080/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]);
        return new(room, 0, 1, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"], MainDeck = Enumerable.Range(1, count).Select(i => "D" + i).ToArray() },
                ["P2"] = PlayerZones.Empty with { Hand = branch == "control" ? ["REVERSAL"] : [], MainDeck = ["E1", "E2"] } },
            cardObjects: cards, runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) },
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = branch == "burnout" ? 7 : 0 });
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
