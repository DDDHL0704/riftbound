using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class CopiedLocalDestructionSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private sealed record Case(string Kind, string CardNo, string Suffix) {
        internal int Copies => Kind.EndsWith("double", StringComparison.Ordinal) ? 2 : 1;
        internal bool Pending => Kind.StartsWith("pending-", StringComparison.Ordinal);
        internal string Key => Kind + "-" + Suffix;
        internal string Scenario => "copied-local-destruction-soraka-" + Suffix;
    }
    private static readonly Case[] Cases = new[] {
        ("SFD·173/221", "173"), ("SFD·239/221", "239"), ("SFD·239*/221", "239-star")
    }.SelectMany(print => new[] { new Case("single", print.Item1, print.Item2), new Case("double", print.Item1, print.Item2) }).ToArray();
    private static readonly Case[] NetworkCases = [new("single", "SFD·173/221", "173"), new("pending-double", "SFD·239*/221", "239-star")];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Fresh(output); var reports = new List<object>();
        foreach (var c in Cases) {
            var run = await Runner.Create(source, Path.Combine(output, c.Key), c.Key, c.Scenario);
            foreach (var step in Steps(c)) await Execute(run, step);
            CheckBoundary(run.State, c, run.P1);
            var digest = await Digest(source, run.Room);
            var duplicate = await run.Session.SubmitAsync(run.LastActor!, run.LastIntent!, run.LastCommand!, run.LastRaw, default);
            Check(duplicate.Accepted && await Digest(source, run.Room) == digest, "C8 duplicate changed an exact seven-table row.");
            var frame = await run.Cold("complete-original-birth"); await run.Save(frame);
            CheckRecallOrder(frame.Events);
            reports.Add(new { c.CardNo, c.Copies, run.Room, commands = frame.Commands.Count, events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(run.State), checkpoints = run.Checkpoints, completeOriginalBirthReplay = true });
        }
        await Write(output, "receipt.json", new { status = "REAL_POSTGRES_COPIED_LOCAL_DESTRUCTION_PASS", reports,
            scope = "Six independent logged finite QA rooms, three exact Soraka printings and one/two actual Songs. Preset resources are QA, not ordinary openings or whole-card certification." });
    }

    private sealed record Step(string Actor, JsonElement Command, string? ChoiceId = null, string? ChoiceHint = null) {
        internal static Step For(string actor, GameCommand command) => new(actor, JsonSerializer.SerializeToElement(command, command.GetType(), Json));
        internal static Step Choose(string id) => new("$choice", JsonSerializer.SerializeToElement(new { cmdType = "PAY_COST" }, Json), ChoiceId: id);
        internal static Step Copy(string hint) => new("$choice", JsonSerializer.SerializeToElement(new { cmdType = "PAY_COST" }, Json), ChoiceHint: hint);
    }
    private static Step[] Top() => [Step.For("$priority", new PassPriorityCommand()), Step.For("$priority", new PassPriorityCommand())];
    private static List<Step> Steps(Case c)
    {
        var steps = new List<Step> { Step.For("p1", new PlayCardCommand("S", c.CardNo, [], Destination: "BATTLEFIELD:HOME")) };
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" })
            steps.Add(Step.For("p1", new PlayCardCommand(id, "OGN·210/298", [], Destination: id.StartsWith("FIELD", StringComparison.Ordinal) ? "BATTLEFIELD:HOME" : "BASE")));
        steps.Add(Step.For("p1", new PlayCardCommand("G1", "OGN·077/298", [], Destination: "BASE")));
        foreach (var gear in c.Copies == 1 ? new[] { "SONG" } : new[] { "SONG", "SONG-2" }) {
            steps.Add(Step.For("p1", new PlayCardCommand(gear, "SFD·059/221", [], Destination: "BASE")));
            steps.Add(Step.For("p1", new AssembleEquipmentCommand(gear, "S", ["ASSEMBLE_GREEN"]))); steps.AddRange(Top());
        }
        var runes = Enumerable.Range(1, 9).Select(i => "P2-FUND-" + i).ToArray();
        steps.AddRange([Step.For("p1", new EndTurnCommand()), Step.For("p2", new TapRuneCommand(runes[0], runes)),
            Step.For("p2", new RecycleRuneCommand(runes[0], runes.Take(3).ToArray())), Step.For("p2", new PlayCardCommand("AOE", "UNL-180/219", [])),
            ..Top(), Step.Choose("LOCAL_RECALL:FIELD-1:S"), Step.Choose("GEAR:S:G1")]);
        if (!c.Pending) steps.Add(Step.Copy("LOCAL_RECALL:BASE-1:S:ATTACHED:" + (c.Copies == 1 ? "SONG:" : "SONG-2:")));
        return steps;
    }
    private static async Task Execute(Runner run, Step step)
    {
        if (step.ChoiceId is null && step.ChoiceHint is null) { await run.Submit(step.Actor, GameCommandJsonMapper.Map(step.Command)); return; }
        var request = run.State.PendingRuleChoice?.Request ?? throw new InvalidOperationException("Missing real C8 replacement decision.");
        Check(request.Kind == "DESTRUCTION_REPLACEMENT" && request.PlayerId == run.P1, "The actual host controller must choose its replacement group.");
        var option = request.Options.Single(o => step.ChoiceId is not null ? o.Id == step.ChoiceId : o.Id.Contains(step.ChoiceHint!, StringComparison.Ordinal));
        await run.Submit(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
    }
    private static void CheckBoundary(MatchState state, Case c, string p1)
    {
        if (c.Pending) {
            var pending = state.PendingRuleChoice ?? throw new InvalidOperationException("Keep the actual copied group pending across API restart.");
            Check(pending.Request.PlayerId == p1 && pending.Request.Kind == "DESTRUCTION_REPLACEMENT" && pending.Answers.Count == 2
                && pending.Answers[0].OptionId == "LOCAL_RECALL:FIELD-1:S" && pending.Answers[1].OptionId == "GEAR:S:G1",
                "Keep the exact original printed-group then Hourglass answer prefix, without committing its virtual moves early.");
            var expected = new[] { "BASE-1", "BASE-2" }.SelectMany(target => Enumerable.Range(1, c.Copies).Select(index => {
                var id = index == 1 ? "SONG" : "SONG-2"; var gear = state.CardObjects[id];
                return $"LOCAL_RECALL:{target}:S:ATTACHED:{id}:{gear.ObjectGeneration}:{gear.AttachmentRevision}";
            })).Order(StringComparer.Ordinal);
            Check(pending.Request.Options.Select(o => o.Id).Order(StringComparer.Ordinal).SequenceEqual(expected),
                "Only each actual unused copy instance may protect the current base group; the printed instance cannot restart.");
            return;
        }
        Check(state.PendingRuleChoice is null && state.PendingPayment is null && state.StackItems.Count == 0, "Complete the actual global destruction.");
        Check(state.CardObjects["S"].CardNo == c.CardNo && state.CardObjects["S"].Power == 4 && state.CardObjects["S"].ObjectGeneration == 1,
            "Retain the exact played Soraka face, literal Might and generation.");
        foreach (var id in new[] { "S", "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }) {
            var unit = state.CardObjects[id];
            Check(state.PlayerZones[p1].Base.Contains(id) && unit.IsExhausted && unit.Damage == 0 && unit.ControllerId == p1
                && unit.OwnerId == p1 && unit.ObjectGeneration == 1 && state.ObjectLocations[id].Zone == "BASE",
                "The printed and separate copied groups must save every literal target exactly once, without changing incarnation.");
        }
        Check(state.PlayerZones[p1].Graveyard.Contains("G1") && !state.DeathLedger.Counts.ContainsKey(p1), "Hourglass is destroyed; no friendly unit dies.");
        foreach (var id in c.Copies == 1 ? new[] { "SONG" } : new[] { "SONG", "SONG-2" })
            Check(state.PlayerZones[p1].Base.Contains(id) && state.CardObjects[id].AttachedToObjectId == "S"
                && state.CardObjects[id].AttachedRuleText?.CardNo == c.CardNo, "Recall must move each real attached Song with its host and preserve its copy binding.");
        CheckPrivateViews(state);
    }
    private static void CheckRecallOrder(IReadOnlyList<RecoveredEvent> events)
    {
        string? Target(GameEvent value) => value.Payload.GetValueOrDefault("targetObjectId") switch {
            string text => text, JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(), _ => null };
        long Recall(string id) => events.Single(e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == id).Sequence;
        var gear = events.Single(e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Target(e.Event) == "G1").Sequence;
        Check(Recall("FIELD-1") < gear && Recall("FIELD-2") < gear && gear < Recall("S") && Recall("S") < Recall("BASE-1") && Recall("S") < Recall("BASE-2"),
            "One whole printed group must precede Hourglass/source recall and the independently started copied base group.");
    }

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output); Check(!File.Exists(Path.Combine(output, "network-fixture.json")), "Use fresh C8 network rooms.");
        var cases = new List<object>();
        foreach (var c in NetworkCases) {
            var run = await Runner.Create(source, Path.Combine(output, "prepare-" + c.Key), c.Key, c.Scenario);
            var frame = await run.Cold("actual-seats-and-finite-seed");
            Check(frame.Commands.Count == 3 && frame.Commands.Take(2).All(cmd => cmd.CommandType == MatchSession.RoomSeatCommandType)
                && frame.Commands[2].CommandType == "DEV_SEED_SCENARIO:" + c.Scenario, "Use genuine logged room seats and a finite seed, not imported accepted history.");
            cases.Add(new { kind = c.Key, c.CardNo, c.Suffix, c.Copies, c.Pending, room = run.Room, p1 = run.P1, p2 = run.P2,
                tokens = run.Tokens, credentials = run.Credentials, seedTick = run.State.Tick, ruleInputs = run.State.RuleInputs,
                initialHash = MatchStateHasher.Hash(run.State), steps = Steps(c), expectedAcceptedCommands = 3 + Steps(c).Count });
        }
        await Write(output, "network-fixture.json", new { kind = "FUTURE_CLIENT_INTENTS_NOT_ACCEPTED_HISTORY", cases,
            scope = "Two actual two-identity finite QA rooms: completed single copy and pending double-copy continuation, both retained across actual API restart." });
    }
    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        Check(stage is "before-restart" or "after-restart", "Unknown C8 network stage.");
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = item.GetProperty("room").GetString()!; var c = NetworkCases.Single(c => c.Key == item.GetProperty("kind").GetString());
            var digest = await NetworkReconnectSqlEvidence.VerifyAsync(source, output, c.Key, room, stage,
                item.GetProperty("p1").GetString()!, item.GetProperty("p2").GetString()!);
            var frame = await LoadAndAudit(source, room);
            Check(await Digest(source, room) == digest, "Pure C8 recovery and original-birth audit must preserve exact seven-table rows.");
            CheckBoundary(frame.AuthoritativeState!, c, item.GetProperty("p1").GetString()!);
            Check(frame.Commands.Count == item.GetProperty("expectedAcceptedCommands").GetInt32() + 1
                && frame.Commands.Count(cmd => !cmd.Accepted) == 1 && !frame.Commands[^1].Accepted
                && frame.Commands[^1].StartedTick == frame.Commands[^1].CompletedTick
                && frame.Commands[^1].StartedEventSequence == frame.Commands[^1].CompletedEventSequence, "Keep exactly the original action prefix and one real stale zero-event rejection.");
            Check(frame.Commands.All(cmd => Same(cmd.RuleInputs, frame.AuthoritativeState!.RuleInputs))
                && Same(item.GetProperty("ruleInputs"), frame.AuthoritativeState!.RuleInputs), "Never rebind original C8 actions to different runtime inputs.");
            await Write(output, c.Key + "-original-commands.json", frame.Commands); await Write(output, c.Key + "-original-events.json", frame.Events);
            reports.Add(new { room, c.Key, c.Pending, commands = frame.Commands.Count, events = frame.Events.Count,
                sevenTableDigest = digest, finalHash = MatchStateHasher.Hash(frame.AuthoritativeState!), originalBirthReplayMatched = true });
        }
        await Write(output, "sql-" + stage + ".json", new { status = "REAL_C8_NETWORK_SQL_ORIGINAL_BIRTH_PASS", stage, cases = reports });
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var item = fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("pending").GetBoolean());
        var room = item.GetProperty("room").GetString()!; var c = NetworkCases.Single(c => c.Pending);
        var original = await LoadAndAudit(source, room); var state = original.AuthoritativeState!; var pending = state.PendingRuleChoice!;
        var reports = new List<object>();
        foreach (var mode in new[] { "missing-copy-option", "forged-copy-revision", "forged-answer-hash", "coherent-alternate-instance" }) {
            var options = pending.Request.Options.ToArray(); var answers = pending.Answers.ToArray();
            MatchState forged;
            if (mode == "coherent-alternate-instance") forged = await AlternatePending(state);
            else {
                if (mode == "missing-copy-option") options = options.Skip(1).ToArray();
                if (mode == "forged-copy-revision") options[0] = options[0] with { Id = options[0].Id + ":FORGED" };
                if (mode == "forged-answer-hash") answers[0] = answers[0] with { RequestHash = "FORGED" };
                forged = state with { PendingRuleChoice = pending with { Request = pending.Request with { Options = options }, Answers = answers } };
            }
            var digest = await Digest(source, room); var rows = await CaptureLatestRows(source, room, state.Tick, original.LastEventSequence);
            var coherent = mode == "coherent-alternate-instance"; string? rejection = null;
            try {
                var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
                foreach (var row in rows) {
                    object value = row.Table switch { "state_snapshots" => forged, "snapshots" => snapshots[row.Player!], _ => prompts[row.Player!] };
                    await UpdatePayload(source, room, row, JsonSerializer.Serialize(value, value.GetType(), Json));
                }
                var stored = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing poisoned C8 QA frame.");
                Check(stored.IsConsistent == coherent, "The declared C8 shape-vs-original-birth rejection layer must match the actual stored frame.");
                if (!coherent) Check(stored.ValidationErrors.Contains("invalid replacement choice continuation"), "Strict original request/answers/origin guard was bypassed.");
                try { _ = await Registry(source).GetOrCreateAsync(room, default); }
                catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { rejection = error.Message; }
                Check(rejection is not null, "Actual SQL registry accepted a forged C8 pending transcript.");
                if (coherent) Check(rejection!.Contains("action-log audit failed", StringComparison.Ordinal), "A coherent alternative must fail against unchanged original actions.");
            }
            finally { foreach (var row in rows) await UpdatePayload(source, room, row, row.Payload); }
            Check(await Digest(source, room) == digest, "Restore every temporary C8 projection row exactly.");
            await LoadAndAudit(source, room);
            Check(await Digest(source, room) == digest, "Post-tamper pure recovery must preserve the exact seven-table baseline.");
            reports.Add(new { room, mode, coherent, rejection, originalHistoryNeverRewritten = true, originalSevenTableDigestRestored = true });
        }
        var session = await Registry(source).GetOrCreateAsync(room, default); var request = state.PendingRuleChoice!.Request;
        var option = request.Options.Single(o => o.Id.StartsWith("LOCAL_RECALL:BASE-1:S:ATTACHED:SONG-2:", StringComparison.Ordinal));
        var command = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]);
        var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject(); raw["promptId"] = session.PromptFor(request.PlayerId).PromptId; raw["snapshotTick"] = state.Tick;
        var wire = JsonSerializer.SerializeToElement(raw);
        var result = await session.SubmitAsync(request.PlayerId, "c8-legitimate-completion-after-tamper", command, wire, default);
        Check(result.Accepted, result.ErrorMessage); CheckBoundary(result.State, c with { Kind = "double" }, request.PlayerId);
        var completed = await LoadAndAudit(source, room);
        Check(completed.Commands.Count == original.Commands.Count + 1 && Same(completed.Commands.Take(original.Commands.Count).ToArray(), original.Commands)
            && Same(completed.Events.Take(original.Events.Count).ToArray(), original.Events), "Legitimate final answer must append to the untouched original pending journal.");
        CheckRecallOrder(completed.Events);
        await Write(output, "registry-tamper-results.json", new { status = "REAL_POSTGRES_C8_TAMPER_REJECTED_AND_ORIGINAL_PENDING_COMPLETED", reports,
            completedCommands = completed.Commands.Count, completeOriginalBirthReplayMatched = true });
    }
    private static async Task<MatchState> AlternatePending(MatchState state)
    {
        var p = state.PendingRuleChoice!; var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(p.Origin, p.Intent, GameCommandJsonMapper.Map(p.Command), default); Check(result.Accepted, result.ErrorMessage);
        foreach (var hint in new[] { "LOCAL_RECALL:FIELD-1:S:ATTACHED:SONG:", "GEAR:S:G1" }) {
            var request = result.State.PendingRuleChoice!.Request;
            var option = request.Options.Single(o => hint == "GEAR:S:G1" ? o.Id == hint : o.Id.StartsWith(hint, StringComparison.Ordinal));
            result = await engine.ResolveAsync(result.State, new("c8-memory-alternative-only", request.PlayerId, CommandTypes.PayCost),
                new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]), default); Check(result.Accepted, result.ErrorMessage);
        }
        Check(result.State.Tick == state.Tick && result.State.PendingRuleChoice is not null, "Build only a shape-valid alternative pending branch, never rewrite accepted rows.");
        return result.State;
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
            var run = new Runner(source, output, "c8-" + kind + "-" + identity, "c8-" + identity + "-a", "c8-" + identity + "-b");
            run.Session = new MatchSession(run.Room, run.engine, run.journal, run.players);
            foreach (var actor in new[] { run.P1, run.P2 })
            {
                run.Tokens[actor] = (await run.Session.EnsurePlayerAsync(actor, default)).ReconnectToken;
                run.Credentials[actor] = "c8-key-" + actor;
            }
            var seeded = await run.Session.SeedScenarioAsync(run.P1, "c8-original-qa-seed", scenario,
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
            var raw = Raw(actor, command); var id = "c8-action-" + submitted++;
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
