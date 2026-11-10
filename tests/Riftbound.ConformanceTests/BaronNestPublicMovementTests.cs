using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BaronNestPublicMovementTests
{
    private static MatchState Position(string? controller)
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "MOVER", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "SECOND", "OGN·219/298", "P1", "BASE");
        state = BaronNashorEntryTests.Nest(state, "P2", controller);
        if (controller is not null)
            state = RadGearTestPosition.Put(state, "GUARD", "OGN·219/298", controller, "BATTLEFIELD", "NEST");
        return state;
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P2", false)] [InlineData(null, false)]
    [InlineData("P1", true)] [InlineData("P2", true)] [InlineData(null, true)]
    public async Task EitherASingleUnitOrAMixedOriginGroupMayEnterAnOpponentOwnedNestWithoutRoam(string? controller, bool group)
    {
        var game = new Driver(Position(controller)); var before = MatchStateHasher.Hash(game.State);
        var prompts = ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [];
        Assert.Contains(prompts, candidate => candidate.Action == CommandTypes.MoveUnit
            && candidate.Sources?.Any(source => source.Id == "MOVER") == true
            && candidate.Destinations?.Any(destination => destination.Id == "BATTLEFIELD:NEST") == true);
        Assert.Equal(before, MatchStateHasher.Hash(game.State)); var pool = game.State.RunePools["P1"];
        await game.Act("P1", new MoveUnitCommand("MOVER", group ? "" : "BATTLEFIELD:BF", "BATTLEFIELD:NEST", [],
            SourceObjectIds: group ? ["MOVER", "SECOND"] : null));
        foreach (var id in group ? new[] { "MOVER", "SECOND" } : ["MOVER"]) {
            Assert.Equal("NEST", game.State.ObjectLocations[id].BattlefieldObjectId); Assert.True(game.State.CardObjects[id].IsExhausted);
        }
        Assert.Equal(pool, game.State.RunePools["P1"]);
        if (controller == "P2") Assert.True(game.State.BattleState.IsActive || game.State.SpellDuelState.IsActive);
        await game.Replay();
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("opponent")] [InlineData("prohibited")]
    public async Task TheNestPermissionDoesNotBypassOrdinarySourceOrMovementRestrictions(string mode)
    {
        var state = Position("P2"); var source = state.CardObjects["MOVER"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["MOVER"] = source with { IsExhausted = mode == "exhausted", ControllerId = mode == "opponent" ? "P2" : "P1",
                UntilEndOfTurnEffects = mode == "prohibited" ? [MovementRestrictionRules.AllPlayers] : [] } } };
        await RadGearTestPosition.Reject(state, "P1", new MoveUnitCommand("MOVER", "", "BATTLEFIELD:NEST", [], SourceObjectIds: ["MOVER", "SECOND"]));
    }

    [Fact]
    public async Task ANestElsewhereCannotGrantMovementToAnOrdinaryBattlefield()
        => await RadGearTestPosition.Reject(Position("P2"), "P1", new MoveUnitCommand("MOVER", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", []));
}
