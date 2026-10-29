using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string SpellPowerReductionPrefix = "SPELL_POWER_REDUCTION:";
    internal sealed record SpellPowerReductionOption(string ChoiceId, int GenericPower,
        IReadOnlyDictionary<string, int> TypedPower, int Reduction);

    private static IReadOnlyList<EmpoweredSpellCostReductionSpec> EmpoweredSpellCostReducers(
        MatchState state, string player, CardBehaviorDefinition behavior)
        => !IsSpellPlayBehavior(behavior) || EffectPlayIgnoresAllCosts(state, player) ? []
            : PublicStaticAuraSourceObjectIds(state.PlayerZones)
                .Where(id => state.CardObjects.TryGetValue(id, out var card) && card.IsEmpowered && !card.IsFaceDown
                    && EffectiveFieldControllerId(state.PlayerZones, id, card) == player)
                .SelectMany(id => AttachedRuleTextRules.Instances(state, state.CardObjects[id]))
                .SelectMany(instance => EmpowerRules.SpellCostReductions(instance.CardNo)).ToArray();

    private static bool IsSpellPowerReductionChoice(string choice)
        => choice.StartsWith(SpellPowerReductionPrefix, StringComparison.Ordinal);

    // CN356.4: a total A reduction can remove either generic or typed power,
    // including additional costs. Keep every legal distribution for the payer.
    internal static IReadOnlyList<SpellPowerReductionOption> ReduceSpellPower(
        int generic, IReadOnlyDictionary<string, int> typed, int reduction)
    {
        if (reduction <= 0) return [new("", generic, typed, 0)];
        var dimensions = new[] { new KeyValuePair<string, int>("any", generic) }
            .Concat(typed.OrderBy(p => p.Key, StringComparer.Ordinal)).Where(p => p.Value > 0).ToArray();
        var amount = (int)Math.Min(reduction, (long)generic + typed.Values.Sum(v => (long)v));
        if (amount == 0) return [new("", generic, typed, 0)];
        var results = new List<SpellPowerReductionOption>(); var removed = new Dictionary<string, int>();
        void Visit(int index, int remaining)
        {
            if (index == dimensions.Length)
            {
                if (remaining != 0) return;
                var id = SpellPowerReductionPrefix + string.Join(",", removed.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Where(p => p.Value > 0).Select(p => $"{p.Key}:{p.Value}"));
                results.Add(new(id, generic - removed.GetValueOrDefault("any"),
                    typed.Where(p => p.Value > removed.GetValueOrDefault(p.Key))
                        .ToDictionary(p => p.Key, p => p.Value - removed.GetValueOrDefault(p.Key), StringComparer.Ordinal), amount));
                return;
            }
            var dimension = dimensions[index];
            for (var count = 0; count <= Math.Min(remaining, dimension.Value); count++)
            {
                removed[dimension.Key] = count; Visit(index + 1, remaining - count);
            }
            removed.Remove(dimension.Key);
        }
        Visit(0, amount); return results;
    }

    private static IReadOnlyList<SpellPowerReductionOption> SpellPlayPowerOptions(
        MatchState state, string player, CardBehaviorDefinition behavior, string? printedChoice,
        int extraGeneric, IReadOnlyDictionary<string, int> extraTyped,
        IReadOnlyList<string>? additionalPrinted = null, int sacrificeReduction = 0, IReadOnlyList<string>? targets = null)
    {
        var reduction = EmpoweredSpellCostReducers(state, player, behavior).Sum(s => s.GenericPower)
            + SourceNextSpellCostReductionEffects(state, player)
                .Where(s => s.AppliesToAllCards || IsSpellPlayBehavior(behavior)).Sum(s => s.GenericPower)
            + BattlefieldFriendlyTargetSpellPowerReduction(state, player, behavior, targets);
        extraGeneric += OpponentSpellCostIncreases(state, player, behavior).Power
            + BattlefieldReactionPowerIncrease(state, player, behavior);
        return PrintedPowerCostRules.OptionsForPlay(behavior, printedChoice, extraGeneric, extraTyped, additionalPrinted)
            .SelectMany(a => ReduceSpellPower(a.GenericPower,
                ConditionalPlayPowerCost(state, player, behavior, ApplySacrificePowerReduction(a.TypedPower, sacrificeReduction)), reduction))
            .DistinctBy(a => (a.ChoiceId, a.GenericPower, PrintedPowerCostRules.ChoiceId(a.TypedPower))).ToArray();
    }

    internal static bool TrySelectPlayPowerCost(MatchState state, string player, CardBehaviorDefinition behavior,
        string? printedChoice, RunePool pool, int extraGeneric, IReadOnlyDictionary<string, int> extraTyped,
        out int generic, out IReadOnlyDictionary<string, int> typed,
        IReadOnlyList<string>? additionalPrinted = null)
    {
        var selected = SpellPlayPowerOptions(state, player, behavior, printedChoice, extraGeneric, extraTyped, additionalPrinted)
            .OrderBy(a => PaymentCostRules.PowerDeficit(pool, a.GenericPower, a.TypedPower)).FirstOrDefault();
        generic = selected?.GenericPower ?? extraGeneric; typed = selected?.TypedPower ?? extraTyped;
        return selected is not null;
    }

    internal static bool CanReachPlayPowerCost(MatchState state, string player, CardBehaviorDefinition behavior,
        RunePaymentReachability reachability, int extraGeneric, IReadOnlyDictionary<string, int> extraTyped,
        IReadOnlyList<string>? additionalPrinted = null, int sacrificeReduction = 0, IReadOnlyList<string>? targets = null)
        => SpellPlayPowerOptions(state, player, behavior, null, extraGeneric, extraTyped,
                additionalPrinted, sacrificeReduction, targets)
            .Any(option => reachability.CanPay(option.GenericPower, option.TypedPower));

    private static IReadOnlyList<ActionPromptChoiceDto> SpellPowerChoices(IReadOnlyList<SpellPowerReductionOption> options)
        => options.Where(a => a.ChoiceId.Length > 0).DistinctBy(a => a.ChoiceId)
            .Select(a => new ActionPromptChoiceDto(a.ChoiceId, "减去 " + SpellPowerReductionLabel(a.ChoiceId),
                "指定总费用中被减去的符能；不选择时按可支付分配计算。")).ToArray();

    private static string SpellPowerReductionLabel(string choice)
        => string.Join("＋", choice[SpellPowerReductionPrefix.Length..].Split(',').Select(part => {
            var pair = part.Split(':');
            var trait = pair[0] == "any" ? "任意" : RuneTraitLabel(pair[0]);
            return pair[1] + trait + "符能";
        }));

    internal static IReadOnlyList<ActionPromptChoiceDto> SpellPowerReductionChoices(
        MatchState state, string player, CardBehaviorDefinition behavior)
        => SpellPowerChoices(SpellPlayPowerOptions(state, player, behavior, null,
            SourceReplayPowerCost(state, player), new Dictionary<string, int>()));
}
