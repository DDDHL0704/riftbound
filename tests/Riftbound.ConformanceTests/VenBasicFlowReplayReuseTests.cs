using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Sdk;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenBasicFlowReplayReuseTests
{
    [Fact]
    public async Task UnchangedInputsReuseTheAuditWhileBatchedCommandsStillNeedOne()
    {
        var game = new Driver(VenUnitEntryBasicTests.Position("OGN·096/298")) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·096/298", []));
        Assert.Equal(0, game.ReplayAuditCount);
        await game.Replay(); Assert.Equal(1, game.ReplayAuditCount);
        await game.Replay(); Assert.Equal(1, game.ReplayAuditCount);
        await game.ColdCheckpoint(); Assert.Equal(1, game.ReplayAuditCount);
        game.RecoverAfterEveryCommand = true;
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(2, game.ReplayAuditCount);
        await game.Replay(); Assert.Equal(2, game.ReplayAuditCount);
    }

    [Theory]
    [InlineData("FINAL_STATE")]
    [InlineData("INITIAL_STATE")]
    [InlineData("EVENT_PAYLOAD")]
    [InlineData("COMMAND_JOURNAL")]
    public async Task ChangedInputsCannotBorrowAnEarlierSuccessfulAudit(string attack)
    {
        var initial = VenUnitEntryBasicTests.Position("OGN·096/298");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·096/298", []));
        var tick = game.State.Tick;
        Assert.Equal(1, game.ReplayAuditCount);
        if (attack is "FINAL_STATE" or "INITIAL_STATE")
        {
            Assert.NotSame(initial.RunePools, game.State.RunePools);
            var state = attack == "INITIAL_STATE" ? initial : game.State;
            var pools = Assert.IsAssignableFrom<IDictionary<string, RunePool>>(state.RunePools);
            pools["P1"] = pools["P1"] with { Mana = pools["P1"].Mana + 1 };
        }
        else if (attack == "EVENT_PAYLOAD")
        {
            var payload = Assert.IsAssignableFrom<IDictionary<string, object?>>(game.Events()[0].Event.Payload);
            payload["fixtureReplayForgery"] = true;
        }
        else
        {
            var command = Assert.Single(game.Commands());
            await game.RecordAsync(new MatchJournalEntry(game.State.RoomId, command.PlayerId,
                command.ClientIntentId + "-forged", command.CommandType, command.RawCommand,
                command.StartedTick, command.CompletedTick, command.StartedEventSequence,
                command.CompletedEventSequence, true, null, game.State, [],
                ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State),
                DateTimeOffset.UnixEpoch) { TypedCommand = command.TypedCommand,
                ErrorCode = command.ErrorCode, RuleInputs = command.RuleInputs }, default);
        }
        Assert.Equal(tick, game.State.Tick);
        await Assert.ThrowsAnyAsync<XunitException>(() => game.Replay());
        Assert.Equal(2, game.ReplayAuditCount);
        await Assert.ThrowsAnyAsync<XunitException>(() => game.Replay());
        Assert.Equal(3, game.ReplayAuditCount);
    }
}
