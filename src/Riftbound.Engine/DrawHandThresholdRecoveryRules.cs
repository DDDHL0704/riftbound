using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class DrawHandThresholdRecoveryRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static bool IsCapturedSkill(MatchState state, StackItemState item)
        => item.DrawHandThreshold is not null && CoreRuleEngine.ValidDrawHandThreshold(state, item);
    internal static bool IsCapturedSkill(MatchState state, TriggerQueueItemState item)
        => item.DrawHandThreshold is not null && CoreRuleEngine.ValidDrawHandThreshold(state, item);

    // A persisted count is not its own proof. Authenticate the original accepted
    // draw, trigger birth and opponent choice; the ordinary replay audit then
    // verifies those counts against the command's real draw instruction.
    internal static IReadOnlyList<string> ValidateOriginalTriggers(MatchRecoveryFrame frame)
    {
        if (frame.AuthoritativeState is not { } state) return [];
        var errors = new List<string>();
        foreach (var item in state.TriggerQueue)
        {
            if (!CoreRuleEngine.ValidDrawHandThreshold(state, item)) errors.Add("invalid queued draw hand-threshold skill");
            else if (item.DrawHandThreshold is { } c) Check(c, item.TriggerId, null);
        }
        foreach (var item in state.StackItems)
        {
            if (!CoreRuleEngine.ValidDrawHandThreshold(state, item)) errors.Add("invalid stacked draw hand-threshold skill");
            else if (item.DrawHandThreshold is { } c) Check(c, CoreRuleEngine.DrawHandThresholdTriggerId(c), item.StackItemId);
        }
        return errors;

        RecoveredCommand? Command(RecoveredEvent ev)
        {
            var matches = frame.Commands.Where(c => c.Accepted && c.CompletedTick == ev.Tick
                && c.StartedEventSequence < ev.Sequence && c.CompletedEventSequence >= ev.Sequence).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        void Check(DrawHandThresholdContext c, string triggerId, string? stackId)
        {
            var births = frame.Events.Where(e => e.Event.Kind == "TRIGGER_QUEUED" && Text(e.Event, "triggerId") == triggerId).ToArray();
            if (births.Length != 1 || Command(births[0]) is not { } original || births[0].Tick != c.CapturedTick
                || Text(births[0].Event, "controllerId") != c.ControllerId
                || Text(births[0].Event, "sourceObjectId") != c.Source.ObjectId
                || Text(births[0].Event, "effectKind") != CoreRuleEngine.DrawHandThresholdEffect
                || !Same(births[0].Event.Payload.GetValueOrDefault("drawHandThreshold"), c with { OpponentId = null }))
            { errors.Add("draw hand-threshold skill differs from its original trigger birth"); return; }

            var draws = frame.Events.Where(e => e.Event.Kind == "CARD_DRAWN"
                && e.Sequence > original.StartedEventSequence && e.Sequence < births[0].Sequence
                && Text(e.Event, "drawInstructionId") == c.DrawId && Text(e.Event, "playerId") == c.ControllerId).ToArray();
            if (draws.Length != 1 || Number(draws[0].Event, "count") != c.DrawnCount)
                errors.Add("draw hand-threshold skill differs from its original draw instruction");

            var choices = stackId is null ? [] : frame.Events.Where(e => e.Event.Kind == "DRAW_HAND_OPPONENT_CONFIRMED"
                && Text(e.Event, "stackItemId") == stackId).ToArray();
            if (c.OpponentId is null ? choices.Length != 0 : choices.Length != 1 || Command(choices[0]) is not { } chosen
                || chosen.PlayerId != c.ControllerId || choices[0].Sequence <= births[0].Sequence
                || Text(choices[0].Event, "controllerId") != c.ControllerId
                || Text(choices[0].Event, "opponentId") != c.OpponentId
                || Text(choices[0].Event, "sourceObjectId") != c.Source.ObjectId)
                errors.Add("draw hand-threshold opponent differs from its original confirmation");
        }
    }

    private static bool Same(object? a, object? b) => a is not null && b is not null
        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a, a.GetType(), Json), JsonSerializer.SerializeToNode(b, b.GetType(), Json));
    private static string? Text(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch
    { string value => value, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
    private static int? Number(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch
    { int value => value, JsonElement { ValueKind: JsonValueKind.Number } value when value.TryGetInt32(out var n) => n, _ => null };
}
