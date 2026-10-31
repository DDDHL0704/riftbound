using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SelectedBattlefieldEffectSpec(int EnemyPowerModifier, bool MoveOptionalEnemy,
    int FriendlyPowerModifier = 0, bool DamagesEnemyUnits = false, int BattlefieldTargetIndex = 0,
    int? DamageFromUnitTargetIndex = null, int? PrimaryEnemyTargetIndex = null,
    int? RequiredFriendlyUnitCount = null, string FriendlyStatusEffectId = "",
    int? OptionalEnemyReturnMinimumUnitCount = null);

public sealed partial class CoreRuleEngine
{
    internal static bool HasFriendlyUnitAtSelectedBattlefield(MatchState state, string playerId, string battlefieldId)
        => BattlefieldLocalRules.Battlefield(state, battlefieldId) is not null
            && state.CardObjects.Keys.Any(id => IsPlayerControlledFieldUnitObject(state, playerId, id)
                && BattlefieldLocalRules.AtUnit(state, id)?.ObjectId == battlefieldId);

    internal static bool IsSelectedBattlefieldTargetRelationAllowed(MatchState state,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, int index)
    {
        if (behavior.SelectedBattlefieldEffect is not { } spec) return true;
        if (index == spec.BattlefieldTargetIndex || index == spec.DamageFromUnitTargetIndex) return true;
        if (targets.Count <= spec.BattlefieldTargetIndex || targets.Count <= index) return false;
        var field = targets[spec.BattlefieldTargetIndex];
        if (index == spec.PrimaryEnemyTargetIndex)
            return BattlefieldLocalRules.AtUnit(state, targets[index])?.ObjectId == field;
        return index == 1 && spec.MoveOptionalEnemy
            && IsUnitMovementDestination(state, targets[index], "BATTLEFIELD:" + field);
    }

    private static void ResolveSelectedBattlefieldEffect(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations, Dictionary<string, RunePool> pools, CardBehaviorDefinition behavior,
        StackItemState item, List<GameEvent> events, ISet<string> damageTriggeredDestroyTargets,
        bool preventDamage)
    {
        if (behavior.SelectedBattlefieldEffect is not { } spec || item.TargetObjectIds.Count <= spec.BattlefieldTargetIndex
            || string.IsNullOrWhiteSpace(item.TargetObjectIds[spec.BattlefieldTargetIndex])) return;
        var field = item.TargetObjectIds[spec.BattlefieldTargetIndex];
        var current = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        if (BattlefieldLocalRules.Battlefield(current, field) is null
            || behavior.TargetScope == CardTargetScopes.FriendlyOccupiedBattlefieldThenEnemyUnit
                && !HasFriendlyUnitAtSelectedBattlefield(current, item.ControllerId, field)) return;
        int? unitDamageAmount = null;
        if (spec.DamageFromUnitTargetIndex is { } unitIndex)
        {
            if (item.TargetObjectIds.Count <= unitIndex || string.IsNullOrWhiteSpace(item.TargetObjectIds[unitIndex])
                || !IsLegalChosenCardTarget(current, item.ControllerId, item.TargetObjectIds[unitIndex], unitIndex,
                    item.TargetObjectIds, PlayCardTargetScopeForBehavior(behavior), behavior)) return;
            unitDamageAmount = Math.Max(0, ResolveCurrentFieldUnitPower(current, cards[item.TargetObjectIds[unitIndex]]));
        }

        for (var repeat = 0; repeat < item.EffectRepeatCount; repeat++)
        {
            var instructionItem = behavior.DamageAmountFromOptionalPowerCost
                ? item with { DamageAmount = ChooseInstructionPowerPayment(current, item, zones, cards, pools, events) }
                : item;
            if (spec.MoveOptionalEnemy && item.TargetObjectIds.Count > 1
                && !string.IsNullOrWhiteSpace(item.TargetObjectIds[1]))
                MoveFieldUnitsToChosenLocation(current, zones, cards, locations, pools, item,
                    [item.TargetObjectIds[1]], "BATTLEFIELD:" + field, events);

            // CN 355.5.a: the chosen battlefield/unit are targets; the local group
            // is collected after movement and neither pays Ward nor gets retargeted.
            var affected = cards.Keys.Where(id => BattlefieldLocalRules.AtUnit(current, id)?.ObjectId == field)
                .Order(StringComparer.Ordinal).ToArray();
            if (spec.OptionalEnemyReturnMinimumUnitCount is { } minimum && affected.Length >= minimum
                && spec.PrimaryEnemyTargetIndex is { } returnIndex && item.TargetObjectIds.Count > returnIndex
                && !string.IsNullOrWhiteSpace(item.TargetObjectIds[returnIndex]))
            {
                var target = item.TargetObjectIds[returnIndex];
                if (IsLegalChosenCardTarget(current, item.ControllerId, target, returnIndex, item.TargetObjectIds,
                        PlayCardTargetScopeForBehavior(behavior), behavior)
                    && TryReturnTargetToHand(zones, cards, target, out var owner, out var equipment))
                    events.Add(BuildReturnedToHandEvent(behavior.DisplayName, item, target, owner, equipment));
            }
            var friendlyCountMatches = spec.RequiredFriendlyUnitCount is not { } required
                || affected.Count(id => IsPlayerControlledFieldUnitObject(current, item.ControllerId, id)) == required;
            // Snapshot simultaneous damage bonuses and group Might before
            // changing any member of the affected group.
            var instructionState = current with { CardObjects = cards.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) };
            foreach (var id in affected)
            {
                if (!string.IsNullOrEmpty(spec.FriendlyStatusEffectId)
                    && IsPlayerControlledFieldUnitObject(current, item.ControllerId, id))
                {
                    var effect = TemporaryCombatKeywordRules.AppliedEffectId(cards[id], spec.FriendlyStatusEffectId);
                    cards[id] = cards[id] with {
                        UntilEndOfTurnEffects = cards[id].UntilEndOfTurnEffects.Append(effect)
                            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                    events.Add(new("STATUS_EFFECT_APPLIED", $"{behavior.DisplayName}施加{spec.FriendlyStatusEffectId}",
                        new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId,
                            ["targetObjectId"] = id, ["effectId"] = spec.FriendlyStatusEffectId }));
                }
                if (spec.DamagesEnemyUnits && IsEnemyBattlefieldUnitObject(current, item.ControllerId, id))
                {
                    var amount = unitDamageAmount is { } fromUnit
                        ? ApplySpellSkillDamageBonuses(instructionState, item.ControllerId, id, fromUnit)
                        : spec.PrimaryEnemyTargetIndex is { } primaryIndex && id != item.TargetObjectIds[primaryIndex]
                            ? ApplySpellSkillDamageBonuses(instructionState, item.ControllerId, id, behavior.OtherEnemyBattlefieldUnitsDamageAmount)
                            : ResolveDamageAmount(instructionState, instructionItem, behavior, id);
                    var damage = ApplyDamageToCardObject(current, cards, item.ControllerId, id,
                        amount, damageTriggeredDestroyTargets,
                        preventDamage, PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new("DAMAGE_APPLIED", $"{behavior.DisplayName}造成 {damage.DamageAmount} 点伤害",
                        BuildDamagePayload(item.SourceObjectId, id, damage)));
                }
                var delta = IsPlayerControlledFieldUnitObject(current, item.ControllerId, id)
                    ? (friendlyCountMatches ? spec.FriendlyPowerModifier : 0) : IsEnemyBattlefieldUnitObject(current, item.ControllerId, id)
                        ? spec.EnemyPowerModifier : 0;
                if (delta == 0) continue;
                cards[id] = ApplyPowerModifier(instructionState, cards[id], behavior, item, id, delta, out var powerEvent);
                events.Add(powerEvent);
            }
        }
    }
}
