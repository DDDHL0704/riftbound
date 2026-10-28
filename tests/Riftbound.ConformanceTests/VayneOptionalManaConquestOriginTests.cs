using System.Reflection;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VayneOptionalManaConquestOriginTests
{
    [Fact]
    public async Task ExistingOptionalManaFixtureReachesTheVaynePaymentBeforeForgeryChecks()
    {
        var position = typeof(OptionalManaTriggerPromptTests).GetMethod("Position", BindingFlags.NonPublic | BindingFlags.Static)!;
        var initial = Assert.IsType<MatchState>(position.Invoke(null, ["vayne"]));
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_VAYNE_OPTIONAL_PROBE");
        var driver = new StandbyDefendObserverTests.Driver(initial, evidence);
        driver.Export("initial.json", initial);
        var move = await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        driver.Export("after-move.json", new { driver.State, move.Events });
        var first = await driver.Act("P1", new PassFocusCommand());
        driver.Export("after-first-pass.json", new { driver.State, first.Events });
        var second = await driver.Act("P2", new PassFocusCommand());
        driver.Export("after-second-pass.json", new { driver.State, lastEvents = second.Events, commands = driver.Commands(), journalEvents = driver.Events() });
        Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
    }
    [Fact]
    public async Task SameVayneFixtureWithAnExplicitBattlefieldLocationReachesPayment()
    {
        var position = typeof(OptionalManaTriggerPromptTests).GetMethod("Position", BindingFlags.NonPublic | BindingFlags.Static)!;
        var initial = Assert.IsType<MatchState>(position.Invoke(null, ["vayne"]));
        initial = initial with { ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
            ["BF"] = new("P2", "BATTLEFIELD", "BF") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Equal(new[] { "SPEND_MANA:1", "DECLINE" }, payment.LegalPaymentChoiceIds);
    }

}
