using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenConditionalPlayCostTests
{
    [Theory]
    [InlineData("VEN·064", 0, 10)]
    [InlineData("VEN·064", 1, 9)]
    [InlineData("VEN·064", 3, 7)]
    [InlineData("VEN·064", 12, 0)]
    [InlineData("VEN·096", 0, 5)]
    [InlineData("VEN·096", 1, 3)]
    [InlineData("VEN·096", 2, 1)]
    [InlineData("VEN·096", 3, 0)]
    public async Task CountedEquipmentAndSameNameGravesQuoteAndPayTheCurrentCost(string no, int count, int mana)
    {
        var state = Position(no);
        for (var i = 0; i < count; i++)
            state = Place(state, "COUNTED-" + i, no == "VEN·064" ? "SFD·022/221" : no,
                "P1", no == "VEN·064" ? "BASE" : "GRAVEYARD");
        // Opponent objects, unrelated grave cards, and hidden equipment do not contribute.
        state = Place(state, "OPPONENT", no == "VEN·064" ? "SFD·022/221" : no, "P2", no == "VEN·064" ? "BASE" : "GRAVEYARD");
        state = Place(state, "SECRET-GEAR", "SFD·022/221", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECRET-GEAR"] = state.CardObjects["SECRET-GEAR"] with { IsFaceDown = true } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana, 0) } };
        var command = new PlayCardCommand("UNIT", no, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        if (mana > 0) await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana - 1, 0) } }, command);
        var driver = new Driver(state);
        await driver.Act("P1", command);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.True(driver.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(no == "VEN·064" ? 8 : 5, driver.State.CardObjects["UNIT"].Power);
        if (no == "VEN·064") Assert.Contains(CardObjectTags.Spellshield, driver.State.CardObjects["UNIT"].Tags);
        await driver.Replay();
    }

    [Theory]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    [InlineData(3, true)] [InlineData(2, false)]
    public async Task ControlledBattlefieldMustHaveExactlyTwoUnitsBeforeEntry(int units, bool controlled)
    {
        var state = Position("VEN·119");
        for (var i = 0; i < units; i++) state = Place(state, "FIELD-" + i, "OGN·219/298", controlled ? "P1" : "P2", "BATTLEFIELD", "BF");
        if (!controlled) state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        var reduced = units == 2 && controlled;
        var mana = reduced ? 3 : 5;
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = reduced ? 0 : 1 }) } };
        var command = new PlayCardCommand("UNIT", "VEN·119", [], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(reduced ? 0 : 1, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        if (!reduced) await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } }, command);
        var driver = new Driver(state);
        await driver.Act("P1", command);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(5, driver.State.CardObjects["UNIT"].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD")]
    public async Task StargazerCanBePlayedNormallyAndRejectsInsufficientMana(string destination)
    {
        var state = Position("VEN·098");
        var command = new PlayCardCommand("UNIT", "VEN·098", [], Destination: destination == "BASE" ? "BASE" : "BATTLEFIELD:BF");
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(4, 0) } }, command);
        var driver = new Driver(state);
        await driver.Act("P1", command);
        Assert.Equal(15, driver.State.RunePools["P1"].Mana);
        Assert.Equal(4, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(destination, driver.State.ObjectLocations["UNIT"].Zone);
        await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·081", true, 1, 2)]
    [InlineData("VEN·081", true, 2, 1)]
    [InlineData("VEN·081", false, 1, 4)]
    [InlineData("VEN·049", true, 1, 1)]
    [InlineData("VEN·049", true, 2, 1)]
    [InlineData("VEN·049", false, 1, 2)]
    public async Task StargazersReduceOnlyGraveyardFlowCostsAndKeepTheirOwnOneManaFloor(
        string spell, bool flow, int count, int mana)
    {
        var state = Position("VEN·098");
        for (var i = 0; i < count; i++) state = Place(state, "GAZER-" + i, "VEN·098", "P1", i % 2 == 0 ? "BASE" : "BATTLEFIELD", i % 2 == 0 ? null : "BF");
        state = Place(state, "SPELL", spell, "P1", flow ? "GRAVEYARD" : "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana, 0) } };
        var command = new PlayCardCommand("SPELL", spell, spell == "VEN·081" ? ["ALLY"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana - 1, 0) } }, command);
        var driver = new Driver(state);
        await driver.Act("P1", command);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        await driver.Top();
        Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        if (spell == "VEN·081") Assert.Equal(state.CardObjects["ALLY"].Power + 6, driver.State.CardObjects["ALLY"].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData("opponent")] [InlineData("hand")] [InlineData("graveyard")] [InlineData("hidden")]
    public void InactiveOrOpponentStargazersDoNotReduceFlow(string branch)
    {
        var state = Position("VEN·098");
        state = Place(state, "GAZER", "VEN·098", branch == "opponent" ? "P2" : "P1",
            branch == "hand" ? "HAND" : branch == "graveyard" ? "GRAVEYARD" : "BASE");
        state = Place(state, "SPELL", "VEN·081", "P1", "GRAVEYARD");
        if (branch == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GAZER"] = state.CardObjects["GAZER"] with { IsFaceDown = true } } };
        var command = new PlayCardCommand("SPELL", "VEN·081", ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return Place(state, "GEAR", "SFD·022/221", "P1", "BANISHED");
    }

    internal static MatchState Place(MatchState state, string id, string no, string player, string zone, string? field = null)
    {
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            Base = p.Value.Base.Where(x => x != id).ToArray(), Hand = p.Value.Hand.Where(x => x != id).ToArray(),
            Battlefields = p.Value.Battlefields.Where(x => x != id).ToArray(), Graveyard = p.Value.Graveyard.Where(x => x != id).ToArray(),
            Banished = p.Value.Banished.Where(x => x != id).ToArray() });
        var own = zones[player];
        zones[player] = zone switch {
            "BASE" => own with { Base = [..own.Base, id] }, "HAND" => own with { Hand = [..own.Hand, id] },
            "BATTLEFIELD" => own with { Battlefields = [..own.Battlefields, id] },
            "GRAVEYARD" => own with { Graveyard = [..own.Graveyard, id] },
            "BANISHED" => own with { Banished = [..own.Banished, id] }, _ => throw new ArgumentOutOfRangeException(nameof(zone)) };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, zone, field) } };
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-conditional-cost", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
