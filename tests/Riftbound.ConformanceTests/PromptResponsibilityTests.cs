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
        var p1Responsibility = prompts["P1"].View!.Responsibility!;
        var p2Responsibility = prompts["P2"].View!.Responsibility!;

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
    }
}
