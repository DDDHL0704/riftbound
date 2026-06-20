using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PromptResponsibilityTests
{
    [Fact]
    public void MainActionPromptsExposeServerResponsibilityForBothPlayers()
    {
        var state = new MatchState(
            "prompt-responsibility-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "seat-1",
                ["P2"] = "seat-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen);

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.NotNull(prompts["P1"].View?.Responsibility);
        Assert.NotNull(prompts["P2"].View?.Responsibility);
        Assert.NotNull(prompts["P1"].ServerFlow);
        Assert.NotNull(prompts["P2"].ServerFlow);
        var p1Responsibility = prompts["P1"].View!.Responsibility!;
        var p2Responsibility = prompts["P2"].View!.Responsibility!;
        var p1Flow = prompts["P1"].ServerFlow!;
        var p2Flow = prompts["P2"].ServerFlow!;

        Assert.Equal(PromptTypes.MainAction, p1Responsibility.PromptType);
        Assert.Equal("P1", p1Responsibility.PromptPlayerId);
        Assert.Equal("P1", p1Responsibility.ResponsiblePlayerId);
        Assert.True(p1Responsibility.IsResponsiblePlayer);
        Assert.True(p1Responsibility.ActionableForPromptPlayer);
        Assert.Equal("PLAYER_ACTION", p1Responsibility.State);
        Assert.Contains("服务端候选", p1Responsibility.NextStep, StringComparison.Ordinal);

        Assert.Equal(PromptTypes.MainAction, p2Responsibility.PromptType);
        Assert.Equal("P2", p2Responsibility.PromptPlayerId);
        Assert.Equal("P1", p2Responsibility.ResponsiblePlayerId);
        Assert.False(p2Responsibility.IsResponsiblePlayer);
        Assert.False(p2Responsibility.ActionableForPromptPlayer);
        Assert.Equal("WAITING_PLAYER", p2Responsibility.State);
        Assert.Contains("等待 P1 处理主行动", p2Responsibility.NextStep, StringComparison.Ordinal);
        Assert.Equal(0, p2Responsibility.QueueCounts["stack"]);
        Assert.Equal(0, p2Responsibility.QueueCounts["triggerQueue"]);

        Assert.Equal("ready", p1Flow.State);
        Assert.Equal("可提交", p1Flow.StateLabel);
        Assert.Equal("提交给服务端", p1Flow.PrimaryLabel);
        Assert.Equal(PromptTypes.MainAction, p1Flow.PromptType);
        Assert.True(p1Flow.ActionableForPromptPlayer);
        Assert.Equal("P1", p1Flow.ResponsiblePlayerId);
        Assert.Contains(p1Flow.Lanes, lane => lane.Key == "stack" && lane.Count == 0);
        Assert.Contains(p1Flow.Steps, step => step.Key == "candidate" && step.State == "ready");

        Assert.Equal("waiting", p2Flow.State);
        Assert.Equal("等待", p2Flow.StateLabel);
        Assert.Equal("等待 P1", p2Flow.PrimaryLabel);
        Assert.False(p2Flow.ActionableForPromptPlayer);
        Assert.Equal("P1", p2Flow.ResponsiblePlayerId);
        Assert.Equal(0, p2Flow.QueueCounts["stack"]);
    }

    [Fact]
    public void ServerFlowTriggerLaneUsesSafeHeadlineOnly()
    {
        var state = new MatchState(
            "prompt-server-flow-trigger-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "seat-1",
                ["P2"] = "seat-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen) with
        {
            TriggerQueue =
            [
                new TriggerQueueItemState(
                    "trigger-hidden",
                    "P2",
                    "P2-hidden-source",
                    "SECRET_HIDDEN_STANDBY_TRIGGER",
                    "UNIT_DESTROYED")
            ]
        };

        var flow = ResolutionResult.BuildPrompts(state)["P1"].ServerFlow!;
        var triggerLane = Assert.Single(flow.Lanes, lane => lane.Key == "trigger");

        Assert.Equal(1, triggerLane.Count);
        Assert.Equal("触发等待结算", triggerLane.Headline);
        Assert.DoesNotContain("SECRET_HIDDEN_STANDBY_TRIGGER", triggerLane.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-hidden-source", triggerLane.Headline, StringComparison.Ordinal);
    }
}
