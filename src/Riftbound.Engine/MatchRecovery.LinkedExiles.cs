namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicUnitEventEffect(MatchState? state, string? triggerId, string? effect)
        => state is not null && state.TriggerQueue.SingleOrDefault(t => t.TriggerId == triggerId) is { UnitEvent: { } context } item
            && effect == item.EffectKind && CoreRuleEngine.ValidUnitEvent(state, context, item.EffectKind, item.SourceObjectId);

    private static void ValidateLinkedExileHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var accepted = commands.Where(c => c.Accepted).ToLookup(c => c.CompletedTick);
        var expected = new Dictionary<(string Source, long Generation, string Instance), string>();
        foreach (var entry in events.Where(e => e.Event.Kind == "LINKED_EXILE_CREATED"
            && accepted[e.Tick].Any(c => c.StartedEventSequence < e.Sequence && e.Sequence <= c.CompletedEventSequence))
            .OrderBy(e => e.Sequence))
        {
            if (!entry.Event.Payload.TryGetValue("link", out var payload)
                || !TryReadLocation<LinkedExileGroup>(payload, out var group) || group?.UnitEventOrigin is not { } origin
                || origin.CreatedTick != entry.Tick)
            {
                errors.Add("linked exile history has an invalid creation");
                continue;
            }
            expected[(group.SourceObjectId, group.SourceGeneration, origin.InstanceId)] = MatchStateHasher.HashValue(group);
        }
        var actual = state.LinkedExiles.Where(g => g.UnitEventOrigin is not null).ToArray();
        if (actual.Length != expected.Count || actual.Any(g => !expected.TryGetValue(
                (g.SourceObjectId, g.SourceGeneration, g.UnitEventOrigin!.InstanceId), out var hash)
            || hash != MatchStateHasher.HashValue(g)))
            errors.Add("linked exile ledger differs from accepted banishment history");
    }
}
