using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record FriendlyResourceKeywordAuraContribution(string SourceId, StaticAuraSpec Aura, int Amount);

public sealed partial class CoreRuleEngine
{
    private static int ResourceKeywordAmountWithoutFriendlyAuras(MatchState state, CardObjectState card, string keyword)
        => CardCombatKeywordRules.KeywordAmount(NonRuleDerivedTags(card), keyword)
            + CardCombatKeywordRules.KeywordAmount(card.RuleDerivedTags ?? [], keyword)
            + SnapshotKeywordGrantRules.Amount(card, keyword)
            + TemporaryCombatKeywordRules.Amount(card, keyword)
            + CopiedRuleTextKeywordAmount(state, card, keyword)
            + ResolveSourceObjectResourceKeywordAmount(state, card, keyword)
            + ResolveEquipmentAuraResourceKeywordAmount(state, card, keyword);

    // CN 480-481: an "if it doesn't already have" grant depends on the
    // independent keyword grants. Apply those first; then only one identical
    // missing-keyword grant can contribute. Nothing is written to the card.
    internal static IReadOnlyList<FriendlyResourceKeywordAuraContribution> FriendlyResourceKeywordAuraContributions(
        MatchState state, IReadOnlyDictionary<string, PlayerZones> zones, CardObjectState target, string keyword)
    {
        if (!IsFaceUpNonStandbyUnit(target) || !IsObjectOnField(zones, target.ObjectId)) return [];
        var controller = EffectiveFieldControllerId(zones, target.ObjectId, target);
        if (string.IsNullOrWhiteSpace(controller)) return [];
        var battlefield = state.ObjectLocations.GetValueOrDefault(target.ObjectId) is { Zone: "BATTLEFIELD" } at
            ? at.BattlefieldObjectId : null;
        var candidates = new List<FriendlyResourceKeywordAuraContribution>();
        foreach (var id in PublicStaticAuraSourceObjectIds(zones)) {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.IsFaceDown
                || EffectivePublicStaticAuraSourceControllerId(zones, id, source) != controller) continue;
            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(source.CardNo)) {
                var applies = StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura(aura)
                    && PublicFieldFriendlyKeywordStaticAuraAppliesToParticipant(aura, id, target.ObjectId, target);
                applies |= id != target.ObjectId && battlefield is not null
                    && source.Tags.Contains(CardObjectTags.UnitCard) && IsObjectOnField(zones, id)
                    && state.ObjectLocations.GetValueOrDefault(id) is { Zone: "BATTLEFIELD" } sourceAt
                    && sourceAt.BattlefieldObjectId == battlefield
                    && StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyKeywordStaticAura(aura)
                    && SameBattlefieldOtherFriendlyKeywordStaticAuraAppliesToParticipant(aura, target);
                if (applies && GrantedResourceKeywordAmount(aura, keyword) is > 0 and var amount)
                    candidates.Add(new(id, aura, amount));
            }
        }
        var contributions = candidates.Where(c => c.Aura.OnlyIfMissingKeyword != true).ToList();
        if (candidates.FirstOrDefault(c => c.Aura.OnlyIfMissingKeyword == true) is { } conditional
            && contributions.Sum(c => c.Amount) == 0 && ResourceKeywordAmountWithoutFriendlyAuras(state, target, keyword) == 0)
            contributions.Add(conditional);
        return contributions;
    }

    internal static bool MissingResourceKeywordAuraApplies(MatchState state, string sourceId, string targetId, StaticAuraSpec aura)
        => aura.OnlyIfMissingKeyword != true || state.CardObjects.TryGetValue(targetId, out var target)
            && FriendlyResourceKeywordAuraContributions(state, state.PlayerZones, target, CardResourceKeywordNames.Spellshield)
                .Any(c => c.SourceId == sourceId && c.Aura == aura);
}
