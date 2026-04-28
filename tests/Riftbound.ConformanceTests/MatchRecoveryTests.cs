using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MatchRecoveryTests
{
    [Fact]
    public void RecoveryValidatorAcceptsContiguousEventStreamAndCurrentPlayerViews()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(2, "TURN_BEGAN")
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-end-turn",
                "END_TURN",
                null,
                0,
                1,
                0,
                2,
                true,
                null)
        };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 1, 2),
            ["bob"] = PlayerView("bob", 1, 2)
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 2, commands, events, playerViews);

        Assert.Empty(errors);
    }

    [Fact]
    public void RecoveryValidatorRejectsEventGapsAndFutureSnapshots()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(3, "CARD_DRAWN")
        };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 1, 4)
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 3, [], events, playerViews);

        Assert.Contains(errors, error => error.Contains("event sequence gap", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("future event sequence 4", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryFrameComputesReplayTailFromEarliestPlayerSnapshot()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(2, "TURN_BEGAN"),
            RecoveredEvent(3, "CARD_DRAWN")
        };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 2, 2),
            ["bob"] = PlayerView("bob", 3, 3)
        };
        var frame = new MatchRecoveryFrame("room-a", 3, 3, [], events, playerViews, []);

        Assert.Equal(2, frame.ReplayFromEventSequence);
        Assert.Single(frame.EventsAfterReplayPoint);
        Assert.Equal(3, frame.EventsAfterReplayPoint[0].Sequence);
    }

    private static RecoveredEvent RecoveredEvent(long sequence, string kind)
    {
        return new RecoveredEvent(
            sequence,
            sequence,
            0,
            new GameEvent(kind, kind, new Dictionary<string, object?>()));
    }

    private static RecoveredPlayerView PlayerView(string playerId, long tick, long lastEventSequence)
    {
        var snapshot = new SnapshotDto(
            tick,
            1,
            "alice",
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>(),
            [],
            new Dictionary<string, object?>(),
            "NEUTRAL_OPEN");
        var prompt = new ActionPromptDto(playerId, true, "test", ["PASS"]);

        return new RecoveredPlayerView(
            playerId,
            tick,
            lastEventSequence,
            snapshot,
            tick,
            lastEventSequence,
            prompt);
    }
}
