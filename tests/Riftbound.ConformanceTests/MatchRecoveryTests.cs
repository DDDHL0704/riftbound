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

    [Fact]
    public async Task RegistryHydratesRecoveredSessionWithoutTreatingPlayerSnapshotAsFullRuleState()
    {
        var frame = RecoveryFrame(
            currentTick: 2,
            lastEventSequence: 6,
            commands:
            [
                new RecoveredCommand(
                    "alice",
                    "intent-pass-priority",
                    "PASS_PRIORITY",
                    null,
                    1,
                    2,
                    5,
                    6,
                    true,
                    null)
            ]);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var session = await registry.GetOrCreateAsync("room-a", CancellationToken.None);

        var snapshot = session.SnapshotFor("alice");
        Assert.Equal(2, snapshot.Tick);
        Assert.Equal(2, snapshot.TurnNumber);
        Assert.Equal("bob", snapshot.ActivePlayerId);
        Assert.Equal("P1", PlayerSeat(snapshot, "alice"));
        Assert.Equal("P2", PlayerSeat(snapshot, "bob"));
    }

    [Fact]
    public async Task RecoveredDuplicateIntentDoesNotWriteJournalOrAdvanceTick()
    {
        var journal = new RecordingMatchJournal();
        var frame = RecoveryFrame(
            currentTick: 2,
            lastEventSequence: 6,
            commands:
            [
                new RecoveredCommand(
                    "alice",
                    "intent-pass-priority",
                    "PASS_PRIORITY",
                    null,
                    1,
                    2,
                    5,
                    6,
                    true,
                    null)
            ]);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            journal,
            new FixedRecoveryStore(frame));
        var session = await registry.GetOrCreateAsync("room-a", CancellationToken.None);

        var duplicate = await session.SubmitAsync(
            "alice",
            "intent-pass-priority",
            new PassPriorityCommand(),
            null,
            CancellationToken.None);

        Assert.True(duplicate.Accepted);
        Assert.Empty(duplicate.Events);
        Assert.Equal(2, duplicate.State.Tick);
        Assert.Empty(journal.Entries);
    }

    [Fact]
    public async Task RecoveredSessionContinuesEventSequenceForNewCommands()
    {
        var journal = new RecordingMatchJournal();
        var frame = RecoveryFrame(currentTick: 2, lastEventSequence: 6);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            journal,
            new FixedRecoveryStore(frame));
        var session = await registry.GetOrCreateAsync("room-a", CancellationToken.None);

        await session.SubmitAsync(
            "alice",
            "intent-new-pass-priority",
            new PassPriorityCommand(),
            null,
            CancellationToken.None);

        var entry = Assert.Single(journal.Entries);
        Assert.Equal(6, entry.StartedEventSequence);
        Assert.Equal(7, entry.CompletedEventSequence);
    }

    [Fact]
    public async Task RegistryRejectsInconsistentRecoveryFrame()
    {
        var frame = new MatchRecoveryFrame(
            "room-a",
            1,
            1,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            ["event stream is empty but match last event sequence is 1"]);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("match recovery is inconsistent", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameForAnotherRoom()
    {
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(RecoveryFrame(currentTick: 1, lastEventSequence: 0)));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-b", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("requested room room-b", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryValidatorRejectsDisagreeingPlayerViews()
    {
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 1, 0, activePlayerId: "alice"),
            ["bob"] = PlayerView("bob", 1, 0, activePlayerId: "bob")
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(errors, error => error.Contains("disagrees on active player", StringComparison.Ordinal));
    }

    private static RecoveredEvent RecoveredEvent(long sequence, string kind)
    {
        return new RecoveredEvent(
            sequence,
            sequence,
            0,
            new GameEvent(kind, kind, new Dictionary<string, object?>()));
    }

    private static MatchRecoveryFrame RecoveryFrame(
        long currentTick,
        long lastEventSequence,
        IReadOnlyList<RecoveredCommand>? commands = null)
    {
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", currentTick, lastEventSequence, turnNumber: 2, activePlayerId: "bob"),
            ["bob"] = PlayerView("bob", currentTick, lastEventSequence, turnNumber: 2, activePlayerId: "bob")
        };
        var events = Enumerable.Range(1, (int)lastEventSequence)
            .Select(sequence => RecoveredEvent(sequence, $"EVENT_{sequence}"))
            .ToArray();
        return new MatchRecoveryFrame(
            "room-a",
            currentTick,
            lastEventSequence,
            commands ?? [],
            events,
            playerViews,
            []);
    }

    private static RecoveredPlayerView PlayerView(
        string playerId,
        long tick,
        long lastEventSequence,
        int turnNumber = 1,
        string activePlayerId = "alice")
    {
        var snapshot = new SnapshotDto(
            tick,
            turnNumber,
            activePlayerId,
            new Dictionary<string, object?>
            {
                ["alice"] = new Dictionary<string, object?>
                {
                    ["id"] = "alice",
                    ["seat"] = "P1"
                },
                ["bob"] = new Dictionary<string, object?>
                {
                    ["id"] = "bob",
                    ["seat"] = "P2"
                }
            },
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

    private static string PlayerSeat(SnapshotDto snapshot, string playerId)
    {
        var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
        return Assert.IsType<string>(player["seat"]);
    }

    private sealed class FixedRecoveryStore(MatchRecoveryFrame? frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(frame);
        }
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
