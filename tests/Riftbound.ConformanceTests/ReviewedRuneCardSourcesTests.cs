using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Exact official source facts and shared physical/resource paths only. These
// controlled cases do not register the cards or certify their behavior.
public sealed class ReviewedRuneCardSourcesTests
{
    [Theory]
    [InlineData(37690, "VEN·005", "巴凯遗民", 2, 2, "red", "在你的开始阶段开始时，如果你控制的符文数量少于任一对手，则给予我在本回合内{{S}}+1。")]
    [InlineData(37691, "VEN·006", "绿洲劫掠者", 4, 4, "red", "在你的开始阶段开始时，如果你控制的符文数量少于任一对手，则给予我在本回合内{{S}}+2和{{游走}}。（我可以向其他战场进行移动。）")]
    [InlineData(40170, "RAD·092/167", "天界双足龙", 7, 7, "orange", "如果你将召出符文，则改为召出同等数量的符文，外加一枚休眠的符文。")]
    public async Task ExactReviewedPrintKeepsIndependentSourcePhysicalAndCostFacts(
        long id, string no, string name, int mana, int might, string trait, string text)
    {
        Assert.True(ReviewedRuneCardSources.TryGet(no, out var source));
        Assert.Equal(id, source.Id); Assert.Equal(no, source.CardNo); Assert.Equal(name, source.CardName);
        Assert.Equal("单位", source.CardCategoryName); Assert.Equal(["unit"], source.CardCategoryList);
        Assert.Equal([trait], source.CardColorList); Assert.Equal(mana, source.Energy); Assert.Equal(might, source.Power);
        Assert.Equal(0, source.ReturnEnergy ?? 0); Assert.Equal(text, source.CardEffect);
        Assert.Contains(ReviewedRuneCardSources.GetPhysicalPrints(), c => c.Id == id && c.CardNo == no);
        Assert.Contains(ReviewedRuneCardSources.GetPrintedResourceCostPrints(), c => c.Id == id && c.CardNo == no);
        Assert.False(ScopedOfficialCardSources.TryGet(no, out _));

        var physical = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", no, "P1");
        Assert.Equal(no, physical.CardNo); Assert.Equal(mana, physical.ManaCost); Assert.Equal(might, physical.Power);
        Assert.Contains(CardObjectTags.UnitCard, physical.Tags); Assert.Contains("COLOR:" + trait, physical.Tags);
        var cost = PrintedPowerCostRules.ForCard(no); Assert.Equal(0, cost.Amount); Assert.Equal([trait], cost.Traits);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
        var state = new MatchState("REVIEWED-RUNE-SOURCE-ONLY", 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(100, 100), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = physical },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["CARD"] = new("P1", "HAND") });
        var result = await new CoreRuleEngine().ResolveAsync(state, new("unregistered", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", no, []), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task NewScopedSnapshotDoesNotAlterOldSourceCountsOrInferPrintedAliases()
    {
        Assert.Equal("a86e10ea59a9ddd8b2794a41f1dd8dce946f0b032ae1976532bf518e7c6d8054", ReviewedRuneCardSources.SnapshotSha256);
        Assert.Equal(3, ReviewedRuneCardSources.GetAll().Count);
        Assert.Equal(3, ReviewedRuneCardSources.GetPhysicalPrints().Count);
        Assert.Equal(3, ReviewedRuneCardSources.GetPrintedResourceCostPrints().Count);
        Assert.Equal(14, ScopedOfficialCardSources.GetAll().Count);
        Assert.Equal(13, ScopedOfficialCardSources.GetPhysicalPrints().Count);
        Assert.Equal(14, ScopedOfficialCardSources.GetPrintedResourceCostPrints().Count);
        Assert.Equal(1089, (await OfficialCardCatalog.LoadDefaultAsync()).Cards.Count);
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        var create = factory.GetMethod("TryCreate", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var alias in new[] { "VEN·005/166", "VEN·006/166", "VEN-005/166", "RAD·092", "VEN·005 ", " 巴凯遗民" })
        {
            Assert.False(ReviewedRuneCardSources.TryGet(alias, out _));
            Assert.False((bool)create.Invoke(null, ["CARD", "P1", alias, null])!);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(alias, out _));
        }
    }

    [Fact]
    public async Task ActualPriorSourceImplementationCannotResumeAsTheNewReviewedInput()
    {
        var current = RuleInputIdentity.Current;
        Assert.Equal("d0164efbc531aa9879094ea0a6bd7b891b4a5e3f3f184ae517bfadb231d7aa7f", current.AuthoritySha256);
        Assert.Equal("e778e6e29472fbaccee0066f01a5f53c5f8191fd228a29f59073733b1a853682", current.CatalogSha256);
        // Captured from the actual cb56 source31 test host; never rewrite its history.
        var prior = new RuleInputIdentity(1, "CN", "zh-CN", current.AuthoritySha256, current.CatalogSha256,
            "d03e3d1558db94b23e33ba80c1953393287c0f8c5a0a56331bf1ad296d567152");
        Assert.Equal("49d14bbcba740dbf64cbe2b541e1a0c7877e23573f9bfcd01cccf6856297c09d", prior.IdentitySha256);
        Assert.NotEqual(prior.ImplementationSha256, current.ImplementationSha256);
        var state = MatchReplayInitialStateBuilder.FromSeats("prior-rune-source", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" })
            with { RuleInputs = prior };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("old-source", "P1", "PASS"), new PassCommand(), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.RecoveryInconsistent, result.ErrorCode); Assert.Empty(result.Events);
        Assert.Throws<MatchSessionException>(() => new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [], state, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
    }
}
