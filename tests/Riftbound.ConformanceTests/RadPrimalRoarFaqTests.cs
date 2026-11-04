using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadPrimalRoarFaqTests
{
    private const string Roar = "RAD·082/167";

    [Theory]
    [InlineData("return")]
    [InlineData("discard")]
    [InlineData("buff")]
    public async Task TheShownUnitsCapturedPowerSurvivesRealResponses(string response)
    {
        var initial = VenUnitEntryBasicTests.Position(Roar);
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "UNL-128/219", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "MOON", "UNL-125/219", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "BUFF", "SFD·034/221", "P1");
        initial = LastBreathDrawBoundaryTests.Add(initial, "TARGET", "OGN·150/298", "P2", "BASE");
        var power = initial.CardObjects["ALLY"].Power;
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("UNIT", Roar, ["TARGET"], OptionalCosts: ["SHOWCASE:ALLY"]));
        var skill = Assert.Single(game.State.StackItems);
        var receipt = Assert.IsType<ShowcaseReceipt>(skill.Showcase);
        Assert.False(receipt.FromHand);
        Assert.Equal(power, receipt.CapturedPower);
        await game.ColdCheckpoint();

        if (response == "buff")
        {
            await game.Act("P1", new PlayCardCommand("BUFF", "SFD·034/221", ["ALLY"]));
            await game.Top();
            Assert.True(game.State.CardObjects["ALLY"].Power > power);
        }
        else
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "UNL-128/219", ["ALLY", "ENEMY"]));
            await game.Top();
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
            Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
            Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > receipt.Object.Generation);
            await game.ColdCheckpoint();
            if (response == "discard")
            {
                await game.Act("P1", new PlayCardCommand("MOON", "UNL-125/219", ["ALLY"]));
                await game.Top();
                Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
                Assert.All(initial.PlayerZones["P1"].MainDeck.Take(2), id => Assert.Contains(id, game.State.PlayerZones["P1"].Hand));
                Assert.Equal(initial.PlayerZones["P1"].MainDeck.Skip(2), game.State.PlayerZones["P1"].MainDeck);
            }
        }

        Assert.Equal(receipt, Assert.Single(game.State.StackItems).Showcase);
        Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal(power, game.State.CardObjects["TARGET"].Damage);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
