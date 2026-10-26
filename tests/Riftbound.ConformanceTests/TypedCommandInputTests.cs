using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TypedCommandInputTests
{
    [Fact]
    public void CapturedArgumentsKeepTheOriginalMutableNestedSelections()
    {
        var targets = new List<string> { "repeat-target" };
        var moves = new List<string> { "repeat-battlefield" };
        var costs = new List<string> { "ECHO" };
        var command = new PlayCardCommand("source", "card", ["primary-target"], OptionalCosts: costs,
            RepeatChoices: [new SpellRepeatChoice("repeat-mode", targets, moves, "destination")]);
        var captured = TypedCommandInput.Capture(command);
        targets[0] = "changed-target";
        moves.Add("changed-battlefield");
        costs.Clear();
        Assert.True(captured.TryRestore(out var value));
        var restored = Assert.IsType<PlayCardCommand>(value);
        Assert.Equal(["primary-target"], restored.TargetObjectIds);
        Assert.Equal(["ECHO"], restored.OptionalCosts);
        var repeat = Assert.Single(restored.RepeatChoices!);
        Assert.Equal("repeat-mode", repeat.Mode);
        Assert.Equal(["repeat-target"], repeat.TargetObjectIds);
        Assert.Equal(["repeat-battlefield"], repeat.MovementDestinations);
        Assert.Equal("destination", repeat.Destination);
        Assert.Null(restored.MovementDestinations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASourceWithoutABatchAndAnExplicitEmptyRuneBatchStayDistinct(bool hasBatch)
    {
        var command = new TapRuneCommand("rune", hasBatch ? [] : null);
        Assert.True(TypedCommandInput.Capture(command).TryRestore(out var value));
        var restored = Assert.IsType<TapRuneCommand>(value);
        Assert.Equal("rune", restored.SourceObjectId);
        if (hasBatch) Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(restored.SourceObjectIds));
        else Assert.Null(restored.SourceObjectIds);
    }
}
