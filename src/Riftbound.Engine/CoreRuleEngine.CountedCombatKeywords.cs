using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static int ResolveCurrentCountedCombatKeyword(MatchState state, CardObjectState unit, string keyword)
    {
        var evaluated = RecomputeRuleDerivedKeywords(state);
        unit = evaluated.CardObjects.GetValueOrDefault(unit.ObjectId) ?? unit;
        var field = evaluated.ObjectLocations.GetValueOrDefault(unit.ObjectId)?.BattlefieldObjectId ?? "";
        return ResolveCountedCombatKeywordFromLayer(evaluated, evaluated.PlayerZones, unit, field, keyword);
    }

    private static int ResolveCountedCombatKeywordFromLayer(MatchState state, IReadOnlyDictionary<string, PlayerZones> zones,
        CardObjectState unit, string field, string keyword)
    {
        var amount = CombatKeywordAmount(NonRuleDerivedTags(unit), keyword)
            + CombatKeywordAmount(unit.RuleDerivedTags ?? [], keyword);
        amount = Math.Max(amount, ResolveSourceObjectKeywordStaticAuraBonus(state, unit, keyword));
        amount = Math.Max(amount, ResolveBattlefieldKeywordStaticAuraBonus(state, zones, field, unit, keyword));
        amount = Math.Max(amount, ResolveFriendlyFilteredUnitsKeywordBonus(state, zones, unit.ObjectId, unit, keyword));
        // Numeric keywords from other friendly units are independent grants (CN 814.2).
        amount += ResolveSameBattlefieldOtherFriendlyUnitsKeywordBonus(state, zones, unit.ObjectId, unit, keyword)
            + ResolveSameBattlefieldFriendlyKeywordBonus(state, unit, keyword)
            + SnapshotKeywordGrantRules.Amount(unit, keyword)
            + TemporaryCombatKeywordRules.Amount(unit, keyword)
            + CopiedRuleTextKeywordAmount(state, unit, keyword);
        // The granted text defines a live value. Later Steadfast grants, copied
        // text and auras change each independent instance's Assault amount.
        if (keyword == CardCombatKeywordNames.Assault
            && TemporaryCombatKeywordRules.AssaultEqualToSteadfastGrants(unit) is var grants && grants > 0)
            amount += grants * ResolveCountedCombatKeywordFromLayer(state, zones, unit, field, CardCombatKeywordNames.Steadfast);
        return amount;
    }
}
