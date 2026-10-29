using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 164.2.a, 204.4.b.1, 430.2.a and 430.3: acquire resources within
// payment without passing focus; the play and acquisition commit atomically.
public sealed class PlayRuneTapPaymentTests
{
    [Fact]
    public async Task EmptyManaPoolOffersTheCardAndInlineTapsSurviveRestoreAndReplay()
    {
        var initial = Position();
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.True(candidate.Enabled);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements);
        Assert.Equal(0, requirement["availableMana"]);
        Assert.Equal(4, requirement["availableManaWithPaymentResources"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]);
        Assert.All(Taps, action => Assert.Contains(choices, choice => choice.Id == action));
        var command = PlayCostPreviewTests.Command(Taps);
        var before = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        var driver = new Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.All(Enumerable.Range(1, 4), n => Assert.True(driver.State.CardObjects[$"TAP-PAY-R{n}"].IsExhausted));
        Assert.Equal(4, driver.Events().Count(e => e.Event.Kind == "RUNE_TAPPED"));
        var paid = Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(Taps, Assert.IsType<string[]>(paid.Payload["paymentResourceActions"]));
        Assert.Equal(new[] { "TAP-PAY-R1", "TAP-PAY-R2", "TAP-PAY-R3", "TAP-PAY-R4" }, Assert.IsType<string[]>(paid.Payload["tappedRuneObjectIds"]));
        await driver.Replay();
    }

    [Fact]
    public async Task OneRuneMayBeTappedThenRecycledForTheSameCardPayment()
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = RunePool.Empty } };
        var driver = new Driver(initial);
        await driver.Act("P1", PlayCostPreviewTests.Command([.. Taps, "RECYCLE_RUNE:TAP-PAY-R1"]));
        Assert.Contains("TAP-PAY-R1", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalStoredPower);
        await driver.Replay();
    }

    [Fact]
    public async Task OneRemainingManaCapacityOffersEveryRuneButRejectsSelectingTwo()
    {
        var initial = Position() with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(int.MaxValue - 1, 1), ["P2"] = RunePool.Empty } };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]));
        Assert.Equal(int.MaxValue, requirement["availableManaWithPaymentResources"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]);
        Assert.All(Taps, action => Assert.Contains(choices, choice => choice.Id == action));

        var command = PlayCostPreviewTests.Command(Taps.Take(2).ToArray());
        var before = MatchStateHasher.Hash(initial);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("capacity-overflow", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));

        foreach (var action in Taps)
        {
            var driver = new Driver(initial);
            await driver.Act("P1", PlayCostPreviewTests.Command([action]));
            Assert.Equal(int.MaxValue - 4, driver.State.RunePools["P1"].Mana);
            Assert.Equal(action["TAP_RUNE:".Length..], Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_TAPPED").Event.Payload["sourceObjectId"]);
            await driver.Replay();
        }
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("recycle-first")]
    [InlineData("opponent")]
    [InlineData("exhausted")]
    [InlineData("face-down")]
    [InlineData("insufficient")]
    [InlineData("capacity")]
    public async Task IllegalOrInsufficientPaymentDoesNotTapOrCommitAnything(string boundary)
    {
        var initial = Position();
        var actions = Taps;
        if (boundary == "duplicate") actions = [.. Taps, "TAP_RUNE:TAP-PAY-R1"];
        if (boundary == "recycle-first") actions = ["RECYCLE_RUNE:TAP-PAY-R1", .. Taps];
        if (boundary is "opponent" or "exhausted" or "face-down")
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["TAP-PAY-R1"] = initial.CardObjects["TAP-PAY-R1"] with { ControllerId = boundary == "opponent" ? "P2" : "P1",
                    IsExhausted = boundary == "exhausted", IsFaceDown = boundary == "face-down" } } };
        if (boundary == "insufficient") actions = Taps.Take(3).ToArray();
        if (boundary == "capacity") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(int.MaxValue - 3, 1) } };
        var before = MatchStateHasher.Hash(initial);
        var command = PlayCostPreviewTests.Command(actions);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.False(quote.CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("tap-rejected", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    [Fact]
    public void HasteOptionalCostIncludesManaReachableByTappingRunes()
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(1, 2), ["P2"] = RunePool.Empty } };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.OptionalCosts!, c => c.Id == HasteOptionalCostNames.HasteReady);
    }

    [Fact]
    public async Task AmbushResponseCandidateCanUseReadyRunesWithAnEmptyManaPool()
    {
        var state = await AmbushPermissionTests.Response(AmbushPermissionTests.Position("UNL-021/219"));
        var runes = Position();
        state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 10) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Concat(runes.PlayerZones["P1"].Base).ToArray() } },
            CardObjects = state.CardObjects.Concat(runes.CardObjects.Where(p => p.Key.StartsWith("TAP-PAY-R", StringComparison.Ordinal)))
                .ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = state.ObjectLocations.Concat(runes.ObjectLocations.Where(p => p.Key.StartsWith("TAP-PAY-R", StringComparison.Ordinal)))
                .ToDictionary(p => p.Key, p => p.Value) };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.Sources!, source => source.Id == "AMBUSH");
        var command = new PlayCardCommand("AMBUSH", "UNL-021/219", [], OptionalCosts: Taps.Take(3).ToArray(), Destination: "BATTLEFIELD:F");
        var result = await new CoreRuleEngine().ResolveAsync(state, new("ambush-tap", "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(3, result.Events.Count(e => e.Kind == "RUNE_TAPPED"));
    }

    private static readonly string[] Taps = ["TAP_RUNE:TAP-PAY-R1", "TAP_RUNE:TAP-PAY-R2", "TAP_RUNE:TAP-PAY-R3", "TAP_RUNE:TAP-PAY-R4"];
    private static MatchState Position()
    {
        var state = PlayCostPreviewTests.Position(new(0, 1)) with { Tick = 0 };
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        locations["CARD"] = new("P1", "HAND"); locations["P2-CARD"] = new("P2", "HAND");
        foreach (var i in Enumerable.Range(1, 4))
        {
            var id = $"TAP-PAY-R{i}";
            cards[id] = new(id, cardNo: "OGN·228/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:purple"]);
            locations[id] = new("P1", "BASE");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["TAP-PAY-R1", "TAP-PAY-R2", "TAP-PAY-R3", "TAP-PAY-R4"] } } };
    }
}
