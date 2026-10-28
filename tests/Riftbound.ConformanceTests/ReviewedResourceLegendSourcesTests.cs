using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Exact source and physical identity, with the separately verified legend action.
public sealed class ReviewedResourceLegendSourcesTests
{
    private const string Text = "{{反应>}} 支付{{A}}{{A}}，{{横置}}：{{获得}}{{2}}。此法力仅能用于打出单位或单位的主动技能。";

    [Theory]
    [InlineData(37844, "VEN·141")]
    [InlineData(37894, "VEN·190")]
    [InlineData(37895, "VEN·190*")]
    public async Task EachExactOfficialApiKeyProvidesTheReviewedLegendFaceAndItsAction(long id, string no)
    {
        Assert.True(ReviewedResourceLegendSources.TryGet(no, out var source));
        Assert.Equal(id, source.Id); Assert.Equal(no, source.CardNo); Assert.Equal("荒漠屠夫", source.CardName);
        Assert.Equal(["legendary"], source.CardCategoryList); Assert.Equal(["传奇"], source.CardCategoryNameList);
        Assert.Equal(["red", "orange"], source.CardColorList); Assert.Equal("雷克顿", source.Hero);
        Assert.Equal(Text, source.CardEffect); Assert.Null(source.Energy); Assert.Null(source.ReturnEnergy); Assert.Null(source.Power);
        Assert.Contains(ReviewedResourceLegendSources.GetPhysicalPrints(), card => card.Id == id && card.CardNo == no);
        Assert.DoesNotContain((await OfficialCardCatalog.LoadDefaultAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(source, Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no));
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
        foreach (var field in typeof(LegendActionAbilityCatalog).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.IsLiteral && field.FieldType == typeof(string) && field.GetRawConstantValue() is string ability)
                Assert.Equal(ability == LegendActionAbilityCatalog.RenektonLegendAbilityId,
                    LegendActionAbilityCatalog.IsSourceCardNoForAbility(ability, no));

        var physical = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", no, "P1");
        Assert.Equal(no, physical.CardNo); Assert.Equal("P1", physical.OwnerId); Assert.Equal("P1", physical.ControllerId);
        Assert.Contains("CARD_TYPE:LEGEND", physical.Tags); Assert.Contains("HERO:雷克顿", physical.Tags);
        Assert.Contains("COLOR:red", physical.Tags); Assert.Contains("COLOR:orange", physical.Tags);
    }

    [Theory]
    [InlineData("VEN·141")]
    [InlineData("VEN·190")]
    [InlineData("VEN·190*")]
    public async Task RuntimeRecognizesEachLegendButRejectsAnUnmatchedChampionWithoutChangingTheLobby(string cardNo)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "jhin-lowcurve").Decklist with { LegendCardNo = cardNo };
        var initial = MatchReplayInitialStateBuilder.FromSeats("renekton-legend-invalid-champion-" + cardNo,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var result = await session.SubmitDeckAsync("P1", "invalid-champion", command, raw, default);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, result.ErrorCode);
        Assert.Contains("英雄牌的英雄标签必须与所选传奇一致", result.ErrorMessage);
        Assert.DoesNotContain("未知牌号 " + cardNo, result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ScopedLegendPrintsDoNotAlterEarlierCatalogsOrInventFaceAliases()
    {
        Assert.Equal("77c2e7046f37110c6ce35c116a0af0b82478aa316dd4852553bd4681bdc9d252", ReviewedResourceLegendSources.SnapshotSha256);
        Assert.Equal(3, ReviewedResourceLegendSources.GetAll().Count);
        Assert.Equal(3, ReviewedResourceLegendSources.GetPhysicalPrints().Count);
        Assert.Equal(3, ReviewedRuneCardSources.GetAll().Count);
        Assert.Equal(14, ScopedOfficialCardSources.GetAll().Count); Assert.Equal(13, ScopedOfficialCardSources.GetPhysicalPrints().Count);
        Assert.Equal(1089, (await OfficialCardCatalog.LoadDefaultAsync()).Cards.Count);
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        var create = factory.GetMethod("TryCreate", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var alias in new[] { "VEN·141/166", "VEN·190/166", "VEN·190*/166", "VEN-141", "VEN·141 ", " 荒漠屠夫" })
        {
            Assert.False(ReviewedResourceLegendSources.TryGet(alias, out _));
            Assert.False((bool)create.Invoke(null, ["LEGEND", "P1", alias, null])!);
        }
    }

    [Fact]
    public async Task PreviousReviewedSourceImplementationCannotResumeUnderThisAdditionalSource()
    {
        var current = RuleInputIdentity.Current;
        Assert.Equal("d0164efbc531aa9879094ea0a6bd7b891b4a5e3f3f184ae517bfadb231d7aa7f", current.AuthoritySha256);
        Assert.Equal(ReviewedRuntimeCatalogAdmissionTests.ExpectedEffectiveCatalogHash(await OfficialCardCatalog.LoadDefaultAsync()), current.CatalogSha256);
        // Captured from the actual c578 source-review test host, not rebound to this build.
        var prior = new RuleInputIdentity(1, "CN", "zh-CN", current.AuthoritySha256,
            "e778e6e29472fbaccee0066f01a5f53c5f8191fd228a29f59073733b1a853682",
            "4a428f66e5ea3eb71e09ccef02393294800066f15fd1b4ce749945304d890363");
        Assert.Equal("4907cedd469d5babd2a03619bf49b00aff4121bd0e210a5980ae60f463326bc0", prior.IdentitySha256);
        Assert.NotEqual(prior.ImplementationSha256, current.ImplementationSha256);
        var state = MatchReplayInitialStateBuilder.FromSeats("prior-legend-source", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" })
            with { RuleInputs = prior };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("old-source", "P1", "PASS"), new PassCommand(), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.RecoveryInconsistent, result.ErrorCode); Assert.Empty(result.Events);
        Assert.Throws<MatchSessionException>(() => new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [], state, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
    }
}
