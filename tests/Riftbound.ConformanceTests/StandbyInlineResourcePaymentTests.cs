using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class StandbyInlineResourcePaymentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyPoolOffersHideAndRecyclesTheChosenRuneWithinAtomicPayment(bool exhausted)
    {
        var initial = Position(exhausted);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.HideCard);
        Assert.True(candidate.Enabled);
        Assert.Contains(candidate.OptionalCosts!, c => c.Id == "STANDBY_A");
        Assert.Contains(candidate.OptionalCosts!, c => c.Id == "RECYCLE_RUNE:HIDE-RUNE");
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]));
        Assert.Equal(1, requirement["costChoiceMinCount"]);
        Assert.Equal(1, requirement["costChoiceMaxCount"]);
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]));
        Assert.Contains(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]), c => c.Id == "RECYCLE_RUNE:HIDE-RUNE");
        var driver = new Driver(initial);
        await driver.Act("P1", Command(["STANDBY_A", "RECYCLE_RUNE:HIDE-RUNE"]));
        Assert.True(driver.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.DoesNotContain("HIDE-RUNE", driver.State.PlayerZones["P1"].Base);
        Assert.Contains("HIDE-RUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalStoredPower);
        var recycled = Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_RECYCLED").Event;
        var paid = Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal("HIDE_CARD", recycled.Payload["paymentWindow"]);
        Assert.Equal(paid.Payload["paymentId"], recycled.Payload["paymentId"]);
        Assert.Equal(new[] { "RECYCLE_RUNE:HIDE-RUNE" }, Assert.IsType<string[]>(paid.Payload["paymentResourceActions"]));
        await driver.Replay();
    }

    [Theory]
    [InlineData("missing-fee")]
    [InlineData("two-fees")]
    [InlineData("duplicate-rune")]
    [InlineData("opponent-rune")]
    [InlineData("face-down")]
    [InlineData("invalid-destination")]
    [InlineData("capacity")]
    [InlineData("insufficient")]
    public async Task InvalidSelectionDoesNotRecycleOrHideAnything(string boundary)
    {
        var initial = Position(false);
        string[] choices = ["STANDBY_A", "RECYCLE_RUNE:HIDE-RUNE"];
        if (boundary == "missing-fee") choices = ["RECYCLE_RUNE:HIDE-RUNE"];
        if (boundary == "two-fees") choices = ["STANDBY_A", "STANDBY_A", "RECYCLE_RUNE:HIDE-RUNE"];
        if (boundary == "duplicate-rune") choices = [.. choices, "RECYCLE_RUNE:HIDE-RUNE"];
        if (boundary == "insufficient") choices = ["STANDBY_A"];
        if (boundary is "opponent-rune" or "face-down") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HIDE-RUNE"] = initial.CardObjects["HIDE-RUNE"] with {
                    ControllerId = boundary == "opponent-rune" ? "P2" : "P1", IsFaceDown = boundary == "face-down" } } };
        if (boundary == "capacity") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 0, spellOnlyPower: int.MaxValue) } };
        var command = Command(choices) with { Destination = boundary == "invalid-destination" ? "BATTLEFIELD:UNKNOWN" : "BATTLEFIELD:F" };
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("hide-rejected", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(boundary == "insufficient" ? ErrorCodes.InsufficientCost : ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    private static HideCardCommand Command(string[] costs) => new("SHADOW", "OGN·135/298", "BATTLEFIELD:F", costs);
    private static MatchState Position(bool exhausted)
    {
        var state = StandbySourceRulesTests.Position("OGN·135/298");
        return state with { Tick = 0,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Append("HIDE-RUNE").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HIDE-RUNE"] = new("HIDE-RUNE", cardNo: "UNL-R03", ownerId: "P1", controllerId: "P1", isExhausted: exhausted,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HIDE-RUNE"] = new("P1", "BASE") } };
    }
}
