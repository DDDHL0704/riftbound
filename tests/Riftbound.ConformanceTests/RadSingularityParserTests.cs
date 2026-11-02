using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadSingularityParserTests
{
    private const string Draw = "当你打出一张卡牌时，抽两张牌。";
    private const string Threshold = "当你抽至少一张牌且有至少九张其他手牌时，弃置你的手牌、摧毁此牌、并选择一名对手。该对手获得1分。";

    [Fact]
    public void OfficialPrintingKeepsTwoSeparateConditionsAndTheOrderedConsequence()
    {
        using var stream = typeof(OfficialCardCatalog).Assembly.GetManifestResourceStream(
            "Riftbound.CardCatalog.VendettaSourceSnapshot");
        Assert.NotNull(stream);
        using var doc = JsonDocument.Parse(stream);
        var card = Assert.Single(doc.RootElement.GetProperty("cards").EnumerateArray(),
            row => row.GetProperty("id").GetInt32() == 39568);
        Assert.Equal("RAD·069/167", card.GetProperty("cardNo").GetString());
        var text = card.GetProperty("cardEffect").GetString()!;
        Assert.Equal(Draw + " \n" + Threshold, text);
        var triggers = TriggerParser.Parse(text);
        var draw = Assert.Single(triggers, t => t.Kind == TriggerKinds.ControllerCardPlayDraw);
        Assert.Equal("CARD_PLAY_COMPLETED", draw.Timing);
        Assert.Equal(2, draw.DrawCount);
        Assert.Null(draw.MinimumOtherHandCount);
        var threshold = Assert.Single(triggers, t => t.Kind == TriggerKinds.ControllerDrawHandThresholdDiscardDestroyScore);
        Assert.Equal("CARDS_DRAWN", threshold.Timing);
        Assert.Equal(1, threshold.MinimumDrawCount);
        Assert.Equal(9, threshold.MinimumOtherHandCount);
        Assert.Equal(TriggerTargetScopes.OpponentPlayer, threshold.TargetScope);
        Assert.True(threshold.DiscardsAllControllerHand);
        Assert.Equal(TriggerZones.Hand, threshold.DiscardSourceZone);
        Assert.Equal(TriggerZones.Graveyard, threshold.DiscardDestinationZone);
        Assert.True(threshold.DestroysSource);
        Assert.Equal(1, threshold.DestroyCount);
        Assert.Equal(1, threshold.ScoreAmount);
        Assert.Null(threshold.DrawCount);
        Assert.Null(threshold.ManaCost);
        Assert.Null(threshold.PowerCost);
    }

    [Theory]
    [InlineData("三", 3)] [InlineData("4", 4)]
    public void SuccessfulPlayDrawCountComesFromTheInstruction(string number, int count)
        => Assert.Equal(count, Assert.Single(TriggerParser.Parse(Draw.Replace("两", number)),
            t => t.Kind == TriggerKinds.ControllerCardPlayDraw).DrawCount);

    [Theory]
    [InlineData("当你从你的手牌以外的位置打出一张卡牌时，抽两张牌。")]
    [InlineData("每回合首次，当你打出一张卡牌时，抽两张牌。")]
    [InlineData("当对手打出一张卡牌时，抽两张牌。")]
    [InlineData("当你打出一张卡牌时，你可以选择抽两张牌。")]
    [InlineData("当你打出一张卡牌时，抽0张牌。")]
    [InlineData("当你打出一张卡牌时，抽两张牌。然后弃置一张牌。")]
    public void OtherTimingOptionalityAndFollowingInstructionsDoNotAcquireAnUnconditionalDraw(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.ControllerCardPlayDraw);

    [Theory]
    [InlineData("九", "8", 8)] [InlineData("九", "十", 10)]
    public void HandThresholdIsAParameterRatherThanAPrintingShortcut(string from, string to, int expected)
        => Assert.Equal(expected, Assert.Single(TriggerParser.Parse(Threshold.Replace(from, to)),
            t => t.Kind == TriggerKinds.ControllerDrawHandThresholdDiscardDestroyScore).MinimumOtherHandCount);

    [Theory]
    [InlineData("其他手牌", "手牌")]
    [InlineData("抽至少一张", "抽至少0张")]
    [InlineData("九张", "0张")]
    [InlineData("弃置你的手牌", "弃置一张手牌")]
    [InlineData("摧毁此牌", "摧毁一件装备")]
    [InlineData("一名对手", "一名玩家")]
    [InlineData("1分", "0分")]
    public void DifferentConditionsAndConsequencesCannotAcquireTheCompositeBehavior(string from, string to)
        => Assert.DoesNotContain(TriggerParser.Parse(Threshold.Replace(from, to)),
            t => t.Kind == TriggerKinds.ControllerDrawHandThresholdDiscardDestroyScore);

    [Fact]
    public async Task ImplementedPrintingUsesTheExactOfficialIdentity()
        => Assert.Equal(39568, Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == "RAD·069/167").Id);
}
