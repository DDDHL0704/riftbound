namespace Riftbound.Engine;

public sealed record SpellManaAlternativeCostSpec(int MinimumPaidMana, int Power, IReadOnlyList<string> Traits);

public sealed partial class CoreRuleEngine
{
    internal const string SpellManaAlternativeCostPrefix = "SPELL_MANA_ALTERNATIVE_COST:";

    internal static string SpellManaAlternativeCostChoiceId(SpellManaAlternativeCostSpec spec)
        => SpellManaAlternativeCostPrefix + spec.MinimumPaidMana + ":" + spec.Power + ":"
            + string.Join(',', spec.Traits.Order(StringComparer.Ordinal));

    internal static bool ControllerConfirmedSpellWithPaidMana(MatchState state, string player, int minimum)
        => minimum > 0 && state.CardPlays is { Finalized: { } plays } history
            && history.TurnNumber == state.TurnNumber
            && plays.Any(play => play is { PaidMana: { } mana } && play.PlayerId == player
                && (play.Types & 4) != 0 && mana >= minimum);

    internal static IReadOnlyList<CardBehaviorDefinition> SpellManaAlternativePlayBehaviors(
        MatchState state, string player, CardBehaviorDefinition behavior)
    {
        var normal = behavior with { AppliedSpellManaAlternativeCost = null };
        if (behavior.SpellManaAlternativeCost is not { MinimumPaidMana: > 0, Power: >= 0 } spec
            || spec.Traits is null || spec.Power > 0 && spec.Traits.Count == 0
            || !ControllerConfirmedSpellWithPaidMana(state, player, spec.MinimumPaidMana)) return [normal];
        var ignoresAll = EffectPlayIgnoresAllCosts(state, player);
        var payable = ignoresAll ? spec with { Power = 0, Traits = [] } : spec;
        var result = new List<CardBehaviorDefinition> { normal, normal with { AppliedSpellManaAlternativeCost = payable } };
        // CN356.1: replacement and ignoring base costs may be ordered either
        // way. This replacement has zero base mana. Ignoring base power after
        // it gives another legal quote; ignoring it first retains the new cost.
        var ignoresPower = state.PendingEffectPlay is { IgnoreBasePower: true } pending && pending.PlayerId == player
            || behavior.StandbyPlayFrom is not null || behavior.IgnoreBasePlayCostFromBoons
            || behavior.AppliedEquipmentPlayGrant is not null;
        if (!ignoresAll && ignoresPower && spec.Power > 0)
            result.Add(normal with { AppliedSpellManaAlternativeCost = spec with { Power = 0, Traits = [] } });
        return result;
    }

    private static bool TrySelectSpellManaAlternativeCost(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string>? selections,
        out CardBehaviorDefinition selected, out IReadOnlyList<string> remaining)
    {
        var chosen = (selections ?? []).Where(c => c?.Trim().StartsWith(SpellManaAlternativeCostPrefix, StringComparison.Ordinal) == true).ToArray();
        remaining = (selections ?? []).Where(c => c?.Trim().StartsWith(SpellManaAlternativeCostPrefix, StringComparison.Ordinal) != true).ToArray();
        selected = behavior with { AppliedSpellManaAlternativeCost = null };
        if (chosen.Length == 0) return true;
        if (chosen.Length != 1) return false;
        var variant = SpellManaAlternativePlayBehaviors(state, player, behavior).FirstOrDefault(b =>
            b.AppliedSpellManaAlternativeCost is { } cost && SpellManaAlternativeCostChoiceId(cost) == chosen[0].Trim());
        if (variant is null) return false;
        selected = variant;
        return true;
    }
}
