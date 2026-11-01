using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // A public permanent can grant keywords to friendly units at its current
    // battlefield, including itself when it is also a unit (CN356.2, CN359.3.f.2).
    internal static IEnumerable<(string SourceId, string InstanceId, string CardNo, StaticAuraSpec Aura)> SameBattlefieldFriendlyKeywordAuras(
        MatchState state, CardObjectState target)
    {
        if (!IsFaceUpNonStandbyUnit(target) || !IsCurrentPublicKeywordSource(state, target)
            || !state.ObjectLocations.TryGetValue(target.ObjectId, out var at) || at.Zone != "BATTLEFIELD") yield break;
        var controller = EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target);
        foreach (var id in PublicStaticAuraSourceObjectIds(state.PlayerZones))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsCurrentPublicKeywordSource(state, source)
                || EffectivePublicStaticAuraSourceControllerId(state.PlayerZones, id, source) != controller
                || !state.ObjectLocations.TryGetValue(id, out var here) || here.Zone != "BATTLEFIELD"
                || here.BattlefieldObjectId != at.BattlefieldObjectId) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (!IsCurrentKeywordRuleInstance(state, instance)) continue;
                foreach (var aura in StaticAuraSpecRules.GetStaticAuras(instance.CardNo)
                    .Where(StaticAuraSpecRules.IsSameBattlefieldFriendlyKeywordAura))
                    yield return (id, instance.Id, instance.CardNo, aura);
            }
        }
    }

    private static int ResolveSameBattlefieldFriendlyKeywordBonus(MatchState state, CardObjectState target, string keyword)
        => SameBattlefieldFriendlyKeywordAuras(state, target)
            .Sum(grant => GrantedCombatKeywordAmount(grant.Aura, keyword));
}
