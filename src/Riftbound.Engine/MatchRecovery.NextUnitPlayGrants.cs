namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateNextUnitPlayGrantHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var actual = (state.UntilEndOfTurnEffects ?? []).Where(m =>
            m?.StartsWith(CoreRuleEngine.NextUnitPlayGrantPrefix, StringComparison.Ordinal) == true).ToArray();
        var pending = state.StackItems.Select(i => i.NextUnitPlayGrant)
            .Concat(state.TriggerQueue.Select(t => t.NextUnitPlayGrant)).OfType<NextUnitPlayGrant>().ToArray();
        if (actual.Length == 0 && pending.Length == 0
            && !events.Any(e => e.Event.Kind == "NEXT_UNIT_PLAY_GRANT_CREATED")) return;

        var start = events.Where(e => e.Event.Kind == "TURN_PLAYER_ADVANCED"
            && TryReadObjectInt(e.Event.Payload, "turnNumber", out var turn) && turn == state.TurnNumber)
            .Select(e => e.Sequence).DefaultIfEmpty(0).Max();
        var accepted = commands.Where(c => c.Accepted).ToLookup(c => c.CompletedTick);
        var current = events.Where(e => e.Sequence > start
            && accepted[e.Tick].Any(c => e.Sequence > c.StartedEventSequence && e.Sequence <= c.CompletedEventSequence))
            .OrderBy(e => e.Sequence).ToArray();
        bool HasConquest(NextUnitPlayGrant grant) => grant.Source is not null && current.Any(e =>
            e.Tick == grant.ConquestTick && e.Event.Kind == "BATTLEFIELD_CONQUERED"
            && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == grant.ControllerId
            && TryReadObjectString(e.Event.Payload, "battlefieldId", out var field) && field == grant.Source.ObjectId);
        foreach (var grant in pending)
            if (!HasConquest(grant)) errors.Add("next unit play grant history has no accepted conquest");

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in current)
        {
            var ev = entry.Event;
            if (ev.Kind == "NEXT_UNIT_PLAY_GRANT_CREATED")
            {
                if (!TryReadObjectString(ev.Payload, "effectId", out var marker) || marker is null
                    || CoreRuleEngine.ReadNextUnitPlayGrant(marker) is not { } grant || !HasConquest(grant)
                    || !TryReadObjectString(ev.Payload, "playerId", out var player) || player != grant.ControllerId
                    || !TryReadObjectString(ev.Payload, "sourceObjectId", out var source) || source != grant.Source.ObjectId
                    || !expected.Add(marker))
                    errors.Add("next unit play grant history has an invalid creation");
            }
            if (ev.Kind is "UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED"
                && TryReadObjectStringList(ev.Payload, CoreRuleEngine.NextUnitPlayConsumedKey, out var consumed))
                foreach (var marker in consumed)
                    if (!expected.Remove(marker)
                        || CoreRuleEngine.ReadNextUnitPlayGrant(marker) is not { } grant
                        || !TryReadObjectString(ev.Payload, "playerId", out var player) || player != grant.ControllerId)
                        errors.Add("next unit play grant history has an invalid consumption");
        }
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length || !expected.SetEquals(actual))
            errors.Add("next unit play grant history differs from the current grants");
    }
}
