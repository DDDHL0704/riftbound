using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven128EmpoweredLastBreathConsumerTests
{
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task AnActualZhonyaReplacementPreventsNativeAndAttachedDependentLastBreathsWithoutCreatingANewUnit(int copies)
    {
        var initial = Position("P1"); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        var additions = new List<string>();
        foreach (var i in new[] { 1, 2 })
        {
            cards["P1-RUNE-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-RUNE-" + i, "UNL-R02", "P1");
            var id = "SONG-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "SFD·059/221", "P1");
            locations[id] = new("P1", "BASE"); additions.Add(id);
        }
        foreach (var i in new[] { 5, 6 })
        {
            var id = "P1-RUNE-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", "P1");
            locations[id] = new("P1", "BASE"); additions.Add(id);
        }
        cards["ZHONYA"] = OfficialGarenConquestBattlefieldCountTests.Printed("ZHONYA", "OGN·077/298", "P1");
        locations["ZHONYA"] = new("P1", "BASE"); additions.Add("ZHONYA");
        initial = initial with { RoomId = "VEN128-REPLACED-DESTRUCTION", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = [..initial.PlayerZones["P1"].Base, ..additions] } } };
        // Pre-existing unattached Songs and Zhonya; actual unit play, native
        // Empower, every assembly payment and the destruction attempt follow.
        var game = new Driver(initial);
        try
        {
            for (var i = 1; i <= 6; i++) await game.Act("P1", new TapRuneCommand("P1-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new RecycleRuneCommand("P1-RUNE-3"));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(3, game.State.RunePools["P1"].Mana);
            for (var i = 1; i <= copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i)); var mana = game.State.RunePools["P1"].Mana;
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"]));
                Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Top();
                Assert.Equal(No, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-2"));
            await game.Act("P2", new PlayCardCommand("KILL", Destroy, ["UNIT"]));
            Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower); await game.Top();
            Assert.Contains("ZHONYA", game.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("ZHONYA", game.State.PlayerZones["P1"].Base);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(0, game.State.CardObjects["UNIT"].Damage);
            for (var i = 1; i <= copies; i++) Assert.Equal("UNIT", game.State.CardObjects["SONG-" + i].AttachedToObjectId);
            Assert.Empty(Minions(game.State)); Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems); JsonCold(initial, game);
            await game.Act("P2", new EndTurnCommand()); Assert.Empty(Minions(game.State)); Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Export("replaced-death-copies-" + copies, initial, game); }
    }
}
