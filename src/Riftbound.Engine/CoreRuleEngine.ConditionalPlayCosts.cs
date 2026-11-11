using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static (int Mana, int Power) OpponentSpellCostIncreases(MatchState state, string player, CardBehaviorDefinition behavior)
    {
        if (!IsSpellPlayBehavior(behavior) || EffectPlayIgnoresAllCosts(state, player)) return (0, 0);
        var mana = 0; var power = 0;
        foreach (var id in PublicStaticAuraSourceObjectIds(state.PlayerZones)) {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.IsFaceDown
                || EffectiveFieldControllerId(state.PlayerZones, id, source) == player) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                if (CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var definition) && definition.OpponentSpellCostIncrease is { } tax) {
                    mana = checked(mana + tax.Mana);
                    power = checked(power + (source.IsEmpowered ? tax.GenericPowerIfEmpowered ?? tax.GenericPower : tax.GenericPower));
                }
        }
        foreach (var source in CombatSpellCostSources(state).Where(source => source.Controller != player))
        {
            mana = checked(mana + source.Spec.EnemyManaIncrease);
            power = checked(power + source.Spec.EnemyGenericPowerIncrease);
        }
        return (mana, power);
    }

    // Re-evaluate the current controller's condition on every quote and payment.
    // In-play object counts exclude face-down cards, which have no unit or equipment type.
    // CN354 puts the played source onto the chain before CN356.4 counts the
    // remaining graveyard. Quotes receive the original state, so exclude it here.
    private static int ConditionalPlayCostCount(MatchState state, string player, CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
        => behavior.CostReductionConditionKind switch
        {
            CardCostReductionConditionKinds.ControllerHeldBattlefieldCountThisTurn
                => state.BattlefieldHolds is { } holds && holds.TurnNumber == state.TurnNumber
                    ? holds.Entries.Count(entry => entry.PlayerId == player) : 0,
            CardCostReductionConditionKinds.ControllerExperienceThresholdCount
                => behavior.CostReductionExperienceThresholds?.Count(threshold
                    => state.PlayerExperience.GetValueOrDefault(player) >= threshold) ?? 0,
            CardCostReductionConditionKinds.OpponentControlsStunnedUnit
                => OpponentControlsStunnedUnit(state.PlayerZones, state.CardObjects, player) ? 1 : 0,
            CardCostReductionConditionKinds.ControlledDistinctUnitTagCount
                => CountControlledFriendlyUnitTags(state, player, behavior.CostReductionUnitTags ?? []),
            CardCostReductionConditionKinds.ControllerGraveyardCardCount
                => state.PlayerZones.TryGetValue(player, out var graveyardZones)
                    ? graveyardZones.Graveyard.Count(id => id != sourceObjectId) : 0,
            CardCostReductionConditionKinds.ControlledFieldEquipmentCount
                => CountControlledPublicFieldEquipmentObjects(state.PlayerZones, state.CardObjects, player),
            CardCostReductionConditionKinds.SameNameInControllerGraveyard
                => state.PlayerZones.TryGetValue(player, out var zones)
                    ? zones.Graveyard.Count(id => id != sourceObjectId && state.CardObjects.TryGetValue(id, out var card)
                        && OfficialCardSourceIdentityGroups.SameKnownName(behavior.CardNo, card.CardNo)) : 0,
            CardCostReductionConditionKinds.ControlledBattlefieldUnitCount
                => BattlefieldLocalRules.ControlledBy(state, player).Any(field =>
                    state.CardObjects.Values.Count(card => !card.IsFaceDown && card.Tags.Contains(CardObjectTags.UnitCard)
                        && IsObjectOnField(state.PlayerZones, card.ObjectId)
                        && state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
                        && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == field.ObjectId)
                    == behavior.CostReductionBattlefieldUnitCount) ? 1 : 0,
            _ => 0
        };

    private static IReadOnlyDictionary<string, int> ConditionalPlayPowerCost(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyDictionary<string, int> typed)
    {
        if (behavior.CostReductionPower <= 0 || string.IsNullOrEmpty(behavior.CostReductionPowerTrait)
            || EffectPlayIgnoresAllCosts(state, player)) return typed;
        var reduction = behavior.CostReductionPower * ConditionalPlayCostCount(state, player, behavior);
        return typed.ToDictionary(p => p.Key, p => p.Key == behavior.CostReductionPowerTrait
            ? Math.Max(0, p.Value - reduction) : p.Value, StringComparer.Ordinal);
    }

    private static IEnumerable<CardBehaviorDefinition> FlowSpellCostReducers(MatchState state, string player,
        CardBehaviorDefinition behavior)
    {
        if (behavior.AppliedFlowCost is null || !IsSpellPlayBehavior(behavior)) yield break;
        foreach (var id in PublicStaticAuraSourceObjectIds(state.PlayerZones))
        {
            if (!state.CardObjects.TryGetValue(id, out var card) || card.IsFaceDown
                || EffectiveFieldControllerId(state.PlayerZones, id, card) != player) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, card))
                if (CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var source)
                    && source.StaticFlowSpellCostReductionMana > 0) yield return source;
        }
    }
}
