using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// The append-only journal uses transport ticks. The retained gameplay branch
// uses the original rule ticks for provenance checks, including IDs that embed them.
internal static class RewindRecoveryHistory
{
    private sealed record ActionNode(RecoveredCommand Command, ActionNode? Previous);
    private sealed record Point(long RuleTick, ActionNode? Previous);

    internal static (long TickOffset, IReadOnlyList<RecoveredCommand> Commands, IReadOnlyList<RecoveredEvent> Events)
        RetainedBranch(IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (!commands.Any(command => command.Accepted && MatchSession.IsRewindCommandType(command.CommandType)))
            return (0, commands, events);

        var points = new Dictionary<long, Point>();
        var eventsBySequence = events.ToLookup(record => record.Sequence);
        ActionNode? tail = null;
        long offset = 0;
        foreach (var command in commands.OrderBy(command => command.StartedEventSequence)
            .ThenBy(command => command.CompletedEventSequence))
        {
            if (!command.Accepted) continue;
            var step = command.StartedEventSequence + 1;
            var before = command.StartedTick - offset;
            if (command.CompletedEventSequence >= step)
                points.TryAdd(step, new Point(before, tail));

            if (MatchSession.IsRewindCommandType(command.CommandType))
            {
                var after = before;
                if (command.CommandType == CommandTypes.AcceptRewind)
                {
                    var rewind = eventsBySequence[step].FirstOrDefault()?.Event;
                    if (rewind is not { Kind: "MATCH_REWOUND", Payload: { } payload }
                        || !payload.TryGetValue("targetStep", out var value)
                        || !TryReadStep(value, out var targetStep) || targetStep >= step
                        || !points.TryGetValue(targetStep, out var target))
                    {
                        errors.Add($"accepted rewind at step {step} has no earlier target");
                        continue;
                    }
                    tail = target.Previous;
                    after = target.RuleTick;
                }
                offset = command.CompletedTick - after;
                continue;
            }

            var translated = command with { StartedTick = before, CompletedTick = command.CompletedTick - offset };
            tail = new ActionNode(translated, tail);
        }

        var retainedCommands = new List<RecoveredCommand>();
        for (var node = tail; node is not null; node = node.Previous)
            retainedCommands.Add(node.Command);
        retainedCommands.Reverse();

        var retainedEvents = new List<RecoveredEvent>();
        var currentCommand = 0;
        foreach (var record in events.OrderBy(record => record.Sequence))
        {
            while (currentCommand < retainedCommands.Count
                && record.Sequence > retainedCommands[currentCommand].CompletedEventSequence)
                currentCommand++;
            if (currentCommand == retainedCommands.Count) break;
            var command = retainedCommands[currentCommand];
            if (record.Sequence > command.StartedEventSequence)
                retainedEvents.Add(record with { Tick = command.CompletedTick });
        }
        return (offset, retainedCommands, retainedEvents);
    }

    private static bool TryReadStep(object? value, out long step)
    {
        step = 0;
        if (value is long number)
        {
            step = number;
            return true;
        }
        return value is JsonElement { ValueKind: JsonValueKind.Number } element && element.TryGetInt64(out step);
    }
}
