using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CrimsonRoseActivatedAbilityTests
{
    private const string CrimsonRoseObjectId = "P1-CRIMSON-ROSE";
    private const string FriendlyBaseUnitObjectId = "P1-BASE-UNIT";
    private const string FriendlyBattlefieldUnitObjectId = "P1-BATTLEFIELD-UNIT";
    private const string EnemyBaseUnitObjectId = "P2-BASE-UNIT";
    private const string EnemySpellshieldUnitObjectId = "P2-SPELLSHIELD-UNIT";
    private const string FriendlyMaduliObjectId = "P1-MADULI";

    [Fact]
    public void CrimsonRoseOpenMainPromptExposesExperienceReadyUnitRequirement()
    {
        var state = BuildCrimsonRoseState(mana: 1, experience: 3);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([CrimsonRoseObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
                StringComparison.Ordinal));

        Assert.Equal(CrimsonRoseObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["manaCost"]);
        Assert.Equal(0, requirement["powerCost"]);
        Assert.Equal(3, requirement["experienceCost"]);
        Assert.Equal(1, requirement["minTargetCount"]);
        Assert.Equal(1, requirement["maxTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.False(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.Equal("ordinary-stack-item-before-ready", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-experience-and-spellshield-tax", requirement["paymentPolicy"]);
        Assert.True(Assert.IsType<bool>(requirement["requiresBaseEquipmentSource"]));
        Assert.True(Assert.IsType<bool>(requirement["appliesSpellshieldTargetTax"]));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        var targetIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(targetChoicesByIndex["0"])
            .Select(choice => choice.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [FriendlyBaseUnitObjectId, FriendlyBattlefieldUnitObjectId, EnemyBaseUnitObjectId, EnemySpellshieldUnitObjectId],
            targetIds);
    }

    [Fact]
    public void CrimsonRoseReadyUnitPromptHidesGatekeeperMaduliCannotBecomeActiveTarget()
    {
        var state = AddFriendlyMaduli(BuildCrimsonRoseState(mana: 1, experience: 3));

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
                StringComparison.Ordinal));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);

        Assert.DoesNotContain(
            FriendlyMaduliObjectId,
            targetChoicesByIndex["0"].Select(choice => choice.Id));
    }

    [Theory]
    [InlineData("insufficient-experience")]
    [InlineData("source-exhausted")]
    [InlineData("source-battlefield")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    public void CrimsonRosePromptHidesIllegalSourceOrInsufficientExperience(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = (prompt.Candidates ?? [])
            .SingleOrDefault(candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        if (activateCandidate is null)
        {
            return;
        }

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var abilityIds = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"])
            .Select(entry => entry["abilityId"] as string)
            .ToArray();
        Assert.DoesNotContain(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, abilityIds);
    }

    [Fact]
    public async Task CrimsonRoseFriendlySpellshieldTargetPaysExperienceNoTaxAndCreatesStack()
    {
        var state = BuildCrimsonRoseState(
            mana: 0,
            experience: 3,
            friendlyBaseUnitTags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]);

        var result = await ActivateCrimsonRoseAsync(state, FriendlyBaseUnitObjectId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "EQUIPMENT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(result.State.CardObjects[FriendlyBaseUnitObjectId].IsExhausted);
        Assert.Equal([CrimsonRoseObjectId, FriendlyBaseUnitObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseCardNo, stackItem.CardNo);
        Assert.Equal([FriendlyBaseUnitObjectId], stackItem.TargetObjectIds);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, costEvent.Payload["abilityId"]);
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        Assert.Equal(3, costEvent.Payload["experienceCost"]);
        Assert.Equal(0, costEvent.Payload["remainingExperience"]);
        Assert.Equal(0, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task CrimsonRoseEnemySpellshieldTargetPaysManaTax()
    {
        var state = BuildCrimsonRoseState(mana: 1, experience: 3);

        var result = await ActivateCrimsonRoseAsync(state, EnemySpellshieldUnitObjectId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.True(result.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(result.State.CardObjects[EnemySpellshieldUnitObjectId].IsExhausted);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal([EnemySpellshieldUnitObjectId], Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task CrimsonRoseStackPassPassReadiesTargetAndKeepsSourceInBaseExhausted()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateCrimsonRoseAsync(
            BuildCrimsonRoseState(mana: 0, experience: 3),
            FriendlyBaseUnitObjectId,
            engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-crimson-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-crimson-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal([CrimsonRoseObjectId, FriendlyBaseUnitObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.True(p2Pass.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.False(p2Pass.State.CardObjects[FriendlyBaseUnitObjectId].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, FriendlyBaseUnitObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["wasExhausted"], true)
            && Equals(gameEvent.Payload["isExhausted"], false));
    }

    [Fact]
    public async Task CrimsonRoseRejectsHandWrittenGatekeeperMaduliReadyTargetWithoutMutation()
    {
        var state = AddFriendlyMaduli(BuildCrimsonRoseState(mana: 1, experience: 3));

        await AssertRejectedNoMutationAsync(state, CrimsonRoseCommand([FriendlyMaduliObjectId]));
    }

    [Fact]
    public async Task CrimsonRoseStaleStackItemSkipsGatekeeperMaduliCannotBecomeActiveTarget()
    {
        var engine = new CoreRuleEngine();
        var baseState = AddFriendlyMaduli(BuildCrimsonRoseState(mana: 1, experience: 3));
        var state = baseState with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            CardObjects = ReplaceCardObject(
                baseState.CardObjects,
                CrimsonRoseObjectId,
                baseState.CardObjects[CrimsonRoseObjectId] with
                {
                    IsExhausted = true
                }),
            StackItems =
            [
                new StackItemState(
                    "STACK-STALE-CRIMSON-MADULI",
                    "P1",
                    CrimsonRoseObjectId,
                    P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind,
                    P4ActivatedAbilityCatalog.CrimsonRoseCardNo,
                    [FriendlyMaduliObjectId],
                    0,
                    1,
                    [])
            ]
        };

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-crimson-maduli-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-crimson-maduli-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.True(p2Pass.State.CardObjects[FriendlyMaduliObjectId].IsExhausted);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, FriendlyMaduliObjectId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("non-active-player")]
    [InlineData("missing-target")]
    [InlineData("too-many-targets")]
    [InlineData("invalid-target")]
    [InlineData("face-down-target")]
    [InlineData("standby-target")]
    [InlineData("insufficient-experience")]
    [InlineData("insufficient-tax-mana")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("recycle-rune")]
    [InlineData("temporary-resource")]
    [InlineData("source-exhausted")]
    [InlineData("source-battlefield")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    public async Task CrimsonRoseRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "missing-target" => CrimsonRoseCommand([]),
            "too-many-targets" => CrimsonRoseCommand([FriendlyBaseUnitObjectId, EnemyBaseUnitObjectId]),
            "invalid-target" => CrimsonRoseCommand(["P1-NON-UNIT-EQUIPMENT"]),
            "face-down-target" => CrimsonRoseCommand(["P1-FACE-DOWN-UNIT"]),
            "standby-target" => CrimsonRoseCommand(["P1-STANDBY-UNIT"]),
            "insufficient-tax-mana" => CrimsonRoseCommand([EnemySpellshieldUnitObjectId]),
            "unsupported-optional-cost" => CrimsonRoseCommand([FriendlyBaseUnitObjectId], ["SPEND_EXPERIENCE:3"]),
            "recycle-rune" => CrimsonRoseCommand([FriendlyBaseUnitObjectId], ["RECYCLE_RUNE:P1-RUNE-BLUE"]),
            "temporary-resource" => CrimsonRoseCommand([FriendlyBaseUnitObjectId], [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-CRIMSON")]),
            _ => CrimsonRoseCommand([FriendlyBaseUnitObjectId])
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ActivateCrimsonRoseAsync(
        MatchState state,
        string targetObjectId,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-crimson-rose", "P1", CommandTypes.ActivateAbility),
            CrimsonRoseCommand([targetObjectId]),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand CrimsonRoseCommand(
        IReadOnlyList<string> targetObjectIds,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            CrimsonRoseObjectId,
            P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
            targetObjectIds,
            optionalCosts);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-crimson-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState BuildInvalidScenarioState(string scenario)
    {
        var extraCardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-NON-UNIT-EQUIPMENT"] = Equipment("P1-NON-UNIT-EQUIPMENT", "SFD·022/221", "P1"),
            ["P1-FACE-DOWN-UNIT"] = Unit("P1-FACE-DOWN-UNIT", "UNL-101/219", "P1", isExhausted: true, isFaceDown: true),
            ["P1-STANDBY-UNIT"] = Unit("P1-STANDBY-UNIT", "UNL-102/219", "P1", isExhausted: true, extraTags: [CardObjectTags.Standby]),
            ["P1-RUNE-BLUE"] = RuneCard("P1-RUNE-BLUE", RuneTrait.Blue)
        };
        var state = scenario switch
        {
            "insufficient-experience" => BuildCrimsonRoseState(mana: 1, experience: 2, extraCardObjects: extraCardObjects),
            "insufficient-tax-mana" => BuildCrimsonRoseState(mana: 0, experience: 3, extraCardObjects: extraCardObjects),
            _ => BuildCrimsonRoseState(mana: 1, experience: 3, extraCardObjects: extraCardObjects)
        };

        state = state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Base = state.PlayerZones["P1"].Base
                        .Concat(["P1-NON-UNIT-EQUIPMENT", "P1-FACE-DOWN-UNIT", "P1-STANDBY-UNIT", "P1-RUNE-BLUE"])
                        .ToArray()
                }),
            ObjectLocations = ReplaceObjectLocations(
                state.ObjectLocations,
                new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
                {
                    ["P1-NON-UNIT-EQUIPMENT"] = new("P1", "BASE"),
                    ["P1-FACE-DOWN-UNIT"] = new("P1", "BASE"),
                    ["P1-STANDBY-UNIT"] = new("P1", "BASE"),
                    ["P1-RUNE-BLUE"] = new("P1", "BASE")
                })
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
            "non-active-player" => state with
            {
                ActivePlayerId = "P2",
                TurnPlayerId = "P2"
            },
            "source-exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { IsExhausted = true })
            },
            "source-battlefield" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = state.PlayerZones["P1"].Base
                            .Where(objectId => !string.Equals(objectId, CrimsonRoseObjectId, StringComparison.Ordinal))
                            .ToArray(),
                        Battlefields = [CrimsonRoseObjectId, FriendlyBattlefieldUnitObjectId]
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    CrimsonRoseObjectId,
                    new ObjectLocationState("P1", "BATTLEFIELD", "P1-MAIN"))
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { ControllerId = "P2" })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { CardNo = "UNL-110/219" })
            },
            "face-down-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { IsFaceDown = true })
            },
            _ => state
        };
    }

    private static MatchState BuildCrimsonRoseState(
        int mana,
        int experience,
        IReadOnlyList<string>? friendlyBaseUnitTags = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [CrimsonRoseObjectId] = Equipment(CrimsonRoseObjectId, P4ActivatedAbilityCatalog.CrimsonRoseCardNo, "P1"),
            [FriendlyBaseUnitObjectId] = Unit(FriendlyBaseUnitObjectId, "UNL-101/219", "P1", isExhausted: true, tags: friendlyBaseUnitTags),
            [FriendlyBattlefieldUnitObjectId] = Unit(FriendlyBattlefieldUnitObjectId, "UNL-102/219", "P1", isExhausted: false),
            [EnemyBaseUnitObjectId] = Unit(EnemyBaseUnitObjectId, "UNL-103/219", "P2", isExhausted: true),
            [EnemySpellshieldUnitObjectId] = Unit(EnemySpellshieldUnitObjectId, "UNL-104/219", "P2", isExhausted: true, extraTags: [CardObjectTags.Spellshield])
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        return new MatchState(
            "room-crimson-rose",
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
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = experience,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [CrimsonRoseObjectId, FriendlyBaseUnitObjectId],
                    Battlefields = [FriendlyBattlefieldUnitObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [EnemyBaseUnitObjectId],
                    Battlefields = [EnemySpellshieldUnitObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [CrimsonRoseObjectId] = new("P1", "BASE"),
                [FriendlyBaseUnitObjectId] = new("P1", "BASE"),
                [FriendlyBattlefieldUnitObjectId] = new("P1", "BATTLEFIELD", "P1-MAIN"),
                [EnemyBaseUnitObjectId] = new("P2", "BASE"),
                [EnemySpellshieldUnitObjectId] = new("P2", "BATTLEFIELD", "P2-MAIN")
            });
    }

    private static MatchState AddFriendlyMaduli(MatchState state)
    {
        return state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Base = state.PlayerZones["P1"].Base.Concat([FriendlyMaduliObjectId]).ToArray()
                }),
            CardObjects = ReplaceCardObject(
                state.CardObjects,
                FriendlyMaduliObjectId,
                Unit(FriendlyMaduliObjectId, P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo, "P1", isExhausted: true)),
            ObjectLocations = ReplaceObjectLocation(
                state.ObjectLocations,
                FriendlyMaduliObjectId,
                new ObjectLocationState("P1", "BASE"))
        };
    }

    private static CardObjectState Equipment(string objectId, string cardNo, string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.EquipmentCard],
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string playerId,
        bool isExhausted,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? extraTags = null,
        bool isFaceDown = false)
    {
        var resolvedTags = tags ?? new[] { CardObjectTags.UnitCard }
            .Concat(extraTags ?? Array.Empty<string>())
            .ToArray();
        return new CardObjectState(
            objectId,
            power: 2,
            isExhausted: isExhausted,
            isFaceDown: isFaceDown,
            tags: resolvedTags,
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

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocations(
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyDictionary<string, ObjectLocationState> replacements)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var entry in replacements)
        {
            next[entry.Key] = entry.Value;
        }

        return next;
    }
}
