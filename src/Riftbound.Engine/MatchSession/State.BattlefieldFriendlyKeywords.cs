using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record MatchState
{
    private static IEnumerable<ContinuousEffectState> BuildSameBattlefieldFriendlyKeywordAuraEffects(MatchState state)
    {
        foreach (var target in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
            foreach (var grant in CoreRuleEngine.SameBattlefieldFriendlyKeywordAuras(state, target))
                yield return new ContinuousEffectState(
                    $"RULE_TEXT:SAME_BATTLEFIELD_FRIENDLY_UNITS_KEYWORD:{grant.SourceId}:{grant.InstanceId}:{target.ObjectId}:{grant.Aura.GrantedKeyword}",
                    "OBJECT", ContinuousEffectLayers.RuleText, grant.Aura.Duration, target.ObjectId, grant.SourceId,
                    SourceCardNo: grant.CardNo);
    }
}
