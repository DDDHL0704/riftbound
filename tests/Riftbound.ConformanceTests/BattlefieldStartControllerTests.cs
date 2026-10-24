using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldStartControllerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("P1", "P1", "P1", "P1")]
    [InlineData("P1", "P1", "P2", "P2")]
    [InlineData("P1", "P1", null, "P1")]
    [InlineData("P2", "P1", "P1", "P1")]
    [InlineData("P2", "P1", "P2", "P2")]
    [InlineData("P2", "P1", null, "P2")]
    [InlineData("P1", "P2", "P1", "P1")]
    [InlineData("P1", "P2", "P2", "P2")]
    [InlineData("P1", "P2", null, "P1")]
    [InlineData("P2", "P2", "P1", "P1")]
    [InlineData("P2", "P2", "P2", "P2")]
    [InlineData("P2", "P2", null, "P2")]
    public async Task PrintedEachPlayerTimingUsesFieldControllerAndOnlyUncontrolledFallsBackToTurn(
        string turn, string physicalOwner, string? fieldController, string expectedActor)
    {
        var opened = await FrostHoldTests.Start(Position(turn, physicalOwner, fieldController));
        var item = Assert.Single(opened.State.StackItems);
        Assert.Equal(expectedActor, item.ControllerId);
        Assert.Equal(expectedActor, CapturedActor(item.BattlefieldStart!));
        Assert.Equal(physicalOwner, opened.State.CardObjects["F"].OwnerId);
        Assert.Equal(turn, opened.State.TurnPlayerId);
        OfficialInsightAndSpellLockTests.Restore(opened.State);
        var done = await Top(opened.State);
        Assert.Equal(1, done.State.CardObjects["TARGET"].Damage);
        Assert.Equal(new[] { expectedActor }, done.State.CardObjects["TARGET"].DamageMarkedByPlayerIds);
        Assert.Contains(done.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
            && Equals(e.Payload["playerId"], expectedActor));
    }

    [Theory]
    [InlineData("P2", "P2", "P1", false)]
    [InlineData("P2", "P1", "P2", true)]
    [InlineData("P1", "P2", "P1", true)]
    [InlineData(null, "P2", "P1", true)]
    public async Task CapturedActorDeterminesFriendlyDamageAndWhichElderCanDestroy(
        string? fieldController, string targetController, string elderController, bool destroyed)
    {
        var state = Position("P1", "P1", fieldController);
        state = At(At(state, "TARGET", targetController, "BATTLEFIELD", "F"), "ELDER", elderController, "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ELDER"] = state.CardObjects["ELDER"] with { OwnerId = elderController } } };
        var opened = await FrostHoldTests.Start(state);
        var done = await Top(opened.State);
        Assert.Equal(destroyed, done.State.PlayerZones["P2"].Graveyard.Contains("TARGET"));
        if (!destroyed) Assert.Equal(1, done.State.CardObjects["TARGET"].Damage);
        Assert.Contains(done.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
            && Equals(e.Payload["playerId"], fieldController ?? "P1"));
    }

    [Theory]
    [InlineData("P1", false)]
    [InlineData(null, false)]
    [InlineData("P2", false)]
    [InlineData("P1", true)]
    [InlineData(null, true)]
    public async Task PureSourceControlChangeOrNewGenerationDoesNotTransferExistingSkill(
        string? nextController, bool changeCardAndGeneration)
    {
        var opened = await FrostHoldTests.Start(Position("P1", "P1", "P2"));
        Assert.Equal("P2", Assert.Single(opened.State.StackItems).ControllerId);
        var field = opened.State.CardObjects["F"];
        var changed = opened.State with { CardObjects = new Dictionary<string, CardObjectState>(opened.State.CardObjects) {
            ["F"] = field with { ControllerId = nextController,
                CardNo = changeCardAndGeneration ? "OGN\u00b7294/298" : field.CardNo,
                ObjectGeneration = field.ObjectGeneration + (changeCardAndGeneration ? 1 : 0) } } };
        OfficialInsightAndSpellLockTests.Restore(changed);
        Assert.Equal("P2", CapturedActor(Assert.Single(changed.StackItems).BattlefieldStart!));
        var done = await Top(changed);
        Assert.Equal(new[] { "P2" }, done.State.CardObjects["TARGET"].DamageMarkedByPlayerIds);
    }

    [Fact]
    public async Task DifferentControllersOrderOnlyTheirOwnCapturedFieldTriggers()
    {
        var state = Position("P1", "P1", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { CardNo = "UNL-212/219", ControllerId = "P1" } } };
        var opened = await FrostHoldTests.Start(state);
        Assert.Equal(2, opened.State.TriggerQueue.Count);
        Assert.Equal("P2", opened.State.TriggerQueue.Single(t => t.SourceObjectId == "F").ControllerId);
        Assert.Equal("P1", opened.State.TriggerQueue.Single(t => t.SourceObjectId == "OTHER").ControllerId);
        var orders = 0;
        while (opened.State.TriggerQueue.Count > 0)
        {
            Assert.True(orders++ < 2);
            var actor = opened.Prompts.Single(p => p.Value.Actionable
                && p.Value.Actions.Contains(CommandTypes.OrderTriggers)).Key;
            var own = opened.State.TriggerQueue.Where(t => t.ControllerId == actor).Select(t => t.TriggerId).ToArray();
            Assert.Single(own);
            // The existing command carries the whole queue; another controller's group is fixed.
            var illegal = new OrderTriggersCommand(OrderedTriggerIds: opened.State.TriggerQueue
                .OrderBy(t => t.ControllerId == "P1" ? 0 : 1).Select(t => t.TriggerId).ToArray());
            var rejected = await new CoreRuleEngine().ResolveAsync(opened.State, new("cross-controller-order", actor, illegal.CmdType), illegal, default);
            Assert.False(rejected.Accepted);
            Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
            var complete = opened.State.TriggerQueue.OrderBy(t => t.ControllerId == "P2" ? 0 : 1)
                .Select(t => t.TriggerId).ToArray();
            opened = await Act(opened.State, actor, new OrderTriggersCommand(OrderedTriggerIds: complete));
        }
        Assert.Equal(2, opened.State.StackItems.Count);
        Assert.All(opened.State.StackItems, item => Assert.Equal(item.ControllerId, CapturedActor(item.BattlefieldStart!)));
        OfficialInsightAndSpellLockTests.Restore(opened.State);
    }

    [Fact]
    public async Task CapturedControllerMustMatchForBothPrivateViewsAndSpectator()
    {
        var opened = await FrostHoldTests.Start(Position("P1", "P1", "P2"));
        var state = opened.State;
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, 0,
            opened.Snapshots[p], state.Tick, 0, opened.Prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
        foreach (var snapshot in views.Values.Select(v => v.Snapshot).Append(spectator.SpectatorSnapshot))
        {
            var payload = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            var original = payload["battlefieldStart"];
            var forged = JsonSerializer.SerializeToNode(original, Json)!;
            forged["controllerId"] = "P1";
            payload["battlefieldStart"] = JsonSerializer.SerializeToElement(forged, Json);
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator),
                e => e.Contains("battlefield start context differs", StringComparison.Ordinal));
            payload["battlefieldStart"] = original;
        }
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("context")]
    [InlineData("both")]
    [InlineData("event")]
    [InlineData("event-and-both")]
    [InlineData("missing-controller")]
    [InlineData("missing-origin")]
    [InlineData("duplicate-origin")]
    [InlineData("no-history")]
    public async Task OriginalTriggerBirthAndStrictReplayRejectEvenConsistentlyForgedControllers(string branch)
    {
        var original = await RecordedStart();
        var originalHash = MatchStateHasher.HashValue(original);
        Assert.Equal("alice", Assert.Single(original.AuthoritativeState!.StackItems).ControllerId);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(original, new CoreRuleEngine(), default));
        var state = original.AuthoritativeState!;
        var item = Assert.Single(state.StackItems);
        var context = JsonSerializer.SerializeToNode(item.BattlefieldStart, Json)!;
        if (branch is "context" or "both" or "event-and-both") context["controllerId"] = "bob";
        if (branch == "missing-controller") context.AsObject().Remove("controllerId");
        item = item with {
            ControllerId = branch is "actor" or "both" or "event-and-both" ? "bob" : item.ControllerId,
            BattlefieldStart = context.Deserialize<BattlefieldStartContext>(Json) };
        state = state with { StackItems = [item] };
        var origin = Assert.Single(original.Events, e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload["effectKind"], "CAPTURED_BATTLEFIELD_START_DAMAGE"));
        var events = original.Events.ToArray();
        if (branch is "event" or "event-and-both") events = events.Select(e => e == origin
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) { ["controllerId"] = "bob" } } }
            : e).ToArray();
        if (branch == "missing-origin") events = events.Where(e => e != origin).ToArray();
        if (branch == "duplicate-origin") events = [..events, origin];
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick,
            original.LastEventSequence, snapshots[p], state.Tick, original.LastEventSequence, prompts[p]));
        var forged = original with { AuthoritativeState = state, Events = events, PlayerViews = views,
            SpectatorReplayFrame = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, original.LastEventSequence,
                events.Select(e => e.Event).ToArray(), state) };
        if (branch == "no-history") forged = forged with { Commands = [], Events = [], OpeningSeed = state.Seed };
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default));
        Assert.Equal(originalHash, MatchStateHasher.HashValue(original));
    }

    private static MatchState Position(string turn, string owner, string? controller)
    {
        var state = At(FrostHoldTests.Position(), "F", owner, "BATTLEFIELD", "F");
        return state with { TurnPlayerId = turn, ActivePlayerId = turn,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["F"] = state.CardObjects["F"] with { OwnerId = owner, ControllerId = controller } } };
    }

    private static string CapturedActor(BattlefieldStartContext context)
    {
        var json = JsonSerializer.SerializeToElement(context, Json);
        Assert.True(json.TryGetProperty("controllerId", out var actor), "A captured start skill must carry its original controller.");
        return actor.GetString()!;
    }

    private static async Task<MatchRecoveryFrame> RecordedStart()
    {
        var journal = new Journal();
        var session = new MatchSession("recorded-field-start-controller-" + Guid.NewGuid().ToString("N"), new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        const string scenario = "battlefield-turn-start-damage";
        var seeded = await session.SeedScenarioAsync("alice", "seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        var result = await session.SubmitAsync("alice", "begin-bob", new EndTurnCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "END_TURN" }), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("bob", result.State.TurnPlayerId);
        Assert.Single(result.State.StackItems);
        var last = journal.Entries[^1];
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((value, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, value))).ToArray();
        var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, last.CompletedTick,
            last.CompletedEventSequence, p.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[p.Key]));
        Assert.Empty(MatchRecoveryValidator.Validate(session.RoomId, last.CompletedEventSequence, commands, events,
            views, last.AuthoritativeState, last.CompletedTick));
        return new(session.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState,
            MatchState.Create(session.RoomId) with { Seed = journal.Entries[0].OpeningSeed }) { OpeningSeed = journal.Entries[0].OpeningSeed };
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
