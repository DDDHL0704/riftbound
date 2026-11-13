namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string UnitControlCaptured = "UNIT_CONTROL_EFFECT_CAPTURED";
    internal const string UnitControlRetired = "UNIT_CONTROL_EFFECT_RETIRED";
    internal const string UnitControlKey = "unitControlEffect";
    internal const string UnitControlProof = "unitControlProof";

    internal static IEnumerable<MatchState> UnitControlFrames(MatchState state)
        => new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>();

    internal static IEnumerable<(string Key, string Proof)> UnitControlReceipts(MatchState state)
    {
        foreach (var frame in UnitControlFrames(state))
        {
            foreach (var card in frame.CardObjects.Values)
                if (card.UnitControl is { Grants: not null } lease)
                    foreach (var grant in lease.Grants.OfType<UnitControlGrant>())
                        yield return (MatchStateHasher.HashValue("CONTROL:" + grant.InstanceId),
                            MatchStateHasher.HashValue(new { lease.Unit, lease.UnderlyingControllerId, Grant = grant }));
            foreach (var context in CurrentControlLosses(frame))
                yield return (MatchStateHasher.HashValue("DELAYED-CONTROL:" + context.Grant.InstanceId), MatchStateHasher.HashValue(context));
        }
    }

    private static ResolutionResult RecordUnitControlHistory(MatchState before, ResolutionResult result)
    {
        var prior = UnitControlReceipts(before).DistinctBy(row => row.Key)
            .ToDictionary(row => row.Key, StringComparer.Ordinal);
        var current = UnitControlReceipts(result.State).DistinctBy(row => row.Key)
            .ToDictionary(row => row.Key, StringComparer.Ordinal);
        if (prior.Count == 0 && current.Count == 0) return result;
        var events = result.Events.ToList();
        void Record(string kind, (string Key, string Proof) row)
        {
            if (events.Any(ev => ev.Kind == kind && Equals(ev.Payload.GetValueOrDefault(UnitControlKey), row.Key))) return;
            var payload = new Dictionary<string, object?> { [UnitControlKey] = row.Key };
            if (kind == UnitControlCaptured) payload[UnitControlProof] = row.Proof;
            events.Add(new(kind, "控制效果及延时技能记录", payload));
        }
        foreach (var row in current.Values.Where(row => !prior.ContainsKey(row.Key))) Record(UnitControlCaptured, row);
        foreach (var row in prior.Values.Where(row => !current.ContainsKey(row.Key))) Record(UnitControlRetired, row);
        return events.Count == result.Events.Count ? result : result with { Events = events };
    }
}
