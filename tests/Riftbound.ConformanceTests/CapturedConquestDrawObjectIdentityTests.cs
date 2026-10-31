using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CapturedConquestDrawObjectIdentityTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ConquestDrawTracksBothBurnoutAndDrawingTheRecycledCard(string player)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·278/298");
        var zones = initial.PlayerZones[player];
        var drawnId = zones.MainDeck[0];
        var removed = zones.MainDeck.Skip(1).Concat(zones.Graveyard).ToHashSet(StringComparer.Ordinal);
        var cards = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        cards["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", "VEN·SP1", player);
        var locations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        locations[drawnId] = new(player, "GRAVEYARD");
        initial = initial with {
            CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = zones with { MainDeck = [], Graveyard = [drawnId] } } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act(player, new PassFocusCommand());
        await game.Act(ThreeBattlefieldTimingProbe.Other(player), new PassFocusCommand());
        Assert.Equal(TriggerKinds.UnitConquestDrawOne, Assert.Single(game.State.StackItems).ConquestSource!.Kind);
        Assert.Contains(drawnId, game.State.PlayerZones[player].Graveyard);

        var resolved = await game.Top();

        Assert.Contains(drawnId, game.State.PlayerZones[player].Hand);
        Assert.Empty(game.State.PlayerZones[player].Graveyard);
        Assert.Empty(game.State.PlayerZones[player].MainDeck);
        Assert.Equal(initial.CardObjects[drawnId].ObjectGeneration + 2, game.State.CardObjects[drawnId].ObjectGeneration);
        Assert.Equal(initial.CardObjects["UNIT"].ObjectGeneration, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Single(resolved.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Single(resolved.Events, e => e.Kind == "CARD_DRAWN");
        await RideTheWindCurrentMovementTests.Replay(initial, game);

        var forged = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            [drawnId] = game.State.CardObjects[drawnId] with { ObjectGeneration = initial.CardObjects[drawnId].ObjectGeneration + 1 } } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch);
    }
}
