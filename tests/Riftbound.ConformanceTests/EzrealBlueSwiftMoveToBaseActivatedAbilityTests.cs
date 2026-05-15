using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EzrealBlueSwiftMoveToBaseActivatedAbilityTests
{
    private const string EzrealObjectId = "P1-EZREAL-SWIFT";
    private const string BattlefieldObjectId = "BF-EZREAL-SWIFT";
    private const string BlueRuneObjectId = "P1-RUNE-BLUE";
    private const string GreenRuneObjectId = "P1-RUNE-GREEN";

    [Fact]
    public void CatalogExposesEzrealBlueSwiftMoveForAllCollectorNumbers()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityEffectKind, ability.EffectKind);
        Assert.Equal(0, ability.ManaCost);
        Assert.Equal(0, ability.PowerCost);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.True(ability.RequiresBattlefieldSource);
        Assert.False(ability.ExhaustsSourceAsCost);
        Assert.False(ability.ReactionSpeed);
        var powerCostByTrait = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);
        Assert.Equal(P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveBluePowerCost, powerCostByTrait[RuneTrait.Blue]);
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo));
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, P4ActivatedAbilityCatalog.EzrealBlueSwiftAltCardNo));
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, P4ActivatedAbilityCatalog.EzrealBlueSwiftPromoCardNo));
    }

    [Fact]
    public void PromptExposesEzrealSwiftMoveRequirementWithBlueCostNoTargetsAndRecycleChoice()
    {
        var state = BuildEzrealSwiftState(
            P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
            RunePool.Empty,
            baseObjectIds: [BlueRuneObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue)
            }) with
        {
            TemporaryPaymentResources = [GenericTemporaryResource("MALZAHAR:TEMP-EZREAL")]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([EzrealObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId, StringComparison.Ordinal));

        Assert.Equal(EzrealObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["manaCost"]);
        Assert.Equal(0, requirement["powerCost"]);
        var powerCostByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["powerCostByTrait"]);
        Assert.Equal(1, powerCostByTrait[RuneTrait.Blue]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.Equal("self", requirement["targetScope"]);
        Assert.Equal("BASE", requirement["destinationZone"]);
        Assert.Equal("move-source-to-controller-base", requirement["movePolicy"]);
        Assert.Equal("ordinary-stack-item-before-move-to-base", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-typed-blue", requirement["paymentPolicy"]);
        Assert.True(Assert.IsType<bool>(requirement["swift"]));
        Assert.Equal("full-swift-reaction-timing-deferred", requirement["swiftTimingPolicy"]);
        Assert.Equal("deferred", requirement["combatDamageStaticPolicy"]);
        Assert.Equal("deferred", requirement["attackDefenseDamageTriggerPolicy"]);
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Empty(targetChoicesByIndex);

        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]).ToArray();
        Assert.Equal([$"RECYCLE_RUNE:{BlueRuneObjectId}"], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(
            paymentResourceChoices,
            choice => choice.Id.StartsWith(PaymentCostRules.TemporaryPaymentResourceActionPrefix, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            requirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Blue, paymentResourcePowerByChoice[$"RECYCLE_RUNE:{BlueRuneObjectId}"]["trait"]);
    }

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.EzrealBlueSwiftAltCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.EzrealBlueSwiftPromoCardNo)]
    public async Task EzrealCommandPaysBlueCreatesStackAndResolutionMovesSourceToBase(string cardNo)
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateEzrealAsync(
            BuildEzrealSwiftState(
                cardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                })),
            engine: engine);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], activated.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), activated.State.RunePools["P1"]);
        Assert.Contains(EzrealObjectId, activated.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(EzrealObjectId, activated.State.PlayerZones["P1"].Base);
        Assert.False(activated.State.CardObjects[EzrealObjectId].IsExhausted);
        var stackItem = Assert.Single(activated.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityEffectKind, stackItem.EffectKind);
        Assert.Empty(stackItem.TargetObjectIds);
        var costEvent = Assert.Single(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, costPowerByTrait[RuneTrait.Blue]);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-ezreal-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-ezreal-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(EzrealObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(EzrealObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("BASE", p2Pass.State.ObjectLocations[EzrealObjectId].Zone);
        Assert.Null(p2Pass.State.ObjectLocations[EzrealObjectId].BattlefieldObjectId);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId, StringComparison.Ordinal));
        var moveEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal(EzrealObjectId, moveEvent.Payload["sourceObjectId"]);
        Assert.Equal("EZREAL_BLUE_SWIFT_MOVE_TO_BASE", moveEvent.Payload["movementPermission"]);
        Assert.Equal("BASE", moveEvent.Payload["destinationZone"]);
        Assert.True(Assert.IsType<bool>(moveEvent.Payload["swift"]));
    }

    [Fact]
    public async Task EzrealCanRecycleBlueRuneForTypedBlueShortfall()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{BlueRuneObjectId}";
        var state = BuildEzrealSwiftState(
            P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
            RunePool.Empty,
            baseObjectIds: [BlueRuneObjectId],
            runeDeckObjectIds: ["P1-RUNE-BOTTOM"],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue),
                ["P1-RUNE-BOTTOM"] = RuneCard("P1-RUNE-BOTTOM", RuneTrait.Green)
            });

        var result = await ActivateEzrealAsync(state, optionalCosts: [paymentResourceAction]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED", "ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.DoesNotContain(BlueRuneObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM", BlueRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([BlueRuneObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task EzrealStackResolutionNoEffectsWhenSourceLeavesBattlefieldBeforeResolution()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateEzrealAsync(
            BuildEzrealSwiftState(
                P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                })),
            engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var staleState = MoveEzrealToBase(activated.State);

        var p1Pass = await engine.ResolveAsync(
            staleState,
            new PlayerIntent("intent-ezreal-stale-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-ezreal-stale-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(EzrealObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", p2Pass.State.ObjectLocations[EzrealObjectId].Zone);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_NO_EFFECT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "SOURCE_NO_LONGER_LEGAL", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("controller-changed")]
    [InlineData("face-down")]
    public async Task EzrealStackResolutionNoEffectsWhenSourceStopsBeingControlledOrPublicBeforeResolution(string scenario)
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateEzrealAsync(
            BuildEzrealSwiftState(
                P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                })),
            engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var staleSource = scenario switch
        {
            "controller-changed" => activated.State.CardObjects[EzrealObjectId] with { ControllerId = "P2" },
            "face-down" => activated.State.CardObjects[EzrealObjectId] with { IsFaceDown = true, CardNo = null },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };
        var staleState = activated.State with
        {
            CardObjects = ReplaceCardObject(activated.State.CardObjects, EzrealObjectId, staleSource)
        };

        var p1Pass = await engine.ResolveAsync(
            staleState,
            new PlayerIntent($"intent-ezreal-{scenario}-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"intent-ezreal-{scenario}-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(EzrealObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(EzrealObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal("BATTLEFIELD", p2Pass.State.ObjectLocations[EzrealObjectId].Zone);
        Assert.Equal(BattlefieldObjectId, p2Pass.State.ObjectLocations[EzrealObjectId].BattlefieldObjectId);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_NO_EFFECT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "SOURCE_NO_LONGER_LEGAL", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("base-source")]
    [InlineData("hand-source")]
    [InlineData("deck-source")]
    [InlineData("graveyard-source")]
    [InlineData("face-down-source")]
    [InlineData("enemy-controlled-source")]
    [InlineData("wrong-card-source")]
    [InlineData("dirty-source")]
    [InlineData("missing-precise-location")]
    [InlineData("submitted-target")]
    [InlineData("battlefield-destination-target")]
    [InlineData("wrong-trait-recycle")]
    [InlineData("generic-temporary-resource")]
    [InlineData("duplicate-recycle")]
    [InlineData("invalid-recycle")]
    [InlineData("unnecessary-recycle")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("insufficient-blue")]
    public async Task EzrealRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "submitted-target" => EzrealCommand(targetObjectIds: ["P2-ENEMY-UNIT"]),
            "battlefield-destination-target" => EzrealCommand(targetObjectIds: [BattlefieldObjectId]),
            "wrong-trait-recycle" => EzrealCommand(optionalCosts: [$"RECYCLE_RUNE:{GreenRuneObjectId}"]),
            "generic-temporary-resource" => EzrealCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-EZREAL")]),
            "duplicate-recycle" => EzrealCommand(optionalCosts: [$"RECYCLE_RUNE:{BlueRuneObjectId}", $"RECYCLE_RUNE:{BlueRuneObjectId}"]),
            "invalid-recycle" => EzrealCommand(optionalCosts: ["RECYCLE_RUNE:P1-RUNE-MISSING"]),
            "unnecessary-recycle" => EzrealCommand(optionalCosts: [$"RECYCLE_RUNE:{BlueRuneObjectId}"]),
            "unsupported-optional-cost" => EzrealCommand(optionalCosts: [$"BATTLEFIELD:{BattlefieldObjectId}"]),
            _ => EzrealCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ActivateEzrealAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-ezreal-activate", "P1", CommandTypes.ActivateAbility),
            EzrealCommand(optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand EzrealCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            EzrealObjectId,
            P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId,
            targetObjectIds ?? [],
            optionalCosts);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-ezreal-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState BuildInvalidScenarioState(string scenario)
    {
        var state = scenario switch
        {
            "wrong-trait-recycle" => BuildEzrealSwiftState(
                P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
                RunePool.Empty,
                baseObjectIds: [GreenRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
                }),
            "generic-temporary-resource" => BuildEzrealSwiftState(P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo, RunePool.Empty) with
            {
                TemporaryPaymentResources = [GenericTemporaryResource("MALZAHAR:TEMP-EZREAL")]
            },
            "duplicate-recycle" => BuildEzrealSwiftState(
                P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
                RunePool.Empty,
                baseObjectIds: [BlueRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue)
                }),
            "unnecessary-recycle" => BuildEzrealSwiftState(
                P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                }),
                baseObjectIds: [BlueRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue)
                }),
            _ => BuildEzrealSwiftState(
                P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                }))
        };

        return scenario switch
        {
            "wrong-timing" => state with
            {
                TimingState = TimingStates.NeutralClosed,
                PriorityPlayerId = "P1",
                StackItems =
                [
                    new StackItemState("STACK-PENDING", "P2", "P2-PENDING", "TEST_PENDING", "UNL-001/219")
                ]
            },
            "base-source" => MoveEzrealToBase(state),
            "hand-source" => MoveEzrealToPrivateZone(state, "HAND"),
            "deck-source" => MoveEzrealToPrivateZone(state, "MAIN_DECK"),
            "graveyard-source" => MoveEzrealToPrivateZone(state, "GRAVEYARD"),
            "face-down-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EzrealObjectId,
                    state.CardObjects[EzrealObjectId] with { IsFaceDown = true, CardNo = null })
            },
            "enemy-controlled-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with { Battlefields = [BattlefieldObjectId] },
                    "P2",
                    state.PlayerZones["P2"] with { Battlefields = [EzrealObjectId, "P2-ENEMY-UNIT"] }),
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EzrealObjectId,
                    state.CardObjects[EzrealObjectId] with { OwnerId = "P2", ControllerId = "P2" }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    EzrealObjectId,
                    new ObjectLocationState("P2", "BATTLEFIELD", BattlefieldObjectId))
            },
            "wrong-card-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EzrealObjectId,
                    state.CardObjects[EzrealObjectId] with { CardNo = "SFD·083/221" })
            },
            "dirty-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EzrealObjectId,
                    state.CardObjects[EzrealObjectId] with { OwnerId = "P2", ControllerId = "P2" })
            },
            "missing-precise-location" => state with
            {
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    EzrealObjectId,
                    new ObjectLocationState("P1", "BATTLEFIELD"))
            },
            "insufficient-blue" => state with
            {
                RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
                {
                    ["P1"] = RunePool.Empty,
                    ["P2"] = RunePool.Empty
                }
            },
            _ => state
        };
    }

    private static MatchState MoveEzrealToBase(MatchState state)
    {
        return state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Base = state.PlayerZones["P1"].Base.Concat([EzrealObjectId]).Distinct(StringComparer.Ordinal).ToArray(),
                    Battlefields = state.PlayerZones["P1"].Battlefields.Where(objectId => !string.Equals(objectId, EzrealObjectId, StringComparison.Ordinal)).ToArray()
                }),
            ObjectLocations = ReplaceObjectLocation(
                state.ObjectLocations,
                EzrealObjectId,
                new ObjectLocationState("P1", "BASE"))
        };
    }

    private static MatchState MoveEzrealToPrivateZone(MatchState state, string zone)
    {
        return state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Base = [],
                    Battlefields = state.PlayerZones["P1"].Battlefields.Where(objectId => !string.Equals(objectId, EzrealObjectId, StringComparison.Ordinal)).ToArray(),
                    Hand = string.Equals(zone, "HAND", StringComparison.Ordinal) ? [EzrealObjectId] : [],
                    MainDeck = string.Equals(zone, "MAIN_DECK", StringComparison.Ordinal) ? [EzrealObjectId] : [],
                    Graveyard = string.Equals(zone, "GRAVEYARD", StringComparison.Ordinal) ? [EzrealObjectId] : []
                }),
            ObjectLocations = ReplaceObjectLocation(
                state.ObjectLocations,
                EzrealObjectId,
                new ObjectLocationState("P1", zone))
        };
    }

    private static MatchState BuildEzrealSwiftState(
        string cardNo,
        RunePool runePool,
        IReadOnlyList<string>? baseObjectIds = null,
        IReadOnlyList<string>? runeDeckObjectIds = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [EzrealObjectId] = Ezreal(EzrealObjectId, cardNo, "P1"),
            [BattlefieldObjectId] = Battlefield(BattlefieldObjectId, "P1"),
            ["P2-ENEMY-UNIT"] = Unit("P2-ENEMY-UNIT", "SFD·125/221", "P2", 2)
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [EzrealObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            ["P2-ENEMY-UNIT"] = new("P2", "BATTLEFIELD", "BF-ENEMY")
        };
        foreach (var objectId in baseObjectIds ?? [])
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "BASE");
        }

        foreach (var objectId in runeDeckObjectIds ?? [])
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "RUNE_DECK");
        }

        return new MatchState(
            "room-ezreal-blue-swift",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "Alice",
                ["P2"] = "Bob"
            },
            status: MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = baseObjectIds ?? [],
                    Battlefields = [BattlefieldObjectId, EzrealObjectId],
                    RuneDeck = runeDeckObjectIds ?? []
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-UNIT"]
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static CardObjectState Ezreal(string objectId, string cardNo, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(string objectId, string cardNo, string playerId, int power)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Battlefield(string objectId, string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "UNL·T01",
            power: 0,
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: controllerId,
            controllerId: controllerId);
    }

    private static CardObjectState RuneCard(string objectId, string trait)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
            cardNo: $"RUNE-{trait}",
            ownerId: "P1",
            controllerId: "P1");
    }

    private static TemporaryPaymentResourceState GenericTemporaryResource(string resourceId)
    {
        return new TemporaryPaymentResourceState(
            resourceId,
            "P1",
            "P1-MALZAHAR",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            "ACTIVATE_ABILITY",
            2,
            2,
            [PaymentCostRules.RuneCostPaymentKind],
            1);
    }

    private static IReadOnlyDictionary<string, CardObjectState> ReplaceCardObject(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        CardObjectState replacement)
    {
        var next = cardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocation(
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        string objectId,
        ObjectLocationState replacement)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, PlayerZones> ReplacePlayerZones(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string playerId,
        PlayerZones zones)
    {
        var next = playerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[playerId] = zones;
        return next;
    }

    private static IReadOnlyDictionary<string, PlayerZones> ReplacePlayerZones(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string firstPlayerId,
        PlayerZones firstZones,
        string secondPlayerId,
        PlayerZones secondZones)
    {
        var next = playerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[firstPlayerId] = firstZones;
        next[secondPlayerId] = secondZones;
        return next;
    }
}
