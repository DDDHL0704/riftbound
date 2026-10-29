using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Count only public in-play objects; a face-down standby card has no unit or equipment type.
    private static int ConditionalPlayCostCount(MatchState state, string player, CardBehaviorDefinition behavior)
        => behavior.CostReductionConditionKind switch
        {
            CardCostReductionConditionKinds.ControlledFieldEquipmentCount
                => CountControlledPublicFieldEquipmentObjects(state.PlayerZones, state.CardObjects, player),
            CardCostReductionConditionKinds.SameNameInControllerGraveyard
                => state.PlayerZones.TryGetValue(player, out var zones)
                    ? zones.Graveyard.Count(id => state.CardObjects.TryGetValue(id, out var card)
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
