using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven078EmpoweredRuneLastBreathConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SubmittedLegalDecksNaturallyPayTheFourManaUnitAndGenericEmpowerAndColdRecoverItsSelfMight(string actor)
    {
        // FromSeats already assigns P1/P2. Real deck submission, ready, mulligan,
        // draws and rune channeling follow; room creation and seat commands do not.
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R04", [No, No, No]);
        try
        {
            await game.WaitFor(actor, 6, No); await game.Tap(actor);
            var unit = game.Hand(actor, No).First(); var mana = game.State.RunePools[actor].Mana;
            Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            await game.Act(actor, new PlayCardCommand(unit, No, []));
            Assert.Equal(mana - 4, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Contains(unit, game.State.PlayerZones[actor].Base); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(4, game.Might(actor, unit)); var generation = game.State.CardObjects[unit].ObjectGeneration;
            foreach (var rune in game.Runes(actor).Take(2).ToArray()) await game.Act(actor, new RecycleRuneCommand(rune));
            Assert.Equal(2, game.State.RunePools[actor].PowerByTrait[RuneTrait.Orange]);
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            var activation = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(1, activation.PaidMana); Assert.Equal(generation, activation.Source.Generation);
            Assert.Equal(mana - 5, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.Might(actor, unit));
            await game.ColdCheckpoint(); await game.Top();
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(6, game.Might(actor, unit));
            Assert.Equal(4, game.State.CardObjects[unit].Power); Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers);
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(6, game.Might(actor, unit)); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven078-submitted-decks-native-" + actor); }
    }
}
