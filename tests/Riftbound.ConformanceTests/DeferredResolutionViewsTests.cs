using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeferredResolutionViewsTests
{
    [Fact]
    public void ResolvingMaterializesOnlyTheViewsSelectedByTheFinalResult()
    {
        var state = new MatchState("DEFERRED", 1, 1, "P1", new Dictionary<string, string> { ["P1"] = "P1" });
        var selected = state with { Tick = 2 };
        var materialized = new Dictionary<string, ActionPromptDto>
        {
            ["P1"] = new("P1", true, "test", ["END_TURN"], SnapshotTick: selected.Tick)
        };
        var calls = 0;
        using (DeferredResolutionViews.DuringResolution())
        {
            _ = DeferredResolutionViews.Build<SnapshotDto>(state, _ => throw new InvalidOperationException("Intermediate view"));
            var prompts = DeferredResolutionViews.Build(selected, input =>
            {
                Assert.Same(selected, input);
                calls++;
                return materialized;
            });
            var result = DeferredResolutionViews.Materialize(new ResolutionResult(true, null, selected, [],
                new Dictionary<string, SnapshotDto>(), prompts));

            Assert.Same(materialized, result.Prompts);
            Assert.Equal(1, calls);
        }

        Assert.Same(materialized, DeferredResolutionViews.Build(selected, _ => materialized));
    }
}
