namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventPlayTargetHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var confirmations = events.Where(record => record.Sequence > boundary
            && record.Event.Kind == "TRIGGER_CONFIRMED"
            && TryReadObjectString(record.Event.Payload, "unitEventTargetsHash", out _)).ToArray();
        foreach (var frame in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
        {
            if (frame is null) continue;
            var items = frame.PendingEffectPlay?.Parent is { } parent ? frame.StackItems.Append(parent) : frame.StackItems;
            foreach (var item in items.Where(CoreRuleEngine.IsUnitEventGraveyardPlay))
            {
                var originals = confirmations.Where(record => TryReadObjectString(record.Event.Payload,
                    "stackItemId", out var stack) && stack == item.StackItemId).ToArray();
                if (item.TargetGenerations is null)
                {
                    if (originals.Length != 0) errors.Add("unit event public target confirmation was rewound");
                    continue;
                }
                if (originals.Length != 1
                    || !TryReadObjectString(originals[0].Event.Payload, "unitEventTargetsHash", out var hash)
                    || hash != CoreRuleEngine.UnitEventPlayTargetsHash(item)
                    || !HasAcceptedBattlefieldChoice(commands, originals[0], item.ControllerId,
                        "UNIT-EVENT:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.TargetObjectIds))
                    errors.Add("unit event public targets differ from their original accepted confirmation");
            }
        }
    }
}
