using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static int EquipmentSkillDamageBonus(MatchState state, string controller, StackItemState? item)
    {
        if (item is null || !StackObjectRules.IsAbility(item)) return 0;
        // Bomb tokens cease to exist. Their validated captured factory retains
        // the source's equipment identity for the independent skill (383.2.c.3.b).
        var equipment = item.FieldContext is { Kind: BombDestructionKind }
            || (state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                ? source.Tags.Contains(CardObjectTags.EquipmentCard)
                : item.CardNo is not null && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var definition)
                    && definition.PlaysSourceToBaseAsEquipment);
        if (!equipment) return 0;
        return state.CardObjects.Values.Where(unit => IsFaceUpNonStandbyUnit(unit)
                && IsObjectOnField(state.PlayerZones, unit.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) == controller)
            .SelectMany(unit => AttachedRuleTextRules.Instances(state, unit))
            .Sum(instance => CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo,
                ability => ability.Kind == StaticAbilityKinds.FriendlyEquipmentSkillDamageBonus, out var bonus) ? bonus.Amount : 0);
    }
}
