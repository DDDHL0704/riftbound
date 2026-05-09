using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RealTriggerQueueTests
{
    [Fact]
    public async Task RealWatchfulSentinelLastBreathTriggersEnterApnapOrderWindowAndResolveThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingTwoWatchfulSentinelsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-watchful-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-watchful-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var queuedEvents = p2Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, queuedEvents.Length);
        Assert.All(queuedEvents, gameEvent =>
        {
            Assert.Equal("WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", gameEvent.Payload["effectKind"]);
            Assert.Equal("UNIT_DESTROYED", gameEvent.Payload["triggeredByEventKind"]);
        });

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-WATCHFUL-SENTINEL", p1Trigger.SourceObjectId);
        Assert.Equal("P2-WATCHFUL-SENTINEL", p2Trigger.SourceObjectId);
        Assert.Equal("WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", p1Trigger.EffectKind);

        var prompt = p2Pass.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        Assert.Equal("P1", Assert.IsType<string>(metadata["orderingPlayerId"]));
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p1TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p1Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal("P1-WATCHFUL-SENTINEL", Assert.IsType<string>(p1TriggerView["sourceObjectId"]));
        Assert.Equal("WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", Assert.IsType<string>(p1TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p1TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-watchful-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: [p1Trigger.TriggerId, p2Trigger.TriggerId]),
            CancellationToken.None);
        Assert.False(illegalReorder.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, illegalReorder.ErrorCode);
        Assert.Equal(p2Pass.State.Tick, illegalReorder.State.Tick);
        Assert.Empty(illegalReorder.State.StackItems);
        Assert.Equal(
            p2Pass.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray(),
            illegalReorder.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray());
        Assert.Empty(illegalReorder.State.PlayerZones["P1"].Hand);
        Assert.Empty(illegalReorder.State.PlayerZones["P2"].Hand);

        var ordered = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-watchful-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);
        Assert.Contains(ordered.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent("intent-real-watchful-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-real-watchful-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal));
        Assert.Equal(["P2-DRAW-001"], p1ResolvesP2Trigger.State.PlayerZones["P2"].Hand);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-real-watchful-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-real-watchful-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        Assert.Contains(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Equal(["P1-DRAW-001"], p2ResolvesP1Trigger.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DRAW-001"], p2ResolvesP1Trigger.State.PlayerZones["P2"].Hand);
    }

    private static MatchState BuildSpiritFireDestroyingTwoWatchfulSentinelsState()
    {
        return new MatchState(
            "real-trigger-room",
            7,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001"],
                    Base = ["P1-WATCHFUL-SENTINEL"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-DRAW-001"],
                    Base = ["P2-WATCHFUL-SENTINEL"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-WATCHFUL-SENTINEL"] = new(
                    "P1-WATCHFUL-SENTINEL",
                    cardNo: "OGN·096/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-WATCHFUL-SENTINEL"] = new(
                    "P2-WATCHFUL-SENTINEL",
                    cardNo: "OGN·096/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-SPIRIT-FIRE"] = new(
                    "P1-SPELL-SPIRIT-FIRE",
                    cardNo: "OGN·256/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-SPIRIT-FIRE",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-WATCHFUL-SENTINEL", "P2-WATCHFUL-SENTINEL"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }
}
