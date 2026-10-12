using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static IReadOnlyList<string> NonRuleDerivedTags(CardObjectState card)
    {
        if (card.RuleDerivedTags is not { Count: > 0 } derived) return card.Tags;
        var printed = AttachedRuleTextRules.PrintedKeywords(AttachedRuleTextRules.Face(card) ?? "");
        return card.Tags.Where(t => !derived.Contains(t) || printed.Contains(t) || card.GrantedTags?.Contains(t) == true).ToArray();
    }

    internal static MatchState RecomputeRuleDerivedKeywords(MatchState state)
    {
        var field = state.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields)).Distinct(StringComparer.Ordinal);
        var candidates = field.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && CardStaticAbilitySpecRules.TryGetStaticAbility(AttachedRuleTextRules.Face(card), CardStaticAbilitySpecRules.IsUnitPowerfulSelfKeywordsAbility, out _))
            .Concat(state.CardObjects.Values.Where(c => c.RuleDerivedTags is not null).Select(c => c.ObjectId))
            .Concat(state.CardObjects.Values.Where(c => c.AttachedRuleText is { } copy
                && CardStaticAbilitySpecRules.TryGetStaticAbility(copy.CardNo, CardStaticAbilitySpecRules.IsUnitPowerfulSelfKeywordsAbility, out _)
                && AttachedRuleTextRules.Bound(state, c)).Select(c => c.AttachedToObjectId!)).Distinct(StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return state;
        var grants = new Dictionary<string, (RuleTextInstance Instance, StaticAbilitySpec Spec)[]>();
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal);
        foreach (var id in candidates) {
            if (!state.CardObjects.TryGetValue(id, out var card)) continue;
            var specs = AttachedRuleTextRules.Instances(state, card).Select(instance =>
                CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo, CardStaticAbilitySpecRules.IsUnitPowerfulSelfKeywordsAbility, out var spec)
                    ? (instance, spec) : ((RuleTextInstance, StaticAbilitySpec)?)null).Where(s => s.HasValue).Select(s => s!.Value).ToArray();
            if (specs.Length == 0 && card.RuleDerivedTags is null) continue;
            // Independent printed/granted tags survive removal of this layer.
            var removable = (card.RuleDerivedTags ?? []).Concat(specs.SelectMany(s => s.Item2.GrantedKeywords ?? [])).ToHashSet(StringComparer.Ordinal);
            var printed = AttachedRuleTextRules.PrintedKeywords(AttachedRuleTextRules.Face(card) ?? "");
            var basis = card.Tags.Where(t => !removable.Contains(t) || printed.Contains(t) || card.GrantedTags?.Contains(t) == true)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            cards[card.ObjectId] = card with { Tags = basis, RuleDerivedTags = null };
            if (specs.Length > 0 && IsFaceUpNonStandbyUnit(card) && IsObjectOnField(state.PlayerZones, card.ObjectId)) grants[card.ObjectId] = specs;
        }
        if (grants.Count == 0 && cards.All(e => e.Value == state.CardObjects[e.Key])) return state;
        var current = state with { CardObjects = cards };
        // Known Powerful-self grants only add keywords. Begin without this layer
        // so a conditional Shield cannot bootstrap its own enabling threshold.
        for (var pass = 0; pass <= grants.Sum(e => e.Value.Length); pass++) {
            var changed = false; var next = new Dictionary<string, CardObjectState>(current.CardObjects, StringComparer.Ordinal);
            foreach (var (id, specs) in grants) {
                var card = current.CardObjects[id]; var might = ResolveCurrentFieldUnitPowerFromEvaluatedKeywords(current, card);
                var keywords = specs.Where(s => might >= s.Spec.RequiredPowerThreshold).SelectMany(s => s.Spec.GrantedKeywords ?? []).ToArray();
                var tags = keywords.Where(k => k is not (CardCombatKeywordNames.Steadfast or CardObjectTags.Spellshield)).Distinct(StringComparer.Ordinal).ToList();
                foreach (var counted in new[] { CardCombatKeywordNames.Steadfast, CardObjectTags.Spellshield }) {
                    var amount = keywords.Count(k => k == counted);
                    if (amount > 0) tags.Add(counted + (amount == 1 ? "" : amount));
                }
                var derived = tags.Order(StringComparer.Ordinal).ToArray();
                if ((card.RuleDerivedTags ?? []).SequenceEqual(derived)) continue;
                next[id] = card with { RuleDerivedTags = derived.Length == 0 ? null : derived,
                    Tags = cards[id].Tags.Concat(derived).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                changed = true;
            }
            if (!changed) break;
            current = current with { CardObjects = next };
        }
        return current.CardObjects.All(e => e.Value.Tags.SequenceEqual(state.CardObjects[e.Key].Tags)
            && (e.Value.RuleDerivedTags ?? []).SequenceEqual(state.CardObjects[e.Key].RuleDerivedTags ?? [])) ? state : current;
    }

    private static ResolutionResult RefreshRuleDerivedKeywordLayers(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = RecomputeRuleDerivedKeywords(result.State);
        return ReferenceEquals(state, result.State) ? result : result with { State = state,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
