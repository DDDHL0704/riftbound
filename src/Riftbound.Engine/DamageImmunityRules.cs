using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class DamageImmunityRules
{
    internal static string? Effect(MatchState state, CardObjectState unit)
    {
        if (UnitMovementRules.CannotTakeDamage(unit)) return "MOVEMENT_DAMAGE_IMMUNITY";
        // Combat setup can pass a pre-role snapshot while the current state
        // already contains the same object's attacker/defender role.
        if (state.CardObjects.TryGetValue(unit.ObjectId, out var current)
            && current.ObjectGeneration == unit.ObjectGeneration) unit = current;
        if (unit.IsFaceDown || !unit.Tags.Contains(CardObjectTags.UnitCard)
            || !AttachedRuleTextRules.Field(state, unit.ObjectId)) return null;
        return AttachedRuleTextRules.Instances(state, unit).Any(instance =>
            CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo,
                ability => ability.Kind == StaticAbilityKinds.SourceUnitDamageImmunity
                    && (ability.RequiresSourceEmpowered != true || unit.IsEmpowered)
                    && (ability.RequiresSourceOutsideBattle != true || !CombatPresenceRules.IsInCombat(state, unit)), out _))
            ? "STATIC_DAMAGE_IMMUNITY" : null;
    }

    internal static bool CannotTakeDamage(MatchState state, CardObjectState unit) => Effect(state, unit) is not null;
}
