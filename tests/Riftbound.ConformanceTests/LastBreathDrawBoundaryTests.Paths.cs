using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task ActualResourceDestructionCostKeepsItsPowerAndQueuesEachEarnedDraw(int copies)
    {
        var initial = Add(Position(), "MALZ", P4ActivatedAbilityCatalog.MalzaharCardNo, "P1", "BASE");
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new PlayCardCommand("UNIT", Unit, []));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < copies; i++) {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
            }
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower); var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["UNIT"]));
            Assert.True(game.State.CardObjects["MALZ"].IsExhausted); Assert.Equal(mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.Empty(game.State.PlayerZones["P1"].Hand);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(1 + copies, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i <= copies; i++) { await game.Top(); Assert.Equal(i + 1, game.State.PlayerZones["P1"].Hand.Count); }
            Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Save("actual-resource-cost-draw-copies-" + copies, initial, game); }
    }

    [Fact]
    public async Task ActualCombatLethalDamageEarnsTheDrawBeforeItsOwnResponseWindow()
    {
        var initial = AtDrawField(AtDrawField(Position(), "UNIT", "P1"), "ENEMY", "P2")
            with { UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF")] };
        var game = new Driver(initial);
        try
        {
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new DeclareBattleCommand("BF", ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
            for (var step = 0; !game.State.PlayerZones["P1"].Graveyard.Contains("UNIT"); step++)
            {
                Assert.InRange(step, 0, 12);
                var damage = ResolutionResult.BuildPrompts(game.State).Values.FirstOrDefault(p => p.Actionable && p.Actions.Contains(CommandTypes.AssignCombatDamage));
                if (damage is not null)
                    await game.Act(damage.PlayerId, new AssignCombatDamageCommand(game.State.BattleState.BattleId!, "BF",
                        [damage.PlayerId == "P1" ? new CombatDamageAssignmentDto("UNIT", "ENEMY", 1) : new CombatDamageAssignmentDto("ENEMY", "UNIT", 4)]));
                else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
                else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
                else Assert.Fail("Battle damage must expose its next ordinary continuation.");
            }
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Empty(game.State.PlayerZones["P1"].Hand);
            Assert.Equal(1, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(generation, Assert.Single(game.State.StackItems).LastBreathDraw!.Source.Generation);
            await game.Top(); Assert.Equal(["P1-DRAW-1"], game.State.PlayerZones["P1"].Hand); await game.Replay();
        }
        finally { await Save("actual-combat-lethal-draw", initial, game); }
    }

    private static MatchState AtDrawField(MatchState state, string id, string player)
        => state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with {
                Hand = state.PlayerZones[player].Hand.Where(x => x != id).ToArray(), Base = state.PlayerZones[player].Base.Where(x => x != id).ToArray(),
                Battlefields = [..state.PlayerZones[player].Battlefields, id] } } };
}
