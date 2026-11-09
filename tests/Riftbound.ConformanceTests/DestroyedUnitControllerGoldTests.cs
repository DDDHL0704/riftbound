using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DestroyedUnitControllerGoldTests
{
    [Theory]
    [InlineData("P1", "P1", 2)] [InlineData("P2", "P1", 2)]
    [InlineData("P1", "P2", 1)] [InlineData("P2", "P2", 1)]
    public async Task BloodMoneyUsesTheControllerImmediatelyBeforeDestructionAndReturnsTheCardToItsOwner(string owner, string controller, int expected)
    {
        const string money = "SFD·162/221";
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create(), "TARGET", "OGN·010/298", controller, "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET"] = initial.CardObjects["TARGET"] with { OwnerId = owner },
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = controller } } };
        var game = new Driver(AttackDamageDistributionTests.Spell(initial, "MONEY", money, "P1"));
        await game.Act("P1", new PlayCardCommand("MONEY", money, ["TARGET"])); await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones[owner].Graveyard);
        var gold = game.State.CardObjects.Values.Where(card => card.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币").ToArray();
        Assert.Equal(expected, gold.Length);
        Assert.All(gold, card => { Assert.Equal("P1", card.OwnerId); Assert.Equal("P1", card.ControllerId);
            Assert.Contains(card.ObjectId, game.State.PlayerZones["P1"].Base); Assert.True(card.IsExhausted); });
        await game.Replay();
    }
}
