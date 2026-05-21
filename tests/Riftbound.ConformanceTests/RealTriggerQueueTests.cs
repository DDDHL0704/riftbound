using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RealTriggerQueueTests
{
    private const string OgsLuxHighCostSpellEffectKind = "OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3";

    [Fact]
    public async Task LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxHighCostSpellState();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-high-cost-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-EVOLUTION-DAY", "OGN·114/298", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var lux = result.State.CardObjects["P1-LUX"];
        Assert.Equal(8, lux.Power);
        Assert.Equal(3, lux.UntilEndOfTurnPowerModifier);
        Assert.Single(result.State.StackItems);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LUX", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, OgsLuxHighCostSpellEffectKind, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["triggeredByEventKind"] as string, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LUX", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, OgsLuxHighCostSpellEffectKind, StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LUX", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-LUX", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["powerDelta"], 3)
            && Equals(gameEvent.Payload["appliedPowerDelta"], 3)
            && Equals(gameEvent.Payload["resultingPower"], 8));
    }

    [Fact]
    public async Task LuxLowCostSpellDoesNotTrigger()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxLowCostSpellState();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-low-cost-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-MIGHT-MAKES-RIGHT", "SFD·106/221", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        AssertLuxDidNotTrigger(result);
        AssertLuxPowerUnchanged(result);
    }

    [Fact]
    public async Task LuxOpponentHighCostSpellDoesNotTrigger()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxHighCostSpellState(
            spellPlayerId: "P2",
            activePlayerId: "P2",
            turnPlayerId: "P2",
            spellObjectId: "P2-SPELL-EVOLUTION-DAY");

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-opponent-high-cost-spell", "P2", CommandTypes.PlayCard),
            new PlayCardCommand("P2-SPELL-EVOLUTION-DAY", "OGN·114/298", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        AssertLuxDidNotTrigger(result);
        AssertLuxPowerUnchanged(result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LuxHiddenOrStandbyHighCostSpellDoesNotTrigger(bool isFaceDown, bool isStandby)
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxHighCostSpellState(luxIsFaceDown: isFaceDown, luxIsStandby: isStandby);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-hidden-or-standby-high-cost-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-EVOLUTION-DAY", "OGN·114/298", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        AssertLuxDidNotTrigger(result);
        AssertLuxPowerUnchanged(result);
    }

    [Fact]
    public async Task LuxInvalidSourceNotOnFieldDoesNotTrigger()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxHighCostSpellState(luxOnField: false);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-invalid-source-high-cost-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-EVOLUTION-DAY", "OGN·114/298", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        AssertLuxDidNotTrigger(result);
        AssertLuxPowerUnchanged(result);
    }

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

        var orderedStateHash = MatchStateHasher.Hash(ordered.State);
        var orderedStackItemIds = ordered.State.StackItems.Select(item => item.StackItemId).ToArray();
        var replay = await engine.ResolveAsync(
            ordered.State,
            new PlayerIntent("intent-cleanup-watchful-default-order-replay", "P1", CommandTypes.OrderTriggers),
            new OrderTriggersCommand(OrderedTriggerIds: defaultOrder),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(orderedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Empty(replay.State.TriggerQueue);
        Assert.Equal(orderedStackItemIds, replay.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", replay.State.PriorityPlayerId);
        Assert.Empty(replay.State.PlayerZones["P1"].Hand);
        Assert.Empty(replay.State.PlayerZones["P2"].Hand);
        Assert.NotEqual(PromptTypes.OrderTriggers, replay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.OrderTriggers, replay.Prompts["P2"].View?.Type);

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
    public async Task StateBasedCleanupMechanicalTrickstersTriggerOrderAndCreateMinionsThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingTwoMechanicalTrickstersState(),
            "mechanical-tricksters");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Base);

        var final = await OrderAndResolveTwoUnitTokenTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "mechanical-tricksters",
            "MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS",
            "P1-CLEANUP-MECHANICAL-TRICKSTER",
            "P2-CLEANUP-MECHANICAL-TRICKSTER",
            "随从",
            1,
            [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily],
            [
                "P1-CLEANUP-MECHANICAL-TRICKSTER-TOKEN-001",
                "P1-CLEANUP-MECHANICAL-TRICKSTER-TOKEN-002",
                "P1-CLEANUP-MECHANICAL-TRICKSTER-TOKEN-003"
            ],
            [
                "P2-CLEANUP-MECHANICAL-TRICKSTER-TOKEN-001",
                "P2-CLEANUP-MECHANICAL-TRICKSTER-TOKEN-002",
                "P2-CLEANUP-MECHANICAL-TRICKSTER-TOKEN-003"
            ]);

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
    }

    [Fact]
    public async Task StateBasedCleanupIroncladVanguardsTriggerOrderAndCreateRobotsThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingTwoIroncladVanguardsState(),
            "ironclad-vanguards");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Base);

        var final = await OrderAndResolveTwoUnitTokenTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "ironclad-vanguards",
            "IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS",
            "P1-CLEANUP-IRONCLAD-VANGUARD",
            "P2-CLEANUP-IRONCLAD-VANGUARD",
            "机器人",
            3,
            [CardObjectTags.UnitCard, "机械"],
            [
                "P1-CLEANUP-IRONCLAD-VANGUARD-TOKEN-001",
                "P1-CLEANUP-IRONCLAD-VANGUARD-TOKEN-002"
            ],
            [
                "P2-CLEANUP-IRONCLAD-VANGUARD-TOKEN-001",
                "P2-CLEANUP-IRONCLAD-VANGUARD-TOKEN-002"
            ]);

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
    }

    [Fact]
    public async Task StateBasedCleanupMuddyDredgersTriggerOrderAndCreateWarhawksThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingTwoMuddyDredgersState(),
            "muddy-dredgers");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("P1-CLEANUP-MUDDY-DREDGER-TOKEN-001", cleanup.State.CardObjects.Keys);
        Assert.DoesNotContain("P2-CLEANUP-MUDDY-DREDGER-TOKEN-001", cleanup.State.CardObjects.Keys);

        var final = await OrderAndResolveTwoUnitTokenTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "muddy-dredgers",
            "MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK",
            "P1-CLEANUP-MUDDY-DREDGER",
            "P2-CLEANUP-MUDDY-DREDGER",
            "战鹰",
            1,
            [CardObjectTags.UnitCard, CardObjectTags.Spellshield, "鸟类"],
            ["P1-CLEANUP-MUDDY-DREDGER-TOKEN-001"],
            ["P2-CLEANUP-MUDDY-DREDGER-TOKEN-001"],
            expectedTokenCardNo: "UNL·T02",
            expectedEventTags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield, "鸟类"]);

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        AssertWarhawkToken(final.State, "P1-CLEANUP-MUDDY-DREDGER-TOKEN-001", "P1");
        AssertWarhawkToken(final.State, "P2-CLEANUP-MUDDY-DREDGER-TOKEN-001", "P2");
    }

    [Fact]
    public async Task StateBasedCleanupHiddenAndStandbyMuddyDredgersDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingHiddenMuddyDredgersState(),
            "hidden-muddy-dredgers");

        AssertNoMuddyDredgerWarhawkLeak(cleanup);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task StateBasedCleanupInvalidMuddyDredgerSourcesDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingInvalidMuddyDredgerSourcesState(),
            "invalid-muddy-dredgers");

        AssertNoMuddyDredgerWarhawkLeak(cleanup);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenMechanicalTrickstersDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingHiddenMechanicalTrickstersState(),
            "hidden-mechanical-tricksters");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenIroncladVanguardsDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingHiddenIroncladVanguardsState(),
            "hidden-ironclad-vanguards");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Empty(cleanup.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task RealKogmawLastBreathDealsFourToDestroyedBattlefieldAndCleanupStabilizes()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingKogmawOnBattlefieldState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-kogmaw-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-kogmaw-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var triggerQueued = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-KOGMAW", triggerQueued.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-KOGMAW", triggerQueued.Payload["battlefieldObjectId"]);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-KOGMAW", StringComparison.Ordinal));

        var final = await ResolveSingleKogmawTriggerThroughStackAsync(
            engine,
            p2Pass.State,
            "real-kogmaw",
            expectedDestroyedVictimObjectId: "P2-KOGMAW-SAME-BATTLEFIELD-VICTIM");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(4, final.State.CardObjects["P1-KOGMAW-SAME-BATTLEFIELD-ALLY"].Damage);
        Assert.Equal(0, final.State.CardObjects["P1-KOGMAW-OTHER-BATTLEFIELD-ALLY"].Damage);
        Assert.False(final.State.CardObjects.ContainsKey("P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"));
        Assert.Equal(["P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"], final.State.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task StateBasedCleanupKogmawLastBreathDealsFourToDestroyedBattlefield()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingKogmawOnBattlefieldState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-kogmaw-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-kogmaw-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-KOGMAW", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.TriggerQueue);
        var triggerStackItem = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("P1-KOGMAW", triggerStackItem.SourceObjectId);
        Assert.Equal("OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT", triggerStackItem.EffectKind);

        var final = await ResolveSingleKogmawTriggerThroughStackAsync(
            engine,
            p2Pass.State,
            "cleanup-kogmaw",
            expectedDestroyedVictimObjectId: "P2-KOGMAW-SAME-BATTLEFIELD-VICTIM");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(4, final.State.CardObjects["P1-KOGMAW-SAME-BATTLEFIELD-ALLY"].Damage);
        Assert.Equal(3, final.State.CardObjects["P1-KOGMAW-OTHER-BATTLEFIELD-ALLY"].Damage);
        Assert.False(final.State.CardObjects.ContainsKey("P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"));
    }

    [Fact]
    public async Task StateBasedCleanupHiddenKogmawsDoNotEnqueueOrDealAoeDamage()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingHiddenKogmawsOnBattlefieldState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-hidden-kogmaw-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-hidden-kogmaw-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && ((gameEvent.Payload["sourceObjectId"] as string) ?? string.Empty).Contains("KOGMAW", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
        Assert.Equal(0, p2Pass.State.CardObjects["P1-KOGMAW-HIDDEN-BYSTANDER"].Damage);
    }

    [Fact]
    public async Task RealKogmawDestroyedWithoutBattlefieldLocationDoesNotEnqueueOrDealDamage()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingKogmawWithoutBattlefieldLocationState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-kogmaw-unsupported-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-kogmaw-unsupported-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Equal(0, p2Pass.State.CardObjects["P1-KOGMAW-SAME-BATTLEFIELD-ALLY"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"].Damage);
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
    public async Task StateBasedCleanupSavageJawfishTriggersOrderAndGainExperienceThroughStack()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingSavageJawfishFriendlyUnitsState(),
            "savage-jawfish");

        Assert.Equal(2, cleanup.State.TriggerQueue.Count);
        Assert.All(cleanup.State.TriggerQueue, trigger =>
        {
            Assert.Equal("SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1", trigger.EffectKind);
            Assert.Equal("UNIT_DESTROYED", trigger.TriggeredByEventKind);
        });
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal(0, cleanup.State.PlayerExperience["P1"]);
        Assert.Equal(0, cleanup.State.PlayerExperience["P2"]);

        var final = await OrderAndResolveTwoSavageJawfishTriggersThroughStackAsync(
            engine,
            cleanup.State,
            "savage-jawfish",
            "P1-CLEANUP-SAVAGE-JAWFISH",
            "P2-CLEANUP-SAVAGE-JAWFISH");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(1, final.State.PlayerExperience["P1"]);
        Assert.Equal(1, final.State.PlayerExperience["P2"]);
    }

    [Fact]
    public async Task StateBasedCleanupHiddenSavageJawfishDoNotEnqueueTriggers()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingFriendlyUnitsWithInvalidSavageJawfishSourcesState(),
            "hidden-savage-jawfish");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
        Assert.Equal(0, cleanup.State.PlayerExperience["P1"]);
        Assert.Equal(0, cleanup.State.PlayerExperience["P2"]);
    }

    [Fact]
    public async Task StateBasedCleanupSavageJawfishSkipsWhenSourceAlsoDies()
    {
        var engine = new CoreRuleEngine();
        var cleanup = await ResolveStarfallCleanupAsync(
            engine,
            BuildStarfallDestroyingSavageJawfishAndFriendlyUnitState(),
            "savage-jawfish-source-also-dies");

        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Empty(cleanup.State.PlayerZones["P1"].Base);
        Assert.Equal(0, cleanup.State.PlayerExperience["P1"]);
        Assert.Equal(0, cleanup.State.PlayerExperience["P2"]);
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task RealGhostlyCentaurFriendlyDestroyedTriggersEnterApnapOrderWindowAndGainPowerThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingGhostlyCentaurFriendlyUnitsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-ghostly-centaur-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-ghostly-centaur-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)));
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Equal(5, p2Pass.State.CardObjects["P1-REAL-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(5, p2Pass.State.CardObjects["P2-REAL-GHOSTLY-CENTAUR"].Power);

        var final = await OrderAndResolveTwoGhostlyCentaurTriggersThroughStackAsync(
            engine,
            p2Pass.State,
            "real-ghostly-centaurs",
            "P1-REAL-GHOSTLY-CENTAUR",
            "P2-REAL-GHOSTLY-CENTAUR");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(7, final.State.CardObjects["P1-REAL-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(7, final.State.CardObjects["P2-REAL-GHOSTLY-CENTAUR"].Power);
        Assert.Equal(2, final.State.CardObjects["P1-REAL-GHOSTLY-CENTAUR"].UntilEndOfTurnPowerModifier);
        Assert.Equal(2, final.State.CardObjects["P2-REAL-GHOSTLY-CENTAUR"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task RealResonantSoulFirstFriendlyDestroyedTriggersEnterApnapOrderWindowAndDrawThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingResonantSoulFriendlyUnitsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-resonant-soul-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-resonant-soul-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)));
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Hand);

        var final = await OrderAndResolveTwoDrawTriggersThroughStackAsync(
            engine,
            p2Pass.State,
            "real-resonant-souls",
            "RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1",
            "P1-REAL-RESONANT-SOUL",
            "P2-REAL-RESONANT-SOUL",
            "P1-REAL-RESONANT-DRAW-001",
            "P2-REAL-RESONANT-DRAW-001");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(["P1-REAL-RESONANT-DRAW-001"], final.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-REAL-RESONANT-DRAW-001"], final.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task RealSavageJawfishFriendlyDestroyedTriggersEnterApnapOrderWindowAndGainExperienceThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingSavageJawfishFriendlyUnitsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-savage-jawfish-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-savage-jawfish-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)));
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal(0, p2Pass.State.PlayerExperience["P1"]);
        Assert.Equal(0, p2Pass.State.PlayerExperience["P2"]);

        var final = await OrderAndResolveTwoSavageJawfishTriggersThroughStackAsync(
            engine,
            p2Pass.State,
            "real-savage-jawfish",
            "P1-REAL-SAVAGE-JAWFISH",
            "P2-REAL-SAVAGE-JAWFISH");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Equal(1, final.State.PlayerExperience["P1"]);
        Assert.Equal(1, final.State.PlayerExperience["P2"]);
    }

    [Fact]
    public async Task RealViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingViktorNonMinionTargetState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-viktor-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-viktor-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        var triggerStackItem = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("P1-REAL-ARC-VIKTOR", triggerStackItem.SourceObjectId);
        Assert.Equal("VIKTOR_DESTROYED_NON_MINION_CREATE_MINION", triggerStackItem.EffectKind);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "VIKTOR_DESTROYED_NON_MINION_CREATE_MINION", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Contains("P1-REAL-ARC-VIKTOR", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-REAL-VIKTOR-TARGET", p2Pass.State.PlayerZones["P1"].Base);

        var final = await ResolveSingleViktorTriggerThroughStackAsync(
            engine,
            p2Pass.State,
            "real-viktor",
            "P1-REAL-ARC-VIKTOR");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Contains("P1-REAL-ARC-VIKTOR-TOKEN-001", final.State.PlayerZones["P1"].Base);
        var token = final.State.CardObjects["P1-REAL-ARC-VIKTOR-TOKEN-001"];
        Assert.Equal("OGN·273/298", token.CardNo);
        Assert.Equal(1, token.Power);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.Contains(CardObjectTags.MinionTokenFamily, token.Tags);
    }

    [Fact]
    public async Task StateBasedCleanupViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingViktorNonMinionTargetState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-viktor-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-viktor-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        var triggerStackItem = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("P1-CLEANUP-OGN-VIKTOR", triggerStackItem.SourceObjectId);
        Assert.Equal("VIKTOR_DESTROYED_NON_MINION_CREATE_MINION", triggerStackItem.EffectKind);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-CLEANUP-VIKTOR-TARGET", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));

        var final = await ResolveSingleViktorTriggerThroughStackAsync(
            engine,
            p2Pass.State,
            "cleanup-viktor",
            "P1-CLEANUP-OGN-VIKTOR");

        Assert.Empty(final.State.TriggerQueue);
        Assert.Empty(final.State.StackItems);
        Assert.Contains("P1-CLEANUP-OGN-VIKTOR-TOKEN-001", final.State.PlayerZones["P1"].Base);
        var token = final.State.CardObjects["P1-CLEANUP-OGN-VIKTOR-TOKEN-001"];
        Assert.Equal("OGN·273/298", token.CardNo);
        Assert.Equal(1, token.Power);
        Assert.Contains(CardObjectTags.MinionTokenFamily, token.Tags);
    }

    [Fact]
    public async Task ViktorDestroyedMinionTargetDoesNotEnqueueTrigger()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingViktorMinionTargetState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-viktor-minion-target-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-viktor-minion-target-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.State.PlayerZones["P1"].Base, objectId => objectId.Contains("TOKEN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StateBasedCleanupInvalidViktorSourcesDoNotEnqueueOrLeak()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingViktorTargetWithInvalidSourcesState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-invalid-viktor-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-invalid-viktor-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(p2Pass.State.PlayerZones["P1"].Base, objectId => objectId.Contains("TOKEN", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.State.PlayerZones["P2"].Base, objectId => objectId.Contains("TOKEN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StateBasedCleanupViktorSkipsWhenSourceAlsoDies()
    {
        var engine = new CoreRuleEngine();
        var state = BuildStarfallDestroyingViktorSourceAndTargetState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleanup-dying-viktor-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-cleanup-dying-viktor-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
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
    public async Task RealMechanicalTricksterLastBreathTriggersOrderAndCreateMinionsThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingTwoMechanicalTrickstersState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-mechanical-trickster-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-mechanical-trickster-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-MECHANICAL-TRICKSTER", p1Trigger.SourceObjectId);
        Assert.Equal("P2-MECHANICAL-TRICKSTER", p2Trigger.SourceObjectId);
        Assert.Equal("MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS", p1Trigger.EffectKind);
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
        Assert.Equal("P2-MECHANICAL-TRICKSTER", Assert.IsType<string>(p2TriggerView["sourceObjectId"]));
        Assert.Equal("MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS", Assert.IsType<string>(p2TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p2TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-mechanical-trickster-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
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
            new PlayerIntent("intent-real-mechanical-trickster-default-order", "P1", CommandTypes.OrderTriggers),
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
            new PlayerIntent("intent-real-mechanical-trickster-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-real-mechanical-trickster-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS", StringComparison.Ordinal));
        var p2TokenEvents = p1ResolvesP2Trigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, p2TokenEvents.Length);
        Assert.All(p2TokenEvents, gameEvent =>
        {
            Assert.Equal("P2", gameEvent.Payload["playerId"]);
            Assert.Equal("P2-MECHANICAL-TRICKSTER", gameEvent.Payload["sourceObjectId"]);
            Assert.Equal("随从", gameEvent.Payload["tokenName"]);
            Assert.Equal(1, gameEvent.Payload["power"]);
        });
        Assert.Equal(
            [
                "P2-MECHANICAL-TRICKSTER-TOKEN-001",
                "P2-MECHANICAL-TRICKSTER-TOKEN-002",
                "P2-MECHANICAL-TRICKSTER-TOKEN-003"
            ],
            p1ResolvesP2Trigger.State.PlayerZones["P2"].Base);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-real-mechanical-trickster-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-real-mechanical-trickster-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        var p1TokenEvents = p2ResolvesP1Trigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, p1TokenEvents.Length);
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Equal(
            [
                "P1-MECHANICAL-TRICKSTER-TOKEN-001",
                "P1-MECHANICAL-TRICKSTER-TOKEN-002",
                "P1-MECHANICAL-TRICKSTER-TOKEN-003"
            ],
            p2ResolvesP1Trigger.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P2-MECHANICAL-TRICKSTER-TOKEN-001",
                "P2-MECHANICAL-TRICKSTER-TOKEN-002",
                "P2-MECHANICAL-TRICKSTER-TOKEN-003"
            ],
            p2ResolvesP1Trigger.State.PlayerZones["P2"].Base);
        foreach (var tokenObjectId in p2ResolvesP1Trigger.State.PlayerZones["P1"].Base.Concat(p2ResolvesP1Trigger.State.PlayerZones["P2"].Base))
        {
            Assert.Equal(1, p2ResolvesP1Trigger.State.CardObjects[tokenObjectId].Power);
            Assert.Equal(
                [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily],
                p2ResolvesP1Trigger.State.CardObjects[tokenObjectId].Tags);
        }
    }

    [Fact]
    public async Task RealMechanicalTricksterHiddenSourcesDoNotEnqueueOrCreateMinions()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingHiddenMechanicalTrickstersState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-hidden-mechanical-trickster-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-hidden-mechanical-trickster-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task RealIroncladVanguardLastBreathTriggersOrderAndCreateRobotsThroughStack()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingTwoIroncladVanguardsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-ironclad-vanguard-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-ironclad-vanguard-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(2, p2Pass.State.TriggerQueue.Count);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);

        var p1Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P1", StringComparison.Ordinal));
        var p2Trigger = Assert.Single(p2Pass.State.TriggerQueue, trigger =>
            string.Equals(trigger.ControllerId, "P2", StringComparison.Ordinal));
        Assert.Equal("P1-IRONCLAD-VANGUARD", p1Trigger.SourceObjectId);
        Assert.Equal("P2-IRONCLAD-VANGUARD", p2Trigger.SourceObjectId);
        Assert.Equal("IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS", p1Trigger.EffectKind);
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
        Assert.Equal("P2-IRONCLAD-VANGUARD", Assert.IsType<string>(p2TriggerView["sourceObjectId"]));
        Assert.Equal("IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS", Assert.IsType<string>(p2TriggerView["effectKind"]));
        Assert.Contains("UNIT_DESTROYED", Assert.IsType<string>(p2TriggerView["visibleText"]), StringComparison.Ordinal);

        var illegalReorder = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-real-ironclad-vanguard-illegal-raw-order", "P1", CommandTypes.OrderTriggers),
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
            new PlayerIntent("intent-real-ironclad-vanguard-default-order", "P1", CommandTypes.OrderTriggers),
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
            new PlayerIntent("intent-real-ironclad-vanguard-p2-trigger-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1ResolvesP2Trigger = await engine.ResolveAsync(
            p2TriggerPass.State,
            new PlayerIntent("intent-real-ironclad-vanguard-p1-resolves-p2-trigger", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1ResolvesP2Trigger.Accepted, p1ResolvesP2Trigger.ErrorMessage);
        Assert.Contains(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS", StringComparison.Ordinal));
        var p2TokenEvents = p1ResolvesP2Trigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, p2TokenEvents.Length);
        Assert.All(p2TokenEvents, gameEvent =>
        {
            Assert.Equal("P2", gameEvent.Payload["playerId"]);
            Assert.Equal("P2-IRONCLAD-VANGUARD", gameEvent.Payload["sourceObjectId"]);
            Assert.Equal("机器人", gameEvent.Payload["tokenName"]);
            Assert.Equal(3, gameEvent.Payload["power"]);
        });
        Assert.Equal(
            [
                "P2-IRONCLAD-VANGUARD-TOKEN-001",
                "P2-IRONCLAD-VANGUARD-TOKEN-002"
            ],
            p1ResolvesP2Trigger.State.PlayerZones["P2"].Base);
        Assert.Single(p1ResolvesP2Trigger.State.StackItems);
        Assert.Equal("P1", p1ResolvesP2Trigger.State.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            p1ResolvesP2Trigger.State,
            new PlayerIntent("intent-real-ironclad-vanguard-p1-trigger-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesP1Trigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent("intent-real-ironclad-vanguard-p2-resolves-p1-trigger", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2ResolvesP1Trigger.Accepted, p2ResolvesP1Trigger.ErrorMessage);
        var p1TokenEvents = p2ResolvesP1Trigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, p1TokenEvents.Length);
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Equal(
            [
                "P1-IRONCLAD-VANGUARD-TOKEN-001",
                "P1-IRONCLAD-VANGUARD-TOKEN-002"
            ],
            p2ResolvesP1Trigger.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P2-IRONCLAD-VANGUARD-TOKEN-001",
                "P2-IRONCLAD-VANGUARD-TOKEN-002"
            ],
            p2ResolvesP1Trigger.State.PlayerZones["P2"].Base);
        foreach (var tokenObjectId in p2ResolvesP1Trigger.State.PlayerZones["P1"].Base.Concat(p2ResolvesP1Trigger.State.PlayerZones["P2"].Base))
        {
            Assert.Equal(3, p2ResolvesP1Trigger.State.CardObjects[tokenObjectId].Power);
            Assert.Equal([CardObjectTags.UnitCard, "机械"], p2ResolvesP1Trigger.State.CardObjects[tokenObjectId].Tags);
        }
    }

    [Fact]
    public async Task RealIroncladVanguardHiddenSourcesDoNotEnqueueOrCreateRobots()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireDestroyingHiddenIroncladVanguardsState();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-real-hidden-ironclad-vanguard-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-real-hidden-ironclad-vanguard-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, p2Pass.Prompts["P1"].View?.Type);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);
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

    private static void AssertWarhawkToken(MatchState state, string tokenObjectId, string playerId)
    {
        var token = state.CardObjects[tokenObjectId];
        Assert.Equal("UNL·T02", token.CardNo);
        Assert.Equal(1, token.Power);
        Assert.Equal(playerId, token.OwnerId);
        Assert.Equal(playerId, token.ControllerId);
        Assert.Contains(tokenObjectId, state.PlayerZones[playerId].Base);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.Contains(CardObjectTags.Spellshield, token.Tags);
    }

    private static void AssertNoMuddyDredgerWarhawkLeak(ResolutionResult cleanup)
    {
        Assert.Empty(cleanup.State.TriggerQueue);
        Assert.Empty(cleanup.State.StackItems);
        Assert.DoesNotContain(cleanup.Events, gameEvent =>
            gameEvent.Payload.TryGetValue("effectKind", out var effectKind)
            && string.Equals(effectKind as string, "MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent =>
            gameEvent.Payload.TryGetValue("abilityId", out var abilityId)
            && string.Equals(abilityId as string, "MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.DoesNotContain(cleanup.State.CardObjects.Values, cardObject =>
            string.Equals(cardObject.CardNo, "UNL·T02", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.OrderTriggers, cleanup.Prompts["P1"].View?.Type);
    }

    private static async Task<ResolutionResult> ResolveSingleViktorTriggerThroughStackAsync(
        CoreRuleEngine engine,
        MatchState state,
        string label,
        string sourceObjectId)
    {
        var triggerStackItem = Assert.Single(state.StackItems);
        Assert.Equal(sourceObjectId, triggerStackItem.SourceObjectId);
        Assert.Equal("VIKTOR_DESTROYED_NON_MINION_CREATE_MINION", triggerStackItem.EffectKind);
        Assert.Equal("P1", state.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-{label}-viktor-trigger-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesTrigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent($"intent-{label}-viktor-trigger-p2-resolves", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1TriggerPass.Accepted, p1TriggerPass.ErrorMessage);
        Assert.True(p2ResolvesTrigger.Accepted, p2ResolvesTrigger.ErrorMessage);
        Assert.Contains(p2ResolvesTrigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "VIKTOR_DESTROYED_NON_MINION_CREATE_MINION", StringComparison.Ordinal));
        var tokenEvent = Assert.Single(p2ResolvesTrigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("P1", tokenEvent.Payload["playerId"]);
        Assert.Equal(sourceObjectId, tokenEvent.Payload["sourceObjectId"]);
        Assert.Equal("OGN·273/298", tokenEvent.Payload["tokenCardNo"]);
        Assert.Equal("随从", tokenEvent.Payload["tokenName"]);
        Assert.Equal(1, tokenEvent.Payload["power"]);
        Assert.Equal("VIKTOR_DESTROYED_NON_MINION_CREATE_MINION", tokenEvent.Payload["reason"]);
        Assert.Contains(
            CardObjectTags.MinionTokenFamily,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(tokenEvent.Payload["tokenTags"]));

        return p2ResolvesTrigger;
    }

    private static async Task<ResolutionResult> ResolveSingleKogmawTriggerThroughStackAsync(
        CoreRuleEngine engine,
        MatchState state,
        string label,
        string expectedDestroyedVictimObjectId)
    {
        var triggerStackItem = Assert.Single(state.StackItems);
        Assert.Equal("P1-KOGMAW", triggerStackItem.SourceObjectId);
        Assert.Equal("OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT", triggerStackItem.EffectKind);
        Assert.Equal("P1", state.PriorityPlayerId);

        var p1TriggerPass = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-{label}-kogmaw-trigger-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2ResolvesTrigger = await engine.ResolveAsync(
            p1TriggerPass.State,
            new PlayerIntent($"intent-{label}-kogmaw-trigger-p2-resolves", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1TriggerPass.Accepted, p1TriggerPass.ErrorMessage);
        Assert.True(p2ResolvesTrigger.Accepted, p2ResolvesTrigger.ErrorMessage);
        Assert.Contains(p2ResolvesTrigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT", StringComparison.Ordinal));
        var kogmawDamageEvents = p2ResolvesTrigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-KOGMAW", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, kogmawDamageEvents.Length);
        Assert.Equal(
            ["P1-KOGMAW-SAME-BATTLEFIELD-ALLY", "P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"],
            kogmawDamageEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["targetObjectId"])).OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray());
        Assert.All(kogmawDamageEvents, gameEvent =>
        {
            Assert.Equal(4, gameEvent.Payload["damage"]);
            Assert.Equal("P1-BATTLEFIELD-KOGMAW", gameEvent.Payload["battlefieldObjectId"]);
        });
        Assert.Contains(p2ResolvesTrigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, expectedDestroyedVictimObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal));

        return p2ResolvesTrigger;
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

    private static async Task<ResolutionResult> OrderAndResolveTwoUnitTokenTriggersThroughStackAsync(
        CoreRuleEngine engine,
        MatchState state,
        string label,
        string effectKind,
        string p1SourceObjectId,
        string p2SourceObjectId,
        string tokenName,
        int tokenPower,
        IReadOnlyList<string> expectedTags,
        IReadOnlyList<string> p1TokenObjectIds,
        IReadOnlyList<string> p2TokenObjectIds,
        string expectedTokenCardNo = "",
        IReadOnlyList<string>? expectedEventTags = null)
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
        Assert.Empty(illegalReorder.State.PlayerZones["P1"].Base);
        Assert.Empty(illegalReorder.State.PlayerZones["P2"].Base);
        Assert.All(p1TokenObjectIds.Concat(p2TokenObjectIds), tokenObjectId =>
            Assert.DoesNotContain(tokenObjectId, illegalReorder.State.CardObjects.Keys));

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
        var p2TokenEvents = p1ResolvesP2Trigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(p2TokenObjectIds.Count, p2TokenEvents.Length);
        Assert.All(p2TokenEvents, gameEvent =>
        {
            Assert.Equal("P2", gameEvent.Payload["playerId"]);
            Assert.Equal(p2SourceObjectId, gameEvent.Payload["sourceObjectId"]);
            Assert.Equal(tokenName, gameEvent.Payload["tokenName"]);
            Assert.Equal(tokenPower, gameEvent.Payload["power"]);
            if (!string.IsNullOrWhiteSpace(expectedTokenCardNo))
            {
                Assert.Equal(expectedTokenCardNo, gameEvent.Payload["tokenCardNo"]);
            }

            if (expectedEventTags is not null)
            {
                Assert.Equal(expectedEventTags, Assert.IsAssignableFrom<IReadOnlyList<string>>(gameEvent.Payload["tokenTags"]));
            }
        });
        Assert.Equal(p2TokenObjectIds, p1ResolvesP2Trigger.State.PlayerZones["P2"].Base);
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
        var p1TokenEvents = p2ResolvesP1Trigger.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(p1TokenObjectIds.Count, p1TokenEvents.Length);
        Assert.All(p1TokenEvents, gameEvent =>
        {
            Assert.Equal("P1", gameEvent.Payload["playerId"]);
            Assert.Equal(p1SourceObjectId, gameEvent.Payload["sourceObjectId"]);
            Assert.Equal(tokenName, gameEvent.Payload["tokenName"]);
            Assert.Equal(tokenPower, gameEvent.Payload["power"]);
            if (!string.IsNullOrWhiteSpace(expectedTokenCardNo))
            {
                Assert.Equal(expectedTokenCardNo, gameEvent.Payload["tokenCardNo"]);
            }

            if (expectedEventTags is not null)
            {
                Assert.Equal(expectedEventTags, Assert.IsAssignableFrom<IReadOnlyList<string>>(gameEvent.Payload["tokenTags"]));
            }
        });
        Assert.Empty(p2ResolvesP1Trigger.State.TriggerQueue);
        Assert.Empty(p2ResolvesP1Trigger.State.StackItems);
        Assert.Equal(p1TokenObjectIds, p2ResolvesP1Trigger.State.PlayerZones["P1"].Base);
        Assert.Equal(p2TokenObjectIds, p2ResolvesP1Trigger.State.PlayerZones["P2"].Base);
        foreach (var tokenObjectId in p2ResolvesP1Trigger.State.PlayerZones["P1"].Base.Concat(p2ResolvesP1Trigger.State.PlayerZones["P2"].Base))
        {
            Assert.Equal(tokenPower, p2ResolvesP1Trigger.State.CardObjects[tokenObjectId].Power);
            Assert.Equal(expectedTags, p2ResolvesP1Trigger.State.CardObjects[tokenObjectId].Tags);
        }

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

    private static async Task<ResolutionResult> OrderAndResolveTwoSavageJawfishTriggersThroughStackAsync(
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
        Assert.Equal("SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1", p1Trigger.EffectKind);
        Assert.Equal("SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1", p2Trigger.EffectKind);

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
        Assert.Equal("SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1", Assert.IsType<string>(p1TriggerView["effectKind"]));
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
        Assert.Equal(0, illegalReorder.State.PlayerExperience["P1"]);
        Assert.Equal(0, illegalReorder.State.PlayerExperience["P2"]);
        Assert.DoesNotContain(illegalReorder.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));

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
        Assert.Equal(0, ordered.State.PlayerExperience["P1"]);
        Assert.Equal(0, ordered.State.PlayerExperience["P2"]);

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
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1", StringComparison.Ordinal));
        var p2ExperienceEvent = Assert.Single(p1ResolvesP2Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P2", p2ExperienceEvent.Payload["playerId"]);
        Assert.Equal(p2SourceObjectId, p2ExperienceEvent.Payload["sourceObjectId"]);
        Assert.Equal("UNL-129/219", p2ExperienceEvent.Payload["cardNo"]);
        Assert.Equal(1, p2ExperienceEvent.Payload["amount"]);
        Assert.Equal(1, p1ResolvesP2Trigger.State.PlayerExperience["P2"]);
        Assert.Equal(0, p1ResolvesP2Trigger.State.PlayerExperience["P1"]);
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
        var p1ExperienceEvent = Assert.Single(p2ResolvesP1Trigger.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P1", p1ExperienceEvent.Payload["playerId"]);
        Assert.Equal(p1SourceObjectId, p1ExperienceEvent.Payload["sourceObjectId"]);
        Assert.Equal(1, p1ExperienceEvent.Payload["amount"]);

        return p2ResolvesP1Trigger;
    }

    private static MatchState BuildLuxLowCostSpellState()
    {
        return BuildLuxHighCostSpellState(
            spellObjectId: "P1-SPELL-MIGHT-MAKES-RIGHT",
            spellCardNo: "SFD·106/221",
            spellMana: 2);
    }

    private static MatchState BuildLuxHighCostSpellState(
        string spellPlayerId = "P1",
        string activePlayerId = "P1",
        string turnPlayerId = "P1",
        string spellObjectId = "P1-SPELL-EVOLUTION-DAY",
        string spellCardNo = "OGN·114/298",
        int spellMana = 6,
        bool luxIsFaceDown = false,
        bool luxIsStandby = false,
        bool luxOnField = true)
    {
        var p1Base = luxOnField ? new[] { "P1-LUX" } : [];
        var p1Hand = string.Equals(spellPlayerId, "P1", StringComparison.Ordinal)
            ? new[] { spellObjectId }
            : [];
        var p2Hand = string.Equals(spellPlayerId, "P2", StringComparison.Ordinal)
            ? new[] { spellObjectId }
            : [];
        var luxTags = luxIsStandby
            ? new[] { CardObjectTags.UnitCard, CardObjectTags.Standby }
            : [CardObjectTags.UnitCard];

        return new MatchState(
            "real-lux-high-cost-spell-room",
            73,
            1,
            activePlayerId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            turnPlayerId: turnPlayerId,
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = string.Equals(spellPlayerId, "P1", StringComparison.Ordinal)
                    ? new RunePool(spellMana, 0)
                    : RunePool.Empty,
                ["P2"] = string.Equals(spellPlayerId, "P2", StringComparison.Ordinal)
                    ? new RunePool(spellMana, 0)
                    : RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = p1Hand,
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = p2Hand
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LUX"] = new(
                    "P1-LUX",
                    isFaceDown: luxIsFaceDown,
                    power: 5,
                    tags: luxTags,
                    manaCost: 6,
                    cardNo: "OGS·006/024",
                    ownerId: "P1",
                    controllerId: "P1"),
                [spellObjectId] = new(
                    spellObjectId,
                    cardNo: spellCardNo,
                    ownerId: spellPlayerId,
                    controllerId: spellPlayerId)
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static void AssertLuxDidNotTrigger(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Payload.TryGetValue("effectKind", out var effectKind) ? effectKind as string : null, OgsLuxHighCostSpellEffectKind, StringComparison.Ordinal)
            || string.Equals(gameEvent.Payload.TryGetValue("trigger", out var trigger) ? trigger as string : null, OgsLuxHighCostSpellEffectKind, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId) ? sourceObjectId as string : null, "P1-LUX", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("targetObjectId", out var targetObjectId) ? targetObjectId as string : null, "P1-LUX", StringComparison.Ordinal));
    }

    private static void AssertLuxPowerUnchanged(ResolutionResult result)
    {
        var lux = result.State.CardObjects["P1-LUX"];
        Assert.Equal(5, lux.Power);
        Assert.Equal(0, lux.UntilEndOfTurnPowerModifier);
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

    private static MatchState BuildSpiritFireDestroyingGhostlyCentaurFriendlyUnitsState()
    {
        return new MatchState(
            "real-ghostly-centaur-trigger-room",
            47,
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
                    Base = ["P1-REAL-GHOSTLY-CENTAUR", "P1-REAL-GHOSTLY-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-REAL-GHOSTLY-CENTAUR", "P2-REAL-GHOSTLY-TARGET"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-REAL-GHOSTLY-CENTAUR"] = new(
                    "P1-REAL-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-REAL-GHOSTLY-CENTAUR"] = new(
                    "P2-REAL-GHOSTLY-CENTAUR",
                    cardNo: "UNL-068/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-REAL-GHOSTLY-TARGET"] = new(
                    "P1-REAL-GHOSTLY-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-REAL-GHOSTLY-TARGET"] = new(
                    "P2-REAL-GHOSTLY-TARGET",
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
                    "STACK-SPIRIT-FIRE-GHOSTLY-CENTAURS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-REAL-GHOSTLY-TARGET", "P2-REAL-GHOSTLY-TARGET"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingResonantSoulFriendlyUnitsState()
    {
        return new MatchState(
            "real-resonant-soul-trigger-room",
            48,
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
                    MainDeck = ["P1-REAL-RESONANT-DRAW-001"],
                    Base = ["P1-REAL-RESONANT-SOUL", "P1-REAL-RESONANT-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-REAL-RESONANT-DRAW-001"],
                    Base = ["P2-REAL-RESONANT-SOUL", "P2-REAL-RESONANT-TARGET"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-REAL-RESONANT-SOUL"] = new(
                    "P1-REAL-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-REAL-RESONANT-SOUL"] = new(
                    "P2-REAL-RESONANT-SOUL",
                    cardNo: "OGN·118/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-REAL-RESONANT-TARGET"] = new(
                    "P1-REAL-RESONANT-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-REAL-RESONANT-TARGET"] = new(
                    "P2-REAL-RESONANT-TARGET",
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
                    "STACK-SPIRIT-FIRE-RESONANT-SOULS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-REAL-RESONANT-TARGET", "P2-REAL-RESONANT-TARGET"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingSavageJawfishFriendlyUnitsState()
    {
        return new MatchState(
            "real-savage-jawfish-trigger-room",
            49,
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
                    Base = ["P1-REAL-SAVAGE-JAWFISH", "P1-REAL-SAVAGE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-REAL-SAVAGE-JAWFISH", "P2-REAL-SAVAGE-TARGET"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-REAL-SAVAGE-JAWFISH"] = new(
                    "P1-REAL-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-REAL-SAVAGE-JAWFISH"] = new(
                    "P2-REAL-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-REAL-SAVAGE-TARGET"] = new(
                    "P1-REAL-SAVAGE-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-REAL-SAVAGE-TARGET"] = new(
                    "P2-REAL-SAVAGE-TARGET",
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
                    "STACK-SPIRIT-FIRE-SAVAGE-JAWFISH",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-REAL-SAVAGE-TARGET", "P2-REAL-SAVAGE-TARGET"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingViktorNonMinionTargetState()
    {
        return BuildSpiritFireDestroyingViktorTargetState(
            "real-viktor-destroyed-non-minion-room",
            "P1-REAL-ARC-VIKTOR",
            "ARC-006/006",
            "P1-REAL-VIKTOR-TARGET",
            null,
            [CardObjectTags.UnitCard]);
    }

    private static MatchState BuildSpiritFireDestroyingViktorMinionTargetState()
    {
        return BuildSpiritFireDestroyingViktorTargetState(
            "real-viktor-destroyed-minion-room",
            "P1-REAL-OGN-VIKTOR",
            "OGN·246/298",
            "P1-REAL-VIKTOR-MINION-TARGET",
            "OGN·273/298",
            [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily]);
    }

    private static MatchState BuildSpiritFireDestroyingViktorTargetState(
        string roomId,
        string viktorObjectId,
        string viktorCardNo,
        string targetObjectId,
        string? targetCardNo,
        IReadOnlyList<string> targetTags)
    {
        return new MatchState(
            roomId,
            53,
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
                    Base = [viktorObjectId, targetObjectId]
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
                [viktorObjectId] = new(
                    viktorObjectId,
                    cardNo: viktorCardNo,
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [targetObjectId] = new(
                    targetObjectId,
                    cardNo: targetCardNo,
                    power: 2,
                    tags: targetTags,
                    ownerId: "P1",
                    controllerId: "P1"),
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
                    $"STACK-SPIRIT-FIRE-{viktorObjectId}",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    [targetObjectId])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingKogmawOnBattlefieldState()
    {
        return BuildKogmawBattlefieldState(
            "real-kogmaw-aoe-room",
            "STACK-SPIRIT-FIRE-KOGMAW",
            "P1-SPELL-SPIRIT-FIRE",
            "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
            "OGN·256/298",
            ["P1-KOGMAW"],
            kogmawIsFaceDown: false,
            kogmawTags: [CardObjectTags.UnitCard],
            kogmawPower: 1,
            includeKogmawBattlefieldLocation: true);
    }

    private static MatchState BuildStarfallDestroyingKogmawOnBattlefieldState()
    {
        return BuildKogmawBattlefieldState(
            "cleanup-kogmaw-aoe-room",
            "STACK-STARFALL-KOGMAW",
            "P1-SPELL-STARFALL",
            "STARFALL_DAMAGE_3_TWICE",
            "OGN·029/298",
            ["P1-KOGMAW", "P1-KOGMAW-OTHER-BATTLEFIELD-ALLY"],
            kogmawIsFaceDown: false,
            kogmawTags: [CardObjectTags.UnitCard],
            kogmawPower: 1,
            includeKogmawBattlefieldLocation: true);
    }

    private static MatchState BuildStarfallDestroyingHiddenKogmawsOnBattlefieldState()
    {
        return new MatchState(
            "cleanup-hidden-kogmaw-aoe-room",
            71,
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
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-KOGMAW",
                        "P1-HIDDEN-KOGMAW",
                        "P1-KOGMAW-HIDDEN-BYSTANDER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-STANDBY-KOGMAW"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KOGMAW"] = new(
                    "P1-BATTLEFIELD-KOGMAW",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-HIDDEN-KOGMAW"] = new(
                    "P1-HIDDEN-KOGMAW",
                    cardNo: "OGN·190/298",
                    power: 1,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-STANDBY-KOGMAW"] = new(
                    "P2-STANDBY-KOGMAW",
                    cardNo: "OGN·190/298",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-KOGMAW-HIDDEN-BYSTANDER"] = new(
                    "P1-KOGMAW-HIDDEN-BYSTANDER",
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
                    "STACK-STARFALL-HIDDEN-KOGMAW",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-HIDDEN-KOGMAW", "P2-STANDBY-KOGMAW"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KOGMAW"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW"),
                ["P1-HIDDEN-KOGMAW"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW"),
                ["P2-STANDBY-KOGMAW"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW"),
                ["P1-KOGMAW-HIDDEN-BYSTANDER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW")
            });
    }

    private static MatchState BuildSpiritFireDestroyingKogmawWithoutBattlefieldLocationState()
    {
        return BuildKogmawBattlefieldState(
            "real-kogmaw-no-battlefield-location-room",
            "STACK-SPIRIT-FIRE-KOGMAW-NO-LOCATION",
            "P1-SPELL-SPIRIT-FIRE",
            "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
            "OGN·256/298",
            ["P1-KOGMAW"],
            kogmawIsFaceDown: false,
            kogmawTags: [CardObjectTags.UnitCard],
            kogmawPower: 1,
            includeKogmawBattlefieldLocation: false);
    }

    private static MatchState BuildKogmawBattlefieldState(
        string roomId,
        string stackItemId,
        string spellObjectId,
        string effectKind,
        string cardNo,
        IReadOnlyList<string> targetObjectIds,
        bool kogmawIsFaceDown,
        IReadOnlyList<string> kogmawTags,
        int kogmawPower,
        bool includeKogmawBattlefieldLocation)
    {
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-KOGMAW"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW"),
            ["P1-KOGMAW-SAME-BATTLEFIELD-ALLY"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW"),
            ["P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW"),
            ["P1-BATTLEFIELD-OTHER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER"),
            ["P1-KOGMAW-OTHER-BATTLEFIELD-ALLY"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER")
        };
        if (includeKogmawBattlefieldLocation)
        {
            objectLocations["P1-KOGMAW"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KOGMAW");
        }

        return new MatchState(
            roomId,
            71,
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
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-KOGMAW",
                        "P1-KOGMAW",
                        "P1-KOGMAW-SAME-BATTLEFIELD-ALLY",
                        "P1-BATTLEFIELD-OTHER",
                        "P1-KOGMAW-OTHER-BATTLEFIELD-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KOGMAW"] = new(
                    "P1-BATTLEFIELD-KOGMAW",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-OTHER"] = new(
                    "P1-BATTLEFIELD-OTHER",
                    cardNo: "OGN·276/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-KOGMAW"] = new(
                    "P1-KOGMAW",
                    cardNo: "OGN·190/298",
                    power: kogmawPower,
                    isFaceDown: kogmawIsFaceDown,
                    tags: kogmawTags,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-KOGMAW-SAME-BATTLEFIELD-ALLY"] = new(
                    "P1-KOGMAW-SAME-BATTLEFIELD-ALLY",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-KOGMAW-SAME-BATTLEFIELD-VICTIM"] = new(
                    "P2-KOGMAW-SAME-BATTLEFIELD-VICTIM",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-KOGMAW-OTHER-BATTLEFIELD-ALLY"] = new(
                    "P1-KOGMAW-OTHER-BATTLEFIELD-ALLY",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [spellObjectId] = new(
                    spellObjectId,
                    cardNo: cardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    stackItemId,
                    "P1",
                    spellObjectId,
                    effectKind,
                    cardNo,
                    targetObjectIds)
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            objectLocations: objectLocations);
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

    private static MatchState BuildStarfallDestroyingTwoMechanicalTrickstersState()
    {
        return BuildStarfallDestroyingTokenLastBreathUnitsState(
            "cleanup-mechanical-trickster-trigger-room",
            "P1-CLEANUP-MECHANICAL-TRICKSTER",
            "P2-CLEANUP-MECHANICAL-TRICKSTER",
            cardNo: "OGN·239/298",
            power: 3,
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard]);
    }

    private static MatchState BuildStarfallDestroyingHiddenMechanicalTrickstersState()
    {
        return BuildStarfallDestroyingTokenLastBreathUnitsState(
            "cleanup-hidden-mechanical-trickster-trigger-room",
            "P1-CLEANUP-HIDDEN-MECHANICAL-TRICKSTER",
            "P2-CLEANUP-STANDBY-MECHANICAL-TRICKSTER",
            cardNo: "OGN·239/298",
            power: 3,
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]);
    }

    private static MatchState BuildStarfallDestroyingTwoIroncladVanguardsState()
    {
        return BuildStarfallDestroyingTokenLastBreathUnitsState(
            "cleanup-ironclad-vanguard-trigger-room",
            "P1-CLEANUP-IRONCLAD-VANGUARD",
            "P2-CLEANUP-IRONCLAD-VANGUARD",
            cardNo: "SFD·021/221",
            power: 3,
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
            p2Tags: [CardObjectTags.UnitCard, "机械", "约德尔人"]);
    }

    private static MatchState BuildStarfallDestroyingHiddenIroncladVanguardsState()
    {
        return BuildStarfallDestroyingTokenLastBreathUnitsState(
            "cleanup-hidden-ironclad-vanguard-trigger-room",
            "P1-CLEANUP-HIDDEN-IRONCLAD-VANGUARD",
            "P2-CLEANUP-STANDBY-IRONCLAD-VANGUARD",
            cardNo: "SFD·021/221",
            power: 3,
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
            p2Tags: [CardObjectTags.UnitCard, "机械", "约德尔人", CardObjectTags.Standby]);
    }

    private static MatchState BuildStarfallDestroyingTwoMuddyDredgersState()
    {
        return BuildStarfallDestroyingTokenLastBreathUnitsState(
            "cleanup-muddy-dredger-trigger-room",
            "P1-CLEANUP-MUDDY-DREDGER",
            "P2-CLEANUP-MUDDY-DREDGER",
            cardNo: "UNL-153/219",
            power: 1,
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard]);
    }

    private static MatchState BuildStarfallDestroyingHiddenMuddyDredgersState()
    {
        return BuildStarfallDestroyingTokenLastBreathUnitsState(
            "cleanup-hidden-muddy-dredger-trigger-room",
            "P1-CLEANUP-HIDDEN-MUDDY-DREDGER",
            "P2-CLEANUP-STANDBY-MUDDY-DREDGER",
            cardNo: "UNL-153/219",
            power: 1,
            p1IsFaceDown: true,
            p2IsFaceDown: false,
            p1Tags: [CardObjectTags.UnitCard],
            p2Tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]);
    }

    private static MatchState BuildStarfallDestroyingInvalidMuddyDredgerSourcesState()
    {
        return BuildStarfallDestroyingTokenLastBreathUnitsState(
            "cleanup-invalid-muddy-dredger-trigger-room",
            "P1-CLEANUP-INVALID-MUDDY-DREDGER",
            "P2-CLEANUP-INVALID-MUDDY-DREDGER",
            cardNo: "UNL-153/219",
            power: 1,
            p1IsFaceDown: false,
            p2IsFaceDown: false,
            p1Tags: [],
            p2Tags: []);
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

    private static MatchState BuildStarfallDestroyingSavageJawfishFriendlyUnitsState()
    {
        return new MatchState(
            "cleanup-savage-jawfish-trigger-room",
            50,
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
                    Base = ["P1-CLEANUP-SAVAGE-JAWFISH", "P1-CLEANUP-SAVAGE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-CLEANUP-SAVAGE-JAWFISH", "P2-CLEANUP-SAVAGE-TARGET"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-SAVAGE-JAWFISH"] = new(
                    "P1-CLEANUP-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-SAVAGE-JAWFISH"] = new(
                    "P2-CLEANUP-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-SAVAGE-TARGET"] = new(
                    "P1-CLEANUP-SAVAGE-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-SAVAGE-TARGET"] = new(
                    "P2-CLEANUP-SAVAGE-TARGET",
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
                    "STACK-STARFALL-CLEANUP-SAVAGE-JAWFISH",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-SAVAGE-TARGET", "P2-CLEANUP-SAVAGE-TARGET"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingFriendlyUnitsWithInvalidSavageJawfishSourcesState()
    {
        return new MatchState(
            "cleanup-hidden-savage-jawfish-trigger-room",
            51,
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
                        "P1-HIDDEN-SAVAGE-JAWFISH",
                        "P1-STANDBY-SAVAGE-JAWFISH",
                        "P1-CLEANUP-SAVAGE-HIDDEN-TARGET-1",
                        "P1-CLEANUP-SAVAGE-HIDDEN-TARGET-2"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-OPPONENT-SAVAGE-JAWFISH"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HIDDEN-SAVAGE-JAWFISH"] = new(
                    "P1-HIDDEN-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STANDBY-SAVAGE-JAWFISH"] = new(
                    "P1-STANDBY-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-OPPONENT-SAVAGE-JAWFISH"] = new(
                    "P2-OPPONENT-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-SAVAGE-HIDDEN-TARGET-1"] = new(
                    "P1-CLEANUP-SAVAGE-HIDDEN-TARGET-1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CLEANUP-SAVAGE-HIDDEN-TARGET-2"] = new(
                    "P1-CLEANUP-SAVAGE-HIDDEN-TARGET-2",
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
                    "STACK-STARFALL-CLEANUP-HIDDEN-SAVAGE-JAWFISH",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-SAVAGE-HIDDEN-TARGET-1", "P1-CLEANUP-SAVAGE-HIDDEN-TARGET-2"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingSavageJawfishAndFriendlyUnitState()
    {
        return new MatchState(
            "cleanup-savage-jawfish-source-also-dies-room",
            52,
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
                    Base = ["P1-DYING-SAVAGE-JAWFISH", "P1-DYING-SAVAGE-FRIEND"]
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
                ["P1-DYING-SAVAGE-JAWFISH"] = new(
                    "P1-DYING-SAVAGE-JAWFISH",
                    cardNo: "UNL-129/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DYING-SAVAGE-FRIEND"] = new(
                    "P1-DYING-SAVAGE-FRIEND",
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
                    "STACK-STARFALL-CLEANUP-DYING-SAVAGE-JAWFISH",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-DYING-SAVAGE-JAWFISH", "P1-DYING-SAVAGE-FRIEND"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingViktorNonMinionTargetState()
    {
        return new MatchState(
            "cleanup-viktor-destroyed-non-minion-room",
            54,
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
                    Base = ["P1-CLEANUP-OGN-VIKTOR", "P1-CLEANUP-VIKTOR-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-CLEANUP-VIKTOR-DUMMY"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLEANUP-OGN-VIKTOR"] = new(
                    "P1-CLEANUP-OGN-VIKTOR",
                    cardNo: "OGN·246/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CLEANUP-VIKTOR-TARGET"] = new(
                    "P1-CLEANUP-VIKTOR-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CLEANUP-VIKTOR-DUMMY"] = new(
                    "P2-CLEANUP-VIKTOR-DUMMY",
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
                    "STACK-STARFALL-CLEANUP-VIKTOR",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-VIKTOR-TARGET", "P2-CLEANUP-VIKTOR-DUMMY"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingViktorTargetWithInvalidSourcesState()
    {
        return new MatchState(
            "cleanup-invalid-viktor-sources-room",
            55,
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
                        "P1-HIDDEN-VIKTOR",
                        "P1-STANDBY-VIKTOR",
                        "P1-CLEANUP-VIKTOR-HIDDEN-TARGET-1",
                        "P1-CLEANUP-VIKTOR-HIDDEN-TARGET-2"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-OPPONENT-VIKTOR"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HIDDEN-VIKTOR"] = new(
                    "P1-HIDDEN-VIKTOR",
                    cardNo: "ARC-006/006",
                    power: 4,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STANDBY-VIKTOR"] = new(
                    "P1-STANDBY-VIKTOR",
                    cardNo: "OGN·246/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-OPPONENT-VIKTOR"] = new(
                    "P2-OPPONENT-VIKTOR",
                    cardNo: "OGN·246a/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-CLEANUP-VIKTOR-HIDDEN-TARGET-1"] = new(
                    "P1-CLEANUP-VIKTOR-HIDDEN-TARGET-1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CLEANUP-VIKTOR-HIDDEN-TARGET-2"] = new(
                    "P1-CLEANUP-VIKTOR-HIDDEN-TARGET-2",
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
                    "STACK-STARFALL-CLEANUP-INVALID-VIKTOR",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-CLEANUP-VIKTOR-HIDDEN-TARGET-1", "P1-CLEANUP-VIKTOR-HIDDEN-TARGET-2"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildStarfallDestroyingViktorSourceAndTargetState()
    {
        return new MatchState(
            "cleanup-viktor-source-also-dies-room",
            56,
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
                    Base = ["P1-DYING-VIKTOR", "P1-DYING-VIKTOR-FRIEND"]
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
                ["P1-DYING-VIKTOR"] = new(
                    "P1-DYING-VIKTOR",
                    cardNo: "OGN·246a/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DYING-VIKTOR-FRIEND"] = new(
                    "P1-DYING-VIKTOR-FRIEND",
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
                    "STACK-STARFALL-CLEANUP-DYING-VIKTOR",
                    "P1",
                    "P1-SPELL-STARFALL",
                    "STARFALL_DAMAGE_3_TWICE",
                    "OGN·029/298",
                    ["P1-DYING-VIKTOR", "P1-DYING-VIKTOR-FRIEND"])
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

    private static MatchState BuildStarfallDestroyingTokenLastBreathUnitsState(
        string roomId,
        string p1ObjectId,
        string p2ObjectId,
        string cardNo,
        int power,
        bool p1IsFaceDown,
        bool p2IsFaceDown,
        IReadOnlyList<string> p1Tags,
        IReadOnlyList<string> p2Tags)
    {
        return new MatchState(
            roomId,
            61,
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
                    Base = [p1ObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [p2ObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [p1ObjectId] = new(
                    p1ObjectId,
                    cardNo: cardNo,
                    power: power,
                    isFaceDown: p1IsFaceDown,
                    tags: p1Tags,
                    ownerId: "P1",
                    controllerId: "P1"),
                [p2ObjectId] = new(
                    p2ObjectId,
                    cardNo: cardNo,
                    power: power,
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
                    [p1ObjectId, p2ObjectId])
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

    private static MatchState BuildSpiritFireDestroyingTwoMechanicalTrickstersState()
    {
        return new MatchState(
            "real-mechanical-trickster-trigger-room",
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
                    Base = ["P1-MECHANICAL-TRICKSTER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-MECHANICAL-TRICKSTER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MECHANICAL-TRICKSTER"] = new(
                    "P1-MECHANICAL-TRICKSTER",
                    cardNo: "OGN·239/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-MECHANICAL-TRICKSTER"] = new(
                    "P2-MECHANICAL-TRICKSTER",
                    cardNo: "OGN·239/298",
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
                    "STACK-SPIRIT-FIRE-MECHANICAL-TRICKSTERS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-MECHANICAL-TRICKSTER", "P2-MECHANICAL-TRICKSTER"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingHiddenMechanicalTrickstersState()
    {
        return new MatchState(
            "real-hidden-mechanical-trickster-trigger-room",
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
                    Base = ["P1-HIDDEN-MECHANICAL-TRICKSTER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-STANDBY-MECHANICAL-TRICKSTER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HIDDEN-MECHANICAL-TRICKSTER"] = new(
                    "P1-HIDDEN-MECHANICAL-TRICKSTER",
                    cardNo: "OGN·239/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-STANDBY-MECHANICAL-TRICKSTER"] = new(
                    "P2-STANDBY-MECHANICAL-TRICKSTER",
                    cardNo: "OGN·239/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
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
                    "STACK-SPIRIT-FIRE-HIDDEN-MECHANICAL-TRICKSTERS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-HIDDEN-MECHANICAL-TRICKSTER", "P2-STANDBY-MECHANICAL-TRICKSTER"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingTwoIroncladVanguardsState()
    {
        return new MatchState(
            "real-ironclad-vanguard-trigger-room",
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
                    Base = ["P1-IRONCLAD-VANGUARD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-IRONCLAD-VANGUARD"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-IRONCLAD-VANGUARD"] = new(
                    "P1-IRONCLAD-VANGUARD",
                    cardNo: "SFD·021/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-IRONCLAD-VANGUARD"] = new(
                    "P2-IRONCLAD-VANGUARD",
                    cardNo: "SFD·021/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
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
                    "STACK-SPIRIT-FIRE-IRONCLAD-VANGUARDS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-IRONCLAD-VANGUARD", "P2-IRONCLAD-VANGUARD"])
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildSpiritFireDestroyingHiddenIroncladVanguardsState()
    {
        return new MatchState(
            "real-hidden-ironclad-vanguard-trigger-room",
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
                    Base = ["P1-HIDDEN-IRONCLAD-VANGUARD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-STANDBY-IRONCLAD-VANGUARD"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HIDDEN-IRONCLAD-VANGUARD"] = new(
                    "P1-HIDDEN-IRONCLAD-VANGUARD",
                    cardNo: "SFD·021/221",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-STANDBY-IRONCLAD-VANGUARD"] = new(
                    "P2-STANDBY-IRONCLAD-VANGUARD",
                    cardNo: "SFD·021/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人", CardObjectTags.Standby],
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
                    "STACK-SPIRIT-FIRE-HIDDEN-IRONCLAD-VANGUARDS",
                    "P1",
                    "P1-SPELL-SPIRIT-FIRE",
                    "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
                    "OGN·256/298",
                    ["P1-HIDDEN-IRONCLAD-VANGUARD", "P2-STANDBY-IRONCLAD-VANGUARD"])
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
