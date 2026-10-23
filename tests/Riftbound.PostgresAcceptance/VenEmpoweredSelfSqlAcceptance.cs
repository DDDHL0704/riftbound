using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class VenEmpoweredSelfSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Cards = ["VEN·043", "VEN·050", "VEN·070"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output); var results = new List<object>();
        foreach (var no in Cards)
        foreach (var boundary in new[] { "permanent", "return-after", "return-before", "revealed-return" }) {
            var dir = Path.Combine(output, no[4..] + "-" + boundary); Directory.CreateDirectory(dir);
            var context = await Context.Create(source, no); await CheckQuote(context.Session, context.State, no);
            await ExerciseAsync(no, boundary, context.Act, () => context.State, async label => {
                if (label == "private-pending") {
                    foreach (var kind in new[] { "source-view", "null-stack", "wrong-input", "paid-mana" })
                        await RejectTamper(source, context.Room, dir, kind);
                }
                await context.Cold(label);
            });
            var final = await context.Cold("final"); await SaveOriginal(dir, final, "final");
            results.Add(new { cardNo = no, boundary, room = context.Room, commands = final.Commands.Count, events = final.Events.Count,
                finalHash = MatchStateHasher.Hash(context.State), ruleInputs = context.State.RuleInputs,
                originalReplayMatched = true, coldPrefixChecks = context.Checkpoints, completeGameOrCardCertification = false });
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_N9_EMPOWER_SOURCE_BOUNDARIES_PASS", results,
            fixtureScope = "Fresh current-rule logged QA setups from SEATING; subsequent play, Empower, return, death and hand reveal are real commands."
        }, Json));
    }

    // The local memory probe executes this same recipe; that probe is not a SQL pass.
    internal static async Task ExerciseAsync(string no, string boundary, Func<string, GameCommand, bool, Task> act,
        Func<MatchState> state, Func<string, Task> checkpoint)
    {
        var actor = Actor(state()); var peer = Peer(state());
        await act(peer, new PlayCardCommand("N9-UNIT", no, [], Destination: "BASE"), false);
        await act(actor, new PlayCardCommand("N9-UNIT", no, [], Destination: "BASE"), true);
        var generation = state().CardObjects["N9-UNIT"].ObjectGeneration;
        await act(peer, new ActivateAbilityCommand("N9-UNIT", "EMPOWER:0:0", []), false);
        await act(actor, new ActivateAbilityCommand("N9-UNIT", "EMPOWER:0:0", []), true);
        CheckPending(state(), no, "VISIBLE");
        CheckPayment(state(), no); await checkpoint("public-pending");
        if (boundary == "permanent") {
            await Top(); CheckSelf(state(), no);
            await act(actor, new ActivateAbilityCommand("N9-UNIT", "EMPOWER:0:0", []), false);
            await act(actor, new EndTurnCommand(), true); CheckSelf(state(), no); await checkpoint("permanent-after-turn"); return;
        }
        if (boundary == "return-after") { await Top(); CheckSelf(state(), no); }
        await act(actor, new PlayCardCommand("N9-RETURN", "OGN·104/298", ["N9-UNIT"]), true); await Top();
        CheckReturned(state(), generation);
        if (boundary == "return-after") { await checkpoint("returned-after-resolution"); return; }
        CheckPending(state(), no, "HIDDEN"); CheckPayment(state(), no, returned: true); await checkpoint("private-pending");
        if (boundary == "revealed-return") {
            if (state().PriorityPlayerId == actor) await act(actor, new PassPriorityCommand(), true);
            await act(peer, new PlayCardCommand("N9-REMOVE", "SFD·163/221", ["N9-CRAB", "N9-ENEMY"]), true);
            await Top(); await Top();
            var choice = state().PendingCardChoice;
            Check(choice is { ChoiceWindow: "REVEALED_HAND_EFFECT" } && choice.PlayerId == peer
                && choice.HandContext?.Cards.GetValueOrDefault("N9-UNIT") == state().CardObjects["N9-UNIT"].ObjectGeneration,
                "Actual crab death must reveal the current returned hand incarnation.");
            CheckPending(state(), no, "VISIBLE"); await checkpoint("revealed-pending");
            await act(peer, new ChooseCardsCommand(choice!.ChoiceId, choice.ChoiceWindow, []), true);
            CheckPending(state(), no, "HIDDEN"); await checkpoint("reveal-ended-pending");
        }
        await Top(); CheckReturned(state(), generation); Check(state().StackItems.Count == 0, "The old Empower skill must finish once without empowering the new hand object.");
        await checkpoint("old-incarnation-finished");
        async Task Top() { await act(state().PriorityPlayerId!, new PassPriorityCommand(), true); await act(state().PriorityPlayerId!, new PassPriorityCommand(), true); }
    }

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output); var cases = new List<object>();
        foreach (var no in Cards)
        foreach (var prefix in new[] { "public", "private", "revealed" }) {
            var key = no[4..] + "-" + prefix; var dir = Path.Combine(output, key); Directory.CreateDirectory(dir);
            var context = await Context.Create(source, no); await CheckQuote(context.Session, context.State, no);
            var frame = await context.Cold("network-prepared"); await SaveOriginal(dir, frame, "prepared");
            cases.Add(new { key, cardNo = no, prefix, room = context.Room, actor = context.Actor, peer = context.Peer,
                tokens = context.Tokens, apiKeys = context.ApiKeys, beforeTick = context.State.Tick,
                beforeHash = MatchStateHasher.Hash(context.State), ruleInputs = context.State.RuleInputs,
                hiddenDecks = context.State.Seats.Keys.ToDictionary(p => p, _ => context.State.PlayerZones.Values
                    .SelectMany(z => z.MainDeck.Concat(z.RuneDeck)).ToArray()),
                privateHands = context.State.PlayerZones.ToDictionary(p => p.Key, p => p.Value.Hand.ToArray()) });
        }
        await File.WriteAllTextAsync(Path.Combine(output, "ven-empowered-self-network-fixtures.json"), JsonSerializer.Serialize(new {
            schemaVersion = 1, cases, scope = "API executes real unit play, Empower, return and crab-death reveal; restart freezes each distinct public/private/revealed source prefix."
        }, Json));
    }

    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        Check(stage is "before-restart" or "after-restart", "Use a declared actual API restart stage.");
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "ven-empowered-self-network-fixtures.json")));
        var results = new List<object>();
        foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = item.GetProperty("room").GetString()!; var no = item.GetProperty("cardNo").GetString()!;
            var prefix = item.GetProperty("prefix").GetString()!; var dir = Path.Combine(output, item.GetProperty("key").GetString()!);
            var frame = await LoadVerified(source, room); var state = frame.AuthoritativeState!;
            CheckBirth(frame, item.GetProperty("actor").GetString()!, item.GetProperty("peer").GetString()!);
            CheckPending(state, no, prefix == "private" ? "HIDDEN" : "VISIBLE"); CheckPayment(state, no, prefix != "public"); CheckPrivacy(state);
            if (prefix != "public") CheckReturned(state, state.StackItems.Single().EmpowerActivation!.Source.Generation);
            Check((state.PendingCardChoice?.ChoiceWindow == "REVEALED_HAND_EFFECT") == (prefix == "revealed"), "Only the actual crab prefix may keep a real revealed-hand choice.");
            using var resume = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(dir, "network-resume.json")));
            var expected = resume.RootElement.GetProperty("acceptedIntents").EnumerateArray().Select(x => x.GetString()).ToArray();
            Check(frame.Commands.Where(c => c.Accepted).Select(c => c.ClientIntentId).SequenceEqual(new[] { "room-seat:P1", "room-seat:P2", "n9-setup" }.Concat(expected)),
                "Durable accepted commands must exactly match the original API actions, excluding duplicates.");
            Check(frame.Commands.Count(c => !c.Accepted) == 1 && frame.Commands.Single(c => !c.Accepted).ClientIntentId == "n9-network-stale"
                && !string.IsNullOrWhiteSpace(frame.Commands.Single(c => !c.Accepted).ErrorMessage),
                "Retain the actual stale prompt rejection in the SQL journal.");
            var sqlSample = await VenEmpoweredSelfSqlAudit.VerifyNativeCheckpointAsync(source, room, dir, stage, Json);
            var receipt = new { room, cardNo = no, prefix, stage, commands = frame.Commands.Count, events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(state), sevenTableDigest = sqlSample.SevenTableDigest, sqlSample,
                originalReplayMatched = true, ruleInputs = state.RuleInputs };
            await File.WriteAllTextAsync(Path.Combine(dir, "sql-" + stage + ".json"), JsonSerializer.Serialize(receipt, Json));
            await SaveOriginal(dir, frame, stage); results.Add(receipt);
        }
        await File.WriteAllTextAsync(Path.Combine(output, "ven-empowered-self-sql-" + stage + ".json"), JsonSerializer.Serialize(new {
            status = "REAL_API_N9_EMPOWER_SOURCE_DURABILITY_PASS", stage, results, completeGameOrCardCertification = false
        }, Json));
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "ven-empowered-self-network-fixtures.json")));
        foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
            if (item.GetProperty("prefix").GetString() != "private") continue;
            var room = item.GetProperty("room").GetString()!; var dir = Path.Combine(output, item.GetProperty("key").GetString()!);
            foreach (var kind in new[] { "source-view", "null-stack", "wrong-input", "paid-mana" }) await RejectTamper(source, room, dir, kind);
        }
        await VerifyNetworkAsync(source, output, "after-restart");
    }

    private static async Task CheckQuote(IMatchSession session, MatchState state, string no)
    {
        var actor = Actor(state); var prompt = session.PromptFor(actor);
        var quote = await session.PreviewPlayCardAsync(actor, new("n9-quote", prompt.PromptId!, state.Tick,
            new PlayCardCommand("N9-UNIT", no, [], Destination: "BASE")), default);
        Check(quote.CanPay && quote.Cost?.Mana == PrintedMana(no)
            && quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Orange) == (no == "VEN·070" ? 1 : 0),
            "Actual unit-play quote must retain each printing's separate mana/orange cost.");
    }

    internal static void CheckPending(MatchState state, string no, string visibility)
    {
        var item = state.StackItems.Single(s => s.EmpowerActivation is not null); var activation = item.EmpowerActivation!;
        Check(item.SourceObjectId == "N9-UNIT" && item.CardNo == no && item.TargetObjectIds.Count == 0
            && activation.Source.Generation == 1 && !state.CardObjects["N9-UNIT"].IsEmpowered,
            "Pending untargeted Empower must retain its original public generation-one source and delay the status.");
        var expectedMana = no == "VEN·070" ? 3 : 7;
        Check(activation.PaidMana == expectedMana && activation.ControlledRunes == (no == "VEN·050" ? 5 : 0),
            "Activation must capture the actual five-rune reduction only for the bear.");
        foreach (var viewer in state.Seats.Keys.Append("")) {
            var snapshot = viewer == "" ? ResolutionResult.BuildSpectatorSnapshot(state) : ResolutionResult.BuildSnapshots(state)[viewer];
            var row = JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("stack").EnumerateArray()
                .Single(s => s.GetProperty("effectKind").GetString() == "EMPOWER_ACTIVATION");
            var hidden = visibility == "HIDDEN" && viewer != Actor(state);
            Check(row.GetProperty("sourceObjectId").GetString() == (hidden ? "HIDDEN" : "N9-UNIT")
                && row.GetProperty("sourceVisibility").GetString() == (hidden ? "HIDDEN" : "VISIBLE")
                && row.GetProperty("cardNo").GetString() == no && !row.TryGetProperty("empowerActivation", out _),
                "Public Empower rows must protect the current private object identity while retaining the printed source card/effect.");
        }
    }

    private static void CheckPayment(MatchState state, string no, bool returned = false)
        => Check(state.RunePools[Actor(state)].Mana == 40 - PrintedMana(no) - (no == "VEN·070" ? 3 : 7) - (returned ? 1 : 0)
            && state.RunePools[Actor(state)].PowerByTrait.GetValueOrDefault(RuneTrait.Orange) == (no == "VEN·070" ? 3 : 4),
            "Durable pool must retain the actual printed, reduced Empower and independent return payments.");

    private static void CheckSelf(MatchState state, string no)
    {
        var unit = state.CardObjects["N9-UNIT"];
        var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[Actor(state)], Json)
            .GetProperty("players").GetProperty(Actor(state)).GetProperty("objects").GetProperty("N9-UNIT");
        Check(unit.IsEmpowered && unit.Power == (no == "VEN·043" ? 0 : 4)
            && view.GetProperty("effectivePower").GetInt32() == (no == "VEN·043" ? 7 : no == "VEN·070" ? 6 : 4)
            && unit.UntilEndOfTurnEffects.Count == 0 && unit.UntilEndOfTurnPowerModifiers.Count == 0,
            "Permanent self benefits derive from empowered status without rewriting printed Might or installing an end-of-turn grant.");
        Check(unit.Tags.Contains(CardObjectTags.Spellshield) == (no != "VEN·070")
            && (unit.RuleDerivedTags?.Contains("游走") == true) == (no == "VEN·070")
            && (unit.RuleDerivedTags?.Contains("坚守3") == true) == (no == "VEN·050"), "Each consumer must retain its exact empowered keywords.");
    }

    private static void CheckReturned(MatchState state, long generation)
        => Check(state.PlayerZones[Actor(state)].Hand.Contains("N9-UNIT") && state.CardObjects["N9-UNIT"].ObjectGeneration == generation + 1
            && !state.CardObjects["N9-UNIT"].IsEmpowered && state.CardObjects["N9-UNIT"].RuleDerivedTags is null,
            "A real return creates one fresh private incarnation; the old skill cannot empower or grant keywords to it.");

    internal static void CheckPrivacy(MatchState state)
    {
        foreach (var viewer in state.Seats.Keys.Append("")) {
            var snapshot = viewer == "" ? ResolutionResult.BuildSpectatorSnapshot(state) : ResolutionResult.BuildSnapshots(state)[viewer];
            var material = JsonSerializer.SerializeToElement(new { snapshot, prompt = viewer == "" ? null : ResolutionResult.BuildPrompts(state)[viewer] }, Json);
            var value = material.GetRawText();
            foreach (var (owner, zones) in state.PlayerZones) {
                var hidden = zones.MainDeck.Concat(zones.RuneDeck).Concat(owner == viewer ? [] : zones.Hand.Where(id =>
                    !(state.PendingCardChoice?.HandContext is { } hand && hand.OwnerId == owner
                        && hand.Cards.TryGetValue(id, out var generation) && generation == state.CardObjects[id].ObjectGeneration)));
                foreach (var id in hidden) Check(!ContainsIdentity(material, id), $"Unrevealed private hand/deck identity {id} escaped viewer {viewer} snapshot or prompt.");
            }
            Check(!value.Contains("empowerActivation", StringComparison.OrdinalIgnoreCase) && !value.Contains("openingSeed", StringComparison.Ordinal)
                && !value.Contains("reconnectToken", StringComparison.Ordinal), "Private activation, seed or credential data escaped a projection.");
        }
    }

    // Public stack handles retain their original spelling. An exact object ID
    // in any scalar or object key exposes an identity; a substring in a handle does not.
    internal static bool ContainsIdentity(JsonElement node, string id) => node.ValueKind switch {
        JsonValueKind.String => node.GetString() == id,
        JsonValueKind.Array => node.EnumerateArray().Any(item => ContainsIdentity(item, id)),
        JsonValueKind.Object => node.EnumerateObject().Any(p => p.Name == id || ContainsIdentity(p.Value, id)),
        _ => false
    };

    private static async Task<MatchRecoveryFrame> LoadVerified(NpgsqlDataSource source, string room)
    {
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing N9 SQL frame.");
        Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(errors.Count == 0, string.Join("; ", errors)); return frame;
    }

    internal static void CheckBirth(MatchRecoveryFrame frame, string actor, string peer)
        => LoggedQaRoomBirthAssertions.Check(frame, actor, peer, frame.Commands.Count < 3 ? "" :
            "ven-empowered-self-" + frame.AuthoritativeState!.CardObjects["N9-UNIT"].CardNo![4..], "n9-setup");

    internal static JsonObject Forge(JsonObject node, string kind, string actor)
    {
        if (kind is "source-view" or "null-stack") {
            if (kind == "null-stack") node["stack"] = null;
            else {
                var row = node["stack"]!.AsArray().Single(s => s!["effectKind"]!.GetValue<string>() == "EMPOWER_ACTIVATION")!;
                row["sourceObjectId"] = "N9-UNIT"; row["sourceVisibility"] = "VISIBLE";
            }
        } else if (kind == "wrong-input") node["ruleInputs"]!["identitySha256"] = new string('0', 64);
        else {
            var activation = node["stackItems"]!.AsArray().Single(s => s!["empowerActivation"] is not null)!["empowerActivation"]!;
            activation["paidMana"] = activation["paidMana"]!.GetValue<int>() - 1;
            node["runePools"]![actor]!["mana"] = node["runePools"]![actor]!["mana"]!.GetValue<int>() + 1;
        }
        return node;
    }

    private static async Task RejectTamper(NpgsqlDataSource source, string room, string output, string kind)
    {
        var original = await LoadVerified(source, room); var digest = await Digest(source, room);
        var playerView = kind is "source-view" or "null-stack"; var actor = Actor(original.AuthoritativeState!); var peer = Peer(original.AuthoritativeState!);
        var table = playerView ? "snapshots" : "state_snapshots";
        await using var connection = await source.OpenConnectionAsync();
        await using var read = new NpgsqlCommand($"select id,payload::text from {table} where match_id=@room {(playerView ? "and player_id=@peer" : "")} order by last_event_sequence desc,id desc limit 1", connection);
        read.Parameters.AddWithValue("room", room); if (playerView) read.Parameters.AddWithValue("peer", peer);
        long rowId; string saved;
        await using (var reader = await read.ExecuteReaderAsync()) { Check(await reader.ReadAsync(), "Missing exact durable tamper row."); rowId = reader.GetInt64(0); saved = reader.GetString(1); }
        var node = Forge(JsonNode.Parse(saved)!.AsObject(), kind, actor);
        string? registryError = null; IReadOnlyList<string> errors = []; bool? replayRejected = null;
        try {
            await Write(node.ToJsonString());
            var changed = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Tamper unexpectedly removed the room.");
            errors = changed.ValidationErrors;
            if (kind == "paid-mana") {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(original.ReplayInitialState!, original.Commands,
                    changed.AuthoritativeState!, new CoreRuleEngine(), default, original.Events);
                replayRejected = !replay.IsMatch; Check(replayRejected.Value, "Coordinated discounted-cost/pool forgery must fail untouched original-log replay.");
            } else Check(!changed.IsConsistent && errors.Any(e => e.Contains(kind == "source-view" ? "source" : kind == "null-stack" ? "stack" : "input", StringComparison.OrdinalIgnoreCase)),
                "Forged source, null stack or wrong rule inputs must produce recovery diagnostics, without throwing.");
            try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source),
                new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source)).GetOrCreateAsync(room, default); }
            catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { registryError = error.Message; }
            Check(registryError is not null, "The actual recovery registry must reject the forged durable row.");
        } finally { await Write(saved); }
        Check(await Digest(source, room) == digest, "Tamper probe must restore the exact original seven-table contents."); await LoadVerified(source, room);
        await File.WriteAllTextAsync(Path.Combine(output, "tamper-" + kind + ".json"), JsonSerializer.Serialize(new {
            kind, table, rowId, errors, replayRejected, registryRejected = true, registryError,
            originalAcceptedCommandsAndEventsNeverChanged = true, exactOriginalSqlPayloadRestored = true
        }, Json));
        async Task Write(string value) {
            await using var update = new NpgsqlCommand($"update {table} set payload=@value::jsonb where id=@id and match_id=@room", connection);
            update.Parameters.AddWithValue("value", value); update.Parameters.AddWithValue("id", rowId); update.Parameters.AddWithValue("room", room);
            Check(await update.ExecuteNonQueryAsync() == 1, "Mutate only this new fixture's selected durable row.");
        }
    }

    private static async Task<string> Digest(NpgsqlDataSource source, string room)
        => (await VenEmpoweredSelfSqlAudit.CaptureAsync(source, room)).SevenTableDigest;

    private static Task SaveOriginal(string output, MatchRecoveryFrame frame, string stage) => Task.WhenAll(
        File.WriteAllTextAsync(Path.Combine(output, stage + "-original-commands.json"), JsonSerializer.Serialize(frame.Commands, Json)),
        File.WriteAllTextAsync(Path.Combine(output, stage + "-original-events.json"), JsonSerializer.Serialize(frame.Events, Json)));

    private sealed class Context(NpgsqlDataSource source, string room, IMatchSession session, MatchState state, string actor, string peer)
    {
        internal string Room => room;
        internal string Actor => actor;
        internal string Peer => peer;
        internal IMatchSession Session = session;
        internal MatchState State = state;
        internal Dictionary<string, string> Tokens = [];
        internal Dictionary<string, string> ApiKeys = new[] { actor, peer }.ToDictionary(p => p, _ => "n9-key-" + Guid.NewGuid().ToString("N"));
        internal List<object> Checkpoints = [];
        private int submissions;
        internal static async Task<Context> Create(NpgsqlDataSource source, string no) {
            var id = Guid.NewGuid().ToString("N"); var room = "n9-ven-empower-" + id;
            var actor = "n9-" + id + "-p1"; var peer = "n9-" + id + "-p2";
            var players = new PostgresMatchPlayerStore(source);
            var session = new MatchSession(room, new CoreRuleEngine(), new PostgresMatchJournal(source), players);
            var context = new Context(source, room, session, MatchState.Create(room), actor, peer);
            foreach (var player in new[] { actor, peer }) {
                var joined = await context.Session.EnsurePlayerAsync(player, default);
                Check(joined.PlayerId == player && !string.IsNullOrWhiteSpace(joined.ReconnectToken), "Actual seat registration must return this player's real reconnect token.");
                context.Tokens[player] = joined.ReconnectToken; context.submissions++;
                await context.Cold("actual-room-seat-" + context.submissions, takePersistedState: true);
            }
            var scenario = "ven-empowered-self-" + no[4..];
            var seeded = await context.Session.SeedScenarioAsync(context.Actor, "n9-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(seeded.Accepted, seeded.ErrorMessage); context.State = seeded.State; context.submissions++;
            await context.Cold("new-current-qa-setup"); return context;
        }
        internal async Task Act(string player, GameCommand command, bool accepted = true) {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = Session.PromptFor(player).PromptId; node["snapshotTick"] = State.Tick;
            var raw = JsonSerializer.SerializeToElement(node); var mapped = GameCommandJsonMapper.Map(raw);
            var before = MatchStateHasher.Hash(State); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State), Json);
            var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State), Json); var id = "n9-step-" + submissions;
            var result = await Session.SubmitAsync(player, id, mapped, raw, default);
            Check(result.Accepted == accepted, result.ErrorMessage); State = result.State; submissions++;
            if (!accepted) Check(result.Events.Count == 0 && MatchStateHasher.Hash(State) == before
                && JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State), Json) == views
                && JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State), Json) == prompts, "Rejected intent changed authoritative state, views, prompts or events.");
            await Cold(id);
            if (accepted) {
                var digest = await Digest(source, room); var duplicate = await Session.SubmitAsync(player, id, mapped, raw, default);
                Check(duplicate.Accepted && MatchStateHasher.Hash(duplicate.State) == MatchStateHasher.Hash(State)
                    && await Digest(source, room) == digest, "Cold duplicate changed the accepted state or SQL rows.");
            }
        }
        internal async Task<MatchRecoveryFrame> Cold(string label, bool takePersistedState = false) {
            var digest = await Digest(source, room); var frame = await LoadVerified(source, room);
            CheckBirth(frame, Actor, Peer);
            if (takePersistedState) State = frame.AuthoritativeState!;
            Check(frame.Commands.Count == submissions && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(State), "SQL frame differs from the original accepted/rejected prefix.");
            Session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source),
                new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source)).GetOrCreateAsync(room, default);
            foreach (var viewer in State.Seats.Keys) {
                Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Session.SnapshotFor(viewer), Json),
                    JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(State)[viewer], Json)), "Cold private snapshot differs.");
                Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Session.PromptFor(viewer), Json),
                    JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(State)[viewer], Json)), "Cold private prompt differs.");
            }
            CheckPrivacy(State); Check(await Digest(source, room) == digest, "Cold recovery changed stored rows.");
            Checkpoints.Add(new { label, State.Tick, commands = frame.Commands.Count, events = frame.Events.Count,
                hash = MatchStateHasher.Hash(State), sevenTableDigest = digest, originalReplayMatched = true }); return frame;
        }
    }

    private static int PrintedMana(string no) => no == "VEN·043" ? 1 : no == "VEN·050" ? 4 : 3;
    private static string Actor(MatchState state) => state.CardObjects["N9-UNIT"].OwnerId!;
    private static string Peer(MatchState state) => state.Seats.Keys.Single(p => p != Actor(state));
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message ?? "N9 acceptance failed."); }
}
