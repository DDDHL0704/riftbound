using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
internal static partial class ActionPromptBuilder
{

    private static IReadOnlyDictionary<string, int> PendingPaymentResourcePowerByTrait(
        MatchState state,
        PendingPaymentState payment)
    {
        var powerByTrait = PendingPaymentResourceActionIds(payment)
            .Select(choiceId => TryParseRecycleRunePaymentActionId(choiceId, out var objectId)
                && state.CardObjects.TryGetValue(objectId, out var runeState)
                && TryGetRuneTrait(runeState, out var runeTrait)
                    ? runeTrait
                    : string.Empty)
            .Where(trait => !string.IsNullOrWhiteSpace(trait))
            .GroupBy(trait => trait, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count() * BasicRuneRecyclePowerGain,
                StringComparer.Ordinal);



        return powerByTrait;
    }

    private static int PendingPaymentResourceTotalPower(MatchState state, PendingPaymentState payment)
    {
        var total = 0;
        foreach (var choiceId in PendingPaymentResourceActionIds(state, payment))
        {
            if (TryParseRecycleRunePaymentActionId(choiceId, out _))
            {
                total += BasicRuneRecyclePowerGain;
                continue;
            }

        }

        return total;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PendingPaymentResourcePowerByChoice(
        MatchState state,
        PendingPaymentState payment)
    {
        return PendingPaymentResourceActionIds(state, payment)
            .ToDictionary(
                choiceId => choiceId,
                choiceId =>
                {
                    var trait = string.Empty;
                    var tap = TryParseTapRunePaymentActionId(choiceId, out _);
                    var power = tap ? 0 : BasicRuneRecyclePowerGain;
                    if (TryParseRecycleRunePaymentActionId(choiceId, out var objectId)
                        && state.CardObjects.TryGetValue(objectId, out var runeState)
                        && TryGetRuneTrait(runeState, out var runeTrait))
                    {
                        trait = runeTrait;
                    }
                    return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["trait"] = trait,
                        ["mana"] = tap ? BasicRuneTapManaGain : 0,
                        ["power"] = power,
                        ["abilityId"] = tap ? "BASIC_RUNE_EXHAUST_GAIN_1_MANA" : "BASIC_RUNE_RECYCLE_GAIN_TRAIT_POWER",
                        ["powerByTrait"] = new Dictionary<string, int>(StringComparer.Ordinal),
                        ["paymentOnly"] = false
                    };
                },
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> MergePowerByTrait(
        IReadOnlyDictionary<string, int> current,
        IReadOnlyDictionary<string, int> additional)
    {
        return current
            .Concat(additional)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => Math.Max(0, entry.Value)),
                StringComparer.Ordinal);
    }

    private static bool IsRecycleRunePaymentActionId(string choiceId)
    {
        return choiceId.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal);
    }

    private static bool TryParseRecycleRunePaymentActionId(string choiceId, out string objectId)
    {
        objectId = string.Empty;
        if (!IsRecycleRunePaymentActionId(choiceId))
        {
            return false;
        }

        objectId = choiceId[RecycleRunePaymentOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(objectId);
    }

    private static string PaymentChoiceLabel(string choiceId)
    {
        if (choiceId == "PAY") return "支付费用";
        if (choiceId == "DECLINE") return "放弃此效果";
        if (choiceId == "UNABLE_TO_PAY") return "无法支付，武装留在原处";
        const string spendManaPrefix = "SPEND_MANA:";
        const string spendPowerPrefix = "SPEND_POWER:";
        if (choiceId.StartsWith(spendManaPrefix, StringComparison.Ordinal))
        {
            return $"支付 {choiceId[spendManaPrefix.Length..]} 法力";
        }

        if (choiceId.StartsWith(spendPowerPrefix, StringComparison.Ordinal))
        {
            return $"支付 {choiceId[spendPowerPrefix.Length..]} 能量";
        }

        if (IsRecycleRunePaymentActionId(choiceId))
        {
            return "回收符文支付资源";
        }

        return choiceId;
    }

    private static IReadOnlyDictionary<string, object?> HideCardMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = HideCardSourceRequirements(state, playerId)
            .Select(HideCardSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-payable-standby-card-only",
            ["destinationPolicy"] = "source-specific-server-filtered-standby-destinations",
            ["optionalCostPolicy"] = "source-specific-server-filtered-standby-costs",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> RevealCardMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = RevealCardSourceRequirements(state, playerId)
            .Select(RevealCardSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "face-down-standby-card-only",
            ["modePolicy"] = "source-specific-server-filtered-standby-reveal-mode",
            ["destinationPolicy"] = "source-specific-server-filtered-standby-reveal-destination",
            ["optionalCostPolicy"] = "source-specific-required-standby-reveal-cost",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> ActivateAbilityMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = ActivateAbilitySourceRequirements(state, playerId)
            .Select(ActivateAbilitySourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-activated-ability-only",
            ["abilityPolicy"] = "source-specific-server-filtered-abilities",
            ["targetPolicy"] = "source-specific-server-filtered-targets",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
            ["sourceRequirements"] = sourceRequirements,
            ["manaSpendSelection"] = ManaSpendSelectionRules.Metadata
        };
    }

    private static IReadOnlyDictionary<string, object?> LegendActionMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = LegendActionSourceRequirements(state, playerId)
            .Select(LegendActionSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-legend-action-only",
            ["abilityPolicy"] = "source-specific-server-filtered-legend-abilities",
            ["targetPolicy"] = "source-specific-server-filtered-targets",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> MoveUnitMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = MoveUnitSourceRequirements(state, playerId)
            .Select(MoveUnitSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-ready-face-up-controlled-non-combat-unit",
            ["destinationPolicy"] = "source-specific-server-filtered-destinations",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
            ["supportsSimultaneousMovement"] = state.BattlefieldStates.Count > 0,
            ["powerPerExtraUnitByDestination"] = state.BattlefieldStates.Keys.ToDictionary(
                id => "BATTLEFIELD:" + id, id => StandardMovementCostRules.PowerPerExtraUnit(state, playerId, id)),
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> AssembleEquipmentMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = AssembleEquipmentSourceRequirements(state, playerId)
            .Select(AssembleEquipmentSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-unattached-controlled-base-equipment",
            ["targetPolicy"] = "source-specific-controlled-unit-hosts",
            ["optionalCostPolicy"] = "source-specific-required-assemble-costs",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> AssembleEquipmentSourceRequirementView(
        AssembleEquipmentPromptRequirement requirement)
    {
        var view = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["equipmentCardNo"] = requirement.EquipmentCardNo,
            ["displayName"] = requirement.DisplayName,
            ["targetChoices"] = requirement.TargetChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["additionalCostChoices"] = requirement.AdditionalCostChoices,
            ["requiredAdditionalCostChoiceCount"] = requirement.RequiredAdditionalCostChoiceCount,
            ["paymentResourceChoices"] = requirement.PaymentResourceChoices,
            ["paymentResourcePowerByChoice"] = requirement.PaymentResourcePowerByChoice,
            ["availablePowerByTrait"] = requirement.AvailablePowerByTrait,
            ["availablePowerByTraitWithPaymentResources"] = requirement.AvailablePowerByTraitWithPaymentResources,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["manaCost"] = requirement.ManaCost,
            ["baseManaCost"] = requirement.BaseManaCost,
            ["manaCostByTargetObjectId"] = requirement.ManaCostByTargetObjectId,
            ["targetPowerManaReductionByTargetObjectId"] = requirement.TargetPowerManaReductionByTargetObjectId,
            ["powerCost"] = requirement.PowerCost,
            ["experienceCost"] = requirement.ExperienceCost,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
        if (requirement.HasAlternativeCost)
        {
            view["requiredOptionalCostChoiceCount"] = 1;
            view["costsByChoice"] = requirement.OptionalCostChoices.ToDictionary(c => c.Id, c => (object)new Dictionary<string, object?> {
                ["mana"] = c.Id.StartsWith(CoreRuleEngine.ExhaustFriendlyUnitOptionalCostPrefix, StringComparison.Ordinal) ? 0 : requirement.ManaCost,
                ["power"] = c.Id.StartsWith(CoreRuleEngine.ExhaustFriendlyUnitOptionalCostPrefix, StringComparison.Ordinal) ? 0 : requirement.PowerCost,
                ["experience"] = c.Id.StartsWith(CoreRuleEngine.ExhaustFriendlyUnitOptionalCostPrefix, StringComparison.Ordinal) ? 0 : requirement.ExperienceCost });
        }
        return requirement.PowerReachability is { } reachable ? WithPowerCapacity(view, reachable) : view;
    }

    private static IReadOnlyDictionary<string, object?> MoveUnitSourceRequirementView(
        MoveUnitPromptRequirement requirement)
    {
        var view = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["origin"] = requirement.Origin,
            ["originLabel"] = requirement.OriginLabel,
            ["mode"] = requirement.Mode,
            ["modeLabel"] = requirement.ModeLabel,
            ["destinationChoices"] = requirement.DestinationChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
        if (string.Equals(requirement.Mode, BaronNestMoveStaticPermission, StringComparison.Ordinal))
        {
            view["movementPermission"] = BaronNestMoveStaticPermission;
            view["requiresRoamOptionalCost"] = false;
        }

        return view;
    }

    private static IReadOnlyDictionary<string, object?> PlayCardMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = PlayCardSourceRequirements(state, playerId).ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-card-behavior-only",
            ["targetPolicy"] = "source-specific-server-filtered-targets",
            ["modePolicy"] = "source-specific-server-filtered-modes",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
            ["destinationPolicy"] = "source-specific-server-filtered-destinations",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> PlayCardSourceRequirements(
        MatchState state,
        string playerId)
        => PlayCardChoices(state, playerId)
            .Select(choice => PlayCardSourceRequirement(state, playerId, choice.SourceObjectId, choice.Behavior));

    internal static IEnumerable<(string SourceObjectId, CardBehaviorDefinition Behavior)> PlayCardChoices(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.ContainsKey(playerId))
        {
            return [];
        }

        return CoreRuleEngine.EffectPlaySources(state, playerId)
            .Where(objectId => IsImplementedPlayableHandSource(state, playerId, objectId))
            .SelectMany(objectId => PlayCardPromptBehaviorsForSource(state, playerId, objectId)
                .Where(behavior => PromptHasRequiredTargetChoices(state, playerId, behavior))
                .Select(behavior => (SourceObjectId: objectId, Behavior: behavior)));
    }

    private static bool PromptBattlefieldStaticPreventsUnitPlayToBattlefield(MatchState state, string playerId, string destination) =>
        BattlefieldLocalRules.PreventsUnitPlay(state, destination);

    private static IEnumerable<string> PublicBoardObjects(MatchState state)
    {
        return state.PlayerZones.Values.SelectMany(zones => zones.Base.Concat(zones.Battlefields));
    }

    private static IEnumerable<string> PublicBattlefieldCardObjects(MatchState state)
    {
        return state.PlayerZones
            .OrderBy(entry => state.Seats.GetValueOrDefault(entry.Key), StringComparer.Ordinal)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsPromptBattlefieldCardObject(cardObject));
    }

    private static bool IsPromptBattlefieldCardObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && IsPromptBattlefieldCardObject(cardObject);
    }

    private static bool IsPromptBattlefieldCardObject(CardObjectState cardObject)
    {
        return !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && IsBattlefieldCardObject(cardObject);
    }

    private static IEnumerable<(string PlayerId, string ObjectId)> OpposingBattlefieldObjects(MatchState state, string playerId)
    {
        return state.PlayerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Battlefields.Select(objectId => (entry.Key, objectId)));
    }

    private static bool IsFaceUpBattlefieldUnitForBattle(MatchState state, string zonePlayerId, string objectId)
    {
        return state.PlayerZones.TryGetValue(zonePlayerId, out var zones)
            && zones.Battlefields.Contains(objectId, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, zonePlayerId)
            && !cardObject.IsFaceDown
            && (!cardObject.IsAttacking && !cardObject.IsDefending || CoreRuleEngine.IsEstablishedBattleDuelParticipant(state, objectId))
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
    }

    private static bool HasBattleDamageAssignmentKeyword(IReadOnlyList<string> tags)
    {
        return tags.Contains(CardCombatKeywordNames.Bulwark, StringComparer.Ordinal)
            || tags.Contains(CardCombatKeywordNames.BackRow, StringComparer.Ordinal);
    }

    private static bool HasBattleDamageAssignmentKeyword(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        return HasBattleDamageAssignmentKeyword(cardObject.Tags)
            || HasFriendlyFilteredUnitsGrantedKeyword(
                state,
                objectId,
                cardObject,
                CardCombatKeywordNames.Bulwark)
            || HasFriendlyFilteredUnitsGrantedKeyword(
                state,
                objectId,
                cardObject,
                CardCombatKeywordNames.BackRow);
    }

    private static bool HasFriendlyFilteredUnitsGrantedKeyword(
        MatchState state,
        string objectId,
        CardObjectState cardObject,
        string keyword)
    {
        if (!cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || cardObject.IsFaceDown
            || !TryFindPromptPublicFieldObjectLocation(state.PlayerZones, objectId, out var targetLocation)
            || !IsPromptPublicObjectLocationCompatible(state, objectId, targetLocation.Zone))
        {
            return false;
        }

        var controllerId = EffectivePromptPublicObjectControllerId(state.PlayerZones, objectId, cardObject);
        if (string.IsNullOrWhiteSpace(controllerId))
        {
            return false;
        }

        return PromptPublicStaticAuraSourceObjectIds(state)
            .Any(sourceObjectId =>
                state.CardObjects.TryGetValue(sourceObjectId, out var sourceState)
                && !sourceState.IsFaceDown
                && StaticAuraSpecRules.GetStaticAuras(sourceState.CardNo)
                    .Where(StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura)
                    .Where(aura => PublicFieldFriendlyPromptKeywordAuraAppliesToParticipant(
                        aura,
                        sourceObjectId,
                        objectId,
                        cardObject))
                    .Any(aura => aura.GrantedKeyword is { Length: > 0 } grantedKeyword
                        && CardCombatKeywordRules.KeywordAmount([grantedKeyword], keyword) > 0)
                && string.Equals(
                    EffectivePromptPublicObjectControllerId(state.PlayerZones, sourceObjectId, sourceState),
                    controllerId,
                    StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> PromptPublicStaticAuraSourceObjectIds(MatchState state)
    {
        return state.PlayerZones
            .SelectMany(entry => entry.Value.Base
                .Concat(entry.Value.Battlefields)
                .Concat(entry.Value.LegendZone))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string EffectivePromptPublicObjectControllerId(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        // A battlefield can be owned while uncontrolled (CN core 190.2).
        if (cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return TryFindPromptPublicStaticAuraSourceLocation(playerZones, objectId, out var location)
            ? location.PlayerId
            : string.Empty;
    }

    private static bool TryFindPromptPublicStaticAuraSourceLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BASE");
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BATTLEFIELD");
                return true;
            }

            if (zones.LegendZone.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "LEGEND");
                return true;
            }
        }

        location = default;
        return false;
    }

    private static bool TryFindPromptPublicFieldObjectLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BASE");
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BATTLEFIELD");
                return true;
            }
        }

        location = default;
        return false;
    }

    private static bool IsPromptPublicObjectLocationCompatible(
        MatchState state,
        string objectId,
        string zone)
    {
        return !state.ObjectLocations.TryGetValue(objectId, out var location)
            || string.Equals(location.Zone, zone, StringComparison.Ordinal);
    }

    private static IEnumerable<string> ControlledBattlefieldExtraStandbyObjects(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields.Where(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && BattlefieldStaticAbilitySpecRules.TryGetAbility(
                    cardObject.CardNo,
                    BattlefieldStaticAbilitySpecRules.IsBattlefieldExtraStandbyDestinationAbility,
                    out _)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            : [];
    }

    private static bool IsImplementedStandbyHideSource(MatchState state, string playerId, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior)
            && HasDelimitedTag(behavior.SourceUnitTags, CardObjectTags.Standby);
    }

    private static bool SourceObjectControlledByPlayerOrLegacyOwned(CardObjectState cardObject, string playerId)
    {
        return CardControlRules.IsControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool HasDelimitedTag(string values, string tag)
    {
        return !string.IsNullOrWhiteSpace(values)
            && values
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(tag, StringComparer.Ordinal);
    }

    private static IEnumerable<string> ControlledBoardObjects(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Base.Concat(zones.Battlefields)
            : [];
    }

    private static IEnumerable<string> EnemyBoardObjects(MatchState state, string playerId)
    {
        return state.PlayerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields));
    }

    private static IEnumerable<string> ControlledLegendActionObjects(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Base.Concat(zones.Battlefields).Concat(zones.ChampionZone)
            : [];
    }

    private static bool PlayerControlsBattlefieldStaticAbility(MatchState state, string playerId, string staticAbilityKind) =>
        BattlefieldLocalRules.ControlledBy(state, playerId).Any(card => BattlefieldStaticAbilitySpecRules.TryGetAbility(
            card.CardNo, ability => ability.Kind == staticAbilityKind, out _));

    private static bool IsControlledObjectWithTag(
        MatchState state,
        string playerId,
        string objectId,
        string tag)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && cardObject.Tags.Contains(tag, StringComparer.Ordinal)
            && TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool IsImplementedActivatedAbilitySource(
        MatchState state,
        string playerId,
        string objectId)
    {
        return ActivateAbilityRequirementsForSource(state, playerId, objectId).Count > 0;
    }

    private static bool HasImplementedLegendActionAbility(string? sourceCardNo, string abilityId)
    {
        return ImplementedLegendActionAbilities()
            .Any(ability => string.Equals(ability.AbilityId, abilityId, StringComparison.Ordinal)
                && ability.SourceCardNos.Contains(sourceCardNo, StringComparer.Ordinal));
    }

    private static bool IsBattlefieldCardObject(CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
            || StaticAuraSpecRules.HasBattlefieldKeywordStaticAura(cardObject.CardNo)
            || BattlefieldTriggerSpecRules.HasImplementedBattlefieldTrigger(cardObject.CardNo)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldDestroyedInBattlePayRecallReplacementAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantLegendAttachArmamentAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldExtraStandbyDestinationAbility,
                out _)
            || StaticAuraSpecRules.HasBattlefieldPowerStaticAura(cardObject.CardNo)
            || StaticAuraSpecRules.HasBattlefieldIsolatedDefenderKeywordModifierStaticAura(cardObject.CardNo)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldWinningScoreIncreaseAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldScoreDelayUntilTurnAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventMoveToBaseAbility,
                out _)
            || HasBattlefieldAllUnitsGrantedKeywordStaticAura(cardObject.CardNo, MoveUnitRoamKeyword)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventUnitPlayAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldEchoCostReductionAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldEquipmentCostReductionAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantUnitExperienceAbility,
                out _)
            || BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardObject.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldTargetSpellSkillDamageBonusAbility,
                out _);
    }

    private static ActionPromptChoiceDto ObjectChoice(MatchState state, string objectId, string reason)
    {
        var label = state.CardObjects.TryGetValue(objectId, out var cardObject)
            ? string.IsNullOrWhiteSpace(cardObject.CardNo) ? "服务端对象" : cardObject.CardNo
            : "服务端对象";
        return new ActionPromptChoiceDto(objectId, label, reason);
    }

    private static ActionPromptChoiceDto StackItemChoice(MatchState state, string stackItemId, string reason)
    {
        var index = state.StackItems.ToList().FindIndex(item => item.StackItemId == stackItemId);
        if (index < 0) return new(stackItemId, "结算链项目", reason);
        var item = state.StackItems[index];
        var name = CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var card) ? card.DisplayName : item.CardNo;
        var kind = StackObjectRules.IsAbility(item) ? "技能 · " : string.Empty;
        return new(stackItemId, $"第 {index + 1} 项 · {kind}{name}（{item.ControllerId}）", "越靠后的项目越先结算");
    }

    private static string PromptTargetReasonForScope(string targetScope, int targetIndex)
    {
        return $"{PromptTargetScopeLabel(targetScope)} / 第 {targetIndex + 1} 个目标";
    }

    private static string PromptTargetScopeForBehavior(CardBehaviorDefinition behavior)
    {
        return IsAgileDirectPlayAttachRepresentative(behavior)
            ? CardTargetScopes.FriendlyUnit
            : behavior.TargetScope;
    }

    private static ActionPromptChoiceDto BattlefieldDestinationChoice(MatchState state, string objectId, string reason)
    {
        var choice = ObjectChoice(state, objectId, reason);
        return new ActionPromptChoiceDto($"BATTLEFIELD:{objectId}", choice.Label, choice.Reason);
    }

    private static string LabelFor(string action)
    {
        return action switch
        {
            "READY" => "准备",
            "SUBMIT_DECK" => "提交卡组",
            "MULLIGAN" => "起手调度",
            "WAIT" => "等待",
            "PLAY_CARD" => "打出卡牌",
            "ACTIVATE_ABILITY" => "激活能力",
            "ASSEMBLE_EQUIPMENT" => "装配装备",
            "MOVE_UNIT" => "移动单位",
            "DECLARE_BATTLE" => "声明战斗",
            "HIDE_CARD" => "布置待命",
            "REVEAL_CARD" => "翻开待命",
            "TAP_RUNE" => "横置符文",
            "RECYCLE_RUNE" => "回收符文",
            "PAY_COST" => "支付费用",
            "CHOOSE_HAND_CARDS" => "选择手牌",
            "CHOOSE_CARDS" => "选择卡牌",
            "ASSIGN_COMBAT_DAMAGE" => "分配战斗伤害",
            "ASSIGN_EFFECT_DAMAGE" => "分配技能伤害",
            "ORDER_TRIGGERS" => "排序触发",
            "LEGEND_ACT" => "传奇行动",
            "PASS" => "让过",
            "PASS_PRIORITY" => "让过优先权",
            "PASS_FOCUS" => "让过焦点",
            "END_TURN" => "结束回合",
            "SURRENDER" => "投降",
            _ => IsProtocolActionToken(action) ? "服务端操作" : action
        };
    }

    private static bool IsProtocolActionToken(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return action.All(character =>
            character is '_' or '-' or ':'
            || (character >= 'A' && character <= 'Z')
            || (character >= '0' && character <= '9'));
    }

    private static string DisabledReasonFor(
        string action,
        string promptReason,
        bool hasRequiredChoices)
    {
        var actionLabel = LabelFor(action);

        if (!hasRequiredChoices)
        {
            return $"{actionLabel} 当前没有服务端可执行候选";
        }

        return string.Equals(action, "WAIT", StringComparison.Ordinal)
            ? promptReason
            : $"当前行动提示不允许执行 {actionLabel}";
    }
}
