namespace Riftbound.Engine;

public sealed record CombatSpellCostAdjustmentSpec(int FriendlyManaReduction,
    int FriendlyGenericPowerReduction, int MinimumFriendlyMana, int EnemyManaIncrease, int EnemyGenericPowerIncrease);

public sealed partial class CoreRuleEngine
{
    // CN466.2/740.2.c: the first battle spell duel already has combat roles.
    // A role flag without the corresponding live battle and location is insufficient.
    private static bool IsSpellCostSourceInBattle(MatchState state, CardObjectState source)
    {
        if (!IsFaceUpNonStandbyUnit(source)) return false;
        if (IsEstablishedBattleDuelParticipant(state, source.ObjectId)) return true;
        var battle = state.BattleState;
        return battle.IsActive && battle.BattlefieldObjectId is { } battlefield
            && (battle.AttackerObjectIds.Contains(source.ObjectId) || battle.DefenderObjectIds.Contains(source.ObjectId))
            && IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, source.ObjectId, battlefield);
    }

    private static IEnumerable<(string Controller, CombatSpellCostAdjustmentSpec Spec)> CombatSpellCostSources(MatchState state)
    {
        foreach (var id in PublicStaticAuraSourceObjectIds(state.PlayerZones))
        {
            if (!state.CardObjects.TryGetValue(id, out var source)) continue;
            var controller = EffectiveFieldControllerId(state.PlayerZones, id, source);
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                if (CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior)
                    && behavior.CombatSpellCostAdjustment is { } spec)
                {
                    // Most sources have no such profile. Avoid parsing their
                    // battle context on every ordinary play-cost query.
                    if (!IsSpellCostSourceInBattle(state, source)) break;
                    yield return (controller, spec);
                }
        }
    }

    private static IReadOnlyList<CombatSpellCostAdjustmentSpec> FriendlyCombatSpellCostReducers(
        MatchState state, string player, CardBehaviorDefinition behavior)
        => !IsSpellPlayBehavior(behavior) || EffectPlayIgnoresAllCosts(state, player) ? []
            : CombatSpellCostSources(state).Where(source => source.Controller == player).Select(source => source.Spec).ToArray();
}
