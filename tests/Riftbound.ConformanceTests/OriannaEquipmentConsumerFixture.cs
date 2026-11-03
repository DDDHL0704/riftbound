using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

internal static class OriannaEquipmentConsumerFixture
{
    private static readonly string[] ManaRunes = Enumerable.Range(0, 8).Select(i => "CONVERT-MANA-" + i).ToArray();
    // Assembly needs green; the follow-up destruction spells need two red
    // (OGN 022) or two yellow (OGN 229), and Cage needs two yellow (RAD 136).
    private static readonly (string Id, string CardNo)[] PowerRuneCards = [
        ("CONVERT-GREEN", "UNL-R02"), ("CONVERT-BLUE", "UNL-R03"),
        ("CONVERT-RED-1", "UNL-R01"), ("CONVERT-RED-2", "UNL-R01"),
        ("CONVERT-YELLOW-1", "UNL-R06"), ("CONVERT-YELLOW-2", "UNL-R06")];
    private static readonly string[] PowerRunes = PowerRuneCards.Select(rune => rune.Id).ToArray();

    internal static MatchState WithResources(MatchState state)
    {
        foreach (var id in ManaRunes) state = RadGearTestPosition.Put(state, id, "UNL-R03", "P1", "BASE");
        foreach (var (id, no) in PowerRuneCards)
            state = RadGearTestPosition.Put(state, id, no, "P1", "BASE");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 4; i++)
                state = RadGearTestPosition.Put(state, "CONVERT-DECK-" + player + i, "OGN·219/298", player, "MAIN_DECK");
        return state;
    }

    internal static async Task AnimateAtNextOwnMain(Driver game, string target)
    {
        // The ordinary Act path validates its journal but does not retain the
        // result object. These projections only drive the existing turn helper.
        ResolutionResult Current() => new(true, null, game.State, [],
            ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State));
        async ValueTask<ResolutionResult> Submit(string player, GameCommand command)
        {
            await game.Act(player, command);
            return Current();
        }
        await game.Act("P1", new EndTurnCommand());
        await TurnSequenceTestDriver.Complete(Current(), Submit);
        await game.Act("P2", new EndTurnCommand());
        // Decline optional Hold costs before reaching the actual main trigger.
        await TurnSequenceTestDriver.Complete(Current(), Submit);
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("LEGEND", choice.SourceObjectId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.ManaCost);
        await game.TapRunes("P1", ManaRunes);
        await game.Act("P1", new RecycleRuneCommand(PowerRunes[0], PowerRunes));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Top();
        Assert.NotNull(game.State.CardObjects[target].LegendEquipmentUnit);
    }
}
