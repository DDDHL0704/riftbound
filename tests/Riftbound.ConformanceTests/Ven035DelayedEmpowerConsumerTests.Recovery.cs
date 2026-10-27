using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanonicalDelayedInverseNeedsItsOriginalBirthDespiteConsistentCurrentViews(bool remove)
    {
        var runes = Enumerable.Repeat("UNL-R03", 6).Concat(Enumerable.Repeat("UNL-R02", 6)).ToArray();
        var game = await VenEmpowerCanonicalOpeningTests.Open("P1", "lillia-lowcurve", "UNL-R03",
            [No, No, No, UnitNo, UnitNo, UnitNo], runeDeck: runes);
        try
        {
            await game.WaitFor("P1", 9, No, UnitNo); await game.Tap("P1");
            await game.Act("P1", new RecycleRuneCommand(game.Runes("P1").First(id => game.State.CardObjects[id].CardNo == "UNL-R03")));
            var unit = game.Hand("P1", UnitNo).First();
            await game.Act("P1", new PlayCardCommand(unit, UnitNo, []));
            if (remove)
            {
                await game.Act("P1", new ActivateAbilityCommand(unit, "EMPOWER:0:0", [])); await game.Top();
                Assert.True(game.State.CardObjects[unit].IsEmpowered);
            }
            await game.Act("P1", new RecycleRuneCommand(game.Runes("P1").First(id => game.State.CardObjects[id].CardNo == "UNL-R02")));
            var spell = game.Hand("P1", No).First();
            await game.Act("P1", new PlayCardCommand(spell, No, [unit], Mode: remove ? Remove : Empower));
            await game.Top(); await game.ColdCheckpoint();
            Assert.Equal(!remove, game.State.CardObjects[unit].IsEmpowered);
            Assert.Contains(spell, game.State.PlayerZones["P1"].Graveyard);
            var marker = Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(CoreRuleEngine.DelayedEmpowerPrefix, StringComparison.Ordinal));
            var context = Assert.IsType<DelayedEmpowerContext>(CoreRuleEngine.ReadDelayedEmpower(marker));
            Assert.Equal(new ObjectBinding(unit, game.State.CardObjects[unit].ObjectGeneration), context.Unit);
            Assert.True(context.Unit.Generation > 0 && context.Source.Generation > 0 && context.CreatedAtTick > 1);
            Assert.Equal(game.State.TurnNumber, context.TurnNumber);
            Assert.Equal("P1", context.TurnPlayerId);
            Assert.Equal("P1", context.ControllerId);

            MatchState Replace(DelayedEmpowerContext replacement) => game.State with {
                UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(e => e == marker
                    ? CoreRuleEngine.DelayedEmpowerMarker(replacement) : e).ToArray() };
            await game.RejectCoordinatedForgery("deleted-delayed-inverse", game.State with {
                UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(e => e != marker).ToArray() });
            await game.RejectCoordinatedForgery("opposite-delayed-inverse", Replace(context with {
                CreatingEffectKind = remove ? "TEMPORARY_EMPOWER_UNIT" : "TEMPORARY_REMOVE_EMPOWER_UNIT" }));
            await game.RejectCoordinatedForgery("different-old-unit-generation", Replace(context with {
                Unit = context.Unit with { Generation = context.Unit.Generation - 1 } }));
            await game.RejectCoordinatedForgery("different-old-source-generation", Replace(context with {
                Source = context.Source with { Generation = context.Source.Generation - 1 } }));
            await game.RejectCoordinatedForgeryTwiceThenRecover("different-delay-birth-tick", Replace(context with {
                CreatedAtTick = context.CreatedAtTick - 1 }));

            foreach (var malformed in new[] {
                Replace(context with { TurnNumber = context.TurnNumber + 1 }),
                Replace(context with { CreatingEffectKind = "UNRELATED_STATUS" }),
                Replace(context with { ControllerId = "UNSEATED" }),
                Replace(context with { Unit = context.Unit with { Generation = game.State.CardObjects[unit].ObjectGeneration + 1 } }),
                game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Append(marker).ToArray() },
                game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(e => e == marker ? e + "!" : e).ToArray() }
            }) Assert.Contains(OfficialInsightAndSpellLockTests.Errors(malformed), e => e.Contains("delayed empower", StringComparison.Ordinal));

            await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
            Assert.Equal(remove, game.State.CardObjects[unit].IsEmpowered);
            await game.ColdCheckpoint();
        }
        finally { await game.Save("ven035-delay-original-birth-remove-" + remove); }
    }
}
