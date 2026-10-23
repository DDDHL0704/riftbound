using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RengarPermissionRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static IEnumerable<object[]> Cases()
    {
        foreach (var alternate in new[] { false, true })
        foreach (var branch in new[] { "focus", "result", "source-returned", "after-close", "other-field", "missing-field", "wrong-actor", "blocked", "poor", "base" })
            yield return [alternate, branch];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RealHandPermissionAndRejectionsSurviveEveryOriginalColdPrefix(bool alternate, string branch)
    {
        var run = new Driver(MatchState.Create("RENGAR-PERMISSION-RECOVERY") with { Seed = 424242 });
        var seed = branch is "blocked" or "poor" ? branch : "normal";
        await run.Prepare("rengar-permission-" + (alternate ? seed == "normal" ? "alt" : "alt-" + seed : seed));
        await run.Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
        if (branch is "result" or "source-returned" or "after-close")
        {
            await run.Act("P1", new PassFocusCommand()); await run.Act("P2", new PassFocusCommand());
            await run.Act("P1", new DeclareBattleCommand("FIELD", ["SOURCE"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
            Assert.Equal("P1", run.State.PendingBattleResult!.AttackingPlayerId);
            if (branch is "source-returned" or "after-close")
            {
                await run.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
                var top = run.State.StackItems[^1].StackItemId;
                for (var i = 0; run.State.StackItems.Any(s => s.StackItemId == top); i++)
                { Assert.InRange(i, 0, 3); await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand()); }
                Assert.Contains("SOURCE", run.State.PlayerZones["P1"].Hand);
                Assert.DoesNotContain(run.State.CardObjects.Values, c => c.ControllerId == "P1" && c.IsAttacking);
                Assert.Equal("P1", run.State.PendingBattleResult!.AttackingPlayerId);
            }
            if (branch == "after-close")
            { await run.Settle(); Assert.Null(run.State.PendingBattleResult); Assert.Single(run.Events, e => e.Kind == "BATTLE_CLOSED"); }
        }
        var allowed = branch is "focus" or "result" or "source-returned" or "base";
        var destination = branch switch { "base" => "BASE", "other-field" => "BATTLEFIELD:OTHER", "missing-field" => "BATTLEFIELD:MISSING", _ => "BATTLEFIELD:FIELD" };
        var prompt = run.Prompt("P1");
        if (allowed) Assert.Contains(prompt.Candidates ?? [], c => c.Action == CommandTypes.PlayCard
            && (c.Sources ?? []).Any(s => s.Id == "RENGAR") && (c.Destinations ?? []).Any(d => d.Id == destination));
        if (branch is "other-field" or "missing-field" or "blocked" or "after-close")
            Assert.DoesNotContain(prompt.Candidates ?? [], c => c.Action == CommandTypes.PlayCard
                && (c.Sources ?? []).Any(s => s.Id == "RENGAR") && (c.Destinations ?? []).Any(d => d.Id == destination));
        var print = alternate ? "SFD·025a/221·P" : "SFD·025/221";
        var before = run.State.RunePools["P1"];
        await run.Act(branch == "wrong-actor" ? "P2" : "P1", new PlayCardCommand("RENGAR", print, [], Destination: destination), allowed);
        if (allowed)
        {
            await run.DuplicateLast();
            Assert.DoesNotContain("RENGAR", run.State.PlayerZones["P1"].Hand);
            Assert.Equal(branch == "base" ? "BASE" : "BATTLEFIELD", run.State.ObjectLocations["RENGAR"].Zone);
            Assert.Equal(branch != "base", run.State.CardObjects["RENGAR"].IsAttacking);
            Assert.False(run.State.CardObjects["RENGAR"].IsDefending);
            Assert.NotEqual(before, run.State.RunePools["P1"]);
            Assert.Single(run.Events, e => e.Kind == (branch == "base" ? "UNIT_PLAYED_TO_BASE" : "UNIT_PLAYED_TO_BATTLEFIELD"));
            if (branch is "result" or "source-returned")
            {
                await run.Settle();
                Assert.Null(run.State.PendingBattleResult); Assert.False(run.State.CardObjects["RENGAR"].IsAttacking);
                Assert.Equal("P1", run.State.CardObjects["FIELD"].ControllerId);
                Assert.Single(run.Events, e => e.Kind == "BATTLE_CLOSED");
            }
        }
        else
        { Assert.Contains("RENGAR", run.State.PlayerZones["P1"].Hand); Assert.Equal(before, run.State.RunePools["P1"]); }
        run.Save($"{alternate}-{branch}");
    }

    [Theory]
    [InlineData("ATTACKER")]
    [InlineData("FIELD")]
    [InlineData("ESTABLISHED")]
    [InlineData("COORDINATED_ATTACKER")]
    public async Task ForgedAttackPermissionCannotReplaceTheOriginalBattleDeclaration(string attack)
    {
        var run = new Driver(MatchState.Create("RENGAR-PERMISSION-FORGERY") with { Seed = 424242 });
        await run.Prepare("rengar-permission-normal");
        await run.Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
        const string prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
        var original = Assert.Single(run.State.UntilEndOfTurnEffects, e => e.StartsWith(prefix, StringComparison.Ordinal));
        var context = System.Text.Json.Nodes.JsonNode.Parse(Convert.FromBase64String(original[prefix.Length..]))!.AsObject();
        if (attack == "FIELD") context["BattlefieldId"] = "OTHER";
        else if (attack == "ESTABLISHED") context["InitialRolesEstablished"] = false;
        else context["InitialAttackingPlayerId"] = "P2";
        var changed = prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(context.ToJsonString()));
        var forged = run.State with { UntilEndOfTurnEffects = run.State.UntilEndOfTurnEffects.Select(e => e == original ? changed : e).ToArray() };
        var frame = run.Frame(forged);
        if (attack == "COORDINATED_ATTACKER")
            frame = frame with { Events = frame.Events.Select(e => e.Event.Kind == "BATTLE_ROLES_ESTABLISHED"
                ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) { ["attackingPlayerId"] = "P2" } } } : e).ToArray() };
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.NotEmpty(errors);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new Journal(), new Store(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(frame.RoomId, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        run.Save("forged-" + attack, new { frame, errors });
    }

    private sealed class Driver
    {
        private readonly MatchState initial;
        private readonly Journal journal = new();
        private MatchSession session;
        private readonly List<MatchState> states = [];
        private (string Actor, string Intent, GameCommand Command, JsonElement Raw) last;
        internal MatchState State { get; private set; }
        internal IEnumerable<GameEvent> Events => journal.Entries.SelectMany(e => e.Events);
        internal Driver(MatchState initial) { this.initial = initial; State = initial; session = new(initial, new CoreRuleEngine(), journal); }

        internal async Task Act(string actor, GameCommand command, bool accepted = true)
        {
            var before = JsonSerializer.Serialize(State, Json);
            var snapshots = State.Seats.Keys.ToDictionary(p => p, p => JsonSerializer.Serialize(session.SnapshotFor(p), Json));
            var prompts = State.Seats.Keys.ToDictionary(p => p, p => JsonSerializer.Serialize(session.PromptFor(p), Json));
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, Json);
            command = GameCommandJsonMapper.Map(wire);
            last = (actor, "rengar-permission-" + journal.Entries.Count, command, wire);
            var result = await session.SubmitAsync(actor, last.Intent, command, last.Raw, default);
            Assert.True(result.Accepted == accepted, result.ErrorMessage ?? "Unexpected accepted intent");
            if (!accepted)
            {
                Assert.Equal(before, JsonSerializer.Serialize(result.State, Json));
                foreach (var p in State.Seats.Keys)
                {
                    Assert.Equal(snapshots[p], JsonSerializer.Serialize(session.SnapshotFor(p), Json));
                    Assert.Equal(prompts[p], JsonSerializer.Serialize(session.PromptFor(p), Json));
                }
            }
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            State = result.State; states.Add(State);
            await Cold();
        }

        internal async Task Prepare(string scenario)
        {
            await session.EnsurePlayerAsync("P1", default); State = journal.Entries[^1].AuthoritativeState; states.Add(State); await Cold();
            await session.EnsurePlayerAsync("P2", default); State = journal.Entries[^1].AuthoritativeState; states.Add(State); await Cold();
            var result = await session.SeedScenarioAsync("P1", "original-rengar-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State; states.Add(State); await Cold();
        }

        private async Task Cold()
        {
            var frame = Frame();
            Assert.True(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            var replayEngine = new RecordingEngine();
            var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, replayEngine, default);
            if (errors.Count > 0) Save("failure-" + Guid.NewGuid().ToString("N"), new { errors, replayed = replayEngine.LastState });
            Assert.True(errors.Count == 0, string.Join("; ", errors));
            var restored = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
            foreach (var p in State.Seats.Keys)
            {
                Assert.Equal(JsonSerializer.Serialize(session.SnapshotFor(p), Json), JsonSerializer.Serialize(restored.SnapshotFor(p), Json));
                Assert.Equal(JsonSerializer.Serialize(session.PromptFor(p), Json), JsonSerializer.Serialize(restored.PromptFor(p), Json));
            }
            session = restored;
        }

        internal async Task DuplicateLast()
        {
            var count = journal.Entries.Count; var before = JsonSerializer.Serialize(State, Json);
            var duplicate = await session.SubmitAsync(last.Actor, last.Intent, last.Command, last.Raw, default);
            Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
            Assert.Equal(count, journal.Entries.Count); Assert.Equal(before, JsonSerializer.Serialize(duplicate.State, Json));
        }

        internal ActionPromptDto Prompt(string player) => session.PromptFor(player);

        internal async Task Settle()
        {
            for (var i = 0; State.StackItems.Count > 0; i++)
            { Assert.InRange(i, 0, 12); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        }

        internal MatchRecoveryFrame Frame(MatchState? replacement = null)
        {
            var state = replacement ?? State; var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, journal.Entries[^1].Events, state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }

        internal void Save(string name, object? tamper = null)
        {
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_RENGAR_PERMISSION_RECOVERY_EVIDENCE");
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            using var stream = new GZipStream(File.Open(Path.Combine(folder, name + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(stream, new { initial, states, entries = journal.Entries, frame = Frame(),
                originalPrefixReplayAndColdRestoreCount = journal.Entries.Count, tamper }, Json);
        }
    }

    private sealed class RecordingEngine : IRuleEngine
    {
        internal MatchState? LastState { get; private set; }
        public async ValueTask<ResolutionResult> ResolveAsync(MatchState state, PlayerIntent intent, GameCommand command, CancellationToken token)
        {
            var result = await new CoreRuleEngine().ResolveAsync(state, intent, command, token);
            LastState = result.State; return result;
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
