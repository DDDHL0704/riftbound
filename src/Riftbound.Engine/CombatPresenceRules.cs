namespace Riftbound.Engine;

internal static class CombatPresenceRules
{
    // CN 740.2.c: the first battle spell duel is already a battle (466.1–2),
    // even before the later combat-damage state is opened.
    internal static bool IsInCombat(MatchState state, CardObjectState unit)
    {
        if (!(unit.IsAttacking || unit.IsDefending)
            || !state.ObjectLocations.TryGetValue(unit.ObjectId, out var location)
            || location.Zone != "BATTLEFIELD") return false;
        var battle = state.BattleState;
        return battle.IsActive && location.BattlefieldObjectId == battle.BattlefieldObjectId
            || CoreRuleEngine.IsEstablishedBattleDuelParticipant(state, unit.ObjectId);
    }
}
