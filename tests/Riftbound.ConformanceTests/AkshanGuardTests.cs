using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AkshanGuardTests
{
    private const string AkshanObjectId = "P1-UNIT-AKSHAN";
    private const string AkshanCardNo = "SFD·109/221";
    private const string AkshanStealPrefix = "AKSHAN_STEAL_EQUIPMENT:";
    private const string AkshanStealReason = "AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL";
    private const string VengeanceObjectId = "P1-SPELL-VENGEANCE";
    private const string EnemyWeaponObjectId = "P2-EQUIPMENT-WEAPON";
    private const string EnemyNonWeaponObjectId = "P2-EQUIPMENT-NON-WEAPON";
    private const string OrangeRuneObjectId = "P1-RUNE-ORANGE";
    private const string PayOrangePower = "orange";

    [Fact]
    public async Task AkshanPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanState();

        var played = await PlayAkshanAsync(engine, state, "P1-UNIT-AKSHAN", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-akshan-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-akshan-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-AKSHAN", "P1-FACE-DOWN-STANDBY-AKSHAN", "P1-UNIT-AKSHAN"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var unit = p2Pass.State.CardObjects["P1-UNIT-AKSHAN"];
        Assert.Equal("SFD·109/221", unit.CardNo);
        Assert.Equal("P1", unit.OwnerId);
        Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(4, unit.Power);
        Assert.Equal([CardObjectTags.UnitCard, "哨兵", "百炼"], unit.Tags);
        Assert.False(unit.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "阿克尚", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["power"]) == 4);
    }

    [Fact]
    public void AkshanPromptExposesOnlyLegalEnemyEquipmentWhenOrangeCostPayable()
    {
        var state = BuildAkshanStealState();

        var requirement = AkshanSourceRequirement(state);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);

        Assert.Equal(
            [StealCost(EnemyNonWeaponObjectId), StealCost(EnemyWeaponObjectId)],
            optionalCostChoices.Select(choice => choice.Id).ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));

        var noOrangeRequirement = AkshanSourceRequirement(BuildAkshanStealState(orangePower: 1));
        var noOrangeChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            noOrangeRequirement["optionalCostChoices"]);
        Assert.Empty(noOrangeChoices);
    }

    [Fact]
    public async Task AkshanOrangeStealWeaponPaysOrangeMovesControlsAndAttaches()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState();
        var optionalCosts = new[] { StealCost(EnemyWeaponObjectId) };

        var played = await PlayAkshanAsync(engine, state, AkshanObjectId, [], optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(PayOrangePower));
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);

        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[PayOrangePower]);

        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(AkshanObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(EnemyWeaponObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, resolved.State.PlayerZones["P2"].Base);
        var equipment = resolved.State.CardObjects[EnemyWeaponObjectId];
        Assert.Equal("P2", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal(AkshanObjectId, equipment.AttachedToObjectId);

        var controlEvent = Assert.Single(resolved.Events, IsAkshanControlChanged);
        Assert.Equal(AkshanObjectId, controlEvent.Payload["sourceObjectId"]);
        Assert.Equal(EnemyWeaponObjectId, controlEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P2", controlEvent.Payload["previousControllerId"]);
        Assert.Equal("P1", controlEvent.Payload["controllerId"]);
        Assert.Equal("P2", controlEvent.Payload["ownerId"]);
        Assert.Equal(AkshanStealReason, controlEvent.Payload["reason"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(controlEvent.Payload["optionalCosts"]));

        var attachedEvent = Assert.Single(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal));
        Assert.Equal(EnemyWeaponObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal(AkshanObjectId, attachedEvent.Payload["attachedToObjectId"]);
    }

    [Fact]
    public async Task AkshanOrangeStealCanRecycleOrangeRuneForSecondOrangePower()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState(orangePower: 1, includeOrangeRune: true);
        var optionalCosts = new[] { StealCost(EnemyWeaponObjectId), RecycleOrangeRuneCost() };

        var requirement = AkshanSourceRequirement(state);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]);

        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, StealCost(EnemyWeaponObjectId), StringComparison.Ordinal));
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, RecycleOrangeRuneCost(), StringComparison.Ordinal));

        var played = await PlayAkshanAsync(engine, state, AkshanObjectId, [], optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.DoesNotContain(OrangeRuneObjectId, played.State.PlayerZones["P1"].Base);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal([StealCost(EnemyWeaponObjectId)], stackItem.OptionalCosts);
        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([StealCost(EnemyWeaponObjectId)], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([RecycleOrangeRuneCost()], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[PayOrangePower]);

        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal("P1", resolved.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Equal(AkshanObjectId, resolved.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
    }

    [Fact]
    public async Task AkshanOrangeStealNonWeaponMovesAndControlsWithoutAttach()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState();

        var played = await PlayAkshanAsync(
            engine,
            state,
            AkshanObjectId,
            [],
            [StealCost(EnemyNonWeaponObjectId)]);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(EnemyNonWeaponObjectId, resolved.State.PlayerZones["P1"].Base);
        var equipment = resolved.State.CardObjects[EnemyNonWeaponObjectId];
        Assert.Equal("P2", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Null(equipment.AttachedToObjectId);
        Assert.Contains(resolved.Events, IsAkshanControlChanged);
        Assert.DoesNotContain(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-EQUIPMENT-FRIENDLY", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-MISSING-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-NON-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-HAND-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-FACE-DOWN-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-STALE-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-P1-CONTROLLED-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-P1-OWNED-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData(EnemyWeaponObjectId, 4, 1, 0, ErrorCodes.InsufficientCost)]
    [InlineData(EnemyWeaponObjectId, 4, 0, 2, ErrorCodes.InsufficientCost)]
    public async Task AkshanOrangeStealRejectsInvalidOrInsufficientChoicesWithoutMutation(
        string equipmentObjectId,
        int mana,
        int orangePower,
        int greenPower,
        string expectedErrorCode)
    {
        var state = BuildAkshanStealState(mana: mana, orangePower: orangePower, greenPower: greenPower);

        var result = await PlayAkshanAsync(
            new CoreRuleEngine(),
            state,
            AkshanObjectId,
            [],
            [StealCost(equipmentObjectId)]);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Contains(AkshanObjectId, result.State.PlayerZones["P1"].Hand);
        Assert.Contains(EnemyWeaponObjectId, result.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal("P2", result.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Null(result.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData("AKSHAN_STEAL_EQUIPMENT:")]
    [InlineData("AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-WEAPON", "AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-NON-WEAPON")]
    [InlineData("AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-WEAPON", "AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-WEAPON")]
    public async Task AkshanOrangeStealRejectsMalformedDuplicateOrConflictingOptionalCosts(params string[] optionalCosts)
    {
        var state = BuildAkshanStealState();

        var result = await PlayAkshanAsync(
            new CoreRuleEngine(),
            state,
            AkshanObjectId,
            [],
            optionalCosts);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Contains(AkshanObjectId, result.State.PlayerZones["P1"].Hand);
        Assert.Equal("P2", result.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Null(result.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task AkshanOrangeStealStaleEquipmentBeforeResolutionNoEffectsEquipmentSide()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState();
        var played = await PlayAkshanAsync(
            engine,
            state,
            AkshanObjectId,
            [],
            [StealCost(EnemyWeaponObjectId)]);
        var staleState = MoveEnemyWeaponToGraveyard(played.State);

        var resolved = await ResolveTopStackAsync(engine, staleState);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(AkshanObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(EnemyWeaponObjectId, resolved.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("P2", resolved.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Null(resolved.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.DoesNotContain(resolved.Events, IsAkshanControlChanged);
        Assert.DoesNotContain(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AkshanStolenEquipmentDoesNotReturnAtEndTurnWhileAkshanRemains()
    {
        var engine = new CoreRuleEngine();
        var resolved = await PlayAndResolveAkshanStealAsync(engine, BuildAkshanStealState());

        var ended = await engine.ResolveAsync(
            resolved.State,
            new PlayerIntent("intent-akshan-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(ended.Accepted, ended.ErrorMessage);
        Assert.Contains(EnemyWeaponObjectId, ended.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, ended.State.PlayerZones["P2"].Base);
        Assert.Equal("P1", ended.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Equal(AkshanObjectId, ended.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.DoesNotContain(ended.Events, IsAkshanControlReturned);
    }

    [Fact]
    public async Task AkshanLeavingFieldReturnsStolenEquipmentToOwnerBase()
    {
        var engine = new CoreRuleEngine();
        var resolved = await PlayAndResolveAkshanStealAsync(
            engine,
            BuildAkshanStealState(mana: 8, includeVengeance: true));

        var vengeancePlayed = await engine.ResolveAsync(
            resolved.State,
            new PlayerIntent("intent-akshan-vengeance-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                VengeanceObjectId,
                "OGN·229/298",
                [AkshanObjectId]),
            CancellationToken.None);
        var vengeanceResolved = await ResolveTopStackAsync(engine, vengeancePlayed.State);

        Assert.True(vengeancePlayed.Accepted, vengeancePlayed.ErrorMessage);
        Assert.True(vengeanceResolved.Accepted, vengeanceResolved.ErrorMessage);
        Assert.Contains(AkshanObjectId, vengeanceResolved.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(AkshanObjectId, vengeanceResolved.State.CardObjects.Keys);
        Assert.Contains(EnemyWeaponObjectId, vengeanceResolved.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, vengeanceResolved.State.PlayerZones["P1"].Base);
        var equipment = vengeanceResolved.State.CardObjects[EnemyWeaponObjectId];
        Assert.Equal("P2", equipment.OwnerId);
        Assert.Equal("P2", equipment.ControllerId);
        Assert.Null(equipment.AttachedToObjectId);

        var returnEvent = Assert.Single(vengeanceResolved.Events, IsAkshanControlReturned);
        Assert.Equal(AkshanObjectId, returnEvent.Payload["sourceObjectId"]);
        Assert.Equal(EnemyWeaponObjectId, returnEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1", returnEvent.Payload["previousControllerId"]);
        Assert.Equal("P2", returnEvent.Payload["controllerId"]);
        Assert.Equal(AkshanStealReason, returnEvent.Payload["reason"]);
    }

    [Theory]
    [InlineData("P1-UNIT-AKSHAN", "P1-TARGET-UNIT", 4, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P2-UNIT-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-UNIT-AKSHAN", "", 3, ErrorCodes.InsufficientCost)]
    public async Task AkshanPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildAkshanState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlayAkshanAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-AKSHAN"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-AKSHAN", "P1-FACE-DOWN-STANDBY-AKSHAN"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-UNIT-AKSHAN"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-UNIT-AKSHAN"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayAkshanAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-akshan-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·109/221",
                targetObjectIds,
                OptionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static IReadOnlyDictionary<string, object?> AkshanSourceRequirement(MatchState state)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        return Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, AkshanObjectId, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayAndResolveAkshanStealAsync(
        CoreRuleEngine engine,
        MatchState state,
        string equipmentObjectId = EnemyWeaponObjectId)
    {
        var played = await PlayAkshanAsync(
            engine,
            state,
            AkshanObjectId,
            [],
            [StealCost(equipmentObjectId)]);
        Assert.True(played.Accepted, played.ErrorMessage);
        return await ResolveTopStackAsync(engine, played.State);
    }

    private static async Task<ResolutionResult> ResolveTopStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-akshan-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-akshan-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        return p2Pass;
    }

    private static MatchState MoveEnemyWeaponToGraveyard(MatchState state)
    {
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var p2Zones = playerZones["P2"];
        playerZones["P2"] = p2Zones with
        {
            Base = p2Zones.Base
                .Where(objectId => !string.Equals(objectId, EnemyWeaponObjectId, StringComparison.Ordinal))
                .ToArray(),
            Graveyard = p2Zones.Graveyard.Contains(EnemyWeaponObjectId, StringComparer.Ordinal)
                ? p2Zones.Graveyard
                : p2Zones.Graveyard.Concat([EnemyWeaponObjectId]).ToArray()
        };

        return state with
        {
            PlayerZones = playerZones
        };
    }

    private static MatchState BuildAkshanStealState(
        int mana = 4,
        int orangePower = 2,
        int greenPower = 0,
        bool includeVengeance = false,
        bool includeOrangeRune = false)
    {
        var powerByTrait = new Dictionary<string, int>(StringComparer.Ordinal);
        if (orangePower > 0)
        {
            powerByTrait[RuneTrait.Orange] = orangePower;
        }

        if (greenPower > 0)
        {
            powerByTrait[RuneTrait.Green] = greenPower;
        }

        var p1Hand = includeVengeance
            ? new[] { AkshanObjectId, VengeanceObjectId }
            : [AkshanObjectId];
        var p1Base = includeOrangeRune
            ? new[] { "P1-EQUIPMENT-FRIENDLY", OrangeRuneObjectId }
            : ["P1-EQUIPMENT-FRIENDLY"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [AkshanObjectId] = Akshan(AkshanObjectId),
            [EnemyWeaponObjectId] = Equipment(EnemyWeaponObjectId, "P2", "P2", weapon: true),
            [EnemyNonWeaponObjectId] = Equipment(EnemyNonWeaponObjectId, "P2", "P2", weapon: false),
            ["P1-EQUIPMENT-FRIENDLY"] = Equipment("P1-EQUIPMENT-FRIENDLY", "P1", "P1", weapon: true),
            ["P2-NON-EQUIPMENT"] = new(
                "P2-NON-EQUIPMENT",
                cardNo: "SFD·125/221",
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-HAND-EQUIPMENT"] = Equipment("P2-HAND-EQUIPMENT", "P2", "P2", weapon: true),
            ["P2-FACE-DOWN-EQUIPMENT"] = Equipment("P2-FACE-DOWN-EQUIPMENT", "P2", "P2", weapon: true, isFaceDown: true),
            ["P2-STALE-EQUIPMENT"] = Equipment("P2-STALE-EQUIPMENT", "P2", "P2", weapon: true),
            ["P2-P1-CONTROLLED-EQUIPMENT"] = Equipment("P2-P1-CONTROLLED-EQUIPMENT", "P2", "P1", weapon: true),
            ["P2-P1-OWNED-EQUIPMENT"] = Equipment("P2-P1-OWNED-EQUIPMENT", "P1", "P2", weapon: true)
        };
        if (includeOrangeRune)
        {
            cardObjects[OrangeRuneObjectId] = Rune(OrangeRuneObjectId, RuneTrait.Orange);
        }

        if (includeVengeance)
        {
            cardObjects[VengeanceObjectId] = new CardObjectState(
                VengeanceObjectId,
                cardNo: "OGN·229/298",
                manaCost: 4,
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1");
        }

        return new MatchState(
            roomId: "akshan-orange-steal-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0, powerByTrait),
                ["P2"] = RunePool.Empty
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
                    Hand = ["P2-HAND-EQUIPMENT"],
                    Base =
                    [
                        EnemyWeaponObjectId,
                        EnemyNonWeaponObjectId,
                        "P2-NON-EQUIPMENT",
                        "P2-FACE-DOWN-EQUIPMENT",
                        "P2-P1-CONTROLLED-EQUIPMENT",
                        "P2-P1-OWNED-EQUIPMENT"
                    ],
                    Graveyard = ["P2-STALE-EQUIPMENT"]
                }
            },
            cardObjects: cardObjects,
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildAkshanState(int mana = 4)
    {
        return new MatchState(
            roomId: "akshan-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-AKSHAN"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-AKSHAN",
                        "P1-FACE-DOWN-STANDBY-AKSHAN"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-AKSHAN"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-AKSHAN"] = Akshan("P1-UNIT-AKSHAN"),
                ["P1-BASE-AKSHAN"] = Akshan("P1-BASE-AKSHAN"),
                ["P1-FACE-DOWN-STANDBY-AKSHAN"] = Akshan(
                    "P1-FACE-DOWN-STANDBY-AKSHAN",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-UNIT-AKSHAN"] = Akshan(
                    "P2-UNIT-AKSHAN",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-TARGET-UNIT"] = new(
                    "P1-TARGET-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static CardObjectState Akshan(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·109/221",
            isFaceDown: isFaceDown,
            manaCost: 4,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Equipment(
        string objectId,
        string ownerId,
        string controllerId,
        bool weapon,
        bool isFaceDown = false)
    {
        var tags = weapon
            ? new[] { CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon }
            : [CardObjectTags.EquipmentCard];
        return new CardObjectState(
            objectId,
            cardNo: weapon ? "SFD·186/221" : "SFD·064/221",
            isFaceDown: isFaceDown,
            tags: tags,
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static bool IsAkshanControlChanged(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "EQUIPMENT_CONTROL_CHANGED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal);
    }

    private static bool IsAkshanControlReturned(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "EQUIPMENT_CONTROL_RETURNED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal);
    }

    private static string StealCost(string equipmentObjectId)
    {
        return $"{AkshanStealPrefix}{equipmentObjectId}";
    }

    private static string RecycleOrangeRuneCost()
    {
        return $"RECYCLE_RUNE:{OrangeRuneObjectId}";
    }

    private static CardObjectState Rune(string objectId, string trait)
    {
        return new CardObjectState(
            objectId,
            cardNo: $"RUNE-{trait}",
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
            ownerId: "P1",
            controllerId: "P1");
    }
}
