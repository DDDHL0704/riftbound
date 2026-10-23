using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class EmpoweredTargetReplacementSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private sealed record Case(string Kind, string Target, string CardNo) {
        internal string Key => Kind + "-" + Target[^1].ToString().ToLowerInvariant();
        internal string Scenario => Kind == "numeric" ? "empowered-target-action-overflow" : "empowered-target-action-replacement";
    }
    private static readonly Case[] Cases = new[] { "stun", "negative-order", "return", "echo", "hidden-source", "numeric" }
        .SelectMany(kind => new[] { new Case(kind, "PLANK-A", "VEN·086"), new Case(kind, "PLANK-B", "VEN·181") }).ToArray();

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Fresh(output); var reports = new List<object>();
        foreach (var c in Cases) {
            var run = await Runner.Create(source, Path.Combine(output, c.Key), c.Key, c.Scenario);
            if (c.Kind == "numeric") await NumericRejection(run, c);
            else {
                foreach (var step in Steps(c)) await Execute(run, step);
                CheckBoundary(run.State, c, run.P1, run.P2);
                var digest = await Digest(source, run.Room);
                var duplicate = await run.Session.SubmitAsync(run.LastActor!, run.LastIntent!, run.LastCommand!, run.LastRaw, default);
                Check(duplicate.Accepted && await Digest(source, run.Room) == digest, "Completed C7 intent changed one of seven SQL tables.");
            }
            var frame = await run.Cold("final-complete-original-journal"); await run.Save(frame);
            reports.Add(new { c.Kind, c.CardNo, c.Target, run.Room, commands = frame.Commands.Count,
                accepted = frame.Commands.Count(cmd => cmd.Accepted), events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(run.State), checkpoints = run.Checkpoints, completeOriginalBirthReplay = true });
        }
        await Write(output, "receipt.json", new { status = "REAL_POSTGRES_EMPOWERED_TARGET_REPLACEMENT_PASS", reports,
            scope = "Twelve independent logged finite QA rooms. Actual play/Empower for ordinary cases; numeric cases are explicit representation QA. Not ordinary openings, whole cards, or complete backend certification." });
    }

    private sealed record Step(string Actor, JsonElement Command, string? ChoiceHint = null) {
        internal static Step For(string actor, GameCommand command) => new(actor, JsonSerializer.SerializeToElement(command, command.GetType(), Json));
        internal static Step Choose(string hint) => new("$choice", JsonSerializer.SerializeToElement(new { cmdType = "PAY_COST" }, Json), hint);
    }
    private static Step[] Top() => [Step.For("$priority", new PassPriorityCommand()), Step.For("$priority", new PassPriorityCommand())];
    private static List<Step> Empower(Case c, string destination) => [
        Step.For("p1", new PlayCardCommand(c.Target, c.CardNo, [], Destination: destination)),
        Step.For("p1", new ActivateAbilityCommand(c.Target, "EMPOWER:0:0", [])), ..Top()];
    private static List<Step> Steps(Case c)
    {
        if (c.Kind == "echo") return [..Empower(c, "BASE"),
            Step.For("p1", new PlayCardCommand("GEAR", "SFD·059/221", [], Destination: "BASE")),
            Step.For("p1", new AssembleEquipmentCommand("GEAR", c.Target, ["ASSEMBLE_GREEN"])), ..Top(),
            Step.For("p1", new PlayCardCommand("READY", "OGN·146/298", [c.Target])), ..Top(),
            Step.For("p1", new MoveUnitCommand(c.Target, "BASE", "BATTLEFIELD:OTHER", [])), Step.For("p1", new PassFocusCommand()),
            Step.For("p2", new PlayCardCommand("ECHO", "SFD·040/221", [c.Target], OptionalCosts: ["ECHO"], RepeatChoices: [new("", [c.Target])])),
            ..Top(), Step.Choose("PRINTED:"), Step.Choose("ATTACHED:")];
        if (c.Kind == "hidden-source") return [
            Step.For("p1", new PlayCardCommand(c.Target, c.CardNo, [], Destination: "BATTLEFIELD:HOME")),
            Step.For("p1", new PlayCardCommand("SMOKE", "OGN·093/298", [c.Target])), ..Top(),
            Step.For("p1", new ActivateAbilityCommand(c.Target, "EMPOWER:0:0", [])), ..Top(),
            Step.For("p1", new PlayCardCommand("PRIVATE-SOURCE", "OGN·051/298", [c.Target], Destination: "BASE")),
            Step.For("p1", new PlayCardCommand("RETURN", "OGN·104/298", ["PRIVATE-SOURCE"])), ..Top(), ..Top()];
        var steps = Empower(c, "BATTLEFIELD:HOME");
        if (c.Kind == "return") return [..steps, Step.For("p1", new PlayCardCommand("RETURN", "OGN·104/298", [c.Target])), ..Top()];
        if (c.Kind == "stun") return [..steps,
            Step.For("p1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [])), Step.For("p1", new PassFocusCommand()),
            Step.For("p2", new PlayCardCommand("STUN-P2", "OGN·050/298", [c.Target])), ..Top()];
        if (c.Kind == "negative-order") {
            var runes = Enumerable.Range(1, 8).Select(i => "P2-FUND-" + i).ToArray();
            return [..steps, Step.For("p1", new EndTurnCommand()), Step.For("p2", new TapRuneCommand(runes[0], runes)),
                Step.For("p2", new RecycleRuneCommand(runes[0])), Step.For("p2", new PlayCardCommand("MEL", "VEN·069", [], Destination: "BASE")), ..Top(),
                Step.For("p2", new ActivateAbilityCommand("MEL", "EMPOWER:0:0", [])), ..Top(),
                Step.For("p2", new PlayCardCommand("SHAKEDOWN", "OGN·095/298", [c.Target])), ..Top(), Step.Choose("CASTER_ADDITIONAL_NEGATIVE_MIGHT")];
        }
        throw new InvalidOperationException("Numeric QA has its own same-session rejection path.");
    }
    private static async Task Execute(Runner run, Step step)
    {
        if (step.ChoiceHint is null) { await run.Submit(step.Actor, GameCommandJsonMapper.Map(step.Command)); return; }
        var request = run.State.PendingRuleChoice?.Request ?? throw new InvalidOperationException("Missing actual C7 synchronous decision.");
        Check(request.Kind == "TARGETED_ACTION_REPLACEMENT" && request.PlayerId == run.P1, "Affected unit controller must choose.");
        var option = request.Options.Single(o => o.Id.Contains(step.ChoiceHint, StringComparison.Ordinal));
        await run.Submit(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
    }

    private static void CheckBoundary(MatchState state, Case c, string p1, string p2)
    {
        var unit = state.CardObjects[c.Target];
        Check(unit.Power == (c.Kind == "echo" ? 12 : c.Kind == "hidden-source" ? 5 : 9)
            && unit.IsEmpowered && unit.ObjectGeneration == 1 && unit.ControllerId == p1 && unit.OwnerId == p1
            && !unit.UntilEndOfTurnEffects.Contains("STUNNED"), "Literal C7 replacement result, controller, generation or status mismatch.");
        var modifiers = unit.UntilEndOfTurnPowerModifiers.Where(m => m.ChosenActionReplacement is not null).ToArray();
        Check(modifiers.Length == (c.Kind == "echo" ? 2 : 1) && modifiers.All(m => m.PowerDelta == 3 && m.RequestedPowerDelta == 3
            && m.MinimumPower == 0 && m.SnapshotMight is null && m.ChosenNegativeMight is null
            && m.SourceObjectId == c.Target && m.SourceCardNo == c.CardNo), "Each inherited chosen action commits exactly one target-owned positive +3 ledger.");
        foreach (var modifier in modifiers) {
            var witness = modifier.ChosenActionReplacement!;
            var source = c.Kind switch { "stun" => "STUN-P2", "negative-order" => "SHAKEDOWN", "return" => "RETURN", "echo" => "ECHO", _ => "PRIVATE-SOURCE" };
            var kind = c.Kind == "return" ? "RETURN_TO_HAND" : c.Kind == "negative-order" ? "NEGATIVE_MIGHT" : "STUN";
            Check(witness.SourceObjectId == source && witness.OriginalKind == kind && witness.Target == new ObjectBinding(c.Target, 1)
                && witness.EffectControllerId == (c.Kind is "stun" or "negative-order" or "echo" ? p2 : p1)
                && witness.Steps[^1].Rule.Source == witness.Target && witness.Steps[^1].Rule.ControllerId == p1,
                "Private original origin and the target replacement controller must remain independently captured.");
            Check(modifier.EffectId == $"TARGETED-ACTION:{witness.AppliedTick}:{witness.Occurrence}:{c.Target}:1:{modifier.AppliedOrder}",
                "Public effect identity must bind public target/action metadata while its original stack remains private.");
        }
        if (c.Kind == "negative-order") {
            var witness = modifiers[0].ChosenActionReplacement!;
            Check(witness.RequestedDelta == -1 && witness.NormalizedDelta == -1 && witness.MinimumMight == 1 && witness.SnapshotMight == 6
                && witness.Steps.Count == 2 && witness.Steps[0].Rule.Source.ObjectId == "MEL" && witness.Steps[0].AfterDelta == -2,
                "Affected controller's caster-first answer must retain both private replacement steps.");
        }
        if (c.Kind == "echo") Check(modifiers.Select(m => m.ChosenActionReplacement!.Occurrence).SequenceEqual(new[] { 0, 1 })
            && modifiers[0].ChosenActionReplacement!.AppliedTick == modifiers[1].ChosenActionReplacement!.AppliedTick
            && modifiers[0].ChosenActionReplacement!.Steps[^1].Rule.Origin is null
            && modifiers[1].ChosenActionReplacement!.Steps[^1].Rule.Origin?.Equipment.ObjectId == "GEAR",
            "Both actual Echo actions must survive consecutive decisions with stable instruction time and independent copied rule origins.");
        if (c.Kind == "return") Check(state.PlayerZones[p1].RuneDeck.SequenceEqual(new[] { "P1-RUNE-2" })
            && state.PlayerZones[p1].Base.Contains("P1-RUNE-1") && state.CardObjects["P1-RUNE-1"].IsExhausted,
            "Replaced legal return must preserve OGN104's independent owner rune instruction without a Ghost Bay return trigger.");
        if (c.Kind == "hidden-source") {
            Check(state.PlayerZones[p1].Hand.Contains("PRIVATE-SOURCE") && state.CardObjects["PRIVATE-SOURCE"].ObjectGeneration == 2,
                "The real reacted-to OGN051 source must actually be back in its hidden hand.");
            var mighty = state.StackItems.Single(i => i.MightySkill is not null).MightySkill!;
            Check(mighty.BeforeMight == 2 && mighty.AfterMight == 5 && mighty.ControllerId == p1,
                "Only the affected unit controller's real Mighty observer may trigger.");
            foreach (var view in ResolutionResult.BuildSnapshots(state).Values)
                Check(!JsonSerializer.Serialize(new { view.Stack, view.Timing }, Json).Contains("PRIVATE-SOURCE", StringComparison.Ordinal),
                    "Public stack/timing, including nested Might frames, must not retain private source IDs in values or effect IDs.");
            Check(!JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)[p2], Json).Contains("PRIVATE-SOURCE", StringComparison.Ordinal),
                "The opponent view must not expose the current private source identity.");
        }
        foreach (var actor in state.Seats.Keys) Check(!HasPrivateField(JsonSerializer.SerializeToElement(new {
            snapshot = ResolutionResult.BuildSnapshots(state)[actor], prompt = ResolutionResult.BuildPrompts(state)[actor] }, Json)),
            "No public JSON object/array may carry the private replacement witness field.");
        Check(!HasPrivateField(JsonSerializer.SerializeToElement(MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state), Json)),
            "The spectator projection must not contain the private replacement witness.");
    }
    private static bool HasPrivateField(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Object => value.EnumerateObject().Any(p => p.Name.Equals("chosenActionReplacement", StringComparison.OrdinalIgnoreCase) || HasPrivateField(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Any(HasPrivateField), _ => false };

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        Check(!File.Exists(Path.Combine(output, "network-fixture.json")), "Use a fresh C7 network fixture.");
        var cases = new List<object>();
        foreach (var c in Cases.Where(c => c.Kind is "echo" or "hidden-source")) {
            var run = await Runner.Create(source, Path.Combine(output, "prepare-" + c.Key), c.Key, c.Scenario);
            var frame = await run.Cold("actual-seats-and-finite-seed");
            Check(frame.Commands.Count == 3 && frame.Commands.Take(2).All(cmd => cmd.CommandType == MatchSession.RoomSeatCommandType)
                && frame.Commands[2].CommandType == "DEV_SEED_SCENARIO:" + c.Scenario,
                "Network preparation must create genuine seats and a recorded finite seed, never import accepted history.");
            cases.Add(new { kind = c.Key, mechanism = c.Kind, c.Target, c.CardNo, room = run.Room, p1 = run.P1, p2 = run.P2,
                tokens = run.Tokens, credentials = run.Credentials, seedTick = run.State.Tick, ruleInputs = run.State.RuleInputs,
                initialHash = MatchStateHasher.Hash(run.State), steps = Steps(c), expectedAcceptedCommands = 3 + Steps(c).Count });
        }
        await Write(output, "network-fixture.json", new { kind = "FUTURE_CLIENT_INTENTS_NOT_ACCEPTED_HISTORY", cases,
            scope = "Four independent explicit QA rooms, both printings, two real identities each. Client decisions use only server payment metadata; no accepted rows imported." });
    }

    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        Check(stage is "before-restart" or "after-restart", "Unknown C7 verification stage.");
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
            var c = new Case(item.GetProperty("mechanism").GetString()!, item.GetProperty("target").GetString()!, item.GetProperty("cardNo").GetString()!);
            var room = item.GetProperty("room").GetString()!; var frame = await LoadAndAudit(source, room);
            CheckBoundary(frame.AuthoritativeState!, c, item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
            Check(frame.Commands.Count == item.GetProperty("expectedAcceptedCommands").GetInt32() + 1
                && frame.Commands.Count(cmd => !cmd.Accepted) == 1 && !frame.Commands[^1].Accepted
                && frame.Commands[^1].StartedTick == frame.Commands[^1].CompletedTick
                && frame.Commands[^1].StartedEventSequence == frame.Commands[^1].CompletedEventSequence,
                "Keep exactly the genuine seat/seed/action prefix and one zero-event stale rejection.");
            Check(frame.Commands.All(cmd => Same(cmd.RuleInputs, frame.AuthoritativeState!.RuleInputs))
                && Same(item.GetProperty("ruleInputs"), frame.AuthoritativeState!.RuleInputs), "Never rebind old accepted RuleInputs to a new runtime.");
            var digest = await Digest(source, room);
            if (stage == "after-restart") {
                using var before = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "sql-before-restart.json")));
                var prior = before.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("room").GetString() == room);
                Check(prior.GetProperty("sevenTableDigest").GetString() == digest,
                    "Actual process restart, reconnect and duplicate must not rewrite any of seven SQL tables.");
            }
            await Write(output, c.Key + "-original-commands.json", frame.Commands);
            await Write(output, c.Key + "-original-events.json", frame.Events);
            reports.Add(new { c.Kind, c.CardNo, room, commands = frame.Commands.Count, events = frame.Events.Count,
                sevenTableDigest = digest, finalHash = MatchStateHasher.Hash(frame.AuthoritativeState!), originalBirthReplayMatched = true,
                actualRegistryRecoveryMatched = true });
        }
        await Write(output, "sql-" + stage + ".json", new { status = "REAL_C7_NETWORK_SQL_ORIGINAL_BIRTH_PASS", stage, cases = reports,
            scope = "Four finite QA rooms only. A true API restart requires the external runner's two process identities; this helper does not manage a server." });
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = item.GetProperty("room").GetString()!; var original = await LoadAndAudit(source, room); var state = original.AuthoritativeState!;
            var c = new Case(item.GetProperty("mechanism").GetString()!, item.GetProperty("target").GetString()!, item.GetProperty("cardNo").GetString()!);
            foreach (var mode in new[] { "missing-witness", "wrong-public-id", "forged-private-stack", "extra-positive" }) {
                var unit = state.CardObjects[c.Target]; var ledger = unit.UntilEndOfTurnPowerModifiers.ToArray();
                var index = Array.FindLastIndex(ledger, m => m.ChosenActionReplacement is not null);
                var modifier = ledger[index]; var context = modifier.ChosenActionReplacement!;
                var forgedUnit = unit;
                if (mode == "extra-positive") {
                    var order = ledger.Max(m => m.AppliedOrder!.Value) + 1;
                    var extraContext = context with { Occurrence = context.Occurrence + 1 };
                    var extra = modifier with { AppliedOrder = order, EffectivePower = unit.Power + 3, ResultingPower = unit.Power + 3,
                        EffectId = $"TARGETED-ACTION:{context.AppliedTick}:{extraContext.Occurrence}:{c.Target}:{unit.ObjectGeneration}:{order}",
                        ChosenActionReplacement = extraContext };
                    forgedUnit = unit with { Power = unit.Power + 3, UntilEndOfTurnPowerModifier = unit.UntilEndOfTurnPowerModifier + 3,
                        UntilEndOfTurnPowerModifiers = [..ledger, extra] };
                } else {
                    ledger[index] = mode switch {
                        "missing-witness" => modifier with { ChosenActionReplacement = null },
                        "wrong-public-id" => modifier with { EffectId = modifier.EffectId + ":FORGED" },
                        _ => modifier with { ChosenActionReplacement = context with { StackItemId = "FORGED-ORIGINAL-STACK" } }
                    };
                    forgedUnit = unit with { UntilEndOfTurnPowerModifiers = ledger };
                }
                var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [c.Target] = forgedUnit } };
                var digest = await Digest(source, room); var rows = await CaptureLatestRows(source, room, state.Tick, original.LastEventSequence);
                var coherent = mode is "forged-private-stack" or "extra-positive"; string? rejection = null; var shapeConsistent = false;
                try {
                    var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
                    foreach (var row in rows) {
                        object value = row.Table switch { "state_snapshots" => forged, "snapshots" => snapshots[row.Player!], _ => prompts[row.Player!] };
                        await UpdatePayload(source, room, row, JsonSerializer.Serialize(value, value.GetType(), Json));
                    }
                    var stored = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing poisoned QA frame.");
                    shapeConsistent = stored.IsConsistent;
                    Check(shapeConsistent == coherent, "Tamper must reach its declared shape-vs-original-birth rejection layer.");
                    if (!coherent) Check(stored.ValidationErrors.Contains("invalid captured chosen action replacement"), "Strict witness/public-ID shape guard was bypassed.");
                    try { _ = await Registry(source).GetOrCreateAsync(room, default); }
                    catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { rejection = error.Message; }
                    Check(rejection is not null, "The real PostgreSQL registry accepted a forged C7 authoritative state.");
                    if (coherent) Check(rejection!.Contains("action-log audit failed", StringComparison.Ordinal),
                        "A shape-valid forgery must fail through the unchanged original-action replay, not an unrelated shape error.");
                }
                finally { foreach (var row in rows) await UpdatePayload(source, room, row, row.Payload); }
                Check(await Digest(source, room) == digest, "Restore every temporary QA SQL row exactly.");
                await LoadAndAudit(source, room);
                reports.Add(new { c.Kind, c.CardNo, room, mode, shapeConsistent, rejection,
                    originalAcceptedCommandsNeverRewritten = true, originalSevenTableDigestRestored = true });
            }
        }
        await Write(output, "registry-tamper-results.json", new { status = "REAL_POSTGRES_C7_TAMPER_REJECTED", reports });
    }

    private static async Task NumericRejection(Runner run, Case c)
    {
        await run.Submit("p1", new PlayCardCommand("STUN-P1", "OGN·050/298", [c.Target]));
        await run.Submit("$priority", new PassPriorityCommand());
        var session = run.Session; var before = run.State;
        var original = await new PostgresMatchRecoveryStore(run.Source).LoadAsync(run.Room, default)
            ?? throw new InvalidOperationException("Missing numeric accepted prefix.");
        var actor = before.PriorityPlayerId!; var command = new PassPriorityCommand(); var raw = run.Raw(actor, command);
        var result = await session.SubmitAsync(actor, "c7-numeric-unsupported", command, raw, default);
        Check(!result.Accepted && result.ErrorCode == ErrorCodes.UnsupportedCardBehavior && result.Events.Count == 0
            && MatchStateHasher.Hash(result.State) == MatchStateHasher.Hash(before) && result.State.RngCursor == before.RngCursor
            && result.State.PriorityPlayerId == before.PriorityPlayerId && result.State.ActivePlayerId == before.ActivePlayerId
            && Same(result.State.RunePools, before.RunePools) && Same(result.Snapshots, ResolutionResult.BuildSnapshots(before))
            && Same(result.Prompts, ResolutionResult.BuildPrompts(before)), "Checked +3 overflow must roll back every state/resource/RNG/private-view field.");
        var rejected = await new PostgresMatchRecoveryStore(run.Source).LoadAsync(run.Room, default)
            ?? throw new InvalidOperationException("Missing numeric rejection row.");
        Check(rejected.IsConsistent && rejected.Commands.Count == original.Commands.Count + 1 && !rejected.Commands[^1].Accepted
            && rejected.Commands[^1].StartedTick == rejected.Commands[^1].CompletedTick
            && rejected.Commands[^1].StartedEventSequence == rejected.Commands[^1].CompletedEventSequence
            && Same(rejected.Commands.Take(original.Commands.Count).ToArray(), original.Commands) && Same(rejected.Events, original.Events),
            "Keep the exact original prefix and one persisted zero-event numeric rejection.");
        var digest = await Digest(run.Source, run.Room);
        var duplicate = await session.SubmitAsync(actor, "c7-numeric-unsupported", command, raw, default);
        Check(!duplicate.Accepted && duplicate.ErrorCode == result.ErrorCode && await Digest(run.Source, run.Room) == digest,
            "Repeated rejected intent must not append any SQL row.");
        Check(ReferenceEquals(session, run.Session), "Do not cold-restore before the same-session legal follow-up.");
        var surrendered = await run.Submit("p1", new SurrenderCommand(), cold: false);
        Check(ReferenceEquals(session, run.Session) && run.State.Status == MatchStatuses.Finished && run.State.WinnerPlayerId == run.P2
            && Same(run.State.BattleState, before.BattleState) && !run.State.UntilEndOfTurnEffects.Any(e => e.StartsWith("BATTLE_DAMAGE_ASSIGNMENT_LEDGER:", StringComparison.Ordinal))
            && surrendered.Events.Count == 1 && surrendered.Events[0].Kind == "MATCH_WON"
            && surrendered.Prompts.Values.All(p => !p.Actionable && p.Actions.SequenceEqual(new[] { "WAIT" }) && p.View?.Type == "MATCH_RESULT"),
            "The actual rejecting session must accept surrender, award its opponent, retain battle history and end all player action.");
        var frame = await run.Cold("same-rejecting-session-surrender");
        Check(frame.Commands.Count == original.Commands.Count + 2 && frame.Commands[^1].Accepted
            && frame.Commands[^1].CommandType == CommandTypes.Surrender && Same(frame.Commands[^2], rejected.Commands[^1])
            && Same(frame.Commands.Take(original.Commands.Count).ToArray(), original.Commands)
            && frame.Events.Count == original.Events.Count + 1 && Same(frame.Events.Take(original.Events.Count).ToArray(), original.Events),
            "Full SQL birth replay must retain accepted prefix, numeric rejection and the same-session surrender in original order.");
        await Write(run.Output, "numeric-rejection.json", new { result.ErrorCode, sameRejectingSessionAcceptedSurrender = true,
            repeatedRejectionAppendedNoRows = true, originalBirthReplayMatched = true,
            scope = "Explicit registered-card numeric representation QA; not a natural deck path." });
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
            var run = new Runner(source, output, "c7-" + kind + "-" + identity, "c7-" + identity + "-a", "c7-" + identity + "-b");
            run.Session = new MatchSession(run.Room, run.engine, run.journal, run.players);
            foreach (var actor in new[] { run.P1, run.P2 })
            {
                run.Tokens[actor] = (await run.Session.EnsurePlayerAsync(actor, default)).ReconnectToken;
                run.Credentials[actor] = "c7-key-" + actor;
            }
            var seeded = await run.Session.SeedScenarioAsync(run.P1, "c7-original-qa-seed", scenario,
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
            var raw = Raw(actor, command); var id = "c7-action-" + submitted++;
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
