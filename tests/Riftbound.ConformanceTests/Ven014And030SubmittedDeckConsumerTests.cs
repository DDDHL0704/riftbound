using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven014And030SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1", "VEN·014", "jhin-lowcurve", "UNL-R01", 5, 2, 1)]
    [InlineData("P2", "VEN·014", "jhin-lowcurve", "UNL-R01", 5, 2, 1)]
    [InlineData("P1", "VEN·030", "lillia-lowcurve", "UNL-R02", 6, 3, 0)]
    [InlineData("P2", "VEN·030", "lillia-lowcurve", "UNL-R02", 6, 3, 0)]
    public async Task SubmittedDecksPayNativeEmpowerAndOriginalHistoryRejectsRefunds(string actor, string no, string deck, string rune, int minimumRunes, int mana, int power)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, deck, rune, [no, no, no]);
        try
        {
            await game.WaitFor(actor, minimumRunes, no);
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            await game.Tap(actor); var available = game.State.RunePools[actor].Mana; var unit = game.Hand(actor, no).First();
            await game.Act(actor, new PlayCardCommand(unit, no, [])); Assert.Equal(available - mana, game.State.RunePools[actor].Mana);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(mana, game.Might(actor, unit));
            var generation = game.State.CardObjects[unit].ObjectGeneration; await game.ColdCheckpoint();
            if (power == 1)
            {
                await game.Act(actor, new RecycleRuneCommand(game.Runes(actor).First()));
                Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Red]);
            }
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(available - mana * 2, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Equal(mana, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(mana, game.Might(actor, unit)); await game.ColdCheckpoint();
            var pool = game.State.RunePools[actor];
            var refundedMana = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
                [actor] = pool with { Mana = pool.Mana + mana } } };
            await game.RejectCoordinatedForgery("refunded-native-empower-mana-" + no + "-" + actor, refundedMana);
            if (power == 1)
            {
                var refundedRed = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
                    [actor] = pool with { PowerByTrait = new Dictionary<string, int>(pool.PowerByTrait) { [RuneTrait.Red] = pool.PowerByTrait.GetValueOrDefault(RuneTrait.Red) + 1 } } } };
                await game.RejectCoordinatedForgery("refunded-required-native-red-power-" + actor, refundedRed);
            }
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(mana, game.Might(actor, unit));
            Assert.Contains(no == "VEN·014" ? "强攻3" : "坚守3", game.State.CardObjects[unit].Tags);
            Assert.Equal(no == "VEN·030", game.State.CardObjects[unit].Tags.Contains(CardObjectTags.Spellshield)); await game.ColdCheckpoint();
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(mana, game.State.CardObjects[unit].Power); Assert.Equal(mana, game.Might(actor, unit));
            Assert.False(game.State.CardObjects[unit].IsAttacking); Assert.False(game.State.CardObjects[unit].IsDefending);
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven014-030-submitted-native-" + no.Replace('·', '-') + "-" + actor); }
    }
}
