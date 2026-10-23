using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class BattleWinnerPhaseSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private sealed record Case(string Kind, bool Pending = false) {
        internal string Key => Kind;
        internal string Scenario => "battle-winner-phase-" + Kind;
        internal bool Guardians => Kind is "guardians" or "final-conquest" or "result-burnout" or "control-observer";
        internal bool Defender => Kind == "defender";
        internal bool Skipped => Kind == "skipped-damage";
        internal bool Final => Kind == "final-conquest";
        internal bool Burnout => Kind == "result-burnout";
        internal bool Control => Kind == "control-observer";
        internal string HostNo => Defender ? "OGN·142/298" : "OGN·210/298";
        internal string AttackerNo => Defender ? "OGN·210/298" : "OGN·142/298";
    }
    private static readonly Case[] Cases = [new("plain"), new("guardians"), new("final-conquest"),
        new("result-burnout"), new("defender"), new("skipped-damage"), new("control-observer")];
    private static readonly Case[] NetworkCases = [Cases[0], Cases[5], new("control-observer", true), Cases[2], Cases[3]];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Fresh(output); var reports = new List<object>();
        foreach (var c in Cases) {
            var run = await Runner.Create(source, Path.Combine(output, c.Key), c.Key, c.Scenario);
            foreach (var step in Steps(c)) await Execute(run, step);
            CheckBoundary(run.State, c, run.P1, run.P2);
            var digest = await Digest(source, run.Room);
            var duplicate = await run.Session.SubmitAsync(run.LastActor!, run.LastIntent!, run.LastCommand!, run.LastRaw, default);
            Check(duplicate.Accepted && await Digest(source, run.Room) == digest, "C10 duplicate changed an exact seven-table row.");
            var frame = await run.Cold("complete-original-birth"); await run.Save(frame);
            CheckPhaseEvents(frame.Events, c, run.P1, run.P2);
            reports.Add(new { c.HostNo, c.Kind, run.Room, commands = frame.Commands.Count, events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(run.State), checkpoints = run.Checkpoints, completeOriginalBirthReplay = true });
        }
        await Write(output, "receipt.json", new { status = "REAL_POSTGRES_BATTLE_WINNER_PHASE_PASS", reports,
            scope = "Seven finite logged QA rooms: either combat winner, skipped damage, two-Guardian replacement, winning conquest, result burnout and separate result/control observers. Preset resources and explicit QA openings do not certify whole cards." });
    }

    private sealed record Step(string Actor, JsonElement Command, string? ChoiceId = null, string? HandId = null) {
        internal static Step For(string actor, GameCommand command) => new(actor, JsonSerializer.SerializeToElement(command, command.GetType(), Json));
        internal static Step Choose(string id) => new("$choice", JsonSerializer.SerializeToElement(new { cmdType = "PAY_COST" }, Json), ChoiceId: id);
        internal static Step Hand(string id) => new("$hand", JsonSerializer.SerializeToElement(new { cmdType = "CHOOSE_HAND_CARDS" }, Json), HandId: id);
    }
    private static Step[] Top() => [Step.For("$priority", new PassPriorityCommand()), Step.For("$priority", new PassPriorityCommand())];
    private static List<Step> Steps(Case c)
    {
        var steps = new List<Step> { Step.For("p1", new PlayCardCommand("HOST", c.HostNo, [], Destination: "BATTLEFIELD:HOME")) };
        if (c.Guardians) foreach (var gear in new[] { "ANGEL", "ANGEL-2" }) {
            steps.Add(Step.For("p1", new PlayCardCommand(gear, "SFD·051/221", [], Destination: "BASE")));
            steps.Add(Step.For("p1", new AssembleEquipmentCommand(gear, "HOST", ["ASSEMBLE_GREEN"]))); steps.AddRange(Top());
        }
        steps.Add(Step.For("p1", new EndTurnCommand()));
        var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        steps.Add(Step.For("p2", new TapRuneCommand(funding[0], funding)));
        steps.Add(Step.For("p2", new PlayCardCommand("ATTACKER", c.AttackerNo, [], Destination: "BASE")));
        steps.Add(Step.For("p2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"]))); steps.AddRange(Top());
        if (c.Skipped) steps.Add(Step.For("p2", new RecycleRuneCommand("P2-RED")));
        steps.Add(Step.For("p2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", [])));
        if (c.Skipped) { steps.Add(Step.For("p2", new PlayCardCommand("RAY", "OGN·009/298", ["HOST"]))); steps.AddRange(Top()); }
        steps.Add(Step.For("$focus", new PassFocusCommand())); steps.Add(Step.For("$focus", new PassFocusCommand()));
        if (!c.Skipped) steps.Add(Step.For("p2", new DeclareBattleCommand("HOME", ["ATTACKER"], ["HOST"], ["COMBAT_ASSIGNMENT"])));
        if (c.Guardians) steps.Add(Step.Choose("ATTACHED_GEAR:HOST:ANGEL-2"));
        if (c.Control || c.Burnout) steps.AddRange(Top());
        if (c.Control && !c.Pending) steps.Add(Step.Hand("P2-DRAW-1"));
        return steps;
    }

    private static async Task Execute(Runner run, Step step)
    {
        if (step.HandId is not null) {
            var hand = run.State.PendingHandChoice ?? throw new InvalidOperationException("Missing actual Zaun hand decision.");
            Check(hand.PlayerId == run.P2 && hand.LegalObjectIds.Contains(step.HandId), "Use the real controller's current legal hand choice.");
            await run.Submit(hand.PlayerId, new ChooseHandCardsCommand(hand.ChoiceId, hand.ChoiceWindow, [step.HandId])); return;
        }
        if (step.ChoiceId is null) { await run.Submit(step.Actor, GameCommandJsonMapper.Map(step.Command)); return; }
        var request = run.State.PendingRuleChoice?.Request ?? throw new InvalidOperationException("Missing actual two-Guardian replacement decision.");
        Check(request.Kind == "DESTRUCTION_REPLACEMENT" && request.PlayerId == run.P1, "The actual host controller must choose its source.");
        var option = request.Options.Single(o => o.Id == step.ChoiceId);
        await run.Submit(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
    }

    private static void CheckBoundary(MatchState state, Case c, string p1, string p2)
    {
        Check(state.PendingRuleChoice is null && state.PendingPayment is null && state.StackItems.Count == 0,
            "All destruction replacement and result-skill stack instructions must finish before this boundary.");
        var expectedController = c.Defender || c.Burnout ? p1 : p2;
        Check(state.CardObjects["HOME"].ControllerId == expectedController, "Only the completed control phase may change battlefield control.");
        Check(state.PlayerScores[p1] == (c.Burnout ? 8 : 0) && state.PlayerScores[p2] == (c.Final ? 8 : c.Defender || c.Burnout ? 0 : 1),
            "Keep exact conquest/hold/burnout scores, including final-point timing.");
        var terminal = c.Final || c.Burnout;
        Check(state.Status == (terminal ? MatchStatuses.Finished : MatchStatuses.InProgress)
            && state.WinnerPlayerId == (c.Final ? p2 : c.Burnout ? p1 : null), "Preserve the correct normal or burnout winner.");
        Check(state.BattleState.IsActive == (terminal || c.Pending), "Victory cuts off before battle close; pending control skills also keep the battle active.");
        if (terminal || c.Pending) Check(state.PendingBattleResult?.Stage == (c.Burnout ? "RESULT_TRIGGERS" : "CONTROL_TRIGGERS"),
            "Keep the exact result/control continuation at the cutoff.");
        else Check(state.PendingBattleResult is null, "Close an ordinary fully completed battle exactly once.");
        var survivor = c.Defender ? "HOST" : "ATTACKER";
        Check(state.CardObjects[survivor].Damage == 0 && state.PlayerZones[c.Defender ? p1 : p2].Battlefields.Contains(survivor),
            "CN 468.1 removes surviving combat damage before result skills and control.");
        if (c.Guardians) {
            var host = state.CardObjects["HOST"];
            Check(state.PlayerZones[p1].Base.Contains("HOST") && host.Damage == 0 && host.IsExhausted && host.ObjectGeneration == 1
                && state.PlayerZones[p1].Graveyard.Contains("ANGEL-2") && state.CardObjects["ANGEL"].AttachedToObjectId == "HOST"
                && state.PlayerZones[p1].Base.Contains("ANGEL") && state.DeathLedger.Counts.GetValueOrDefault(p1) == 0,
                "The real replacement preserves and recalls the same host with the surviving attachment; it did not die.");
        } else Check(state.PlayerZones[c.Defender ? p2 : p1].Graveyard.Contains(c.Defender ? "ATTACKER" : "HOST"),
            "The unprotected losing combatant really dies.");
        if (c.Pending) Check(state.PendingHandChoice is { RequiredCount: 1 } hand && hand.PlayerId == p2
            && hand.LegalObjectIds.Contains("P2-DRAW-1"), "Retain the original real controller's uncommitted Zaun hand choice.");
        else Check(state.PendingHandChoice is null, "No completed boundary may retain a hand decision.");
        if (terminal) Check(ResolutionResult.BuildPrompts(state).Values.All(prompt => !prompt.Actionable), "Terminal prompts must not offer a new action.");
        CheckPrivateViews(state);
    }

    private static string? Text(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch {
        string value => value, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
    private static void CheckPhaseEvents(IReadOnlyList<RecoveredEvent> events, Case c, string p1, string p2)
    {
        RecoveredEvent One(string kind) { var rows = events.Where(e => e.Event.Kind == kind).ToArray(); Check(rows.Length == 1, "Expected one actual " + kind); return rows[0]; }
        var result = One("BATTLE_RESULT_DETERMINED");
        if (!c.Skipped) Check(One("DAMAGE_REMOVED").Sequence < result.Sequence, "CN 468.1 cleanup must precede the result phase.");
        if (c.Skipped) Check(!events.Any(e => e.Event.Kind == "COMBAT_DAMAGE_ASSIGNED"), "The real Ray path skips combat damage.");
        if (c.Guardians) {
            var recall = One("UNIT_RECALLED_TO_BASE");
            Check(One("EQUIPMENT_DESTROYED").Sequence < recall.Sequence && recall.Sequence < result.Sequence
                && Text(recall.Event, "sourceObjectId") == "HOST", "Destroy the granting source, then recall, then determine the result.");
            Check(!events.Any(e => e.Event.Kind == "UNIT_DESTROYED" && Text(e.Event, "targetObjectId") == "HOST"), "The replaced host did not die.");
        }
        if (c.Control || c.Burnout) {
            var legend = One("LEGEND_TRIGGER_RESOLVED"); Check(Text(legend.Event, "trigger") == "BATTLE_WON_DRAW_ONE" && result.Sequence < legend.Sequence,
                "Resolve the one earned result draw after determining the result.");
            if (c.Burnout) Check(legend.Sequence < One("MATCH_WON").Sequence && One("BURNOUT_APPLIED").Sequence < One("MATCH_WON").Sequence,
                "Result draw burnout wins before any control instruction.");
            else Check(legend.Sequence < One("BATTLEFIELD_CONQUERED").Sequence, "Result skills finish before control/conquest skills.");
        } else Check(!events.Any(e => e.Event.Kind == "TRIGGER_QUEUED"), "An empty result queue must not manufacture a skill.");
        if (c.Defender || c.Burnout) Check(!events.Any(e => e.Event.Kind == "BATTLEFIELD_CONQUERED"), "No spurious conquest on defended or pre-control finished battle.");
        else Check(result.Sequence < One("BATTLEFIELD_CONQUERED").Sequence, "Result determination must precede conquest.");
        if (c.Final || c.Burnout || c.Pending) Check(!events.Any(e => e.Event.Kind == "BATTLE_CLOSED"), "Stop at the actual victory or pending-control cutoff.");
        else {
            var closed = One("BATTLE_CLOSED"); Check(result.Sequence < closed.Sequence, "Close after the result phase.");
            if (!c.Defender) Check(One("BATTLEFIELD_CONQUERED").Sequence < closed.Sequence, "Close after control/conquest.");
        }
        if (c.Final) Check(One("BATTLEFIELD_CONQUERED").Sequence < One("MATCH_WON").Sequence, "The winning conquest precedes terminal cutoff.");
        if (c.Control && !c.Pending) Check(One("BATTLEFIELD_CONQUERED").Sequence < One("CARD_DISCARDED").Sequence
            && One("CARD_DISCARDED").Sequence < One("BATTLE_CLOSED").Sequence && events.Count(e => e.Event.Kind == "CARD_DRAWN") == 3,
            "Zaun discards/draws once, after the result draw and before battle close.");
        if (c.Pending) Check(!events.Any(e => e.Event.Kind == "CARD_DISCARDED"), "Do not auto-commit the pending private hand choice.");
    }

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output); Check(!File.Exists(Path.Combine(output, "network-fixture.json")), "Use fresh C10 network rooms.");
        var cases = new List<object>();
        foreach (var c in NetworkCases) {
            var run = await Runner.Create(source, Path.Combine(output, "prepare-" + c.Key), c.Key, c.Scenario);
            var frame = await run.Cold("actual-seats-and-finite-seed");
            Check(frame.Commands.Count == 3 && frame.Commands.Take(2).All(cmd => cmd.CommandType == MatchSession.RoomSeatCommandType)
                && frame.Commands[2].CommandType == "DEV_SEED_SCENARIO:" + c.Scenario, "Use genuine logged room seats and a finite seed, not imported accepted history.");
            cases.Add(new { kind = c.Key, c.HostNo, c.Pending, room = run.Room, p1 = run.P1, p2 = run.P2,
                tokens = run.Tokens, credentials = run.Credentials, seedTick = run.State.Tick, ruleInputs = run.State.RuleInputs,
                initialHash = MatchStateHasher.Hash(run.State), steps = Steps(c), expectedAcceptedCommands = 3 + Steps(c).Count });
        }
        await Write(output, "network-fixture.json", new { kind = "FUTURE_CLIENT_INTENTS_NOT_ACCEPTED_HISTORY", cases,
            scope = "Five genuine two-identity finite QA rooms: plain/skipped winners, a pending control hand choice, normal winning conquest and result-draw burnout across actual API restart." });
    }
    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        Check(stage is "before-restart" or "after-restart", "Unknown C10 network stage.");
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = item.GetProperty("room").GetString()!; var c = NetworkCases.Single(c => c.Key == item.GetProperty("kind").GetString());
            var digest = await NetworkReconnectSqlEvidence.VerifyAsync(source, output, c.Key, room, stage,
                item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
            var frame = await LoadAndAudit(source, room);
            Check(await Digest(source, room) == digest, "Pure C10 recovery and original-birth audit must preserve exact seven-table rows.");
            CheckBoundary(frame.AuthoritativeState!, c, item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
            Check(frame.Commands.Count == item.GetProperty("expectedAcceptedCommands").GetInt32() + 1
                && frame.Commands.Count(cmd => !cmd.Accepted) == 1 && !frame.Commands[^1].Accepted
                && frame.Commands[^1].StartedTick == frame.Commands[^1].CompletedTick
                && frame.Commands[^1].StartedEventSequence == frame.Commands[^1].CompletedEventSequence, "Keep exactly the original action prefix and one real stale zero-event rejection.");
            Check(frame.Commands.All(cmd => Same(cmd.RuleInputs, frame.AuthoritativeState!.RuleInputs))
                && Same(item.GetProperty("ruleInputs"), frame.AuthoritativeState!.RuleInputs), "Never rebind original C10 actions to different runtime inputs.");
            await Write(output, c.Key + "-original-commands.json", frame.Commands); await Write(output, c.Key + "-original-events.json", frame.Events);
            reports.Add(new { room, c.Key, c.Pending, commands = frame.Commands.Count, events = frame.Events.Count,
                sevenTableDigest = digest, finalHash = MatchStateHasher.Hash(frame.AuthoritativeState!), originalBirthReplayMatched = true });
        }
        await Write(output, "sql-" + stage + ".json", new { status = "REAL_C10_NETWORK_SQL_ORIGINAL_BIRTH_PASS", stage, cases = reports });
    }

    private static readonly string[] TamperModes = ["wrong-winner", "future-result-tick", "forged-public-binding", "coherent-earlier-stage"];
    private static MatchState ForgeTerminalState(MatchState state, string mode)
    {
        var pending = state.PendingBattleResult ?? throw new InvalidOperationException("Missing original terminal result continuation.");
        var changed = mode switch {
            "wrong-winner" => pending with { WinnerPlayerId = pending.DefendingPlayerId },
            "future-result-tick" => pending with { ResultTick = state.Tick + 1 },
            "forged-public-binding" => pending with { PublicObjectBindings = [new ObjectBinding("FORGED-OBJECT", 1)] },
            "coherent-earlier-stage" => pending with { Stage = "RESULT_TRIGGERS" },
            _ => throw new InvalidOperationException("Unknown finite C10 terminal forgery.") };
        return state with { PendingBattleResult = changed };
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var item = fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("kind").GetString() == "final-conquest");
        var room = item.GetProperty("room").GetString()!;
        var original = await LoadAndAudit(source, room); var state = original.AuthoritativeState!; var pending = state.PendingBattleResult!;
        Check(state.Status == MatchStatuses.Finished && pending.Stage == "CONTROL_TRIGGERS", "Use the actual empty-result winning conquest cutoff.");
        var reports = new List<object>();
        foreach (var mode in TamperModes) {
            var coherent = mode == "coherent-earlier-stage";
            var forged = ForgeTerminalState(state, mode);

            string? digest = null, restoredDigest = null, rejection = null, registryCode = null;
            List<SavedRow> rows = []; var restoreErrors = new List<string>();
            bool? shapeConsistent = null; IReadOnlyList<string>? shapeErrors = null;
            var registryReturned = false; var restoredOriginalAudit = false; Exception? attemptFailure = null;
            try {
                digest = await Digest(source, room); rows = await CaptureLatestRows(source, room, state.Tick, original.LastEventSequence);
                var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
                foreach (var row in rows) {
                    object value = row.Table switch { "state_snapshots" => forged, "snapshots" => snapshots[row.Player!], _ => prompts[row.Player!] };
                    await UpdatePayload(source, room, row, JsonSerializer.Serialize(value, value.GetType(), Json));
                }
                var stored = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default)
                    ?? throw new InvalidOperationException("Missing poisoned C10 QA frame.");
                shapeConsistent = stored.IsConsistent; shapeErrors = stored.ValidationErrors;
                try { _ = await Registry(source).GetOrCreateAsync(room, default); registryReturned = true; }
                catch (MatchSessionException error) { registryCode = error.Code; rejection = error.Message; }
            }
            catch (Exception error) { attemptFailure = error; }
            finally {
                // Restore every captured row even if another restore fails; save
                // actual outcomes before enforcing any expected rejection layer.
                foreach (var row in rows) {
                    try { await UpdatePayload(source, room, row, row.Payload); }
                    catch (Exception error) { restoreErrors.Add(row.Table + ":" + row.Id + ": " + error); }
                }
                try { restoredDigest = await Digest(source, room); }
                catch (Exception error) { restoreErrors.Add("seven-table restore sampling: " + error); }
                if (digest is not null && restoredDigest == digest) {
                    try {
                        await LoadAndAudit(source, room);
                        restoredOriginalAudit = await Digest(source, room) == digest;
                    }
                    catch (Exception error) { restoreErrors.Add("restored original audit: " + error); }
                }
                var evidence = new TamperAttemptEvidence(room, mode, coherent, shapeConsistent, shapeErrors, registryReturned,
                    registryCode, rejection, attemptFailure?.ToString(), digest, restoredDigest, restoreErrors, restoredOriginalAudit);
                // This is intentionally before RequireTamperRejection. Failure,
                // wrong layer and failed restoration must remain inspectable.
                await SaveTamperAttempt(output, evidence, rows, forged, reports);
            }
        }
        var session = await Registry(source).GetOrCreateAsync(room, default);
        var digestAfterRestore = await Digest(source, room); var last = original.Commands.Last(cmd => cmd.Accepted);
        var duplicate = await session.SubmitAsync(last.PlayerId, last.ClientIntentId, GameCommandJsonMapper.Map(last.RawCommand!.Value), last.RawCommand.Value, default);
        Check(duplicate.Accepted && await Digest(source, room) == digestAfterRestore, "The original winning intent remains exactly idempotent after tamper restoration.");
        var command = new EndTurnCommand(); var actor = state.WinnerPlayerId!;
        var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject(); raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = state.Tick;
        string? terminalCode = null;
        try { await session.SubmitAsync(actor, "c10-terminal-new-intent-after-tamper", command, JsonSerializer.SerializeToElement(raw), default); }
        catch (MatchSessionException error) { terminalCode = error.Code; }
        Check(terminalCode == ErrorCodes.MatchFinished && await Digest(source, room) == digestAfterRestore,
            "A fresh terminal intent must fail with MATCH_FINISHED before writing any row.");
        var restored = await LoadAndAudit(source, room);
        Check(Same(restored.Commands, original.Commands) && Same(restored.Events, original.Events), "Never rewrite the terminal original journal.");

        var controlItem = fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("pending").GetBoolean());
        var controlRoom = controlItem.GetProperty("room").GetString()!; var controlOriginal = await LoadAndAudit(source, controlRoom);
        var controlSession = await Registry(source).GetOrCreateAsync(controlRoom, default); var hand = controlOriginal.AuthoritativeState!.PendingHandChoice!;
        var choose = new ChooseHandCardsCommand(hand.ChoiceId, hand.ChoiceWindow, ["P2-DRAW-1"]);
        var chooseRaw = JsonSerializer.SerializeToNode(choose, Json)!.AsObject(); chooseRaw["promptId"] = controlSession.PromptFor(hand.PlayerId).PromptId;
        chooseRaw["snapshotTick"] = controlOriginal.CurrentTick;
        var completion = await controlSession.SubmitAsync(hand.PlayerId, "c10-original-control-completion", choose, JsonSerializer.SerializeToElement(chooseRaw), default);
        Check(completion.Accepted, completion.ErrorMessage); var completed = await LoadAndAudit(source, controlRoom);
        Check(completed.Commands.Count == controlOriginal.Commands.Count + 1 && Same(completed.Commands.Take(controlOriginal.Commands.Count).ToArray(), controlOriginal.Commands)
            && Same(completed.Events.Take(controlOriginal.Events.Count).ToArray(), controlOriginal.Events), "Legitimate control choice appends to the untouched original pending journal.");
        var completedCase = new Case("control-observer"); var p1 = controlItem.GetProperty("p1").GetString()!; var p2 = controlItem.GetProperty("p2").GetString()!;
        CheckBoundary(completion.State, completedCase, p1, p2); CheckPhaseEvents(completed.Events, completedCase, p1, p2);
        await Write(output, "registry-tamper-results.json", new { status = "REAL_POSTGRES_C10_TERMINAL_TAMPER_REJECTED_AND_ORIGINAL_CONTROL_COMPLETED", reports,
            terminalDuplicateUnchanged = true, terminalCode, completedCommands = completed.Commands.Count, completeOriginalBirthReplayMatched = true });
    }

    private sealed record TamperAttemptEvidence(string Room, string Mode, bool Coherent, bool? ShapeConsistent,
        IReadOnlyList<string>? ShapeErrors, bool RegistryReturned, string? RegistryCode, string? Rejection,
        string? Exception, string? OriginalDigest, string? RestoredDigest, IReadOnlyList<string> RestoreErrors,
        bool RestoredOriginalAudit);

    private static async Task SaveTamperAttempt(string output, TamperAttemptEvidence evidence, object capturedRows,
        MatchState forged, List<object> reports)
    {
        await Write(output, evidence.Mode + "-tamper-attempt.json", new { evidence, capturedRows, forgedState = forged });
        reports.Add(evidence); await Write(output, "registry-tamper-attempts.json", reports);
        RequireTamperRejection(evidence);
    }

    private static void RequireTamperRejection(TamperAttemptEvidence evidence)
    {
        Check(evidence.Exception is null, "C10 tamper operation failed; inspect its saved actual exception: " + evidence.Exception);
        Check(evidence.OriginalDigest is not null && evidence.OriginalDigest == evidence.RestoredDigest
            && evidence.RestoreErrors.Count == 0 && evidence.RestoredOriginalAudit,
            "C10 original projection rows and exact seven-table audit were not fully restored; inspect the saved restore results.");
        Check(evidence.ShapeConsistent == evidence.Coherent, "C10 actual shape rejection layer differs from the saved expectation.");
        if (!evidence.Coherent) Check(evidence.ShapeErrors?.Contains("invalid captured battle result continuation") == true,
            "C10 original request/origin structural guard did not reject this forged pending state.");
        Check(!evidence.RegistryReturned && evidence.RegistryCode == ErrorCodes.RecoveryInconsistent,
            "C10 registry did not reject this forged pending state with the expected code.");
        Check(evidence.Rejection?.Contains(evidence.Coherent ? "action-log audit failed" : "invalid captured battle result continuation",
            StringComparison.Ordinal) == true, "C10 registry rejected at a different layer; inspect the saved actual message.");
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
            var run = new Runner(source, output, "c10-" + kind + "-" + identity, "c10-" + identity + "-a", "c10-" + identity + "-b");
            run.Session = new MatchSession(run.Room, run.engine, run.journal, run.players);
            foreach (var actor in new[] { run.P1, run.P2 })
            {
                run.Tokens[actor] = (await run.Session.EnsurePlayerAsync(actor, default)).ReconnectToken;
                run.Credentials[actor] = "c10-key-" + actor;
            }
            var seeded = await run.Session.SeedScenarioAsync(run.P1, "c10-original-qa-seed", scenario,
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
            var raw = Raw(actor, command); var id = "c10-action-" + submitted++;
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
        var privateIds = state.PlayerZones.Values.SelectMany(z => z.MainDeck.Concat(z.RuneDeck))
            .Where(id => id.StartsWith("PRIVATE-", StringComparison.Ordinal)).ToArray();
        foreach (var actor in state.Seats.Keys) {
            var view = JsonSerializer.Serialize(new { snapshot = ResolutionResult.BuildSnapshots(state)[actor], prompt = ResolutionResult.BuildPrompts(state)[actor] }, Json);
            Check(privateIds.All(id => !view.Contains(JsonSerializer.Serialize(id), StringComparison.Ordinal))
                && !view.Contains("reconnectToken", StringComparison.Ordinal) && !view.Contains("openingSeed", StringComparison.Ordinal),
                "Still-private deck identities or server credentials escaped in a current view.");
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
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var readOnly = new NpgsqlCommand("set transaction read only", connection, transaction))
            await readOnly.ExecuteNonQueryAsync();
        foreach (var table in Tables)
        {
            rows.AppendLine(table);
            await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection, transaction);
            query.Parameters.AddWithValue("room", room);
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.AppendLine(reader.GetString(0));
        }
        await transaction.CommitAsync();
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
