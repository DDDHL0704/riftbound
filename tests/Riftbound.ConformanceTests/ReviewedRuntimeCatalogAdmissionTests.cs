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
        Assert.Equal(1297, runtime.Total);
        Assert.Equal(1297, runtime.Cards.Count);
        Assert.Contains(ReviewedRuneCardSources.SnapshotPath, runtime.Source);
        Assert.Contains(VendettaCardSources.SnapshotPath, runtime.Source);
        Assert.Contains(ReviewedResourceLegendSources.SnapshotPath, runtime.Source);
        var originalNos = original.Cards.Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["RAD·005/167", "RAD·014/167", "RAD·020/167", "RAD·020a/167", "RAD·026/167", "RAD·050/167", "RAD·054/167", "RAD·055/167", "RAD·061/167", "RAD·061a/167", "RAD·066/167", "RAD·072/167", "RAD·075/167", "RAD·093/167", "RAD·097/167", "RAD·137/167", "RAD·137a/167", "RAD·143/167", "RAD·144/167", "RAD·149/167", "RAD·150/167", "RAD·170*/167", "RAD·170/167", "RAD·173*/167", "RAD·173/167", "RAD·179*/167", "RAD·179/167", "RAD·180*/167", "RAD·180/167", "RAD·181*/167", "RAD·181/167", "RAD·182*/167", "RAD·182/167", "RAD·183*/167", "RAD·183/167", "VEN·002", "VEN·004", "VEN·005", "VEN·006", "VEN·009", "VEN·011", "VEN·015", "VEN·016", "VEN·018", "VEN·019", "VEN·019a", "VEN·020", "VEN·021", "VEN·021a", "VEN·022", "VEN·023", "VEN·023a", "VEN·024", "VEN·025", "VEN·027", "VEN·028", "VEN·029", "VEN·031", "VEN·034", "VEN·036", "VEN·037", "VEN·038", "VEN·038a", "VEN·039", "VEN·040", "VEN·041", "VEN·044", "VEN·045", "VEN·046", "VEN·046a", "VEN·052", "VEN·053", "VEN·054", "VEN·060", "VEN·061", "VEN·062", "VEN·063", "VEN·063a", "VEN·064", "VEN·065", "VEN·066", "VEN·067", "VEN·068", "VEN·068a", "VEN·071", "VEN·073", "VEN·075", "VEN·076", "VEN·077", "VEN·079", "VEN·080", "VEN·082", "VEN·083", "VEN·084", "VEN·084a", "VEN·085", "VEN·087", "VEN·088", "VEN·088a", "VEN·090", "VEN·091", "VEN·092", "VEN·092a", "VEN·094", "VEN·095", "VEN·096", "VEN·097", "VEN·098", "VEN·099", "VEN·100", "VEN·101", "VEN·102", "VEN·104", "VEN·106", "VEN·107", "VEN·108", "VEN·109", "VEN·110a", "VEN·111", "VEN·112", "VEN·112a", "VEN·113", "VEN·113a", "VEN·114", "VEN·115", "VEN·116", "VEN·119", "VEN·120", "VEN·121", "VEN·125", "VEN·126", "VEN·127", "VEN·131", "VEN·132", "VEN·133", "VEN·134", "VEN·135", "VEN·135a", "VEN·136", "VEN·136a", "VEN·139", "VEN·140", "VEN·141", "VEN·142", "VEN·143", "VEN·144", "VEN·145", "VEN·146", "VEN·147", "VEN·148", "VEN·149", "VEN·150", "VEN·151", "VEN·153", "VEN·154", "VEN·155", "VEN·155a", "VEN·157", "VEN·158", "VEN·160", "VEN·161", "VEN·162", "VEN·163", "VEN·164", "VEN·165", "VEN·166", "VEN·167", "VEN·168", "VEN·169", "VEN·171", "VEN·172", "VEN·173", "VEN·174", "VEN·175", "VEN·176", "VEN·177", "VEN·178", "VEN·180", "VEN·182", "VEN·184", "VEN·185", "VEN·187", "VEN·188", "VEN·189", "VEN·189*", "VEN·190", "VEN·190*", "VEN·191", "VEN·191*", "VEN·192", "VEN·192*", "VEN·193", "VEN·193*", "VEN·194", "VEN·194*", "VEN·195", "VEN·195*", "VEN·196", "VEN·196*", "VEN·197", "VEN·197*", "VEN·SP1", "VEN·SP2", "VEN·SP4", "VEN·SP5", "VEN·T04", "VEN·T05", "VEN·T06"], runtime.Cards.Where(card => !originalNos.Contains(card.CardNo))
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
        Assert.Equal(1297, specs.Count);
        Assert.Single(specs, spec => spec.CardNo == "VEN·005");
        Assert.Single(specs, spec => spec.CardNo == "VEN·006");
        Assert.Single(specs, spec => spec.CardNo == "VEN·175");
        Assert.Equal(BehaviorImplementationStatuses.Implemented, Assert.Single(specs, spec => spec.CardNo == "VEN·175").Status);
        Assert.Single(specs, spec => spec.CardNo == "VEN·019");
        Assert.Single(specs, spec => spec.CardNo == "VEN·092");
        Assert.Single(specs, spec => spec.CardNo == "VEN·092a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·177");
        Assert.Single(specs, spec => spec.CardNo == "VEN·167");
        Assert.Single(specs, spec => spec.CardNo == "VEN·019a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·021");
        Assert.Single(specs, spec => spec.CardNo == "VEN·021a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·038");
        Assert.Single(specs, spec => spec.CardNo == "VEN·038a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·041");
        Assert.Single(specs, spec => spec.CardNo == "VEN·171");
        Assert.Single(specs, spec => spec.CardNo == "VEN·046");
        Assert.Single(specs, spec => spec.CardNo == "VEN·046a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·063");
        Assert.Single(specs, spec => spec.CardNo == "VEN·063a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·178");
        Assert.Single(specs, spec => spec.CardNo == "VEN·065");
        Assert.Single(specs, spec => spec.CardNo == "VEN·173");
        Assert.Single(specs, spec => spec.CardNo == "VEN·135");
        Assert.Single(specs, spec => spec.CardNo == "VEN·135a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·136");
        Assert.Single(specs, spec => spec.CardNo == "VEN·136a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·187");
        Assert.Single(specs, spec => spec.CardNo == "VEN·141");
        foreach (var no in new[] { "VEN·140", "VEN·146", "VEN·148", "VEN·154" })
            Assert.Equal(BehaviorImplementationStatuses.Implemented, Assert.Single(specs, spec => spec.CardNo == no).Status);
        Assert.Single(specs, spec => spec.CardNo == "VEN·190");
        Assert.Single(specs, spec => spec.CardNo == "VEN·190*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·145");
        Assert.Single(specs, spec => spec.CardNo == "VEN·192");
        Assert.Single(specs, spec => spec.CardNo == "VEN·192*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·143");
        Assert.Single(specs, spec => spec.CardNo == "VEN·191");
        Assert.Single(specs, spec => spec.CardNo == "VEN·191*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·147");
        Assert.Single(specs, spec => spec.CardNo == "VEN·193");
        Assert.Single(specs, spec => spec.CardNo == "VEN·193*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·110a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·188");
        Assert.Single(specs, spec => spec.CardNo == "VEN·139");
        Assert.Single(specs, spec => spec.CardNo == "VEN·189");
        Assert.Single(specs, spec => spec.CardNo == "VEN·189*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·151");
        Assert.Single(specs, spec => spec.CardNo == "VEN·195");
        Assert.Single(specs, spec => spec.CardNo == "VEN·195*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·153");
        Assert.Single(specs, spec => spec.CardNo == "VEN·180");
        Assert.Single(specs, spec => spec.CardNo == "VEN·196");
        Assert.Single(specs, spec => spec.CardNo == "VEN·196*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·155");
        Assert.Single(specs, spec => spec.CardNo == "VEN·155a");
        Assert.Single(specs, spec => spec.CardNo == "VEN·176");
        Assert.Single(specs, spec => spec.CardNo == "VEN·197");
        Assert.Single(specs, spec => spec.CardNo == "VEN·197*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·149");
        Assert.Single(specs, spec => spec.CardNo == "VEN·194");
        Assert.Single(specs, spec => spec.CardNo == "VEN·194*");
        Assert.Single(specs, spec => spec.CardNo == "VEN·SP1");
        Assert.Single(specs, spec => spec.CardNo == "VEN·SP4");
        Assert.Single(specs, spec => spec.CardNo == "VEN·150");
        Assert.Single(specs, spec => spec.CardNo == "VEN·174");
        Assert.Single(specs, spec => spec.CardNo == "VEN·084");
        Assert.Single(specs, spec => spec.CardNo == "VEN·084a");
        Assert.All(specs.Where(spec => spec.CardNo is "VEN·019" or "VEN·019a" or "VEN·021" or "VEN·021a" or "VEN·023" or "VEN·023a" or "VEN·038" or "VEN·038a" or "VEN·041" or "VEN·046" or "VEN·046a" or "VEN·063" or "VEN·063a" or "VEN·065" or "VEN·068" or "VEN·068a" or "VEN·084" or "VEN·084a" or "VEN·088" or "VEN·088a" or "VEN·092" or "VEN·092a" or "VEN·109" or "VEN·110a" or "VEN·112" or "VEN·112a" or "VEN·113" or "VEN·113a" or "VEN·135" or "VEN·135a" or "VEN·136" or "VEN·136a" or "VEN·139" or "VEN·140" or "VEN·141" or "VEN·142" or "VEN·143" or "VEN·144" or "VEN·145" or "VEN·146" or "VEN·147" or "VEN·148" or "VEN·149" or "VEN·150" or "VEN·151" or "VEN·153" or "VEN·154" or "VEN·155" or "VEN·155a" or "VEN·167" or "VEN·168" or "VEN·169" or "VEN·171" or "VEN·172" or "VEN·173" or "VEN·174" or "VEN·176" or "VEN·177" or "VEN·178" or "VEN·180" or "VEN·182" or "VEN·184" or "VEN·187" or "VEN·188" or "VEN·189" or "VEN·189*" or "VEN·190" or "VEN·190*" or "VEN·191" or "VEN·191*" or "VEN·192" or "VEN·192*" or "VEN·193" or "VEN·193*" or "VEN·194" or "VEN·194*" or "VEN·195" or "VEN·195*" or "VEN·196" or "VEN·196*" or "VEN·197" or "VEN·197*" or "VEN·SP1" or "VEN·SP2" or "VEN·SP4" or "VEN·SP5" or "VEN·T05"),
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
        var cards = original.Cards.Concat(reviewed).Concat(VendettaCardSources.GetAdmittedCards())
            .Concat(RadiantCardSources.GetAdmittedCards()).Concat(ReviewedResourceLegendSources.GetPhysicalPrints())
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
