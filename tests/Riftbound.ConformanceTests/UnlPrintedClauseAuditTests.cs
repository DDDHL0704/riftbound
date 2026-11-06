using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class UnlPrintedClauseAuditTests
{
    [Fact]
    public async Task AnnouncerHoldGrantsBoonOnlyAtTheHeldField()
    {
        var initial = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·294/298"),
            "ANNOUNCER", "UNL-043/219", "F", "P1");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "FRIEND", "SFD·125/221", "F", "P1");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ELSEWHERE", "SFD·125/221", "OTHER", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("HOLD_BOON_ALL", Assert.Single(game.State.StackItems).EffectKind);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["FRIEND"].Tags);
        await game.Top();
        foreach (var id in new[] { "ANNOUNCER", "FRIEND" })
        {
            Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[id].Tags);
            Assert.Equal(initial.CardObjects[id].Power + 1, game.State.CardObjects[id].Power);
        }
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ELSEWHERE"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task AZeroMightCrabCanConquerAndHoldTheSameBattlefield()
    {
        var initial = ConquestLifecycleRegressionTests.State("OGN·294/298");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", "UNL-053/219", "P1") with { IsExhausted = false },
                ["P2-D1"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-D1", "SFD·125/221", "P2"),
                ["P2-D2"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-D2", "SFD·125/221", "P2") },
        };
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["P2-D1", "P2-D2"] } } };
        var game = new Driver(initial);
        Assert.Equal(0, game.State.CardObjects["UNIT"].Power);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Equal("P1", game.State.BattlefieldStates["BF"].ControllerId);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(2, game.State.PlayerScores["P1"]);
        Assert.Equal(0, game.State.CardObjects["UNIT"].Power);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Battlefields);
        await game.Replay();
    }
}
