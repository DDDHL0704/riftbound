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
            || !ledger.TryGetValue(binding.LegendObjectId, out var recorded) || binding != recorded))
            errors.Add("bound legend provenance differs from accepted history");
        foreach (var item in state.StackItems.Where(item =>
            CoreRuleEngine.IsDepartedBoundLegendSource(state, item.SourceObjectId, item.CardNo)))
        {
            var binding = actual.Single(binding => binding.LegendObjectId == item.SourceObjectId);
            if (!events.Any(recovered => recovered.Tick >= binding.EnteredAtTick && recovered.Tick <= binding.DepartedAtTick
                && recovered.Event.Kind == "STACK_ITEM_ADDED"
                && ReadNormalizedPayloadString(recovered.Event.Payload, "stackItemId") == item.StackItemId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "sourceObjectId") == item.SourceObjectId
                && ReadNormalizedPayloadString(recovered.Event.Payload, "cardNo") == item.CardNo
                && ReadNormalizedPayloadString(recovered.Event.Payload, "effectKind") == item.EffectKind))
                errors.Add("departed legend skill lacks its accepted activation receipt");
        }
    }
}
