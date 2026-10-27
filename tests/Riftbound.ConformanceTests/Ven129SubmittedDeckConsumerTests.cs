using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven129SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SubmittedYellowDecksActuallyPayBothSidesAndMoveOneCompanionIntoARealDefensiveBattle(string actor)
    {
        const string no = "VEN·129";
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R06", [no, no, no, "VEN·124", "VEN·124", "VEN·124"]);
        try
        {
            await game.WaitFor(actor, 8, no, "VEN·124"); var runes = game.Runes(actor); var other = actor == "P1" ? "P2" : "P1";
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            foreach (var id in runes.Take(7)) await game.Act(actor, new TapRuneCommand(id));
            await game.Act(actor, new RecycleRuneCommand(runes[0])); Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Yellow]);
            var unit = game.Hand(actor, no).First(); var friend = game.Hand(actor, "VEN·124").First();
            await game.Act(actor, new PlayCardCommand(unit, no, [])); Assert.Equal(3, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            await game.Act(actor, new PlayCardCommand(friend, "VEN·124", [])); Assert.Equal(0, game.State.RunePools[actor].Mana);
            Assert.Equal(6, game.Might(actor, unit)); Assert.Equal(3, game.Might(actor, friend)); Assert.True(game.State.CardObjects[unit].IsExhausted); Assert.True(game.State.CardObjects[friend].IsExhausted);
            var generation = game.State.CardObjects[unit].ObjectGeneration; Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
            await game.WaitFor(other, 3, "VEN·124"); var enemy = game.Hand(other, "VEN·124").First();
            Assert.Equal(0, game.State.RunePools[other].Mana);
            foreach (var id in game.Runes(other).Where(id => !game.State.CardObjects[id].IsExhausted).Take(3)) await game.Act(other, new TapRuneCommand(id));
            await game.Act(other, new PlayCardCommand(enemy, "VEN·124", [])); Assert.Equal(0, game.State.RunePools[other].Mana); Assert.True(game.State.CardObjects[enemy].IsExhausted);
            await game.Act(other, new EndTurnCommand()); Assert.Equal(actor, game.State.TurnPlayerId); Assert.False(game.State.CardObjects[unit].IsExhausted); Assert.False(game.State.CardObjects[friend].IsExhausted);
            var field = game.State.PlayerZones[actor].Battlefields.First(id => game.State.ObjectLocations[id].BattlefieldObjectId == id);
            await game.Act(actor, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field, [], SourceObjectIds: [unit, friend]));
            Assert.Equal(1, Common.OtherFriendlyUnits(game.State, actor, unit, field)); Assert.Equal(6, game.Might(actor, unit)); await game.ColdCheckpoint();
            await game.Act(actor, new EndTurnCommand()); Assert.Equal(other, game.State.TurnPlayerId); Assert.False(game.State.CardObjects[enemy].IsExhausted);
            await game.Act(other, new MoveUnitCommand(enemy, "BASE", "BATTLEFIELD:" + field, []));
            Assert.True(game.State.CardObjects[unit].IsDefending); Assert.False(game.State.CardObjects[unit].IsAttacking);
            Assert.Equal(1, Common.OtherFriendlyUnits(game.State, actor, unit, field)); Assert.Equal(6, game.Might(actor, unit)); await game.ColdCheckpoint();
            var prefix = game.Events().Length;
            await Common.FinishCombat(() => game.State, async (player, command) => { await game.Act(player, command); await game.ColdCheckpoint(); }, game.Events, unit, 6);
            var damage = game.Events().Skip(prefix).Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), unit) && e.Event.Payload.ContainsKey("combatRole")).ToArray();
            Assert.Equal(6, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.All(damage, e => { Assert.Equal(6, e.Event.Payload["basePower"]); Assert.Equal(6, e.Event.Payload["combatPower"]); Assert.Equal(0, e.Event.Payload["keywordBonus"]); });
            Assert.Equal(6, game.Might(actor, unit)); Assert.Equal(6, game.State.CardObjects[unit].Power); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.False(game.State.CardObjects[unit].IsAttacking); Assert.False(game.State.CardObjects[unit].IsDefending); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven129-submitted-real-one-ally-" + actor); }
    }
}
