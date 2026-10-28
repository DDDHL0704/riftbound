using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RenektonSubmittedDeckTests
{
    [Theory]
    [InlineData("HERO")]
    [InlineData("ALT")]
    public async Task ChosenChampionReturnRecognizesBothImplementedHeroPrintings(string target)
    {
        var state = OfficialChosenChampionReturnTests.Position();
        state = state with {
            PlayerDecklists = new Dictionary<string, OfficialDecklist>(state.PlayerDecklists) {
                ["P1"] = state.PlayerDecklists["P1"] with { ChampionCardNo = "VEN·019" } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HERO"] = state.CardObjects["HERO"] with { CardNo = "VEN·019" },
                ["ALT"] = state.CardObjects["ALT"] with { CardNo = "VEN·019a" } } };
        var opened = await Act(state, "P1", new PassPriorityCommand());
        Assert.Equal(new[] { "ALT", "HERO" }, opened.State.PendingCardChoice!.LegalObjectIds);
        var confirmed = await OfficialHeldConfirmationTests.Choose(opened.State, target);
        var returned = await Top(confirmed.State);
        Assert.Equal(new[] { target }, returned.State.PlayerZones["P1"].ChampionZone);
        PublicRecovery(returned.State);
    }

    [Theory]
    [InlineData("VEN·141")]
    [InlineData("VEN·190")]
    [InlineData("VEN·190*")]
    public async Task ExistingBattlefieldGrantedLegendAbilityAcceptsTheRuntimeLegend(string legendNo)
    {
        var state = LegendAttachmentTests.ForgePosition();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { CardNo = legendNo } } };
        var activated = await Act(state, "P1", LegendAttachmentTests.Command(LegendAttachmentTests.Forge));
        Assert.Single(activated.State.StackItems, item => item.LegendAttachment is not null);
        PublicRecovery(activated.State);
        var done = await Top(activated.State);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("VEN·141", "VEN·019")]
    [InlineData("VEN·141", "VEN·019a")]
    [InlineData("VEN·190", "VEN·019")]
    [InlineData("VEN·190", "VEN·019a")]
    [InlineData("VEN·190*", "VEN·019")]
    [InlineData("VEN·190*", "VEN·019a")]
    public async Task OfficialPrintsCanBeSubmittedAndOpenAsLegendAndChampion(string legendNo, string championNo)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            legendNo, championNo, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("red"), 6).Concat(Enumerable.Repeat(Rune("orange"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("renekton-submitted-" + legendNo + "-" + championNo,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var player in new[] { "P1", "P2" })
        {
            var submitted = await session.SubmitDeckAsync(player, "deck-" + player, command, raw, default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
        }
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Contains(opened.State.PlayerZones[player].LegendZone, id => opened.State.CardObjects[id].CardNo == legendNo);
            Assert.Contains(opened.State.PlayerZones[player].ChampionZone, id => opened.State.CardObjects[id].CardNo == championNo);
        }
    }
}
