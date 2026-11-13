namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string EquipmentControlCaptured = "EQUIPMENT_CONTROL_EFFECT_CAPTURED";
    internal const string EquipmentControlRetired = "EQUIPMENT_CONTROL_EFFECT_RETIRED";
    internal const string EquipmentControlKey = "equipmentControlEffect";
    internal const string EquipmentControlProof = "equipmentControlProof";

    private static IEnumerable<MatchState> EquipmentControlFrames(MatchState state)
        => new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>();

    internal static bool ValidEquipmentControlLeases(MatchState state)
        => EquipmentControlFrames(state).All(frame => frame.CardObjects.Values.All(card =>
            card.EquipmentControl is not { } lease || lease.Equipment is not null
                && lease.Equipment.ObjectId == card.ObjectId && lease.Equipment.Generation == card.ObjectGeneration
                && lease.Grants is { Count: > 0 } && frame.Seats.ContainsKey(lease.UnderlyingControllerId ?? "")
                && card.ControllerId == LayeredControlController(card, lease.UnderlyingControllerId)
                && IsObjectOnField(frame.PlayerZones, card.ObjectId)
                && lease.Grants.Select(grant => grant?.SkillId).Distinct(StringComparer.Ordinal).Count() == lease.Grants.Count
                && lease.Grants.All(grant => grant is { Source: not null }
                    && !string.IsNullOrWhiteSpace(grant.SkillId) && !string.IsNullOrWhiteSpace(grant.Reason)
                    && grant.CreatedAtTick >= 0 && grant.CreatedAtTick <= frame.Tick
                    && !string.IsNullOrWhiteSpace(grant.Source.ObjectId)
                    && frame.Seats.ContainsKey(grant.ControllerId ?? "") && grant.Source.Generation >= 0
                    && frame.CardObjects.TryGetValue(grant.Source.ObjectId, out var source)
                    && source.ObjectGeneration == grant.Source.Generation
                    && IsObjectOnField(frame.PlayerZones, source.ObjectId))
                && lease.Grants.Zip(lease.Grants.Skip(1)).All(pair => pair.First.CreatedAtTick <= pair.Second.CreatedAtTick)));

    internal static IEnumerable<(string Key, string Proof)> EquipmentControlReceipts(MatchState state)
    {
        foreach (var frame in EquipmentControlFrames(state))
            foreach (var card in frame.CardObjects.Values)
                if (card.EquipmentControl is { Grants: not null } lease)
                    foreach (var grant in lease.Grants.OfType<EquipmentControlGrant>())
                        yield return (MatchStateHasher.HashValue(grant.SkillId), MatchStateHasher.HashValue(new {
                            lease.Equipment, lease.UnderlyingControllerId, Grant = grant }));
    }

    private static ResolutionResult RecordEquipmentControlHistory(MatchState before, ResolutionResult result)
    {
        var prior = EquipmentControlReceipts(before).DistinctBy(row => row.Key)
            .ToDictionary(row => row.Key, StringComparer.Ordinal);
        var current = EquipmentControlReceipts(result.State).DistinctBy(row => row.Key)
            .ToDictionary(row => row.Key, StringComparer.Ordinal);
        if (prior.Count == 0 && current.Count == 0) return result;
        var events = result.Events.ToList();
        void Record(string kind, (string Key, string Proof) row)
        {
            if (events.Any(ev => ev.Kind == kind && Equals(ev.Payload.GetValueOrDefault(EquipmentControlKey), row.Key))) return;
            var payload = new Dictionary<string, object?> { [EquipmentControlKey] = row.Key };
            if (kind == EquipmentControlCaptured) payload[EquipmentControlProof] = row.Proof;
            events.Add(new(kind, "装备控制效果记录", payload));
        }
        foreach (var row in current.Values.Where(row => !prior.ContainsKey(row.Key))) Record(EquipmentControlCaptured, row);
        foreach (var row in prior.Values.Where(row => !current.ContainsKey(row.Key))) Record(EquipmentControlRetired, row);
        return events.Count == result.Events.Count ? result : result with { Events = events };
    }
}
