using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RenataActivatedAbilityTests
{
    private const string RenataObjectId = "P1-RENATA";
    private const string BlueRuneObjectId = "P1-RUNE-BLUE";
    private const string RedRuneObjectId = "P1-RUNE-RED";
    private const string DrawCardObjectId = "P1-DRAW-001";

    [Fact]
    public void RenataOpenMainPromptExposesTypedBlueDrawRequirement()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{BlueRuneObjectId}";
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-RENATA-PROMPT");
        var state = BuildRenataState(
            P4ActivatedAbilityCatalog.RenataGlascCardNo,
            new RunePool(1, 0),
            p1BaseObjectIds: [BlueRuneObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue)
            }) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([RenataObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId,
                StringComparison.Ordinal));

        Assert.Equal(RenataObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascCardNo, requirement["cardNo"]);
        Assert.Equal(1, requirement["manaCost"]);
        Assert.Equal(0, requirement["powerCost"]);
        var powerCostByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["powerCostByTrait"]);
        Assert.Equal(1, powerCostByTrait[RuneTrait.Blue]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.False(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.False(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.Equal("ordinary-stack-item-before-draw", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-typed-blue", requirement["paymentPolicy"]);
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Empty(targetChoicesByIndex);

        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]);
        Assert.Equal([paymentResourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(
            paymentResourceChoices,
            choice => choice.Id.StartsWith(PaymentCostRules.TemporaryPaymentResourceActionPrefix, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            requirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Blue, paymentResourcePowerByChoice[paymentResourceAction]["trait"]);
        Assert.Equal(1, paymentResourcePowerByChoice[paymentResourceAction]["power"]);
        var availablePowerByTraitWithResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            requirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(1, availablePowerByTraitWithResources[RuneTrait.Blue]);
    }

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.RenataGlascCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.RenataGlascAltCardNo)]
    public async Task RenataDrawCommandPaysTypedBlueAndCreatesStackWithoutImmediateDraw(string cardNo)
    {
        var state = BuildRenataState(cardNo, new RunePool(1, 0, new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RuneTrait.Blue] = 1
        }));

        var result = await ActivateRenataAsync(state);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal([DrawCardObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.False(result.State.CardObjects[RenataObjectId].IsExhausted);
        Assert.Equal([RenataObjectId], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascDrawAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(cardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, costEvent.Payload["abilityId"]);
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, costPowerByTrait[RuneTrait.Blue]);
    }

    [Fact]
    public async Task RenataDrawCanRecycleBlueRuneForTypedBlueShortfall()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{BlueRuneObjectId}";
        var state = BuildRenataState(
            P4ActivatedAbilityCatalog.RenataGlascCardNo,
            new RunePool(1, 0),
            p1BaseObjectIds: [BlueRuneObjectId],
            runeDeckObjectIds: ["P1-RUNE-BOTTOM"],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue),
                ["P1-RUNE-BOTTOM"] = RuneCard("P1-RUNE-BOTTOM", RuneTrait.Red)
            });

        var result = await ActivateRenataAsync(state, optionalCosts: [paymentResourceAction]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED", "ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.DoesNotContain(BlueRuneObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM", BlueRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[BlueRuneObjectId].Zone);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([BlueRuneObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, costPowerByTrait[RuneTrait.Blue]);
    }

    [Fact]
    public async Task RenataDrawStackPassPassDrawsOneWithoutMovingOrExhaustingSource()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateRenataAsync(
            BuildRenataState(
                P4ActivatedAbilityCatalog.RenataGlascCardNo,
                new RunePool(1, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                })),
            engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-renata-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-renata-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal([DrawCardObjectId], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal([RenataObjectId], p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.False(p2Pass.State.CardObjects[RenataObjectId].IsExhausted);
        Assert.Equal(0, p2Pass.State.PlayerScores["P1"]);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 1));
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("target")]
    [InlineData("temporary-resource")]
    [InlineData("wrong-trait-recycle")]
    [InlineData("unnecessary-recycle")]
    [InlineData("duplicate-recycle")]
    [InlineData("invalid-recycle")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("insufficient-mana")]
    [InlineData("insufficient-blue")]
    [InlineData("base-source")]
    [InlineData("face-down")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("non-active-player")]
    public async Task RenataDrawRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "target" => RenataCommand(targetObjectIds: ["P2-TARGET"]),
            "temporary-resource" => RenataCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-RENATA")]),
            "wrong-trait-recycle" => RenataCommand(optionalCosts: [$"RECYCLE_RUNE:{RedRuneObjectId}"]),
            "unnecessary-recycle" => RenataCommand(optionalCosts: [$"RECYCLE_RUNE:{BlueRuneObjectId}"]),
            "duplicate-recycle" => RenataCommand(optionalCosts: [$"RECYCLE_RUNE:{BlueRuneObjectId}", $"RECYCLE_RUNE:{BlueRuneObjectId}"]),
            "invalid-recycle" => RenataCommand(optionalCosts: ["RECYCLE_RUNE:P1-RUNE-MISSING"]),
            "unsupported-optional-cost" => RenataCommand(optionalCosts: ["UNSUPPORTED_OPTIONAL_COST"]),
            _ => RenataCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ActivateRenataAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-renata-draw", "P1", CommandTypes.ActivateAbility),
            RenataCommand(optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand RenataCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            RenataObjectId,
            P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId,
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
            new PlayerIntent("intent-renata-invalid", "P1", CommandTypes.ActivateAbility),
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
            "insufficient-mana" => BuildRenataState(
                P4ActivatedAbilityCatalog.RenataGlascCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                })),
            "insufficient-blue" => BuildRenataState(P4ActivatedAbilityCatalog.RenataGlascCardNo, new RunePool(1, 0)),
            "wrong-trait-recycle" => BuildRenataState(
                P4ActivatedAbilityCatalog.RenataGlascCardNo,
                new RunePool(1, 0),
                p1BaseObjectIds: [RedRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red)
                }),
            "unnecessary-recycle" => BuildRenataState(
                P4ActivatedAbilityCatalog.RenataGlascCardNo,
                new RunePool(1, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                }),
                p1BaseObjectIds: [BlueRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue)
                }),
            "duplicate-recycle" => BuildRenataState(
                P4ActivatedAbilityCatalog.RenataGlascCardNo,
                new RunePool(1, 0),
                p1BaseObjectIds: [BlueRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue)
                }),
            _ => BuildRenataState(
                P4ActivatedAbilityCatalog.RenataGlascCardNo,
                new RunePool(1, 0, new Dictionary<string, int>(StringComparer.Ordinal)
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
                    new StackItemState(
                        "STACK-PENDING",
                        "P2",
                        "P2-PENDING-SPELL",
                        "TEST_PENDING",
                        "UNL-001/219")
                ]
            },
            "temporary-resource" => state with
            {
                TemporaryPaymentResources = [TemporaryResource("MALZAHAR:TEMP-RENATA")]
            },
            "base-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = [RenataObjectId],
                        Battlefields = []
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    RenataObjectId,
                    new ObjectLocationState("P1", "BASE"))
            },
            "face-down" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    RenataObjectId,
                    state.CardObjects[RenataObjectId] with { IsFaceDown = true })
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    RenataObjectId,
                    state.CardObjects[RenataObjectId] with { ControllerId = "P2" })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    RenataObjectId,
                    state.CardObjects[RenataObjectId] with { CardNo = "SFD·089/221" })
            },
            "non-active-player" => state with
            {
                ActivePlayerId = "P2",
                TurnPlayerId = "P2"
            },
            _ => state
        };
    }

    private static MatchState BuildRenataState(
        string cardNo,
        RunePool runePool,
        IReadOnlyList<string>? p1BaseObjectIds = null,
        IReadOnlyList<string>? runeDeckObjectIds = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [RenataObjectId] = Unit(RenataObjectId, cardNo, "P1", power: 4),
            [DrawCardObjectId] = Unit(DrawCardObjectId, "UNL-101/219", "P1", power: 2)
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [RenataObjectId] = new("P1", "BATTLEFIELD", "P1-MAIN"),
            [DrawCardObjectId] = new("P1", "MAIN_DECK")
        };
        foreach (var objectId in p1BaseObjectIds ?? [])
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "BASE");
        }

        foreach (var objectId in runeDeckObjectIds ?? [])
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "RUNE_DECK");
        }

        return new MatchState(
            "room-renata-draw",
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
                    Battlefields = [RenataObjectId],
                    Base = p1BaseObjectIds ?? [],
                    RuneDeck = runeDeckObjectIds ?? [],
                    MainDeck = [DrawCardObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string playerId,
        int power)
    {
        return new CardObjectState(
            objectId,
            power: power,
            tags: [CardObjectTags.UnitCard],
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
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

    private static TemporaryPaymentResourceState TemporaryResource(string resourceId)
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

    private static IReadOnlyDictionary<string, PlayerZones> ReplacePlayerZones(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string playerId,
        PlayerZones replacement)
    {
        var next = playerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[playerId] = replacement;
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
}
