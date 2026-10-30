using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DiscardDestroyTriggerParserTests
{
    private const string OfficialText = "当你打出我时，或当我进攻时，你可以选择弃置一张手牌，以此摧毁一个单位或装备。";

    [Theory]
    [InlineData("RAD·137/167", 39643)]
    [InlineData("RAD·137a/167", 39644)]
    public void BothOfficialPrintingsKeepTheOptionalDiscardCostAndPermanentTarget(string cardNo, int id)
    {
        using var stream = typeof(OfficialCardCatalog).Assembly.GetManifestResourceStream(
            "Riftbound.CardCatalog.VendettaSourceSnapshot");
        Assert.NotNull(stream);
        using var doc = JsonDocument.Parse(stream);
        var row = Assert.Single(doc.RootElement.GetProperty("cards").EnumerateArray(),
            row => row.GetProperty("cardNo").GetString() == cardNo && row.GetProperty("id").GetInt32() == id);
        Assert.Equal(OfficialText, row.GetProperty("cardEffect").GetString());
        AssertDiscardDestroy(row.GetProperty("cardEffect").GetString()!);
    }

    [Fact]
    public void TheSharedParserDoesNotDependOnTheCardsNameOrPrinting()
        => AssertDiscardDestroy(OfficialText);

    [Theory]
    [InlineData("当你打出我时，或当我进攻时，你可以选择弃置一张手牌，以此抽一张牌。")]
    [InlineData("当你打出我时，或当我进攻时，摧毁一个单位或装备。")]
    [InlineData("当你打出我时，或当我进攻时，你可以选择弃置两张手牌，以此摧毁一个单位或装备。")]
    [InlineData("当我防守时，你可以选择弃置一张手牌，以此摧毁一个单位或装备。")]
    public void OtherEffectsAndCostsCannotAcquireTheDiscardDestroyBehavior(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text),
            trigger => trigger.Kind == TriggerKinds.UnitPlayedOrAttacksOptionalDiscardDestroy);

    private static void AssertDiscardDestroy(string text)
    {
        var trigger = Assert.Single(TriggerParser.Parse(text),
            trigger => trigger.Kind == TriggerKinds.UnitPlayedOrAttacksOptionalDiscardDestroy);
        Assert.Equal(TriggerTimings.SourceUnitPlayedOrAttack, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.UnitOrEquipmentOnField, trigger.TargetScope);
        Assert.True(trigger.Optional);
        Assert.Equal(1, trigger.DiscardCount);
        Assert.Equal(TriggerZones.Hand, trigger.DiscardSourceZone);
        Assert.Equal(TriggerZones.Graveyard, trigger.DiscardDestinationZone);
        Assert.Equal(1, trigger.DestroyCount);
        Assert.Null(trigger.ManaCost);
        Assert.Null(trigger.PowerCost);
        Assert.Null(trigger.ExhaustsSource);
    }
}
