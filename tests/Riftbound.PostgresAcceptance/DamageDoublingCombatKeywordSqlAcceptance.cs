using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class DamageDoublingCombatKeywordSqlAcceptance
{
    private const string Double = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";
    private const string Declaration = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Fresh(output);
        var reports = new List<object>();
        foreach (var kind in new[] { "lotus", "control", "return", "tokens", "numeric-scale", "numeric-marked" })
        {
            var folder = Path.Combine(output, kind); Directory.CreateDirectory(folder);
            var scenario = kind == "numeric-scale" ? "damage-doubling-overflow"
                : kind == "numeric-marked" ? "damage-marked-overflow" : "damage-doubling-combat-keywords";
            var run = await Runner.Create(source, folder, kind, scenario);
            if (kind.StartsWith("numeric-", StringComparison.Ordinal))
                await NumericRejection(run);
            else
            {
                foreach (var step in Steps(kind)) await run.Submit(step.Actor, GameCommandJsonMapper.Map(step.Command));
                CheckBoundary(run.State, kind, run.P1, run.P2);
                await run.Cold("changed-value-boundary");
                var digest = await Digest(source, run.Room);
                var duplicate = await run.Session.SubmitAsync(run.LastActor!, run.LastIntent!, run.LastCommand!,
                    run.LastRaw, default);
                Check(duplicate.Accepted && await Digest(source, run.Room) == digest,
                    "The real SQL session must deduplicate the completed boundary command without changing any of seven tables.");
            }
            var frame = await run.Cold("final-original-journal");
            await run.Save(frame);
            reports.Add(new { kind, run.Room, commands = frame.Commands.Count, accepted = frame.Commands.Count(c => c.Accepted),
                events = frame.Events.Count, finalHash = MatchStateHasher.Hash(run.State), checkpoints = run.Checkpoints,
                completeOriginalReplayMatched = true, realRegistryRecovered = true });
        }
        await Write(output, "receipt.json", new {
            status = "REAL_POSTGRES_DAMAGE_DOUBLING_COMBAT_KEYWORD_PASS", executedAtUtc = DateTimeOffset.UtcNow, reports,
            scope = "Fresh recorded ROOM_SEAT and finite DEV_SEED_SCENARIO positions under current RuleInputs. Not ordinary deck openings or whole-card certification. Both numeric rejection types continue on their own rejecting PostgreSQL session."
        });
        Console.WriteLine("POSTGRES_DAMAGE_DOUBLING_COMBAT_KEYWORDS_PASS: six logged QA rooms; two damage applications, C6 declaration metadata, and same-session numeric rejection follow-up.");
    }

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        Check(!File.Exists(Path.Combine(output, "network-fixture.json")), "Fresh C5/C6 network preparation is required.");
        var cases = new List<object>();
        foreach (var kind in new[] { "lotus", "return", "tokens" })
        {
            var run = await Runner.Create(source, Path.Combine(output, "prepare-" + kind), kind, "damage-doubling-combat-keywords");
            var frame = await run.Cold("network-seeded-current-input");
            Check(frame.Commands.Count == 3 && frame.Commands.Take(2).All(c => c.CommandType == MatchSession.RoomSeatCommandType)
                && frame.Commands[2].CommandType == "DEV_SEED_SCENARIO:damage-doubling-combat-keywords",
                "Network setup must be actual seats plus a recorded finite seed, never imported accepted rows.");
            cases.Add(new { kind, room = run.Room, p1 = run.P1, p2 = run.P2, tokens = run.Tokens,
                credentials = run.Credentials, steps = Steps(kind), seedTick = run.State.Tick,
                ruleInputs = run.State.RuleInputs, initialHash = MatchStateHasher.Hash(run.State),
                expectedAcceptedCommands = 3 + Steps(kind).Count });
        }
        await Write(output, "network-fixture.json", new {
            kind = "FUTURE_CLIENT_INTENTS_NOT_ACCEPTED_HISTORY", cases,
            scope = "Explicit logged QA positions. Each case has independent room, player identifiers and credentials. API play/restart/recover must execute the remaining intents."
        });
    }

    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        Check(stage is "before-restart" or "after-restart", "Unknown C5/C6 network verification stage.");
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var kind = item.GetProperty("kind").GetString()!;
            var room = item.GetProperty("room").GetString()!;
            var frame = await LoadAndAudit(source, room);
            var state = frame.AuthoritativeState!;
            CheckBoundary(state, kind, item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
            Check(frame.Commands.Count == item.GetProperty("expectedAcceptedCommands").GetInt32() + 1
                && frame.Commands.Count(c => !c.Accepted) == 1 && frame.Commands[^1].ErrorMessage is not null,
                "Keep the actual seat/seed/action prefix and one stale rejection; duplicates and wrong authenticated actors must not append rows.");
            var stale = frame.Commands[^1];
            Check(!stale.Accepted && stale.StartedTick == stale.CompletedTick
                && stale.StartedEventSequence == stale.CompletedEventSequence,
                "The persisted stale rejection must not advance tick or emit an event.");
            Check(frame.Commands.All(c => Same(c.RuleInputs, state.RuleInputs))
                && Same(item.GetProperty("ruleInputs"), state.RuleInputs), "Fresh SQL/API histories must retain their current recorded RuleInputs.");
            var digest = await Digest(source, room);
            if (stage == "after-restart")
            {
                using var before = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "sql-before-restart.json")));
                var prior = before.RootElement.GetProperty("cases").EnumerateArray()
                    .Single(c => c.GetProperty("room").GetString() == room);
                Check(prior.GetProperty("sevenTableDigest").GetString() == digest,
                    "Actual API restart, reconnect and duplicate completion must leave all seven SQL tables unchanged.");
            }
            await Write(output, kind + "-original-commands.json", frame.Commands);
            await Write(output, kind + "-original-events.json", frame.Events);
            reports.Add(new { kind, room, commands = frame.Commands.Count, events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(state), sevenTableDigest = digest,
                originalReplayMatched = true, actualRegistryRecoveryMatched = true });
        }
        await Write(output, "sql-" + stage + ".json", new {
            status = "REAL_API_C5_C6_SQL_ORIGINAL_REPLAY_PASS", stage, cases = reports,
            scope = "Recorded explicit QA positions; this does not certify ordinary openings, complete cards, or an API process restart unless the external runner actually restarted it."
        });
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var room = item.GetProperty("room").GetString()!;
            var kind = item.GetProperty("kind").GetString()!;
            var original = await LoadAndAudit(source, room);
            var state = original.AuthoritativeState!;
            MatchState forged;
            var coherent = kind == "lotus";
            if (coherent)
            {
                var target = state.CardObjects["TARGET"];
                forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                {
                    ["TARGET"] = target with { UntilEndOfTurnEffects = target.UntilEndOfTurnEffects
                        .Append(Double + ":3").Order(StringComparer.Ordinal).ToArray() }
                } };
            }
            else
            {
                var marker = state.UntilEndOfTurnEffects.Single(e => e.StartsWith(Declaration, StringComparison.Ordinal));
                var context = JsonNode.Parse(Convert.FromBase64String(marker[Declaration.Length..]))!.AsObject();
                if (kind == "return")
                    context["PublicObjectBindings"]!.AsArray().Add(new JsonObject { ["ObjectId"] = "FARRON", ["Generation"] = 0L });
                else context["CeasedTokens"] = new JsonObject();
                var replacement = Declaration + Convert.ToBase64String(Encoding.UTF8.GetBytes(context.ToJsonString()));
                forged = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects
                    .Select(e => e == marker ? replacement : e).Order(StringComparer.Ordinal).ToArray() };
            }
            var digest = await Digest(source, room);
            var rows = await CaptureLatestRows(source, room, state.Tick, original.LastEventSequence);
            string? rejection = null; bool shapeConsistent = false;
            try
            {
                var snapshots = coherent ? ResolutionResult.BuildSnapshots(forged) : null;
                var prompts = coherent ? ResolutionResult.BuildPrompts(forged) : null;
                foreach (var row in rows)
                {
                    var value = row.Table == "state_snapshots" ? JsonSerializer.Serialize(forged, Json)
                        : !coherent ? row.Payload
                        : row.Table == "snapshots" ? JsonSerializer.Serialize(snapshots![row.Player!], Json)
                        : JsonSerializer.Serialize(prompts![row.Player!], Json);
                    await UpdatePayload(source, room, row, value);
                }
                var altered = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default)
                    ?? throw new InvalidOperationException("Missing tampered PostgreSQL frame.");
                shapeConsistent = altered.IsConsistent;
                if (coherent) Check(shapeConsistent, "A canonical third application with coherently reprojected views must reach original-journal auditing.");
                var registry = Registry(source);
                try { await registry.GetOrCreateAsync(room, default); }
                catch (MatchSessionException exception) when (exception.Code == ErrorCodes.RecoveryInconsistent) { rejection = exception.Message; }
                Check(rejection is not null, "Actual PostgreSQL-backed recovery registry must reject the forged state.");
                if (coherent) Check(rejection!.Contains("action-log audit failed", StringComparison.Ordinal),
                    "A shape-valid extra application needs original-journal birth rejection, not an unrelated shape failure.");
            }
            finally
            {
                foreach (var row in rows) await UpdatePayload(source, room, row, row.Payload);
            }
            Check(await Digest(source, room) == digest, "Temporary tampering must restore every SQL row exactly.");
            await LoadAndAudit(source, room);
            reports.Add(new { kind, room, shapeConsistent, rejection, originalAcceptedRowsNeverRewritten = true,
                originalSevenTableDigestRestored = true });
        }
        await Write(output, "registry-tamper-results.json", new { status = "REAL_POSTGRES_C5_C6_TAMPER_REJECTED", reports });
    }

    private static async Task NumericRejection(Runner run)
    {
        foreach (var step in LotusSteps()) await run.Submit(step.Actor, GameCommandJsonMapper.Map(step.Command));
        await run.Submit("p1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:FIELD", [], SourceObjectIds: ["ATTACKER", "HELPER"]));
        await run.Submit("$focus", new PassFocusCommand()); await run.Submit("$focus", new PassFocusCommand());
        await run.Submit("p1", new DeclareBattleCommand("FIELD", ["ATTACKER", "HELPER"], ["TARGET"], ["COMBAT_ASSIGNMENT"]));
        await run.Submit("p1", new AssignCombatDamageCommand("battle:FIELD", "FIELD", Assignments(run.Session.PromptFor(run.P1))));
        var session = run.Session;
        var before = run.State;
        var original = await new PostgresMatchRecoveryStore(run.Source).LoadAsync(run.Room, default)
            ?? throw new InvalidOperationException("Missing numeric prefix.");
        var rawCommand = new AssignCombatDamageCommand("battle:FIELD", "FIELD", Assignments(session.PromptFor(run.P2)));
        var raw = run.Raw(run.P2, rawCommand);
        var result = await session.SubmitAsync(run.P2, "numeric-unsupported", rawCommand, raw, default);
        Check(!result.Accepted && result.ErrorCode == ErrorCodes.UnsupportedCardBehavior && result.Events.Count == 0,
            "The actual numeric boundary must reject with its dedicated capability error and zero events.");
        Check(MatchStateHasher.Hash(result.State) == MatchStateHasher.Hash(before)
            && result.State.RngCursor == before.RngCursor && result.State.ActivePlayerId == before.ActivePlayerId
            && result.State.PriorityPlayerId == before.PriorityPlayerId && Same(result.State.RunePools, before.RunePools)
            && Same(result.Snapshots, ResolutionResult.BuildSnapshots(before)) && Same(result.Prompts, ResolutionResult.BuildPrompts(before)),
            "Rejected arithmetic must roll back all state, resources, RNG, priority and both private views.");
        var rejected = await new PostgresMatchRecoveryStore(run.Source).LoadAsync(run.Room, default)
            ?? throw new InvalidOperationException("Missing numeric rejection row.");
        Check(rejected.IsConsistent && rejected.Commands.Count == original.Commands.Count + 1
            && rejected.Events.Count == original.Events.Count && !rejected.Commands[^1].Accepted
            && rejected.Commands[^1].StartedTick == rejected.Commands[^1].CompletedTick
            && rejected.Commands[^1].StartedEventSequence == rejected.Commands[^1].CompletedEventSequence
            && Same(rejected.Commands.Take(original.Commands.Count).ToArray(), original.Commands)
            && Same(rejected.Events, original.Events), "Retain the exact accepted prefix and zero-event rejection row.");
        var digest = await Digest(run.Source, run.Room);
        var duplicate = await session.SubmitAsync(run.P2, "numeric-unsupported", rawCommand, raw, default);
        Check(!duplicate.Accepted && duplicate.ErrorCode == result.ErrorCode && await Digest(run.Source, run.Room) == digest,
            "Repeating the rejected intent on this same session must not append another row.");
        Check(ReferenceEquals(session, run.Session), "Do not swap the rejecting session before the legal follow-up.");
        await run.Submit("p2", new SurrenderCommand(), cold: false);
        Check(ReferenceEquals(session, run.Session) && run.State.Status == MatchStatuses.Finished
            && !run.State.BattleState.IsActive, "The rejecting real PostgreSQL session must itself accept surrender and clear partial combat.");
        var frame = await run.Cold("same-rejecting-session-surrender");
        Check(frame.Commands.Count == original.Commands.Count + 2 && frame.Commands.Count(c => !c.Accepted) == 1
            && frame.Commands[^1].CommandType == CommandTypes.Surrender && frame.Commands[^1].Accepted,
            "Original SQL order must be accepted prefix, numeric rejection, then legal surrender.");
        await Write(run.Output, "numeric-rejection.json", new {
            result.ErrorCode, result.ErrorMessage, sameSessionFollowUp = true, repeatedRejectionAppendedNoRows = true,
            rejectedEvents = result.Events.Count, rejectedTick = before.Tick, finalTick = run.State.Tick,
            acceptedPrefixRetained = true, completeOriginalReplayMatched = true,
            scope = "Representation-boundary QA; not an ordinary deck reaching this Might."
        });
    }

    private static List<Step> LotusSteps()
    {
        var steps = new List<Step>();
        foreach (var id in new[] { "LOTUS1", "LOTUS2" })
        {
            steps.Add(Step.For("p1", new PlayCardCommand(id, "UNL-013/219", ["TARGET"])));
            steps.Add(Step.For("$priority", new PassPriorityCommand()));
            steps.Add(Step.For("$priority", new PassPriorityCommand()));
        }
        return steps;
    }

    private static List<Step> Steps(string kind)
    {
        if (kind == "lotus")
            return [..LotusSteps(), Step.For("p1", new PlayCardCommand("RAY", "OGN·009/298", ["TARGET"])),
                Step.For("$priority", new PassPriorityCommand()), Step.For("$priority", new PassPriorityCommand())];
        var steps = new List<Step>
        {
            Step.For("p1", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BASE")),
            Step.For("p1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])),
            Step.For("$priority", new PassPriorityCommand()), Step.For("$priority", new PassPriorityCommand()),
            Step.For("p1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:FIELD", [],
                SourceObjectIds: kind == "tokens" ? ["ALLY", "FARRON", "TOKEN1", "TOKEN2"] : ["ALLY", "FARRON"]))
        };
        if (kind == "return")
        {
            steps.Add(Step.For("p1", new PlayCardCommand("RETURN", "OGN·104/298", ["FARRON"])));
            steps.Add(Step.For("$priority", new PassPriorityCommand())); steps.Add(Step.For("$priority", new PassPriorityCommand()));
        }
        if (kind == "tokens")
            for (var i = 1; i <= 2; i++)
            {
                if (i == 2) steps.Add(Step.For("$focus", new PassFocusCommand()));
                steps.Add(Step.For("p1", new PlayCardCommand("KILL" + i, "OGN·014/298", ["TOKEN" + i])));
                steps.Add(Step.For("$priority", new PassPriorityCommand())); steps.Add(Step.For("$priority", new PassPriorityCommand()));
            }
        steps.Add(Step.For("$focus", new PassFocusCommand())); steps.Add(Step.For("$focus", new PassFocusCommand()));
        steps.Add(Step.For("p1", new DeclareBattleCommand("FIELD", kind == "return" ? ["ALLY"] : ["ALLY", "FARRON"],
            ["ENEMY1", "ENEMY2"], ["COMBAT_ASSIGNMENT"])));
        return steps;
    }

    private static void CheckBoundary(MatchState state, string kind, string p1, string p2)
    {
        if (kind == "lotus")
        {
            Check(state.CardObjects["TARGET"].Damage == 12
                && state.CardObjects["TARGET"].UntilEndOfTurnEffects.Where(e => e.StartsWith(Double, StringComparison.Ordinal))
                    .SequenceEqual(new[] { Double, Double + ":2" }), "Two actual Lotus applications must persist independently and Ray must cause 12 damage.");
            return;
        }
        Check(state.BattleState.IsActive, "The changed C6 declaration must survive as an actual pending assignment.");
        var snapshot = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[p1], Json);
        Check(snapshot.GetProperty("players").GetProperty(p1).GetProperty("objects").GetProperty("ALLY")
            .GetProperty("effectivePower").GetInt32() == (kind == "return" ? 3 : 4), "Printed Assault and current independent Farron aura must reach persisted snapshots.");
        var marker = state.UntilEndOfTurnEffects.Single(e => e.StartsWith(Declaration, StringComparison.Ordinal));
        using var context = JsonDocument.Parse(Convert.FromBase64String(marker[Declaration.Length..]));
        var bindings = context.RootElement.GetProperty("PublicObjectBindings").Deserialize<ObjectBinding[]>(Json)!;
        var expected = kind == "return" ? new[] { "ALLY", "ENEMY1", "ENEMY2" } : ["ALLY", "FARRON", "ENEMY1", "ENEMY2"];
        Check(bindings.Select(b => b.ObjectId).Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal))
            && bindings.All(b => b.Generation == (b.ObjectId == "ALLY" ? 1 : 0)),
            "New declaration metadata must retain exactly the remaining participants' original incarnations.");
        if (kind == "return")
            Check(state.ObjectLocations["FARRON"].Zone == "HAND" && state.CardObjects["FARRON"].ObjectGeneration == 1
                && !bindings.Any(b => b.ObjectId == "FARRON"), "Returned new generation must not be recaptured as a public participant.");
        if (kind == "tokens")
            Check(!state.CardObjects.ContainsKey("TOKEN1") && !state.CardObjects.ContainsKey("TOKEN2")
                && (!context.RootElement.TryGetProperty("CeasedTokens", out var ceased) || ceased.ValueKind == JsonValueKind.Null),
                "Deleted precombat tokens must actually disappear; empty ceased-token metadata is null, not an invalid empty map.");
        Check(state.PlayerZones[p2].Battlefields.Contains("TARGET"), "The independent spell battlefield must not be confused with this combat.");
    }

    private static IReadOnlyList<CombatDamageAssignmentDto> Assignments(ActionPromptDto prompt)
    {
        var metadata = JsonSerializer.SerializeToElement(prompt.View!.Metadata, Json);
        var pool = metadata.GetProperty("assignableDamagePool");
        var legal = metadata.GetProperty("legalTargets");
        var lethal = metadata.GetProperty("lethalDamageThreshold");
        var used = new Dictionary<string, int>(); var result = new List<CombatDamageAssignmentDto>();
        foreach (var source in pool.EnumerateObject())
        {
            var remaining = source.Value.GetInt32();
            var targets = legal.GetProperty(source.Name).EnumerateArray().Select(x => x.GetString()!).ToArray();
            for (var i = 0; i < targets.Length && remaining > 0; i++)
            {
                var amount = i == targets.Length - 1 ? remaining : Math.Min(remaining,
                    Math.Max(0, lethal.GetProperty(targets[i]).GetInt32() - used.GetValueOrDefault(targets[i])));
                if (amount == 0) continue;
                result.Add(new(source.Name, targets[i], amount)); remaining -= amount;
                used[targets[i]] = used.GetValueOrDefault(targets[i]) + amount;
            }
        }
        return result;
    }

    private sealed record Step(string Actor, JsonElement Command)
    {
        internal static Step For(string actor, GameCommand command)
            => new(actor, JsonSerializer.SerializeToElement(command, command.GetType(), Json));
    }

    private sealed class Runner(NpgsqlDataSource source, string output, string room, string p1, string p2)
    {
        internal NpgsqlDataSource Source => source;
        internal string Room => room;
        internal string Output => output;
        internal string P1 => p1;
        internal string P2 => p2;
        internal IMatchSession Session { get; private set; } = null!;
        internal MatchState State { get; private set; } = null!;
        internal Dictionary<string, string> Tokens { get; } = [];
        internal Dictionary<string, string> Credentials { get; } = [];
        internal List<object> Checkpoints { get; } = [];
        internal string? LastActor, LastIntent;
        internal GameCommand? LastCommand;
        internal JsonElement LastRaw;
        private int submitted;
        private readonly CoreRuleEngine engine = new();
        private readonly PostgresMatchJournal journal = new(source);
        private readonly PostgresMatchPlayerStore players = new(source);

        internal static async Task<Runner> Create(NpgsqlDataSource source, string output, string kind, string scenario)
        {
            Directory.CreateDirectory(output);
            var identity = Guid.NewGuid().ToString("N");
            var run = new Runner(source, output, "ddck-" + kind + "-" + identity, "ddck-" + identity + "-a", "ddck-" + identity + "-b");
            run.Session = new MatchSession(run.Room, run.engine, run.journal, run.players);
            foreach (var actor in new[] { run.P1, run.P2 })
            {
                run.Tokens[actor] = (await run.Session.EnsurePlayerAsync(actor, default)).ReconnectToken;
                run.Credentials[actor] = "ddck-key-" + actor;
            }
            var seeded = await run.Session.SeedScenarioAsync(run.P1, "ddck-original-qa-seed", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
            Check(seeded.Accepted, seeded.ErrorMessage);
            run.State = seeded.State;
            var frame = await run.Cold("original-seats-and-logged-seed");
            Check(frame.ReplayInitialState is { Tick: 0, Status: MatchStatuses.Empty, Seats.Count: 0 }
                && frame.OpeningSeed != 0, "Full replay must begin before the recorded seats, not at an imported midgame snapshot.");
            return run;
        }

        internal JsonElement Raw(string actor, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = Session.PromptFor(actor).PromptId; node["snapshotTick"] = State.Tick;
            return JsonSerializer.SerializeToElement(node);
        }

        internal async Task<ResolutionResult> Submit(string actor, GameCommand command, bool cold = true)
        {
            actor = actor switch { "p1" => P1, "p2" => P2, "$priority" => State.PriorityPlayerId!, "$focus" => State.FocusPlayerId!, _ => actor };
            Check(!string.IsNullOrWhiteSpace(actor), "The actual server must expose the expected focus/priority.");
            var raw = Raw(actor, command); var id = "ddck-action-" + submitted++;
            var result = await Session.SubmitAsync(actor, id, GameCommandJsonMapper.Map(raw), raw, default);
            await Write(output, id + "-result.json", result);
            Check(result.Accepted, result.ErrorMessage); State = result.State;
            LastActor = actor; LastIntent = id; LastCommand = command; LastRaw = raw;
            CheckPrivateViews(State);
            if (cold) await Cold(id);
            return result;
        }

        internal async Task<MatchRecoveryFrame> Cold(string label)
        {
            var before = await Digest(source, room);
            var frame = await LoadAndAudit(source, room);
            Check(MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(State), "Cold SQL authoritative state changed.");
            Session = MatchSession.Restore(frame, engine, journal, players);
            foreach (var actor in State.Seats.Keys)
                Check(Same(Session.SnapshotFor(actor), ResolutionResult.BuildSnapshots(State)[actor])
                    && Same(Session.PromptFor(actor), ResolutionResult.BuildPrompts(State)[actor]), "Cold private snapshot/prompt mismatch.");
            CheckPrivateViews(State);
            Check(await Digest(source, room) == before, "Cold recovery or registry auditing rewrote a SQL row.");
            Checkpoints.Add(new { label, State.Tick, commands = frame.Commands.Count, events = frame.Events.Count, completeOriginalReplay = true });
            return frame;
        }

        internal async Task Save(MatchRecoveryFrame frame)
        {
            await Write(output, "commands.json", frame.Commands); await Write(output, "events.json", frame.Events);
            await Write(output, "final.json", State);
        }
    }

    private static void CheckPrivateViews(MatchState state)
    {
        foreach (var actor in state.Seats.Keys)
        {
            var view = JsonSerializer.Serialize(new { snapshot = ResolutionResult.BuildSnapshots(state)[actor], prompt = ResolutionResult.BuildPrompts(state)[actor] }, Json);
            Check(!view.Contains("PRIVATE-P1", StringComparison.Ordinal) && !view.Contains("PRIVATE-P2", StringComparison.Ordinal)
                && !view.Contains("reconnectToken", StringComparison.Ordinal) && !view.Contains("openingSeed", StringComparison.Ordinal),
                "Never-public deck identities or server credentials escaped in a current view.");
        }
    }

    private static async Task<MatchRecoveryFrame> LoadAndAudit(NpgsqlDataSource source, string room)
    {
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default)
            ?? throw new InvalidOperationException("Missing real PostgreSQL frame.");
        Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(errors.Count == 0, string.Join("; ", errors));
        var session = await Registry(source).GetOrCreateAsync(room, default);
        foreach (var actor in frame.AuthoritativeState!.Seats.Keys)
            Check(Same(session.SnapshotFor(actor), frame.PlayerViews[actor].Snapshot), "Actual registry restored a different private view.");
        return frame;
    }

    private static InMemoryMatchSessionRegistry Registry(NpgsqlDataSource source)
        => new(new CoreRuleEngine(), new PostgresMatchJournal(source), new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source));

    private sealed record SavedRow(string Table, long Id, string? Player, string Payload);

    private static async Task<List<SavedRow>> CaptureLatestRows(NpgsqlDataSource source, string room, long tick, long sequence)
    {
        var rows = new List<SavedRow>();
        await using var connection = await source.OpenConnectionAsync();
        foreach (var table in new[] { "state_snapshots", "snapshots", "action_prompts" })
        {
            var tickColumn = table == "state_snapshots" ? "state_tick" : table == "snapshots" ? "snapshot_tick" : "prompt_tick";
            var playerColumn = table == "state_snapshots" ? "null::text" : "player_id";
            await using var query = new NpgsqlCommand($"select id,{playerColumn},payload::text from {table} where match_id=@room and {tickColumn}=@tick and last_event_sequence=@sequence", connection);
            query.Parameters.AddWithValue("room", room); query.Parameters.AddWithValue("tick", tick); query.Parameters.AddWithValue("sequence", sequence);
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(new(table, Convert.ToInt64(reader.GetValue(0)), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2)));
        }
        Check(rows.Count == 5, "Capture one authoritative snapshot and two complete current private views/prompts.");
        return rows;
    }

    private static async Task UpdatePayload(NpgsqlDataSource source, string room, SavedRow row, string payload)
    {
        await using var connection = await source.OpenConnectionAsync();
        await using var update = new NpgsqlCommand($"update {row.Table} set payload=@payload where match_id=@room and id=@id", connection);
        update.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, payload); update.Parameters.AddWithValue("room", room); update.Parameters.AddWithValue("id", row.Id);
        Check(await update.ExecuteNonQueryAsync() == 1, "Expected one isolated QA snapshot row.");
    }

    private static async Task<string> Digest(NpgsqlDataSource source, string room)
    {
        var rows = new StringBuilder();
        await using var connection = await source.OpenConnectionAsync();
        foreach (var table in Tables)
        {
            rows.AppendLine(table);
            await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection);
            query.Parameters.AddWithValue("room", room);
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.AppendLine(reader.GetString(0));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
    }

    private static bool Same(object? a, object? b)
        => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a, Json), JsonSerializer.SerializeToNode(b, Json));
    private static Task Write(string output, string name, object value)
        => File.WriteAllTextAsync(Path.Combine(output, name), JsonSerializer.Serialize(value, value.GetType(), Json));
    private static void Fresh(string output)
    {
        Check(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Use a fresh evidence directory; preserve previous failures.");
        Directory.CreateDirectory(output);
    }
    private static void Check(bool value, string? message)
    { if (!value) throw new InvalidOperationException(message); }
}
