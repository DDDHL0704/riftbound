using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven050SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1", 8, 4)] [InlineData("P2", 8, 4)]
    [InlineData("P1", 12, 0)] [InlineData("P2", 12, 0)]
    public async Task SubmittedBlueDecksActuallyPayFourOrZeroAndRejectCoherentFalseRuneBirths(string actor, int controlled, int mana)
    {
        const string no = "VEN·050";
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "lillia-lowcurve", "UNL-R03", [no, no, no]);
        try
        {
            await game.WaitFor(actor, controlled, no); var runes = game.Runes(actor); Assert.InRange(runes.Length, controlled, 12);
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var manaRunes = runes.Take(4 + mana).ToArray();
            await game.Act(actor, new TapRuneCommand(manaRunes[0], manaRunes));
            Assert.Equal(4 + mana, game.State.RunePools[actor].Mana); var unit = game.Hand(actor, no).First();
            await game.Act(actor, new PlayCardCommand(unit, no, [])); Assert.Equal(mana, game.State.RunePools[actor].Mana);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.State.CardObjects[unit].Power); Assert.Equal(4, game.Might(actor, unit));
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            foreach (var batch in runes.Skip(controlled).Chunk(12))
                await game.Act(actor, new RecycleRuneCommand(batch[0], batch));
            Assert.Equal(controlled, game.Runes(actor).Length); Assert.Equal(12 - controlled, game.State.PlayerZones[actor].RuneDeck.Count);
            Assert.Equal(mana, game.State.RunePools[actor].Mana); Assert.Equal(runes.Length - controlled, game.State.RunePools[actor].TotalPower);
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            var pending = Assert.Single(game.State.StackItems); var paid = pending.EmpowerActivation!;
            Assert.Equal(controlled, paid.ControlledRunes); Assert.Equal(mana, paid.PaidMana); Assert.Null(paid.DestructionCost);
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.Might(actor, unit));
            await game.ColdCheckpoint();
            var falseMana = controlled == 8 ? 3 : 0; var pool = game.State.RunePools[actor];
            var forged = game.State with {
                StackItems = [pending with { EmpowerActivation = paid with { ControlledRunes = controlled + 1, PaidMana = falseMana } }],
                RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { [actor] = pool with { Mana = pool.Mana + mana - falseMana } } };
            await game.RejectCoordinatedForgery("false-eight-nine-or-twelve-thirteen-rune-birth-" + actor + "-" + controlled, forged);
            await game.Act(actor, new RecycleRuneCommand(game.Runes(actor)[0]));
            Assert.Equal(controlled - 1, game.Runes(actor).Length); Assert.Equal(paid, Assert.Single(game.State.StackItems).EmpowerActivation);
            Assert.Equal(0, game.State.RunePools[actor].Mana); await game.ColdCheckpoint();
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.Might(actor, unit));
            Assert.Equal(4, game.State.CardObjects[unit].Power); Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects[unit].Tags);
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(4, game.Might(actor, unit)); Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven050-submitted-" + actor + "-" + controlled); }
    }
}
