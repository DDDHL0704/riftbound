using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReviewedRuntimeCatalogAdmissionTests
{
    private const string PreviousCatalogIdentity = "e778e6e29472fbaccee0066f01a5f53c5f8191fd228a29f59073733b1a853682";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RuntimeSnapshotAddsTheImplementedReviewedPrintsAndKeepsTheOriginalImmutable()
    {
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Same(runtime, await OfficialCardCatalog.LoadRuntimeAsync());
        Assert.Equal(1089, original.Total);
        Assert.Equal(1089, original.Cards.Count);
        Assert.Equal(1110, runtime.Total);
        Assert.Equal(1110, runtime.Cards.Count);
        Assert.Contains(ReviewedRuneCardSources.SnapshotPath, runtime.Source);
        Assert.Contains(VendettaCardSources.SnapshotPath, runtime.Source);
        Assert.Contains(ReviewedResourceLegendSources.SnapshotPath, runtime.Source);
        var originalNos = original.Cards.Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["VEN·005", "VEN·006", "VEN·019", "VEN·019a", "VEN·054", "VEN·060", "VEN·062", "VEN·075", "VEN·077", "VEN·087", "VEN·125", "VEN·133", "VEN·139", "VEN·141", "VEN·147", "VEN·189", "VEN·189*", "VEN·190", "VEN·190*", "VEN·193", "VEN·193*"], runtime.Cards.Where(card => !originalNos.Contains(card.CardNo))
            .Select(card => card.CardNo).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(37690, Assert.Single(runtime.Cards, card => card.CardNo == "VEN·005").Id);
        Assert.Equal(37691, Assert.Single(runtime.Cards, card => card.CardNo == "VEN·006").Id);
        Assert.DoesNotContain(runtime.Cards, card => card.CardNo is "VEN·005/166" or "VEN·006/166" or "RAD·092/167");
        var mutable = Assert.IsAssignableFrom<IList<OfficialCard>>(runtime.Cards);
        Assert.Throws<NotSupportedException>(() => mutable[0] = mutable[0] with { CardEffect = "changed" });
        var schema = OfficialCardSchemaValidator.Validate(runtime);
        Assert.True(schema.IsValid, string.Join("; ", schema.Violations.Select(v => $"{v.CardNo}:{v.Field}:{v.Reason}")));
        var units = FunctionalUnitBuilder.Build(runtime.Cards);
        var play = CardBehaviorRegistry.GetAll().Select(definition => new ImplementedCardBehavior(
            definition.CardNo, definition.EffectKind, definition.DisplayName, CardBehaviorRegistry.TriggerEffectKinds(definition))).ToArray();
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(runtime.Cards, play);
        var specs = BehaviorSpecCatalogBuilder.Build(runtime.Cards, units, implemented);
        Assert.Equal(1110, specs.Count);
        Assert.Single(specs, spec => spec.CardNo == "VEN·005");
        Assert.Single(specs, spec => spec.CardNo == "VEN·006");
        Assert.Single(specs, spec => spec.CardNo == "VEN·019");
        Assert.Single(specs, spec => spec.CardNo == "VEN·019a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·141");
        Assert.Single(specs, spec => spec.CardNo == "VEN·190");
        Assert.Single(specs, spec => spec.CardNo == "VEN·190*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·147");
        Assert.Single(specs, spec => spec.CardNo == "VEN·193");
        Assert.Single(specs, spec => spec.CardNo == "VEN·193*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·139");
        Assert.Single(specs, spec => spec.CardNo == "VEN·189");
        Assert.Single(specs, spec => spec.CardNo == "VEN·189*");
        Assert.All(specs.Where(spec => spec.CardNo is "VEN·019" or "VEN·019a" or "VEN·141" or "VEN·190" or "VEN·190*" or "VEN·147" or "VEN·193" or "VEN·193*" or "VEN·139" or "VEN·189" or "VEN·189*"),
            spec => Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status));
    }

    [Theory]
    [InlineData("VEN·005", OfficialDeckFormat.CoreRules)]
    [InlineData("VEN·006", OfficialDeckFormat.CoreRules)]
    [InlineData("VEN·005", OfficialDeckFormat.ChinaStandard20260724)]
    [InlineData("VEN·006", OfficialDeckFormat.ChinaStandard20260724)]
    public async Task ActualSessionAcceptsAndOpensBothReviewedPrints(string cardNo, OfficialDeckFormat format)
    {
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = DeckWith(original, cardNo, format);
        Assert.DoesNotContain(original.Cards, card => card.CardNo == cardNo);
        var initial = MatchReplayInitialStateBuilder.FromSeats("reviewed-runtime-" + cardNo + "-" + format,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, Web);
        foreach (var player in new[] { "P1", "P2" })
        {
            var submitted = await session.SubmitDeckAsync(player, "deck-" + player, command, raw, default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
            Assert.Equal(3, submitted.State.PlayerDecklists[player].MainDeck.Count(no => no == cardNo));
            Assert.Equal(ExpectedEffectiveCatalogHash(original), submitted.State.RuleInputs!.CatalogSha256);
        }
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), Web);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        foreach (var player in new[] { "P1", "P2" })
        {
            var cards = opened.State.CardObjects.Values.Where(card => card.OwnerId == player && card.CardNo == cardNo).ToArray();
            Assert.Equal(3, cards.Length);
            Assert.All(cards, card => {
                Assert.Contains(CardObjectTags.UnitCard, card.Tags);
                Assert.Equal(cardNo == "VEN·005" ? 2 : 4, card.ManaCost);
                Assert.Equal(cardNo == "VEN·005" ? 2 : 4, card.Power);
            });
        }
    }

    [Theory]
    [InlineData("VEN·005/166")]
    [InlineData("VEN·006/166")]
    [InlineData("RAD·092/167")]
    public async Task UnsupportedPrintsAndUnadmittedSourcesCannotEnterADeck(string cardNo)
    {
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = DeckWith(original, cardNo, OfficialDeckFormat.CoreRules);
        var initial = MatchReplayInitialStateBuilder.FromSeats("reviewed-runtime-reject-" + cardNo,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var before = MatchStateHasher.Hash(initial);
        var result = await session.SubmitDeckAsync("P1", "bad-deck", command, JsonSerializer.SerializeToElement(command, Web), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, result.ErrorCode);
        Assert.Contains("未知牌号 " + cardNo, result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ActualRuleIdentityIncludesTheExactEffectiveCatalogAndRejectsThePreviousOne()
    {
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        Assert.Equal(1089, original.Cards.Count);
        var expected = ExpectedEffectiveCatalogHash(original);
        Assert.NotEqual(PreviousCatalogIdentity, expected);
        Assert.Equal(expected, RuleInputIdentity.Current.CatalogSha256);
        var current = RuleInputIdentity.Current;
        var prior = new RuleInputIdentity(current.SchemaVersion, current.Region, current.Language,
            current.AuthoritySha256, PreviousCatalogIdentity, current.ImplementationSha256);
        Assert.NotEqual(prior.IdentitySha256, current.IdentitySha256);
        var old = MatchReplayInitialStateBuilder.FromSeats("reviewed-prior-catalog",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { RuleInputs = prior };
        var result = await new CoreRuleEngine().ResolveAsync(old, new("prior", "P1", "PASS"), new PassCommand(), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.RecoveryInconsistent, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Throws<MatchSessionException>(() => new MatchSession(old, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(old, [], old, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch);
    }

    internal static string ExpectedEffectiveCatalogHash(OfficialCardCatalog original)
    {
        var reviewed = ReviewedRuneCardSources.GetAll().Where(card => (card.Id, card.CardNo) is
            (37690, "VEN·005") or (37691, "VEN·006")).ToArray();
        Assert.Equal(2, reviewed.Length);
        var cards = original.Cards.Concat(reviewed).Concat(VendettaCardSources.GetAdmittedCards()).Concat(ReviewedResourceLegendSources.GetPhysicalPrints())
            .OrderBy(card => card.CardNo, StringComparer.Ordinal).Select(c => new {
            c.Id,c.CardNo,c.CardName,c.SubTitle,c.CardCategory,c.CardCategoryName,c.CardCategoryList,c.CardCategoryNameList,
            c.CardColorList,c.Hero,c.Region,c.Tag,c.CardEffect,c.Energy,c.ReturnEnergy,c.Power,c.ExtendType,c.ExtendTypeName,
            c.CardGroupLimit,c.Status,c.ListSort,c.ProductCodeList,c.ProductNameList,c.Rarity,c.RarityName,c.ExtendRarity,c.ExtendRarityName });
        var armaments = OfficialArmamentCatalog.All.Select(a => new {
            a.CardNo,a.SourceCardNo,a.BaseMightBonus,a.EffectText,a.CopiesHostPrintedRulesText,a.CopiesChosenFriendlyUnit,a.ReplacesHostDestruction });
        return MatchStateHasher.HashValue(new { Cards = cards, Armaments = armaments });
    }

    private static OfficialDecklist DeckWith(OfficialCardCatalog catalog, string cardNo, OfficialDeckFormat format)
    {
        var original = PreconstructedDeckCatalog.Build(catalog, format).Single(deck => deck.Id == "jhin-lowcurve").Decklist;
        var cards = original.MainDeck.ToList();
        for (var i = 0; i < 3; i++) cards.RemoveAt(cards.FindIndex(no => no != original.ChampionCardNo));
        cards.InsertRange(0, [cardNo, cardNo, cardNo]);
        return original with { MainDeck = cards, RuneDeck = Enumerable.Repeat("UNL-R01", 12).ToArray() };
    }
}
