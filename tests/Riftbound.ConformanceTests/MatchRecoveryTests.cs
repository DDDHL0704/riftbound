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
    public async Task RegistryHydratesRecoveredSessionFromAuthoritativeState()
    {
        var authoritativeState = new MatchState(
            "room-a",
            4,
            3,
            "bob",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            });
        var frame = new MatchRecoveryFrame(
            "room-a",
            4,
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            authoritativeState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var session = await registry.GetOrCreateAsync("room-a", CancellationToken.None);

        var snapshot = session.SnapshotFor("alice");
        Assert.Equal(4, snapshot.Tick);
        Assert.Equal(3, snapshot.TurnNumber);
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
    public async Task RecoveredSessionAcceptsPersistedReconnectTokenAndRotatesIt()
    {
        const string oldToken = "rt_old_token";
        var playerStore = new RecordingMatchPlayerStore();
        playerStore.Seed("room-a", "alice", ReconnectTokenHasher.Hash(oldToken));
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(RecoveryFrame(currentTick: 2, lastEventSequence: 6)),
            playerStore);
        var session = await registry.GetOrCreateAsync("room-a", CancellationToken.None);

        var reconnect = await session.ReconnectPlayerAsync("alice", oldToken, CancellationToken.None);

        Assert.Equal("alice", reconnect.PlayerId);
        Assert.Equal("P1", reconnect.Seat);
        Assert.StartsWith("rt_", reconnect.ReconnectToken, StringComparison.Ordinal);
        Assert.NotEqual(oldToken, reconnect.ReconnectToken);
        Assert.Equal(
            ReconnectTokenHasher.Hash(reconnect.ReconnectToken),
            playerStore.HashFor("room-a", "alice"));
        await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.ReconnectPlayerAsync("alice", oldToken, CancellationToken.None));
    }

    [Fact]
    public async Task RecoveredExistingPlayerMustUseReconnectInsteadOfJoin()
    {
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(RecoveryFrame(currentTick: 2, lastEventSequence: 6)));
        var session = await registry.GetOrCreateAsync("room-a", CancellationToken.None);

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.EnsurePlayerAsync("alice", CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidReconnectToken, error.Code);
        Assert.Equal("reconnect token required for existing player", error.Message);
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

    [Fact]
    public void RecoveryValidatorRejectsPlayerViewsThatDisagreeWithAuthoritativeState()
    {
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 1, 0, activePlayerId: "alice"),
            ["bob"] = PlayerView("bob", 1, 0, activePlayerId: "alice")
        };
        var authoritativeState = new MatchState(
            "room-a",
            1,
            1,
            "bob",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            });

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            playerViews,
            authoritativeState);

        Assert.Contains(
            errors,
            error => error.Contains("disagrees with authoritative state active player", StringComparison.Ordinal));
    }

    [Fact]
    public void MatchStateHashIsStableAcrossDictionaryInsertionOrder()
    {
        var first = new MatchState(
            "room-a",
            5,
            2,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with { Hand = ["A-HAND-1"] },
                ["bob"] = PlayerZones.Empty with { Hand = ["B-HAND-1"] }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["A-HAND-1"] = new("A-HAND-1", power: 4, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard]),
                ["B-HAND-1"] = new("B-HAND-1", power: 3, cardNo: "SFD·126/221", tags: [CardObjectTags.UnitCard])
            });
        var second = first with
        {
            Seats = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bob"] = "P2",
                ["alice"] = "P1"
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["bob"] = first.PlayerZones["bob"],
                ["alice"] = first.PlayerZones["alice"]
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["B-HAND-1"] = first.CardObjects["B-HAND-1"],
                ["A-HAND-1"] = first.CardObjects["A-HAND-1"]
            }
        };

        Assert.Equal(MatchStateHasher.Hash(first), MatchStateHasher.Hash(second));
    }

    [Fact]
    public void SpectatorReplayFrameRedactsPrivateZonesFaceDownObjectsAndRngState()
    {
        var state = new MatchState(
            "room-a",
            5,
            2,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            seed: 260330,
            rngCursor: 9,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Hand = ["A-HAND-1"],
                    Base = ["A-FACEDOWN-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Hand = ["B-HAND-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["A-HAND-1"] = new("A-HAND-1", power: 4, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard]),
                ["B-HAND-1"] = new("B-HAND-1", power: 3, cardNo: "SFD·126/221", tags: [CardObjectTags.UnitCard]),
                ["A-FACEDOWN-1"] = new(
                    "A-FACEDOWN-1",
                    power: 5,
                    isFaceDown: true,
                    cardNo: "OGN·173/298",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            });
        var entry = new MatchJournalEntry(
            "room-a",
            "alice",
            "intent-1",
            "PLAY_CARD",
            null,
            4,
            5,
            7,
            8,
            true,
            null,
            state,
            [new GameEvent("CARD_PLAYED", "测试事件", new Dictionary<string, object?>())],
            ResolutionResult.BuildSnapshots(state),
            ResolutionResult.BuildPrompts(state),
            DateTimeOffset.UtcNow);

        var frame = MatchReplayRedactor.BuildSpectatorFrame(entry);

        Assert.Equal("room-a", frame.RoomId);
        Assert.Equal(5, frame.Tick);
        Assert.Equal(8, frame.EventSequence);
        Assert.Equal(MatchStateHasher.Hash(state), frame.AuthoritativeStateHash);
        Assert.Matches("^[0-9a-f]{64}$", frame.AuthoritativeStateHash);
        Assert.DoesNotContain("seed", frame.SpectatorSnapshot.Timing.Keys);
        Assert.DoesNotContain("rngCursor", frame.SpectatorSnapshot.Timing.Keys);

        var aliceView = Assert.IsType<Dictionary<string, object?>>(frame.SpectatorSnapshot.Players["alice"]);
        var aliceZones = Assert.IsType<Dictionary<string, object?>>(aliceView["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(aliceZones["hand"]));
        Assert.Equal(1, Assert.IsType<int>(aliceZones["handHidden"]));
        var aliceObjects = Assert.IsType<Dictionary<string, object?>>(aliceView["objects"]);
        Assert.DoesNotContain("A-HAND-1", aliceObjects.Keys);
        var faceDown = Assert.IsType<Dictionary<string, object?>>(aliceObjects["A-FACEDOWN-1"]);
        Assert.True(Assert.IsType<bool>(faceDown["isFaceDown"]));
        Assert.DoesNotContain("cardNo", faceDown.Keys);
        Assert.DoesNotContain("tags", faceDown.Keys);
        Assert.DoesNotContain("power", faceDown.Keys);

        var bobView = Assert.IsType<Dictionary<string, object?>>(frame.SpectatorSnapshot.Players["bob"]);
        var bobZones = Assert.IsType<Dictionary<string, object?>>(bobView["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(bobZones["hand"]));
        Assert.Equal(1, Assert.IsType<int>(bobZones["handHidden"]));
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

    private sealed class RecordingMatchPlayerStore : IMatchPlayerStore
    {
        private readonly Dictionary<(string RoomId, string PlayerId), string> hashes = new();

        public void Seed(string roomId, string playerId, string reconnectTokenHash)
        {
            hashes[(roomId, playerId)] = reconnectTokenHash;
        }

        public string? HashFor(string roomId, string playerId)
        {
            return hashes.TryGetValue((roomId, playerId), out var hash) ? hash : null;
        }

        public ValueTask SavePlayerSessionAsync(
            string roomId,
            string playerId,
            string seat,
            string reconnectTokenHash,
            CancellationToken cancellationToken)
        {
            hashes[(roomId, playerId)] = reconnectTokenHash;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> HasReconnectTokenHashAsync(
            string roomId,
            string playerId,
            string reconnectTokenHash,
            CancellationToken cancellationToken)
        {
            var matches = hashes.TryGetValue((roomId, playerId), out var hash)
                && string.Equals(hash, reconnectTokenHash, StringComparison.Ordinal);
            return ValueTask.FromResult(matches);
        }
    }
}
