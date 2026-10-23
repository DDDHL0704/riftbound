namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasControllerLethalDamage(MatchState state, string player)
        => state.CardObjects.Values.Any(card => FieldObjectTypeRules.IsVisibleUnit(card)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) == player
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior) && behavior.MakesControllerDamageLethal);

    private static bool IsMarkedDamageLethal(MatchState state, CardObjectState card)
        => FieldObjectTypeRules.IsVisibleUnit(card) && card.Damage > 0 && card.DamageMarkedByPlayerIds is { Count: > 0 } players
            && players.Any(player => player != EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card)
                && HasControllerLethalDamage(state, player));

    private static int RequiredLethalDamage(MatchState state, CardObjectState target, int power, string? assigningPlayer)
    {
        if (UnitMovementRules.CannotTakeDamage(target)) return 0;
        var ordinary = System.Math.Max(0, System.Math.Max(1, power) - target.Damage);
        return assigningPlayer is not null && assigningPlayer != EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target)
            && HasControllerLethalDamage(state, assigningPlayer)
                ? target.DamageMarkedByPlayerIds?.Contains(assigningPlayer) == true ? 0 : System.Math.Min(1, ordinary)
                : ordinary;
    }

    private static int CombatBudgetForLethalDamage(CardObjectState target, int requiredDamage)
        // CN 467.2.c.4.a/c.5: choose the least original budget whose modified
        // assignment is lethal. Damage application still applies the modifier once.
        => target.UntilEndOfTurnEffects.Contains(DamageReceivedDoubledThisTurnEffectId, System.StringComparer.Ordinal)
            // A pending prevention is a separate replacement interaction; doubling
            // alone cannot justify reducing its existing assignment requirement.
            && !target.UntilEndOfTurnEffects.Contains(PreventNextDamageThisTurnEffectId, System.StringComparer.Ordinal)
            ? requiredDamage / 2 + requiredDamage % 2
            : requiredDamage;
}
