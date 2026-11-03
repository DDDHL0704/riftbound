using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateBoundLegendHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var ledger = new Dictionary<string, BoundLegend>(StringComparer.Ordinal);
        foreach (var recovered in events.Where(e => e.Event.Kind is "BOUND_LEGEND_ENTERED" or "BOUND_LEGEND_DEPARTED"))
        {
            var ev = recovered.Event;
            BoundLegend? binding = null;
            try {
                if (ev.Payload.TryGetValue("boundLegend", out var value))
                    binding = value as BoundLegend ?? JsonSerializer.Deserialize<BoundLegend>(
                        JsonSerializer.Serialize(value), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            } catch (JsonException) { }
            if (binding is null || binding.Source is null || string.IsNullOrWhiteSpace(binding.LegendObjectId)
                || string.IsNullOrWhiteSpace(binding.PlayerId) || string.IsNullOrWhiteSpace(binding.LegendCardNo)
                || string.IsNullOrWhiteSpace(binding.SourceCardNo) || string.IsNullOrWhiteSpace(binding.Source.ObjectId)
                || !commands.Any(command => command.Accepted
                && recovered.Sequence > command.StartedEventSequence && recovered.Sequence <= command.CompletedEventSequence))
            {
                errors.Add("bound legend event lacks an accepted creation or departure receipt");
                continue;
            }
            if (ev.Kind == "BOUND_LEGEND_ENTERED")
            {
                if (binding.EnteredAtTick != recovered.Tick || binding.DepartedAtTick is not null
                    || !ledger.TryAdd(binding.LegendObjectId, binding))
                    errors.Add("bound legend creation receipt is invalid or repeated");
            }
            else if (!ledger.TryGetValue(binding.LegendObjectId, out var original)
                || original.DepartedAtTick is not null || binding.DepartedAtTick != recovered.Tick
                || original != binding with { DepartedAtTick = null })
                errors.Add("bound legend departure receipt differs from its creation");
            else ledger[binding.LegendObjectId] = binding;
        }
        var actual = state.BoundLegends ?? [];
        if (ledger.Count != actual.Count || actual.Any(binding => binding is null
            || string.IsNullOrWhiteSpace(binding.LegendObjectId)
            || !ledger.TryGetValue(binding.LegendObjectId, out var recorded) || binding != recorded))
            errors.Add("bound legend provenance differs from accepted history");
        if (ledger.Count == 0 && actual.Count == 0) return;
        foreach (var item in state.StackItems.Where(item =>
            CoreRuleEngine.IsDepartedBoundLegendSource(state, item.SourceObjectId, item.CardNo)))
        {
            var binding = actual.Single(binding => binding.LegendObjectId == item.SourceObjectId);
            var lifetime = events.Where(recovered => recovered.Tick >= binding.EnteredAtTick
                && recovered.Tick <= binding.DepartedAtTick).ToArray();
            var activation = lifetime.Any(recovered => recovered.Event.Kind == "STACK_ITEM_ADDED"
                && ReadNormalizedPayloadString(recovered.Event.Payload, "stackItemId") == item.StackItemId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "sourceObjectId") == item.SourceObjectId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "controllerId") == item.ControllerId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "cardNo") == item.CardNo
                && ReadNormalizedPayloadString(recovered.Event.Payload, "effectKind") == item.EffectKind);
            var trigger = lifetime.FirstOrDefault(recovered => recovered.Event.Kind == "TRIGGER_QUEUED"
                && "ordered-" + ReadNormalizedPayloadString(recovered.Event.Payload, "triggerId") == item.StackItemId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "sourceObjectId") == item.SourceObjectId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "controllerId") == item.ControllerId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "effectKind") == item.EffectKind);
            var published = trigger is not null && lifetime.Any(recovered => recovered.Sequence > trigger.Sequence
                && recovered.Event.Kind == "TRIGGERS_MOVED_TO_STACK"
                && TryReadObjectStringList(recovered.Event.Payload, "stackItemIds", out var ids)
                && ids.Contains(item.StackItemId, StringComparer.Ordinal));
            if (!activation && !published)
                errors.Add("departed legend skill lacks its accepted activation receipt");
        }
        foreach (var modifier in state.CardObjects.Values.Where(card => card is not null)
            .SelectMany(card => card.UntilEndOfTurnPowerModifiers ?? [])
            .Where(modifier => modifier is not null && modifier.SourceObjectId is { } source && modifier.SourceCardNo is { } no
                && CoreRuleEngine.IsDepartedBoundLegendSource(state, source, no)))
        {
            var binding = actual.Single(binding => binding.LegendObjectId == modifier.SourceObjectId);
            if (!events.Any(recovered => recovered.Tick >= binding.EnteredAtTick && recovered.Tick <= state.Tick
                && recovered.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"
                && ReadNormalizedPayloadString(recovered.Event.Payload, "sourceObjectId") == modifier.SourceObjectId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "targetObjectId") == modifier.TargetObjectId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "reason") == modifier.EffectKind
                && TryReadObjectInt(recovered.Event.Payload, "appliedPowerDelta", out var delta) && delta == modifier.PowerDelta
                && TryReadObjectInt(recovered.Event.Payload, "resultingPower", out var power) && power == modifier.ResultingPower
                && commands.Any(command => command.Accepted && recovered.Sequence > command.StartedEventSequence
                    && recovered.Sequence <= command.CompletedEventSequence)))
                errors.Add("departed legend modifier lacks its accepted resolution receipt");
        }
    }
}
