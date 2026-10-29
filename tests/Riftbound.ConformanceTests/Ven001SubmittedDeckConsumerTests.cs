using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven001SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1", 4, 2)] [InlineData("P2", 4, 2)]
    [InlineData("P1", 5, 5)] [InlineData("P2", 5, 5)]
    public async Task SubmittedRedDecksPayTheRealThresholdAndOriginalHistoryRejectsACoherentFalseRuneBirth(string actor, int controlled, int mana)
    {
        const string no = "VEN·001";
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "jhin-lowcurve", "UNL-R01", [no, no, no]);
        try
        {
            await game.WaitFor(actor, 6, no); Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var beforeRunes = game.Runes(actor).Length; await game.Tap(actor); var firstMana = game.State.RunePools[actor].Mana;
            var unit = game.Hand(actor, no).First(); await game.Act(actor, new PlayCardCommand(unit, no, []));
            Assert.Equal(firstMana - 6, game.State.RunePools[actor].Mana); Assert.Equal(6, game.State.CardObjects[unit].Power); Assert.Equal(6, game.Might(actor, unit));
            Assert.False(game.State.CardObjects[unit].IsEmpowered); var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.Equal(actor, game.State.TurnPlayerId); var runes = game.Runes(actor);
            Assert.Equal(Math.Min(12, beforeRunes + 2), runes.Length); Assert.Equal(0, game.State.RunePools[actor].Mana);
            await game.Tap(actor);
            foreach (var batch in runes.Skip(controlled).Chunk(12))
                await game.Act(actor, new RecycleRuneCommand(batch[0], batch));
            Assert.Equal(controlled, game.Runes(actor).Length); Assert.Equal(12 - controlled, game.State.PlayerZones[actor].RuneDeck.Count);
            Assert.Equal(runes.Length, game.State.RunePools[actor].Mana); Assert.Equal(runes.Length - controlled, game.State.RunePools[actor].TotalPower);
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            var pending = Assert.Single(game.State.StackItems); var paid = pending.EmpowerActivation!;
            Assert.Equal(controlled, paid.ControlledRunes); Assert.Equal(mana, paid.PaidMana); Assert.Null(paid.DestructionCost);
            Assert.Equal(runes.Length - mana, game.State.RunePools[actor].Mana); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(6, game.Might(actor, unit)); await game.ColdCheckpoint();
            var falseRunes = controlled == 4 ? 5 : 4; var falseMana = controlled == 4 ? 5 : 2; var pool = game.State.RunePools[actor];
            var forged = game.State with {
                StackItems = [pending with { EmpowerActivation = paid with { ControlledRunes = falseRunes, PaidMana = falseMana } }],
                RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { [actor] = pool with { Mana = pool.Mana + mana - falseMana } } };
            await game.RejectCoordinatedForgery("false-four-five-rune-birth-" + actor + "-" + controlled, forged);
            await game.Act(actor, new RecycleRuneCommand(game.Runes(actor)[0]));
            Assert.Equal(controlled - 1, game.Runes(actor).Length); Assert.Equal(paid, Assert.Single(game.State.StackItems).EmpowerActivation);
            Assert.Equal(runes.Length - mana, game.State.RunePools[actor].Mana); await game.ColdCheckpoint();
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(6, game.Might(actor, unit));
            Assert.Equal(6, game.State.CardObjects[unit].Power); Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects[unit].Tags);
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(6, game.Might(actor, unit)); Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven001-submitted-" + actor + "-" + controlled); }
    }
}
