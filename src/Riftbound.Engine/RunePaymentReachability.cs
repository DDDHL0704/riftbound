namespace Riftbound.Engine;

// Prospective rune recycles each add exactly one power. Keep their shared
// capacity separate from the actual pool; independent trait bounds are not a
// simultaneously attainable wallet when capacity binds.
internal sealed class RunePaymentReachability
{
    private readonly RunePool current;
    private readonly IReadOnlyDictionary<string, int> candidates;
    private readonly long candidateTotal;
    internal bool Supported { get; }
    internal int Capacity { get; }
    internal int MaximumTotal { get; }
    internal bool CapacityBinds => Supported && Capacity > 0 && candidateTotal > Capacity;
    internal RunePaymentReachability(RunePool pool, IReadOnlyDictionary<string, int> powerByTrait)
    {
        current = pool;
        candidates = powerByTrait.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        candidateTotal = candidates.Values.Sum(v => (long)v);
        Supported = pool.HasSupportedAmounts && candidates.Values.All(v => v >= 0);
        if (!Supported) return;
        Capacity = int.MaxValue - pool.TotalPower;
        MaximumTotal = (int)((long)pool.TotalPower + Math.Min(Capacity, candidateTotal));
    }

    internal IReadOnlyDictionary<string, int> TraitUpperBounds()
    {
        if (!Supported) return current.PowerByTrait;
        return current.PowerByTrait.Keys.Concat(candidates.Keys).Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToDictionary(t => t,
                t => (int)((long)current.PowerByTrait.GetValueOrDefault(t) + Math.Min(Capacity, (long)candidates.GetValueOrDefault(t))), StringComparer.Ordinal)
            .Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
    }

    internal bool CanPay(int generic, IReadOnlyDictionary<string, int> typed)
    {
        if (!Supported || generic < 0 || typed.Values.Any(v => v < 0)) return false;
        var typedTotal = typed.Values.Sum(v => (long)v);
        var total = generic + typedTotal;
        if (total > int.MaxValue || total > (long)current.TotalPower + candidateTotal) return false;
        long typedDeficit = 0, requiresWildcard = 0;
        foreach (var (trait, amount) in typed)
        {
            var deficit = Math.Max(0L, (long)amount - current.PowerByTrait.GetValueOrDefault(trait));
            typedDeficit += deficit;
            requiresWildcard += Math.Max(0L, deficit - candidates.GetValueOrDefault(trait));
        }
        if (requiresWildcard > (long)current.Power + candidates.GetValueOrDefault(string.Empty)) return false;
        var minimumGain = Math.Max(Math.Max(0L, typedDeficit - current.Power), Math.Max(0L, total - current.TotalPower));
        return minimumGain <= Capacity;
    }

    internal bool CanPayForPlay(CardBehaviorDefinition behavior, int extraGeneric,
        IReadOnlyDictionary<string, int> extraTyped, Func<IReadOnlyDictionary<string, int>, IReadOnlyDictionary<string, int>>? transform = null, IReadOnlyList<string>? additionalPrintedCosts = null)
    {
        var printed = PrintedPowerCostRules.ForPlay(behavior);
        var generic = (long)extraGeneric + (printed.Traits.Count == 0 ? printed.Amount : 0);
        if (generic is < 0 or > int.MaxValue) return false;
        IReadOnlyList<IReadOnlyDictionary<string, int>> allocations = PrintedPowerCostRules.AllocationsForPlay(behavior);
        foreach (var card in additionalPrintedCosts ?? [])
        {
            var additional = PrintedPowerCostRules.ForCard(card);
            if (additional.Traits.Count == 0) generic += additional.Amount;
            if (generic > int.MaxValue) return false;
            var combined = new List<IReadOnlyDictionary<string, int>>();
            foreach (var first in allocations)
            foreach (var second in PrintedPowerCostRules.Allocations(card))
                if (TryCombine(first, second, out var sum)) combined.Add(sum);
            allocations = combined.DistinctBy(PrintedPowerCostRules.ChoiceId).ToArray();
        }
        foreach (var allocation in allocations)
        {
            if (!TryCombine(allocation, extraTyped, out var typed)) continue;
            if (transform is not null) typed = transform(typed);
            if (CanPay((int)generic, typed)) return true;
        }
        return false;
    }

    internal static bool TryCombine(IReadOnlyDictionary<string, int> first, IReadOnlyDictionary<string, int> second,
        out IReadOnlyDictionary<string, int> combined)
    {
        var sums = first.Concat(second).GroupBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(p => (long)p.Value), StringComparer.Ordinal);
        combined = new Dictionary<string, int>();
        if (sums.Values.Any(v => v is < 0 or > int.MaxValue)) return false;
        combined = sums.ToDictionary(p => p.Key, p => (int)p.Value, StringComparer.Ordinal); return true;
    }
}
