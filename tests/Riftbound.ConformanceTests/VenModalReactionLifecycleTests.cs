using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenModalReactionLifecycleTests
{
    [Fact]
    public async Task SeparateCopiesSubtractTwoEachAndBothTemporaryChangesExpire()
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(VenModalReactionTests.Position(), "SECOND", "VEN·052");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·052", ["ENEMY"], Mode: "ENEMY_UNIT_POWER_MINUS_2"));
        await driver.Top(); Assert.Equal(2, driver.State.CardObjects["ENEMY"].Power);
        await driver.Act("P1", new PlayCardCommand("SECOND", "VEN·052", ["ENEMY"], Mode: "ENEMY_UNIT_POWER_MINUS_2"));
        await driver.Top(); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(2, driver.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers.Count);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Empty(driver.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers);
        await driver.Replay(); VenModalReactionTests.Export("two-separate-reductions", initial, driver);
    }

    [Fact]
    public async Task ReducingAnAlreadyWoundedUnitToItsNonzeroDamageDestroysIt()
    {
        var initial = VenModalReactionTests.Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Damage = 2 } } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·052", ["ENEMY"], Mode: "ENEMY_UNIT_POWER_MINUS_2"));
        await driver.Top();
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("ENEMY", driver.State.PlayerZones["P2"].Battlefields);
        await driver.Replay(); VenModalReactionTests.Export("wounded-enemy-destruction", initial, driver);
    }

    [Fact]
    public async Task AReturnedFriendlyTokenCeasesInsteadOfPersistingInAHand()
    {
        var initial = VenModalReactionTests.Position();
        var token = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·271/298", "P1");
        Assert.True(TokenObjectRules.IsToken(token)); Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ALLY"] = token } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·052", ["ALLY"], Mode: "RETURN_FRIENDLY_UNIT"));
        await driver.Top();
        Assert.False(driver.State.CardObjects.ContainsKey("ALLY")); Assert.False(driver.State.ObjectLocations.ContainsKey("ALLY"));
        Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Base);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        await driver.Replay(); VenModalReactionTests.Export("friendly-token-ceases", initial, driver);
    }
}
