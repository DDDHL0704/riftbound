using System.Text.Json;
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
    public void RecoveryValidatorRejectsPromptSnapshotTickMismatch()
    {
        var prompt = new ActionPromptDto(
            "alice",
            true,
            "test prompt",
            ["PASS"],
            SnapshotTick: 7);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 3, 0) with
            {
                Prompt = prompt
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "prompt for alice has payload snapshot tick 7 but row tick 3",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsOutOfOrderRecoveredEvents()
    {
        var events = new[]
        {
            RecoveredEvent(2, "TURN_BEGAN"),
            RecoveredEvent(1, "TURN_ENDED")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            [],
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "event stream is not ordered by sequence: 1 after 2",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRejectedCommandsThatAdvanceTickOrRecordEvents()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED")
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-unsupported",
                "UNKNOWN_RECOVERY_TEST",
                RawCommand("UNKNOWN_RECOVERY_TEST"),
                4,
                5,
                0,
                1,
                false,
                "当前命令不受服务端支持。")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            1,
            commands,
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "rejected command intent-unsupported covers 1 event(s)",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "rejected command intent-unsupported advances tick 4->5",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsCommandsWithInvalidTickBounds()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-negative-tick",
                "PASS",
                RawCommand("PASS"),
                -1,
                0,
                0,
                0,
                true,
                null),
            new RecoveredCommand(
                "alice",
                "intent-backward-tick",
                "PASS",
                RawCommand("PASS"),
                5,
                4,
                0,
                0,
                true,
                null)
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            commands,
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-negative-tick has negative started tick -1",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-backward-tick completes before tick start: 5->4",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAcceptedCommandsThatOverlapEventOwnership()
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
                "intent-first",
                "END_TURN",
                RawCommand("END_TURN"),
                1,
                2,
                0,
                2,
                true,
                null),
            new RecoveredCommand(
                "bob",
                "intent-overlap",
                "PASS",
                RawCommand("PASS"),
                2,
                2,
                1,
                2,
                true,
                null)
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            commands,
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "event sequence 2 is covered by multiple accepted commands: intent-first and intent-overlap",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRawCommandTypeMismatch()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-mismatched-raw",
                "PASS",
                RawCommand("END_TURN"),
                2,
                2,
                0,
                0,
                false,
                "mismatched raw command")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            commands,
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-mismatched-raw raw cmdType END_TURN does not match recovered command type PASS",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorAcceptsDevSeedScenarioRawCommandType()
    {
        var events = new[]
        {
            RecoveredEvent(1, "DEV_SCENARIO_SEEDED")
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-dev-seed",
                "DEV_SEED_SCENARIO:basic-play",
                RawDevSeedCommand("basic-play"),
                0,
                1,
                0,
                1,
                true,
                null)
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            1,
            commands,
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Empty(errors);
    }

    [Fact]
    public void RecoveryValidatorRejectsDuplicateRecoveredCommandIntentForSamePlayer()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-duplicate",
                "PASS",
                RawCommand("PASS"),
                2,
                2,
                0,
                0,
                false,
                "first rejected duplicate"),
            new RecoveredCommand(
                "alice",
                "intent-duplicate",
                "END_TURN",
                RawCommand("END_TURN"),
                2,
                2,
                0,
                0,
                false,
                "second rejected duplicate")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            commands,
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-duplicate for player alice appears more than once in recovery frame",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorAllowsSameRecoveredCommandIntentForDifferentPlayers()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-shared",
                "PASS",
                RawCommand("PASS"),
                2,
                2,
                0,
                0,
                false,
                "alice rejected command"),
            new RecoveredCommand(
                "bob",
                "intent-shared",
                "PASS",
                RawCommand("PASS"),
                2,
                2,
                0,
                0,
                false,
                "bob rejected command")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            commands,
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.DoesNotContain(
            errors,
            error => error.Contains(
                "appears more than once in recovery frame",
                StringComparison.Ordinal));
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
        Assert.Equal("已有玩家重连需要提供重连令牌。", error.Message);
        Assert.DoesNotContain("reconnect token required", error.Message, StringComparison.Ordinal);
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
    public void RecoveryValidatorRejectsAuthoritativeStateTickMismatch()
    {
        var authoritativeState = new MatchState(
            "room-a",
            3,
            1,
            "alice",
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
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 4);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state tick 3 does not match recovery tick 4", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorAcceptsMatchingSpectatorReplayFrame()
    {
        var authoritativeState = new MatchState(
            "room-a",
            3,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            });
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(2, "TURN_BEGAN")
        };
        var spectatorReplayFrame = MatchReplayRedactor.BuildSpectatorFrame(
            "room-a",
            3,
            2,
            events.Select(recoveredEvent => recoveredEvent.Event).ToArray(),
            authoritativeState);

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            [],
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 3,
            spectatorReplayFrame: spectatorReplayFrame);

        Assert.Empty(errors);
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayFrameMismatch()
    {
        var authoritativeState = new MatchState(
            "room-a",
            3,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            });
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(2, "TURN_BEGAN")
        };
        var spectatorReplayFrame = MatchReplayRedactor.BuildSpectatorFrame(
            "room-a",
            3,
            2,
            events.Select(recoveredEvent => recoveredEvent.Event).ToArray(),
            authoritativeState) with
            {
                EventSequence = 3,
                Tick = 4,
                AuthoritativeStateHash = new string('0', 64)
            };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            [],
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 3,
            spectatorReplayFrame: spectatorReplayFrame);

        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame event sequence 3 does not match recovery sequence 2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame tick 4 does not match authoritative state tick 3", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame tick 4 does not match recovery tick 3", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame hash does not match authoritative state hash", StringComparison.Ordinal));
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
    public async Task ActionLogReplayerReplaysRecoveredCommandsToFinalStateHash()
    {
        var initialState = ReplayInitialState();
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        await liveSession.SubmitAsync(
            "alice",
            "intent-end-turn",
            new EndTurnCommand(),
            RawCommand("END_TURN"),
            CancellationToken.None);
        var expectedFinalState = journal.Entries[^1].AuthoritativeState;
        var recoveredCommands = journal.Entries.Select(ToRecoveredCommand).ToArray();

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            expectedFinalState,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(expectedFinalState), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
    }

    [Fact]
    public async Task ActionLogReplayerReportsFinalStateHashMismatch()
    {
        var initialState = ReplayInitialState();
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var wrongExpectedState = journal.Entries[^1].AuthoritativeState with
        {
            Tick = journal.Entries[^1].AuthoritativeState.Tick + 1
        };
        var recoveredCommands = journal.Entries.Select(ToRecoveredCommand).ToArray();

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            wrongExpectedState,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.False(replay.IsMatch);
        Assert.NotEqual(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Contains(replay.Errors, error => error.Contains("replayed final state hash", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionLogReplayerReplaysAcceptedAndRejectedCommandDiagnosticsToFinalStateHash()
    {
        var initialState = ReplayInitialState();
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        await liveSession.SubmitAsync(
            "alice",
            "intent-unsupported",
            new UnsupportedCommand("UNKNOWN_RECOVERY_TEST", RawCommand("UNKNOWN_RECOVERY_TEST")),
            RawCommand("UNKNOWN_RECOVERY_TEST"),
            CancellationToken.None);
        await liveSession.SubmitAsync(
            "alice",
            "intent-end-turn",
            new EndTurnCommand(),
            RawCommand("END_TURN"),
            CancellationToken.None);
        await liveSession.SubmitAsync(
            "bob",
            "intent-surrender",
            new SurrenderCommand(),
            RawCommand("SURRENDER"),
            CancellationToken.None);
        var expectedFinalState = journal.Entries[^1].AuthoritativeState;
        var recoveredCommands = journal.Entries.Select(ToRecoveredCommand).ToArray();

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            expectedFinalState,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(expectedFinalState), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
        var rejected = Assert.Single(journal.Entries, entry => !entry.Accepted);
        Assert.Equal("UNKNOWN_RECOVERY_TEST", rejected.CommandType);
        Assert.Equal("当前命令不受服务端支持。", rejected.ErrorMessage);
        Assert.Equal(rejected.StartedTick, rejected.CompletedTick);
        Assert.Equal(rejected.StartedEventSequence, rejected.CompletedEventSequence);
    }

    [Fact]
    public async Task ActionLogReplayerReportsRejectedCommandDiagnosticMismatch()
    {
        var initialState = ReplayInitialState();
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.SubmitAsync(
            "alice",
            "intent-unsupported",
            new UnsupportedCommand("UNKNOWN_RECOVERY_TEST", RawCommand("UNKNOWN_RECOVERY_TEST")),
            RawCommand("UNKNOWN_RECOVERY_TEST"),
            CancellationToken.None);
        var expectedFinalState = journal.Entries[^1].AuthoritativeState;
        var recoveredCommands = journal.Entries
            .Select(ToRecoveredCommand)
            .Select(command => string.Equals(command.ClientIntentId, "intent-unsupported", StringComparison.Ordinal)
                ? command with { ErrorMessage = "tampered recovered diagnostic" }
                : command)
            .ToArray();

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            expectedFinalState,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.False(replay.IsMatch);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Contains(
            replay.Errors,
            error => error.Contains("command intent-unsupported error message", StringComparison.Ordinal)
                && error.Contains("tampered recovered diagnostic", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RegistryRunsActionLogReplayAuditBeforeRecoveryRestore()
    {
        var initialState = MatchReplayInitialStateBuilder.FromSeats(
            "room-a",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            });
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.ReadyAsync("alice", "intent-ready-a", RawCommand("READY"), CancellationToken.None);
        var expectedFinalState = journal.Entries[^1].AuthoritativeState;
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            initialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var recovered = await registry.GetOrCreateAsync("room-a", CancellationToken.None);

        var snapshot = recovered.SnapshotFor("alice");
        Assert.Equal(expectedFinalState.Tick, snapshot.Tick);
        Assert.Equal("alice", snapshot.ActivePlayerId);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenActionLogReplayHashMismatches()
    {
        var initialState = MatchReplayInitialStateBuilder.FromSeats(
            "room-a",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            });
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.ReadyAsync("alice", "intent-ready-a", RawCommand("READY"), CancellationToken.None);
        var wrongFinalState = journal.Entries[^1].AuthoritativeState with
        {
            Tick = journal.Entries[^1].AuthoritativeState.Tick + 1
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            wrongFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            wrongFinalState,
            initialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("replayed final state hash", error.Message, StringComparison.Ordinal);
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

    [Fact]
    public void SpectatorReplayFramesRedactPrivateInformationAcrossGeneratedStates()
    {
        for (var index = 0; index < 16; index++)
        {
            var state = GeneratedPrivateState(index);
            var frame = MatchReplayRedactor.BuildSpectatorFrame(
                state.RoomId,
                state.Tick,
                index + 1,
                [new GameEvent("PROPERTY_EVENT", "property event", new Dictionary<string, object?>())],
                state);

            Assert.Equal(MatchStateHasher.Hash(state), frame.AuthoritativeStateHash);
            Assert.DoesNotContain("seed", frame.SpectatorSnapshot.Timing.Keys);
            Assert.DoesNotContain("rngCursor", frame.SpectatorSnapshot.Timing.Keys);
            AssertSpectatorPlayerRedacted(frame.SpectatorSnapshot, "alice", $"A-{index}", 1 + index % 3);
            AssertSpectatorPlayerRedacted(frame.SpectatorSnapshot, "bob", $"B-{index}", 2 + index % 2);
        }
    }

    private static MatchState ReplayInitialState()
    {
        return new MatchState(
            "room-a",
            2,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen);
    }

    private static MatchState GeneratedPrivateState(int index)
    {
        var alicePrefix = $"A-{index}";
        var bobPrefix = $"B-{index}";
        var aliceHand = Enumerable.Range(0, 1 + index % 3)
            .Select(cardIndex => $"{alicePrefix}-HAND-{cardIndex}")
            .ToArray();
        var bobHand = Enumerable.Range(0, 2 + index % 2)
            .Select(cardIndex => $"{bobPrefix}-HAND-{cardIndex}")
            .ToArray();
        var aliceFaceDown = $"{alicePrefix}-FACEDOWN";
        var bobFaceDown = $"{bobPrefix}-FACEDOWN";
        var aliceVisible = $"{alicePrefix}-VISIBLE";
        var bobVisible = $"{bobPrefix}-VISIBLE";
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [aliceFaceDown] = new(
                aliceFaceDown,
                power: 5 + index,
                isFaceDown: true,
                cardNo: "OGN·173/298",
                ownerId: "alice",
                controllerId: "alice",
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
            [bobFaceDown] = new(
                bobFaceDown,
                power: 4 + index,
                isFaceDown: true,
                cardNo: "SFD·126/221",
                ownerId: "bob",
                controllerId: "bob",
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
            [aliceVisible] = new(
                aliceVisible,
                power: 3,
                cardNo: "SFD·125/221",
                ownerId: "alice",
                controllerId: "alice",
                tags: [CardObjectTags.UnitCard]),
            [bobVisible] = new(
                bobVisible,
                power: 2,
                cardNo: "SFD·126/221",
                ownerId: "bob",
                controllerId: "bob",
                tags: [CardObjectTags.UnitCard])
        };
        foreach (var objectId in aliceHand)
        {
            cardObjects[objectId] = new(
                objectId,
                power: 7,
                cardNo: "OGN·096/298",
                ownerId: "alice",
                controllerId: "alice",
                tags: [CardObjectTags.UnitCard]);
        }

        foreach (var objectId in bobHand)
        {
            cardObjects[objectId] = new(
                objectId,
                power: 6,
                cardNo: "UNL-021/219",
                ownerId: "bob",
                controllerId: "bob",
                tags: [CardObjectTags.UnitCard]);
        }

        return new MatchState(
            "room-property",
            10 + index,
            2 + index,
            index % 2 == 0 ? "alice" : "bob",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Hand = aliceHand,
                    Base = [aliceFaceDown],
                    Battlefields = [aliceVisible]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Hand = bobHand,
                    Base = [bobFaceDown],
                    Battlefields = [bobVisible]
                }
            },
            cardObjects: cardObjects,
            seed: 260330 + index,
            rngCursor: 17 + index);
    }

    private static void AssertSpectatorPlayerRedacted(
        SnapshotDto snapshot,
        string playerId,
        string objectPrefix,
        int expectedHiddenHandCount)
    {
        var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
        var zones = Assert.IsType<Dictionary<string, object?>>(player["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(zones["hand"]));
        Assert.Equal(expectedHiddenHandCount, Assert.IsType<int>(zones["handHidden"]));

        var objects = Assert.IsType<Dictionary<string, object?>>(player["objects"]);
        for (var cardIndex = 0; cardIndex < expectedHiddenHandCount; cardIndex++)
        {
            Assert.DoesNotContain($"{objectPrefix}-HAND-{cardIndex}", objects.Keys);
        }

        var faceDown = Assert.IsType<Dictionary<string, object?>>(objects[$"{objectPrefix}-FACEDOWN"]);
        Assert.True(Assert.IsType<bool>(faceDown["isFaceDown"]));
        Assert.DoesNotContain("cardNo", faceDown.Keys);
        Assert.DoesNotContain("tags", faceDown.Keys);
        Assert.DoesNotContain("power", faceDown.Keys);
    }

    private static RecoveredCommand ToRecoveredCommand(MatchJournalEntry entry)
    {
        return new RecoveredCommand(
            entry.PlayerId,
            entry.ClientIntentId,
            entry.CommandType,
            entry.RawCommand?.Clone(),
            entry.StartedTick,
            entry.CompletedTick,
            entry.StartedEventSequence,
            entry.CompletedEventSequence,
            entry.Accepted,
            entry.ErrorMessage);
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

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement.Clone();
    }

    private static JsonElement RawDevSeedCommand(string scenarioId)
    {
        return JsonDocument.Parse($$"""{"cmdType":"DEV_SEED_SCENARIO","scenarioId":"{{scenarioId}}"}""").RootElement.Clone();
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
