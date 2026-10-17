using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

// CN printing UNL-003/219 / OGN·067/298: the play destination is a trigger
// condition. These tests do not certify the damage/pull instructions themselves.
public sealed class BattlefieldConditionalPlayTriggerTests
{
    [Theory]
    [InlineData("UNL-003/219", "")]
    [InlineData("UNL-003/219", "BASE")]
    [InlineData("OGN·067/298", "")]
    [InlineData("OGN·067/298", "BASE")]
    public async Task BasePlacementDoesNotQueueTheBattlefieldOnlyPrintedAbility(string cardNo, string destination)
    {
        var initial = PermanentPlayTargetTests.Position(cardNo);
        var result = await Act(initial, "P1", new PlayCardCommand("UNIT", cardNo, [], Destination: destination));
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", result.State.ObjectLocations["UNIT"].Zone);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.TriggerQueue);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.DoesNotContain(result.Events, e => e.Kind == "TRIGGER_QUEUED");
        Assert.Contains(result.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE");
        Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    [Theory]
    [InlineData("UNL-012/219")]
    [InlineData("UNL-021/219")]
    [InlineData("OGN·223/298")]
    [InlineData("VEN·017")]
    public async Task BattlefieldReminderOrLaterInstructionDoesNotSuppressAnUnconditionalPlayAbility(string cardNo)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(PermanentPlayTargetTests.Position(cardNo),
            "FRIEND", "OGN·001/298", "F", "P1");
        var result = await Act(initial, "P1",
            new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Base);
        var ability = Assert.Single(result.State.StackItems);
        Assert.True(ability.SourceConfirmed);
        Assert.Contains(result.Events, e => e.Kind == "TRIGGER_QUEUED");
        Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task ActualStandbyPlacementRetainsTheBattlefieldConditionalTrigger()
    {
        var initial = await StandbySourceRulesTests.ReadyShadow("UNL-003/219");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY-HERE", "OGN·096/298", "F", "P2");
        var result = await Act(initial, "P1", new PlayCardCommand("SHADOW", "UNL-003/219", []));
        Assert.Equal("F", result.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        var ability = Assert.Single(result.State.StackItems, i => i.SourceObjectId == "SHADOW");
        Assert.True(ability.SourceConfirmed);
        Assert.Equal("BATTLEFIELD:F", ability.Destination);
        Assert.Equal("F", ability.PlayedFromStandby!.Battlefield.ObjectId);
        Assert.Contains(result.Events, e => e.Kind == "TRIGGER_QUEUED");
        Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    [Theory]
    [InlineData("UNL-003/219")]
    [InlineData("OGN·067/298")]
    public async Task IndependentLegendPlayTriggerStillResolvesWhenThePrintedBattlefieldConditionIsFalse(string cardNo)
    {
        var initial = PermanentPlayTargetTests.Position(cardNo);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: "UNL-183/219", ownerId: "P1", controllerId: "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") }
        };
        Restore(initial);
        ArmamentMightTests.PublicRecovery(initial);
        var beforePower = initial.CardObjects["HOST"].Power;
        var result = await Act(initial, "P1", new PlayCardCommand("UNIT", cardNo, ["HOST"], Destination: "BASE"));
        Assert.Single(result.State.StackItems);
        result = await Top(result.State);
        Assert.Empty(result.State.StackItems);
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Equal(beforePower + 1, result.State.CardObjects["HOST"].Power);
        Assert.Contains(result.Events, e => e.Kind == "LEGEND_TRIGGER_RESOLVED");
        Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }
}
