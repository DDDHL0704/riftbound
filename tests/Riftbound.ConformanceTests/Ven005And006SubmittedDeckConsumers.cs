using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven005And006SubmittedDeckConsumers
{
    [Theory]
    [InlineData("P1", "VEN·005", 2, 2, 1, false, false)]
    [InlineData("P2", "VEN·005", 2, 2, 1, false, false)]
    [InlineData("P1", "VEN·005", 2, 2, 1, false, true)]
    [InlineData("P2", "VEN·005", 2, 2, 1, false, true)]
    [InlineData("P1", "VEN·006", 4, 4, 2, true, false)]
    [InlineData("P2", "VEN·006", 4, 4, 2, true, false)]
    [InlineData("P1", "VEN·006", 4, 4, 2, true, true)]
    [InlineData("P2", "VEN·006", 4, 4, 2, true, true)]
    public async Task RealSubmittedDeckPaysForTheUnitAndComparesControlledRunesAtBeginning(
        string actor, string no, int mana, int might, int bonus, bool roam, bool fewer)
    {
        var enemy = actor == "P1" ? "P2" : "P1";
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "jhin-lowcurve", "UNL-R01", [no, no, no]);
        try
        {
            await game.WaitFor(actor, 4, no);
            var unit = game.Hand(actor, no).First();
            var previousGeneration = game.State.CardObjects[unit].ObjectGeneration;
            await game.Tap(actor); var paidFrom = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(unit, no, []));
            Assert.Equal(paidFrom - mana, game.State.RunePools[actor].Mana);
            Assert.Equal("BASE", game.State.ObjectLocations[unit].Zone);
            Assert.True(game.State.CardObjects[unit].IsExhausted);
            Assert.Equal(previousGeneration + 1, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(might, game.Might(actor, unit));
            if (fewer) await game.Act(actor, new RecycleRuneCommand(game.Runes(actor).First()));
            await game.Act(actor, new EndTurnCommand());
            Assert.Equal(enemy, game.State.TurnPlayerId);
            var own = game.Runes(actor).Length; var opposing = game.Runes(enemy).Length;
            if (!fewer)
            {
                Assert.True(opposing >= own, "The fixed ordinary opening must supply an equal-rune control through real opponent recycling.");
                foreach (var id in game.Runes(enemy).Take(opposing - own)) await game.Act(enemy, new RecycleRuneCommand(id));
            }
            Assert.Equal(fewer, game.Runes(actor).Length < game.Runes(enemy).Length);
            if (!fewer) Assert.Equal(game.Runes(actor).Length, game.Runes(enemy).Length);
            await game.Act(enemy, new EndTurnCommand());
            Assert.Equal(actor, game.State.TurnPlayerId);
            if (fewer)
            {
                Assert.Equal(MatchPhases.TurnStart, game.State.Phase);
                var trigger = Assert.Single(game.State.StackItems);
                Assert.Equal(unit, trigger.SourceObjectId); Assert.Equal(no, trigger.BeginningRune!.CardNo);
                Assert.Equal(previousGeneration + 1, trigger.BeginningRune.Source.Generation);
                Assert.Equal(might, game.Might(actor, unit));
                await game.ColdCheckpoint();
                var forged = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects)
                    { [unit] = game.State.CardObjects[unit] with { Damage = 1 } } };
                await game.RejectCoordinatedForgery("invented-damage-before-beginning-resolution", forged);
                await game.Top();
                Assert.Equal(might + bonus, game.Might(actor, unit));
                Assert.Equal(roam, game.State.CardObjects[unit].UntilEndOfTurnEffects.Contains("ROAM"));
            }
            else
            {
                Assert.Equal(MatchPhases.Main, game.State.Phase);
                Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
                Assert.Equal(might, game.Might(actor, unit));
                Assert.DoesNotContain("ROAM", game.State.CardObjects[unit].UntilEndOfTurnEffects);
            }
            await game.Act(actor, new EndTurnCommand());
            Assert.Equal(might, game.Might(actor, unit));
            Assert.DoesNotContain("ROAM", game.State.CardObjects[unit].UntilEndOfTurnEffects);
            Assert.Equal(previousGeneration + 1, game.State.CardObjects[unit].ObjectGeneration);
            await game.ColdCheckpoint();
        }
        finally { await game.Save("ven005-006-submitted-" + actor + "-" + no + "-" + fewer); }
    }
}
