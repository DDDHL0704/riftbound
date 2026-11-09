using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AkshanIndependentPaidPlayTests
{
    private const string Akshan = "SFD·109/221", Fee = "SPEND_POWER:2:ORANGE";

    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT", Akshan, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        return RadGearTestPosition.Put(state, "GEAR", "SFD·022/221", "P2", "BASE");
    }

    [Theory, InlineData("BASE"), InlineData("BATTLEFIELD:BF")]
    public async Task TheOrangeFeeIsPaidBeforeEntryAndTheIndependentTheftChoosesEquipmentAfterEntry(string destination)
    {
        var game = new Driver(Position());
        var resources = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee], Destination: destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(resources.Mana - 4, game.State.RunePools["P1"].Mana);
        Assert.Equal(resources.TotalPower - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal("UNIT", choice.SourceObjectId);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("UNIT", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(game.State.ObjectLocations["UNIT"].BattlefieldObjectId,
            game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task PayingTheAdditionalOrangeDoesNotRequireAnyEnemyEquipmentToExist()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "GEAR", "SFD·022/221", "P2", "GRAVEYARD"));
        var resources = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", Akshan, [], OptionalCosts: [Fee]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(resources.TotalPower - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
