using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GangplankTargetReplacementSelectionTests
{
    [Theory]
    [InlineData("VEN·086", "P1")] [InlineData("VEN·086", "P2")]
    [InlineData("VEN·181", "P1")] [InlineData("VEN·181", "P2")]
    public async Task ARealPlayedEquipmentAbilitySelectsTheUnitAndKeepsItsExhaustPayment(string no, string caster)
    {
        var initial = Add(GangplankTargetReplacementConsumerTests.Position(no, caster, "negative"), "ORB", "OGN·090/298", caster);
        var driver = await Empower(initial, no);
        if (caster == "P2") await FundSecondTurn(driver);
        await driver.Act(caster, new PlayCardCommand("ORB", "OGN·090/298", [], Destination: "BASE"));
        Assert.Contains(CardObjectTags.EquipmentCard, driver.State.CardObjects["ORB"].Tags);
        Assert.False(driver.State.CardObjects["ORB"].IsExhausted);
        await driver.Act(caster, new ActivateAbilityCommand("ORB", OrbOfRegretTests.Ability, ["UNIT"]));
        Assert.True(driver.State.CardObjects["ORB"].IsExhausted); Assert.Single(driver.State.StackItems);
        await driver.Top(); await driver.Replay();
        GangplankTargetReplacementConsumerTests.Export($"chosen-skill-{no}-{caster}", initial, driver,
            new { expectedMight = 9, sourceStillExhausted = true, expectedDelta = 3 });
        Assert.Equal(9, driver.State.CardObjects["UNIT"].Power); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(3, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        Assert.True(driver.State.CardObjects["ORB"].IsExhausted);
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ARealAllObjectReturnDoesNotPretendToHaveChosenTheEmpoweredUnit(string no)
    {
        var initial = Add(GangplankTargetReplacementConsumerTests.Position(no, "P1", "negative"), "ALL-RETURN", "SFD·147/221", "P1");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = initial.RunePools["P1"] with { PowerByTrait = new Dictionary<string, int>(initial.RunePools["P1"].PowerByTrait) { [RuneTrait.Purple] = 4 } } } };
        var driver = await Empower(initial, no); var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("ALL-RETURN", "SFD·147/221", [])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > generation);
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("unchosen-all-return-" + no, initial, driver,
            new { expectedReturned = true, expectedMight = 6, expectedEmpowered = false });
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task AnActualPlayedUnitsAllEnemyReductionIsNotAnExplicitUnitSelection(string no)
    {
        var initial = Add(GangplankTargetReplacementConsumerTests.Position(no, "P2", "negative"), "WATCHER", "OGN·116/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["P2-MANA-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-MANA-1", "UNL-R03", "P2") } };
        var driver = await Empower(initial, no); await FundSecondTurn(driver);
        await driver.Act("P2", new RecycleRuneCommand("P2-MANA-1"));
        await driver.Act("P2", new PlayCardCommand("WATCHER", "OGN·116/298", [], Destination: "BASE"));
        while (driver.State.StackItems.Count > 0) await driver.Top();
        Assert.Equal(3, driver.State.CardObjects["UNIT"].Power); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(-3, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("unchosen-all-enemy-" + no, initial, driver,
            new { expectedMight = 3, expectedEmpowered = true, expectedDelta = -3 });
    }

    private static MatchState Add(MatchState state, string id, string no, string player)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "HAND") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == player ? p.Value with { Hand = [..p.Value.Hand, id] } : p.Value) };

    private static async Task<Driver> Empower(MatchState state, string no)
    {
        var driver = new Driver(state); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top(); return driver;
    }
    private static async Task FundSecondTurn(Driver driver)
    {
        await driver.Act("P1", new EndTurnCommand()); var runes = Enumerable.Range(1, 8).Select(i => "P2-MANA-" + i).ToArray();
        await driver.Act("P2", new TapRuneCommand(runes[0], runes));
    }
}
