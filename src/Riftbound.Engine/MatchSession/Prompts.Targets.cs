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

    private static IReadOnlyList<MoveUnitPromptRequirement> MoveUnitSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        var requirements = new List<MoveUnitPromptRequirement>();
        foreach (var objectId in zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => IsMovableUnitSource(state, playerId, objectId))
            .Distinct(StringComparer.Ordinal))
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                var battlefieldDestinations = MoveUnitBaseToBattlefieldDestinationChoices(state)
                    .ToArray();
                requirements.Add(new MoveUnitPromptRequirement(
                    objectId,
                    MoveUnitBaseZone,
                    "基地",
                    "BASE_TO_BATTLEFIELD",
                    "基地 -> 战场",
                    battlefieldDestinations.Length > 0
                        ? battlefieldDestinations
                        : [new ActionPromptChoiceDto(MoveUnitBattlefieldZone, "战场", "implemented coarse battlefield destination")],
                    [],
                    [],
                    true,
                    null));
            }

            if (!zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                continue;
            }

            if (!HasMoveUnitPromptPreventMoveToBase(state, playerId, objectId))
            {
                requirements.Add(new MoveUnitPromptRequirement(
                    objectId,
                    MoveUnitBattlefieldZone,
                    "战场",
                    "BATTLEFIELD_TO_BASE",
                    "战场 -> 基地",
                    [new ActionPromptChoiceDto(MoveUnitBaseZone, "基地", "implemented coarse base destination")],
                    [],
                    [],
                    true,
                    null));
            }

            if (TryMoveUnitPreciseBattlefieldOrigin(state, playerId, objectId, zones, out var baronOrigin, out var baronOriginLabel))
            {
                var baronNestDestinations = MoveUnitBaronNestDestinationChoices(
                    state,
                    playerId,
                    baronOrigin)
                    .ToArray();
                if (baronNestDestinations.Length > 0)
                {
                    requirements.Add(new MoveUnitPromptRequirement(
                        objectId,
                        baronOrigin,
                        baronOriginLabel,
                        BaronNestMoveStaticPermission,
                        "男爵巢穴",
                        baronNestDestinations,
                        [],
                        [],
                        true,
                        null));
                }
            }

            if (!HasMoveUnitPromptRoamPermission(state, playerId, objectId, zones)
                || !TryMoveUnitPreciseBattlefieldOrigin(state, playerId, objectId, zones, out var preciseOrigin, out var preciseOriginLabel))
            {
                continue;
            }

            var preciseDestinations = MoveUnitPreciseBattlefieldDestinationChoices(
                state,
                playerId,
                preciseOrigin)
                .ToArray();
            if (preciseDestinations.Length == 0)
            {
                continue;
            }

            requirements.Add(new MoveUnitPromptRequirement(
                objectId,
                preciseOrigin,
                preciseOriginLabel,
                "ROAM",
                "游走",
                preciseDestinations,
                [new ActionPromptChoiceDto(MoveUnitRoamOptionalCost, "游走", "required for precise battlefield movement")],
                [MoveUnitRoamOptionalCost],
                true,
                null));
        }

        return requirements;
    }

    private static bool HasMoveUnitPromptPreventMoveToBase(MatchState state, string playerId, string sourceObjectId) =>
        BattlefieldLocalRules.PreventsMoveToBase(state, sourceObjectId);

    private static bool HasMoveUnitPromptRoamPermission(
        MatchState state,
        string playerId,
        string sourceObjectId,
        PlayerZones zones)
    {
        if (!zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(sourceObjectId, out var sourceState))
        {
            return false;
        }

        return sourceState.Tags.Contains(MoveUnitRoamKeyword, StringComparer.Ordinal)
            || sourceState.UntilEndOfTurnEffects.Contains(MoveUnitRoamOptionalCost, StringComparer.Ordinal)
            || HasSourceObjectFilteredPromptKeyword(state, sourceState, MoveUnitRoamKeyword)
            || HasFriendlyFilteredUnitsGrantedKeyword(
                state,
                sourceObjectId,
                sourceState,
                MoveUnitRoamKeyword)
            || BattlefieldLocalRules.GrantsKeyword(state, sourceObjectId, MoveUnitRoamKeyword);
    }

    private static IEnumerable<ActionPromptChoiceDto> MoveUnitBaronNestDestinationChoices(
        MatchState state,
        string playerId,
        string origin)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Battlefields
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && P6TokenFactoryCatalog.IsBaronNestBattlefieldToken(cardObject.CardNo)
                && IsPromptBattlefieldCardObject(cardObject)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            .Select(objectId => new ActionPromptChoiceDto(
                $"{MoveUnitBattlefieldZone}:{objectId}",
                BattlefieldLocationLabel(state, objectId),
                "implemented Baron Nest destination-specific movement"))
            .Where(choice => !string.Equals(choice.Id, origin, StringComparison.Ordinal))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First());
    }

    private static bool HasSourceObjectFilteredPromptKeyword(
        MatchState state,
        CardObjectState sourceState,
        string keyword)
    {
        if (!sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || sourceState.IsFaceDown)
        {
            return false;
        }

        return StaticAuraSpecRules.GetStaticAuras(sourceState.CardNo)
            .Where(StaticAuraSpecRules.IsSourceObjectKeywordStaticAura)
            .Where(aura => string.IsNullOrWhiteSpace(aura.TargetFilter)
                || StaticAuraSpecRules.TargetMatchesFilter(aura, sourceState))
            .Where(aura => !aura.RequiredPlayerExperience.HasValue
                || (!string.IsNullOrWhiteSpace(sourceState.ControllerId)
                    && state.PlayerExperience.TryGetValue(sourceState.ControllerId, out var experience)
                    && experience >= aura.RequiredPlayerExperience.Value))
            .Any(aura => aura.GrantedKeyword is { Length: > 0 } grantedKeyword
                && CardCombatKeywordRules.KeywordAmount([grantedKeyword], keyword) > 0);
    }

    private static bool HasBattlefieldAllUnitsGrantedKeywordStaticAura(string? cardNo, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return StaticAuraSpecRules.GetStaticAuras(cardNo)
            .Where(StaticAuraSpecRules.IsBattlefieldAllUnitsKeywordStaticAura)
            .Any(aura => aura.GrantedKeyword is { Length: > 0 } grantedKeyword
                && CardCombatKeywordRules.KeywordAmount([grantedKeyword], keyword) > 0);
    }

    private static bool TryMoveUnitPreciseBattlefieldOrigin(
        MatchState state,
        string playerId,
        string sourceObjectId,
        PlayerZones zones,
        out string origin,
        out string originLabel)
    {
        origin = string.Empty;
        originLabel = string.Empty;

        if (state.ObjectLocations.TryGetValue(sourceObjectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal)
            && string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            origin = $"{MoveUnitBattlefieldZone}:{location.BattlefieldObjectId}";
            originLabel = BattlefieldLocationLabel(state, location.BattlefieldObjectId);
            return true;
        }

        var battlefieldObjectId = zones.Battlefields.FirstOrDefault(objectId =>
            !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && IsPromptBattlefieldCardObject(cardObject));
        if (string.IsNullOrWhiteSpace(battlefieldObjectId))
        {
            return false;
        }

        origin = $"{MoveUnitBattlefieldZone}:{battlefieldObjectId}";
        originLabel = BattlefieldLocationLabel(state, battlefieldObjectId);
        return true;
    }

    private static IEnumerable<ActionPromptChoiceDto> MoveUnitPreciseBattlefieldDestinationChoices(
        MatchState state,
        string playerId,
        string origin)
    {
        IEnumerable<ActionPromptChoiceDto> choices = state.BattlefieldStates.Keys
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    && IsPromptBattlefieldCardObject(cardObject))
                .Select(objectId => new ActionPromptChoiceDto(
                    $"{MoveUnitBattlefieldZone}:{objectId}",
                    BattlefieldLocationLabel(state, objectId),
                    "implemented precise battlefield roam destination"));

        return choices
            .Append(new ActionPromptChoiceDto(
                $"{MoveUnitBattlefieldZone}:{playerId}-MAIN",
                "己方主战场",
                "implemented precise battlefield roam destination"))
            .Where(choice => !string.Equals(choice.Id, origin, StringComparison.Ordinal))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First());
    }

    private static IEnumerable<ActionPromptChoiceDto> MoveUnitBaseToBattlefieldDestinationChoices(MatchState state)
    {
        return state.BattlefieldStates.Values
            .OrderBy(battlefield => battlefield.BattlefieldObjectId, StringComparer.Ordinal)
            .Where(battlefield => state.CardObjects.TryGetValue(battlefield.BattlefieldObjectId, out var cardObject)
                && IsPromptBattlefieldCardObject(cardObject)
                && !string.IsNullOrWhiteSpace(cardObject.CardNo))
            .Select(battlefield => BattlefieldDestinationChoice(
                state,
                battlefield.BattlefieldObjectId,
                "服务端合法战场目的地"))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First());
    }

    private static string BattlefieldLocationLabel(MatchState state, string? battlefieldObjectId)
    {
        if (string.IsNullOrWhiteSpace(battlefieldObjectId))
        {
            return "战场";
        }

        return state.CardObjects.TryGetValue(battlefieldObjectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            ? $"{cardObject.CardNo} / {battlefieldObjectId}"
            : battlefieldObjectId;
    }

    private static bool IsTapRuneSource(MatchState state, string playerId, string objectId)
    {
        return state.RunePools.GetValueOrDefault(playerId, RunePool.Empty) is { HasSupportedAmounts: true } pool
            && (long)pool.TotalStoredMana < int.MaxValue
            && IsObjectInPlayerZone(state, playerId, objectId, "BASE")
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown
            && !cardObject.IsExhausted;
    }

    private static bool IsRecycleRuneSource(MatchState state, string playerId, string objectId)
    {
        return state.RunePools.GetValueOrDefault(playerId, RunePool.Empty) is { HasSupportedAmounts: true } pool
            && pool.TotalStoredPower < int.MaxValue
            && IsObjectInPlayerZone(state, playerId, objectId, "BASE")
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && cardObject.Tags.Any(tag => tag.StartsWith("COLOR:", StringComparison.OrdinalIgnoreCase))
            && !cardObject.IsFaceDown;
    }

    private static IReadOnlyList<ActivateAbilityPromptRequirement> ActivateAbilitySourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Concat(zones.LegendZone)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(objectId => ActivateAbilityRequirementsForSource(state, playerId, objectId))
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> ActivateAbilityHoneyfruitLevelSixChoices(
        MatchState state,
        string playerId,
        string sourceObjectId,
        P4ActivatedAbilityDefinition ability)
    {
        if (!P4ActivatedAbilityCatalog.IsHoneyfruitResourceAbility(ability.AbilityId)
            || !state.PlayerExperience.TryGetValue(playerId, out var experience)
            || experience < P4ActivatedAbilityCatalog.HoneyfruitLevelSixExperience
            || !CoreRuleEngine.ResourceAbilityAmountsSupported(state, playerId, ability,
                [P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix + sourceObjectId]))
        {
            return [];
        }

        return
        [
            new ActionPromptChoiceDto(
                $"{P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix}{sourceObjectId}",
                "6 级强化",
                "Honeyfruit level-six branch: gain 1 mana plus 1 wildcard power")
        ];
    }

    private static bool ActivateAbilitySourceMatches(
        P4ActivatedAbilityDefinition ability,
        PlayerZones zones,
        string sourceObjectId,
        CardObjectState cardObject)
    {
        if (ability.SourceScope == ActivatedAbilitySourceScopes.FriendlyLegend)
            return zones.LegendZone.Contains(sourceObjectId, StringComparer.Ordinal)
                && !cardObject.IsFaceDown;
        if (!string.IsNullOrWhiteSpace(ability.SourceRequiredTag))
            return cardObject.Tags.Contains(ability.SourceRequiredTag, StringComparer.Ordinal)
                && !cardObject.IsFaceDown;
        if (ability.RequiresBaseEquipmentSource)
        {
            return (zones.Base.Contains(sourceObjectId, StringComparer.Ordinal) || zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal))
                && cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                && !cardObject.IsFaceDown;
        }

        return cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> ActivateAbilityConversionOptionalCostChoices(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        if (!ResourceQuantityRules.TryConversionMaximum(runePool, ability.AbilityId, out var conversionMaximum, state)) return [];
        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, StringComparison.Ordinal))
        {
            return ResourceConversionChoices(
                P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix,
                conversionMaximum,
                "转换法力为费用符能");
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, StringComparison.Ordinal))
        {
            return ResourceConversionChoices(
                P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix,
                conversionMaximum,
                "转换任意特性符能为法力");
        }

        return [];
    }

    private static IReadOnlyList<ActionPromptChoiceDto> ResourceConversionChoices(
        string prefix,
        int maxAmount,
        string labelPrefix)
    {
        if ((long)maxAmount + 1 > ResourceQuantityRules.DirectChoiceLimit)
            return [new(prefix + ResourceQuantityRules.ChooseAmountSuffix, $"{labelPrefix}：选择 0–{maxAmount}", "server-authorized resource conversion amount")];
        return Enumerable.Range(0, maxAmount + 1)
            .Select(amount => new ActionPromptChoiceDto(
                $"{prefix}{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"{labelPrefix}：{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                "server-authorized resource conversion amount"))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> ActivateAbilitySpellshieldTaxPowerByTargetObjectId(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> targetChoicesByIndex)
    {
        if (!ability.AppliesSpellshieldTargetTax || targetChoicesByIndex.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return targetChoicesByIndex.Values
            .SelectMany(choices => choices)
            .Select(choice => choice.Id)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                objectId => objectId,
                objectId => SpellshieldTaxPowerForTarget(state, playerId, objectId),
                StringComparer.Ordinal);
    }

    private static IEnumerable<ActionPromptChoiceDto> LegendActionArmamentTargetChoices(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability,
        string? selectedUnitObjectId)
    {
        return ControlledLegendActionObjects(state, playerId)
            .Where(objectId => LegendActionIsValidArmamentSecondTarget(state, playerId, selectedUnitObjectId, objectId, ability))
            .Select(objectId => ObjectChoice(state, objectId, "受控武装目标"));
    }

    private static IEnumerable<ActionPromptChoiceDto> LegendActionOwnedTeemoTargetChoices(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Concat(zones.ChampionZone)
            .Where(objectId => LegendActionIsValidOwnedTeemoUnitTarget(state, playerId, objectId))
            .Select(objectId => ObjectChoice(state, objectId, "己方提莫单位"));
    }

    private static IEnumerable<ActionPromptChoiceDto> LegendActionPendingFriendlyUnitTargetChoices(
        MatchState state,
        string playerId)
    {
        return ControlledLegendActionObjects(state, playerId)
            .Where(objectId => LegendActionIsPendingFriendlyUnitTarget(state, playerId, objectId))
            .Select(objectId => ObjectChoice(state, objectId, "被当前结算链项目指定的友方单位"));
    }

    private static bool LegendActionIsValidFriendlyUnitTarget(
        MatchState state,
        string playerId,
        string targetObjectId,
        LegendActionAbilityDefinition ability)
    {
        if (!LegendActionIsControlledFieldObjectWithTag(state, playerId, targetObjectId, CardObjectTags.UnitCard))
        {
            return false;
        }

        if (!state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || string.IsNullOrWhiteSpace(targetState.CardNo)
            || (ability.RequiresArmamentSecondTarget || CoreRuleEngine.IsLegendUnitKeywordEffect(ability.EffectKind)) && targetState.IsFaceDown)
        {
            return false;
        }

        if (CoreRuleEngine.IsLegendDuelMovementEffect(ability.EffectKind)
            && !CoreRuleEngine.IsLegendDuelUnit(state, playerId, targetObjectId)) return false;
        if (ability.RequiresBattlefieldTarget
            && (!TryFindLegendActionFieldObjectLocation(state.PlayerZones, targetObjectId, out var location)
                || !string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)))
        {
            return false;
        }

        return !ability.RequiresExhaustedTarget
            || targetState.IsExhausted;
    }

    private static bool LegendActionIsValidArmamentSecondTarget(
        MatchState state,
        string playerId,
        string? unitObjectId,
        string equipmentObjectId,
        LegendActionAbilityDefinition ability)
    {
        if (string.IsNullOrWhiteSpace(unitObjectId)
            || string.Equals(unitObjectId, equipmentObjectId, StringComparison.Ordinal)
            || !LegendActionIsControlledFieldObjectWithTag(state, playerId, equipmentObjectId, CardObjectTags.EquipmentCard)
            || !state.CardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
            || string.IsNullOrWhiteSpace(equipmentState.CardNo)
            || equipmentState.IsFaceDown
            || !equipmentState.Tags.Contains("武装", StringComparer.Ordinal))
        {
            return false;
        }

        var isAttached = !string.IsNullOrWhiteSpace(equipmentState.AttachedToObjectId);
        if (ability.RequiresUnattachedArmamentSecondTarget && isAttached)
        {
            return false;
        }

        if (ability.RequiresAttachedArmamentSecondTarget && !isAttached)
        {
            return false;
        }

        return true;
    }

    private static bool LegendActionIsValidOwnedTeemoUnitTarget(MatchState state, string playerId, string targetObjectId)
    {
        return state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !targetState.IsFaceDown
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && string.Equals(targetState.OwnerId, playerId, StringComparison.Ordinal)
            && UnitIdentityCatalog.IsSourceCardNoForIdentity(
                UnitIdentityCatalog.TeemoUnitIdentityId,
                targetState.CardNo);
    }

    private static bool LegendActionIsPendingFriendlyUnitTarget(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return LegendActionIsControlledFieldObjectWithTag(state, playerId, targetObjectId, CardObjectTags.UnitCard)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !string.IsNullOrWhiteSpace(targetState.CardNo)
            && state.StackItems.Any(stackItem =>
                string.Equals(stackItem.ControllerId, playerId, StringComparison.Ordinal)
                && stackItem.TargetObjectIds.Contains(targetObjectId, StringComparer.Ordinal));
    }

    private static bool LegendActionIsControlledFieldObjectWithTag(
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

    private static bool TryFindLegendActionFieldObjectLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, MoveUnitBaseZone);
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, MoveUnitBattlefieldZone);
                return true;
            }
        }

        location = default;
        return false;
    }

    private static bool LegendActionSourceHasAbility(
        MatchState state,
        string playerId,
        string? sourceCardNo,
        LegendActionAbilityDefinition ability)
    {
        return ability.SourceCardNos.Contains(sourceCardNo, StringComparer.Ordinal)
            || (!string.IsNullOrWhiteSpace(ability.RequiredControlledBattlefieldStaticAbilityKind)
                && PlayerControlsBattlefieldStaticAbility(
                    state,
                    playerId,
                    ability.RequiredControlledBattlefieldStaticAbilityKind));
    }

    private static bool LegendActionTimingAllowed(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        if (ability.TimingKind == LegendActionTimingKinds.ResourceAction)
            return ResourceActionWindow.CanAct(state, playerId);
        if (ability.TimingKind == LegendActionTimingKinds.QuickOpen)
            return CoreRuleEngine.CanActivateSwiftAbility(state, playerId);

        if (state.PendingPayment is not null || state.PendingEffectPlay is not null || state.PendingCardChoice is not null)
            return false;

        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal))
        {
            return false;
        }

        return ability.TimingKind switch
        {
            LegendActionTimingKinds.MainOpen =>
                string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                && string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
                && state.StackItems.Count == 0,
            LegendActionTimingKinds.PriorityWindow =>
                state.StackItems.Count > 0
                && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
                && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal),
            LegendActionTimingKinds.SpellDuelFocus =>
                state.StackItems.Count == 0
                && string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(state.FocusPlayerId)
                && string.Equals(state.FocusPlayerId, playerId, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool LegendActionPrerequisitesMet(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        var pool = state.RunePools.GetValueOrDefault(playerId, RunePool.Empty);
        if (ability.ManaGainAmount > 0 && !CoreRuleEngine.CanGainSpellOnlyMana(pool, ability.ManaGainAmount)) return false;
        if (ability.PowerGainAmount > 0 && (!pool.HasSupportedAmounts
            || (long)pool.TotalStoredPower + ability.PowerGainAmount > int.MaxValue)) return false;
        if (ability.RequiresPlayedAnotherCardThisTurn
            && (!state.PlayerCardsPlayedThisTurn.TryGetValue(playerId, out var playedCount) || playedCount <= 0))
        {
            return false;
        }

        if (ability.RequiresPlayedArmamentThisTurn
            && !state.UntilEndOfTurnEffects.Contains($"{PlayedArmamentThisTurnEffectPrefix}{playerId}", StringComparer.Ordinal))
        {
            return false;
        }

        return !ability.RequiresEzrealEnemyTargetsThisTurn
            || LegendActionEzrealEnemyTargetsThisTurnCount(state.UntilEndOfTurnEffects, playerId) >= EzrealEnemyTargetThreshold;
    }

    private static int LegendActionEzrealEnemyTargetsThisTurnCount(IReadOnlyList<string> effectIds, string playerId)
    {
        var prefix = $"{EzrealEnemyTargetsThisTurnPrefix}{playerId}:";
        return effectIds
            .Where(effectId => effectId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(effectId => effectId[prefix.Length..])
            .Select(value => int.TryParse(value, out var count) ? count : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int ResolveLegendActionManaCost(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        var reduction = string.Equals(
            ability.ManaCostReductionKind,
            LegendActionManaCostReductionKinds.FriendlyEphemeralFieldObjects,
            StringComparison.Ordinal)
                ? CoreRuleEngine.CountFriendlyEphemeralFieldUnits(state, playerId)
                : 0;
        return Math.Max(0, ability.ManaCost - reduction);
    }

    private static IReadOnlyList<string> LegendActionRequiredCostTokens(int manaCost, LegendActionAbilityDefinition ability)
    {
        var requiredCostToken = string.IsNullOrWhiteSpace(ability.RequiredCostToken)
            ? manaCost > 0 ? $"SPEND_MANA:{manaCost}" : string.Empty
            : ability.RequiredCostToken;
        return string.IsNullOrWhiteSpace(requiredCostToken) ? [] : [requiredCostToken];
    }

    private static bool CanPayLegendActionRequirement(
        MatchState state,
        string playerId,
        LegendActionPromptRequirement requirement)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        if (AbilityAvailableManaWithRuneTaps(state, playerId, runePool, ManaPaymentPurpose.Other) < requirement.ManaCost)
        {
            return false;
        }
        var resources = requirement.PaymentResourcePowerByChoice ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>();
        if (!new RunePaymentReachability(PaymentCostRules.PowerPoolForPurpose(runePool, PowerPaymentPurpose.Other),
                CandidatePowerByTrait(resources)).CanPay(requirement.PowerCost, new Dictionary<string, int>())) return false;

        var experience = state.PlayerExperience.TryGetValue(playerId, out var currentExperience)
            ? currentExperience
            : 0;
        return requirement.ExperienceCost <= 0 || experience >= requirement.ExperienceCost;
    }

    private static string LegendActionCostLabel(string cost)
    {
        if (cost.StartsWith("SPEND_MANA:", StringComparison.Ordinal)
            && int.TryParse(cost["SPEND_MANA:".Length..], out var mana))
        {
            return $"支付 {mana} 法力";
        }

        if (cost.StartsWith("SPEND_EXPERIENCE:", StringComparison.Ordinal)
            && int.TryParse(cost["SPEND_EXPERIENCE:".Length..], out var experience))
        {
            return $"支付 {experience} 经验";
        }

        return cost;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> ActivateAbilityTargetChoicesByIndex(
        MatchState state,
        string playerId,
        string sourceObjectId,
        P4ActivatedAbilityDefinition ability)
    {
        if (ability.RequiredTargetCount == 0)
        {
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        }

        if (ability.SimpleEffect is not null)
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal) {
                ["0"] = ability.SimpleEffect.TransfersSourceToChosenPlayer
                    ? state.Seats.Keys.Order(StringComparer.Ordinal).Select(id => new ActionPromptChoiceDto(id, state.Seats[id], "获得来源控制权并召回")).ToArray()
                    : PublicBoardObjects(state)
                    .Where(id => CoreRuleEngine.IsSimpleAbilityTarget(state, playerId, sourceObjectId, id, ability))
                    .Where(id => CanPayXerathTargetCost(state, playerId, ability, id))
                    .Select(id => ObjectChoice(state, id, ability.SimpleEffect.BattlefieldUnitDamage > 0 ? "战场单位目标" : "装备目标")).ToArray() };

        if (ability.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn)
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal) {
                ["0"] = PublicBoardObjects(state)
                    .Where(id => CoreRuleEngine.IsPowerAbilityTarget(state, playerId, id))
                    .Where(id => CanPayXerathTargetCost(state, playerId, ability, id))
                    .Select(id => ObjectChoice(state, id, "unit target")).ToArray() };

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.XerathDamageAbilityId, StringComparison.Ordinal))
        {
            var choices = PublicBoardObjects(state)
                .Where(objectId => IsPromptFieldUnitObject(state, objectId))
                .Where(objectId => TargetProtectionRules.IsLegalActivatedSkillTarget(state, playerId, objectId))
                .Where(objectId => CanPayXerathTargetCost(state, playerId, ability, objectId))
                .Select(objectId => ObjectChoice(
                    state,
                    objectId,
                    IsPromptEnemyFieldObject(state, playerId, objectId)
                        && SpellshieldTaxPowerForTarget(state, playerId, objectId) > 0
                            ? "unit target with spellshield tax"
                            : "unit target"))
                .ToArray();
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, StringComparison.Ordinal))
        {
            var choices = PublicBoardObjects(state)
                .Where(objectId => IsPromptFieldUnitObject(state, objectId))
                .Where(objectId => IsPromptReadyTargetObject(state, objectId))
                .Where(objectId => TargetProtectionRules.IsLegalActivatedSkillTarget(state, playerId, objectId))
                .Where(objectId => CanPayCrimsonRoseTargetCost(state, playerId, ability, objectId))
                .Select(objectId => ObjectChoice(
                    state,
                    objectId,
                    IsPromptEnemyFieldObject(state, playerId, objectId)
                        && SpellshieldTaxPowerForTarget(state, playerId, objectId) > 0
                            ? "unit target with spellshield tax"
                            : "unit target"))
                .ToArray();
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal))
        {
            var choices = PublicBoardObjects(state)
                .Where(objectId => IsPromptShadowStunTarget(state, playerId, sourceObjectId, objectId))
                .Where(objectId => CanPayShadowTargetCost(state, playerId, sourceObjectId, ability, objectId))
                .Select(objectId => ObjectChoice(
                    state,
                    objectId,
                    SpellshieldTaxPowerForTarget(state, playerId, objectId) > 0
                        ? "enemy attacking unit at this battlefield with spellshield tax"
                        : "enemy attacking unit at this battlefield"))
                .ToArray();
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, StringComparison.Ordinal))
        {
            var choices = state.PlayerZones.TryGetValue(playerId, out var zones)
                ? zones.Base
                    .Concat(zones.Battlefields)
                    .Distinct(StringComparer.Ordinal)
                    .Where(objectId => IsPromptAzirSwiftSwapTarget(state, playerId, sourceObjectId, objectId))
                    .Select(objectId => ObjectChoice(state, objectId, "controlled face-up unit swap target"))
                    .ToArray()
                : [];
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId, StringComparison.Ordinal))
        {
            var choices = state.PlayerZones
                .SelectMany(entry => entry.Value.Battlefields)
                .Distinct(StringComparer.Ordinal)
                .Where(objectId => IsPromptGatekeeperMaduliMoveTarget(state, playerId, sourceObjectId, objectId))
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "enemy-controlled battlefield with lower enemy unit power"))
                .ToArray();
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        if (string.Equals(
                ability.Kind,
                ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest,
                StringComparison.Ordinal))
        {
            var choices = state.PlayerZones.TryGetValue(playerId, out var zones)
                ? zones.Base
                    .Concat(zones.Battlefields)
                    .Distinct(StringComparer.Ordinal)
                    .Where(objectId => IsPromptDestroyFriendlyUnitActivatedAbilityTarget(
                        state,
                        playerId,
                        sourceObjectId,
                        objectId))
                    .Select(objectId => ObjectChoice(state, objectId, "friendly unit target"))
                    .ToArray()
                : [];
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, StringComparison.Ordinal))
        {
            var choices = state.PlayerZones.TryGetValue(playerId, out var zones)
                ? zones.Base
                    .Concat(zones.Battlefields)
                    .Distinct(StringComparer.Ordinal)
                    .Where(objectId => IsPromptMalzaharDestroyCostTarget(state, playerId, sourceObjectId, objectId))
                    .Select(objectId => ObjectChoice(state, objectId, "resource skill cost: destroy friendly unit or equipment"))
                    .ToArray()
                : [];
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
    }

    private static bool CanPayCrimsonRoseTargetCost(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability,
        string targetObjectId)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var experience = state.PlayerExperience.TryGetValue(playerId, out var currentExperience)
            ? currentExperience
            : 0;
        return experience >= ability.ExperienceCost
            && ActivateAbilityAvailablePowerWithPaymentResources(state, playerId, ability) >= SpellshieldTaxPowerForTarget(state, playerId, targetObjectId);
    }

    private static bool IsPromptReadyTargetObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var targetState)
            && !CardStaticAbilitySpecRules.CardCannotBecomeActive(targetState.CardNo);
    }

    private static bool CanPayShadowTargetCost(
        MatchState state,
        string playerId,
        string sourceObjectId,
        P4ActivatedAbilityDefinition ability,
        string targetObjectId)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        return AbilityAvailableManaWithRuneTaps(state, playerId, runePool, CoreRuleEngine.ManaPurposeForActivatedAbility(state, sourceObjectId)) >= ability.ManaCost
            && ActivateAbilityAvailablePowerWithPaymentResources(state, playerId, ability) >= ability.PowerCost + SpellshieldTaxPowerForTarget(state, playerId, targetObjectId);
    }

    private static bool IsPromptShadowStunTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        string targetObjectId)
    {
        return IsPromptEnemyFieldObject(state, playerId, targetObjectId)
            && IsPromptFieldUnitObject(state, targetObjectId)
            && IsPromptAttackingBattlefieldObject(state, targetObjectId)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, playerId, targetObjectId)
            && SamePromptBattlefield(state, sourceObjectId, targetObjectId);
    }

    private static bool IsPromptAzirSwiftSwapTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        string targetObjectId)
    {
        return !string.Equals(sourceObjectId, targetObjectId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && string.Equals(targetState.ControllerId, playerId, StringComparison.Ordinal)
            && !targetState.IsFaceDown
            && !string.IsNullOrWhiteSpace(targetState.CardNo)
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !targetState.IsFaceDown
            && !targetState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !targetState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && (zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> AzirArmamentReattachChoicesByTargetObjectId(
        MatchState state,
        string playerId,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> targetChoicesByIndex)
    {
        if (!targetChoicesByIndex.TryGetValue("0", out var targetChoices))
        {
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        }

        return targetChoices.ToDictionary(
            targetChoice => targetChoice.Id,
            targetChoice => AzirArmamentReattachChoicesForTarget(state, playerId, targetChoice.Id),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<ActionPromptChoiceDto> AzirArmamentReattachChoicesForTarget(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return state.CardObjects
            .Where(entry => IsPromptAzirArmamentReattachChoice(state, playerId, targetObjectId, entry.Key))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry =>
            {
                var choice = ObjectChoice(state, entry.Key, "确认武装目标，移动完成后再决定是否改贴");
                return new ActionPromptChoiceDto(
                    entry.Key,
                    choice.Label,
                    choice.Reason);
            })
            .ToArray();
    }

    private static bool IsPromptAzirArmamentReattachChoice(
        MatchState state,
        string playerId,
        string targetObjectId,
        string equipmentObjectId)
    {
        return state.CardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
            && equipmentState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !equipmentState.IsFaceDown
            && OfficialArmamentCatalog.Find(equipmentState.CardNo) is not null
            && state.PlayerZones.Values.Any(z => z.Base.Contains(equipmentObjectId) || z.Battlefields.Contains(equipmentObjectId))
            && string.Equals(equipmentState.AttachedToObjectId, targetObjectId, StringComparison.Ordinal);
    }

    private static bool IsPromptGatekeeperMaduliMoveTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        string targetBattlefieldObjectId)
    {
        if (!P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId,
            out var ability))
        {
            return false;
        }

        if (!state.CardObjects.TryGetValue(sourceObjectId, out var sourceState)
            || !sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || sourceState.IsFaceDown
            || !P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)
            || !string.Equals(sourceState.ControllerId, playerId, StringComparison.Ordinal)
            || !state.PlayerZones.TryGetValue(playerId, out var sourceZones)
            || (!sourceZones.Base.Contains(sourceObjectId, StringComparer.Ordinal)
                && !sourceZones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal))
            || IsPromptAlreadyAtBattlefield(state, sourceObjectId, targetBattlefieldObjectId)
            || !TryGetPromptEnemyControlledBattlefield(
                state,
                playerId,
                targetBattlefieldObjectId,
                out var targetControllerId))
        {
            return false;
        }

        return Math.Max(0, sourceState.Power) > PromptEnemyUnitPowerSumAtBattlefield(
            state,
            targetBattlefieldObjectId,
            targetControllerId);
    }

    private static bool TryGetPromptEnemyControlledBattlefield(
        MatchState state,
        string playerId,
        string targetBattlefieldObjectId,
        out string targetControllerId)
    {
        targetControllerId = string.Empty;
        if (!state.CardObjects.TryGetValue(targetBattlefieldObjectId, out var targetState)
            || targetState.IsFaceDown
            || string.IsNullOrWhiteSpace(targetState.CardNo)
            || !targetState.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(targetState.ControllerId)
            || string.Equals(targetState.ControllerId, playerId, StringComparison.Ordinal))
        {
            return false;
        }

        var targetLocation = state.PlayerZones
            .Where(entry => entry.Value.Battlefields.Contains(targetBattlefieldObjectId, StringComparer.Ordinal))
            .Select(entry => (PlayerId: entry.Key, Zone: "BATTLEFIELD"))
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetLocation.PlayerId)
            || !string.Equals(targetLocation.PlayerId, targetState.ControllerId, StringComparison.Ordinal))
        {
            return false;
        }

        targetControllerId = targetState.ControllerId;
        return true;
    }

    private static bool IsPromptAlreadyAtBattlefield(
        MatchState state,
        string sourceObjectId,
        string battlefieldObjectId)
    {
        return state.ObjectLocations.TryGetValue(sourceObjectId, out var location)
            && string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal);
    }

    private static int PromptEnemyUnitPowerSumAtBattlefield(
        MatchState state,
        string battlefieldObjectId,
        string targetControllerId)
    {
        return state.ObjectLocations
            .Where(entry => string.Equals(entry.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Where(objectId => !string.Equals(objectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !cardObject.IsFaceDown
                && string.Equals(cardObject.ControllerId, targetControllerId, StringComparison.Ordinal))
            .Sum(objectId => Math.Max(0, state.CardObjects[objectId].Power));
    }

    private static bool SamePromptBattlefield(MatchState state, string firstObjectId, string secondObjectId)
    {
        return state.ObjectLocations.TryGetValue(firstObjectId, out var firstLocation)
            && state.ObjectLocations.TryGetValue(secondObjectId, out var secondLocation)
            && string.Equals(firstLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(secondLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(firstLocation.BattlefieldObjectId)
            && string.Equals(firstLocation.BattlefieldObjectId, secondLocation.BattlefieldObjectId, StringComparison.Ordinal);
    }

    private static bool IsPromptMalzaharDestroyCostTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.ControllerId == playerId
            && !cardObject.IsFaceDown
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && !cardObject.IsFaceDown
            && (cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal))
            && CoreRuleEngine.CanStartMalzaharResourceCost(state, playerId, sourceObjectId, objectId);
    }

    private static bool IsPromptDestroyFriendlyUnitActivatedAbilityTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        string objectId)
    {
        return !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && !cardObject.IsFaceDown
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown
            && !cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !cardObject.Tags.Contains(CardObjectTags.SpellCard, StringComparer.Ordinal)
            && !cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && (zones.Base.Contains(objectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool CanPayXerathTargetCost(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability,
        string targetObjectId)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        return ActivateAbilityAvailablePowerWithPaymentResources(state, playerId, ability) >= ability.PowerCost + SpellshieldTaxPowerForTarget(state, playerId, targetObjectId);
    }

    private static IReadOnlyList<ActionPromptChoiceDto> ActivateAbilityPaymentResourceChoices(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability)
    {
        var powerCostByTrait = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);
        var acceptsLegend = SupportsInlineRenektonAbilityPayment(ability) && (ability.ManaCost > 0 || ability.PowerCost > 0);
        var legendChoices = acceptsLegend ? PromptRenektonPaymentChoices(state, playerId) : [];
        var hasLegend = legendChoices.Count > 0;
        var runes = ability.PowerCost > 0 || powerCostByTrait.Count > 0 || ability.AppliesSpellshieldTargetTax
            || ability.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn
            || hasLegend
                ? (hasLegend ? RenektonAwareRecycleRuneChoices(state, playerId,
                    "payment resource action: recycle rune while activating ability")
                : RecycleRunePaymentResourceChoices(state, playerId,
                    "payment resource action: recycle rune while activating ability")) : [];
        var legends = legendChoices.Select(choice => new ActionPromptChoiceDto(choice.Id, choice.Label,
            "传奇支付资源行动；其符能费用与技能费用分别核对"));
        return runes.Concat(legends).ToArray();
    }

    private static bool SupportsInlineRenektonAbilityPayment(P4ActivatedAbilityDefinition ability)
        => ability.AbilityId is P4ActivatedAbilityCatalog.ViDoublePowerAbilityId
            or P4ActivatedAbilityCatalog.NextSpellEchoAbilityId or P4ActivatedAbilityCatalog.ScryingBlossomAbilityId
            or P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId or P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId;

    private static IReadOnlyList<ActionPromptChoiceDto> RecycleRunePaymentResourceChoices(
        MatchState state, string playerId, string reason)
    {
        return CoreRuleEngine.RecycleRunePaymentResourceActionIds(state, playerId)
            .Select(actionId =>
            {
                var objectId = actionId[RecycleRunePaymentOptionalCostPrefix.Length..];
                var choice = ObjectChoice(state, objectId, reason);
                return new ActionPromptChoiceDto(actionId, $"回收符文支付：{choice.Label}", choice.Reason);
            })
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> RenektonAwareRecycleRuneChoices(
        MatchState state, string playerId, string reason)
    {
        var current = RecycleRunePaymentResourceChoices(state, playerId, reason);
        if (PromptRenektonPaymentChoices(state, playerId).Count == 0) return current;
        return current.Concat(CoreRuleEngine.RenektonRecyclableRuneActionIds(state, playerId).Select(actionId =>
        {
            var objectId = actionId[RecycleRunePaymentOptionalCostPrefix.Length..];
            var choice = ObjectChoice(state, objectId, reason);
            return new ActionPromptChoiceDto(actionId, $"回收符文支付：{choice.Label}", choice.Reason);
        })).DistinctBy(choice => choice.Id, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyDictionary<string, int> ActivateAbilityPaymentResourcePowerByTrait(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability)
    {
        var choices = ActivateAbilityPaymentResourceChoices(state, playerId, ability);
        return ActivateAbilityPaymentResourcePowerByTrait(state, playerId, choices);
    }

    private static IReadOnlyDictionary<string, int> ActivateAbilityPaymentResourcePowerByTrait(
        MatchState state, string playerId, IReadOnlyList<ActionPromptChoiceDto> choices)
    {
        if (choices.Count == 0
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var choiceIds = choices
            .Select(choice => choice.Id)
            .ToHashSet(StringComparer.Ordinal);
        var powerByTrait = zones.Base
            .Where(objectId => choiceIds.Contains($"{RecycleRunePaymentOptionalCostPrefix}{objectId}"))
            .Select(objectId => state.CardObjects.TryGetValue(objectId, out var runeState)
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

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ActivateAbilityPaymentResourcePowerByChoice(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability)
    {
        var choices = ActivateAbilityPaymentResourceChoices(state, playerId, ability);
        return ActivateAbilityPaymentResourcePowerByChoice(state, playerId, choices);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ActivateAbilityPaymentResourcePowerByChoice(
        MatchState state, string playerId, IReadOnlyList<ActionPromptChoiceDto> choices)
    {
        var metadata = ActivateAbilityPaymentResourcePowerByChoice(state, choices)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var legend in PromptRenektonPaymentChoices(state, playerId))
        {
            if (!metadata.ContainsKey(legend.Id)) continue;
            metadata[legend.Id] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mana"] = 2, ["unitPlayOrUnitAbilityOnly"] = true, ["powerCost"] = 2,
                ["powerSpend"] = legend.PowerSpend, ["sourceObjectId"] = legend.SourceObjectId,
                ["requiredRecycleRunePowerByTrait"] = legend.RequiredRunePower,
                ["resourceLifecycle"] = "rune-pool"
            };
        }
        return metadata;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ActivateAbilityPaymentResourcePowerByChoice(
        MatchState state, IReadOnlyList<ActionPromptChoiceDto> choices)
    {
        if (choices.Count == 0)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        var metadata = choices.ToDictionary(
            choice => choice.Id,
            choice =>
            {
                var trait = string.Empty;
                if (choice.Id.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
                {
                    var objectId = choice.Id[RecycleRunePaymentOptionalCostPrefix.Length..];
                    if (state.CardObjects.TryGetValue(objectId, out var runeState)
                        && TryGetRuneTrait(runeState, out var runeTrait))
                    {
                        trait = runeTrait;
                    }
                }

                return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["trait"] = trait,
                    ["power"] = BasicRuneRecyclePowerGain
                };
            },
            StringComparer.Ordinal);

        return metadata;
    }

    private static int ActivateAbilityAvailablePowerWithPaymentResources(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var powerPool = PaymentCostRules.PowerPoolForPurpose(runePool, CoreRuleEngine.PowerPurposeForActivatedAbility(ability));
        return new RunePaymentReachability(powerPool, ActivateAbilityPaymentResourcePowerByTrait(state, playerId, ability)).MaximumTotal;
    }

    private static bool CanPromptActivateAbilityInCurrentWindow(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability)
    {
        if (ability.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn)
            return CoreRuleEngine.CanActivateOrdinaryPowerAbility(state, playerId);
        if (ability.AbilityId == P4ActivatedAbilityCatalog.ShadowStunAbilityId)
            return CoreRuleEngine.IsShadowBattleResponseWindow(state, playerId);
        if (ability.SwiftSpeed) return CoreRuleEngine.CanActivateSwiftAbility(state, playerId);
        if (ability.IsResourceSkill && ability.ReactionSpeed)
            return CoreRuleEngine.CanActivateReactionResourceSkill(state, playerId);
        if (state.PendingPayment is not null || state.PendingHandChoice is not null || state.PendingCardChoice is not null
            || (state.PendingTaskQueue.IsBlocking && !HasOpenBattleResponsePriority(state))) return false;

        var isOpenMain = state.Phase == MatchPhases.Main && state.TimingState == TimingStates.NeutralOpen
            && state.ActivePlayerId == playerId && state.StackItems.Count == 0;
        if (isOpenMain) return !ability.ReactionSpeed;

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal))
        {
            return string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
                && string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
                && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal)
                && state.BattleState.IsActive
                && !string.IsNullOrWhiteSpace(state.BattleState.BattlefieldObjectId);
        }

        return string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, StringComparison.Ordinal)
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && string.Equals(state.FocusPlayerId, playerId, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(state.PriorityPlayerId);
    }

    private static bool CanPayManaAndAnyPower(MatchState state, RunePool runePool, int manaCost, int powerCost)
    {
        return manaCost >= 0
            && powerCost >= 0
            && PaymentCostRules.AvailableMana(runePool, ManaPaymentPurpose.Other, state) >= manaCost
            && runePool.TotalPower >= powerCost;
    }

    private static string ActivateAbilityDisplayName(P4ActivatedAbilityDefinition ability)
    {
        return ability.AbilityId switch
        {
            P4ActivatedAbilityCatalog.ViDoublePowerAbilityId => "蔚",
            P4ActivatedAbilityCatalog.XerathDamageAbilityId => "泽拉斯",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId => "玛尔扎哈",
            P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId => "龙魂贤者",
            P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId => "烈娜塔·戈拉斯克",
            P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId => "烈娜塔·戈拉斯克",
            P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId => "蜜糖果实",
            P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId => "阿兹尔",
            P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId => "守门者马杜里",
            P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId => "伊泽瑞尔",
            P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId => "猩红玫瑰",
            P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId => "绵绵魄罗",
            P4ActivatedAbilityCatalog.ShadowStunAbilityId => "黑影",
            P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId => "能量通道",
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId => "远古簇碑",
            P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId => "海克斯异常体",
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId => "金币",
            P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId => "金币",
            _ when P4ActivatedAbilityCatalog.TryGetSigilTypedResourceProfile(ability.AbilityId, out var profile)
                => profile.DisplayName,
            _ => ability.DisplayName
        };
    }

    private static string ActivateAbilityLabel(P4ActivatedAbilityDefinition ability)
    {
        if (ability.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn)
            return $"{ability.DisplayName}：横置，让一名单位本回合战力{ability.PowerModifierAmount:+0;-0}，最低{ability.MinimumPowerAfterModifier}";
        return ability.AbilityId switch
        {
            P4ActivatedAbilityCatalog.ViDoublePowerAbilityId => "蔚：支付 2 法力和 1 符能，战力翻倍",
            P4ActivatedAbilityCatalog.XerathDamageAbilityId => "泽拉斯：横置并支付符能，造成 3 点伤害",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId => "玛尔扎哈：摧毁友方单位或装备并横置，获得 2 点任意特性符能",
            P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId => "龙魂贤者：反应，横置，获得 1 点法力",
            P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId => "烈娜塔·戈拉斯克：支付 1 法力和 1 蓝色符能，抽 1 张牌",
            P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId => "烈娜塔·戈拉斯克：支付 4 法力和 4 蓝色符能并横置，获得 1 分",
            P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId => "蜜糖果实：反应，横置，获得 1 点费用符能；6 级时额外获得 1 点法力",
            P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId => "阿兹尔：迅捷，支付 1 绿色符能，与受控单位交换位置",
            P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId => "守门者马杜里：支付 1 紫色符能，移动到较弱敌方战场",
            P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId => "伊泽瑞尔：迅捷，支付 1 蓝色符能，移动到你的基地",
            P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId => "猩红玫瑰：消耗 3 经验并横置，让一名单位变为活跃状态",
            P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId => "绵绵魄罗：横置，打出两名拥有法盾的战鹰",
            P4ActivatedAbilityCatalog.ShadowStunAbilityId => "黑影：迅捷，支付 1 法力和 1 符能并横置，眩晕此处进攻的敌方单位",
            P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId => "能量通道：反应，横置，获得 1 点法力",
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId => "远古簇碑：反应，横置，支付法力获得等量费用符能",
            P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId => "海克斯异常体：反应，横置，支付通用符能获得等量法力",
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId => "使用金币：横置并摧毁，获得 1 点任意特性符能",
            P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId => "使用金币：横置并摧毁，获得 1 点任意特性符能",
            _ when P4ActivatedAbilityCatalog.TryGetSigilTypedResourceProfile(ability.AbilityId, out var profile)
                => $"{profile.DisplayName}：反应，横置，获得 1 点{profile.TraitLabel}费用符能",
            _ => ability.DisplayName
        };
    }

    private static string ActivateAbilityTargetScopeLabel(P4ActivatedAbilityDefinition ability)
    {
        if (ability.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn) return "任意公开单位";
        return ability.RequiredTargetCount == 0
            ? "无目标"
            : string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.XerathDamageAbilityId, StringComparison.Ordinal)
                ? "任意场上单位"
                : string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, StringComparison.Ordinal)
                    ? "任意单位"
                    : string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal)
                        ? "此战场进攻中的敌方单位"
                    : string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, StringComparison.Ordinal)
                        ? "受你控制的公开单位"
                    : string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId, StringComparison.Ordinal)
                        ? "敌方控制且敌方单位战力较低的战场"
                    : string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, StringComparison.Ordinal)
                        ? "友方单位或装备（成本）"
                    : string.Equals(
                        ability.Kind,
                        ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest,
                        StringComparison.Ordinal)
                        ? "友方单位"
                        : "服务端目标";
    }

    private static int SpellshieldTaxPowerForTarget(MatchState state, string playerId, string targetObjectId)
        => CoreRuleEngine.SpellshieldPowerCostForTarget(state, playerId, targetObjectId);

    private static string? BattlefieldGrantUnitExperienceObjectId(MatchState state, string playerId, string sourceObjectId)
    {
        var battlefield = BattlefieldLocalRules.AtUnit(state, sourceObjectId);
        return BattlefieldLocalRules.HasAbility(battlefield, BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantUnitExperienceAbility)
            ? battlefield!.ObjectId : null;
    }

    private static IReadOnlyList<AssembleEquipmentPromptRequirement> AssembleEquipmentSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Where(objectId => IsImplementedAssembleEquipmentSource(state, playerId, objectId))
            .Distinct(StringComparer.Ordinal)
            .Select(objectId => AssembleEquipmentSourceRequirement(state, playerId, objectId))
            .Where(requirement => requirement.TargetChoices.Count > 0)
            .ToArray();
    }

    private static AssembleEquipmentPromptRequirement AssembleEquipmentSourceRequirement(
        MatchState state,
        string playerId,
        string objectId)
    {
        var assembleProfile = AssembleEquipmentProfileForObject(state, objectId)
            ?? AssembleEquipmentProfileCatalog.FallbackRepresentative;
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        runePool = PaymentCostRules.PowerPoolForPurpose(runePool, PowerPaymentPurpose.Equipment);
        var paymentResourceChoices = AssembleEquipmentPaymentResourceChoices(state, playerId, assembleProfile);
        var paymentResourcePowerByTrait = AssembleEquipmentPaymentResourcePowerByTrait(state, playerId, assembleProfile);
        var targetChoices = AssembleEquipmentTargetChoicesForSource(
            state,
            playerId,
            objectId,
            assembleProfile,
            paymentResourcePowerByTrait);
        var additionalCostChoices = AssembleEquipmentAdditionalCostChoices(state, playerId, objectId, assembleProfile);
        var manaCostByTargetObjectId = AssembleEquipmentManaCostByTargetObjectId(state, targetChoices, assembleProfile);
        var targetPowerManaReductionByTargetObjectId = AssembleEquipmentTargetPowerManaReductionByTargetObjectId(
            state,
            targetChoices,
            assembleProfile);
        var manaCost = manaCostByTargetObjectId.Count > 0
            ? manaCostByTargetObjectId.Values.Min()
            : assembleProfile.ManaCost;
        var costChoices = new[] { new ActionPromptChoiceDto(assembleProfile.OptionalCost, assembleProfile.OptionalCostLabel) }
            .Concat(assembleProfile.HasExhaustUnitAlternative
                ? CoreRuleEngine.EquipmentExhaustCostChoices(state, playerId).Select(id => new ActionPromptChoiceDto(id,
                    "让友方单位变为休眠状态：" + ObjectChoice(state, id[CoreRuleEngine.ExhaustFriendlyUnitOptionalCostPrefix.Length..], "装配费用").Label)) : [])
            .ToArray();

        return new AssembleEquipmentPromptRequirement(
            objectId,
            assembleProfile.CardNo,
            assembleProfile.DisplayName,
            targetChoices,
            costChoices,
            additionalCostChoices,
            AssembleEquipmentRequiredAdditionalCostChoiceCount(assembleProfile),
            paymentResourceChoices,
            AssembleEquipmentPaymentResourcePowerByChoice(state, playerId, assembleProfile),
            PlayCardAvailablePowerByTrait(runePool, new Dictionary<string, int>(StringComparer.Ordinal)),
            PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait),
            assembleProfile.HasExhaustUnitAlternative ? [] : [assembleProfile.OptionalCost],
            manaCost,
            assembleProfile.ManaCost,
            manaCostByTargetObjectId,
            targetPowerManaReductionByTargetObjectId,
            assembleProfile.PowerCost,
            assembleProfile.ExperienceCost,
            true,
            null) { PowerReachability = new RunePaymentReachability(runePool, paymentResourcePowerByTrait),
                HasAlternativeCost = assembleProfile.HasExhaustUnitAlternative };
    }

    private static IReadOnlyList<ActionPromptChoiceDto> AssembleEquipmentTargetChoicesForSource(
        MatchState state,
        string playerId,
        string sourceObjectId,
        AssembleEquipmentProfile? assembleProfile = null,
        IReadOnlyDictionary<string, int>? paymentResourcePowerByTrait = null)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;

        return ControlledBoardObjects(state, playerId)
            .Where(objectId => !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal))
            .Where(objectId => IsImplementedAssembleEquipmentTarget(state, playerId, objectId))
            .Where(objectId => assembleProfile is null
                || (state.CardObjects.TryGetValue(objectId, out var targetState)
                    && CanPayAssembleEquipmentCost(
                        state,
                        playerId,
                        runePool,
                        assembleProfile,
                        paymentResourcePowerByTrait,
                        AssembleEquipmentManaCost(state, assembleProfile, targetState))))
            .Select(objectId => ObjectChoice(state, objectId, "implemented controlled unit host"))
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> AssembleEquipmentAdditionalCostChoices(
        MatchState state,
        string playerId,
        string sourceObjectId,
        AssembleEquipmentProfile assembleProfile)
    {
        var choices = new List<ActionPromptChoiceDto>();
        if (assembleProfile.RequiresDestroyFriendlyUnitCost)
        {
            choices.AddRange(AssembleEquipmentDestroyFriendlyUnitTargetObjectIds(state, playerId, sourceObjectId)
                .Select(objectId =>
                {
                    var choice = ObjectChoice(state, objectId, "required destroy friendly unit assemble cost");
                    return new ActionPromptChoiceDto(
                        $"{DestroyFriendlyUnitAdditionalCostPrefix}{objectId}",
                        $"摧毁友方单位：{choice.Label}",
                        choice.Reason);
                }));
        }

        if (assembleProfile.RequiredGraveyardRecycleCardCount > 0)
        {
            choices.AddRange(AssembleEquipmentGraveyardRecycleTargetObjectIds(state, playerId)
                .Select(objectId =>
                {
                    var choice = ObjectChoice(state, objectId, "required recycle graveyard card assemble cost");
                    return new ActionPromptChoiceDto(
                        $"{RecycleGraveyardCardAdditionalCostPrefix}{objectId}",
                        $"回收废牌堆：{choice.Label}",
                        choice.Reason);
                }));
        }

        return choices.ToArray();
    }

    private static int AssembleEquipmentRequiredAdditionalCostChoiceCount(AssembleEquipmentProfile assembleProfile)
    {
        return (assembleProfile.RequiresDestroyFriendlyUnitCost ? 1 : 0)
            + assembleProfile.RequiredGraveyardRecycleCardCount;
    }

    private static IReadOnlyList<string> AssembleEquipmentDestroyFriendlyUnitTargetObjectIds(
        MatchState state,
        string playerId,
        string sourceObjectId)
    {
        return ControlledBoardObjects(state, playerId)
            .Where(objectId => !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal))
            .Where(objectId => IsImplementedAssembleEquipmentDestroyFriendlyUnitCostTarget(state, playerId, objectId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsImplementedAssembleEquipmentDestroyFriendlyUnitCostTarget(
        MatchState state,
        string playerId,
        string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && !cardObject.IsFaceDown;
    }

    private static IReadOnlyList<string> AssembleEquipmentGraveyardRecycleTargetObjectIds(
        MatchState state,
        string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Graveyard
                .Where(objectId => IsImplementedAssembleEquipmentGraveyardRecycleCostTarget(state, playerId, objectId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .ToArray()
            : [];
    }

    private static bool IsImplementedAssembleEquipmentGraveyardRecycleCostTarget(
        MatchState state,
        string playerId,
        string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo);
    }

    private static bool IsImplementedAssembleEquipmentSource(MatchState state, string playerId, string objectId)
    {
        if (!IsObjectInPlayerZone(state, playerId, objectId, "BASE")
            || !state.CardObjects.TryGetValue(objectId, out var cardObject)
            || cardObject.IsFaceDown
            || !cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            || !string.IsNullOrWhiteSpace(cardObject.AttachedToObjectId))
        {
            return false;
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
        {
            return false;
        }

        var assembleProfile = AssembleEquipmentProfileForObject(state, objectId);
        if (assembleProfile is null)
        {
            return false;
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourcePowerByTrait = AssembleEquipmentPaymentResourcePowerByTrait(state, playerId, assembleProfile);
        var targetChoices = AssembleEquipmentTargetChoicesForSource(
            state,
            playerId,
            objectId,
            assembleProfile,
            paymentResourcePowerByTrait);
        if (targetChoices.Count == 0)
        {
            return false;
        }

        if (assembleProfile.RequiresDestroyFriendlyUnitCost)
        {
            var destroyCostTargetObjectIds = AssembleEquipmentDestroyFriendlyUnitTargetObjectIds(
                state,
                playerId,
                objectId);
            if (!targetChoices.Any(targetChoice => destroyCostTargetObjectIds.Any(
                    destroyObjectId => !string.Equals(destroyObjectId, targetChoice.Id, StringComparison.Ordinal))))
            {
                return false;
            }
        }

        if (assembleProfile.RequiredGraveyardRecycleCardCount > 0
            && AssembleEquipmentGraveyardRecycleTargetObjectIds(state, playerId).Count < assembleProfile.RequiredGraveyardRecycleCardCount)
        {
            return false;
        }

        return targetChoices.Any(targetChoice => state.CardObjects.TryGetValue(targetChoice.Id, out var targetState)
            && CanPayAssembleEquipmentCost(
                state,
                playerId,
                runePool,
                assembleProfile,
                paymentResourcePowerByTrait,
                AssembleEquipmentManaCost(state, assembleProfile, targetState)));
    }

    private static AssembleEquipmentProfile? AssembleEquipmentProfileForObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && AssembleEquipmentProfileCatalog.TryGet(cardObject.CardNo, out var assembleProfile)
                ? assembleProfile
                : null;
    }

    private static bool CanPayAssembleEquipmentCost(
        MatchState state,
        string playerId,
        RunePool runePool,
        AssembleEquipmentProfile assembleProfile,
        IReadOnlyDictionary<string, int>? paymentResourcePowerByTrait = null,
        int manaCost = 0)
    {
        if (assembleProfile.HasExhaustUnitAlternative && CoreRuleEngine.EquipmentExhaustCostChoices(state, playerId).Length > 0)
            return true;
        runePool = PaymentCostRules.PowerPoolForPurpose(runePool, PowerPaymentPurpose.Equipment);
        if (PaymentCostRules.AvailableMana(runePool, ManaPaymentPurpose.Other, state) < manaCost)
        {
            return false;
        }

        var availableExperience = state.PlayerExperience.TryGetValue(playerId, out var currentExperience)
            ? currentExperience
            : 0;
        if (availableExperience < assembleProfile.ExperienceCost)
        {
            return false;
        }

        var resources = paymentResourcePowerByTrait ?? new Dictionary<string, int>();
        return new RunePaymentReachability(runePool, resources).CanPay(
            AssembleEquipmentUsesAnyPower(assembleProfile) ? assembleProfile.PowerCost : 0,
            AssembleEquipmentUsesAnyPower(assembleProfile) ? new Dictionary<string, int>()
                : new Dictionary<string, int> { [assembleProfile.PowerTrait] = assembleProfile.PowerCost });
    }

    private static IReadOnlyDictionary<string, int> AssembleEquipmentManaCostByTargetObjectId(
        MatchState state,
        IReadOnlyList<ActionPromptChoiceDto> targetChoices,
        AssembleEquipmentProfile assembleProfile)
    {
        return targetChoices
            .Where(choice => state.CardObjects.ContainsKey(choice.Id))
            .ToDictionary(
                choice => choice.Id,
                choice => AssembleEquipmentManaCost(state, assembleProfile, state.CardObjects[choice.Id]),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> AssembleEquipmentTargetPowerManaReductionByTargetObjectId(
        MatchState state,
        IReadOnlyList<ActionPromptChoiceDto> targetChoices,
        AssembleEquipmentProfile assembleProfile)
    {
        return targetChoices
            .Where(choice => state.CardObjects.ContainsKey(choice.Id))
            .ToDictionary(
                choice => choice.Id,
                choice => AssembleEquipmentTargetPowerManaReduction(state, assembleProfile, state.CardObjects[choice.Id]),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<ActionPromptChoiceDto> AssembleEquipmentPaymentResourceChoices(
        MatchState state,
        string playerId,
        AssembleEquipmentProfile assembleProfile)
        => RecycleRunePaymentResourceChoices(state, playerId, assembleProfile.PaymentResourceReason);

    private static IReadOnlyDictionary<string, int> AssembleEquipmentPaymentResourcePowerByTrait(
        MatchState state,
        string playerId,
        AssembleEquipmentProfile assembleProfile)
    {
        var choices = AssembleEquipmentPaymentResourceChoices(state, playerId, assembleProfile);
        if (choices.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        if (AssembleEquipmentUsesAnyPower(assembleProfile))
        {
            var choiceIds = choices
                .Select(choice => choice.Id)
                .ToHashSet(StringComparer.Ordinal);
            var powerByTrait = state.PlayerZones.TryGetValue(playerId, out var zones)
                ? zones.Base
                    .Where(objectId => choiceIds.Contains($"{RecycleRunePaymentOptionalCostPrefix}{objectId}"))
                    .Select(objectId => state.CardObjects.TryGetValue(objectId, out var runeState)
                        && TryGetRuneTrait(runeState, out var runeTrait)
                            ? runeTrait
                            : string.Empty)
                    .Where(trait => !string.IsNullOrWhiteSpace(trait))
                    .GroupBy(trait => trait, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Count() * BasicRuneRecyclePowerGain,
                        StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal);

            return powerByTrait;
        }

        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [assembleProfile.PowerTrait] = choices.Count * BasicRuneRecyclePowerGain
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> AssembleEquipmentPaymentResourcePowerByChoice(
        MatchState state,
        string playerId,
        AssembleEquipmentProfile assembleProfile)
    {
        var choices = AssembleEquipmentPaymentResourceChoices(state, playerId, assembleProfile);
        if (choices.Count == 0)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        var metadata = choices.ToDictionary(
            choice => choice.Id,
            choice =>
            {
                var trait = assembleProfile.PowerTrait;
                if (AssembleEquipmentUsesAnyPower(assembleProfile)
                    && choice.Id.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
                {
                    var objectId = choice.Id[RecycleRunePaymentOptionalCostPrefix.Length..];
                    if (state.CardObjects.TryGetValue(objectId, out var runeState)
                        && TryGetRuneTrait(runeState, out var runeTrait))
                    {
                        trait = runeTrait;
                    }
                }

                return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["trait"] = trait,
                    ["power"] = BasicRuneRecyclePowerGain
                };
            },
            StringComparer.Ordinal);

        return metadata;
    }

    private static bool AssembleEquipmentUsesAnyPower(AssembleEquipmentProfile assembleProfile)
    {
        return string.IsNullOrWhiteSpace(assembleProfile.PowerTrait);
    }

    private static int AssembleEquipmentManaCost(MatchState state, AssembleEquipmentProfile assembleProfile, CardObjectState targetState)
    {
        return Math.Max(0, assembleProfile.ManaCost - AssembleEquipmentTargetPowerManaReduction(state, assembleProfile, targetState)
            - CoreRuleEngine.EquipmentAbilityManaReduction(state, targetState.ControllerId!));
    }

    private static int AssembleEquipmentTargetPowerManaReduction(
        MatchState state, AssembleEquipmentProfile assembleProfile,
        CardObjectState targetState)
    {
        return assembleProfile.ReduceManaCostByTargetPower ? CoreRuleEngine.ResolveCurrentFieldUnitPower(state, targetState) : 0;
    }

    private static bool IsImplementedAssembleEquipmentTarget(MatchState state, string playerId, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && !cardObject.IsFaceDown;
    }

    private static bool IsObjectInPlayerZone(
        MatchState state,
        string playerId,
        string objectId,
        string zone)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        return zone switch
        {
            "BASE" => zones.Base.Contains(objectId, StringComparer.Ordinal),
            "BATTLEFIELD" => zones.Battlefields.Contains(objectId, StringComparer.Ordinal),
            _ => false
        };
    }

    private static bool IsImplementedPlayableHandSource(MatchState state, string playerId, string objectId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || string.IsNullOrWhiteSpace(cardObject.CardNo)
            || !SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
                && !CoreRuleEngine.IsForeignGraveyardEffectPlay(state, playerId, objectId)
                && !(state.PendingEffectPlay is { SourceReplay: not null } self && self.PlayerId == playerId && self.Sources.ContainsKey(objectId)))
        {
            return false;
        }

        return PlayCardPromptBehaviorsForSource(state, playerId, objectId).Any(behavior =>
            PromptHasRequiredTargetChoices(state, playerId, behavior));
    }

    private static bool IsAgileDirectPlayAttachRepresentative(CardBehaviorDefinition behavior)
    {
        return behavior.PlaysSourceToBaseAsEquipment
            && CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary(behavior.CardNo);
    }


    private static string? PromptOpponentOf(MatchState state, string playerId)
    {
        return state.Seats.Keys.FirstOrDefault(seatPlayerId =>
            !string.Equals(seatPlayerId, playerId, StringComparison.Ordinal));
    }

    private static int PromptEffectiveWinningScore(MatchState state) =>
        BaseWinningScore + BattlefieldLocalRules.WinningScoreIncrease(state.PlayerZones, state.CardObjects);

    private static IReadOnlyList<string> PromptDelimitedValues(string values)
    {
        return string.IsNullOrWhiteSpace(values)
            ? []
            : values
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
    }

    private static bool HasPromptDiscardHandCardOptionalCostTarget(
        MatchState state,
        string playerId,
        string? sourceObjectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Any(objectId => CanPromptDiscardHandCardAsOptionalCost(state, playerId, sourceObjectId, objectId));
    }

    private static bool CanPromptDiscardHandCardAsOptionalCost(
        MatchState state,
        string playerId,
        string? sourceObjectId,
        string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && (string.IsNullOrWhiteSpace(sourceObjectId)
                || !string.Equals(sourceObjectId, objectId, StringComparison.Ordinal))
            && (!state.CardObjects.TryGetValue(objectId, out var cardObject)
                || SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static bool ShouldPreferModeSpecificPlayCardBehaviors(
        MatchState state,
        string playerId,
        IReadOnlyList<CardBehaviorDefinition> behaviors)
    {
        var modeBehaviors = behaviors
            .Where(behavior => !string.IsNullOrWhiteSpace(behavior.Mode))
            .ToArray();

        return modeBehaviors.Length > 0
            && behaviors.Any(IsDefaultUnitPlayBehavior)
            && modeBehaviors.All(IsModeSpecificUnitPlayBehavior)
            && modeBehaviors.Any(behavior => PromptHasRequiredTargetChoices(state, playerId, behavior));
    }

    private static bool IsDefaultUnitPlayBehavior(CardBehaviorDefinition behavior)
    {
        return string.IsNullOrWhiteSpace(behavior.Mode)
            && behavior.PlaysSourceToBaseAsUnit
            && behavior.RequiredTargetCount == 0;
    }

    private static bool IsModeSpecificUnitPlayBehavior(CardBehaviorDefinition behavior)
    {
        return !string.IsNullOrWhiteSpace(behavior.Mode)
            && behavior.PlaysSourceToBaseAsUnit
            && behavior.RequiredTargetCount > 0;
    }

    private static bool PublicFieldFriendlyPromptKeywordAuraAppliesToParticipant(
        StaticAuraSpec aura,
        string sourceObjectId,
        string targetObjectId,
        CardObjectState target)
    {
        if (target.IsFaceDown)
        {
            return false;
        }

        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.OtherFriendlyUnits, StringComparison.Ordinal))
        {
            return !string.Equals(sourceObjectId, targetObjectId, StringComparison.Ordinal);
        }

        return string.Equals(aura.TargetScope, StaticAuraTargetScopes.FriendlyFilteredUnits, StringComparison.Ordinal)
            && StaticAuraSpecRules.TargetMatchesFilter(aura, target);
    }
}
