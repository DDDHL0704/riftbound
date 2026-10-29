using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHeroReprintTests
{
    public static IEnumerable<object[]> Printings() => new object[][] {
        ["RAD·179*/167", 39697L, "厄斐琉斯", "出尘", 4, 4],
        ["RAD·179/167", 39698L, "厄斐琉斯", "出尘", 4, 4],
        ["RAD·180*/167", 39699L, "伊泽瑞尔", "翩然俊朗", 4, 3],
        ["RAD·180/167", 39700L, "伊泽瑞尔", "翩然俊朗", 4, 3],
        ["RAD·181*/167", 39701L, "永恩", "一剑封尘", 5, 5],
        ["RAD·181/167", 39702L, "永恩", "一剑封尘", 5, 5],
        ["RAD·182*/167", 39703L, "凯隐", "巨镰奔扬", 6, 6],
        ["RAD·182/167", 39704L, "凯隐", "巨镰奔扬", 6, 6],
        ["RAD·183*/167", 39705L, "瑟提", "魁首", 4, 5],
        ["RAD·183/167", 39706L, "瑟提", "魁首", 4, 5]
    };
    public static IEnumerable<object[]> CardNumbers() => Printings().Select(row => new[] { row[0] });
    public static IEnumerable<object[]> InvalidPlays() => Printings().SelectMany(row =>
        new[] { "mana", "power", "targets", "wrong-turn" }.Select(branch => new[] { row[0], branch }));

    private static MatchState Position(string cardNo) => VenUnitEntryBasicTests.Position(cardNo);

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task EveryPhysicalPrintingPaysItsOfficialCostAndEntersWithItsPrintedStats(
        string no, long id, string name, string subtitle, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(name, card.CardName); Assert.Equal(subtitle, card.SubTitle);
        Assert.Equal(mana, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(power, card.Power);
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        if (game.State.PendingCardChoice is { ChoiceWindow: "TEMPERED_CONFIRMATION" } choice)
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(power, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task InvalidPaymentTargetsAndTimingLeaveStateAndPublicViewsUnchanged(string no, string branch)
    {
        var state = Position(no);
        if (branch is "mana" or "power")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = branch == "mana" ? new(0, 20) : new(20, 0) } };
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var command = new PlayCardCommand("UNIT", no, branch == "targets" ? ["ALLY"] : [], Destination: "BASE");
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid-rad-hero", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State)));
    }

    [Theory]
    [InlineData("SFD·116/221")] [InlineData("RAD·181*/167")] [InlineData("RAD·181/167")]
    public async Task YoneCanAttachAnArmamentAfterItsIndependentTemperedResponseWindow(string no)
    {
        var selected = await TemperedPlayTriggerAuditTests.Confirm(TemperedPlayTriggerAuditTests.PositionFor(no));
        Assert.Null(selected.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.NotNull(selected.State.PriorityPlayerId);
        var power = selected.State.RunePools["P1"].TotalPower;
        var resolved = await OfficialGraveyardRecastTests.Top(selected.State);
        Assert.Equal("HOST", resolved.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(power - 1, resolved.State.RunePools["P1"].TotalPower);
        Assert.Empty(resolved.State.StackItems);
        OfficialGraveyardRecastTests.Restore(resolved.State);
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task AllAdmittedRadiantPrintingsHaveImplementedBehaviorAndKeepTheSourceSnapshotImmutable()
    {
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        var printings = RadiantCardSources.GetAdmittedCards();
        Assert.Equal(CardNumbers().Select(row => (string)row[0])
                .Concat(["RAD·042/167", "RAD·042a/167", "RAD·045/167", "RAD·045a/167", "RAD·085/167"]).Order(),
            printings.Where(card => card.CardCategoryName == "英雄单位").Select(card => card.CardNo).Order());
        Assert.Throws<NotSupportedException>(() => ((IList<OfficialCard>)printings)[0] = printings[0] with { CardEffect = "forged" });
        var play = CardBehaviorRegistry.GetAll().Select(definition => new ImplementedCardBehavior(
            definition.CardNo, definition.EffectKind, definition.DisplayName, CardBehaviorRegistry.TriggerEffectKinds(definition))).ToArray();
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(runtime.Cards, play);
        var specs = BehaviorSpecCatalogBuilder.Build(runtime.Cards, FunctionalUnitBuilder.Build(runtime.Cards), implemented);
        foreach (var card in printings)
            Assert.Equal(BehaviorImplementationStatuses.Implemented, Assert.Single(specs, spec => spec.CardNo == card.CardNo).Status);
        Assert.DoesNotContain((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo.StartsWith("RAD·", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CardNumbers))]
    public async Task EveryPrintingCanBeSubmittedInAChinaStandardDeck(string no)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        var hero = Assert.Single(allowed.Cards, c => c.CardNo == no);
        var legend = allowed.Cards.Where(c => c.CardCategory == "legendary"
            && hero.CardColorList.All(c.CardColorList.Contains)
            && allowed.Cards.Any(champion => champion.CardCategoryName == "英雄单位" && champion.Hero == c.Hero
                && champion.CardColorList.All(c.CardColorList.Contains)))
            .OrderByDescending(c => c.Hero == hero.Hero).ThenBy(c => c.CardNo, StringComparer.Ordinal).First();
        var champion = allowed.Cards.Where(c => c.CardCategoryName == "英雄单位" && c.Hero == legend.Hero
            && c.CardColorList.All(legend.CardColorList.Contains)).OrderBy(c => c.CardNo, StringComparer.Ordinal).First();
        var runes = legend.CardColorList.SelectMany(color => Enumerable.Repeat(allowed.Cards
            .Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo, 6)).ToArray();
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            legend.CardNo, champion.CardNo, new[] { no }, new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, runes])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("rad-hero-deck-" + no,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var submitted = await session.SubmitDeckAsync("P1", "deck", command, raw, default);
        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        Assert.Equal(champion.CardNo, submitted.State.PlayerDecklists["P1"].ChampionCardNo);
        Assert.Contains(no, submitted.State.PlayerDecklists["P1"].MainDeck);
    }
}
