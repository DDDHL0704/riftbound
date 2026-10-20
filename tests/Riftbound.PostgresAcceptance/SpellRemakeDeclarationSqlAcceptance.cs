using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class SpellRemakeDeclarationSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        if (Directory.Exists(output)) throw new InvalidOperationException("Never overwrite an original declaration-scope SQL run.");
        Directory.CreateDirectory(output);
        foreach (var mirror in new[] { false, true })
        foreach (var branch in new[] { "draw", "mode", "zero", "echo", "no-complete" })
            await RunBranch(source, Path.Combine(output, (mirror ? "mirror-" : "reversal-") + branch), mirror, branch);
    }

    private static async Task RunBranch(NpgsqlDataSource source, string output, bool mirror, string branch)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var room = "declaration-scope-" + Guid.NewGuid().ToString("N");
        var noComplete = branch == "no-complete";
        var card = noComplete ? "OGN·046/298" : branch == "draw" ? "OGN·083/298" : branch == "mode" ? "UNL-182/219" : "SFD·080/221";
        var initial = Initial(room, card, mirror, noComplete);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, player,
                ReconnectTokenHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))), default);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var state = initial; var commands = 0; var cold = 0; var rejected = 0;
        await Submit("P1", new PlayCardCommand("SPELL", card, noComplete ? ["UNIT-1"] : [], branch == "mode" ? "DRAW_1" : "", branch == "echo" ? ["ECHO"] : []));
        var parent = state.StackItems.Single().StackItemId;
        var casterPaid = state.RunePools["P1"];
        await Submit("P1", new PassPriorityCommand());
        await Submit("P2", new PlayCardCommand("CONTROL", mirror ? "VEN·152" : "OGN·080/298", [parent]));
        await Top();
        if (mirror)
        {
            Check(state.PendingRuleChoice?.Request is { PlayerId: "P2", Kind: "OPTIONAL_SPELL_CONTROL_PAYMENT" }, "Original optional control payment remains required.");
            await Choose("PAY:ANY");
        }
        var targetChoice = branch is "zero" or "echo";
        if (targetChoice || mirror && branch == "mode")
        {
            var request = state.PendingRuleChoice?.Request ?? throw new InvalidOperationException("A permitted declaration must remain available.");
            Check(request.PlayerId == "P2" && request.Kind == "SPELL_CHOICE_REMAKE", "Only the actual new controller chooses.");
            await Save("before-decision");
            await Reject("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
            await Reject("P2", new PayCostCommand(request.Id + "-stale", "RULE_REPLACEMENT", ["KEEP_ALL"]));
            await Reject("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["FORGED"]));
            if (targetChoice)
            {
                Check(request.Preview!.StackItems.Single(i => i.SourceObjectId == "SPELL").TargetObjectIds.Count == 0, "The original cast really selected zero.");
                var option = request.Options.Single(o => o.Label.Contains(branch == "echo" ? "第 2 次" : "第 1 次", StringComparison.Ordinal)
                    && o.Label.Contains("添加目标 1 为", StringComparison.Ordinal) && o.ObjectIds?.Contains("UNIT-1") == true);
                await Choose(option.Id); await Save("draft"); await Choose("CONFIRM");
            }
            else
            {
                Check(request.Options.Any(o => o.Label.Contains("模式 ", StringComparison.Ordinal)), "Mirror retains its broader mode permission.");
                await Choose("KEEP_ALL");
            }
        }
        else
        {
            Check(state.PendingRuleChoice is null, "No impossible or intrinsically empty remake decision may interrupt control.");
            await Reject("P2", new PayCostCommand("empty-remake", "RULE_REPLACEMENT", ["CONFIRM"]));
        }
        var controlled = state.StackItems.Single();
        Check(controlled.ControllerId == "P2" && state.PlayerZones["P2"].Graveyard.Contains("CONTROL"), "Control finishes before the controlled spell resolves.");
        Check(state.RunePools["P2"].Mana == (mirror ? 29 : 26) && state.RunePools["P2"].TotalPower == (mirror ? 28 : 27), "Only the original control costs are paid.");
        if (branch == "echo")
            Check(controlled.TargetObjectIds.Count == 0 && controlled.RepeatExecutions is { Count: 2 } repeats
                && repeats[0].TargetObjectIds.Count == 0 && repeats[1].TargetObjectIds.SequenceEqual(new[] { "UNIT-1" }), "Zero targets in the first execution do not suppress the second execution.");
        else Check(controlled.TargetObjectIds.SequenceEqual(targetChoice || noComplete ? new[] { "UNIT-1" } : []), "The explicit permitted declaration is retained.");
        var controllerPaid = state.RunePools["P2"];
        await Save("after-control"); await Top();
        var draws = branch == "draw" ? 2 : branch == "mode" ? 1 : 0;
        Check(state.StackItems.Count == 0 && state.PendingRuleChoice is null && state.PendingCardChoice is null
            && state.Status == MatchStatuses.InProgress, "The controlled source completes without an artificial choice or burnout.");
        if (noComplete)
            Check(state.PlayerZones["P2"].Hand.SequenceEqual(new[] { "UNIT-2" })
                && state.PlayerZones["P1"].MainDeck.SequenceEqual(initial.PlayerZones["P1"].MainDeck),
                "A unit in hand cannot make a complete friendly-unit declaration; the original target stays unchanged.");
        else
            Check(state.PlayerZones["P2"].Hand.SequenceEqual(initial.PlayerZones["P2"].MainDeck.Take(draws))
                && state.PlayerZones["P1"].MainDeck.SequenceEqual(initial.PlayerZones["P1"].MainDeck), "Only the resolving controller draws from their own deck.");
        Check(state.CardObjects["UNIT-1"].Damage == (targetChoice ? 1 : 0) && state.CardObjects["UNIT-2"].Damage == 0
            && state.CardObjects["UNIT-1"].Power == 9 && state.CardObjects["UNIT-2"].Power == 9, "Literal damage marks and stored Might remain distinct.");
        Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(state.RunePools["P1"]), JsonSerializer.SerializeToNode(casterPaid))
            && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(state.RunePools["P2"]), JsonSerializer.SerializeToNode(controllerPaid)),
            "Neither original costs nor Echo costs are paid again.");
        Check(state.PlayerZones["P1"].Graveyard.Contains("SPELL") && !state.PlayerZones["P2"].Graveyard.Contains("SPELL")
            && state.CardPlays.Finalized.Single(p => p.Source.ObjectId == "SPELL").PlayerId == "P1"
            && state.CardPlays.Resolved.Single(p => p.Source.ObjectId == "SPELL").PlayerId == "P2", "Physical owner, finalizer and resolver stay distinct.");
        var final = await Save("final");
        Check(final.Events.Count(e => e.Event.Kind == "SPELL_CHOICES_REMADE") == (targetChoice ? 1 : 0), "No fictitious remake event for an empty declaration or KEEP.");
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_DECLARATION_SCOPE_COLD_DEDUP_REPLAY_AND_ATOMIC_REJECTION_PASS", room, mirror, branch, card,
            commands, events = final.Events.Count, coldRecoveries = cold, atomicRejections = rejected, actualRuleInputs = state.RuleInputs,
            originalInitial = "explicit-fixture-initial.json", originalBlankMetadataNotRewritten = true,
            allOwnFixtureRowsRetainedForIndependentReadback = true, completeGameOrWholePrintingCertification = false
        }, json));
        Console.WriteLine($"POSTGRES_DECLARATION_SCOPE_PASS: {mirror}/{branch}, {commands} commands, {final.Events.Count} events, {cold} cold restores, {rejected} rejects");

        async Task Top() { await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
        async Task Choose(string option)
        {
            var request = state.PendingRuleChoice!.Request;
            await Submit("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        async Task Submit(string actor, GameCommand command)
        {
            var intent = "declaration-" + commands;
            var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(fields, json);
            var expected = await engine.ResolveAsync(state, new(intent, actor, command.CmdType), command, default);
            var accepted = await session.SubmitAsync(actor, intent, command, raw, default);
            Check(accepted.Accepted && expected.Accepted, accepted.ErrorMessage);
            Check(MatchStateHasher.Hash(accepted.State) == MatchStateHasher.Hash(expected.State)
                && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(accepted.Events), JsonSerializer.SerializeToNode(expected.Events)), "Original next state and events match across cold restoration.");
            state = accepted.State; commands++;
            var before = await Digest(); var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("A real SQL frame is required.");
            Check(frame.IsConsistent && frame.Commands.Count == commands && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(state), "Original SQL state and command history agree.");
            foreach (var viewer in initial.Seats.Keys)
            {
                var other = viewer == "P1" ? "P2" : "P1";
                var view = JsonSerializer.Serialize(session.SnapshotFor(viewer), json) + JsonSerializer.Serialize(session.PromptFor(viewer), json);
                foreach (var id in state.PlayerZones[other].MainDeck.Concat(state.PlayerZones[other].Hand))
                    Check(!view.Contains("\"" + id + "\"", StringComparison.Ordinal), "Opponent hand or unrevealed deck identity leaked.");
            }
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, state, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors));
            var restored = MatchSession.Restore(frame, engine, journal, players);
            foreach (var player in initial.Seats.Keys)
                Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))), "Both original private views and prompts are exact after cold restoration.");
            session = restored; cold++;
            Check((await session.SubmitAsync(actor, intent, command, raw, default)).Accepted && await Digest() == before,
                "Cold duplicate does not change any of the seven original tables.");
        }
        async Task Reject(string actor, GameCommand command)
        {
            var before = await Digest(); var result = await engine.ResolveAsync(state, new("invalid-declaration", actor, command.CmdType), command, default);
            Check(!result.Accepted && result.Events.Count == 0 && MatchStateHasher.Hash(result.State) == MatchStateHasher.Hash(state)
                && await Digest() == before, "Invalid actor, old choice or forged decision is atomic."); rejected++;
        }
        async Task<MatchRecoveryFrame> Save(string phase)
        {
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Original complete SQL frame required.");
            await using var file = new FileStream(Path.Combine(output, phase + "-original-frame.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, frame, json); return frame;
        }
        async Task<string> Digest()
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var connection = await source.OpenConnectionAsync();
            foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
            {
                digest.AppendData(Encoding.UTF8.GetBytes(table + "\n"));
                await using var command = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection);
                command.Parameters.AddWithValue("room", room); await using var rows = await command.ExecuteReaderAsync();
                while (await rows.ReadAsync()) digest.AppendData(Encoding.UTF8.GetBytes(rows.GetString(0) + "\n"));
            }
            return Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
        }
    }

    private static MatchState Initial(string room, string spell, bool mirror, bool noFriendly = false)
    {
        var cards = new Dictionary<string, CardObjectState> {
            ["SPELL"] = new("SPELL", cardNo: spell, manaCost: spell is "SFD·080/221" or "OGN·046/298" ? 1 : 4, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["CONTROL"] = new("CONTROL", cardNo: mirror ? "VEN·152" : "OGN·080/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]),
            ["UNIT-1"] = new("UNIT-1", power: 9, cardNo: "OGN·001/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
            ["UNIT-2"] = new("UNIT-2", power: 9, cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) };
        foreach (var player in new[] { "P1", "P2" })
        foreach (var index in Enumerable.Range(0, 8))
        { var id = player + "-DECK-" + index; cards[id] = new(id, cardNo: "OGN·048/298", ownerId: player, controllerId: player, tags: [CardObjectTags.SpellCard]); }
        return new(room, 0, 1, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen, cardObjects: cards,
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"], Base = ["UNIT-1"], MainDeck = Enumerable.Range(0, 8).Select(i => "P1-DECK-" + i).ToArray() },
                ["P2"] = PlayerZones.Empty with { Hand = noFriendly ? ["CONTROL", "UNIT-2"] : ["CONTROL"], Base = noFriendly ? [] : ["UNIT-2"], MainDeck = Enumerable.Range(0, 8).Select(i => "P2-DECK-" + i).ToArray() } },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) });
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
