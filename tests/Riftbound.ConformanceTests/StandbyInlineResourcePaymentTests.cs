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
    [InlineData("OGN·263/298")]
    [InlineData("OGN·263a/298")]
    [InlineData("OGN·307/298")]
    [InlineData("OGN·307*/298")]
    public async Task ReplacementManaFeeCanUseAReadyRuneFromAnEmptyPool(string legendCard)
    {
        var initial = WithTeemo(Position(false), legendCard);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.HideCard);
        Assert.True(candidate.Enabled);
        Assert.Contains(candidate.OptionalCosts!, c => c.Id == "STANDBY_TEEMO_MANA");
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]));
        Assert.Contains(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]),
            c => c.Id == "TAP_RUNE:HIDE-RUNE");
        var byChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(requirement["paymentResourcePowerByChoice"]);
        Assert.Equal(1, byChoice["TAP_RUNE:HIDE-RUNE"]["mana"]);
        Assert.Equal(0, byChoice["TAP_RUNE:HIDE-RUNE"]["power"]);

        var driver = new Driver(initial);
        await driver.Act("P1", Command(["STANDBY_TEEMO_MANA", "TAP_RUNE:HIDE-RUNE"]));
        Assert.Equal("TEEMO", driver.State.CardObjects["SHADOW"].StandbyPlacement!.Permission);
        Assert.True(driver.State.CardObjects["HIDE-RUNE"].IsExhausted);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(initial.Tick + 1, driver.State.Tick);
        var tapped = Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_TAPPED");
        var paid = Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID");
        Assert.True(tapped.Sequence < paid.Sequence);
        Assert.Equal("HIDE_CARD", tapped.Event.Payload["paymentWindow"]);
        Assert.Equal(paid.Event.Payload["paymentId"], tapped.Event.Payload["paymentId"]);
        Assert.Equal(new[] { "TAP_RUNE:HIDE-RUNE" }, Assert.IsType<string[]>(paid.Event.Payload["paymentResourceActions"]));
        Assert.Equal(new[] { "HIDE-RUNE" }, Assert.IsType<string[]>(paid.Event.Payload["tappedRuneObjectIds"]));
    }

    [Theory]
    [InlineData("STANDBY_A")]
    [InlineData("STANDBY_TEEMO_MANA")]
    public async Task TappingThenRecyclingOneRunePaysEitherFeeAndKeepsTheSurplus(string fee)
    {
        var driver = new Driver(WithTeemo(Position(false)));
        await driver.Act("P1", Command(["TAP_RUNE:HIDE-RUNE", "RECYCLE_RUNE:HIDE-RUNE", fee]));
        Assert.True(driver.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Contains("HIDE-RUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(fee == "STANDBY_A" ? 1 : 0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(fee == "STANDBY_A" ? 0 : 1, driver.State.RunePools["P1"].TotalStoredPower);
        Assert.Equal(new[] { "RUNE_TAPPED", "RUNE_RECYCLED" }, driver.Events()
            .Where(e => e.Event.Kind is "RUNE_TAPPED" or "RUNE_RECYCLED").Select(e => e.Event.Kind));
        var paid = Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID");
        Assert.Equal(new[] { "TAP_RUNE:HIDE-RUNE", "RECYCLE_RUNE:HIDE-RUNE" },
            Assert.IsType<string[]>(paid.Event.Payload["paymentResourceActions"]));
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("opponent")]
    [InlineData("hidden")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("recycle-first")]
    [InlineData("capacity")]
    [InlineData("insufficient")]
    [InlineData("restricted-mana")]
    [InlineData("invalid-destination")]
    [InlineData("missing-fee")]
    public async Task InvalidInlineTapLeavesTheRuneCardAndResourcesUnchanged(string boundary)
    {
        var initial = WithTeemo(Position(boundary == "exhausted"));
        string[] choices = ["STANDBY_TEEMO_MANA", "TAP_RUNE:HIDE-RUNE"];
        if (boundary is "opponent" or "hidden") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HIDE-RUNE"] = initial.CardObjects["HIDE-RUNE"] with {
                    ControllerId = boundary == "opponent" ? "P2" : "P1", IsFaceDown = boundary == "hidden" } } };
        if (boundary == "missing") choices = ["STANDBY_TEEMO_MANA", "TAP_RUNE:missing"];
        if (boundary == "duplicate") choices = [.. choices, "TAP_RUNE:HIDE-RUNE"];
        if (boundary == "recycle-first") choices = ["STANDBY_TEEMO_MANA", "RECYCLE_RUNE:HIDE-RUNE", "TAP_RUNE:HIDE-RUNE"];
        if (boundary == "insufficient") choices = ["STANDBY_TEEMO_MANA", "RECYCLE_RUNE:HIDE-RUNE"];
        if (boundary == "missing-fee") choices = ["TAP_RUNE:HIDE-RUNE"];
        if (boundary == "capacity") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(int.MaxValue, 0) } };
        if (boundary == "restricted-mana") {
            initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(0, 0, spellOnlyMana: 1, unitPlayOrUnitAbilityOnlyMana: 1) } };
            choices = ["STANDBY_TEEMO_MANA"];
        }
        var before = MatchStateHasher.Hash(initial);
        var command = Command(choices) with { Destination = boundary == "invalid-destination" ? "BATTLEFIELD:UNKNOWN" : "BATTLEFIELD:F" };
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("hide-tap-rejected", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(boundary is "insufficient" or "restricted-mana" ? ErrorCodes.InsufficientCost : ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
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
    private static MatchState WithTeemo(MatchState state, string legendCard = "OGN·263/298") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = new("LEGEND", cardNo: legendCard, ownerId: "P1", controllerId: "P1") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            ["LEGEND"] = new("P1", "LEGEND_ZONE") } };
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
