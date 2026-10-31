using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZaunBurnoutObjectIdentityTests
{
    [Theory]
    [InlineData("P1", 1)]
    [InlineData("P1", 2)]
    [InlineData("P2", 1)]
    [InlineData("P2", 2)]
    public async Task DiscardBurnoutAndRedrawCreateNewObjectsEvenWhenTheCardEndsBackInHand(string player, int handCount)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·298/298");
        var removed = initial.PlayerZones[player].MainDeck
            .Concat(initial.PlayerZones[player].Hand.Skip(handCount)).ToHashSet(StringComparer.Ordinal);
        initial = initial with {
            CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { [player] = initial.PlayerZones[player] with {
                MainDeck = [], Graveyard = [], Hand = initial.PlayerZones[player].Hand.Take(handCount).ToArray() } } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        var opponent = ThreeBattlefieldTimingProbe.Other(player);
        await game.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act(player, new PassFocusCommand());
        await game.Act(opponent, new PassFocusCommand());
        var resolved = await game.Top();
        if (handCount == 2) {
            var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
            resolved = await game.Act(player, new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H1"]));
        }

        Assert.Contains("H1", game.State.PlayerZones[player].Hand);
        Assert.Empty(game.State.PlayerZones[player].MainDeck);
        Assert.Empty(game.State.PlayerZones[player].Graveyard);
        Assert.Equal(initial.CardObjects["H1"].ObjectGeneration + 3, game.State.CardObjects["H1"].ObjectGeneration);
        Assert.Equal(initial.CardObjects["UNIT"].ObjectGeneration, game.State.CardObjects["UNIT"].ObjectGeneration);
        if (handCount == 2) Assert.Equal(initial.CardObjects["H2"].ObjectGeneration, game.State.CardObjects["H2"].ObjectGeneration);
        Assert.Single(resolved.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(initial.PlayerScores[opponent] + 1, game.State.PlayerScores[opponent]);
        await RideTheWindCurrentMovementTests.Replay(initial, game);

        var forged = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["H1"] = game.State.CardObjects["H1"] with { ObjectGeneration = initial.CardObjects["H1"].ObjectGeneration } } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch);
    }
}
