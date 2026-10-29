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
        if (DamageImmunityRules.CannotTakeDamage(state, target)) return 0;
        var ordinary = System.Math.Max(0, System.Math.Max(1, power) - target.Damage);
        return assigningPlayer is not null && assigningPlayer != EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target)
            && HasControllerLethalDamage(state, assigningPlayer)
                ? target.DamageMarkedByPlayerIds?.Contains(assigningPlayer) == true ? 0 : System.Math.Min(1, ordinary)
                : ordinary;
    }
}
