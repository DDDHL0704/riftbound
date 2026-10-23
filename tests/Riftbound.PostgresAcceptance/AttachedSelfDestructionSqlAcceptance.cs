using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class AttachedSelfDestructionSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private sealed record Case(string Kind, string Scenario, string HostNo, bool Pending = false, string Selected = "ANGEL") {
        internal string Key => Kind;
        internal bool Double => Scenario == "guardian-angel-double";
        internal bool Control => Kind == "control-group";
        internal bool Lethal => Kind == "lethal-last-breath";
    }
    private static readonly Case[] Cases = [
        new("single", "guardian-angel-single", "OGN·210/298"),
        new("double-first", "guardian-angel-double", "OGN·210/298"),
        new("double-second", "guardian-angel-double", "OGN·210/298", Selected: "ANGEL-2"),
        new("lethal-last-breath", "guardian-angel-lethal-last-breath", "OGN·096/298"),
        new("control-group", "guardian-angel-control-group", "SFD·173/221")
    ];
    private static readonly Case[] NetworkCases = [Cases[0],
        new("pending-double", "guardian-angel-double", "OGN·210/298", true, "ANGEL-2"), Cases[4]];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Fresh(output); var reports = new List<object>();
        foreach (var c in Cases) {
            var run = await Runner.Create(source, Path.Combine(output, c.Key), c.Key, c.Scenario);
            foreach (var step in Steps(c)) await Execute(run, step);
            CheckBoundary(run.State, c, run.P1, run.P2);
            var digest = await Digest(source, run.Room);
            var duplicate = await run.Session.SubmitAsync(run.LastActor!, run.LastIntent!, run.LastCommand!, run.LastRaw, default);
            Check(duplicate.Accepted && await Digest(source, run.Room) == digest, "C9 duplicate changed an exact seven-table row.");
            var frame = await run.Cold("complete-original-birth"); await run.Save(frame);
            CheckGuardianEvents(frame.Events, c, run.P1, run.P2);
            reports.Add(new { c.HostNo, c.Kind, run.Room, commands = frame.Commands.Count, events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(run.State), checkpoints = run.Checkpoints, completeOriginalBirthReplay = true });
        }
        await Write(output, "receipt.json", new { status = "REAL_POSTGRES_ATTACHED_SELF_DESTRUCTION_PASS", reports,
            scope = "Five finite logged QA rooms: real Guardian attachment, either of two sources, lethal Last Breath and foreign-controlled attached group. Preset resources and explicit QA openings do not certify whole cards." });
    }

    private sealed record Step(string Actor, JsonElement Command, string? ChoiceId = null) {
        internal static Step For(string actor, GameCommand command) => new(actor, JsonSerializer.SerializeToElement(command, command.GetType(), Json));
        internal static Step Choose(string id) => new("$choice", JsonSerializer.SerializeToElement(new { cmdType = "PAY_COST" }, Json), ChoiceId: id);
    }
    private static Step[] Top() => [Step.For("$priority", new PassPriorityCommand()), Step.For("$priority", new PassPriorityCommand())];
    private static List<Step> Steps(Case c)
    {
        var steps = new List<Step> {
            Step.For("p1", new PlayCardCommand("HOST", c.HostNo, [], Destination: c.Control || c.Lethal ? "BATTLEFIELD:HOME" : "BASE")),
            Step.For("p1", new PlayCardCommand("ANGEL", "SFD·051/221", [], Destination: "BASE")),
            Step.For("p1", new AssembleEquipmentCommand("ANGEL", "HOST", ["ASSEMBLE_GREEN"])) };
        steps.AddRange(Top());
        if (c.Double || c.Control) {
            var gear = c.Double ? "ANGEL-2" : "SONG"; var number = c.Double ? "SFD·051/221" : "SFD·059/221";
            steps.Add(Step.For("p1", new PlayCardCommand(gear, number, [], Destination: "BASE")));
            steps.Add(Step.For("p1", new AssembleEquipmentCommand(gear, "HOST", ["ASSEMBLE_GREEN"]))); steps.AddRange(Top());
        }
        if (c.Lethal) { steps.Add(Step.For("p1", new PlayCardCommand("READY", "OGN·146/298", ["HOST"]))); steps.AddRange(Top()); }
        steps.Add(Step.For("p1", new EndTurnCommand()));
        var funding = Enumerable.Range(1, c.Control ? 18 : 9).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var batch in funding.Chunk(12)) steps.Add(Step.For("p2", new TapRuneCommand(batch[0], batch)));
        if (c.Control) {
            var purple = Enumerable.Range(1, 3).Select(i => "P2-PURPLE-" + i).ToArray();
            steps.Add(Step.For("p2", new RecycleRuneCommand(purple[0], purple)));
            steps.Add(Step.For("p2", new PlayCardCommand("TAKE", "OGN·203/298", ["HOST"]))); steps.AddRange(Top());
        }
        if (c.Lethal) steps.Add(Step.For("p2", new RecycleRuneCommand("P2-RED")));
        else steps.Add(Step.For("p2", new RecycleRuneCommand(funding[0], funding.Take(2).ToArray())));
        steps.Add(Step.For("p2", new PlayCardCommand(c.Lethal ? "RAY" : "KILL", c.Lethal ? "OGN·009/298" : "OGN·229/298", ["HOST"])));
        steps.AddRange(Top());
        if (c.Double && !c.Pending) steps.Add(Step.Choose("ATTACHED_GEAR:HOST:" + c.Selected));
        return steps;
    }

    private static async Task Execute(Runner run, Step step)
    {
        if (step.ChoiceId is null) { await run.Submit(step.Actor, GameCommandJsonMapper.Map(step.Command)); return; }
        var request = run.State.PendingRuleChoice?.Request ?? throw new InvalidOperationException("Missing real C9 replacement decision.");
        Check(request.Kind == "DESTRUCTION_REPLACEMENT" && request.PlayerId == run.P1, "The actual host controller must choose its attached source.");
        var option = request.Options.Single(o => o.Id == step.ChoiceId);
        await run.Submit(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
    }
    private static void CheckBoundary(MatchState state, Case c, string p1, string p2)
    {
        if (c.Pending) {
            var pending = state.PendingRuleChoice ?? throw new InvalidOperationException("Keep the actual two-Guardian decision pending across restart.");
            Check(pending.Request.PlayerId == p1 && pending.Request.Kind == "DESTRUCTION_REPLACEMENT" && pending.Answers.Count == 0,
                "Keep the exact uncommitted original two-source decision.");
            Check(pending.Request.Options.Select(o => o.Id).Order(StringComparer.Ordinal).SequenceEqual(new[] {
                "ATTACHED_GEAR:HOST:ANGEL", "ATTACHED_GEAR:HOST:ANGEL-2" }), "Only the two actual attached Guardians may protect this host.");
            foreach (var gear in new[] { "ANGEL", "ANGEL-2" })
                Check(state.CardObjects[gear].AttachedToObjectId == "HOST" && state.CardObjects[gear].ObjectGeneration == 1
                    && state.PlayerZones[p1].Base.Contains(gear), "Keep both current original source bindings before committing the choice.");
            return;
        }
        var controller = c.Control ? p2 : p1; var host = state.CardObjects["HOST"];
        Check(state.PendingRuleChoice is null && state.PendingPayment is null && state.StackItems.Count == 0, "Complete the real destruction replacement.");
        Check(host.CardNo == c.HostNo && host.ObjectGeneration == 1 && host.ControllerId == controller && host.OwnerId == p1
            && host.IsExhausted && host.Damage == 0 && host.DamageMarkedByPlayerIds is null && host.MovementCountThisTurn == 0,
            "Heal and exhaust the same host incarnation under its actual controller, without recording movement or death.");
        Check(state.PlayerZones[controller].Base.Contains("HOST") && state.ObjectLocations["HOST"] == new ObjectLocationState(controller, "BASE"),
            "Recall the host to its actual controller's base.");
        Check(state.PlayerZones[p1].Graveyard.Contains(c.Selected) && state.CardObjects[c.Selected].AttachedToObjectId is null,
            "Destroy only the chosen granting equipment, to its original owner's graveyard.");
        var survivor = c.Double ? (c.Selected == "ANGEL" ? "ANGEL-2" : "ANGEL") : c.Control ? "SONG" : null;
        if (survivor is not null) {
            var gear = state.CardObjects[survivor];
            Check(state.PlayerZones[controller].Base.Contains(survivor) && state.ObjectLocations[survivor] == new ObjectLocationState(controller, "BASE")
                && gear.AttachedToObjectId == "HOST" && gear.OwnerId == p1 && gear.ControllerId == p1 && gear.ObjectGeneration == 1,
                "The surviving attached group follows the host without changing its equipment ownership, control or generation.");
            Check(state.PlayerZones.Sum(p => p.Value.Base.Count(id => id == survivor) + p.Value.Battlefields.Count(id => id == survivor)) == 1,
                "The surviving attachment has exactly one actual field location.");
            if (c.Control) Check(gear.AttachedRuleText?.CardNo == c.HostNo, "Preserve the actual Song's captured text binding after the group changes controller.");
        }
        Check(state.DeathLedger.Counts.GetValueOrDefault(controller) == 0, "Replacement does not count the host as dead.");
        if (c.Lethal) Check(state.PlayerZones[p1].MainDeck.Count == 7, "The prevented Last Breath must not draw a card.");
        CheckPrivateViews(state);
    }
    private static void CheckGuardianEvents(IReadOnlyList<RecoveredEvent> events, Case c, string p1, string p2)
    {
        string? Text(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch {
            string value => value, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
        var recalled = events.Where(e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Text(e.Event, "targetObjectId") == "HOST"
            && Text(e.Event, "replacementEffectId") == ReplacementKinds.AttachedUnitDestroyedDestroySourceRecallExhausted).ToArray();
        Check(recalled.Length == 1 && Text(recalled[0].Event, "sourceObjectId") == "HOST"
            && Text(recalled[0].Event, "controllerId") == (c.Control ? p2 : p1), "The host executes its added text as the actual source and controller.");
        var gear = events.Single(e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Text(e.Event, "targetObjectId") == c.Selected);
        Check(gear.Sequence < recalled[0].Sequence && Text(gear.Event, "sourceObjectId") == "HOST", "Destroy the granting source before recalling the host.");
        Check(!events.Any(e => (e.Event.Kind == "UNIT_DESTROYED" && Text(e.Event, "targetObjectId") == "HOST")
            || (e.Event.Kind == "TRIGGER_QUEUED" && Text(e.Event, "sourceObjectId") == "HOST")), "The host did not die or enqueue its own Last Breath.");
    }

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output); Check(!File.Exists(Path.Combine(output, "network-fixture.json")), "Use fresh C9 network rooms.");
        var cases = new List<object>();
        foreach (var c in NetworkCases) {
            var run = await Runner.Create(source, Path.Combine(output, "prepare-" + c.Key), c.Key, c.Scenario);
            var frame = await run.Cold("actual-seats-and-finite-seed");
            Check(frame.Commands.Count == 3 && frame.Commands.Take(2).All(cmd => cmd.CommandType == MatchSession.RoomSeatCommandType)
                && frame.Commands[2].CommandType == "DEV_SEED_SCENARIO:" + c.Scenario, "Use genuine logged room seats and a finite seed, not imported accepted history.");
            cases.Add(new { kind = c.Key, c.HostNo, c.Control, c.Lethal, c.Double, c.Pending, room = run.Room, p1 = run.P1, p2 = run.P2,
                tokens = run.Tokens, credentials = run.Credentials, seedTick = run.State.Tick, ruleInputs = run.State.RuleInputs,
                initialHash = MatchStateHasher.Hash(run.State), steps = Steps(c), expectedAcceptedCommands = 3 + Steps(c).Count });
        }
        await Write(output, "network-fixture.json", new { kind = "FUTURE_CLIENT_INTENTS_NOT_ACCEPTED_HISTORY", cases,
            scope = "Three genuine two-identity finite QA rooms: completed attachment, pending two-source choice and foreign-controlled attachment group across actual API restart." });
    }
    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        Check(stage is "before-restart" or "after-restart", "Unknown C9 network stage.");
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = item.GetProperty("room").GetString()!; var c = NetworkCases.Single(c => c.Key == item.GetProperty("kind").GetString());
            var digest = await NetworkReconnectSqlEvidence.VerifyAsync(source, output, c.Key, room, stage,
                item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
            var frame = await LoadAndAudit(source, room);
            Check(await Digest(source, room) == digest, "Pure C9 recovery and original-birth audit must preserve exact seven-table rows.");
            CheckBoundary(frame.AuthoritativeState!, c, item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
            Check(frame.Commands.Count == item.GetProperty("expectedAcceptedCommands").GetInt32() + 1
                && frame.Commands.Count(cmd => !cmd.Accepted) == 1 && !frame.Commands[^1].Accepted
                && frame.Commands[^1].StartedTick == frame.Commands[^1].CompletedTick
                && frame.Commands[^1].StartedEventSequence == frame.Commands[^1].CompletedEventSequence, "Keep exactly the original action prefix and one real stale zero-event rejection.");
            Check(frame.Commands.All(cmd => Same(cmd.RuleInputs, frame.AuthoritativeState!.RuleInputs))
                && Same(item.GetProperty("ruleInputs"), frame.AuthoritativeState!.RuleInputs), "Never rebind original C9 actions to different runtime inputs.");
            await Write(output, c.Key + "-original-commands.json", frame.Commands); await Write(output, c.Key + "-original-events.json", frame.Events);
            reports.Add(new { room, c.Key, c.Pending, commands = frame.Commands.Count, events = frame.Events.Count,
                sevenTableDigest = digest, finalHash = MatchStateHasher.Hash(frame.AuthoritativeState!), originalBirthReplayMatched = true });
        }
        await Write(output, "sql-" + stage + ".json", new { status = "REAL_C9_NETWORK_SQL_ORIGINAL_BIRTH_PASS", stage, cases = reports });
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var item = fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("pending").GetBoolean());
        var room = item.GetProperty("room").GetString()!; var c = NetworkCases.Single(c => c.Pending);
        var original = await LoadAndAudit(source, room); var state = original.AuthoritativeState!; var pending = state.PendingRuleChoice!;
        var reports = new List<object>();
        foreach (var mode in new[] { "missing-source-option", "forged-source-label", "origin-only-generation", "coherent-source-generation" }) {
            var coherent = mode == "coherent-source-generation";
            var options = pending.Request.Options.ToArray(); var originCards = new Dictionary<string, CardObjectState>(pending.Origin.CardObjects);
            var outerCards = new Dictionary<string, CardObjectState>(state.CardObjects);
            if (mode == "missing-source-option") options = options.Skip(1).ToArray();
            if (mode == "forged-source-label") options[0] = options[0] with { Label = options[0].Label + " FORGED" };
            if (mode is "origin-only-generation" or "coherent-source-generation") {
                originCards["ANGEL"] = originCards["ANGEL"] with { ObjectGeneration = originCards["ANGEL"].ObjectGeneration + 1 };
                if (coherent) outerCards["ANGEL"] = originCards["ANGEL"];
            }
            var forged = state with { CardObjects = outerCards, PendingRuleChoice = pending with {
                Request = pending.Request with { Options = options }, Origin = pending.Origin with { CardObjects = originCards } } };

            var digest = await Digest(source, room); var rows = await CaptureLatestRows(source, room, state.Tick, original.LastEventSequence);
            string? rejection = null;
            try {
                var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
                foreach (var row in rows) {
                    object value = row.Table switch { "state_snapshots" => forged, "snapshots" => snapshots[row.Player!], _ => prompts[row.Player!] };
                    await UpdatePayload(source, room, row, JsonSerializer.Serialize(value, value.GetType(), Json));
                }
                var stored = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing poisoned C9 QA frame.");
                Check(stored.IsConsistent == coherent, "The declared C9 shape-vs-original-birth rejection layer must match the actual stored frame.");
                if (!coherent) Check(stored.ValidationErrors.Contains("invalid replacement choice continuation"), "Strict original request/answers/origin guard was bypassed.");
                try { _ = await Registry(source).GetOrCreateAsync(room, default); }
                catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { rejection = error.Message; }
                Check(rejection is not null, "Actual SQL registry accepted a forged C9 pending transcript.");
                if (coherent) Check(rejection!.Contains("action-log audit failed", StringComparison.Ordinal), "A coherent alternative must fail against unchanged original actions.");
            }
            finally { foreach (var row in rows) await UpdatePayload(source, room, row, row.Payload); }
            Check(await Digest(source, room) == digest, "Restore every temporary C9 projection row exactly.");
            await LoadAndAudit(source, room);
            Check(await Digest(source, room) == digest, "Post-tamper pure recovery must preserve the exact seven-table baseline.");
            reports.Add(new { room, mode, coherent, rejection, originalHistoryNeverRewritten = true, originalSevenTableDigestRestored = true });
        }
        var session = await Registry(source).GetOrCreateAsync(room, default); var request = state.PendingRuleChoice!.Request;
        var option = request.Options.Single(o => o.Id == "ATTACHED_GEAR:HOST:ANGEL-2");
        var command = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]);
        var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject(); raw["promptId"] = session.PromptFor(request.PlayerId).PromptId; raw["snapshotTick"] = state.Tick;
        var wire = JsonSerializer.SerializeToElement(raw);
        var result = await session.SubmitAsync(request.PlayerId, "c9-legitimate-completion-after-tamper", command, wire, default);
        Check(result.Accepted, result.ErrorMessage); CheckBoundary(result.State, c with { Pending = false }, request.PlayerId, item.GetProperty("p2").GetString()!);
        var completed = await LoadAndAudit(source, room);
        Check(completed.Commands.Count == original.Commands.Count + 1 && Same(completed.Commands.Take(original.Commands.Count).ToArray(), original.Commands)
            && Same(completed.Events.Take(original.Events.Count).ToArray(), original.Events), "Legitimate final answer must append to the untouched original pending journal.");
        CheckGuardianEvents(completed.Events, c, item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
        await Write(output, "registry-tamper-results.json", new { status = "REAL_POSTGRES_C9_TAMPER_REJECTED_AND_ORIGINAL_PENDING_COMPLETED", reports,
            completedCommands = completed.Commands.Count, completeOriginalBirthReplayMatched = true });
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
            var run = new Runner(source, output, "c9-" + kind + "-" + identity, "c9-" + identity + "-a", "c9-" + identity + "-b");
            run.Session = new MatchSession(run.Room, run.engine, run.journal, run.players);
            foreach (var actor in new[] { run.P1, run.P2 })
            {
                run.Tokens[actor] = (await run.Session.EnsurePlayerAsync(actor, default)).ReconnectToken;
                run.Credentials[actor] = "c9-key-" + actor;
            }
            var seeded = await run.Session.SeedScenarioAsync(run.P1, "c9-original-qa-seed", scenario,
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
            var raw = Raw(actor, command); var id = "c9-action-" + submitted++;
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
