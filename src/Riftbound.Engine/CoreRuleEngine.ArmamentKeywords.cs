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
                && !gear.IsFaceDown && !gear.Tags.Contains(CardObjectTags.Standby)
                && gear.Tags.Contains(CardObjectTags.EquipmentCard) && IsArmamentKeywordProvider(gear)
                && IsObjectOnField(state.PlayerZones, gear.ObjectId)
                && state.CardObjects.TryGetValue(host, out var unit) && !unit.IsFaceDown
                && !unit.Tags.Contains(CardObjectTags.Standby)
                && (unit.Tags.Contains(CardObjectTags.UnitCard) || unit.Tags.Contains(CardObjectTags.EquipmentCard))
                && IsObjectOnField(state.PlayerZones, host)
                && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host))
            .GroupBy(gear => gear.AttachedToObjectId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(gear => gear.ObjectId, StringComparer.Ordinal)
                .SelectMany(gear => (OfficialArmamentCatalog.Find(AttachedRuleTextRules.Face(gear))?.GrantedKeywords ?? [])
                    .Concat(provided.GetValueOrDefault(EffectiveFieldControllerId(state.PlayerZones, gear.ObjectId, gear)) ?? []))
                .ToArray(), StringComparer.Ordinal);
    }

    private static bool IsArmamentKeywordProvider(CardObjectState gear)
        => OfficialArmamentCatalog.Find(AttachedRuleTextRules.Face(gear)) is not null
            || gear.Tags.Contains(CardEquipmentKeywordNames.Weapon, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string[]> FriendlyArmamentProvidedKeywordGrants(MatchState state)
    {
        var grants = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var sourceId in PublicStaticAuraSourceObjectIds(state.PlayerZones))
        {
            if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.IsFaceDown
                || source.Tags.Contains(CardObjectTags.Standby)) continue;
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
