using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 719: each weapon's lower-box keywords belong to its host. A
    // continuous "your weapons provide" grant follows the weapon's controller.
    private static IReadOnlyDictionary<string, string[]> AttachedArmamentKeywordGrants(MatchState state)
    {
        var provided = FriendlyArmamentProvidedKeywordGrants(state);
        return state.CardObjects.Values.Where(gear => gear.AttachedToObjectId is { } host
                && !gear.IsFaceDown
                && gear.Tags.Contains(CardObjectTags.EquipmentCard) && IsArmamentKeywordProvider(gear)
                && IsObjectOnField(state.PlayerZones, gear.ObjectId)
                && state.CardObjects.TryGetValue(host, out var unit) && !unit.IsFaceDown
                && (unit.Tags.Contains(CardObjectTags.UnitCard) || unit.Tags.Contains(CardObjectTags.EquipmentCard))
                && IsObjectOnField(state.PlayerZones, host)
                && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host))
            .GroupBy(gear => gear.AttachedToObjectId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(gear => gear.ObjectId, StringComparer.Ordinal)
                .SelectMany(gear => (OfficialArmamentCatalog.Find(AttachedRuleTextRules.Face(gear))?.GrantedKeywords ?? [])
                    .Concat(provided.GetValueOrDefault(EffectiveFieldControllerId(state.PlayerZones, gear.ObjectId, gear)) ?? []))
                .ToArray(), StringComparer.Ordinal);
    }

    // CN 136.2/718.5.e-g/719: lower text belongs to the current host;
    // its "you" and "here" follow that host, independently of gear control.
    private static IReadOnlyDictionary<string, string[]> AttachedArmamentTraitAndHereGrants(MatchState state)
    {
        var grants = new List<(string Target, string Tag)>();
        foreach (var gear in state.CardObjects.Values.OrderBy(card => card.ObjectId, StringComparer.Ordinal))
        {
            if (gear.AttachedToObjectId is not { } hostId || gear.IsFaceDown
                || !gear.Tags.Contains(CardObjectTags.EquipmentCard)
                || !IsObjectOnField(state.PlayerZones, gear.ObjectId)
                || !state.CardObjects.TryGetValue(hostId, out var host) || host.IsFaceDown
                || !(host.Tags.Contains(CardObjectTags.UnitCard) || host.Tags.Contains(CardObjectTags.EquipmentCard))
                || !IsObjectOnField(state.PlayerZones, hostId)
                || !IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, hostId)
                || OfficialArmamentCatalog.Find(AttachedRuleTextRules.Face(gear)) is not { } definition) continue;
            grants.AddRange((definition.GrantedTraits ?? []).Select(trait => (hostId, trait)));
            if (definition.FriendlyUnitsHereKeywords is not { Count: > 0 } aura) continue;
            var controller = EffectiveFieldControllerId(state.PlayerZones, hostId, host);
            foreach (var target in state.CardObjects.Values.Where(target => IsFaceUpNonStandbyUnit(target)
                && IsObjectOnField(state.PlayerZones, target.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target) == controller
                && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, hostId, target.ObjectId))
                .OrderBy(target => target.ObjectId, StringComparer.Ordinal))
                grants.AddRange(aura.Select(keyword => (target.ObjectId, keyword)));
        }
        return grants.GroupBy(grant => grant.Target, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(grant => grant.Tag).ToArray(), StringComparer.Ordinal);
    }

    private static bool IsArmamentKeywordProvider(CardObjectState gear)
        => OfficialArmamentCatalog.Find(AttachedRuleTextRules.Face(gear)) is not null
            || gear.Tags.Contains(CardEquipmentKeywordNames.Weapon, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string[]> FriendlyArmamentProvidedKeywordGrants(MatchState state)
    {
        var grants = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var sourceId in PublicStaticAuraSourceObjectIds(state.PlayerZones))
        {
            if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.IsFaceDown) continue;
            var controller = EffectivePublicStaticAuraSourceControllerId(state.PlayerZones, sourceId, source);
            if (string.IsNullOrWhiteSpace(controller)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                foreach (var aura in StaticAuraSpecRules.GetStaticAuras(instance.CardNo))
                {
                    if (aura.Kind != StaticAuraKinds.FriendlyArmamentProvidedKeyword
                        || aura.Layer != ContinuousEffectLayers.RuleText
                        || aura.Duration != "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD"
                        || aura.TargetScope != StaticAuraTargetScopes.FriendlyEquipment
                        || aura.ParticipantScope != StaticAuraParticipantScopes.FriendlyPublicFieldEquipment
                        || aura.TargetFilter != StaticAuraTargetFilters.TagPrefix + CardEquipmentKeywordNames.Weapon
                        || string.IsNullOrWhiteSpace(aura.GrantedKeyword)) continue;
                    if (!grants.TryGetValue(controller, out var keywords))
                        grants[controller] = keywords = [];
                    keywords.Add(aura.GrantedKeyword);
                }
        }
        return grants.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }
}
