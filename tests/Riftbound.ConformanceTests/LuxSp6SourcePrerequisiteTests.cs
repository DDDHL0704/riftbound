using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LuxSp6SourcePrerequisiteTests
{
    private const string No = "VEN·SP6";

    [Fact]
    public async Task ExactCnPromoIsARealHeroUnitAndPreservesTheExistingLuxPrinting()
    {
        var cards = (await OfficialCardCatalog.LoadDefaultAsync()).Cards;
        var card = Assert.Single(cards, c => c.CardNo == No); Assert.Equal(37921, card.Id);
        Assert.Equal("拉克丝", card.CardName); Assert.Equal("冕卫之光", card.SubTitle);
        Assert.Equal(4, card.Energy); Assert.Equal(2, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal(["yellow"], card.CardColorList); Assert.Contains("英雄单位", card.CardCategoryNameList);
        Assert.Equal("{{横置}}：{{反应}} — {{获得}}{{2}}。此法力仅能用于打出法术。", card.CardEffect);
        Assert.Equal(31593, Assert.Single(cards, c => c.CardNo == "OGS·014/024").Id);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var definition));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGS·014/024", out var existing));
        Assert.Equal(existing.EffectKind, definition.EffectKind); Assert.True(definition.PlaysSourceToBaseAsUnit);
        Assert.Equal(4, definition.ManaCost); Assert.Equal(2, definition.SourceUnitPower); Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var actual = OfficialGarenConquestBattlefieldCountTests.Printed("LUX-SP6", No, "P1");
        Assert.Equal(2, actual.Power); Assert.Equal(4, actual.ManaCost); Assert.Equal(No, actual.CardNo);
        Assert.Contains(CardObjectTags.UnitCard, actual.Tags); Assert.Contains("CARD_TYPE:HERO", actual.Tags);
        Assert.DoesNotContain(CardObjectTags.SpellCard, actual.Tags);
    }

    [Fact]
    public async Task RealPromoPlayPaysFourManaAndColdRecoversItsPrintedIdentity()
    {
        var initial = VenUnitEntryBasicTests.Position(No); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE"));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base); Assert.Equal(16, driver.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(No, driver.State.CardObjects["UNIT"].CardNo); Assert.Equal(2, driver.State.CardObjects["UNIT"].Power);
        Assert.True(driver.State.CardObjects["UNIT"].IsExhausted); await driver.Replay();
        if (Environment.GetEnvironmentVariable("RIFTBOUND_LUX_SP6_SOURCE_EVIDENCE") is { Length: > 0 } output) {
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "real-promo-play.json"), JsonSerializer.Serialize(new {
                scope = "Exact registered source and ordinary unit entry only; restricted-mana activation remains line3 S6.", initial,
                final = driver.State, commands = driver.Commands(), events = driver.Events()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }
}
