using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EquipmentEventIntegrationTests
{
    [Theory]
    [InlineData("SFD·090/221", "ASSEMBLE_BLUE")]
    [InlineData("SFD·172/221", "ASSEMBLE_YELLOW")]
    public async Task AnAssemblyAndItsLaterGrantedLastBreathKeepTheirDistinctCaptureFacts(string gear, string payment)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "PROGRESS", "SFD·075/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GEAR", gear, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "KILL", "OGN·229/298", "P1", "HAND");
        var game = new Driver(state);
        var might = game.State.CardObjects["PROGRESS"].Power;
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", [payment]));
        await game.Top();
        var assemblyObserver = Assert.Single(game.State.StackItems);
        Assert.Equal("PROGRESS", assemblyObserver.SourceObjectId);
        Assert.NotNull(assemblyObserver.UnitEvent!.EquipmentSkillTriggerHash);
        Assert.Null(assemblyObserver.UnitEvent.ArmamentDeath);
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        await game.Top();
        Assert.Equal(might + 1, game.State.CardObjects["PROGRESS"].Power);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["ALLY"]));
        await game.Top();
        var lastBreath = Assert.Single(game.State.StackItems);
        Assert.Equal("ALLY", lastBreath.SourceObjectId);
        Assert.NotNull(lastBreath.UnitEvent!.ArmamentDeath);
        Assert.Null(lastBreath.UnitEvent.EquipmentSkillTriggerHash);
        await game.Top();
        Assert.Equal(might + 1, game.State.CardObjects["PROGRESS"].Power);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task TheCaptainsAcceptedModeDoesNotReplaceTheLastBreathGrantedByItsEquipment()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "CAPTAIN", "SFD·091/221", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·172/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "KILL", "OGN·229/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CAPTAIN", "SFD·091/221", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SELF_BOON"]));
        await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["CAPTAIN"].Tags);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "CAPTAIN", ["ASSEMBLE_YELLOW"]));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["CAPTAIN"]));
        await game.Top();
        var captured = Assert.Single(game.State.StackItems).UnitEvent!;
        Assert.Equal("SFD·172/221", captured.CardNo);
        Assert.Null(captured.Mode);
        Assert.NotNull(captured.ArmamentDeath);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }
}
