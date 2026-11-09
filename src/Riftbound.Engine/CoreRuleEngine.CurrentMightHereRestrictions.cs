using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool IsTargetProtectedByHigherMightFriendlyUnitHere(MatchState state,
        string targetingPlayer, string targetId)
        => !string.IsNullOrWhiteSpace(targetingPlayer)
            && state.CardObjects.TryGetValue(targetId, out var target)
            && EffectiveFieldControllerId(state.PlayerZones, targetId, target) != targetingPlayer
            && HasHigherMightHereStaticAbility(state, target,
                StaticAbilityKinds.HereLowerMightFriendlyUnitEnemyTargetProtection, friendlySources: true);

    private static bool IsCombatDamagePreventedByHigherMightEnemyHere(MatchState state, CardObjectState target)
        => HasHigherMightHereStaticAbility(state, target,
            StaticAbilityKinds.HereLowerMightEnemyUnitCombatDamagePrevention, friendlySources: false);

    // CN359.3.f.2 and UNL judge FAQ260511 p23: compare current Might and
    // current control at each use. An earned spell can lose its legal target
    // after the protecting unit grows in response. Combat damage suppression
    // never changes either unit's actual Might or its lethal threshold.
    private static bool HasHigherMightHereStaticAbility(MatchState state, CardObjectState target,
        string kind, bool friendlySources)
    {
        if (!IsFaceUpNonStandbyUnit(target) || !IsObjectOnField(state.PlayerZones, target.ObjectId)) return false;
        var controller = EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target);
        var sources = state.CardObjects.Values.Where(source => source.ObjectId != target.ObjectId
            && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && (EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) == controller) == friendlySources
            && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, source.ObjectId, target.ObjectId)
            && AttachedRuleTextRules.Instances(state, source).Any(instance =>
                CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo, ability => ability.Kind == kind, out _))).ToArray();
        if (sources.Length == 0) return false;
        var evaluated = RecomputeRuleDerivedKeywords(state);
        // This path uses damageContribution:false inside current-Might
        // calculation, so opposing damage-prevention sources cannot recurse.
        var targetPower = ResolveCurrentFieldUnitPowerFromEvaluatedKeywords(evaluated, evaluated.CardObjects[target.ObjectId]);
        return sources.Any(source => ResolveCurrentFieldUnitPowerFromEvaluatedKeywords(evaluated,
            evaluated.CardObjects[source.ObjectId]) > targetPower);
    }
}
