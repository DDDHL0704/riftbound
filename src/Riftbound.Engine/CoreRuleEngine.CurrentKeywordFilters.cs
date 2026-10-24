using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool IsCurrentKeywordPowerFilter(StaticAuraSpec aura)
        => aura.Kind == StaticAuraKinds.BattlefieldFilteredUnitsPower
            && StaticAuraSpecRules.IsBattlefieldFilteredUnitsPowerStaticAura(aura)
            && aura.TargetFilter?.StartsWith(StaticAuraTargetFilters.CurrentKeywordPrefix, StringComparison.Ordinal) == true;

    // Read the supplied keyword layer. In particular, do not recalculate Might
    // or keywords here: this query also runs inside keyword-layer evaluation.
    // The first supported current-possession filter is boolean Bulwark (CN815).
    internal static (bool Matches, IReadOnlyList<string> PublicKeywordSourceIds) InspectCurrentKeywordFilter(
        MatchState layerState, StaticAuraSpec aura, CardObjectState target)
    {
        if (!IsCurrentKeywordPowerFilter(aura)
            || aura.TargetFilter != StaticAuraTargetFilters.CurrentKeywordPrefix + CardCombatKeywordNames.Bulwark
            || !layerState.CardObjects.TryGetValue(target.ObjectId, out var current)
            || current.ObjectGeneration != target.ObjectGeneration
            || !IsFaceUpNonStandbyUnit(current)
            || !IsObjectOnField(layerState.PlayerZones, current.ObjectId)
            || !IsCurrentPublicKeywordSource(layerState, current)) return (false, []);

        target = current;
        var keyword = CardCombatKeywordNames.Bulwark;
        var matches = HasExactCombatKeyword(target.Tags, keyword) || TemporaryCombatKeywordRules.Has(target, keyword);
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instance in AttachedRuleTextRules.Instances(layerState, target))
        {
            if (!IsCurrentKeywordRuleInstance(layerState, instance)
                || CopiedRuleTextKeywordAmount(layerState, target, instance, keyword) <= 0) continue;
            matches = true;
            if (instance.Origin is { } origin) sources.Add(origin.Equipment.ObjectId);
        }

        foreach (var grant in CurrentPublicKeywordGrants(layerState, target, keyword))
        {
            matches = true;
            sources.Add(grant.SourceId);
            if (grant.Instance.Origin is { } origin) sources.Add(origin.Equipment.ObjectId);
        }

        return (matches, sources.Where(id => layerState.CardObjects.TryGetValue(id, out var source)
                && IsCurrentPublicKeywordSource(layerState, source))
            .Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsCurrentKeywordRuleInstance(MatchState state, RuleTextInstance instance)
        => instance.Origin is not { } origin
            || origin.Equipment is not null && origin.Unit is not null
                && state.CardObjects.TryGetValue(origin.Equipment.ObjectId, out var gear)
                && gear.ObjectGeneration == origin.Equipment.Generation && gear.AttachmentRevision == origin.Revision
                && gear.AttachedToObjectId == origin.Unit.ObjectId && IsCurrentPublicKeywordSource(state, gear)
                && state.CardObjects.TryGetValue(origin.Unit.ObjectId, out var unit) && unit.ObjectGeneration == origin.Unit.Generation;

    private static bool IsCurrentPublicKeywordSource(MatchState state, CardObjectState source)
    {
        if (source.IsFaceDown) return false;
        foreach (var (player, zones) in state.PlayerZones)
        {
            var zone = zones.Base.Contains(source.ObjectId, StringComparer.Ordinal) ? "BASE"
                : zones.Battlefields.Contains(source.ObjectId, StringComparer.Ordinal) ? "BATTLEFIELD"
                : zones.LegendZone.Contains(source.ObjectId, StringComparer.Ordinal) ? "LEGEND" : null;
            if (zone is not null && (!state.ObjectLocations.TryGetValue(source.ObjectId, out var at)
                || at.PlayerId == player && at.Zone == zone)) return true;
        }
        return false;
    }

    private static IEnumerable<(string SourceId, RuleTextInstance Instance, StaticAuraSpec Aura)> CurrentPublicKeywordGrants(
        MatchState state, CardObjectState target, string keyword)
    {
        var zones = state.PlayerZones;
        var controller = EffectiveFieldControllerId(zones, target.ObjectId, target);
        var battlefield = BattlefieldLocalRules.AtUnit(state, target.ObjectId);
        foreach (var id in PublicStaticAuraSourceObjectIds(zones))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsCurrentPublicKeywordSource(state, source)) continue;
            var friendly = !string.IsNullOrWhiteSpace(controller)
                && EffectivePublicStaticAuraSourceControllerId(zones, id, source) == controller;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (!IsCurrentKeywordRuleInstance(state, instance)) continue;
                foreach (var aura in StaticAuraSpecRules.GetStaticAuras(instance.CardNo))
                {
                    if (GrantedCombatKeywordAmount(aura, keyword) <= 0) continue;
                    var applies = friendly && StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura(aura)
                        && PublicFieldFriendlyKeywordStaticAuraAppliesToParticipant(aura, id, target.ObjectId, target);
                    applies |= friendly && id != target.ObjectId && source.Tags.Contains(CardObjectTags.UnitCard)
                        && battlefield is not null && BattlefieldLocalRules.AtUnit(state, id)?.ObjectId == battlefield.ObjectId
                        && StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyKeywordStaticAura(aura)
                        && SameBattlefieldOtherFriendlyKeywordStaticAuraAppliesToParticipant(aura, target);
                    applies |= battlefield?.ObjectId == id && StaticAuraSpecRules.IsBattlefieldKeywordStaticAura(aura)
                        && BattlefieldStaticAuraAppliesToParticipant(state, aura, target);
                    if (applies) yield return (id, instance, aura);
                }
            }
        }
    }
}
