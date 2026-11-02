using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AdditionalLegendSelectionTests
{
    private const string CurrentText = "如果你的卡组中包含妮蔻，则在起始传奇之外，选择最多三名不同的传奇。（“不同”指的是卡牌名称各不相同。）";

    private static async Task<(OfficialCardCatalog Catalog, OfficialDecklist Deck)> Position()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var path = Path.Combine(Path.GetDirectoryName(OfficialCardCatalog.DefaultCatalogPath())!, "upstream", "2026-10-16",
            "source-refresh-20261015T160523Z", "card-catalog.zh-CN.json");
        var source = await OfficialCardCatalogLoader.LoadFromFileAsync(path, default);
        // The isolated selection mechanism is tested with the current erratum;
        // Neeko stays outside runtime admission until its complete effect works.
        var neeko = Assert.Single(source.Cards, c => c.CardNo == "RAD·167/167") with { CardEffect = CurrentText };
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "jhin-lowcurve").Decklist;
        catalog = catalog with { Cards = [..catalog.Cards, neeko], Total = catalog.Total + 1 };
        var main = deck.MainDeck.ToList();
        main[main.FindLastIndex(no => no != deck.ChampionCardNo)] = neeko.CardNo;
        return (catalog, deck with { MainDeck = main });
    }

    private static string[] Choices(OfficialCardCatalog catalog, OfficialDecklist deck, int count)
    {
        var starting = catalog.Cards.Single(c => c.CardNo == deck.LegendCardNo);
        return catalog.Cards.Where(c => c.CardCategoryName == "传奇" && c.CardName != starting.CardName)
            .GroupBy(c => c.CardName, StringComparer.Ordinal).Select(g => g.First().CardNo).Take(count).ToArray();
    }

    [Fact]
    public void OnlyTheOptionalCurrentErratumSuppliesTheSelectionPermission()
    {
        var authorityPath = Path.Combine(Path.GetDirectoryName(OfficialCardCatalog.DefaultCatalogPath())!, "upstream", "2026-10-28",
            "radiance-rule-authorities", "source-snapshot.json");
        using var authority = JsonDocument.Parse(File.ReadAllText(authorityPath));
        Assert.Contains(authority.RootElement.GetProperty("sources").EnumerateArray(), source =>
            source.TryGetProperty("localizedBodyHtml", out var body)
                && System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(body.GetString()!, "<[^>]+>", ""))
                    .Contains(CurrentText, StringComparison.Ordinal));
        var spec = Assert.IsType<AdditionalLegendSelectionSpec>(AdditionalLegendSelectionParser.Parse(CurrentText));
        Assert.Equal("妮蔻", spec.RequiredCardName); Assert.Equal(3, spec.MaximumCount);
        Assert.Null(AdditionalLegendSelectionParser.Parse(CurrentText.Replace("最多三名", "三名", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ADeckContainingThePermissionCanChooseZeroToThreeOtherLegendNames(int count)
    {
        var (catalog, deck) = await Position();
        var selected = Choices(catalog, deck, count); Assert.Equal(count, selected.Length);
        var validation = OfficialDeckValidator.Validate(deck with { AdditionalLegendCardNos = selected }, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }

    [Fact]
    public async Task MultipleCopiesShareOneSelectionPoolAndDoNotIncreaseTheLimit()
    {
        var (catalog, deck) = await Position();
        var main = deck.MainDeck.ToList();
        for (var copy = 1; copy < 3; copy++)
            main[main.FindIndex(no => no != deck.ChampionCardNo && no != "RAD·167/167")] = "RAD·167/167";
        deck = deck with { MainDeck = main, AdditionalLegendCardNos = Choices(catalog, deck, 3) };
        var valid = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
        var tooMany = OfficialDeckValidator.Validate(deck with { AdditionalLegendCardNos = Choices(catalog, deck, 4) }, catalog);
        Assert.Contains(tooMany.Errors, error => error.Contains("最多可额外选择 3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAdditionalLegendMayHaveTraitsOutsideTheStartingLegend()
    {
        var (catalog, deck) = await Position();
        var allowed = catalog.Cards.Single(card => card.CardNo == deck.LegendCardNo).CardColorList;
        var chosen = catalog.Cards.First(card => card.CardCategoryName == "传奇"
            && card.CardColorList.Any(trait => !allowed.Contains(trait)));
        var result = OfficialDeckValidator.Validate(deck with { AdditionalLegendCardNos = [chosen.CardNo] }, catalog);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("too-many")] [InlineData("same-print")] [InlineData("same-name")]
    [InlineData("starting-legend")] [InlineData("not-legend")] [InlineData("unknown")]
    public async Task InvalidSelectionsCannotBeAdmitted(string branch)
    {
        var (catalog, deck) = await Position();
        var selected = Choices(catalog, deck, 4);
        var error = "最多";
        switch (branch)
        {
            case "same-print": selected = [selected[0], selected[0]]; error = "不同的卡牌名称"; break;
            case "same-name":
                var startingName = catalog.Cards.Single(c => c.CardNo == deck.LegendCardNo).CardName;
                var prints = catalog.Cards.Where(c => c.CardCategoryName == "传奇" && c.CardName != startingName)
                    .GroupBy(c => c.CardName).First(g => g.Count() > 1).Take(2).ToArray();
                selected = prints.Select(c => c.CardNo).ToArray(); error = "不同的卡牌名称"; break;
            case "starting-legend": selected = [deck.LegendCardNo]; error = "起始传奇"; break;
            case "not-legend": selected = [deck.ChampionCardNo]; error = "必须是一张传奇牌"; break;
            case "unknown": selected = ["missing"]; error = "additionalLegendCardNos"; break;
        }
        var validation = OfficialDeckValidator.Validate(deck with { AdditionalLegendCardNos = selected }, catalog);
        Assert.False(validation.IsValid); Assert.Contains(validation.Errors, message => message.Contains(error, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADeckWithoutThePermissionCannotSubmitAnyAdditionalLegend()
    {
        var (catalog, deck) = await Position();
        var main = deck.MainDeck.ToArray(); main[Array.IndexOf(main, "RAD·167/167")] = deck.ChampionCardNo;
        var validation = OfficialDeckValidator.Validate(deck with { MainDeck = main, AdditionalLegendCardNos = Choices(catalog, deck, 1) }, catalog);
        Assert.Contains(validation.Errors, message => message.Contains("最多可额外选择 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WireMappingNormalizationAndSerializationRetainTheExactChosenPrintings()
    {
        var (_, deck) = await Position();
        var raw = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.SubmitDeck,
            legendCardNo = deck.LegendCardNo, championCardNo = deck.ChampionCardNo, mainDeck = deck.MainDeck,
            runeDeck = deck.RuneDeck, battlefields = deck.Battlefields, additionalLegendCardNos = new[] { " UNL-181/219 ", "SFD·181/221" } });
        var command = Assert.IsType<SubmitDeckCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Equal(new[] { "UNL-181/219", "SFD·181/221" }, command.AdditionalLegendCardNos);
        var normalized = OfficialDeckValidator.Normalize(deck with { AdditionalLegendCardNos = command.AdditionalLegendCardNos });
        var restored = JsonSerializer.Deserialize<OfficialDecklist>(JsonSerializer.Serialize(normalized))!;
        Assert.Equal(command.AdditionalLegendCardNos, restored.AdditionalLegendCardNos);
        var ordinary = OfficialDeckValidator.Normalize(deck with { AdditionalLegendCardNos = [] });
        Assert.Null(ordinary.AdditionalLegendCardNos);
        Assert.DoesNotContain("AdditionalLegendCardNos", JsonSerializer.Serialize(ordinary));
    }

    [Fact]
    public async Task RegisteredLegendsStayPrivateAndDoNotExistInAnyGameZoneBeforeTheyEnter()
    {
        var (catalog, deck) = await Position();
        var selected = Choices(catalog, deck, 3);
        var initial = OfficialHoldSequenceTests.State("OGN·294/298");
        var state = initial with { PlayerDecklists = new Dictionary<string, OfficialDecklist> {
            ["P1"] = deck with { AdditionalLegendCardNos = selected } } };
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var no in selected)
        {
            Assert.DoesNotContain(state.CardObjects.Values, card => card.CardNo == no);
            Assert.DoesNotContain(no, JsonSerializer.Serialize(snapshots));
            Assert.DoesNotContain(no, JsonSerializer.Serialize(prompts));
            Assert.DoesNotContain(no, JsonSerializer.Serialize(spectator));
        }
        Assert.Equal(MatchStateHasher.HashValue(initial.CardObjects), MatchStateHasher.HashValue(state.CardObjects));
        Assert.Equal(MatchStateHasher.HashValue(initial.PlayerZones), MatchStateHasher.HashValue(state.PlayerZones));
        Assert.Equal(selected, state.PlayerDecklists["P1"].AdditionalLegendCardNos);
    }

    [Theory]
    [InlineData("HIDDEN")] [InlineData("")] [InlineData("  ")]
    public async Task RecoveryRejectsRedactedOrBlankAdditionalLegendCardNumbers(string value)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "jhin-lowcurve").Decklist;
        var session = new MatchSession("additional-legend-recovery", new CoreRuleEngine());
        session.EnsurePlayer("P1"); session.EnsurePlayer("P2");
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo,
            deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var accepted = await session.SubmitDeckAsync("P1", "ordinary-deck", command,
            NativeCommandWire.Bind(command, JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.SubmitDeck })), default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var state = accepted.State;
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var malformed = state with { PlayerDecklists = new Dictionary<string, OfficialDecklist> {
            ["P1"] = deck with { AdditionalLegendCardNos = [value] } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(malformed),
            error => error.Contains("additional legend card", StringComparison.Ordinal));
    }
}
