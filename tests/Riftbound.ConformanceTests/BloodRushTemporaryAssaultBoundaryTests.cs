using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BloodRushTemporaryAssaultBoundaryTests
{
    private const string No = BloodRushTemporaryAssaultTests.No;
    private const string Grant = "COMBAT_KEYWORD:强攻2:";

    [Theory]
    [InlineData("ALLY", false)] [InlineData("ALLY", true)]
    [InlineData("ENEMY", false)] [InlineData("ENEMY", true)]
    public async Task OrdinaryAndEchoGrantsBothExpireAndNeitherChangesANonattackingUnitsMight(string target, bool echo)
    {
        var initial = Neutral(); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", No, [target], OptionalCosts: echo ? ["ECHO"] : []));
        Assert.Single(driver.State.StackItems); await driver.Top();
        Assert.Equal(echo ? 18 : 19, driver.State.RunePools["P1"].Mana); Assert.Equal(10, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(echo ? 2 : 1, driver.State.CardObjects[target].UntilEndOfTurnEffects.Count(e => e.StartsWith(Grant, StringComparison.Ordinal)));
        Assert.Equal(4, driver.State.CardObjects[target].Power);
        Assert.Equal(4, CurrentPowerTargetTests.Snapshot(driver.State).GetProperty("players").GetProperty(target == "ALLY" ? "P1" : "P2")
            .GetProperty("objects").GetProperty(target).GetProperty("effectivePower").GetInt32());
        await driver.Act("P1", new EndTurnCommand()); Assert.Empty(driver.State.CardObjects[target].UntilEndOfTurnEffects);
        Assert.Equal(4, driver.State.CardObjects[target].Power); await driver.Replay();
        BloodRushTemporaryAssaultTests.Export($"expiry-{target}-{echo}", initial, driver);
    }

    [Fact]
    public async Task OneRealCounterCancelsBothEffectsOfTheSinglePaidEchoStackItem()
    {
        // Current CN DOC-14: paid Echo repeats the same stack effect; a counter
        // cancels both executions rather than requiring a second counter target.
        var initial = AttackDamageDistributionTests.Spell(Neutral(), "COUNTER", "OGN·045/298", "P2"); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], OptionalCosts: ["ECHO"]));
        var item = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand()); await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item.StackItemId]));
        await driver.Top(); Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(4, driver.State.CardObjects["ALLY"].Power); Assert.Equal(18, driver.State.RunePools["P1"].Mana);
        Assert.Single(driver.State.PlayerZones["P1"].Graveyard, id => id == "SPELL");
        await driver.Replay(); BloodRushTemporaryAssaultTests.Export("echo-counter-both", initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualReturnSeparatesBothEchoGrantsFromANewIncarnation(bool before)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Neutral(), "RETURN", "OGN·104/298"); var driver = new Driver(initial);
        var generation = initial.CardObjects["ALLY"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], OptionalCosts: ["ECHO"]));
        if (!before) { await driver.Top(); Assert.Equal(2, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count); }
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await driver.Top();
        if (before) await driver.Top();
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Hand); Assert.True(driver.State.CardObjects["ALLY"].ObjectGeneration > generation);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects); Assert.Equal(4, driver.State.CardObjects["ALLY"].Power);
        Assert.Single(driver.State.PlayerZones["P1"].Graveyard, id => id == "SPELL"); await driver.Replay();
        BloodRushTemporaryAssaultTests.Export("echo-return-" + before, initial, driver);
    }

    [Theory]
    [InlineData("echo-mana")] [InlineData("duplicate-echo")] [InlineData("unknown-echo")]
    [InlineData("priority")] [InlineData("source-graveyard")]
    public async Task InvalidEchoSelectionsAndSwiftTimingCannotPartlyPayMoveOrGrant(string branch)
    {
        var initial = Neutral(); string[] costs = ["ECHO"];
        if (branch == "echo-mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Red] = 10 }) } };
        if (branch == "duplicate-echo") costs = ["ECHO", "ECHO"];
        if (branch == "unknown-echo") costs = ["ECHO:UNKNOWN"];
        if (branch == "priority") {
            initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "PENDING", "OGN·048/298");
            var opened = await new CoreRuleEngine().ResolveAsync(initial, new("open-response", "P1", CommandTypes.PlayCard), new PlayCardCommand("PENDING", "OGN·048/298", []), default);
            Assert.True(opened.Accepted, opened.ErrorMessage); initial = opened.State;
        }
        if (branch == "source-graveyard") initial = initial with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SPELL"] = new("P1", "GRAVEYARD") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "SPELL"] } : p.Value) };
        await OptionalEntryTargetBoundaryTests.Reject(initial, "P1", new PlayCardCommand("SPELL", No, ["ALLY"], OptionalCosts: costs));
    }

    private static MatchState Neutral()
    {
        var initial = VenFlowCombatKeywordTests.Position(false);
        return initial with { RoomId = "BLOOD-RUSH-ASSAULT-BOUNDARY", CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", No, "P1") } };
    }
}
