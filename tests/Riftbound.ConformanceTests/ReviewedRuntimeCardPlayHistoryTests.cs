using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ReviewedRuntimeCardPlayHistoryTests
{
    [Theory]
    [InlineData("VEN·005")]
    [InlineData("VEN·006")]
    public async Task PaidUnitPlayIsRecordedAndBoundToRecovery(string cardNo)
    {
        var initial = VenUnitEntryBasicTests.Position(cardNo);
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", cardNo, []));

        var history = driver.State.CardPlays;
        Assert.Equal(initial.TurnNumber, history.TurnNumber);
        foreach (var stage in new[] { history.Finalized, history.Resolved })
        {
            var entry = Assert.Single(stage);
            Assert.Equal("P1", entry.PlayerId);
            Assert.Equal(cardNo, entry.CardNo);
            Assert.Equal(1, entry.Types);
            Assert.Equal("UNIT", entry.Source.ObjectId);
            Assert.Equal(initial.CardObjects["UNIT"].ObjectGeneration + 1, entry.Source.Generation);
        }
        Assert.Equal(history.Finalized[0].PlayId, history.Resolved[0].PlayId);
        await driver.Replay();

        var forged = driver.State with { CardPlays = history with { Resolved = [] } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
            new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch);
    }

    [Theory]
    [InlineData("VEN·005")]
    [InlineData("VEN·006")]
    public async Task PaidUnitPlayKeepsTheLegendTriggerRecoverable(string cardNo)
    {
        var initial = RengarUnitPlayConfirmationTests.WithLegend(VenUnitEntryBasicTests.Position(cardNo));
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", cardNo, []));
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("LEGEND", choice.SourceObjectId);
        Assert.Contains("UNIT", choice.LegalObjectIds);
        var might = driver.State.CardObjects["UNIT"].Power;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        await driver.Top();
        Assert.Equal(might + 1, driver.State.CardObjects["UNIT"].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·005", "巴凯遗民")]
    [InlineData("VEN·006", "绿洲劫掠者")]
    public async Task PrivateDeckChoiceUsesTheReviewedNameWithoutLeakingIt(string cardNo, string name)
    {
        var initial = FateWeaverDeckChoiceTests.Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT4"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT4", cardNo, "P1") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("WEAVER", FateWeaverDeckChoiceTests.Weaver, []));
        await driver.Top();
        Assert.Equal("DECK_EFFECT", Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice).ChoiceWindow);
        var prompts = ResolutionResult.BuildPrompts(driver.State);
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Assert.Contains(name, JsonSerializer.Serialize(prompts["P1"], options));
        Assert.DoesNotContain(name, JsonSerializer.Serialize(prompts["P2"], options));
    }

    [Theory]
    [InlineData("VEN·005")]
    [InlineData("VEN·006")]
    public void ReviewedPrintHasAKnownNameForIdentityComparisons(string cardNo)
    {
        var type = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.OfficialCardSourceIdentityGroups");
        Assert.NotNull(type);
        var method = type.GetMethod("SameKnownName", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        Assert.NotNull(method);
        Assert.True(Assert.IsType<bool>(method.Invoke(null, [cardNo, cardNo])));
        Assert.False(Assert.IsType<bool>(method.Invoke(null, [cardNo, cardNo + "/166"])));
    }
}
