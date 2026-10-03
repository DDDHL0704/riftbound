using Riftbound.GodotClient;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NativePooledDamageSelectionTests
{
    private static readonly DamageAssignmentPromptItem[] Sources =
    [
        new("A1", "来源一", 2, [new("D1", "目标一", 3), new("D2", "目标二", 1)]),
        new("A2", "来源二", 2, [new("D1", "目标一", 3), new("D2", "目标二", 1)])
    ];

    [Fact]
    public void TargetTotalCanSpanMoreThanOneSource()
    {
        Assert.True(PooledDamageSelection.TrySplit(Sources, new Dictionary<string, long> { ["D1"] = 3, ["D2"] = 1 }, out var values));
        Assert.Equal(new DamageAssignmentSelection[] { new("A1", "D1", 2), new("A2", "D1", 1), new("A2", "D2", 1) }, values);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 2)]
    [InlineData(-1, 5)]
    public void IncompleteExcessOrNegativeTotalsCannotBeSubmitted(long first, long second)
        => Assert.False(PooledDamageSelection.TrySplit(Sources, new Dictionary<string, long> { ["D1"] = first, ["D2"] = second }, out _));

    [Fact]
    public void UnknownTargetCannotEnterPayload()
        => Assert.False(PooledDamageSelection.TrySplit(Sources, new Dictionary<string, long> { ["UNKNOWN"] = 4 }, out _));

    [Fact]
    public void TargetOrderAndLethalAreJudgedByServer()
    {
        Assert.True(PooledDamageSelection.TrySplit(Sources, new Dictionary<string, long> { ["D2"] = 4 }, out var values));
        Assert.All(values, value => Assert.Equal("D2", value.TargetObjectId));
    }
}
