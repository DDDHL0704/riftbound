using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenTokenPrintingTests
{
    [Theory]
    [InlineData("VEN·T02", 37922, "indicator_equipment", "SFD·T03")]
    [InlineData("VEN·T03", 37923, "indicator_unit", "SFD·T01")]
    public async Task ExactOfficialTokenPrintsReuseTheExistingFactories(string no, long id, string category, string existing)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(category, card.CardCategory);
        Assert.Contains("VEN(标准补充包)", card.ProductCodeList);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory));
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(existing, out var original));
        Assert.Equal(original.TokenFamilyName, factory.TokenFamilyName);
        Assert.Equal(original.DefaultPower, factory.DefaultPower);
        Assert.Equal(original.Tags, factory.Tags);
    }

    [Fact]
    public async Task VenGoldPaysItsReactionResourceCostAndVanishesAfterDestruction()
    {
        var state = VenUnitEntryBasicTests.Position("VEN·048");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T02", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = factory.CreateObject("GEAR", "P1", "P1") } };
        var game = new Driver(state); var before = state.RunePools["P1"].TotalPower;
        await game.Act("P1", new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId, []));
        Assert.Equal(before + 1, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }
}
