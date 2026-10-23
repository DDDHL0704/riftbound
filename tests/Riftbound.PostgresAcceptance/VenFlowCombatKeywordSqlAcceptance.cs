using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class VenFlowCombatKeywordSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string Grant = "COMBAT_KEYWORD:强攻3:";

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output); var results = new List<object>();
        foreach (var flow in new[] { false, true })
        foreach (var boundary in new[] { "expiry", "return-after", "return-before" }) {
            var dir = Path.Combine(output, (flow ? "flow-" : "normal-") + boundary); Directory.CreateDirectory(dir);
            var context = await Context.Create(source, flow);
            await context.Act(context.Peer, new PlayCardCommand("N3-SPELL", "VEN·012", ["N3-ALLY"]), false);
            await CheckQuote(context.Session, context.State);
            await context.Act(context.Actor, new PlayCardCommand("N3-SPELL", "VEN·012", ["N3-ALLY"]));
            Check(context.State.RunePools[context.Actor].Mana == 17 && context.State.RunePools[context.Actor].PowerByTrait.GetValueOrDefault(RuneTrait.Red) == 9,
                "Normal and Flow must each pay three mana and one red power at confirmation.");
            var generation = context.State.CardObjects["N3-ALLY"].ObjectGeneration;
            if (boundary == "return-before") {
                await context.Act(context.Actor, new PlayCardCommand("N3-RETURN", "OGN·104/298", ["N3-ALLY"])); await context.Top();
            }
            await context.Top(); CheckSpellExit(context.State, flow);
            if (boundary == "return-before") CheckReturned(context.State, generation);
            else {
                CheckActive(context.State, flow);
                if (boundary == "expiry") {
                    await RejectTamper(source, context.Room, dir, "grant");
                    await RejectTamper(source, context.Room, dir, "payment");
                    await context.Cold("after-restoring-original-tamper-inputs");
                    await context.Act(context.Actor, new EndTurnCommand());
                    Check(!context.State.CardObjects["N3-ALLY"].UntilEndOfTurnEffects.Any(e => e.StartsWith(Grant, StringComparison.Ordinal))
                        && context.State.CardObjects["N3-ALLY"].Power == 4, "The keyword expires without changing stored printed power.");
                } else {
                    await context.Act(context.Actor, new PlayCardCommand("N3-RETURN", "OGN·104/298", ["N3-ALLY"])); await context.Top();
                    CheckReturned(context.State, generation);
                }
            }
            var final = await context.Cold("final"); await SaveOriginal(dir, final);
            results.Add(new { flow, boundary, room = context.Room, commands = final.Commands.Count, events = final.Events.Count,
                finalHash = MatchStateHasher.Hash(context.State), ruleInputs = context.State.RuleInputs,
                originalReplayMatched = true, coldPrefixChecks = context.Checkpoints, completeGameOrCardCertification = false });
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_N3_FLOW_KEYWORD_BOUNDARIES_PASS", results,
            fixtureScope = "New current-rule logged QA midgames from a genuine SEATING replay origin; not ordinary deck openings or imported historical journals."
        }, Json));
    }

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output); var cases = new List<object>();
        foreach (var flow in new[] { false, true }) {
            var dir = Path.Combine(output, flow ? "flow" : "normal"); Directory.CreateDirectory(dir);
            var context = await Context.Create(source, flow); await CheckQuote(context.Session, context.State);
            var frame = await context.Cold("network-prepared"); await SaveOriginal(dir, frame, "prepared");
            cases.Add(new { room = context.Room, actor = context.Actor, flow, tokens = context.Tokens, apiKeys = context.ApiKeys,
                beforeTick = context.State.Tick, beforeHash = MatchStateHasher.Hash(context.State),
                command = new PlayCardCommand("N3-SPELL", "VEN·012", ["N3-ALLY"]),
                expectedMana = 17, expectedRedPower = 9, expectedStoredPower = 4,
                hiddenFrom = context.State.Seats.Keys.ToDictionary(viewer => viewer, viewer => context.State.PlayerZones
                    .SelectMany(p => p.Value.MainDeck.Concat(p.Value.RuneDeck)
                        .Concat(p.Key != viewer ? p.Value.Hand.Where(id => id.StartsWith("PRIVATE-", StringComparison.Ordinal) || id == "N3-RETURN") : []))
                    .Distinct(StringComparer.Ordinal).ToArray()),
                ruleInputs = context.State.RuleInputs });
        }
        await File.WriteAllTextAsync(Path.Combine(output, "ven-flow-network-fixtures.json"), JsonSerializer.Serialize(new {
            schemaVersion = 1, cases, scope = "Fresh current QA setup commands; normal source in hand, Flow source in public graveyard. Real API performs all subsequent plays and priority passes."
        }, Json));
    }

    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        Check(stage is "before-restart" or "after-restart", "Use a declared actual API restart stage.");
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "ven-flow-network-fixtures.json")));
        var rows = new List<object>();
        foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = item.GetProperty("room").GetString()!; var flow = item.GetProperty("flow").GetBoolean();
            var frame = await LoadVerified(source, room); CheckActive(frame.AuthoritativeState!, flow);
            LoggedQaRoomBirthAssertions.Check(frame, Actor(frame.AuthoritativeState!), Peer(frame.AuthoritativeState!),
                flow ? "ven-flow-combat-flow" : "ven-flow-combat-normal", "n3-setup");
            Check(frame.Commands.Count == 7 && frame.Commands.Count(c => c.Accepted) == 6
                && frame.Commands.Where(c => c.Accepted).Select(c => c.ClientIntentId).SequenceEqual(new[] {
                    "room-seat:P1", "room-seat:P2", "n3-setup", "n3-network-play", "n3-network-resolve-0", "n3-network-resolve-1" })
                && frame.Commands.Single(c => !c.Accepted).ClientIntentId == "n3-network-stale",
                "Retain two real seat transitions, QA setup, actual play, two passes and one stale rejection; duplicates write no new command.");
            var dir = Path.Combine(output, flow ? "flow" : "normal");
            var sqlSample = await VenEmpoweredSelfSqlAudit.VerifyNativeCheckpointAsync(source, room, dir, stage, Json);
            var receipt = new { room, flow, stage, commands = frame.Commands.Count, events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(frame.AuthoritativeState!), sevenTableDigest = sqlSample.SevenTableDigest, sqlSample, originalReplayMatched = true,
                ruleInputs = frame.AuthoritativeState!.RuleInputs };
            await File.WriteAllTextAsync(Path.Combine(dir, "sql-" + stage + ".json"), JsonSerializer.Serialize(receipt, Json));
            await SaveOriginal(dir, frame, stage); rows.Add(receipt);
        }
        await File.WriteAllTextAsync(Path.Combine(output, "ven-flow-sql-" + stage + ".json"), JsonSerializer.Serialize(new {
            status = "REAL_API_N3_NORMAL_AND_FLOW_DURABILITY_PASS", stage, rows, completeGameOrCardCertification = false
        }, Json));
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "ven-flow-network-fixtures.json")));
        foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = item.GetProperty("room").GetString()!;
            var dir = Path.Combine(output, item.GetProperty("flow").GetBoolean() ? "flow" : "normal");
            await RejectTamper(source, room, dir, "grant"); await RejectTamper(source, room, dir, "payment");
        }
        await VerifyNetworkAsync(source, output, "after-restart");
    }

    private static async Task CheckQuote(IMatchSession session, MatchState state)
    {
        var actor = Actor(state); var prompt = session.PromptFor(actor); var card = new PlayCardCommand("N3-SPELL", "VEN·012", ["N3-ALLY"]);
        var quote = await session.PreviewPlayCardAsync(actor, new("n3-quote", prompt.PromptId!, state.Tick, card), default);
        Check(quote.CanPay && quote.Cost is { Mana: 3 } cost && cost.PowerByTrait.GetValueOrDefault(RuneTrait.Red) == 1,
            "Current normal/Flow quote must expose exactly three mana and one red power.");
    }

    private static void CheckActive(MatchState state, bool flow)
    {
        CheckSpellExit(state, flow);
        var unit = state.CardObjects["N3-ALLY"];
        Check(unit.Power == 4 && !unit.IsExhausted && unit.UntilEndOfTurnEffects.Count(e => e.StartsWith(Grant, StringComparison.Ordinal)) == 1
            && unit.UntilEndOfTurnPowerModifiers.Count == 0, "N3 readies the unit and grants one temporary keyword, not unconditional stored Might.");
        Check(state.RunePools[Actor(state)].Mana == 17 && state.RunePools[Actor(state)].PowerByTrait.GetValueOrDefault(RuneTrait.Red) == 9,
            "Durable state must retain the real three-mana/one-red payment.");
    }

    private static void CheckSpellExit(MatchState state, bool flow)
    {
        Check((flow ? state.PlayerZones[Actor(state)].Banished : state.PlayerZones[Actor(state)].Graveyard).Contains("N3-SPELL"),
            "Normal ends in graveyard; Flow's actual delayed replacement ends in banished.");
        Check(!state.PlayerZones[Actor(state)].Hand.Contains("N3-SPELL"), "The physical spell cannot remain in hand after resolution.");
    }

    private static void CheckReturned(MatchState state, long previous)
        => Check(state.PlayerZones[Actor(state)].Hand.Contains("N3-ALLY") && state.CardObjects["N3-ALLY"].ObjectGeneration > previous
            && state.CardObjects["N3-ALLY"].UntilEndOfTurnEffects.Count == 0 && state.CardObjects["N3-ALLY"].Power == 4,
            "A real hand return starts a fresh incarnation; old target grants cannot enter its new private incarnation.");

    private static async Task<MatchRecoveryFrame> LoadVerified(NpgsqlDataSource source, string room)
    {
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing N3 SQL frame.");
        Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(errors.Count == 0, string.Join("; ", errors)); return frame;
    }

    private static async Task RejectTamper(NpgsqlDataSource source, string room, string output, string kind)
    {
        var original = await LoadVerified(source, room); var digest = await Digest(source, room);
        await using var connection = await source.OpenConnectionAsync();
        await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
        read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
        var node = JsonNode.Parse(saved)!.AsObject();
        if (kind == "grant") {
            var effects = node["cardObjects"]!["N3-ALLY"]!["untilEndOfTurnEffects"]!.AsArray();
            var index = Enumerable.Range(0, effects.Count).Single(i => effects[i]!.GetValue<string>().StartsWith(Grant, StringComparison.Ordinal));
            effects[index] = "COMBAT_KEYWORD:强攻30:1";
        } else node["runePools"]![Actor(original.AuthoritativeState!)]!["powerByTrait"]![RuneTrait.Red] = 10;
        var forged = node.Deserialize<MatchState>(Json)!;
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(original.ReplayInitialState!, original.Commands, forged,
            new CoreRuleEngine(), default, original.Events);
        Check(!replay.IsMatch, "A coherent-looking grant/payment forgery must fail the untouched original command/event replay.");
        string? registryError = null;
        try {
            await Write(node.ToJsonString());
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source),
                new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source));
            try { await registry.GetOrCreateAsync(room, default); }
            catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { registryError = error.Message; }
            Check(registryError is not null, "The actual recovery registry must reject the temporarily forged durable snapshot.");
        } finally { await Write(saved); }
        Check(await Digest(source, room) == digest, "Restore the exact original seven-table contents after the tamper probe.");
        await LoadVerified(source, room);
        await File.WriteAllTextAsync(Path.Combine(output, "tamper-" + kind + ".json"), JsonSerializer.Serialize(new {
            kind, originalReplayRejected = true, replay.Errors, registryRejected = true, registryError,
            originalAcceptedCommandsAndEventsNeverChanged = true, exactOriginalSqlPayloadRestored = true
        }, Json));
        async Task Write(string value) {
            await using var update = new NpgsqlCommand("update state_snapshots set payload=@value::jsonb where match_id=@room and state_tick=@tick", connection);
            update.Parameters.AddWithValue("value", value); update.Parameters.AddWithValue("room", room);
            update.Parameters.AddWithValue("tick", original.AuthoritativeState!.Tick); Check(await update.ExecuteNonQueryAsync() == 1, "Mutate only this new fixture's latest snapshot.");
        }
    }

    private static async Task<string> Digest(NpgsqlDataSource source, string room)
        => (await VenEmpoweredSelfSqlAudit.CaptureAsync(source, room)).SevenTableDigest;

    private static Task SaveOriginal(string output, MatchRecoveryFrame frame, string stage = "final") => Task.WhenAll(
        File.WriteAllTextAsync(Path.Combine(output, stage + "-original-commands.json"), JsonSerializer.Serialize(frame.Commands, Json)),
        File.WriteAllTextAsync(Path.Combine(output, stage + "-original-events.json"), JsonSerializer.Serialize(frame.Events, Json)));

    private sealed class Context(NpgsqlDataSource source, string room, IMatchSession session, MatchState state, string actor, string peer, bool flow)
    {
        internal string Room => room;
        internal string Actor => actor;
        internal string Peer => peer;
        internal IMatchSession Session = session;
        internal MatchState State = state;
        internal Dictionary<string, string> Tokens = [];
        internal Dictionary<string, string> ApiKeys = new[] { actor, peer }.ToDictionary(id => id, _ => "n3-key-" + Guid.NewGuid().ToString("N"));
        internal List<object> Checkpoints = [];
        private int submissions;
        internal static async Task<Context> Create(NpgsqlDataSource source, bool flow) {
            var id = Guid.NewGuid().ToString("N"); var room = "n3-ven-flow-" + id;
            var actor = "n3-" + id + "-p1"; var peer = "n3-" + id + "-p2";
            var players = new PostgresMatchPlayerStore(source);
            var session = new MatchSession(room, new CoreRuleEngine(), new PostgresMatchJournal(source), players);
            var context = new Context(source, room, session, MatchState.Create(room), actor, peer, flow);
            foreach (var player in new[] { actor, peer }) {
                var joined = await context.Session.EnsurePlayerAsync(player, default);
                Check(joined.PlayerId == player && !string.IsNullOrWhiteSpace(joined.ReconnectToken), "Actual seat registration must return its own real reconnect token.");
                context.Tokens[player] = joined.ReconnectToken; context.submissions++;
                await context.Cold("actual-room-seat-" + context.submissions, takePersistedState: true);
            }
            var scenario = flow ? "ven-flow-combat-flow" : "ven-flow-combat-normal";
            var seeded = await context.Session.SeedScenarioAsync(context.Actor, "n3-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(seeded.Accepted, seeded.ErrorMessage); context.State = seeded.State; context.submissions++;
            await context.Cold("new-current-qa-setup"); return context;
        }
        internal async Task Act(string player, GameCommand command, bool accepted = true) {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = Session.PromptFor(player).PromptId; node["snapshotTick"] = State.Tick;
            var raw = JsonSerializer.SerializeToElement(node); var mapped = GameCommandJsonMapper.Map(raw);
            var before = MatchStateHasher.Hash(State); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State), Json);
            var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State), Json); var id = "n3-step-" + submissions;
            var result = await Session.SubmitAsync(player, id, mapped, raw, default);
            Check(result.Accepted == accepted, result.ErrorMessage); State = result.State; submissions++;
            if (!accepted) Check(result.Events.Count == 0 && MatchStateHasher.Hash(State) == before
                && JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State), Json) == views
                && JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State), Json) == prompts, "Rejected intent changed state, views or prompts.");
            await Cold(id);
            if (accepted) {
                var digest = await Digest(source, room); var duplicate = await Session.SubmitAsync(player, id, mapped, raw, default);
                Check(duplicate.Accepted && MatchStateHasher.Hash(duplicate.State) == MatchStateHasher.Hash(State)
                    && await Digest(source, room) == digest, "Cold duplicate changed the original state or SQL rows.");
            }
        }
        internal async Task Top() { await Act(State.PriorityPlayerId!, new PassPriorityCommand()); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        internal async Task<MatchRecoveryFrame> Cold(string label, bool takePersistedState = false) {
            var digest = await Digest(source, room); var frame = await LoadVerified(source, room);
            LoggedQaRoomBirthAssertions.Check(frame, Actor, Peer, flow ? "ven-flow-combat-flow" : "ven-flow-combat-normal", "n3-setup");
            if (takePersistedState) State = frame.AuthoritativeState!;
            Check(frame.Commands.Count == submissions && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(State), "SQL frame differs from current accepted/rejected prefixes.");
            Session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source),
                new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source)).GetOrCreateAsync(room, default);
            foreach (var viewer in State.Seats.Keys) {
                Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Session.SnapshotFor(viewer), Json),
                    JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(State)[viewer], Json)), "Cold private snapshot differs.");
                Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Session.PromptFor(viewer), Json),
                    JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(State)[viewer], Json)), "Cold prompt differs.");
                var view = JsonSerializer.Serialize(new { snapshot = Session.SnapshotFor(viewer), prompt = Session.PromptFor(viewer) }, Json);
                foreach (var zone in State.PlayerZones) {
                    var hidden = zone.Value.MainDeck.Concat(zone.Value.RuneDeck).Concat(zone.Key != viewer
                        ? zone.Value.Hand.Where(id => id.StartsWith("PRIVATE-", StringComparison.Ordinal) || id is "N3-SPELL" or "N3-RETURN") : []);
                    foreach (var id in hidden) Check(!view.Contains(id, StringComparison.Ordinal), "Unrevealed private identity escaped a projection or prompt.");
                }
            }
            Check(await Digest(source, room) == digest, "Cold recovery changed stored rows.");
            Checkpoints.Add(new { label, State.Tick, commands = frame.Commands.Count, events = frame.Events.Count,
                hash = MatchStateHasher.Hash(State), sevenTableDigest = digest, originalReplayMatched = true }); return frame;
        }
    }

    private static string Actor(MatchState state) => state.CardObjects["N3-ALLY"].OwnerId!;
    private static string Peer(MatchState state) => state.Seats.Keys.Single(p => p != Actor(state));
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message ?? "N3 acceptance failed."); }
}
