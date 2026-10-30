using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static int ResolveCurrentCountedCombatKeyword(MatchState state, CardObjectState unit, string keyword)
        => CurrentCombatKeywordValue(state, unit, keyword);

    private static int ResolveCountedCombatKeywordFromLayer(MatchState state, IReadOnlyDictionary<string, PlayerZones> zones,
        CardObjectState unit, string field, string keyword)
    {
        var amount = CombatKeywordAmount(NonRuleDerivedTags(unit), keyword)
            + CombatKeywordAmount(unit.RuleDerivedTags ?? [], keyword);
        amount = Math.Max(amount, ResolveSourceObjectKeywordStaticAuraBonus(state, unit, keyword));
        amount = Math.Max(amount, ResolveSameBattlefieldOtherFriendlyUnitsKeywordBonus(state,
            zones, unit.ObjectId, unit, keyword));
        amount = Math.Max(amount, ResolveBattlefieldKeywordStaticAuraBonus(state, zones, field, unit, keyword));
        amount = Math.Max(amount, ResolveFriendlyFilteredUnitsKeywordBonus(state, zones, unit.ObjectId, unit, keyword));
        return amount + TemporaryCombatKeywordRules.Amount(unit, keyword)
            + CopiedRuleTextKeywordAmount(state, unit, keyword);
    }
}
