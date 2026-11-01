using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZeroPrintedPowerPromptTests
{
    [Theory]
    [InlineData(false, false, 1, 0)]
    [InlineData(false, true, 2, 0)]
    [InlineData(true, false, 2, 1)]
    [InlineData(true, true, 3, 1)]
    public async Task ZeroPrintedPowerOffersNoEmptyTraitAllocationWhileNormalFlowAndEchoPayTheirActualCosts(
        bool flow, bool echo, int mana, int power)
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: flow);
        if (echo) initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!,
            c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            candidate.Metadata!["sourceRequirements"]));
        Assert.Equal("RUSH", requirement["sourceObjectId"]);
        Assert.Equal(0, requirement["printedPowerCost"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["printedPowerChoices"]));
        Assert.DoesNotContain(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]),
            choice => choice.Id.StartsWith("PRINTED_POWER:", StringComparison.Ordinal));

        var command = new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [],
            OptionalCosts: echo ? ["ECHO"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower);
        Assert.Empty(quote.Cost.PowerByTrait);
        Assert.Equal(1, quote.Cost.PrintedMana);
        Assert.Equal(0, quote.Cost.PrintedPower);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(mana, initial.RunePools["P1"].Mana - driver.State.RunePools["P1"].Mana);
        Assert.Equal(power, initial.RunePools["P1"].TotalPower - driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(flow, Assert.Single(driver.State.StackItems).FlowPlay is not null);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AnExplicitEmptyAllocationForZeroPrintedPowerStillRejectsWithoutChangingAnyState()
    {
        var initial = LightningRushDeckDrawFlowTests.Position();
        var before = MatchStateHasher.Hash(initial);
        var command = new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [],
            OptionalCosts: ["PRINTED_POWER:"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.False(quote.IsValid);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("original-empty-allocation-refusal", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Equal("卡面符能分配无效。", result.ErrorMessage);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }
}
