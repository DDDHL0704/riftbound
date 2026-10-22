using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// Independent CN 190.6 / 191.5 checks for the two current start consumers.
// A source can change control after capture; its current controller is not an oracle.
internal static class BattlefieldStartRecoveryRules
{
    private const string Damage = "CAPTURED_BATTLEFIELD_START_DAMAGE";
    private const string Draw = "CAPTURED_BATTLEFIELD_START_DESTROY_DRAW";

    internal static bool Valid(MatchState state, BattlefieldStartContext? context,
        string effect, string controller, string source, string? cardNo = null)
    {
        if (context is null) return effect is not (Damage or Draw);
        var number = context.CardNo?.Replace('\u00b7', '-');
        var definition = effect == Damage && number == "UNL-212/219" && context.Damage == 1 && context.DrawCount == 0
            || effect == Draw && number == "UNL-209/219" && context.Damage == 0 && context.DrawCount == 1;
        return definition && context.SourceGeneration >= 0 && (cardNo is null || cardNo == context.CardNo)
            && !string.IsNullOrWhiteSpace(context.BattlefieldObjectId) && source == context.BattlefieldObjectId
            && context.ControllerId == controller && state.Seats.ContainsKey(controller)
            && state.Phase == "TURN_START" && state.TurnStartStep == "SCORE"
            && state.CardObjects.TryGetValue(source, out var field)
            && (field.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal)
                || field.CardNo?.Replace('\u00b7', '-') is "UNL-212/219" or "UNL-209/219");
    }

    internal static IReadOnlyList<string> ValidateOriginalTriggers(MatchRecoveryFrame frame)
    {
        if (frame.AuthoritativeState is not { } state) return [];
        var errors = new List<string>();
        foreach (var item in state.TriggerQueue.Where(t => t.BattlefieldStart is not null || t.EffectKind is Damage or Draw))
            Check(item.TriggerId, item.BattlefieldStart, item.EffectKind, item.ControllerId, item.SourceObjectId, item.SourceCardNo);
        foreach (var item in state.StackItems.Where(t => t.BattlefieldStart is not null || t.EffectKind is Damage or Draw))
        {
            if (!item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal))
                errors.Add("captured battlefield start ability has no original ordered trigger identity");
            else Check(item.StackItemId["ordered-".Length..], item.BattlefieldStart, item.EffectKind,
                item.ControllerId, item.SourceObjectId, item.CardNo);
        }
        return errors;

        void Check(string triggerId, BattlefieldStartContext? context, string effect, string actor, string source, string? cardNo)
        {
            if (!Valid(state, context, effect, actor, source, cardNo))
            {
                errors.Add("invalid captured battlefield start controller or definition");
                return;
            }
            var origins = frame.Events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && Read(e.Event, "triggerId") == triggerId).ToArray();
            if (frame.Commands.Count == 0 || origins.Length != 1)
            {
                errors.Add("captured battlefield start requires one original journaled trigger birth");
                return;
            }
            var origin = origins[0];
            if (Read(origin.Event, "controllerId") != actor || Read(origin.Event, "sourceObjectId") != source
                || Read(origin.Event, "effectKind") != effect || Read(origin.Event, "triggeredByEventKind") != "TURN_START_BEGAN")
                errors.Add("captured battlefield start controller or source differs from its original trigger birth");
            if (frame.Commands.Count(c => c.Accepted && c.StartedEventSequence < origin.Sequence
                && c.CompletedEventSequence >= origin.Sequence && c.CompletedTick == origin.Tick) != 1)
                errors.Add("captured battlefield start trigger birth has no unique original accepted input");
        }
    }

    private static string? Read(GameEvent gameEvent, string key)
        => gameEvent.Payload.GetValueOrDefault(key) switch {
            string value => value,
            JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(),
            _ => null };
}
