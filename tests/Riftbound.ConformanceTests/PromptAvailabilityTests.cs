using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PromptAvailabilityTests
{
    [Fact]
    public async Task PromptScopedCommandMissingFromEnabledCandidatesIsRejectedWithoutMutation()
    {
        var state = new MatchState(
            roomId: "PROMPT-AVAILABILITY",
            tick: 7,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            });
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var prompt = session.PromptFor("P1");
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            candidate.Action == CommandTypes.EndTurn && candidate.Enabled);
        Assert.DoesNotContain(prompt.Candidates ?? [], candidate =>
            candidate.Action == CommandTypes.MoveUnit && candidate.Enabled);

        var command = new MoveUnitCommand("missing-unit", "BASE", "BATTLEFIELD:missing", []);
        var raw = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.MoveUnit,
            sourceObjectId = command.SourceObjectId,
            origin = command.Origin,
            destination = command.Destination,
            optionalCosts = Array.Empty<string>(),
            sourceObjectIds = Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var before = MatchStateHasher.Hash(state);

        var rejected = await session.SubmitAsync(
            "P1",
            "missing-prompt-action",
            command,
            raw,
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("当前操作未在行动提示中提供，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }
}
