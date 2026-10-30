using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record EquipmentKeywordAuraContribution(string SourceId, RuleTextInstance Instance, int Index, StaticAuraSpec Aura);

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<EquipmentKeywordAuraContribution> EquipmentKeywordAuraContributions(
        MatchState state, CardObjectState target)
    {
        if (target.IsFaceDown || !target.Tags.Contains(CardObjectTags.EquipmentCard)
            || !IsObjectOnField(state.PlayerZones, target.ObjectId)) return [];
        var controller = EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target);
        if (!IsPlayerControlledFieldObject(state, controller, target.ObjectId)) return [];
        var contributions = new List<EquipmentKeywordAuraContribution>();
        foreach (var sourceId in PublicStaticAuraSourceObjectIds(state.PlayerZones))
        {
            if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.IsFaceDown
                || !IsPlayerControlledFieldObject(state, controller, sourceId)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                foreach (var (aura, index) in StaticAuraSpecRules.GetStaticAuras(instance.CardNo).Select((a, i) => (a, i)))
                    if (aura.Kind == StaticAuraKinds.FriendlyEquipmentKeyword
                        && aura.Layer == ContinuousEffectLayers.RuleText
                        && aura.TargetScope == StaticAuraTargetScopes.FriendlyEquipment
                        && aura.ParticipantScope == StaticAuraParticipantScopes.FriendlyPublicFieldEquipment
                        && aura.Duration == "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD"
                        && !string.IsNullOrWhiteSpace(aura.GrantedKeyword))
                        contributions.Add(new(source.ObjectId, instance, index, aura));
        }
        return contributions;
    }

    private static int ResolveEquipmentAuraResourceKeywordAmount(MatchState state, CardObjectState target, string keyword)
        => EquipmentKeywordAuraContributions(state, target).Sum(grant => GrantedResourceKeywordAmount(grant.Aura, keyword));
}

public sealed partial record MatchState
{
    private static IEnumerable<ContinuousEffectState> BuildEquipmentKeywordAuraEffects(MatchState state)
        => state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal)
            .SelectMany(target => CoreRuleEngine.EquipmentKeywordAuraContributions(state, target).Select(grant =>
                new ContinuousEffectState($"EQUIPMENT_KEYWORD_AURA:{grant.SourceId}:{grant.Instance.Id}:{grant.Index}:{target.ObjectId}:{grant.Aura.GrantedKeyword}",
                    "OBJECT", ContinuousEffectLayers.RuleText, grant.Aura.Duration, target.ObjectId, grant.SourceId,
                    EffectKind: grant.Aura.Kind, SourceCardNo: grant.Instance.CardNo,
                    SourcePath: "CoreRuleEngine.EquipmentKeywordAuraContributions")));
}
