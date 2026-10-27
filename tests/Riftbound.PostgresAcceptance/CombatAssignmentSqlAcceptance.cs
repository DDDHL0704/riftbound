using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class CombatAssignmentSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var branch in new[] { "multiple-defenders", "choose-first-attacker", "choose-second-attacker", "one-on-one" })
            await RunBranch(source, Path.Combine(output, branch), branch);
        foreach (var branch in new[] { "declared-source-second", "vayne-secondary-pay", "vayne-secondary-decline", "vayne-secondary-dies" })
            await RunBranch(source, Path.Combine(output, branch), branch);
        foreach (var branch in new[] { "natural-source-first", "natural-source-second" })
            await RunBranch(source, Path.Combine(output, branch), branch);
    }

    private static async Task RunBranch(NpgsqlDataSource source, string output, string branch)
    {
        Directory.CreateDirectory(output);
        var room = "combat-assignment-" + Guid.NewGuid().ToString("N");
        var multipleDefenders = branch == "multiple-defenders";
        var natural = branch.StartsWith("natural-source-", StringComparison.Ordinal);
        var conquestContinuation = natural || branch == "declared-source-second" || branch.StartsWith("vayne-secondary-", StringComparison.Ordinal);
        var vayne = branch.StartsWith("vayne-secondary-", StringComparison.Ordinal);
        var vayneDies = branch == "vayne-secondary-dies";
        var attackers = branch is "declared-source-second" or "natural-source-second" ? new[] { "A2", "A1" }
            : natural || branch.StartsWith("choose-", StringComparison.Ordinal) || vayne ? new[] { "A1", "A2" } : ["A1"];
        var defenders = multipleDefenders ? new[] { "D1", "D2" } : ["D1"];
        var cards = new Dictionary<string, CardObjectState> {
            ["F"] = new("F", cardNo: "OGN·275/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) };
        foreach (var id in attackers.Concat(defenders))
        {
            var owner = id.StartsWith('A') ? "P1" : "P2";
            cards[id] = new(id, cardNo: "SFD·125/221", ownerId: owner, controllerId: owner,
                power: multipleDefenders && id == "A1" ? 4 : 2, tags: [CardObjectTags.UnitCard]);
        }
        if (conquestContinuation)
            cards["D1"] = cards["D1"] with { Power = vayneDies ? 5 : 1 };
        if (vayne)
            cards["A2"] = cards["A2"] with { CardNo = "OGN·035/298", Tags = [CardObjectTags.UnitCard, "强攻3"] };
        if (conquestContinuation) cards["F"] = cards["F"] with { ControllerId = "P2" };
        if (natural) cards["F"] = cards["F"] with { OwnerId = "P2", ControllerId = "P2" };
        var initial = new MatchState(room, 0, 4, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = natural ? PlayerZones.Empty with { Base = attackers } : PlayerZones.Empty with { Battlefields = new[] { "F" }.Concat(attackers).ToArray() },
                ["P2"] = PlayerZones.Empty with { Battlefields = natural ? new[] { "F" }.Concat(defenders).ToArray() : defenders } }, cardObjects: cards,
            objectLocations: cards.ToDictionary(e => e.Key, e => natural && e.Key.StartsWith('A')
                ? new ObjectLocationState("P1", "BASE") : new ObjectLocationState(e.Value.OwnerId!, "BATTLEFIELD", "F")),
            runePools: new Dictionary<string, RunePool> { ["P1"] = conquestContinuation ? new(1, 0) : RunePool.Empty, ["P2"] = RunePool.Empty },
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            untilEndOfTurnEffects: natural ? [] : [BattlefieldTaskMarkers.SpellDuelCompleted("F")]);
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var submitted = 0; long sequence = 0;
        var checkpoints = new List<object>(); var mutations = new List<object>();
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("combat-" + player), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, Json));
        try
        {
            if (natural)
            {
                await Submit("P1", new MoveUnitCommand("A1", "BASE", "BATTLEFIELD:F", [], ["A1", "A2"]));
                await Submit("P1", new PassFocusCommand());
                await Submit("P2", new PassFocusCommand());
            }
            var declared = await Submit("P1", new DeclareBattleCommand("F", attackers, defenders, ["COMBAT_ASSIGNMENT"]));
            if (branch == "one-on-one")
            {
                Check(!current.BattleState.IsActive && !declared.Events.Any(e => e.Kind == "BATTLE_DAMAGE_ASSIGNMENT_OPENED"),
                    "One recipient on each side must retain automatic resolution.");
                Check(current.PlayerZones["P1"].Graveyard.SequenceEqual(["A1"])
                    && current.PlayerZones["P2"].Graveyard.SequenceEqual(["D1"]), "Both equal-power units must die.");
            }
            else
            {
                Check(current.BattleState.IsActive && session.PromptFor("P1").Actionable && !session.PromptFor("P2").Actionable,
                    "Attacker chooses first, before either side deals damage.");
                Check(!declared.Events.Any(e => e.Kind == "DAMAGE_APPLIED"), "Opening assignments cannot deal damage.");
                var outgoing = multipleDefenders ? new CombatDamageAssignmentDto[] { new("A1", "D1", 2), new("A1", "D2", 2) }
                    : [new("A1", "D1", 2), new("A2", "D1", vayne ? 5 : 2)];
                var first = await Submit("P1", new AssignCombatDamageCommand("battle:F", "F", outgoing));
                Check(session.PromptFor("P2").Actionable && !session.PromptFor("P1").Actionable
                    && !first.Events.Any(e => e.Kind == "DAMAGE_APPLIED"), "Defender chooses independently before simultaneous damage.");
                var chosen = branch == "choose-second-attacker" || vayneDies ? "A2" : "A1";
                var incoming = defenders.Select(id => new CombatDamageAssignmentDto(id, chosen,
                    conquestContinuation ? vayneDies ? 5 : 1 : 2)).ToArray();
                var settled = await Submit("P2", new AssignCombatDamageCommand("battle:F", "F", incoming));
                Check(!current.BattleState.IsActive && defenders.All(id => current.PlayerZones["P2"].Graveyard.Contains(id)),
                    "All defenders receive lethal damage and battle closes.");
                if (conquestContinuation)
                {
                    Check(current.PlayerZones["P1"].Battlefields.Contains("A1"), "The other attacking unit survives.");
                    if (branch == "declared-source-second")
                        Check(Equals(settled.Events.Single(e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["sourceObjectId"], "A2"),
                            "Actual SQL continuation must retain the accepted declaration source.");
                    if (natural)
                        Check(Equals(settled.Events.Single(e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["sourceObjectId"], attackers[0]),
                            "Natural movement context cannot replace the actual accepted combat declaration order.");
                    if (vayne && !vayneDies)
                    {
                        var payment = current.PendingPayment ?? throw new InvalidOperationException("Surviving secondary Vayne lost conquest payment.");
                        Check(payment.ManaCost == 1 && current.RunePools["P1"].Mana == 1,
                            "Vayne cannot return or pay before the explicit decision.");
                        var decline = branch == "vayne-secondary-decline";
                        await Submit("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
                            [decline ? "DECLINE" : "SPEND_MANA:1"]));
                        Check(current.PendingPayment is null && current.RunePools["P1"].Mana == (decline ? 1 : 0)
                            && (decline ? current.PlayerZones["P1"].Battlefields : current.PlayerZones["P1"].Hand).Contains("A2"),
                            "Independent payment decision must change only the eligible Vayne and its actual mana.");
                    }
                    if (vayneDies)
                        Check(current.PlayerZones["P1"].Graveyard.Contains("A2") && current.PendingPayment is null,
                            "Vayne killed by simultaneous damage cannot trigger conquest.");
                    await RejectEventMutation("BATTLEFIELD_CONQUERED", "conquest-source");
                }
                else
                {
                    Check(current.PlayerZones["P1"].Graveyard.Contains(chosen), "The defender's selected attacker must die.");
                    if (!multipleDefenders)
                        Check(current.PlayerZones["P1"].Battlefields.Contains(chosen == "A1" ? "A2" : "A1"),
                            "The other attacker must survive; target choice changes the actual survivor identity.");
                }
                foreach (var kind in new[] { "COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED", "COMBAT_DAMAGE_ASSIGNED" })
                    foreach (var mutation in new[] { "damage", "source-case", "member-case", "array-order" })
                        await RejectEventMutation(kind, mutation);
            }
            var final = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing final SQL frame.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_COMBAT_ASSIGNMENT_COLD_REPLAY_TAMPER_PASS", room, branch, submitted, events = sequence,
                coldRecoveries = checkpoints.Count, checkpoints, mutations, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                assignments = final.Events.Where(e => e.Event.Kind is "COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED" or "COMBAT_DAMAGE_ASSIGNED"),
                scope = "Explicit combat fixture with actual SQL JSONB, strict event replay, both independent choices and duplicates; not a normal opening or visible paired product certification."
            }, Json));
            Console.WriteLine($"POSTGRES_COMBAT_ASSIGNMENT_PASS: {branch}, {submitted} commands, {sequence} events, {checkpoints.Count} cold replays, {mutations.Count} event tamper rejections");
        }
        finally
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
            cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }

        async Task<ResolutionResult> Submit(string actor, GameCommand command)
        {
            var intent = "combat-" + submitted; var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), Json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var result = await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(raw), raw, default);
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual SQL match.");
            await VerifyClean(frame);
            session = MatchSession.Restore(frame, engine, journal, players);
            foreach (var player in initial.Seats.Keys)
                Check(session.SnapshotFor(player).Tick == current.Tick && session.PromptFor(player).SnapshotTick == current.Tick, "Both cold player views must match.");
            Check((await session.SubmitAsync(actor, intent, command, raw, default)).Accepted, "Cold duplicate must remain idempotent.");
            var duplicate = (await store.LoadAsync(room, default))!;
            Check(duplicate.Commands.Count == submitted && duplicate.Events.Count == sequence, "Duplicate appended SQL history.");
            checkpoints.Add(new { current.Tick, commands = submitted, events = sequence, replayMatched = true });
            return result;
        }
        async Task VerifyClean(MatchRecoveryFrame frame)
        {
            Check(frame.IsConsistent && frame.Commands.Count == submitted && frame.Events.Count == sequence
                && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), string.Join("; ", frame.ValidationErrors));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, current, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        async Task RejectEventMutation(string kind, string mutation)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select event_sequence,payload::text from game_events where match_id=@room and event_type=@kind order by event_sequence limit 1", connection);
            read.Parameters.AddWithValue("room", room); read.Parameters.AddWithValue("kind", kind);
            long eventSequence; string saved;
            await using (var reader = await read.ExecuteReaderAsync())
            { Check(await reader.ReadAsync(), "Required persisted assignment event missing."); eventSequence = reader.GetInt64(0); saved = reader.GetString(1); }
            var node = JsonNode.Parse(saved)!;
            if (mutation == "conquest-source")
                node["payload"]!["sourceObjectId"] = node["payload"]!["sourceObjectId"]!.GetValue<string>() == "A1" ? "A2" : "A1";
            else
            {
                var list = node["payload"]!["assignments"]!.AsArray(); var first = list[0]!.AsObject();
                if (mutation == "damage") first["damage"] = 99;
                if (mutation == "source-case") first["sourceObjectId"] = first["sourceObjectId"]!.GetValue<string>().ToLowerInvariant();
                if (mutation == "member-case") { first["Damage"] = first["damage"]!.DeepClone(); first.Remove("damage"); }
                if (mutation == "array-order")
                {
                    Check(list.Count > 1, "Array-order mutation requires independent entries.");
                    node["payload"]!["assignments"] = new JsonArray(list.Reverse().Select(value => value!.DeepClone()).ToArray());
                }
            }
            try
            {
                await WriteEvent(node.ToJsonString());
                var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing forged SQL fixture.");
                Check(MatchStateHasher.Hash(bad.AuthoritativeState!) == MatchStateHasher.Hash(current), "Event tamper must not change expected state.");
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, bad.Commands, current, engine, default, bad.Events);
                Check(!replay.IsMatch && replay.Errors.Any(e => e.Contains("payload hash", StringComparison.Ordinal)), "Actual JSONB event tamper must fail replay.");
                mutations.Add(new { kind, mutation, eventSequence, rejected = true, replay.Errors });
            }
            finally { await WriteEvent(saved); }
            await VerifyClean((await store.LoadAsync(room, default))!);

            async Task WriteEvent(string payload)
            {
                await using var write = new NpgsqlCommand("update game_events set payload=@saved::jsonb where match_id=@room and event_sequence=@sequence", connection);
                write.Parameters.AddWithValue("saved", payload); write.Parameters.AddWithValue("room", room); write.Parameters.AddWithValue("sequence", eventSequence);
                Check(await write.ExecuteNonQueryAsync() == 1, "Mutate exactly one event belonging to this disposable explicit fixture.");
            }
        }
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
