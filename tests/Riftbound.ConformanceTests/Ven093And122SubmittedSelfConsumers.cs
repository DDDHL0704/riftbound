using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Arena = Riftbound.ConformanceTests.Ven093And122PhysicalSelfConsumers;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven093And122SubmittedSelfConsumers
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task LegalPurpleDeckPaysFourThenTwoAndActuallyRoamsWithFiveMightAndTheSameGeneration(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "vex-spells", "UNL-R05",
            ["VEN·093", "VEN·093", "VEN·093"], battlefields: ["OGN·278/298", "OGN·276/298", "OGN·275/298"]);
        async Task Act(string player, GameCommand command) { await game.Act(player, command); await game.ColdCheckpoint(); }
        try
        {
            await game.WaitFor(actor, 6, "VEN·093"); await game.ColdCheckpoint();
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var unit = game.Hand(actor, "VEN·093").First(); var runes = game.Runes(actor);
            foreach (var id in runes.Take(4)) await Act(actor, new TapRuneCommand(id));
            var prefix = game.Events().Length; await Act(actor, new PlayCardCommand(unit, "VEN·093", []));
            Arena.Cost(game.Events().Skip(prefix), 4, 0); Assert.Equal(4, game.Might(actor, unit));
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            foreach (var id in runes.Skip(4).Take(2)) await Act(actor, new TapRuneCommand(id));
            prefix = game.Events().Length; await Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Arena.Cost(game.Events().Skip(prefix), 2, 0); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(4, game.Might(actor, unit)); Assert.True(game.State.CardObjects[unit].IsExhausted);
            while (game.State.StackItems.Count > 0) await Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.State.CardObjects[unit].Power);
            Assert.Equal(5, game.Might(actor, unit)); Assert.Contains(CardCombatKeywordNames.Roam, game.State.CardObjects[unit].Tags);
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            await Act(actor, new EndTurnCommand()); await Act(Arena.Other(actor), new EndTurnCommand());
            var field = Field(game.State, actor); var other = Field(game.State, Arena.Other(actor));
            Assert.False(game.State.CardObjects[unit].IsExhausted);
            await Act(actor, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field, []));
            await Common.CompleteMovementWindow(() => game.State, Act);
            await Act(actor, new EndTurnCommand()); await Act(Arena.Other(actor), new EndTurnCommand());
            Assert.False(game.State.CardObjects[unit].IsExhausted);
            await Act(actor, new MoveUnitCommand(unit, "BATTLEFIELD:" + field, "BATTLEFIELD:" + other, ["ROAM"]));
            Assert.Equal(other, game.State.ObjectLocations[unit].BattlefieldObjectId);
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(4, game.State.CardObjects[unit].Power); Assert.Equal(5, game.Might(actor, unit));
            await Common.CompleteMovementWindow(() => game.State, Act);
            Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven093-submitted-paid-self-roam-" + actor); }
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P2", false)]
    [InlineData("P1", true)] [InlineData("P2", true)]
    public async Task IndependentlyLegalYellowAndBlueDecksPayNativeEmpowerAndTheActualSpellOrAbilityWard(string actor, bool ability)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R06",
            ["VEN·122", "VEN·122", "VEN·122"], opponent: new("vex-spells", "UNL-R03",
                ["UNL-066/219", "UNL-066/219", "UNL-066/219", OrbOfRegretTests.Card, OrbOfRegretTests.Card, OrbOfRegretTests.Card]));
        async Task Act(string player, GameCommand command) { await game.Act(player, command); await game.ColdCheckpoint(); }
        try
        {
            var enemy = Arena.Other(actor); await game.WaitFor(actor, 5, "VEN·122"); await game.ColdCheckpoint();
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var unit = game.Hand(actor, "VEN·122").First(); var runes = game.Runes(actor);
            foreach (var id in runes.Take(3)) await Act(actor, new TapRuneCommand(id));
            var prefix = game.Events().Length; await Act(actor, new PlayCardCommand(unit, "VEN·122", []));
            Arena.Cost(game.Events().Skip(prefix), 3, 0); Assert.Equal(3, game.Might(actor, unit));
            foreach (var id in runes.Skip(3).Take(2)) await Act(actor, new TapRuneCommand(id));
            prefix = game.Events().Length; await Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Arena.Cost(game.Events().Skip(prefix), 2, 0); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            while (game.State.StackItems.Count > 0) await Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(3, game.State.CardObjects[unit].Power);
            Assert.Equal(4, game.Might(actor, unit)); Assert.Contains("法盾2", game.State.CardObjects[unit].RuleDerivedTags!);
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.WaitFor(enemy, 7, ability ? OrbOfRegretTests.Card : "UNL-066/219"); await game.ColdCheckpoint();
            Assert.Equal(4, game.Might(actor, unit));
            Assert.Equal(0, game.State.RunePools[enemy].Mana); Assert.Equal(0, game.State.RunePools[enemy].TotalPower);
            var resources = game.Runes(enemy); var source = game.Hand(enemy, ability ? OrbOfRegretTests.Card : "UNL-066/219").First();
            foreach (var id in resources.Take(ability ? 1 : 7)) await Act(enemy, new TapRuneCommand(id));
            if (ability)
            {
                prefix = game.Events().Length; await Act(enemy, new PlayCardCommand(source, OrbOfRegretTests.Card, []));
                Arena.Cost(game.Events().Skip(prefix), 1, 0); Assert.False(game.State.CardObjects[source].IsExhausted);
            }
            await Act(enemy, new RecycleRuneCommand(resources[0])); await Act(enemy, new RecycleRuneCommand(resources[1]));
            Assert.Equal(2, game.State.RunePools[enemy].TotalPower); prefix = game.Events().Length;
            if (ability) await Act(enemy, new ActivateAbilityCommand(source, OrbOfRegretTests.Ability, [unit]));
            else await Act(enemy, new PlayCardCommand(source, "UNL-066/219", [unit]));
            Arena.Cost(game.Events().Skip(prefix), ability ? 0 : 7, 2);
            var cost = Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "COST_PAID");
            Assert.Equal(2, cost.Event.Payload["spellshieldTaxPower"]);
            Assert.Equal(0, game.State.RunePools[enemy].Mana); Assert.Equal(0, game.State.RunePools[enemy].TotalPower);
            if (ability) Assert.True(game.State.CardObjects[source].IsExhausted);
            Assert.Equal(3, game.State.CardObjects[unit].Power); Assert.Equal(4, game.Might(actor, unit));
            while (game.State.StackItems.Count > 0) await Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(ability ? 2 : -7, game.State.CardObjects[unit].Power);
            Assert.Equal(ability ? 3 : 0, game.Might(actor, unit));
            Assert.Equal(ability ? -1 : -10, Assert.Single(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers).PowerDelta);
            Assert.Equal("BASE", game.State.ObjectLocations[unit].Zone); Assert.Equal(0, game.State.CardObjects[unit].Damage);
            await Act(enemy, new EndTurnCommand());
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(3, game.State.CardObjects[unit].Power);
            Assert.Equal(4, game.Might(actor, unit)); Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven122-submitted-paid-ward-" + actor + "-" + ability); }
    }

    private static string Field(MatchState state, string player) => Assert.Single(state.PlayerZones[player].Battlefields,
        id => state.CardObjects[id].Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag));
}
