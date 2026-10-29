using System.Text.Json.Serialization;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DepartedTokenTarget(ObjectBinding Target, string TokenFactoryCardNo, long DepartedTick);

public sealed partial class CoreRuleEngine
{
    // CN 186.1 / 359.3.e: a targeted token can cease to exist while a response
    // resolves. Retain its confirmed identity so restoration can distinguish a
    // departed target from an invented reference. Resolution still masks it.
    private static ResolutionResult RecordDepartedTokenTargets(MatchState before, ResolutionResult result)
    {
        var changed = false;
        var stack = result.State.StackItems.Select(item => {
            var targets = TargetBindings(item).Distinct().ToArray();
            var departed = (item.DepartedTokenTargets ?? []).Where(t => targets.Contains(t.Target)).ToList();
            foreach (var target in targets)
                if (!result.State.CardObjects.ContainsKey(target.ObjectId)
                    && before.CardObjects.TryGetValue(target.ObjectId, out var token)
                    && TokenObjectRules.IsToken(token) && token.TokenFactoryCardNo is { } factory
                    && token.ObjectGeneration == target.Generation
                    && !departed.Any(t => t.Target == target))
                    departed.Add(new(target, factory, result.State.Tick));
            if (departed.SequenceEqual(item.DepartedTokenTargets ?? [])) return item;
            changed = true;
            return item with { DepartedTokenTargets = departed.Count == 0 ? null : departed.ToArray() };
        }).ToArray();
        if (!changed) return result;
        var next = result.State with { StackItems = stack };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static IEnumerable<ObjectBinding> TargetBindings(StackItemState item)
    {
        foreach (var id in item.TargetObjectIds ?? [])
            if (item.TargetGenerations?.TryGetValue(id, out var generation) == true)
                yield return new(id, generation);
        foreach (var execution in item.RepeatExecutions ?? [])
            foreach (var id in execution?.TargetObjectIds ?? [])
                if (execution?.TargetGenerations?.TryGetValue(id, out var generation) == true)
                    yield return new(id, generation);
    }

    internal static bool ValidDepartedTokenTargets(MatchState state, StackItemState item)
    {
        if (item.DepartedTokenTargets is not { } departed) return true;
        var bindings = TargetBindings(item).ToHashSet();
        return departed.Count > 0 && departed.All(t => t is { Target.Generation: >= 0, DepartedTick: > 0 }
            && t.DepartedTick <= state.Tick && !string.IsNullOrWhiteSpace(t.Target.ObjectId)
            && !string.IsNullOrWhiteSpace(t.TokenFactoryCardNo)
            && !state.CardObjects.ContainsKey(t.Target.ObjectId) && bindings.Contains(t.Target)
            && P6TokenFactoryCatalog.TryGetByCardNo(t.TokenFactoryCardNo, out _))
            && departed.Select(t => t.Target.ObjectId).Distinct(StringComparer.Ordinal).Count() == departed.Count;
    }
}
