using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven128EmpoweredLastBreathConsumerTests
{
    [Theory]
    [InlineData("P1", false)] [InlineData("P2", false)]
    [InlineData("P1", true)] [InlineData("P2", true)]
    public async Task ActualReturnIsNotDestructionAndAReplayedIncarnationDoesNotRetainEmpowerment(string owner, bool empower)
    {
        var initial = Hand(Position(owner), "RETURN", "OGN·104/298", owner); var game = new Driver(initial);
        try
        {
            await Tap(game, owner); await game.Act(owner, new PlayCardCommand("UNIT", No, []));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empower)
            {
                await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-1"));
                await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
                Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            }
            var mana = game.State.RunePools[owner].Mana;
            await game.Act(owner, new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
            Assert.Equal(mana - 1, game.State.RunePools[owner].Mana); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Hand); Assert.DoesNotContain("UNIT", game.State.PlayerZones[owner].Graveyard);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Empty(Minions(game.State)); JsonCold(initial, game);
            await game.Act(owner, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            await Tap(game, owner); await game.Act(owner, new PlayCardCommand("UNIT", No, []));
            Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(Minions(game.State));
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); JsonCold(initial, game); await game.Replay();
        }
        finally { await Export("return-new-incarnation-" + owner + "-" + empower, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task AnEarnedDependentSkillSurvivesItsSourceReturningFromTheGraveyardToAPrivateHand(string owner)
    {
        var initial = Hand(Position(owner), "REVIVE", "OGN·170/298", owner);
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        foreach (var i in new[] { 5, 6 })
        {
            var id = owner + "-RUNE-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", owner);
            locations[id] = new(owner, "BASE");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            [owner] = initial.PlayerZones[owner] with { Base = [..initial.PlayerZones[owner].Base, owner + "-RUNE-5", owner + "-RUNE-6"] } } };
        var game = new Driver(initial); var opponent = owner == "P1" ? "P2" : "P1";
        try
        {
            for (var i = 1; i <= 4; i++) await game.Act(owner, new TapRuneCommand(owner + "-RUNE-" + i));
            await game.Act(owner, new PlayCardCommand("UNIT", No, [])); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-1"));
            await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); await game.Act(owner, new EndTurnCommand()); await Tap(game, opponent);
            await game.Act(opponent, new RecycleRuneCommand(opponent + "-RUNE-1")); await game.Act(opponent, new RecycleRuneCommand(opponent + "-RUNE-2"));
            await game.Act(opponent, new PlayCardCommand("KILL", Destroy, ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Graveyard); Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(TriggerKinds.UnitLastBreathCreateMinions, Assert.Single(game.State.StackItems).EffectKind);
            if (game.State.PriorityPlayerId != owner) await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(0, game.State.RunePools[owner].Mana);
            await game.Act(owner, new TapRuneCommand(owner + "-RUNE-5")); await game.Act(owner, new TapRuneCommand(owner + "-RUNE-6"));
            Assert.Equal(2, game.State.RunePools[owner].Mana); await game.Act(owner, new PlayCardCommand("REVIVE", "OGN·170/298", ["UNIT"]));
            Assert.Equal(0, game.State.RunePools[owner].Mana); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Hand); Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Single(game.State.StackItems); JsonCold(initial, game);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)[opponent], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var skill = Assert.Single(JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray());
                Assert.Equal("HIDDEN", skill.GetProperty("sourceObjectId").GetString());
            }
            await game.Top(); Assert.Equal(2, Minions(game.State).Length); Assert.Empty(game.State.StackItems);
            Assert.All(Minions(game.State), m => { Assert.Equal(owner, m.ControllerId); Assert.Contains(m.ObjectId, game.State.PlayerZones[owner].Base); });
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            JsonCold(initial, game); await game.Replay();
        }
        finally { await Export("earned-private-hand-" + owner, initial, game); }
    }

    private static MatchState Hand(MatchState state, string id, string no, string owner) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
}
