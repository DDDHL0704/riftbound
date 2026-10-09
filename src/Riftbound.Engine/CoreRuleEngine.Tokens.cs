using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool IsTokenObject(CardObjectState card) =>
        P6TokenFactoryCatalog.IsTokenFactory(card.CardNo) || card.Tags.Contains("映像", StringComparer.Ordinal);

    private static ResolutionResult FinalizeTokenDepartures(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var departed = before.CardObjects.Where(e => IsTokenObject(e.Value)
            && !result.State.CardObjects.ContainsKey(e.Key)).Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        if (departed.Count == 0) return result;
        var state = result.State with { ObjectLocations = result.State.ObjectLocations
            .Where(e => !departed.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal) };
        var events = result.Events.ToList();
        foreach (var id in departed)
            if (!events.Any(e => e.Kind == "TOKEN_CEASED_TO_EXIST"
                && e.Payload.TryGetValue("targetObjectId", out var target) && Equals(target, id)))
                events.Add(new("TOKEN_CEASED_TO_EXIST", "离场指示物消失", new Dictionary<string, object?>
                    { ["targetObjectId"] = id, ["cardNo"] = before.CardObjects[id].CardNo }));
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
