using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// CN Radiance judge FAQ, DOC-17, common-card questions. Exercise the
// named cards through commands, including their actual costs and responses.
public sealed class RadJudgeFaqExamplesTests
{
    [Fact]
    public async Task VilemawBlockedStandardMoveCannotExhaustInventorOrTriggerIt()
    {
        var state = Put(Create("OGN·295/298"), "INVENTOR", "RAD·105/167", "P1", "BATTLEFIELD", "BF");
        Assert.False(state.CardObjects["INVENTOR"].IsExhausted);
        await Reject(state, "P1", new MoveUnitCommand("INVENTOR", "BATTLEFIELD", "BASE"));
        Assert.False(state.CardObjects["INVENTOR"].IsExhausted);
        Assert.Null(state.PendingCardChoice);
        Assert.Empty(state.StackItems);
        Assert.Empty(state.TriggerQueue);
        Assert.DoesNotContain(
            (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
                .Where(candidate => candidate.Action == CommandTypes.MoveUnit)
                .SelectMany(candidate => candidate.Sources ?? []),
            source => source.Id == "INVENTOR");
    }

}
