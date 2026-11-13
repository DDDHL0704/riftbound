using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string LevelTargetsConfirmed = "LEVEL_TARGETS_CONFIRMED";
    internal const string LevelTargetsRetired = "LEVEL_TARGETS_RETIRED";

    // Resolution may ignore a dependent instruction, but must never acquire a
    // target which was absent at confirmation (CN 355.10 / 359.3.e.6 / 824).
    internal static IEnumerable<(string Key, string Proof)> LevelTargetReceipts(MatchState state)
    {
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
            foreach (var item in frame.StackItems)
                if (CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior) && behavior.AdditionalTargetsAtLevel > 0)
                    yield return (MatchStateHasher.HashValue(item.StackItemId), MatchStateHasher.HashValue(new {
                        item.TargetObjectIds, item.TargetGenerations, item.RepeatExecutions }));
    }

    private static ResolutionResult RecordLevelTargetHistory(MatchState before, ResolutionResult result)
    {
        var prior = LevelTargetReceipts(before).DistinctBy(r => r.Key).ToDictionary(r => r.Key, StringComparer.Ordinal);
        var current = LevelTargetReceipts(result.State).DistinctBy(r => r.Key).ToDictionary(r => r.Key, StringComparer.Ordinal);
        if (prior.Count == 0 && current.Count == 0) return result;
        var events = result.Events.ToList();
        foreach (var row in current.Values.Where(row => !prior.TryGetValue(row.Key, out var old) || old.Proof != row.Proof))
            events.Add(new(LevelTargetsConfirmed, "确认依赖性指示的目标", new Dictionary<string, object?> {
                ["levelTargetKey"] = row.Key, ["levelTargetProof"] = row.Proof }));
        foreach (var row in prior.Values.Where(row => !current.ContainsKey(row.Key)))
            events.Add(new(LevelTargetsRetired, "依赖性指示离开结算链", new Dictionary<string, object?> { ["levelTargetKey"] = row.Key }));
        return events.Count == result.Events.Count ? result : result with { Events = events };
    }
}
