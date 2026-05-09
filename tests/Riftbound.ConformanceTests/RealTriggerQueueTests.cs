using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RealTriggerQueueTests
{
    [Fact]
    public async Task StateBasedCleanupWatchfulSentinelTriggersOrderAndResolveThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingTwoWatchfulSentinelsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-watchful-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-watchful-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(2, p2Pass.Events.Count(gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal)));
        var queuedEvents = p2Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, queuedEvents.Length);
        Assert.All(queuedEvents, gameEvent =>
        {
            Assert.Equal("WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", gameEvent.Payload["effectKind"]);
            Assert.Equal("UNIT_DESTROYED", gameEvent.Payload["triggeredByEventKind"]);
        });
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-CLEANUP-WATCHFUL-SENTINEL", p1Trigger.SourceObjectId);
        Assert.Equal("P2-CLEANUP-WATCHFUL-SENTINEL", p2Trigger.SourceObjectId);
        Assert.All(p2Pass.State.TriggerQueue, trigger => Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind));

        var prompt = p2Pass.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p1TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p1Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal("P1-CLEANUP-WATCHFUL-SENTINEL", Assert.IsType<string>(p1TriggerView["sourceObjectId"]));
        Assert.Equal("WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", Assert.IsType<string>(p1TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p1TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-cleanup-watchful-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
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
            new PlayerIntent("intent-cleanup-watchful-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent("intent-cleanup-watchful-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-cleanup-watchful-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", StringComparison.Ordinal));
        Assert.Equal(["P2-CLEANUP-DRAW-001"], p1ResolvesP2Trigger.State.PlayerZones["P2"].Hand);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-cleanup-watchful-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-cleanup-watchful-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Equal(["P1-CLEANUP-DRAW-001"], p2ResolvesP1Trigger.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-CLEANUP-DRAW-001"], p2ResolvesP1Trigger.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenWatchfulSentinelsDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingHiddenWatchfulSentinelsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-hidden-watchful-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-hidden-watchful-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task StateBasedCleanupHonestBrokerTriggersOrderAndCreateGoldThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingTwoHonestBrokersState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-honest-broker-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-honest-broker-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(2, p2Pass.Events.Count(gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-CLEANUP-HONEST-BROKER", p1Trigger.SourceObjectId);
        Assert.Equal("P2-CLEANUP-HONEST-BROKER", p2Trigger.SourceObjectId);
        Assert.Equal("HONEST_BROKER_LAST_BREATH_CREATE_GOLD", p1Trigger.EffectKind);
        Assert.All(p2Pass.State.TriggerQueue, trigger => Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind));

        var prompt = p2Pass.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p2TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p2Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal("P2-CLEANUP-HONEST-BROKER", Assert.IsType<string>(p2TriggerView["sourceObjectId"]));
        Assert.Equal("HONEST_BROKER_LAST_BREATH_CREATE_GOLD", Assert.IsType<string>(p2TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p2TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-cleanup-honest-broker-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: [p1Trigger.TriggerId, p2Trigger.TriggerId]),
            CancellationToken.None);
        Assert.False(illegalReorder.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, illegalReorder.ErrorCode);
        Assert.Equal(p2Pass.State.Tick, illegalReorder.State.Tick);
        Assert.Empty(illegalReorder.State.StackItems);
        Assert.Equal(
            p2Pass.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray(),
            illegalReorder.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray());
        Assert.Empty(illegalReorder.State.PlayerZones["P1"].Base);
        Assert.Empty(illegalReorder.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("P1-CLEANUP-HONEST-BROKER-TOKEN-001", illegalReorder.State.CardObjects.Keys);
        Assert.DoesNotContain("P2-CLEANUP-HONEST-BROKER-TOKEN-001", illegalReorder.State.CardObjects.Keys);

        var ordered = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-cleanup-honest-broker-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent("intent-cleanup-honest-broker-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-cleanup-honest-broker-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "HONEST_BROKER_LAST_BREATH_CREATE_GOLD", StringComparison.Ordinal));
        var p2TokenEvent = Assert.Single(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("P2", p2TokenEvent.Payload["playerId"]);
        Assert.Equal("P2-CLEANUP-HONEST-BROKER", p2TokenEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-CLEANUP-HONEST-BROKER-TOKEN-001", p2TokenEvent.Payload["tokenObjectId"]);
        Assert.Equal(true, p2TokenEvent.Payload["isExhausted"]);
        Assert.Equal(["P2-CLEANUP-HONEST-BROKER-TOKEN-001"], p1ResolvesP2Trigger.State.PlayerZones["P2"].Base);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-cleanup-honest-broker-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-cleanup-honest-broker-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        var p1TokenEvent = Assert.Single(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("P1", p1TokenEvent.Payload["playerId"]);
        Assert.Equal("P1-CLEANUP-HONEST-BROKER", p1TokenEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-CLEANUP-HONEST-BROKER-TOKEN-001", p1TokenEvent.Payload["tokenObjectId"]);
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Equal(["P1-CLEANUP-HONEST-BROKER-TOKEN-001"], p2ResolvesP1Trigger.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-CLEANUP-HONEST-BROKER-TOKEN-001"], p2ResolvesP1Trigger.State.PlayerZones["P2"].Base);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P1-CLEANUP-HONEST-BROKER-TOKEN-001"].IsExhausted);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P2-CLEANUP-HONEST-BROKER-TOKEN-001"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard], p2ResolvesP1Trigger.State.CardObjects["P1-CLEANUP-HONEST-BROKER-TOKEN-001"].Tags);
        Assert.Equal([CardObjectTags.EquipmentCard], p2ResolvesP1Trigger.State.CardObjects["P2-CLEANUP-HONEST-BROKER-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenHonestBrokersDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingHiddenHonestBrokersState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-hidden-honest-broker-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-hidden-honest-broker-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task StateBasedCleanupScoutingWarhawkTriggersOrderAndCallRuneThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingTwoScoutingWarhawksState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-scouting-warhawk-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-scouting-warhawk-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(2, p2Pass.Events.Count(gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal(["P1-CLEANUP-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P2-CLEANUP-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P2"].RuneDeck);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-CLEANUP-SCOUTING-WARHAWK", p1Trigger.SourceObjectId);
        Assert.Equal("P2-CLEANUP-SCOUTING-WARHAWK", p2Trigger.SourceObjectId);
        Assert.Equal("SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", p1Trigger.EffectKind);
        Assert.All(p2Pass.State.TriggerQueue, trigger => Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind));

        var prompt = p2Pass.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p2TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p2Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal("P2-CLEANUP-SCOUTING-WARHAWK", Assert.IsType<string>(p2TriggerView["sourceObjectId"]));
        Assert.Equal("SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", Assert.IsType<string>(p2TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p2TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-cleanup-scouting-warhawk-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: [p1Trigger.TriggerId, p2Trigger.TriggerId]),
            CancellationToken.None);
        Assert.False(illegalReorder.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, illegalReorder.ErrorCode);
        Assert.Equal(p2Pass.State.Tick, illegalReorder.State.Tick);
        Assert.Empty(illegalReorder.State.StackItems);
        Assert.Equal(
            p2Pass.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray(),
            illegalReorder.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray());
        Assert.Equal(["P1-CLEANUP-SCOUTING-WARHAWK-RUNE"], illegalReorder.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P2-CLEANUP-SCOUTING-WARHAWK-RUNE"], illegalReorder.State.PlayerZones["P2"].RuneDeck);
        Assert.Empty(illegalReorder.State.PlayerZones["P1"].Base);
        Assert.Empty(illegalReorder.State.PlayerZones["P2"].Base);

        var ordered = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-cleanup-scouting-warhawk-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent("intent-cleanup-scouting-warhawk-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-cleanup-scouting-warhawk-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", StringComparison.Ordinal));
        var p2RuneEvent = Assert.Single(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P2", p2RuneEvent.Payload["playerId"]);
        Assert.Equal("P2-CLEANUP-SCOUTING-WARHAWK", p2RuneEvent.Payload["sourceObjectId"]);
        Assert.Equal(1, p2RuneEvent.Payload["count"]);
        Assert.Equal("SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", p2RuneEvent.Payload["reason"]);
        Assert.Equal(
            ["P2-CLEANUP-SCOUTING-WARHAWK-RUNE"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2RuneEvent.Payload["runeObjectIds"]));
        Assert.Empty(p1ResolvesP2Trigger.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(["P2-CLEANUP-SCOUTING-WARHAWK-RUNE"], p1ResolvesP2Trigger.State.PlayerZones["P2"].Base);
        Assert.True(p1ResolvesP2Trigger.State.CardObjects["P2-CLEANUP-SCOUTING-WARHAWK-RUNE"].IsExhausted);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-cleanup-scouting-warhawk-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-cleanup-scouting-warhawk-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        var p1RuneEvent = Assert.Single(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P1", p1RuneEvent.Payload["playerId"]);
        Assert.Equal("P1-CLEANUP-SCOUTING-WARHAWK", p1RuneEvent.Payload["sourceObjectId"]);
        Assert.Equal(
            ["P1-CLEANUP-SCOUTING-WARHAWK-RUNE"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1RuneEvent.Payload["runeObjectIds"]));
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Empty(p2ResolvesP1Trigger.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(p2ResolvesP1Trigger.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(["P1-CLEANUP-SCOUTING-WARHAWK-RUNE"], p2ResolvesP1Trigger.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-CLEANUP-SCOUTING-WARHAWK-RUNE"], p2ResolvesP1Trigger.State.PlayerZones["P2"].Base);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P1-CLEANUP-SCOUTING-WARHAWK-RUNE"].IsExhausted);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P2-CLEANUP-SCOUTING-WARHAWK-RUNE"].IsExhausted);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenScoutingWarhawksDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingHiddenScoutingWarhawksState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-hidden-scouting-warhawk-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-hidden-scouting-warhawk-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-CLEANUP-HIDDEN-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P2-CLEANUP-STANDBY-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P2"].RuneDeck);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task StateBasedCleanupSadPorosTriggerOrderAndDrawThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingSadPorosState(),
            "sad-poros");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("SAD_PORO_LAST_BREATH_DRAW_1", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var final = await OrderAndResolveTwoDrawTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "sad-poros",
            "SAD_PORO_LAST_BREATH_DRAW_1",
            "P1-CLEANUP-SAD-PORO-SFD",
            "P2-CLEANUP-SAD-PORO-UNL",
            "P1-SAD-PORO-DRAW-001",
            "P2-SAD-PORO-DRAW-001");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(["P1-SAD-PORO-DRAW-001"], final.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-SAD-PORO-DRAW-001"], final.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupSadPoroSkipsWhenNotIsolated()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingSadPoroWithAllyState(),
            "sad-poro-not-isolated");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-CLEANUP-SAD-PORO-ALLY"], cleanup.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SAD-PORO-DRAW-001"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupLoyalPoroTriggersWhenNotIsolatedAndDrawsThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingLoyalPorosWithAlliesState(),
            "loyal-poros");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("LOYAL_PORO_LAST_BREATH_DRAW_1", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.Equal(["P1-CLEANUP-LOYAL-PORO-ALLY"], cleanup.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-CLEANUP-LOYAL-PORO-ALLY"], cleanup.State.PlayerZones["P2"].Base);

        var final = await OrderAndResolveTwoDrawTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "loyal-poros",
            "LOYAL_PORO_LAST_BREATH_DRAW_1",
            "P1-CLEANUP-LOYAL-PORO",
            "P2-CLEANUP-LOYAL-PORO",
            "P1-LOYAL-PORO-DRAW-001",
            "P2-LOYAL-PORO-DRAW-001");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(["P1-CLEANUP-LOYAL-PORO-ALLY"], final.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-CLEANUP-LOYAL-PORO-ALLY"], final.State.PlayerZones["P2"].Base);
        Assert.Equal(["P1-LOYAL-PORO-DRAW-001"], final.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-LOYAL-PORO-DRAW-001"], final.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupLoyalPoroSkipsWhenIsolated()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingIsolatedLoyalPorosState(),
            "loyal-poros-isolated");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-LOYAL-PORO-DRAW-001"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-LOYAL-PORO-DRAW-001"], cleanup.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupLoyalPoroSkipsWhenOnlyOtherFriendlyAlsoDies()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingLoyalPoroWithDyingAllyState(),
            "loyal-poro-ally-also-dies");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-LOYAL-PORO-DRAW-001"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenPorosDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingHiddenPorosState(),
            "hidden-poros");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-HIDDEN-PORO-DRAW-001"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-STANDBY-PORO-DRAW-001"], cleanup.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupUnsungHeroesTriggerOrderAndDrawTwoThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingPowerfulUnsungHeroesState(),
            "unsung-heroes");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var final = await OrderAndResolveTwoDrawTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "unsung-heroes",
            "UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2",
            "P1-CLEANUP-UNSUNG-HERO",
            "P2-CLEANUP-UNSUNG-HERO",
            ["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"],
            ["P2-UNSUNG-HERO-DRAW-001", "P2-UNSUNG-HERO-DRAW-002"]);

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"], final.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNSUNG-HERO-DRAW-001", "P2-UNSUNG-HERO-DRAW-002"], final.State.PlayerZones["P2"].Hand);
        Assert.Empty(final.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(final.State.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task StateBasedCleanupUnsungHeroSkipsWhenBelowPowerful()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingBelowPowerfulUnsungHeroesState(),
            "unsung-heroes-below-powerful");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-UNSUNG-HERO-DRAW-001", "P2-UNSUNG-HERO-DRAW-002"], cleanup.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenUnsungHeroesDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingHiddenUnsungHeroesState(),
            "hidden-unsung-heroes");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-HIDDEN-UNSUNG-HERO-DRAW-001", "P1-HIDDEN-UNSUNG-HERO-DRAW-002"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-STANDBY-UNSUNG-HERO-DRAW-001", "P2-STANDBY-UNSUNG-HERO-DRAW-002"], cleanup.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupGhostlyCentaursTriggerOrderAndGainPowerThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingGhostlyCentaurFriendlyUnitsState(),
            "ghostly-centaurs");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.DoesNotContain(cleanup.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Equal(5, cleanup.State.CardObjects["P1-CLEANUP-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(5, cleanup.State.CardObjects["P2-CLEANUP-GHOSTLY-CENTAUR"].Power);

        var final = await OrderAndResolveTwoGhostlyCentaurTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "ghostly-centaurs",
            "P1-CLEANUP-GHOSTLY-CENTAUR",
            "P2-CLEANUP-GHOSTLY-CENTAUR");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(7, final.State.CardObjects["P1-CLEANUP-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(7, final.State.CardObjects["P2-CLEANUP-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(2, final.State.CardObjects["P1-CLEANUP-GHOSTLY-CENTAUR"].UntilEndOfTurnPowerModifier);
        Assert.Equal(2, final.State.CardObjects["P2-CLEANUP-GHOSTLY-CENTAUR"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenGhostlyCentaursDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingFriendlyUnitsWithInvalidGhostlySourcesState(),
            "hidden-ghostly-centaurs");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(5, cleanup.State.CardObjects["P1-HIDDEN-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(5, cleanup.State.CardObjects["P1-STANDBY-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(5, cleanup.State.CardObjects["P2-OPPONENT-GHOSTLY-CENTAUR"].Power);
    }

    [Fact]
    public async Task StateBasedCleanupGhostlyCentaurSkipsWhenSourceAlsoDies()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingGhostlyCentaurAndFriendlyUnitState(),
            "ghostly-centaur-source-also-dies");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task StateBasedCleanupResonantSoulsTriggerOrderAndDrawThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingResonantSoulFriendlyUnitsState(),
            "resonant-souls");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var final = await OrderAndResolveTwoDrawTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "resonant-souls",
            "RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1",
            "P1-CLEANUP-RESONANT-SOUL",
            "P2-CLEANUP-RESONANT-SOUL",
            "P1-RESONANT-SOUL-DRAW-001",
            "P2-RESONANT-SOUL-DRAW-001");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-001"], final.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-RESONANT-SOUL-DRAW-001"], final.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupResonantSoulsSkipWhenOwnersAlreadyDestroyedThisTurn()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingResonantSoulFriendlyUnitsState() with
        {
            DestroyedUnitOwnerIdsThisTurn = ["P1", "P2"]
        };
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            state,
            "resonant-souls-already-destroyed");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-001"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-RESONANT-SOUL-DRAW-001"], cleanup.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenResonantSoulsDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingFriendlyUnitsWithInvalidResonantSoulSourcesState(),
            "hidden-resonant-souls");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-001"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task StateBasedCleanupResonantSoulSkipsWhenSourceAlsoDies()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingResonantSoulAndFriendlyUnitState(),
            "resonant-soul-source-also-dies");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RESONANT-SOUL-DRAW-001"], cleanup.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Hand);
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
    }

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

    [Fact]
    public async Task RealHonestBrokerLastBreathTriggersOrderAndCreateGoldThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingTwoHonestBrokersState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-honest-broker-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-honest-broker-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-HONEST-BROKER", p1Trigger.SourceObjectId);
        Assert.Equal("P2-HONEST-BROKER", p2Trigger.SourceObjectId);
        Assert.Equal("HONEST_BROKER_LAST_BREATH_CREATE_GOLD", p1Trigger.EffectKind);
        Assert.All(p2Pass.State.TriggerQueue, trigger => Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind));

        var prompt = p2Pass.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p2TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p2Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal("P2-HONEST-BROKER", Assert.IsType<string>(p2TriggerView["sourceObjectId"]));
        Assert.Equal("HONEST_BROKER_LAST_BREATH_CREATE_GOLD", Assert.IsType<string>(p2TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p2TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-honest-broker-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: [p1Trigger.TriggerId, p2Trigger.TriggerId]),
            CancellationToken.None);
        Assert.False(illegalReorder.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, illegalReorder.ErrorCode);
        Assert.Equal(p2Pass.State.Tick, illegalReorder.State.Tick);
        Assert.Empty(illegalReorder.State.StackItems);
        Assert.Equal(
            p2Pass.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray(),
            illegalReorder.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray());
        Assert.Empty(illegalReorder.State.PlayerZones["P1"].Base);
        Assert.Empty(illegalReorder.State.PlayerZones["P2"].Base);

        var ordered = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-honest-broker-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent("intent-real-honest-broker-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-real-honest-broker-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "HONEST_BROKER_LAST_BREATH_CREATE_GOLD", StringComparison.Ordinal));
        var p2TokenEvent = Assert.Single(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("P2", p2TokenEvent.Payload["playerId"]);
        Assert.Equal("P2-HONEST-BROKER", p2TokenEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-HONEST-BROKER-TOKEN-001", p2TokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("金币", p2TokenEvent.Payload["tokenName"]);
        Assert.Equal(true, p2TokenEvent.Payload["isExhausted"]);
        Assert.Equal(["P2-HONEST-BROKER-TOKEN-001"], p1ResolvesP2Trigger.State.PlayerZones["P2"].Base);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-real-honest-broker-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-real-honest-broker-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        var p1TokenEvent = Assert.Single(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("P1", p1TokenEvent.Payload["playerId"]);
        Assert.Equal("P1-HONEST-BROKER-TOKEN-001", p1TokenEvent.Payload["tokenObjectId"]);
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Equal(["P1-HONEST-BROKER-TOKEN-001"], p2ResolvesP1Trigger.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-HONEST-BROKER-TOKEN-001"], p2ResolvesP1Trigger.State.PlayerZones["P2"].Base);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P1-HONEST-BROKER-TOKEN-001"].IsExhausted);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P2-HONEST-BROKER-TOKEN-001"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard], p2ResolvesP1Trigger.State.CardObjects["P1-HONEST-BROKER-TOKEN-001"].Tags);
        Assert.Equal([CardObjectTags.EquipmentCard], p2ResolvesP1Trigger.State.CardObjects["P2-HONEST-BROKER-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task RealScoutingWarhawkLastBreathTriggersOrderAndCallRuneThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingTwoScoutingWarhawksState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-scouting-warhawk-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-scouting-warhawk-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal(["P1-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P2-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P2"].RuneDeck);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-SCOUTING-WARHAWK", p1Trigger.SourceObjectId);
        Assert.Equal("P2-SCOUTING-WARHAWK", p2Trigger.SourceObjectId);
        Assert.Equal("SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", p1Trigger.EffectKind);
        Assert.All(p2Pass.State.TriggerQueue, trigger => Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind));

        var prompt = p2Pass.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p2TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p2Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal("P2-SCOUTING-WARHAWK", Assert.IsType<string>(p2TriggerView["sourceObjectId"]));
        Assert.Equal("SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", Assert.IsType<string>(p2TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p2TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-scouting-warhawk-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: [p1Trigger.TriggerId, p2Trigger.TriggerId]),
            CancellationToken.None);
        Assert.False(illegalReorder.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, illegalReorder.ErrorCode);
        Assert.Equal(p2Pass.State.Tick, illegalReorder.State.Tick);
        Assert.Empty(illegalReorder.State.StackItems);
        Assert.Equal(
            p2Pass.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray(),
            illegalReorder.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray());
        Assert.Equal(["P1-SCOUTING-WARHAWK-RUNE"], illegalReorder.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P2-SCOUTING-WARHAWK-RUNE"], illegalReorder.State.PlayerZones["P2"].RuneDeck);
        Assert.Empty(illegalReorder.State.PlayerZones["P1"].Base);
        Assert.Empty(illegalReorder.State.PlayerZones["P2"].Base);

        var ordered = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-scouting-warhawk-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent("intent-real-scouting-warhawk-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-real-scouting-warhawk-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", StringComparison.Ordinal));
        var p2RuneEvent = Assert.Single(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P2", p2RuneEvent.Payload["playerId"]);
        Assert.Equal("P2-SCOUTING-WARHAWK", p2RuneEvent.Payload["sourceObjectId"]);
        Assert.Equal(1, p2RuneEvent.Payload["count"]);
        Assert.Equal("SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1", p2RuneEvent.Payload["reason"]);
        Assert.Equal(
            ["P2-SCOUTING-WARHAWK-RUNE"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2RuneEvent.Payload["runeObjectIds"]));
        Assert.Empty(p1ResolvesP2Trigger.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(["P2-SCOUTING-WARHAWK-RUNE"], p1ResolvesP2Trigger.State.PlayerZones["P2"].Base);
        Assert.True(p1ResolvesP2Trigger.State.CardObjects["P2-SCOUTING-WARHAWK-RUNE"].IsExhausted);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-real-scouting-warhawk-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-real-scouting-warhawk-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        var p1RuneEvent = Assert.Single(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P1", p1RuneEvent.Payload["playerId"]);
        Assert.Equal("P1-SCOUTING-WARHAWK", p1RuneEvent.Payload["sourceObjectId"]);
        Assert.Equal(
            ["P1-SCOUTING-WARHAWK-RUNE"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1RuneEvent.Payload["runeObjectIds"]));
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Empty(p2ResolvesP1Trigger.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(p2ResolvesP1Trigger.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(["P1-SCOUTING-WARHAWK-RUNE"], p2ResolvesP1Trigger.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SCOUTING-WARHAWK-RUNE"], p2ResolvesP1Trigger.State.PlayerZones["P2"].Base);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P1-SCOUTING-WARHAWK-RUNE"].IsExhausted);
        Assert.True(p2ResolvesP1Trigger.State.CardObjects["P2-SCOUTING-WARHAWK-RUNE"].IsExhausted);
    }

    [Fact]
    public async Task RealHiddenScoutingWarhawkLastBreathDoesNotEnterTriggerQueue()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingHiddenScoutingWarhawksState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-hidden-scouting-warhawk-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-hidden-scouting-warhawk-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-HIDDEN-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P2-STANDBY-SCOUTING-WARHAWK-RUNE"], p2Pass.State.PlayerZones["P2"].RuneDeck);
    }

    private static async Task<ResolutionResult> ResolveStarfallCleanupAsync(
        CoreRuleEngine engine,
        MatchState state,
        string label)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-cleanup-{label}-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"intent-cleanup-{label}-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal));

        return p2Pass;
    }

    private static async Task<ResolutionResult> OrderAndResolveTwoDrawTriggersThroughStackAsync(
        CoreRuleEngine engine,
        MatchState state,
        string label,
        string effectKind,
        string p1SourceObjectId,
        string p2SourceObjectId,
        string p1DrawObjectId,
        string p2DrawObjectId)
    {
        return await OrderAndResolveTwoDrawTriggersThroughStackAsync(
            engine,
            state,
            label,
            effectKind,
            p1SourceObjectId,
            p2SourceObjectId,
            [p1DrawObjectId],
            [p2DrawObjectId]);
    }

    private static async Task<ResolutionResult> OrderAndResolveTwoDrawTriggersThroughStackAsync(
        CoreRuleEngine engine,
        MatchState state,
        string label,
        string effectKind,
        string p1SourceObjectId,
        string p2SourceObjectId,
        IReadOnlyList<string> p1DrawObjectIds,
        IReadOnlyList<string> p2DrawObjectIds)
    {
        var p1Trigger = Assert.Single(state.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(state.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal(p1SourceObjectId, p1Trigger.SourceObjectId);
        Assert.Equal(p2SourceObjectId, p2Trigger.SourceObjectId);
        Assert.Equal(effectKind, p1Trigger.EffectKind);
        Assert.Equal(effectKind, p2Trigger.EffectKind);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p1TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p1Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal(p1SourceObjectId, Assert.IsType<string>(p1TriggerView["sourceObjectId"]));
        Assert.Equal(effectKind, Assert.IsType<string>(p1TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p1TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-cleanup-{label}-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: [p1Trigger.TriggerId, p2Trigger.TriggerId]),
            CancellationToken.None);
        Assert.False(illegalReorder.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, illegalReorder.ErrorCode);
        Assert.Equal(state.Tick, illegalReorder.State.Tick);
        Assert.Empty(illegalReorder.State.StackItems);
        Assert.Equal(
            state.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray(),
            illegalReorder.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray());
        Assert.Empty(illegalReorder.State.PlayerZones["P1"].Hand);
        Assert.Empty(illegalReorder.State.PlayerZones["P2"].Hand);
        Assert.Equal(p1DrawObjectIds, illegalReorder.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(p2DrawObjectIds, illegalReorder.State.PlayerZones["P2"].MainDeck);

        var ordered = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-cleanup-{label}-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent($"intent-cleanup-{label}-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent($"intent-cleanup-{label}-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, effectKind, StringComparison.Ordinal));
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal));
        Assert.Equal(p2DrawObjectIds, p1ResolvesP2Trigger.State.PlayerZones["P2"].Hand);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent($"intent-cleanup-{label}-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent($"intent-cleanup-{label}-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        Assert.Contains(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));

        return p2ResolvesP1Trigger;
    }

    private static async Task<ResolutionResult> OrderAndResolveTwoGhostlyCentaurTriggersThroughStackAsync(
        CoreRuleEngine engine,
        MatchState state,
        string label,
        string p1SourceObjectId,
        string p2SourceObjectId)
    {
        var p1Trigger = Assert.Single(state.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(state.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal(p1SourceObjectId, p1Trigger.SourceObjectId);
        Assert.Equal(p2SourceObjectId, p2Trigger.SourceObjectId);
        Assert.Equal("GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2", p1Trigger.EffectKind);
        Assert.Equal("GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2", p2Trigger.EffectKind);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var defaultOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal([p2Trigger.TriggerId, p1Trigger.TriggerId], defaultOrder);
        var triggerViews = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["triggers"]).ToArray();
        var p1TriggerView = Assert.Single(triggerViews, trigger =>
            string.Equals(trigger["triggerId"] as string, p1Trigger.TriggerId, StringComparison.Ordinal));
        Assert.Equal(p1SourceObjectId, Assert.IsType<string>(p1TriggerView["sourceObjectId"]));
        Assert.Equal("GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2", Assert.IsType<string>(p1TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p1TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-cleanup-{label}-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: [p1Trigger.TriggerId, p2Trigger.TriggerId]),
            CancellationToken.None);
        Assert.False(illegalReorder.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, illegalReorder.ErrorCode);
        Assert.Equal(state.Tick, illegalReorder.State.Tick);
        Assert.Empty(illegalReorder.State.StackItems);
        Assert.Equal(
            state.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray(),
            illegalReorder.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray());
        Assert.Equal(5, illegalReorder.State.CardObjects[p1SourceObjectId].Power);
        Assert.Equal(5, illegalReorder.State.CardObjects[p2SourceObjectId].Power);
        Assert.DoesNotContain(illegalReorder.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));

        var ordered = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-cleanup-{label}-default-order", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);
        Assert.True(ordered.Accepted, ordered.ErrorMessage);
        Assert.Empty(ordered.State.TriggerQueue);
        Assert.Equal(
            [$"ordered-{p1Trigger.TriggerId}", $"ordered-{p2Trigger.TriggerId}"],
            ordered.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", ordered.State.PriorityPlayerId);
        Assert.Equal(5, ordered.State.CardObjects[p1SourceObjectId].Power);
        Assert.Equal(5, ordered.State.CardObjects[p2SourceObjectId].Power);

        var p2TriggerPass = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent($"intent-cleanup-{label}-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent($"intent-cleanup-{label}-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2", StringComparison.Ordinal));
        var p2PowerEvent = Assert.Single(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Equal(p2SourceObjectId, p2PowerEvent.Payload["sourceObjectId"]);
        Assert.Equal(p2SourceObjectId, p2PowerEvent.Payload["targetObjectId"]);
        Assert.Equal(2, p2PowerEvent.Payload["appliedPowerDelta"]);
        Assert.Equal(7, p1ResolvesP2Trigger.State.CardObjects[p2SourceObjectId].Power);
        Assert.Equal(5, p1ResolvesP2Trigger.State.CardObjects[p1SourceObjectId].Power);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent($"intent-cleanup-{label}-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent($"intent-cleanup-{label}-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        var p1PowerEvent = Assert.Single(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Equal(p1SourceObjectId, p1PowerEvent.Payload["sourceObjectId"]);
        Assert.Equal(p1SourceObjectId, p1PowerEvent.Payload["targetObjectId"]);
        Assert.Equal(2, p1PowerEvent.Payload["appliedPowerDelta"]);

        return p2ResolvesP1Trigger;
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

    private static MatchState BuildSpiritFireDestroyingTwoScoutingWarhawksState()
    {
        return BuildSpiritFireDestroyingScoutingWarhawksState(
            "real-scouting-warhawk-trigger-room",
            "P1-SCOUTING-WARHAWK",
            "P2-SCOUTING-WARHAWK",
            "P1-SCOUTING-WARHAWK-RUNE",
            "P2-SCOUTING-WARHAWK-RUNE",
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "鸟类"],
            p2Tags: [CardObjectTags.UnitCard, "鸟类"]);
    }

    private static MatchState BuildSpiritFireDestroyingHiddenScoutingWarhawksState()
    {
        return BuildSpiritFireDestroyingScoutingWarhawksState(
            "real-hidden-scouting-warhawk-trigger-room",
            "P1-HIDDEN-SCOUTING-WARHAWK",
            "P2-STANDBY-SCOUTING-WARHAWK",
            "P1-HIDDEN-SCOUTING-WARHAWK-RUNE",
            "P2-STANDBY-SCOUTING-WARHAWK-RUNE",
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "鸟类"],
            p2Tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "鸟类"]);
    }

    private static MatchState BuildSpiritFireDestroyingScoutingWarhawksState(
        string roomId,
        string p1WarhawkObjectId,
        string p2WarhawkObjectId,
        string p1RuneObjectId,
        string p2RuneObjectId,
        bool p1IsFaceDown,
        bool p2IsFaceDown,
        IReadOnlyList<string> p1Tags,
        IReadOnlyList<string> p2Tags)
    {
        return new MatchState(
            roomId,
            23,
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
                    RuneDeck = [p1RuneObjectId],
                    Base = [p1WarhawkObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    RuneDeck = [p2RuneObjectId],
                    Base = [p2WarhawkObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [p1WarhawkObjectId] = new(
                    p1WarhawkObjectId,
                    cardNo: "OGN·216/298",
                    power: 1,
                    isFaceDown: p1IsFaceDown,
                    tags: p1Tags,
                    ownerId: "P1",
                    controllerId: "P1"),
                [p2WarhawkObjectId] = new(
                    p2WarhawkObjectId,
                    cardNo: "OGN·216/298",
                    power: 1,
                    isFaceDown: p2IsFaceDown,
                    tags: p2Tags,
                    ownerId: "P2",
                    controllerId: "P2"),
                [p1RuneObjectId] = new(
                    p1RuneObjectId,
                    ownerId: "P1",
                    controllerId: "P1",
                    tags: [CardObjectTags.RuneCard]),
                [p2RuneObjectId] = new(
                    p2RuneObjectId,
                    ownerId: "P2",
                    controllerId: "P2",
                    tags: [CardObjectTags.RuneCard]),
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
                    "STACK-SPIRIT-FIRE-SCOUTING-WARHAWKS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    [p1WarhawkObjectId, p2WarhawkObjectId])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingTwoWatchfulSentinelsState()
    {
        return BuildStarfallDestroyingWatchfulSentinelsState(
            "cleanup-watchful-trigger-room",
            "P1-CLEANUP-WATCHFUL-SENTINEL",
            "P2-CLEANUP-WATCHFUL-SENTINEL",
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard]);
    }

    private static MatchState BuildStarfallDestroyingHiddenWatchfulSentinelsState()
    {
        return BuildStarfallDestroyingWatchfulSentinelsState(
            "cleanup-hidden-watchful-trigger-room",
            "P1-HIDDEN-WATCHFUL-SENTINEL",
            "P2-STANDBY-WATCHFUL-SENTINEL",
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]);
    }

    private static MatchState BuildStarfallDestroyingTwoHonestBrokersState()
    {
        return BuildStarfallDestroyingHonestBrokersState(
            "cleanup-honest-broker-trigger-room",
            "P1-CLEANUP-HONEST-BROKER",
            "P2-CLEANUP-HONEST-BROKER",
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard]);
    }

    private static MatchState BuildStarfallDestroyingHiddenHonestBrokersState()
    {
        return BuildStarfallDestroyingHonestBrokersState(
            "cleanup-hidden-honest-broker-trigger-room",
            "P1-HIDDEN-HONEST-BROKER",
            "P2-STANDBY-HONEST-BROKER",
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]);
    }

    private static MatchState BuildStarfallDestroyingTwoScoutingWarhawksState()
    {
        return BuildStarfallDestroyingScoutingWarhawksState(
            "cleanup-scouting-warhawk-trigger-room",
            "P1-CLEANUP-SCOUTING-WARHAWK",
            "P2-CLEANUP-SCOUTING-WARHAWK",
            "P1-CLEANUP-SCOUTING-WARHAWK-RUNE",
            "P2-CLEANUP-SCOUTING-WARHAWK-RUNE",
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "鸟类"],
            p2Tags: [CardObjectTags.UnitCard, "鸟类"]);
    }

    private static MatchState BuildStarfallDestroyingHiddenScoutingWarhawksState()
    {
        return BuildStarfallDestroyingScoutingWarhawksState(
            "cleanup-hidden-scouting-warhawk-trigger-room",
            "P1-CLEANUP-HIDDEN-SCOUTING-WARHAWK",
            "P2-CLEANUP-STANDBY-SCOUTING-WARHAWK",
            "P1-CLEANUP-HIDDEN-SCOUTING-WARHAWK-RUNE",
            "P2-CLEANUP-STANDBY-SCOUTING-WARHAWK-RUNE",
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "鸟类"],
            p2Tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "鸟类"]);
    }

    private static MatchState BuildStarfallDestroyingGhostlyCentaurFriendlyUnitsState()
    {
        return new MatchState(
            "cleanup-ghostly-centaur-trigger-room",
            41,
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
                    Base = ["P1-CLEANUP-GHOSTLY-CENTAUR", "P1-CLEANUP-GHOSTLY-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-CLEANUP-GHOSTLY-CENTAUR", "P2-CLEANUP-GHOSTLY-TARGET"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-GHOSTLY-CENTAUR"] = new(
                    "P1-CLEANUP-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-GHOSTLY-CENTAUR"] = new(
                    "P2-CLEANUP-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-GHOSTLY-TARGET"] = new(
                    "P1-CLEANUP-GHOSTLY-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-GHOSTLY-TARGET"] = new(
                    "P2-CLEANUP-GHOSTLY-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-GHOSTLY-CENTAURS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-GHOSTLY-TARGET", "P2-CLEANUP-GHOSTLY-TARGET"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingFriendlyUnitsWithInvalidGhostlySourcesState()
    {
        return new MatchState(
            "cleanup-hidden-ghostly-centaur-trigger-room",
            42,
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
                    Base =
                    [
                        "P1-HIDDEN-GHOSTLY-CENTAUR",
                        "P1-STANDBY-GHOSTLY-CENTAUR",
                        "P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-1",
                        "P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-2"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-OPPONENT-GHOSTLY-CENTAUR"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HIDDEN-GHOSTLY-CENTAUR"] = new(
                    "P1-HIDDEN-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STANDBY-GHOSTLY-CENTAUR"] = new(
                    "P1-STANDBY-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-OPPONENT-GHOSTLY-CENTAUR"] = new(
                    "P2-OPPONENT-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-1"] = new(
                    "P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-2"] = new(
                    "P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-2",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-HIDDEN-GHOSTLY-CENTAURS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-1", "P1-CLEANUP-GHOSTLY-HIDDEN-TARGET-2"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingGhostlyCentaurAndFriendlyUnitState()
    {
        return new MatchState(
            "cleanup-ghostly-centaur-source-also-dies-room",
            43,
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
                    Base = ["P1-DYING-GHOSTLY-CENTAUR", "P1-DYING-GHOSTLY-FRIEND"]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DYING-GHOSTLY-CENTAUR"] = new(
                    "P1-DYING-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DYING-GHOSTLY-FRIEND"] = new(
                    "P1-DYING-GHOSTLY-FRIEND",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-DYING-GHOSTLY-CENTAUR",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-DYING-GHOSTLY-CENTAUR", "P1-DYING-GHOSTLY-FRIEND"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingResonantSoulFriendlyUnitsState()
    {
        return new MatchState(
            "cleanup-resonant-soul-trigger-room",
            44,
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
                    MainDeck = ["P1-RESONANT-SOUL-DRAW-001"],
                    Base = ["P1-CLEANUP-RESONANT-SOUL", "P1-CLEANUP-RESONANT-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-RESONANT-SOUL-DRAW-001"],
                    Base = ["P2-CLEANUP-RESONANT-SOUL", "P2-CLEANUP-RESONANT-TARGET"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-RESONANT-SOUL"] = new(
                    "P1-CLEANUP-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-RESONANT-SOUL"] = new(
                    "P2-CLEANUP-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-RESONANT-TARGET"] = new(
                    "P1-CLEANUP-RESONANT-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-RESONANT-TARGET"] = new(
                    "P2-CLEANUP-RESONANT-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-RESONANT-SOULS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-RESONANT-TARGET", "P2-CLEANUP-RESONANT-TARGET"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingFriendlyUnitsWithInvalidResonantSoulSourcesState()
    {
        return new MatchState(
            "cleanup-hidden-resonant-soul-trigger-room",
            45,
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
                    MainDeck = ["P1-RESONANT-SOUL-DRAW-001"],
                    Base =
                    [
                        "P1-HIDDEN-RESONANT-SOUL",
                        "P1-STANDBY-RESONANT-SOUL",
                        "P1-CLEANUP-RESONANT-HIDDEN-TARGET-1",
                        "P1-CLEANUP-RESONANT-HIDDEN-TARGET-2"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-OPPONENT-RESONANT-SOUL"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HIDDEN-RESONANT-SOUL"] = new(
                    "P1-HIDDEN-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STANDBY-RESONANT-SOUL"] = new(
                    "P1-STANDBY-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-OPPONENT-RESONANT-SOUL"] = new(
                    "P2-OPPONENT-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-RESONANT-HIDDEN-TARGET-1"] = new(
                    "P1-CLEANUP-RESONANT-HIDDEN-TARGET-1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CLEANUP-RESONANT-HIDDEN-TARGET-2"] = new(
                    "P1-CLEANUP-RESONANT-HIDDEN-TARGET-2",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-HIDDEN-RESONANT-SOULS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-RESONANT-HIDDEN-TARGET-1", "P1-CLEANUP-RESONANT-HIDDEN-TARGET-2"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingResonantSoulAndFriendlyUnitState()
    {
        return new MatchState(
            "cleanup-resonant-soul-source-also-dies-room",
            46,
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
                    MainDeck = ["P1-RESONANT-SOUL-DRAW-001"],
                    Base = ["P1-DYING-RESONANT-SOUL", "P1-DYING-RESONANT-FRIEND"]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DYING-RESONANT-SOUL"] = new(
                    "P1-DYING-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DYING-RESONANT-FRIEND"] = new(
                    "P1-DYING-RESONANT-FRIEND",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-DYING-RESONANT-SOUL",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-DYING-RESONANT-SOUL", "P1-DYING-RESONANT-FRIEND"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingSadPorosState()
    {
        return new MatchState(
            "cleanup-sad-poro-trigger-room",
            31,
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
                    MainDeck = ["P1-SAD-PORO-DRAW-001"],
                    Base = ["P1-CLEANUP-SAD-PORO-SFD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-SAD-PORO-DRAW-001"],
                    Base = ["P2-CLEANUP-SAD-PORO-UNL"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-SAD-PORO-SFD"] = new(
                    "P1-CLEANUP-SAD-PORO-SFD",
                    cardNo: "SFD·036/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-SAD-PORO-UNL"] = new(
                    "P2-CLEANUP-SAD-PORO-UNL",
                    cardNo: "UNL-221/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-SAD-POROS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-SAD-PORO-SFD", "P2-CLEANUP-SAD-PORO-UNL"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingSadPoroWithAllyState()
    {
        return new MatchState(
            "cleanup-sad-poro-not-isolated-room",
            32,
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
                    MainDeck = ["P1-SAD-PORO-DRAW-001"],
                    Base = ["P1-CLEANUP-SAD-PORO-SFD", "P1-CLEANUP-SAD-PORO-ALLY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-SAD-PORO-SFD"] = new(
                    "P1-CLEANUP-SAD-PORO-SFD",
                    cardNo: "SFD·036/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CLEANUP-SAD-PORO-ALLY"] = new(
                    "P1-CLEANUP-SAD-PORO-ALLY",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-SAD-PORO-NOT-ISOLATED",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-SAD-PORO-SFD", "P1-CLEANUP-SAD-PORO-ALLY"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingLoyalPorosWithAlliesState()
    {
        return new MatchState(
            "cleanup-loyal-poro-trigger-room",
            33,
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
                    MainDeck = ["P1-LOYAL-PORO-DRAW-001"],
                    Base = ["P1-CLEANUP-LOYAL-PORO", "P1-CLEANUP-LOYAL-PORO-ALLY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-LOYAL-PORO-DRAW-001"],
                    Base = ["P2-CLEANUP-LOYAL-PORO", "P2-CLEANUP-LOYAL-PORO-ALLY"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-LOYAL-PORO"] = new(
                    "P1-CLEANUP-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-LOYAL-PORO"] = new(
                    "P2-CLEANUP-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-LOYAL-PORO-ALLY"] = new(
                    "P1-CLEANUP-LOYAL-PORO-ALLY",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-LOYAL-PORO-ALLY"] = new(
                    "P2-CLEANUP-LOYAL-PORO-ALLY",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-LOYAL-POROS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-LOYAL-PORO", "P2-CLEANUP-LOYAL-PORO"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingIsolatedLoyalPorosState()
    {
        return new MatchState(
            "cleanup-isolated-loyal-poro-room",
            34,
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
                    MainDeck = ["P1-LOYAL-PORO-DRAW-001"],
                    Base = ["P1-CLEANUP-LOYAL-PORO"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-LOYAL-PORO-DRAW-001"],
                    Base = ["P2-CLEANUP-LOYAL-PORO"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-LOYAL-PORO"] = new(
                    "P1-CLEANUP-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-LOYAL-PORO"] = new(
                    "P2-CLEANUP-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-LOYAL-POROS-ISOLATED",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-LOYAL-PORO", "P2-CLEANUP-LOYAL-PORO"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingLoyalPoroWithDyingAllyState()
    {
        return new MatchState(
            "cleanup-loyal-poro-ally-also-dies-room",
            36,
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
                    MainDeck = ["P1-LOYAL-PORO-DRAW-001"],
                    Base = ["P1-CLEANUP-LOYAL-PORO", "P1-CLEANUP-LOYAL-PORO-DYING-ALLY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-LOYAL-PORO"] = new(
                    "P1-CLEANUP-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CLEANUP-LOYAL-PORO-DYING-ALLY"] = new(
                    "P1-CLEANUP-LOYAL-PORO-DYING-ALLY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-LOYAL-PORO-ALLY-ALSO-DIES",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-LOYAL-PORO", "P1-CLEANUP-LOYAL-PORO-DYING-ALLY"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingHiddenPorosState()
    {
        return new MatchState(
            "cleanup-hidden-poros-room",
            35,
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
                    MainDeck = ["P1-HIDDEN-PORO-DRAW-001"],
                    Base = ["P1-CLEANUP-HIDDEN-SAD-PORO"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-STANDBY-PORO-DRAW-001"],
                    Base = ["P2-CLEANUP-STANDBY-LOYAL-PORO", "P2-CLEANUP-STANDBY-LOYAL-PORO-ALLY"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-HIDDEN-SAD-PORO"] = new(
                    "P1-CLEANUP-HIDDEN-SAD-PORO",
                    cardNo: "SFD·036/221",
                    power: 3,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-STANDBY-LOYAL-PORO"] = new(
                    "P2-CLEANUP-STANDBY-LOYAL-PORO",
                    cardNo: "UNL-156/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-CLEANUP-STANDBY-LOYAL-PORO-ALLY"] = new(
                    "P2-CLEANUP-STANDBY-LOYAL-PORO-ALLY",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-HIDDEN-POROS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-HIDDEN-SAD-PORO", "P2-CLEANUP-STANDBY-LOYAL-PORO"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingPowerfulUnsungHeroesState()
    {
        return BuildStarfallDestroyingUnsungHeroesState(
            "cleanup-unsung-hero-trigger-room",
            "P1-CLEANUP-UNSUNG-HERO",
            "P2-CLEANUP-UNSUNG-HERO",
            ["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"],
            ["P2-UNSUNG-HERO-DRAW-001", "P2-UNSUNG-HERO-DRAW-002"],
            p1Power: 5,
            p2Power: 5,
            p1Damage: 2,
            p2Damage: 2,
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "精锐"],
            p2Tags: [CardObjectTags.UnitCard, "精锐"]);
    }

    private static MatchState BuildStarfallDestroyingBelowPowerfulUnsungHeroesState()
    {
        return BuildStarfallDestroyingUnsungHeroesState(
            "cleanup-unsung-hero-below-powerful-room",
            "P1-CLEANUP-UNSUNG-HERO",
            "P2-CLEANUP-UNSUNG-HERO",
            ["P1-UNSUNG-HERO-DRAW-001", "P1-UNSUNG-HERO-DRAW-002"],
            ["P2-UNSUNG-HERO-DRAW-001", "P2-UNSUNG-HERO-DRAW-002"],
            p1Power: 4,
            p2Power: 4,
            p1Damage: 1,
            p2Damage: 1,
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "精锐"],
            p2Tags: [CardObjectTags.UnitCard, "精锐"]);
    }

    private static MatchState BuildStarfallDestroyingHiddenUnsungHeroesState()
    {
        return BuildStarfallDestroyingUnsungHeroesState(
            "cleanup-hidden-unsung-hero-room",
            "P1-CLEANUP-HIDDEN-UNSUNG-HERO",
            "P2-CLEANUP-STANDBY-UNSUNG-HERO",
            ["P1-HIDDEN-UNSUNG-HERO-DRAW-001", "P1-HIDDEN-UNSUNG-HERO-DRAW-002"],
            ["P2-STANDBY-UNSUNG-HERO-DRAW-001", "P2-STANDBY-UNSUNG-HERO-DRAW-002"],
            p1Power: 5,
            p2Power: 5,
            p1Damage: 2,
            p2Damage: 2,
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "精锐"],
            p2Tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "精锐"]);
    }

    private static MatchState BuildStarfallDestroyingUnsungHeroesState(
        string roomId,
        string p1UnsungHeroObjectId,
        string p2UnsungHeroObjectId,
        IReadOnlyList<string> p1MainDeck,
        IReadOnlyList<string> p2MainDeck,
        int p1Power,
        int p2Power,
        int p1Damage,
        int p2Damage,
        bool p1IsFaceDown,
        bool p2IsFaceDown,
        IReadOnlyList<string> p1Tags,
        IReadOnlyList<string> p2Tags)
    {
        return new MatchState(
            roomId,
            37,
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
                    MainDeck = p1MainDeck,
                    Base = [p1UnsungHeroObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = p2MainDeck,
                    Base = [p2UnsungHeroObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [p1UnsungHeroObjectId] = new(
                    p1UnsungHeroObjectId,
                    cardNo: "SFD·167/221",
                    power: p1Power,
                    damage: p1Damage,
                    isFaceDown: p1IsFaceDown,
                    tags: p1Tags,
                    ownerId: "P1",
                    controllerId: "P1"),
                [p2UnsungHeroObjectId] = new(
                    p2UnsungHeroObjectId,
                    cardNo: "SFD·167/221",
                    power: p2Power,
                    damage: p2Damage,
                    isFaceDown: p2IsFaceDown,
                    tags: p2Tags,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    $"STACK-STARFALL-{roomId}",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    [p1UnsungHeroObjectId, p2UnsungHeroObjectId])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingWatchfulSentinelsState(
        string roomId,
        string p1WatchfulObjectId,
        string p2WatchfulObjectId,
        bool p1IsFaceDown,
        bool p2IsFaceDown,
        IReadOnlyList<string> p1Tags,
        IReadOnlyList<string> p2Tags)
    {
        return new MatchState(
            roomId,
            17,
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
                    MainDeck = ["P1-CLEANUP-DRAW-001"],
                    Base = [p1WatchfulObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-CLEANUP-DRAW-001"],
                    Base = [p2WatchfulObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [p1WatchfulObjectId] = new(
                    p1WatchfulObjectId,
                    cardNo: "OGN·096/298",
                    power: 3,
                    isFaceDown: p1IsFaceDown,
                    tags: p1Tags,
                    ownerId: "P1",
                    controllerId: "P1"),
                [p2WatchfulObjectId] = new(
                    p2WatchfulObjectId,
                    cardNo: "OGN·096/298",
                    power: 3,
                    isFaceDown: p2IsFaceDown,
                    tags: p2Tags,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-WATCHFUL",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    [p1WatchfulObjectId, p2WatchfulObjectId])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingHonestBrokersState(
        string roomId,
        string p1HonestBrokerObjectId,
        string p2HonestBrokerObjectId,
        bool p1IsFaceDown,
        bool p2IsFaceDown,
        IReadOnlyList<string> p1Tags,
        IReadOnlyList<string> p2Tags)
    {
        return new MatchState(
            roomId,
            19,
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
                    Base = [p1HonestBrokerObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [p2HonestBrokerObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [p1HonestBrokerObjectId] = new(
                    p1HonestBrokerObjectId,
                    cardNo: "SFD·155/221",
                    power: 3,
                    isFaceDown: p1IsFaceDown,
                    tags: p1Tags,
                    ownerId: "P1",
                    controllerId: "P1"),
                [p2HonestBrokerObjectId] = new(
                    p2HonestBrokerObjectId,
                    cardNo: "SFD·155/221",
                    power: 3,
                    isFaceDown: p2IsFaceDown,
                    tags: p2Tags,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-HONEST-BROKER",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    [p1HonestBrokerObjectId, p2HonestBrokerObjectId])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingScoutingWarhawksState(
        string roomId,
        string p1WarhawkObjectId,
        string p2WarhawkObjectId,
        string p1RuneObjectId,
        string p2RuneObjectId,
        bool p1IsFaceDown,
        bool p2IsFaceDown,
        IReadOnlyList<string> p1Tags,
        IReadOnlyList<string> p2Tags)
    {
        return new MatchState(
            roomId,
            29,
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
                    RuneDeck = [p1RuneObjectId],
                    Base = [p1WarhawkObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    RuneDeck = [p2RuneObjectId],
                    Base = [p2WarhawkObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [p1WarhawkObjectId] = new(
                    p1WarhawkObjectId,
                    cardNo: "OGN·216/298",
                    power: 3,
                    isFaceDown: p1IsFaceDown,
                    tags: p1Tags,
                    ownerId: "P1",
                    controllerId: "P1"),
                [p2WarhawkObjectId] = new(
                    p2WarhawkObjectId,
                    cardNo: "OGN·216/298",
                    power: 3,
                    isFaceDown: p2IsFaceDown,
                    tags: p2Tags,
                    ownerId: "P2",
                    controllerId: "P2"),
                [p1RuneObjectId] = new(
                    p1RuneObjectId,
                    ownerId: "P1",
                    controllerId: "P1",
                    tags: [CardObjectTags.RuneCard]),
                [p2RuneObjectId] = new(
                    p2RuneObjectId,
                    ownerId: "P2",
                    controllerId: "P2",
                    tags: [CardObjectTags.RuneCard]),
                ["P1-SPELL-STARFALL"] = new(
                    "P1-SPELL-STARFALL",
                    cardNo: "OGN·029/298",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-STARFALL-CLEANUP-SCOUTING-WARHAWKS",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    [p1WarhawkObjectId, p2WarhawkObjectId])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingTwoHonestBrokersState()
    {
        return new MatchState(
            "real-honest-broker-trigger-room",
            11,
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
                    Base = ["P1-HONEST-BROKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-HONEST-BROKER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HONEST-BROKER"] = new(
                    "P1-HONEST-BROKER",
                    cardNo: "SFD·155/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-HONEST-BROKER"] = new(
                    "P2-HONEST-BROKER",
                    cardNo: "SFD·155/221",
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
                    "STACK-SPIRIT-FIRE-HONEST-BROKERS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-HONEST-BROKER", "P2-HONEST-BROKER"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }
}
