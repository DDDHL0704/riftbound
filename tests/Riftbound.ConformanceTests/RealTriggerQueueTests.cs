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
