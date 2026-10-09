using Riftbound.CardCatalog;

namespace Riftbound.Engine;

// CN 131.3, 135.2.e.5-6: the printed number is a total [C] cost, shared
// across the card's traits. It is not an additional cost per trait.
public static class PrintedPowerCostRules
{
    public const string ChoicePrefix = "PRINTED_POWER:";
    public sealed record Cost(int Amount, IReadOnlyList<string> Traits);
    private static readonly Lazy<IReadOnlyDictionary<string, Cost>> Costs = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(
            card => card.CardNo,
            card => new Cost(Math.Max(0, card.ReturnEnergy ?? 0), card.CardColorList
                .Select(RuneTrait.Normalize).Where(trait => trait is RuneTrait.Red or RuneTrait.Green or RuneTrait.Blue or RuneTrait.Yellow or RuneTrait.Orange or RuneTrait.Purple)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()), StringComparer.Ordinal));

    public static Cost ForCard(string cardNo)
        => Costs.Value.TryGetValue(cardNo, out var cost) ? cost : new(0, []);

    public static IReadOnlyList<IReadOnlyDictionary<string, int>> Allocations(string cardNo)
    {
        var cost = ForCard(cardNo);
        if (cost.Amount == 0 || cost.Traits.Count == 0) return [new Dictionary<string, int>()];
        var results = new List<IReadOnlyDictionary<string, int>>();
        var current = new Dictionary<string, int>(StringComparer.Ordinal);
        void Visit(int index, int remaining)
        {
            if (index == cost.Traits.Count)
            {
                if (remaining == 0) results.Add(current.Where(x => x.Value > 0).ToDictionary(x => x.Key, x => x.Value));
                return;
            }
            for (var count = remaining; count >= 0; count--)
            {
                current[cost.Traits[index]] = count;
                Visit(index + 1, remaining - count);
            }
        }
        Visit(0, cost.Amount);
        return results;
    }

    public static string ChoiceId(IReadOnlyDictionary<string, int> allocation)
        => ChoicePrefix + string.Join(",", allocation.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}:{x.Value}"));

    public static IReadOnlyDictionary<string, int> Combine(IReadOnlyDictionary<string, int> first, IReadOnlyDictionary<string, int> second)
        => first.Concat(second).GroupBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Sum(y => y.Value));

    public static bool TrySelect(string cardNo, string? choice, RunePool pool, int extraGeneric,
        IReadOnlyDictionary<string, int> extraTyped, out int generic, out IReadOnlyDictionary<string, int> typed,
        IReadOnlyList<string>? additionalPrintedCosts = null)
    {
        var cost = ForCard(cardNo);
        generic = extraGeneric + (cost.Traits.Count == 0 ? cost.Amount : 0);
        var allocations = Allocations(cardNo);
        if (choice is not null)
        {
            allocations = allocations.Where(a => cost.Amount > 0 && string.Equals(ChoiceId(a), choice, StringComparison.Ordinal)).ToArray();
            if (allocations.Count == 0) { typed = extraTyped; return false; }
        }
        // Each base-cost Echo can choose its printed traits independently of the
        // original spell's explicit allocation.
        foreach (var additionalCard in additionalPrintedCosts ?? [])
        {
            var additional = ForCard(additionalCard);
            generic += additional.Traits.Count == 0 ? additional.Amount : 0;
            allocations = allocations.SelectMany(first => Allocations(additionalCard)
                .Select(second => Combine(first, second)))
                .DistinctBy(ChoiceId).ToArray();
        }
        var genericCost = generic;
        typed = allocations.Select(a => Combine(a, extraTyped))
            .OrderBy(a => PaymentCostRules.PowerDeficit(pool, genericCost, a)).First();
        return true;
    }
}
