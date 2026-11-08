using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class IvernTraitScoreTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task TheSharedPointRestrictionIsRecheckedAfterAnActualReturnResponse(bool returnTianna)
    {
        var state = RadGearTestPosition.Put(Position(), "OTHER", "OGN·296/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "TIANNA", "SFD·060/221", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P2", "HAND");
        state = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 0 } };
        var game = new Driver(state);
        await Conquer(game);
        Assert.Equal(6, game.State.PlayerScores["P1"]);
        Assert.NotNull(ScoreSkill(game));
        var before = game.Events().Count(e => e.Event.Kind == "SCORE_GAIN_PREVENTED");
        if (returnTianna)
        {
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["TIANNA"]));
            await game.Top();
            Assert.Contains("TIANNA", game.State.PlayerZones["P2"].Hand);
        }
        await game.Top();
        Assert.Equal(returnTianna ? 7 : 6, game.State.PlayerScores["P1"]);
        Assert.Null(game.State.WinnerPlayerId);
        Assert.Equal(before + (returnTianna ? 0 : 1),
            game.Events().Count(e => e.Event.Kind == "SCORE_GAIN_PREVENTED"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheIndependentSkillCanWinAfterTheLastConquestPointWasReplacedByADraw()
    {
        var state = Position();
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OTHER"] = new("OTHER", cardNo: "OGN·294/298", ownerId: "P1",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["OTHER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER") },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 } };
        var game = new Driver(state);
        await Conquer(game);
        Assert.Equal(7, game.State.PlayerScores["P1"]);
        Assert.Contains("DRAW1", game.State.PlayerZones["P1"].Hand);
        Assert.NotNull(ScoreSkill(game));
        await game.Top();
        Assert.Equal(8, game.State.PlayerScores["P1"]);
        Assert.Equal(MatchStatuses.Finished, game.State.Status);
        Assert.Equal("P1", game.State.WinnerPlayerId);
        await game.ColdCheckpoint();
    }
}
