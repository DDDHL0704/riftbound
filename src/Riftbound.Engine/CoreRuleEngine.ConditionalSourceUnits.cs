using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record ConditionalSourceUnitContribution(RuleTextInstance Instance, string Condition,
    int PowerDelta, IReadOnlyList<string> Keywords);

public sealed partial class CoreRuleEngine
{
    internal const string ConditionalSourceUnitEffect = "CONDITIONAL_SOURCE_UNIT";
    private static readonly string[] ConditionalSourceUnitHistoryPrefixes = [DiscardedHandCardThisTurnEffectPrefix,
        GainedExperienceThisTurnEffectPrefix, PlayedFourPlusCostSpellThisTurnEffectPrefix];

    private static string[] PublicConditionalSourceUnitTurnHistory(IReadOnlyList<string> effects, IEnumerable<string> players)
    {
        var allowed = players.SelectMany(player => ConditionalSourceUnitHistoryPrefixes.Select(prefix => prefix + player))
            .ToHashSet(StringComparer.Ordinal);
        return effects.Where(allowed.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool ValidConditionalSourceUnitTurnHistory(IReadOnlyList<string>? effects, IEnumerable<string> players)
        => effects is not null && effects.SequenceEqual(PublicConditionalSourceUnitTurnHistory(effects, players));

    private static IEnumerable<(RuleTextInstance Instance, CardBehaviorDefinition Behavior)> ConditionalSourceUnitDefinitions(
        MatchState state, CardObjectState source)
    {
        if (!IsFaceUpNonStandbyUnit(source) || !IsObjectOnField(state.PlayerZones, source.ObjectId)) yield break;
        foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            if (CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior)
                && behavior.ConditionalSourceUnitConditionKind != CardConditionalSourceUnitConditionKinds.None)
                yield return (instance, behavior);
    }

    private static bool HasConditionalSourceUnitKeywords(MatchState state, CardObjectState source)
        => ConditionalSourceUnitDefinitions(state, source).Any(s => !string.IsNullOrEmpty(s.Behavior.ConditionalSourceUnitTags));

    internal static IReadOnlyList<ConditionalSourceUnitContribution> ConditionalSourceUnitContributions(
        MatchState state, CardObjectState source)
    {
        var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source);
        return ConditionalSourceUnitDefinitions(state, source)
            .Where(s => ConditionalSourceUnitConditionApplies(s.Behavior, controller, state.UntilEndOfTurnEffects))
            .Select(s => new ConditionalSourceUnitContribution(s.Instance, s.Behavior.ConditionalSourceUnitConditionKind,
                ResolveConditionalSourceUnitPowerBonus(s.Behavior, controller, state.UntilEndOfTurnEffects),
                ResolveConditionalSourceUnitTags(s.Behavior, controller, state.UntilEndOfTurnEffects))).ToArray();
    }
}
