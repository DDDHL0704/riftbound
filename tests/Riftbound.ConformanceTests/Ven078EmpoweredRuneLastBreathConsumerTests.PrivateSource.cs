using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven078EmpoweredRuneLastBreathConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task AnEarnedTwoRuneSkillSurvivesActualBurnoutDrawingItsFormerSourceIntoAPrivateHand(string owner)
    {
        var initial = Hand(Position(owner, 8), "DRAW", "OGN·048/298", owner); var opponent = Other(owner);
        var moved = initial.PlayerZones[owner].MainDeck.Concat(initial.PlayerZones[owner].Graveyard).ToArray();
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            [owner] = initial.PlayerZones[owner] with { MainDeck = [], Graveyard = [], Hand = [..initial.PlayerZones[owner].Hand, ..moved] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState(owner, "HAND") : p.Value) };
        var game = new Driver(initial);
        try
        {
            for (var i = 1; i <= 6; i++) await game.Act(owner, new TapRuneCommand(owner + "-RUNE-" + i));
            await game.Act(owner, new PlayCardCommand("UNIT", No, [])); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await Empower(game, owner); await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(game.State, owner));
            await game.Act(owner, new EndTurnCommand()); await Kill(game, opponent);
            Assert.Equal(["UNIT"], game.State.PlayerZones[owner].Graveyard); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var graveGeneration = game.State.CardObjects["UNIT"].ObjectGeneration; Assert.Equal(generation + 1, graveGeneration);
            Assert.Equal(TriggerKinds.UnitLastBreathCallRuneOne, Assert.Single(game.State.StackItems).EffectKind); Cold(initial, game);
            if (game.State.PriorityPlayerId != owner) await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act(owner, new TapRuneCommand(owner + "-RUNE-7")); await game.Act(owner, new TapRuneCommand(owner + "-RUNE-8"));
            Assert.Equal(2, game.State.RunePools[owner].Mana);
            await game.Act(owner, new PlayCardCommand("DRAW", "OGN·048/298", [])); Assert.Equal(0, game.State.RunePools[owner].Mana);
            // Meditation is Reaction. The ordinary Top helper declines its
            // optional exhaust; real burnout then draws the sole grave card.
            await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones[owner].Hand);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > graveGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(1, game.State.PlayerScores[opponent]); Assert.Single(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
            Assert.Single(game.State.StackItems); Cold(initial, game);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)[opponent], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var skill = Assert.Single(JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray());
                Assert.Equal("HIDDEN", skill.GetProperty("sourceObjectId").GetString());
            }
            var before = game.State.PlayerZones[owner].RuneDeck.ToArray(); Assert.True(before.Length >= 2);
            await game.Top(); Assert.Equal(before.Skip(2), game.State.PlayerZones[owner].RuneDeck); CalledRunes(game.State, owner, before.Take(2));
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Cold(initial, game); await game.Replay();
        }
        finally { await Export("earned-private-hand-" + owner, initial, game); }
    }
}
