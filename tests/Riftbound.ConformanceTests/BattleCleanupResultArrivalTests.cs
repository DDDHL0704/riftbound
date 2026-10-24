using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattleCleanupResultArrivalTests
{
    [Fact]
    public async Task ARealReactionArrivalDuringDeathFeprParticipatesInTheLaterOutcome()
    {
        var initial = BattleCleanupDeathObserverTimingTests.Opening(true);
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var zones = new Dictionary<string, PlayerZones>(initial.PlayerZones);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        cards["LATE"] = OfficialGarenConquestBattlefieldCountTests.Printed("LATE", "OGN·241/298", "P1");
        locations["LATE"] = new("P1", "HAND");
        var funds = Enumerable.Range(1, 4).Select(i => "P1-FUND-" + i).ToArray();
        foreach (var id in funds)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", "P1");
            locations[id] = new("P1", "BASE");
        }
        zones["P1"] = zones["P1"] with { Hand = [..zones["P1"].Hand, "LATE"], Base = [..zones["P1"].Base, ..funds] };
        initial = initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var run = await BattleCleanupDeathObserverTimingTests.Fight(initial);
        Assert.Equal("CLEANUP_TRIGGERS", run.State.PendingBattleResult!.Stage);
        Assert.Null(run.State.PendingBattleResult.WinnerPlayerId);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
        var rejected = JsonSerializer.SerializeToElement(new { cmdType = "END_TURN",
            promptId = ResolutionResult.BuildPrompts(run.State)["P2"].PromptId, snapshotTick = run.State.Tick });
        await run.ActRetained(new("P2", "c12-cannot-end-during-death-fepr", rejected), false);
        Assert.Equal("P1", run.State.PriorityPlayerId);
        await run.Act("P1", new TapRuneCommand(funds[0], funds.Take(3).ToArray()));
        await run.Act("P1", new RecycleRuneCommand(funds[3]));
        await run.Act("P1", new PlayCardCommand("LATE", "OGN·241/298", [], Destination: "BATTLEFIELD:HOME"));
        Assert.Equal("CLEANUP_TRIGGERS", run.State.PendingBattleResult!.Stage);
        Assert.True(run.State.CardObjects["LATE"].IsDefending);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
        await run.Top();
        Assert.Null(run.State.PendingBattleResult);
        Assert.False(run.State.BattleState.IsActive);
        Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
        Assert.Equal(0, run.State.PlayerScores["P1"]);
        Assert.Equal(0, run.State.PlayerScores["P2"]);
        Assert.Equal(7, run.State.CardObjects["OBSERVER"].Power);
        Assert.False(run.State.CardObjects["LATE"].IsDefending);
        var history = Assert.Single(run.State.BattleResolutions);
        Assert.Equal("P1", history.WinnerPlayerId);
        Assert.Contains("LATE", history.SurvivingDefenderObjectIds);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLE_NO_RESULT");
        var result = Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
        var entered = Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BATTLEFIELD"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "LATE"));
        Assert.True(entered.Sequence < result.Sequence);
        Assert.True(result.Sequence < Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_CLOSED").Sequence);
        await run.Replay();
        BattleCleanupDeathObserverTimingTests.Save("late-defender-before-outcome", initial, run);
    }
}
