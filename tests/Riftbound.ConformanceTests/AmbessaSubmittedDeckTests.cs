using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AmbessaSubmittedDeckTests
{
    public static IEnumerable<object[]> Prints() => new[] { "VEN·153", "VEN·196", "VEN·196*" }
        .SelectMany(legend => new[] { "VEN·136", "VEN·136a", "VEN·187" }.Select(hero => new object[] { legend, hero }));

    [Theory]
    [MemberData(nameof(Prints))]
    public async Task ChinaStandardDeckAcceptsAndOpensEveryLegendAndHeroPrintingForBothPlayers(string legend, string hero)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            legend, hero, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("orange"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("ambessa-" + legend + "-" + hero,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web); var raw = JsonSerializer.SerializeToElement(command, json);
        foreach (var player in new[] { "P1", "P2" })
        {
            var submitted = await session.SubmitDeckAsync(player, "deck-" + player, command, raw, default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
        }
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), json);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Contains(opened.State.PlayerZones[player].LegendZone, id => opened.State.CardObjects[id].CardNo == legend);
            Assert.Contains(opened.State.PlayerZones[player].ChampionZone, id => opened.State.CardObjects[id].CardNo == hero);
        }
    }
}
