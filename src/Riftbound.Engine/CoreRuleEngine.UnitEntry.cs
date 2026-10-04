using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record UnitEntry(
        CardObjectState UnitState,
        bool HasteReadyOptionalCostPaid,
        bool SourceReadyOptionalCostPaid,
        int FriendlyEquipmentPowerBonus,
        StaticAbilitySpec? AppliedEntryStaticAbility,
        string AppliedEntryStaticAbilitySourceObjectId,
        string? AppliedEntryStaticAbilitySourceCardNo);

    // Normal plays and effect-driven plays share printed keywords, current
    // level/aura conditions, power and the CN 143.4 exhausted-entry default.
    private static UnitEntry PrepareUnitEntry(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        IReadOnlyDictionary<string, int> playerExperience,
        IReadOnlyList<string> destroyedUnitOwnerIdsThisTurn,
        IReadOnlyList<string> untilEndOfTurnEffects)
    {
        var zones = playerZones[stackItem.ControllerId];
        var existingState = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            ? sourceState
            : new CardObjectState(stackItem.SourceObjectId);
        var baseUnitPower = behavior.SourceUnitPower > 0
            ? behavior.SourceUnitPower
            : existingState.Power;
        var unitPower = behavior.AddsControllerGraveyardCountToSourceUnitPower
            ? baseUnitPower + zones.Graveyard.Count
            : baseUnitPower;
        var friendlyEquipmentPowerBonus = ResolveFriendlyEquipmentStaticPowerBonus(
            behavior,
            playerZones,
            cardObjects,
            stackItem.ControllerId);
        var levelApplies = ControllerMeetsLevelExperienceThreshold(
            behavior,
            stackItem.ControllerId,
            playerExperience);
        if (levelApplies)
        {
            unitPower += behavior.LevelSourceUnitPowerBonus;
        }
        unitPower += ResolveConditionalSourceUnitPowerBonus(behavior, stackItem.ControllerId, untilEndOfTurnEffects);
        unitPower += friendlyEquipmentPowerBonus;

        var hasteReadyOptionalCostPaid = IsHasteReadyOptionalCostPaidForPlayUnit(
            behavior,
            stackItem.OptionalCosts);
        var exhaustsForUnpaidHasteReady = HasHasteReadyEntryCost(behavior) && !hasteReadyOptionalCostPaid;
        var sourceReadyOptionalCostPaid = IsSourceReadyOptionalCostPaid(
            behavior,
            stackItem.OptionalCosts,
            stackItem.ControllerId,
            untilEndOfTurnEffects);
        var entersReadyFromSourceUnitStaticAbility =
            TryGetSourceUnitEnterReadyStaticAbility(
                behavior,
                playerZones,
                cardObjects,
                stackItem.SourceObjectId,
                stackItem.ControllerId,
                playerExperience,
                destroyedUnitOwnerIdsThisTurn,
                IsStackItemBattlefieldDestination(stackItem),
                out var sourceUnitEntryStaticAbility);
        var entersReadyFromOtherFriendlyStaticAbility =
            TryGetFriendlyUnitEnterReadyStaticAbilitySource(
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                behavior.CardNo,
                playerExperience,
                out var entryStaticAbilitySourceObjectId,
                out var entryStaticAbilitySourceState,
                out var entryStaticAbility);
        var appliedEntryStaticAbility = entersReadyFromSourceUnitStaticAbility
            ? sourceUnitEntryStaticAbility
            : entersReadyFromOtherFriendlyStaticAbility
                ? entryStaticAbility
                : null;
        var appliedEntryStaticAbilitySourceObjectId = entersReadyFromSourceUnitStaticAbility
            ? stackItem.SourceObjectId
            : entryStaticAbilitySourceObjectId;
        var appliedEntryStaticAbilitySourceCardNo = entersReadyFromSourceUnitStaticAbility
            ? behavior.CardNo
            : entryStaticAbilitySourceState.CardNo;
        var unitState = existingState with
        {
            Power = unitPower,
            IsExhausted = entersReadyFromSourceUnitStaticAbility
                || entersReadyFromOtherFriendlyStaticAbility
                || sourceReadyOptionalCostPaid
                || hasteReadyOptionalCostPaid
                    ? false
                    : existingState.IsExhausted || behavior.SourceUnitIsExhausted || exhaustsForUnpaidHasteReady,
            CardNo = string.IsNullOrWhiteSpace(existingState.CardNo) ? behavior.CardNo : existingState.CardNo,
            Tags = existingState.Tags
                .Concat([CardObjectTags.UnitCard])
                .Concat(ParseDelimitedValues(behavior.SourceUnitTags))
                .Concat(levelApplies ? ParseDelimitedValues(behavior.LevelSourceUnitTags) : [])
                .Concat(ResolveConditionalSourceUnitTags(behavior, stackItem.ControllerId, untilEndOfTurnEffects))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        return new(unitState, hasteReadyOptionalCostPaid, sourceReadyOptionalCostPaid,
            friendlyEquipmentPowerBonus, appliedEntryStaticAbility,
            appliedEntryStaticAbilitySourceObjectId, appliedEntryStaticAbilitySourceCardNo);
    }

    private static CardObjectState InitializeEffectPlayedUnit(
        MatchState context,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardObjectState previous,
        string ownerId,
        string controllerId,
        bool toBattlefield = false,
        string statusEffectId = "",
        int crossedZones = 1)
    {
        var unit = PrintedCardFactory.TryRestoreOutsidePlay(previous, ownerId, out var printed)
            ? printed : previous with
            {
                Damage = 0,
                Power = previous.Power - previous.UntilEndOfTurnPowerModifier,
                UntilEndOfTurnEffects = [], UntilEndOfTurnPowerModifier = 0,
                UntilEndOfTurnPowerModifiers = [], AttachedToObjectId = null,
                IsAttacking = false, IsDefending = false
            };
        unit = unit with { OwnerId = ownerId, ControllerId = controllerId, IsExhausted = true,
            ObjectGeneration = checked(previous.ObjectGeneration + crossedZones) };
        cardObjects[unit.ObjectId] = unit;
        if (CardBehaviorRegistry.TryGetByCardNo(unit.CardNo ?? string.Empty, out var behavior))
        {
            // Entry calculation only. Optional costs, destinations and nested
            // play triggers must still be handled by the enclosing play flow.
            var entry = PrepareUnitEntry(playerZones, cardObjects, behavior,
                new StackItemState(controllerId: controllerId, sourceObjectId: unit.ObjectId,
                    cardNo: unit.CardNo, destination: toBattlefield ? "BATTLEFIELD:ENTRY" : "BASE"),
                context.PlayerExperience, context.DestroyedUnitOwnerIdsThisTurn, context.UntilEndOfTurnEffects);
            unit = entry.UnitState;
        }
        if (!string.IsNullOrWhiteSpace(statusEffectId))
            unit = unit with { UntilEndOfTurnEffects = [statusEffectId] };
        cardObjects[unit.ObjectId] = unit;
        return unit;
    }
}
