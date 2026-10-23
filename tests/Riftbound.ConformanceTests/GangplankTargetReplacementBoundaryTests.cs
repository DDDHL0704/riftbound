using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GangplankTargetReplacementBoundaryTests
{
    [Theory]
    [InlineData("VEN·086", false)] [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)] [InlineData("VEN·181", true)]
    public async Task ActualTwoAndSixSwapLocksBothDifferencesBeforeTheSelectedNegativeHalfIsReplaced(string no, bool reversed)
    {
        var initial = Position(no);
        initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "SWAP", "SFD·145/221");
        var small = OfficialGarenConquestBattlefieldCountTests.Printed("SMALL", "OGN·210/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["SMALL"] = small },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SMALL"] = new("P1", "HAND") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [..p.Value.Hand, "SMALL"] } : p.Value) };
        var driver = await Empower(initial, no);
        await driver.Act("P1", new PlayCardCommand("SMALL", "OGN·210/298", [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(2, driver.State.CardObjects["SMALL"].Power); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        await driver.Act("P1", new PlayCardCommand("SWAP", "SFD·145/221", reversed ? ["UNIT", "SMALL"] : ["SMALL", "UNIT"])); await driver.Top();
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export($"swap-{no}-{reversed}", initial, driver,
            new { expectedSmall = 6, expectedEmpowered = 9, sourcePowers = new[] { 2, 6 }, reversed });
        Assert.Equal(6, driver.State.CardObjects["SMALL"].Power); Assert.Equal(9, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(4, Assert.Single(driver.State.CardObjects["SMALL"].UntilEndOfTurnPowerModifiers).PowerDelta);
        Assert.Equal(3, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(2, driver.State.CardObjects["SMALL"].Power); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ARealEarlierStunMakesTheLaterChosenStunANonActionAfterEmpower(string no)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Position(no), "FIRST-STUN", "OGN·050/298");
        initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "SECOND-STUN", "OGN·050/298");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await driver.Act("P1", new PlayCardCommand("FIRST-STUN", "OGN·050/298", ["UNIT"])); await driver.Top();
        Assert.Contains("STUNNED", driver.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top();
        var beforeEvents = driver.Events().Length;
        await driver.Act("P1", new PlayCardCommand("SECOND-STUN", "OGN·050/298", ["UNIT"])); await driver.Top();
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("already-stunned-" + no, initial, driver,
            new { expectedMight = 6, expectedNewStunEvents = 0, actualNewStunEvents = driver.Events().Skip(beforeEvents).Count(e =>
                e.Event.Kind == "STATUS_EFFECT_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("effectId"), "STUNNED")) });
        Assert.Equal(6, driver.State.CardObjects["UNIT"].Power); Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(driver.Events().Skip(beforeEvents), e => e.Event.Kind == "STATUS_EFFECT_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("effectId"), "STUNNED"));
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task RealModifiersReachOneBeforeEmpowerAndTheLaterFloorZeroDoesNotBecomePlusThree(string no)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Position(no), "SMOKE", "OGN·093/298");
        initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "LOWER", "OGN·095/298");
        initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "ZERO", "OGN·095/298");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await driver.Act("P1", new PlayCardCommand("SMOKE", "OGN·093/298", ["UNIT"])); await driver.Top();
        Assert.Equal(2, driver.State.CardObjects["UNIT"].Power);
        await driver.Act("P1", new PlayCardCommand("LOWER", "OGN·095/298", ["UNIT"])); await driver.Top(); Assert.Equal(1, driver.State.CardObjects["UNIT"].Power);
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top();
        var deckBefore = driver.State.PlayerZones["P1"].MainDeck.Count; var beforeEvents = driver.Events().Length;
        await driver.Act("P1", new PlayCardCommand("ZERO", "OGN·095/298", ["UNIT"])); await driver.Top();
        Assert.Equal(1, driver.State.CardObjects["UNIT"].Power); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.DoesNotContain(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers, p => p.PowerDelta == 3);
        Assert.Equal(deckBefore - 1, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Single(driver.Events().Skip(beforeEvents), e => e.Event.Kind == "CARD_DRAWN");
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("minimum-zero-" + no, initial, driver, new { expectedMight = 1, independentDraws = 1 });
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ARealChosenPositiveModifierKeepsItsOriginalSixPointInstruction(string no)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Position(no), "POSITIVE", "VEN·081"); var driver = await Empower(initial, no);
        await driver.Act("P1", new PlayCardCommand("POSITIVE", "VEN·081", ["UNIT"])); await driver.Top();
        Assert.Equal(12, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(6, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("positive-control-" + no, initial, driver, new { expectedMight = 12, originalDelta = 6 });
    }

    private static MatchState Position(string no)
    {
        var state = GangplankTargetReplacementConsumerTests.Position(no, "P1", "negative");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = state.RunePools["P1"] with { PowerByTrait = new Dictionary<string, int>(state.RunePools["P1"].PowerByTrait) { [RuneTrait.Purple] = 4 } } } };
    }
    private static async Task<Driver> Empower(MatchState initial, string no)
    {
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top(); return driver;
    }
}
