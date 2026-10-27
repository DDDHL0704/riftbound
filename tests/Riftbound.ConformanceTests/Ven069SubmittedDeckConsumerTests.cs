using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven069SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SubmittedDecksNaturallyPayMelsBluePlayCostAndManaOnlyEmpowerWithoutRefundingItsHistory(string actor)
    {
        // FromSeats already assigns P1/P2. Actual deck submission, ready,
        // mulligan, draws and rune channeling follow; room/seat commands do not.
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "lillia-lowcurve", "UNL-R03",
            ["VEN·069", "VEN·069", "VEN·069"]);
        try
        {
            await game.WaitFor(actor, 8, "VEN·069"); Assert.Equal(0, game.State.RunePools[actor].Mana);
            Assert.Equal(0, game.State.RunePools[actor].TotalPower); await game.Tap(actor);
            var unit = game.Hand(actor, "VEN·069").First(); var mana = game.State.RunePools[actor].Mana;
            var hand = game.State.PlayerZones[actor].Hand.Count; var deck = game.State.PlayerZones[actor].MainDeck.Count;
            var rune = game.Runes(actor).First(); Assert.Equal("UNL-R03", game.State.CardObjects[rune].CardNo);
            await game.Act(actor, new RecycleRuneCommand(rune));
            Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Blue]);
            await game.Act(actor, new PlayCardCommand(unit, "VEN·069", []));
            Assert.Equal(mana - 4, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Contains(unit, game.State.PlayerZones[actor].Base); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(4, game.Might(actor, unit)); Assert.Equal(hand - 1, game.State.PlayerZones[actor].Hand.Count);
            Assert.Equal(deck, game.State.PlayerZones[actor].MainDeck.Count);
            var entry = Assert.Single(game.State.StackItems); Assert.Equal(unit, entry.SourceObjectId);
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.ColdCheckpoint(); await game.Top();
            Assert.Equal(hand, game.State.PlayerZones[actor].Hand.Count); Assert.Equal(deck - 1, game.State.PlayerZones[actor].MainDeck.Count);
            Assert.Equal(mana - 4, game.State.RunePools[actor].Mana); Assert.Empty(game.State.StackItems);
            await game.ColdCheckpoint();
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(mana - 7, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var activation = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(3, activation.PaidMana); Assert.Equal(generation, activation.Source.Generation);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.Might(actor, unit));
            await game.ColdCheckpoint();
            var pool = game.State.RunePools[actor];
            var refunded = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
                [actor] = pool with { Mana = pool.Mana + 3 } } };
            await game.RejectCoordinatedForgery("refunded-actual-mel-three-mana-empower-" + actor, refunded);
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(mana - 7, game.State.RunePools[actor].Mana); Assert.Equal(4, game.Might(actor, unit));
            Assert.Equal(hand, game.State.PlayerZones[actor].Hand.Count); Assert.Equal(deck - 1, game.State.PlayerZones[actor].MainDeck.Count);
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            await game.ColdCheckpoint();
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(4, game.State.CardObjects[unit].Power); Assert.Equal(4, game.Might(actor, unit));
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven069-submitted-decks-native-" + actor); }
    }
}
