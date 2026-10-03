namespace Riftbound.Engine;

internal static class StandardMovementCostRules
{
    // CN UNL-163/219: every opposing Patrol at the destination adds A per extra unit.
    public static int PowerPerExtraUnit(MatchState state, string playerId, string? battlefieldId) =>
        battlefieldId is null || !state.BattlefieldStates.TryGetValue(battlefieldId, out var battlefield) ? 0
            : battlefield.OccupantObjectIds.Count(id => state.CardObjects.TryGetValue(id, out var card)
                && !CardControlRules.IsControlledByPlayerOrLegacyOwned(card, playerId)
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? string.Empty, out var behavior)
                && behavior.EffectKind == "MAGESEEKER_PATROL_MOVE_TAX_STATIC");
}
