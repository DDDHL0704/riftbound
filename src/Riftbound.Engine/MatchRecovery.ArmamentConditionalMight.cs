namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateArmamentAttachmentTurnHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var frames = new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>().ToArray();
        if (events.Any(entry => entry.Event.Payload is null)
            || frames.Any(frame => frame.CardObjects.Values.Any(card => card.UntilEndOfTurnEffects is null)))
        {
            errors.Add("armament attachment-turn history contains a null original payload or effect list");
            return;
        }
        if (!frames.Any(frame => frame.CardObjects.Values.Any(card =>
                card.UntilEndOfTurnEffects.Contains(CoreRuleEngine.ArmamentAttachedThisTurn)))
            && !events.Any(entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.ArmamentAttachmentTurnObjectKey))) return;
        foreach (var frame in frames)
        {
            var boundary = CurrentGameAcceptedEventBoundary(frame, commands, events);
            var attached = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick)
                .OrderBy(entry => entry.Sequence))
            {
                if (entry.Event.Kind == "TURN_END_CLEANUP_STARTED") { attached.Clear(); continue; }
                if (!entry.Event.Payload.ContainsKey(CoreRuleEngine.ArmamentAttachmentTurnObjectKey)) continue;
                if (entry.Event.Kind != "EQUIPMENT_ATTACHED"
                    || !TryReadObjectString(entry.Event.Payload, CoreRuleEngine.ArmamentAttachmentTurnObjectKey, out var key)
                    || key is not { Length: 64 } || !key.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f')
                    || !commands.Any(command => command.Accepted && command.CompletedTick == entry.Tick
                        && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence))
                {
                    errors.Add("armament attachment-turn fact lacks its original accepted attachment");
                    continue;
                }
                attached.Add(key);
            }
            foreach (var card in frame.CardObjects.Values)
            {
                var actual = card.UntilEndOfTurnEffects.Contains(CoreRuleEngine.ArmamentAttachedThisTurn);
                var onField = !card.IsFaceDown && frame.PlayerZones.Values.Any(zones =>
                    zones.Base.Contains(card.ObjectId) || zones.Battlefields.Contains(card.ObjectId));
                var expected = onField && attached.Contains(CoreRuleEngine.ArmamentAttachmentTurnObject(card));
                if (actual != expected)
                    errors.Add("armament attachment-turn state differs from original accepted attachment and turn cleanup history");
            }
        }
    }
}
