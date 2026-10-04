using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class PermanentConfirmationAssert
{
    internal static async Task<ResolutionResult> ResolveAfterPlayAsync(CoreRuleEngine engine, ResolutionResult played)
    {
        Assert.True(played.Accepted, played.ErrorMessage);
        var result = played;
        var events = played.Events.ToList();
        for (var i = 0; result.State.StackItems.Count > 0 && i < 20; i++)
        {
            result = await engine.ResolveAsync(result.State,
                new($"confirmation-pass-{result.State.Tick}-{i}", result.State.PriorityPlayerId!, CommandTypes.PassPriority),
                new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            events.AddRange(result.Events);
        }
        Assert.Empty(result.State.StackItems);
        return result with { Events = events };
    }

    // Stale/reordered/duplicate command tests retain full state, prompt, snapshot and
    // journal comparisons. CN 359.2 replaces their obsolete permanent stack probe.
    internal static string Entry(ResolutionResult result, string sourceId, bool hasPlayAbility,
        string? expectedStateHash = null)
    {
        Assert.Equal(1, result.State.Tick);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.DoesNotContain(sourceId, result.State.PlayerZones["P1"].Hand);
        Assert.Contains(sourceId, result.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", result.State.ObjectLocations[sourceId].Zone);
        Assert.False(result.State.CardObjects[sourceId].IsFaceDown);
        if (hasPlayAbility)
        {
            Assert.True(Assert.Single(result.State.StackItems).SourceConfirmed);
            Assert.Equal("P1", result.State.PriorityPlayerId);
        }
        else
        {
            Assert.Empty(result.State.StackItems);
            Assert.Null(result.State.PriorityPlayerId);
            Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        }
        var hash = MatchStateHasher.Hash(result.State);
        if (expectedStateHash is not null) Assert.Equal(expectedStateHash, hash);
        return hash;
    }
}
