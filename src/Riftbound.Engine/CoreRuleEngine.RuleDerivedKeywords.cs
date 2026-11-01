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
        => RecomputeDisarmKeywords(RecomputeConditionalRuleDerivedKeywords(state));

    private static MatchState RecomputeConditionalRuleDerivedKeywords(MatchState state)
    {
        var armamentKeywords = AttachedArmamentKeywordGrants(state);
        var field = state.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields)).Distinct(StringComparer.Ordinal);
        var candidates = field.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && CardStaticAbilitySpecRules.TryGetStaticAbility(AttachedRuleTextRules.Face(card), CardStaticAbilitySpecRules.IsUnitConditionalSelfKeywordsAbility, out _))
            .Concat(armamentKeywords.Keys)
            .Concat(state.CardObjects.Values.Where(c => c.RuleDerivedTags is not null).Select(c => c.ObjectId))
            .Concat(state.CardObjects.Values.Where(c => c.IsEmpowered && c.Tags.Contains(CardObjectTags.UnitCard)).Select(c => c.ObjectId))
            .Concat(state.CardObjects.Values.Where(c => c.AttachedRuleText is { } copy
                && CardStaticAbilitySpecRules.TryGetStaticAbility(copy.CardNo, CardStaticAbilitySpecRules.IsUnitConditionalSelfKeywordsAbility, out _)
                && AttachedRuleTextRules.Bound(state, c)).Select(c => c.AttachedToObjectId!)).Distinct(StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return state;
        var grants = new Dictionary<string, (RuleTextInstance Instance, StaticAbilitySpec Spec)[]>();
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal);
        foreach (var id in candidates) {
            if (!state.CardObjects.TryGetValue(id, out var card)) continue;
            var specs = AttachedRuleTextRules.Instances(state, card).Select(instance =>
                CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo, CardStaticAbilitySpecRules.IsUnitConditionalSelfKeywordsAbility, out var spec)
                    ? (instance, spec) : ((RuleTextInstance, StaticAbilitySpec)?)null).Where(s => s.HasValue).Select(s => s!.Value).ToArray();
            if (specs.Length == 0 && card.RuleDerivedTags is null && EmpowerRules.Keywords(state, card).Length == 0
                && !armamentKeywords.ContainsKey(id)) continue;
            // Independent printed/granted tags survive removal of this layer.
            var removable = (card.RuleDerivedTags ?? []).Concat(specs.SelectMany(s => s.Item2.GrantedKeywords ?? [])).ToHashSet(StringComparer.Ordinal);
            var printed = AttachedRuleTextRules.PrintedKeywords(AttachedRuleTextRules.Face(card) ?? "");
            var basis = card.Tags.Where(t => !removable.Contains(t) || printed.Contains(t) || card.GrantedTags?.Contains(t) == true)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            cards[card.ObjectId] = card with { Tags = basis, RuleDerivedTags = null };
            if ((specs.Length > 0 || EmpowerRules.Keywords(state, card).Length > 0 || armamentKeywords.ContainsKey(id))
                && IsFaceUpNonStandbyUnit(card) && IsObjectOnField(state.PlayerZones, card.ObjectId)) grants[card.ObjectId] = specs;
        }
        if (grants.Count == 0 && cards.All(e => e.Value == state.CardObjects[e.Key])) return state;
        var current = state with { CardObjects = cards };
        // Known Powerful-self grants only add keywords. Begin without this layer
        // so a conditional Shield cannot bootstrap its own enabling threshold.
        for (var pass = 0; pass <= grants.Sum(e => e.Value.Length); pass++) {
            var changed = false; var next = new Dictionary<string, CardObjectState>(current.CardObjects, StringComparer.Ordinal);
            foreach (var (id, specs) in grants) {
                var card = current.CardObjects[id]; var might = ResolveCurrentFieldUnitPowerFromEvaluatedKeywords(current, card);
                var keywords = specs.Where(s => ConditionalSelfKeywordsApply(current, card, s.Spec, might))
                    .SelectMany(s => s.Spec.Kind == StaticAbilityKinds.UnitEquipmentCountSelfKeywords
                        ? Enumerable.Repeat(CardCombatKeywordNames.Assault,
                            CountControlledPublicFieldEquipmentObjects(current.PlayerZones, current.CardObjects,
                                EffectiveFieldControllerId(current.PlayerZones, id, card)))
                        : s.Spec.GrantedKeywords ?? [])
                    .Concat(EmpowerRules.Keywords(current, card))
                    .Concat(armamentKeywords.GetValueOrDefault(id) ?? []).ToArray();
                var countedKeywords = new[] { CardCombatKeywordNames.Assault, CardCombatKeywordNames.Steadfast, CardObjectTags.Spellshield };
                var tags = keywords.Where(k => !countedKeywords.Any(name => k == name
                    || k.StartsWith(name, StringComparison.Ordinal) && int.TryParse(k[name.Length..], out _)))
                    .Distinct(StringComparer.Ordinal).ToList();
                foreach (var counted in new[] { CardCombatKeywordNames.Assault, CardCombatKeywordNames.Steadfast, CardObjectTags.Spellshield }) {
                    var amount = keywords.Sum(k => CombatKeywordAmount([k], counted));
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

    private static bool ConditionalSelfKeywordsApply(MatchState state, CardObjectState source, StaticAbilitySpec spec, int might)
    {
        if (CardStaticAbilitySpecRules.IsUnitPowerfulSelfKeywordsAbility(spec)) return might >= spec.RequiredPowerThreshold;
        if (spec.Kind == StaticAbilityKinds.UnitEquipmentCountSelfKeywords) return true;
        return spec.Kind == StaticAbilityKinds.UnitExactOtherFriendlyBattlefieldKeywords
            && HasExactOtherFriendlyBattlefieldUnitCount(state, source, spec.RequiredExactOtherFriendlyBattlefieldUnits);
    }

    private static bool HasExactOtherFriendlyBattlefieldUnitCount(MatchState state, CardObjectState source, int? required)
    {
        if (required is null || !IsFaceUpNonStandbyUnit(source) || !IsObjectOnField(state.PlayerZones, source.ObjectId)
            || !state.ObjectLocations.TryGetValue(source.ObjectId, out var sourceAt) || sourceAt.Zone != "BATTLEFIELD"
            || sourceAt.BattlefieldObjectId is null || BattlefieldLocalRules.Battlefield(state, sourceAt.BattlefieldObjectId) is null) return false;
        var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source);
        var count = state.CardObjects.Values.Count(card => card.ObjectId != source.ObjectId && !card.IsFaceDown && card.Tags.Contains(CardObjectTags.UnitCard)
            && CardControlRules.IsControlledByPlayerOrLegacyOwned(card, controller)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
            && at.BattlefieldObjectId == sourceAt.BattlefieldObjectId);
        return count == required;
    }

    private static bool HasFriendlyEquipmentHere(MatchState state, CardObjectState source)
    {
        if (!IsObjectOnField(state.PlayerZones, source.ObjectId)
            || !state.ObjectLocations.TryGetValue(source.ObjectId, out var here) || here.Zone is not ("BASE" or "BATTLEFIELD")) return false;
        var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source);
        return state.CardObjects.Values.Any(card => !card.IsFaceDown && card.Tags.Contains(CardObjectTags.EquipmentCard)
            && CardControlRules.IsControlledByPlayerOrLegacyOwned(card, controller)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var at) && at.Zone == here.Zone
            && (here.Zone == "BASE" ? at.PlayerId == here.PlayerId : at.BattlefieldObjectId == here.BattlefieldObjectId));
    }

    private static bool CanDealCombatDamage(MatchState state, CardObjectState source)
        => !AttachedRuleTextRules.Instances(state, source).Any(instance =>
            CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo,
                ability => ability.Kind is StaticAbilityKinds.UnitCannotDealCombatDamage
                    or StaticAbilityKinds.UnitExactOtherFriendlyBattlefieldCombatDamage
                    or StaticAbilityKinds.UnitFriendlyEquipmentHereCombatDamage, out var spec)
            && (spec.Kind == StaticAbilityKinds.UnitCannotDealCombatDamage
                || (spec.Kind == StaticAbilityKinds.UnitFriendlyEquipmentHereCombatDamage
                    ? !HasFriendlyEquipmentHere(state, source)
                    : !HasExactOtherFriendlyBattlefieldUnitCount(state, source, spec.RequiredExactOtherFriendlyBattlefieldUnits))));

    private static ResolutionResult RefreshRuleDerivedKeywordLayers(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = RecomputeRuleDerivedKeywords(result.State);
        return ReferenceEquals(state, result.State) ? result : result with { State = state,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
