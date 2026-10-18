using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TriggerChronologyRecoveryTests
{
    [Theory]
    [InlineData("missing-legend")]
    [InlineData("zero-legend")]
    [InlineData("future-legend")]
    [InlineData("earlier-legend")]
    [InlineData("missing-companion")]
    [InlineData("earlier-companion")]
    public async Task RecordedChronologyIsBoundToTheActualUnitPlayAndCompanionOrigin(string mutation)
    {
        var (initial, driver) = await RengarUnitPlayConfirmationTests.OpenConcurrentPlay();
        await BanishEffectPlayTests.Replay(initial, driver);
        var state = driver.State;
        var original = MatchStateHasher.Hash(state);
        var forged = state with { TriggerQueue = state.TriggerQueue.Select(t => {
            if (t.UnitPlayMight is not null)
                return mutation switch {
                    "missing-legend" => t with { EnqueuedAtTick = null },
                    "zero-legend" => t with { EnqueuedAtTick = 0 },
                    "future-legend" => t with { EnqueuedAtTick = state.Tick + 1 },
                    "earlier-legend" => t with { EnqueuedAtTick = state.Tick - 1 },
                    _ => t
                };
            return mutation switch {
                "missing-companion" => t with { EnqueuedAtTick = null },
                "earlier-companion" => t with { EnqueuedAtTick = state.Tick - 1 },
                _ => t
            };
        }).ToArray() };
        var errors = OfficialInsightAndSpellLockTests.Errors(forged);
        Assert.Contains(errors, e => e.Contains(mutation.Contains("companion")
            ? "deferred permanent play ordering origin" : "unit play might trigger"));
        var command = new OrderTriggersCommand(forged.TriggerQueue.Select(t => t.TriggerId).ToArray());
        var rejected = await new CoreRuleEngine().ResolveAsync(forged, new("bad-queue-time", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(forged), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(original, MatchStateHasher.Hash(driver.State));
    }
}
