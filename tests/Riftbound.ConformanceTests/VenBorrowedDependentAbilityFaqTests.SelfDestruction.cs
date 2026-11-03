using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("OGN·186/298", "normal", false)]
    [InlineData("RAD·012/167", "normal", false)]
    [InlineData("OGN·186/298", "return", false)]
    [InlineData("RAD·012/167", "return", false)]
    [InlineData("OGN·186/298", "hourglass", false)]
    [InlineData("RAD·012/167", "hourglass", false)]
    [InlineData("OGN·186/298", "normal", true)]
    [InlineData("RAD·012/167", "normal", true)]
    [InlineData("OGN·186/298", "return", true)]
    [InlineData("RAD·012/167", "return", true)]
    public async Task BorrowedSelfDestructionResolvesAgainstTheBorrowersCapturedGeneration(string donor, string response, bool token)
    {
        var initial = BorrowedConditionPosition(donor);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(12, 0, new Dictionary<string, int> { ["purple"] = 2, ["red"] = 2 }) } };
        if (response == "hourglass")
            initial = RadGearTestPosition.Put(initial, "HOURGLASS", "OGN·077/298", "P1", "BASE");
        if (token)
        {
            var host = initial.CardObjects["HOST"];
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = host with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                    CopyFaceTransitions = [new(0, host.ObjectGeneration, P6TokenFactoryCatalog.ImageTokenCardNo, host.CardNo!)] } } };
        }
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "摧毁此牌");
        var generation = initial.CardObjects["HOST"].ObjectGeneration;
        await RejectBorrowedResource(initial, "HOST", id, ["ALLY"]);
        await RejectBorrowedResource(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(12, 0, new Dictionary<string, int> { ["blue"] = 20 }) } }, "HOST", id, []);
        if (donor == "RAD·012/167")
            await RejectBorrowedResource(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = initial.RunePools["P1"] with { Mana = 1 } } }, "HOST", id, []);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(generation, Assert.Single(game.State.StackItems).BorrowedExhaust!.Borrower.Object.Generation);
        Assert.Equal(12 - (donor == "RAD·012/167" ? 2 : 0), game.State.RunePools["P1"].Mana);
        var color = donor == "RAD·012/167" ? "red" : "purple";
        Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[color]);
        if (response == "return")
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
        await game.Top();
        Assert.Contains("DONOR", game.State.PlayerZones["P1"].Base);
        Assert.Equal(initial.CardObjects["DONOR"], game.State.CardObjects["DONOR"]);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(game.State.PlayerZones["P1"].Banished);
        if (token) Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        else if (response == "hourglass")
        {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Contains("HOURGLASS", game.State.PlayerZones["P1"].Graveyard);
        }
        else
        {
            Assert.Contains("HOST", response == "return" ? game.State.PlayerZones["P1"].Hand : game.State.PlayerZones["P1"].Graveyard);
            Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
        }
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
