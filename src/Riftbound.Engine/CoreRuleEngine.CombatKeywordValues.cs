using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Numeric keyword queries share the same printed, copied, aura and temporary
    // contributions as combat. A keyword's value exists outside its battle role.
    private static int BaseCombatKeywordValue(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones, string objectId, CardObjectState cardObject,
        string combatKeyword, bool isAttacking, int defendingUnitCount, string battlefieldId,
        string? battlefieldSteadfastObjectId = null, int battlefieldSteadfastKeywordBonus = 0)
    {
        var keywordBonus = ResolveCountedCombatKeywordFromLayer(
            state, playerZones, cardObject, battlefieldId, combatKeyword);
        if (isAttacking)
        {
            keywordBonus += CountLucianLegendEquipmentAssaultBonus(state, playerZones, objectId);
        }

        keywordBonus += ResolveBattlefieldIsolatedDefenderKeywordModifier(
            state,
            playerZones,
            cardObject,
            isAttacking,
            defendingUnitCount,
            battlefieldId,
            combatKeyword);

        if (!isAttacking
            && string.Equals(objectId, battlefieldSteadfastObjectId, StringComparison.Ordinal))
        {
            keywordBonus += battlefieldSteadfastKeywordBonus;
        }

        return keywordBonus;
    }

    internal static int CurrentCombatKeywordValue(MatchState state, CardObjectState card, string keyword)
    {
        if (!IsFaceUpNonStandbyUnit(card) || !IsObjectOnField(state.PlayerZones, card.ObjectId)) return 0;
        var evaluated = RecomputeRuleDerivedKeywords(state);
        card = evaluated.CardObjects[card.ObjectId];
        return CurrentCombatKeywordValueFromLayer(evaluated, evaluated.PlayerZones, card, keyword, []);
    }

    private static int CurrentCombatKeywordValueFromLayer(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, CardObjectState card, string keyword, HashSet<string> visiting)
    {
        if (!visiting.Add(keyword)) return 0;
        var field = state.ObjectLocations.GetValueOrDefault(card.ObjectId)?.BattlefieldObjectId ?? "";
        var defenders = card.IsDefending ? state.CardObjects.Values.Count(c => c.IsDefending
            && state.ObjectLocations.GetValueOrDefault(c.ObjectId)?.BattlefieldObjectId == field) : 0;
        var amount = BaseCombatKeywordValue(state, zones, card.ObjectId, card, keyword,
            keyword == CardCombatKeywordNames.Assault, defenders, field);
        foreach (var basis in TemporaryCombatKeywordRules.ValueSources(card, keyword))
            amount = checked(amount + CurrentCombatKeywordValueFromLayer(state, zones, card, basis, visiting));
        visiting.Remove(keyword);
        return Math.Max(0, amount);
    }

    private static int LinkedCombatKeywordValue(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, CardObjectState card, string keyword)
        => TemporaryCombatKeywordRules.ValueSources(card, keyword).Sum(basis =>
            CurrentCombatKeywordValueFromLayer(state, zones, card, basis, new HashSet<string>(StringComparer.Ordinal) { keyword }));
}
