using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GatekeeperMaduliActivatedAbilityTests
{
    private const string MaduliObjectId = "P1-MADULI";
    private const string PurpleRuneObjectId = "P1-RUNE-PURPLE";
    private const string GreenRuneObjectId = "P1-RUNE-GREEN";
    private const string LegalBattlefieldObjectId = "P2-BF-LOW";
    private const string StrongBattlefieldObjectId = "P2-BF-STRONG";
    private const string FriendlyBattlefieldObjectId = "P1-BF-FRIENDLY";
    private const string LegalEnemyUnitObjectId = "P2-LOW-UNIT";
    private const string StrongEnemyUnitObjectId = "P2-STRONG-UNIT";

    [Fact]
    public void CatalogExposesGatekeeperMaduliPurpleMoveAbility()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityEffectKind, ability.EffectKind);
        Assert.Equal(0, ability.ManaCost);
        Assert.Equal(0, ability.PowerCost);
        Assert.Equal(1, ability.RequiredTargetCount);
        Assert.False(ability.ExhaustsSourceAsCost);
        Assert.False(ability.ReactionSpeed);
        var powerCostByTrait = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);
        Assert.Equal(P4ActivatedAbilityCatalog.GatekeeperMaduliMovePurplePowerCost, powerCostByTrait[RuneTrait.Purple]);
    }

    [Fact]
    public void PromptExposesMaduliRequirementWithPurpleCostLegalBattlefieldTargetAndRecycleChoice()
    {
        var state = BuildMaduliState(
            RunePool.Empty,
            baseObjectIds: [PurpleRuneObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [PurpleRuneObjectId] = RuneCard(PurpleRuneObjectId, RuneTrait.Purple)
            }) with
        {
            TemporaryPaymentResources = [GenericTemporaryResource("MALZAHAR:TEMP-MADULI")]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([MaduliObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId, StringComparison.Ordinal));

        Assert.Equal(MaduliObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["manaCost"]);
        Assert.Equal(0, requirement["powerCost"]);
        var powerCostByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["powerCostByTrait"]);
        Assert.Equal(1, powerCostByTrait[RuneTrait.Purple]);
        Assert.Equal(1, requirement["minTargetCount"]);
        Assert.Equal(1, requirement["maxTargetCount"]);
        Assert.False(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.Equal("enemy-controlled-battlefield-with-lower-enemy-unit-power", requirement["targetScope"]);
        Assert.Equal("move-source-to-target-battlefield", requirement["movePolicy"]);
        Assert.Equal("ordinary-stack-item-before-move", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-typed-purple", requirement["paymentPolicy"]);
        Assert.Equal("implemented", requirement["staticCannotBecomeActivePolicy"]);

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Equal([LegalBattlefieldObjectId], targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());

        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]).ToArray();
        Assert.Equal([$"RECYCLE_RUNE:{PurpleRuneObjectId}"], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(
            paymentResourceChoices,
            choice => choice.Id.StartsWith(PaymentCostRules.TemporaryPaymentResourceActionPrefix, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            requirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Purple, paymentResourcePowerByChoice[$"RECYCLE_RUNE:{PurpleRuneObjectId}"]["trait"]);
        var availablePowerByTraitWithResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            requirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(1, availablePowerByTraitWithResources[RuneTrait.Purple]);
    }

    [Fact]
    public async Task MaduliCommandPaysPurpleCreatesStackAndResolutionMovesToTargetBattlefield()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateMaduliAsync(
            BuildMaduliState(new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Purple] = 1
            })),
            engine: engine);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], activated.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), activated.State.RunePools["P1"]);
        Assert.False(activated.State.CardObjects[MaduliObjectId].IsExhausted);
        var stackItem = Assert.Single(activated.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal([LegalBattlefieldObjectId], stackItem.TargetObjectIds);
        var costEvent = Assert.Single(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, costPowerByTrait[RuneTrait.Purple]);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-maduli-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-maduli-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain(MaduliObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(MaduliObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("BATTLEFIELD", p2Pass.State.ObjectLocations[MaduliObjectId].Zone);
        Assert.Equal(LegalBattlefieldObjectId, p2Pass.State.ObjectLocations[MaduliObjectId].BattlefieldObjectId);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId, StringComparison.Ordinal));
        var moveEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal(MaduliObjectId, moveEvent.Payload["sourceObjectId"]);
        Assert.Equal(LegalBattlefieldObjectId, moveEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("GATEKEEPER_MADULI_PURPLE_MOVE", moveEvent.Payload["movementPermission"]);
        Assert.Equal(6, moveEvent.Payload["sourcePower"]);
        Assert.Equal(3, moveEvent.Payload["enemyUnitPowerSum"]);
    }

    [Fact]
    public async Task MaduliCanRecyclePurpleRuneForTypedPurpleShortfall()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{PurpleRuneObjectId}";
        var state = BuildMaduliState(
            RunePool.Empty,
            baseObjectIds: [PurpleRuneObjectId],
            runeDeckObjectIds: ["P1-RUNE-BOTTOM"],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [PurpleRuneObjectId] = RuneCard(PurpleRuneObjectId, RuneTrait.Purple),
                ["P1-RUNE-BOTTOM"] = RuneCard("P1-RUNE-BOTTOM", RuneTrait.Green)
            });

        var result = await ActivateMaduliAsync(state, optionalCosts: [paymentResourceAction]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED", "ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.DoesNotContain(PurpleRuneObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM", PurpleRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([PurpleRuneObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task MaduliStackResolutionNoEffectsWhenTargetPowerConditionBecomesStale()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateMaduliAsync(
            BuildMaduliState(new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Purple] = 1
            })),
            engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var staleState = activated.State with
        {
            CardObjects = ReplaceCardObject(
                activated.State.CardObjects,
                LegalEnemyUnitObjectId,
                activated.State.CardObjects[LegalEnemyUnitObjectId] with { Power = 6 })
        };

        var p1Pass = await engine.ResolveAsync(
            staleState,
            new PlayerIntent("intent-maduli-stale-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-maduli-stale-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(MaduliObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", p2Pass.State.ObjectLocations[MaduliObjectId].Zone);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_NO_EFFECT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "TARGET_NO_LONGER_LEGAL", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("hand-source")]
    [InlineData("graveyard-source")]
    [InlineData("face-down-source")]
    [InlineData("enemy-controlled-source")]
    [InlineData("wrong-card-source")]
    [InlineData("dirty-source")]
    [InlineData("friendly-battlefield")]
    [InlineData("uncontrolled-battlefield")]
    [InlineData("non-battlefield-target")]
    [InlineData("insufficient-power-gap")]
    [InlineData("dirty-target")]
    [InlineData("stale-target")]
    [InlineData("wrong-trait-recycle")]
    [InlineData("generic-temporary-resource")]
    [InlineData("duplicate-recycle")]
    [InlineData("invalid-recycle")]
    [InlineData("unnecessary-recycle")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("insufficient-purple")]
    public async Task MaduliRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "friendly-battlefield" => MaduliCommand(targetObjectIds: [FriendlyBattlefieldObjectId]),
            "uncontrolled-battlefield" => MaduliCommand(targetObjectIds: ["P2-BF-UNCONTROLLED"]),
            "non-battlefield-target" => MaduliCommand(targetObjectIds: [LegalEnemyUnitObjectId]),
            "insufficient-power-gap" => MaduliCommand(targetObjectIds: [StrongBattlefieldObjectId]),
            "dirty-target" => MaduliCommand(targetObjectIds: ["P2-BF-DIRTY"]),
            "stale-target" => MaduliCommand(targetObjectIds: ["P2-BF-STALE"]),
            "wrong-trait-recycle" => MaduliCommand(optionalCosts: [$"RECYCLE_RUNE:{GreenRuneObjectId}"]),
            "generic-temporary-resource" => MaduliCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-MADULI")]),
            "duplicate-recycle" => MaduliCommand(optionalCosts: [$"RECYCLE_RUNE:{PurpleRuneObjectId}", $"RECYCLE_RUNE:{PurpleRuneObjectId}"]),
            "invalid-recycle" => MaduliCommand(optionalCosts: ["RECYCLE_RUNE:P1-RUNE-MISSING"]),
            "unnecessary-recycle" => MaduliCommand(optionalCosts: [$"RECYCLE_RUNE:{PurpleRuneObjectId}"]),
            "unsupported-optional-cost" => MaduliCommand(optionalCosts: ["UNSUPPORTED_OPTIONAL_COST"]),
            _ => MaduliCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ActivateMaduliAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-maduli-activate", "P1", CommandTypes.ActivateAbility),
            MaduliCommand(optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand MaduliCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            MaduliObjectId,
            P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId,
            targetObjectIds ?? [LegalBattlefieldObjectId],
            optionalCosts);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-maduli-invalid", "P1", CommandTypes.ActivateAbility),
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
            "wrong-trait-recycle" => BuildMaduliState(
                RunePool.Empty,
                baseObjectIds: [GreenRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
                }),
            "generic-temporary-resource" => BuildMaduliState(RunePool.Empty) with
            {
                TemporaryPaymentResources = [GenericTemporaryResource("MALZAHAR:TEMP-MADULI")]
            },
            "duplicate-recycle" => BuildMaduliState(
                RunePool.Empty,
                baseObjectIds: [PurpleRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [PurpleRuneObjectId] = RuneCard(PurpleRuneObjectId, RuneTrait.Purple)
                }),
            "unnecessary-recycle" => BuildMaduliState(
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Purple] = 1
                }),
                baseObjectIds: [PurpleRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [PurpleRuneObjectId] = RuneCard(PurpleRuneObjectId, RuneTrait.Purple)
                }),
            _ => BuildMaduliState(new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Purple] = 1
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
            "hand-source" => MoveMaduliToZone(state, hand: true),
            "graveyard-source" => MoveMaduliToZone(state, graveyard: true),
            "face-down-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    MaduliObjectId,
                    state.CardObjects[MaduliObjectId] with { IsFaceDown = true, CardNo = null })
            },
            "enemy-controlled-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with { Base = [] },
                    "P2",
                    state.PlayerZones["P2"] with { Base = [MaduliObjectId], Battlefields = state.PlayerZones["P2"].Battlefields }),
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    MaduliObjectId,
                    state.CardObjects[MaduliObjectId] with { OwnerId = "P2", ControllerId = "P2" }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    MaduliObjectId,
                    new ObjectLocationState("P2", "BASE"))
            },
            "wrong-card-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    MaduliObjectId,
                    state.CardObjects[MaduliObjectId] with { CardNo = "UNL-145/219" })
            },
            "dirty-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    MaduliObjectId,
                    state.CardObjects[MaduliObjectId] with { OwnerId = "P2", ControllerId = "P2" })
            },
            "uncontrolled-battlefield" => state,
            "insufficient-purple" => state with
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

    private static MatchState MoveMaduliToZone(MatchState state, bool hand = false, bool graveyard = false)
    {
        var zone = hand ? "HAND" : graveyard ? "GRAVEYARD" : "BASE";
        return state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Base = [],
                    Hand = hand ? [MaduliObjectId] : [],
                    Graveyard = graveyard ? [MaduliObjectId] : []
                }),
            ObjectLocations = ReplaceObjectLocation(
                state.ObjectLocations,
                MaduliObjectId,
                new ObjectLocationState("P1", zone))
        };
    }

    private static MatchState BuildMaduliState(
        RunePool runePool,
        IReadOnlyList<string>? baseObjectIds = null,
        IReadOnlyList<string>? runeDeckObjectIds = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null)
    {
        var baseIds = new[] { MaduliObjectId }
            .Concat(baseObjectIds ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var p2Battlefields = new[]
        {
            LegalBattlefieldObjectId,
            StrongBattlefieldObjectId,
            "P2-BF-UNCONTROLLED",
            "P2-BF-DIRTY",
            LegalEnemyUnitObjectId,
            StrongEnemyUnitObjectId
        };
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [MaduliObjectId] = Unit(MaduliObjectId, P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo, "P1", 6),
            [LegalBattlefieldObjectId] = Battlefield(LegalBattlefieldObjectId, "P2"),
            [StrongBattlefieldObjectId] = Battlefield(StrongBattlefieldObjectId, "P2"),
            [FriendlyBattlefieldObjectId] = Battlefield(FriendlyBattlefieldObjectId, "P1"),
            ["P2-BF-UNCONTROLLED"] = Battlefield("P2-BF-UNCONTROLLED", string.Empty),
            ["P2-BF-DIRTY"] = Battlefield("P2-BF-DIRTY", "P1"),
            ["P2-BF-STALE"] = Battlefield("P2-BF-STALE", "P2"),
            [LegalEnemyUnitObjectId] = Unit(LegalEnemyUnitObjectId, "SFD·125/221", "P2", 3),
            [StrongEnemyUnitObjectId] = Unit(StrongEnemyUnitObjectId, "SFD·126/221", "P2", 6)
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [MaduliObjectId] = new("P1", "BASE"),
            [LegalBattlefieldObjectId] = new("P2", "BATTLEFIELD", LegalBattlefieldObjectId),
            [StrongBattlefieldObjectId] = new("P2", "BATTLEFIELD", StrongBattlefieldObjectId),
            [FriendlyBattlefieldObjectId] = new("P1", "BATTLEFIELD", FriendlyBattlefieldObjectId),
            ["P2-BF-UNCONTROLLED"] = new("P2", "BATTLEFIELD", "P2-BF-UNCONTROLLED"),
            ["P2-BF-DIRTY"] = new("P2", "BATTLEFIELD", "P2-BF-DIRTY"),
            ["P2-BF-STALE"] = new("P2", "GRAVEYARD"),
            [LegalEnemyUnitObjectId] = new("P2", "BATTLEFIELD", LegalBattlefieldObjectId),
            [StrongEnemyUnitObjectId] = new("P2", "BATTLEFIELD", StrongBattlefieldObjectId)
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
            "room-gatekeeper-maduli",
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
                    Base = baseIds,
                    Battlefields = [FriendlyBattlefieldObjectId],
                    RuneDeck = runeDeckObjectIds ?? []
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo,
        string playerId,
        int power)
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
            ownerId: string.IsNullOrWhiteSpace(controllerId) ? string.Empty : controllerId,
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
