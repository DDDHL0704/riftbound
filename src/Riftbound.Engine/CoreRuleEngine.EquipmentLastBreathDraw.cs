using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string EquipmentLastBreathDrawCaptureKey = "equipmentLastBreathDraws";

    private static LastBreathDrawContext[] CaptureEquipmentLastBreathDraws(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState source)
    {
        // A permanent that is also a unit uses the unit-death capture exactly once.
        if (!source.Tags.Contains(CardObjectTags.EquipmentCard) || source.Tags.Contains(CardObjectTags.UnitCard)
            || source.IsFaceDown || !IsObjectOnField(zones, source.ObjectId)) return [];
        var frame = RuleChoices.Value!;
        return CaptureLastBreathDraws(state, zones, cards, source,
            $"death-{frame.Origin.Tick}-{frame.DeathSequence++}");
    }

    private static ResolutionResult QueueEquipmentLastBreathDraws(ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Payload.ContainsKey(EquipmentLastBreathDrawCaptureKey))) return result;
        var queue = result.State.TriggerQueue.ToList();
        var events = new List<GameEvent>();
        foreach (var ev in result.Events)
        {
            events.Add(ev.Payload.ContainsKey(EquipmentLastBreathDrawCaptureKey)
                ? ev with { Payload = ev.Payload.Where(p => p.Key != EquipmentLastBreathDrawCaptureKey)
                    .ToDictionary(p => p.Key, p => p.Value) } : ev);
            if (result.State.Status != MatchStatuses.InProgress
                || ev.Payload.GetValueOrDefault(EquipmentLastBreathDrawCaptureKey) is not IReadOnlyList<LastBreathDrawContext> captured) continue;
            foreach (var birth in captured)
            {
                var trigger = LastBreathDrawTrigger(birth with {
                    CreatedAtTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick });
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
