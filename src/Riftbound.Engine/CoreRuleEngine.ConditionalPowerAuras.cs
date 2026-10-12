using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record ConditionalPowerAuraContribution(string SourceId, RuleTextInstance Instance, StaticAuraSpec Spec,
    int Index, int Delta, int Before, int After);

public sealed partial class CoreRuleEngine
{
    internal static int CurrentPowerBeforeConditionalAuras(MatchState state, CardObjectState card)
    {
        var battle = state.BattleState;
        if (battle.IsActive && (battle.AttackerObjectIds.Contains(card.ObjectId) || battle.DefenderObjectIds.Contains(card.ObjectId)))
            return ResolveAssignmentBattleCombatPower(state, battle, card.ObjectId, out _, out _,
                damageContribution: false, conditionalPowerAuras: false, clampResult: false);
        var field = state.ObjectLocations.GetValueOrDefault(card.ObjectId)?.BattlefieldObjectId ?? "";
        return card.Power + ResolveFieldPowerStaticAuraBonus(state, state.PlayerZones, card.ObjectId, card, field);
    }

    internal static IReadOnlyList<ConditionalPowerAuraContribution> ConditionalPowerAuraContributions(MatchState state,
        CardObjectState target, int? startingPower = null)
    {
        var result = new List<ConditionalPowerAuraContribution>();
        if (!IsFaceUpNonStandbyUnit(target) || !IsObjectOnField(state.PlayerZones, target.ObjectId)
            || !target.UntilEndOfTurnEffects.Contains("STUNNED", StringComparer.Ordinal)) return result;
        var power = startingPower ?? CurrentPowerBeforeConditionalAuras(state, target);
        var controller = EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target);
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal)) {
            if (!IsFaceUpNonStandbyUnit(source) || !IsObjectOnField(state.PlayerZones, source.ObjectId)
                || EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) == controller
                || !IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, source.ObjectId, target.ObjectId)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                foreach (var (spec, index) in StaticAuraSpecRules.GetStaticAuras(instance.CardNo).Select((s, i) => (s, i))) {
                    if (!StaticAuraSpecRules.IsConditionalEnemyPowerAura(spec) || !StaticAuraSpecRules.TargetMatchesFilter(spec, target)) continue;
                    // The passive's amount depends on other numeric effects (477.3 /
                    // 479), and is reevaluated; no until-turn snapshot is created.
                    var delta = Math.Max(spec.PowerDeltaPerParticipant, -Math.Max(0, power - spec.MinimumPowerAfterModifier!.Value));
                    result.Add(new(source.ObjectId, instance, spec, index, delta, Math.Max(0, power), Math.Max(0, power + delta)));
                    power += delta;
                }
        }
        return result;
    }
}
