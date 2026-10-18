namespace Riftbound.Engine;

internal static class TriggerQueueRecoveryRules
{
    // Validate recorded facts independently of the producer and ordering builder.
    // No absent timestamp is reconstructed when loading an old development game.
    internal static bool Valid(MatchState state)
    {
        var ticks = state.TriggerQueue.Where(t => t.EnqueuedAtTick is not null)
            .Select(t => t.EnqueuedAtTick!.Value).ToArray();
        return ticks.All(t => t > 0 && t <= state.Tick)
            && ticks.SequenceEqual(ticks.Order());
    }
}
