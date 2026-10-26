using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static MatchState RuleChoiceResourceContext(MatchState context)
        => RuleChoices.Value?.ResourceDeathLedger is { } ledger ? context with { DeathLedger = ledger } : context;

    private static ResolutionResult CompleteRuleChoiceResourceEffects(MatchState context, ResolutionResult result)
    {
        if (!result.Events.Any(e => e.Payload.ContainsKey("capturedUnitDeath"))) return result;
        var frame = RuleChoices.Value!; var completionTick = frame.CompletionTick;
        try
        {
            // Resource deaths settle at their own confirmed answer, including
            // when the containing command is replayed by a later answer.
            frame.CompletionTick = frame.AnswerTick;
            var completed = RecordDeathObservers(context, result);
            completed = StampNewTriggerQueueTimes(context, completed with {
                State = completed.State with { Tick = frame.AnswerTick } });
            frame.ResourceDeathLedger = completed.State.DeathLedger;
            return completed;
        }
        finally { frame.CompletionTick = completionTick; }
    }

    // Child contexts may already contain uncommitted outer effects. Apply only
    // their changes to the original checkpoint, never their whole After/Preview.
    private static MatchState CommitRuleChoiceResources(MatchState origin, IReadOnlyList<RuleChoiceResourceStep> steps)
    {
        if (steps.Count == 0) return origin;
        var committed = JsonSerializer.SerializeToNode(origin, ResourceStepJson)!.AsObject();
        foreach (var step in steps)
        {
            var before = JsonNode.Parse(step.Before.GetRawText())!.AsObject();
            var after = JsonNode.Parse(step.After.GetRawText())!.AsObject();
            foreach (var key in before.Select(p => p.Key).Union(after.Select(p => p.Key)))
            {
                // Tick belongs to the accepted outer command. The child payment
                // window exists only to authorize its immediate resource action.
                if (key is "tick" or "pendingPayment" or "pendingRuleChoice") continue;
                committed[key] = ApplyRuleChoiceResourceDifference(committed[key], before[key], after[key], key);
            }
        }
        var next = committed.Deserialize<MatchState>(ResourceStepJson)!;
        if (next.RunePools.Values.Any(pool => !pool.HasSupportedAmounts))
            throw new InvalidOperationException("Completed resource steps produced an unsupported pool.");
        return next;
    }

    private static JsonNode? ApplyRuleChoiceResourceDifference(JsonNode? current, JsonNode? before, JsonNode? after, string path)
    {
        if (JsonNode.DeepEquals(before, after)) return current?.DeepClone();
        var additive = path.StartsWith("runePools.", StringComparison.Ordinal)
            || path.StartsWith("deathLedger.counts.", StringComparison.Ordinal);
        if (after is null && !additive) return null;
        // Resource gains/conversions are deltas even when an earlier uncommitted
        // instruction has spent part of the pool visible to the child.
        if (additive && (before is JsonValue || after is JsonValue))
            return JsonValue.Create(checked((int)((current?.GetValue<int>() ?? 0)
                + (long)(after?.GetValue<int>() ?? 0) - (before?.GetValue<int>() ?? 0))));
        if (before is JsonObject || after is JsonObject)
        {
            var prior = before as JsonObject ?? new(); var next = after as JsonObject ?? new();
            var result = current?.DeepClone() as JsonObject ?? new();
            foreach (var key in prior.Select(p => p.Key).Union(next.Select(p => p.Key)))
            {
                if (JsonNode.DeepEquals(prior[key], next[key])) continue;
                result[key] = ApplyRuleChoiceResourceDifference(result[key], prior[key], next[key], path + "." + key);
                if (result[key] is null) result.Remove(key);
            }
            return result;
        }
        if (before is JsonArray priorList && after is JsonArray nextList)
        {
            var result = current?.DeepClone() as JsonArray ?? new();
            var removed = priorList.ToList();
            var added = new List<int>();
            for (var i = 0; i < nextList.Count; i++)
            {
                var match = removed.FindIndex(value => JsonNode.DeepEquals(value, nextList[i]));
                if (match >= 0) removed.RemoveAt(match); else added.Add(i);
            }
            foreach (var old in removed)
            {
                var index = result.ToList().FindIndex(value => JsonNode.DeepEquals(value, old));
                if (index >= 0) result.RemoveAt(index);
            }
            foreach (var index in added)
            {
                // Keep the child's insertion order without replacing unrelated
                // members which belong to the still-uncommitted outer action.
                var insertion = result.Count;
                for (var following = index + 1; following < nextList.Count; following++)
                {
                    var anchor = result.ToList().FindIndex(value => JsonNode.DeepEquals(value, nextList[following]));
                    if (anchor >= 0) { insertion = anchor; break; }
                }
                result.Insert(insertion, nextList[index]?.DeepClone());
            }
            return result;
        }
        return after?.DeepClone();
    }

    private static IReadOnlyList<GameEvent> UncommittedRuleChoiceEvents(IReadOnlyList<GameEvent> events,
        IReadOnlyList<RuleChoiceResourceStep> committed)
    {
        var remaining = events.ToList(); var cursor = 0;
        foreach (var value in committed.SelectMany(step => step.Events))
        {
            var expected = MatchStateHasher.HashValue(value);
            var index = remaining.FindIndex(cursor, candidate => MatchStateHasher.HashValue(candidate) == expected);
            if (index < 0) throw new InvalidOperationException("Completed resource event prefix no longer matches its command.");
            remaining.RemoveAt(index); cursor = index;
        }
        return remaining;
    }
}
