using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MalzaharResourceSkillTests
{
    private const string MalzaharObjectId = "P1-MALZAHAR";
    private const string FriendlyUnitObjectId = "P1-FRIENDLY-UNIT";
    private const string FriendlyEquipmentObjectId = "P1-FRIENDLY-EQUIPMENT";
    private const string FriendlySpellObjectId = "P1-FRIENDLY-SPELL";
    private const string FriendlyHiddenUnitObjectId = "P1-HIDDEN-UNIT";
    private const string FriendlyHandUnitObjectId = "P1-HAND-UNIT";
    private const string EnemyUnitObjectId = "P2-ENEMY-UNIT";

    [Fact]
    public void MalzaharOpenMainPromptExposesSourceAndFriendlyUnitOrEquipmentCostTargets()
    {
        var state = BuildMalzaharState();
        var prompts = ResolutionResult.BuildPrompts(state);

        var p1Candidate = Assert.Single(
            prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.True(p1Candidate.Enabled);
        Assert.Equal([MalzaharObjectId], (p1Candidate.Sources ?? []).Select(choice => choice.Id).ToArray());

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(p1Candidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                StringComparison.Ordinal));
        Assert.Equal(MalzaharObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharCardNo, requirement["cardNo"]);
        Assert.Equal("友方单位或装备（成本）", requirement["targetScopeLabel"]);
        Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(requirement["usesTargetAsCost"]));
        Assert.True(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceGeneratedPower, requirement["generatedPower"]);
        Assert.Equal(
            P4ActivatedAbilityCatalog.MalzaharPaymentOnlyResourceRestriction,
            requirement["resourceRestriction"]);
        Assert.Equal("open-main-only-representative", requirement["timingPolicy"]);
        Assert.Equal("resolves-immediately-without-stack-item", requirement["reactionPolicy"]);

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        var targetIds = targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray();
        Assert.Contains(FriendlyUnitObjectId, targetIds);
        Assert.Contains(FriendlyEquipmentObjectId, targetIds);
        Assert.DoesNotContain(MalzaharObjectId, targetIds);
        Assert.DoesNotContain(FriendlySpellObjectId, targetIds);
        Assert.DoesNotContain(FriendlyHiddenUnitObjectId, targetIds);
        Assert.DoesNotContain(FriendlyHandUnitObjectId, targetIds);
        Assert.DoesNotContain(EnemyUnitObjectId, targetIds);

        Assert.DoesNotContain(
            prompts["P2"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
    }

    [Fact]
    public void MalzaharPromptDoesNotOpenSpellDuelRepresentativeWindow()
    {
        var state = BuildMalzaharState() with
        {
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = "P1"
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.DoesNotContain(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(FriendlyUnitObjectId, "UNIT_DESTROYED")]
    [InlineData(FriendlyEquipmentObjectId, "EQUIPMENT_DESTROYED")]
    public async Task MalzaharResourceSkillDestroysCostExhaustsSourceGainsPaymentOnlyPowerWithoutStack(
        string costObjectId,
        string expectedRemovalEventKind)
    {
        var state = BuildMalzaharState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-malzahar-resource-skill", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                MalzaharObjectId,
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                [costObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.True(result.State.CardObjects[MalzaharObjectId].IsExhausted);
        Assert.DoesNotContain(costObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(costObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("P1", result.State.ObjectLocations[costObjectId].PlayerId);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations[costObjectId].Zone);
        Assert.Equal(2, result.State.RunePools["P1"].Power);
        Assert.Equal(2, result.State.RunePools["P1"].TotalPower);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var removalEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, expectedRemovalEventKind, StringComparison.Ordinal));
        Assert.Equal("RESOURCE_SKILL_COST", removalEvent.Payload["reason"]);
        Assert.Equal(costObjectId, removalEvent.Payload["targetObjectId"]);
        Assert.Equal("ACTIVATE_ABILITY", removalEvent.Payload["paymentWindow"]);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, removalEvent.Payload["abilityId"]);
        Assert.True(Assert.IsType<bool>(removalEvent.Payload["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(removalEvent.Payload["paymentOnly"]));

        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", powerEvent.Payload["paymentWindow"]);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, powerEvent.Payload["abilityId"]);
        Assert.Equal(MalzaharObjectId, powerEvent.Payload["sourceObjectId"]);
        Assert.Equal(costObjectId, powerEvent.Payload["destroyedCostObjectId"]);
        Assert.True(Assert.IsType<bool>(powerEvent.Payload["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(powerEvent.Payload["paymentOnly"]));
        Assert.Equal(2, powerEvent.Payload["generatedPower"]);
        Assert.Equal(2, powerEvent.Payload["power"]);
        Assert.Equal(2, powerEvent.Payload["powerAfter"]);
        Assert.Equal(
            P4ActivatedAbilityCatalog.MalzaharPaymentOnlyResourceRestriction,
            powerEvent.Payload["resourceRestriction"]);
        Assert.Equal("representative-resource-generated-into-rune-pool", powerEvent.Payload["restrictionLifecycle"]);
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("non-malzahar")]
    [InlineData("opponent-source")]
    [InlineData("face-down-source")]
    [InlineData("hand-source")]
    [InlineData("spell-duel")]
    public async Task MalzaharResourceSkillRejectsInvalidSourcesWithoutMutation(string caseName)
    {
        var state = BuildInvalidSourceState(caseName);
        var initialHash = MatchStateHasher.Hash(state);
        var sourceObjectId = string.Equals(caseName, "opponent-source", StringComparison.Ordinal)
            ? EnemyUnitObjectId
            : MalzaharObjectId;

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-malzahar-invalid-source-{caseName}", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                sourceObjectId,
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                [FriendlyUnitObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("self")]
    [InlineData("enemy")]
    [InlineData("hidden")]
    [InlineData("unknown")]
    [InlineData("spell")]
    [InlineData("hand")]
    public async Task MalzaharResourceSkillRejectsInvalidCostTargetsWithoutMutation(string caseName)
    {
        var state = BuildMalzaharState();
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-malzahar-invalid-target-{caseName}", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                MalzaharObjectId,
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                InvalidTargetIds(caseName)),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.State.StackItems);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
    }

    private static MatchState BuildInvalidSourceState(string caseName)
    {
        var state = BuildMalzaharState();
        return caseName switch
        {
            "exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    MalzaharObjectId,
                    state.CardObjects[MalzaharObjectId] with { IsExhausted = true })
            },
            "non-malzahar" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    MalzaharObjectId,
                    state.CardObjects[MalzaharObjectId] with { CardNo = "UNL-030/219" })
            },
            "face-down-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    MalzaharObjectId,
                    state.CardObjects[MalzaharObjectId] with { IsFaceDown = true })
            },
            "hand-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = state.PlayerZones["P1"].Base
                            .Where(objectId => !string.Equals(objectId, MalzaharObjectId, StringComparison.Ordinal))
                            .ToArray(),
                        Hand = state.PlayerZones["P1"].Hand.Concat([MalzaharObjectId]).ToArray()
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    MalzaharObjectId,
                    new ObjectLocationState("P1", "HAND"))
            },
            "spell-duel" => state with
            {
                TimingState = TimingStates.SpellDuelOpen,
                FocusPlayerId = "P1"
            },
            _ => state
        };
    }

    private static IReadOnlyList<string> InvalidTargetIds(string caseName)
    {
        return caseName switch
        {
            "missing" => [],
            "duplicate" => [FriendlyUnitObjectId, FriendlyEquipmentObjectId],
            "self" => [MalzaharObjectId],
            "enemy" => [EnemyUnitObjectId],
            "hidden" => [FriendlyHiddenUnitObjectId],
            "unknown" => ["UNKNOWN-MALZAHAR-COST"],
            "spell" => [FriendlySpellObjectId],
            "hand" => [FriendlyHandUnitObjectId],
            _ => [FriendlyUnitObjectId]
        };
    }

    private static MatchState BuildMalzaharState()
    {
        return new MatchState(
            "room-malzahar-resource",
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        MalzaharObjectId,
                        FriendlyUnitObjectId,
                        FriendlyEquipmentObjectId,
                        FriendlySpellObjectId,
                        FriendlyHiddenUnitObjectId
                    ],
                    Hand = [FriendlyHandUnitObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [EnemyUnitObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [MalzaharObjectId] = Unit(MalzaharObjectId, P4ActivatedAbilityCatalog.MalzaharCardNo, "P1"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "UNL-101/219", "P1"),
                [FriendlyEquipmentObjectId] = Equipment(FriendlyEquipmentObjectId, "SFD-001/221", "P1"),
                [FriendlySpellObjectId] = new(
                    FriendlySpellObjectId,
                    tags: [CardObjectTags.SpellCard],
                    cardNo: "OGN-001/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [FriendlyHiddenUnitObjectId] = Unit(
                    FriendlyHiddenUnitObjectId,
                    "UNL-102/219",
                    "P1",
                    isFaceDown: true),
                [FriendlyHandUnitObjectId] = Unit(FriendlyHandUnitObjectId, "UNL-103/219", "P1"),
                [EnemyUnitObjectId] = Unit(EnemyUnitObjectId, "UNL-104/219", "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [MalzaharObjectId] = new("P1", "BASE"),
                [FriendlyUnitObjectId] = new("P1", "BASE"),
                [FriendlyEquipmentObjectId] = new("P1", "BASE"),
                [FriendlySpellObjectId] = new("P1", "BASE"),
                [FriendlyHiddenUnitObjectId] = new("P1", "BASE"),
                [FriendlyHandUnitObjectId] = new("P1", "HAND"),
                [EnemyUnitObjectId] = new("P2", "BASE")
            });
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string playerId,
        bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.UnitCard],
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId,
            isFaceDown: isFaceDown);
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
