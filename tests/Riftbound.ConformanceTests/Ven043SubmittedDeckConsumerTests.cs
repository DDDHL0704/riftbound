using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven043SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SubmittedGreenDecksReallyPlayZeroMightThenPaySevenAndRejectCoherentRefundOrPrematureEmpower(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "lillia-lowcurve", "UNL-R02",
            ["VEN·043", "VEN·043", "VEN·043"]);
        try
        {
            await game.WaitFor(actor, 8, "VEN·043");
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            await game.Tap(actor); var mana = game.State.RunePools[actor].Mana;
            var unit = game.Hand(actor, "VEN·043").First();
            await game.Act(actor, new PlayCardCommand(unit, "VEN·043", []));
            Assert.Equal(mana - 1, game.State.RunePools[actor].Mana); Assert.Contains(unit, game.State.PlayerZones[actor].Base);
            Assert.Equal(0, game.State.CardObjects[unit].Power); Assert.Equal(0, game.Might(actor, unit));
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects[unit].Tags);
            Assert.Empty(game.State.StackItems); await game.ColdCheckpoint();
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(mana - 8, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Equal(7, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(0, game.Might(actor, unit));
            await game.ColdCheckpoint();
            var pool = game.State.RunePools[actor];
            var refunded = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
                [actor] = pool with { Mana = pool.Mana + 7 } } };
            await game.RejectCoordinatedForgery("refunded-steelclaw-seven-mana-" + actor, refunded);
            var premature = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                [unit] = game.State.CardObjects[unit] with { IsEmpowered = true } } };
            await game.RejectCoordinatedForgery("premature-steelclaw-empowered-status-" + actor, premature);
            await game.Top(); Check(game, actor, unit, generation);
            Assert.Equal(mana - 8, game.State.RunePools[actor].Mana); await game.ColdCheckpoint();
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Check(game, actor, unit, generation); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven043-submitted-zero-might-and-seven-mana-" + actor); }
    }

    private static void Check(VenEmpowerCanonicalOpeningTests.CanonicalGame game, string actor, string unit, long generation)
    {
        Assert.Contains(unit, game.State.PlayerZones[actor].Base); Assert.DoesNotContain(unit, game.State.PlayerZones[actor].Graveyard);
        Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(0, game.State.CardObjects[unit].Power);
        Assert.Equal(7, game.Might(actor, unit)); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
        Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects[unit].Tags);
        Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers);
    }
}
