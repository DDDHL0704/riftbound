using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven128EmpoweredLastBreathConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningNaturallyPaysTheUnitAndItsTypedEmpowerAndColdRecoversThePermanentStatus(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R06", [No, No, No]);
        try
        {
            await game.WaitFor(actor, 4, No); await game.Tap(actor);
            var unit = game.Hand(actor, No).First(); var mana = game.State.RunePools[actor].Mana;
            Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            await game.Act(actor, new PlayCardCommand(unit, No, []));
            Assert.Equal(mana - 2, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Contains(unit, game.State.PlayerZones[actor].Base); Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(2, game.Might(actor, unit));
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.Act(actor, new RecycleRuneCommand(game.Runes(actor).First()));
            Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Yellow]);
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            var activation = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(1, activation.PaidMana); Assert.Equal(generation, activation.Source.Generation);
            Assert.Equal(mana - 3, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(2, game.Might(actor, unit));
            await game.ColdCheckpoint(); await game.Top();
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(2, game.Might(actor, unit));
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnEffects);
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(2, game.Might(actor, unit)); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven128-native-opening-" + actor); }
    }
}
