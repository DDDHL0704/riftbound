using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class OriannaBoundLegendIntegrationTests
{
    private const string Orianna = "RAD·145/167";
    private const string Neeko = "RAD·167/167";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnAddedLegendCanDepartBeforeOrAfterItsEquipmentTransformationResolves(bool beforeResolution)
    {
        var game = await Position();
        var legend = await AddLegend(game);
        await OwnMain(game); await Confirm(game, "GEAR");
        if (!beforeResolution) await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(legend));
        Assert.NotNull(Assert.Single(game.State.BoundLegends!).DepartedAtTick);
        if (beforeResolution) {
            Assert.Equal(legend, Assert.Single(game.State.StackItems).SourceObjectId);
            await game.ColdCheckpoint(); await game.Top();
        }
        Assert.Equal(legend, game.State.CardObjects["GEAR"].LegendEquipmentUnit!.SourceLegend.ObjectId);
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ANewLegendCannotEndAnOlderLegendsConversionOfTheSameEquipment()
    {
        var game = await Position();
        var first = await AddLegend(game);
        await OwnMain(game); await Confirm(game, "GEAR"); await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"]));
        await game.Top();
        var second = await AddLegend(game);
        Assert.NotEqual(first, second);
        await OwnMain(game); await Confirm(game, "GEAR"); await game.Top();
        await OwnMain(game); await Confirm(game, "SECOND");
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        await game.Top();
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["SECOND"].Tags);
        await game.ColdCheckpoint(); await game.Replay();
    }

    private static async Task<string> AddLegend(Driver game)
    {
        var resources = game.State.RunePools["P1"].TotalPower == 0
            ? new[] { "RECYCLE_RUNE:POWER-RUNE" } : [];
        await game.Act("P1", new PlayCardCommand("NEEKO", Neeko, [], OptionalCosts: resources, Destination: "BASE"));
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        Assert.Equal(Orianna, game.State.CardObjects[legend].CardNo);
        return legend;
    }

    private static async Task OwnMain(Driver game)
    {
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.NotNull(game.State.PendingCardChoice);
    }

    private static async Task Confirm(Driver game, string target)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await game.TapRunes("P1", Enumerable.Range(0, 12).Select(i => "RUNE-" + i));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
    }

    private static async Task<Driver> Position()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(item => item.Id == "jhin-lowcurve").Decklist;
        var main = deck.MainDeck.ToArray();
        main[Array.FindLastIndex(main, no => no != deck.ChampionCardNo)] = Neeko;
        deck = deck with { MainDeck = main, AdditionalLegendCardNos = [Orianna] };
        var state = Put(Create(), "NEEKO", Neeko, "P1", "HAND");
        state = Put(state, "GEAR", "SFD·022/221", "P1", "BASE");
        state = Put(state, "SECOND", "SFD·033/221", "P1", "BASE");
        state = Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        for (var i = 0; i < 12; i++) state = Put(state, "RUNE-" + i, "UNL-R03", "P1", "BASE");
        state = Put(state, "POWER-RUNE", "UNL-R03", "P1", "BASE");
        return new Driver(state with {
            PlayerDecklists = new Dictionary<string, OfficialDecklist> { ["P1"] = deck },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(50, 50), ["P2"] = new(50, 50) }
        });
    }
}
