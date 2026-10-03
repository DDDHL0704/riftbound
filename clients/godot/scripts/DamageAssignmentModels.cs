using System;
using System.Collections.Generic;
using System.Linq;

namespace Riftbound.GodotClient;

public sealed record DamageTargetPromptItem(string TargetObjectId, string Label, int LethalDamageThreshold, int Priority = 1, long SuggestedDamage = 0);
public sealed record DamageAssignmentPromptItem(string SourceObjectId, string SourceLabel, int DamagePool, IReadOnlyList<DamageTargetPromptItem> Targets);
public sealed record DamageAssignmentSelection(string SourceObjectId, string TargetObjectId, int Damage);

public static class PooledDamageSelection
{
    // Convert the user's target totals to the existing source ledger protocol.
    // This only accounts for amounts and server choices; rules are judged remotely.
    public static bool TrySplit(IReadOnlyList<DamageAssignmentPromptItem> sources,
        IReadOnlyDictionary<string, long> damageByTarget, out IReadOnlyList<DamageAssignmentSelection> selections)
    {
        selections = [];
        if (sources.Count == 0 || sources.Any(source => source.DamagePool <= 0)
            || damageByTarget.Values.Any(damage => damage < 0)
            || sources.Sum(source => (long)source.DamagePool) != damageByTarget.Values.Sum()) return false;
        var remaining = damageByTarget.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var result = new List<DamageAssignmentSelection>();
        foreach (var source in sources)
        {
            var pool = source.DamagePool;
            foreach (var target in source.Targets)
            {
                var amount = (int)Math.Min(pool, remaining.GetValueOrDefault(target.TargetObjectId));
                if (amount <= 0) continue;
                result.Add(new(source.SourceObjectId, target.TargetObjectId, amount));
                remaining[target.TargetObjectId] -= amount;
                pool -= amount;
            }
            if (pool != 0) return false;
        }
        if (remaining.Values.Any(value => value != 0)) return false;
        selections = result;
        return true;
    }
}
