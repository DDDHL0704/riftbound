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
    public void RecoveryValidatorRejectsNegativeCurrentTick()
    {
        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            currentTick: -1);

        Assert.Contains(
            errors,
            error => error.Contains("current tick cannot be negative", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsPlayerViewTicksAfterRecoveryTick()
    {
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 4, 0) with
            {
                PromptTick = 5
            }
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            playerViews,
            currentTick: 3);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice has row tick 4 after recovery tick 3",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "prompt for alice has row tick 5 after recovery tick 3",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsPromptMetadataWithoutMatchingPayload()
    {
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 3, 0) with
            {
                Prompt = null
            },
            ["bob"] = PlayerView("bob", 3, 0) with
            {
                PromptTick = null,
                PromptEventSequence = null
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "prompt metadata for alice has tick/event sequence without prompt payload",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "prompt for bob is missing row tick/event sequence metadata",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsNegativePlayerViewRowMetadata()
    {
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 0, 0) with
            {
                SnapshotTick = -1,
                SnapshotEventSequence = -2,
                PromptTick = -3,
                PromptEventSequence = -4
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice has negative row tick -1", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice has negative event sequence -2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("prompt for alice has negative row tick -3", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("prompt for alice has negative event sequence -4", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingPlayerViewSnapshot()
    {
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 0, 0) with
            {
                Snapshot = null!
            },
            ["bob"] = PlayerView("bob", 0, 0)
        };
        var authoritativeState = new MatchState(
            "room-a",
            0,
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
            playerViews,
            authoritativeState);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingSnapshotPlayers()
    {
        var alice = PlayerView("alice", 0, 0);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Players = null!
                }
            },
            ["bob"] = PlayerView("bob", 0, 0)
        };
        var authoritativeState = new MatchState(
            "room-a",
            0,
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
            playerViews,
            authoritativeState);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice players are required", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsInvalidRecoveredEventTickAndOrder()
    {
        var events = new[]
        {
            new RecoveredEvent(
                1,
                -1,
                0,
                new GameEvent("NEGATIVE_TICK", "negative tick", new Dictionary<string, object?>())),
            new RecoveredEvent(
                2,
                4,
                -1,
                new GameEvent("FUTURE_EVENT", "future event", new Dictionary<string, object?>())),
            new RecoveredEvent(
                3,
                2,
                0,
                new GameEvent("BACKWARD_TICK", "backward tick", new Dictionary<string, object?>()))
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            3,
            [],
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            currentTick: 3);

        Assert.Contains(
            errors,
            error => error.Contains("event sequence 1 has negative tick -1", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("event sequence 2 has negative order -1", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("event sequence 2 has tick 4 after recovery tick 3", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "event sequence 3 tick 2 is before previous event tick 4",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsNonPositiveRecoveredEventSequence()
    {
        var events = new[]
        {
            RecoveredEvent(0, "ZERO_SEQUENCE"),
            RecoveredEvent(-1, "NEGATIVE_SEQUENCE")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains("event sequence value 0 must be positive", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("event sequence value -1 must be positive", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsBlankRecoveredEventKind()
    {
        var events = new[]
        {
            new RecoveredEvent(
                1,
                0,
                0,
                new GameEvent(" ", "blank kind", new Dictionary<string, object?>()))
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            1,
            [],
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains("event sequence 1 kind is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsNullRecoveredEventPayload()
    {
        var events = new[]
        {
            new RecoveredEvent(
                1,
                0,
                0,
                new GameEvent("NULL_PAYLOAD", "null payload", null!))
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            1,
            [],
            events,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains("event sequence 1 payload is required", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsNegativeCommandCompletedEventSequence()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-negative-completed-sequence",
                "PASS",
                RawCommand("PASS"),
                0,
                0,
                0,
                -1,
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
                "command intent-negative-completed-sequence has negative completed event sequence -1",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsNegativeCommandCompletedTick()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-negative-completed-tick",
                "PASS",
                RawCommand("PASS"),
                0,
                -1,
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
                "command intent-negative-completed-tick has negative completed tick -1",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRecoveredCommandTicksAfterRecoveryTick()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-future-start",
                "PASS",
                RawCommand("PASS"),
                4,
                4,
                0,
                0,
                true,
                null),
            new RecoveredCommand(
                "alice",
                "intent-future-completed",
                "PASS",
                RawCommand("PASS"),
                2,
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
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            currentTick: 3);

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-future-start starts at tick 4 after recovery tick 3",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-future-start completes at tick 4 after recovery tick 3",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-future-completed completes at tick 4 after recovery tick 3",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsOutOfOrderRecoveredCommands()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-later",
                "PASS",
                RawCommand("PASS"),
                2,
                2,
                2,
                2,
                true,
                null),
            new RecoveredCommand(
                "alice",
                "intent-earlier",
                "PASS",
                RawCommand("PASS"),
                1,
                1,
                1,
                1,
                true,
                null)
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            commands,
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "command stream is not ordered by event span: intent-earlier 1->1 after 2->2",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsBlankRecoveredCommandIdentityAndType()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                " ",
                "",
                " ",
                null,
                0,
                0,
                0,
                0,
                false,
                "missing command identity")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            commands,
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains("command player id is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("command client intent id is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("command type is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsCommandDiagnosticPresenceMismatch()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-accepted-error",
                "PASS",
                RawCommand("PASS"),
                0,
                0,
                0,
                0,
                true,
                "accepted command should not carry an error"),
            new RecoveredCommand(
                "alice",
                "intent-rejected-missing-error",
                "PASS",
                RawCommand("PASS"),
                0,
                0,
                0,
                0,
                false,
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
                "accepted command intent-accepted-error has error message",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "rejected command intent-rejected-missing-error is missing error message",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsInvalidRawCommandShape()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-raw-array",
                "PASS",
                RawJson("[]"),
                0,
                0,
                0,
                0,
                false,
                "raw array"),
            new RecoveredCommand(
                "alice",
                "intent-raw-missing-cmd",
                "PASS",
                RawJson("""{"payload":true}"""),
                0,
                0,
                0,
                0,
                false,
                "missing cmd type"),
            new RecoveredCommand(
                "alice",
                "intent-raw-blank-cmd",
                "PASS",
                RawJson("""{"cmdType":" "}"""),
                0,
                0,
                0,
                0,
                false,
                "blank cmd type")
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
                "command intent-raw-array raw command must be a JSON object",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-raw-missing-cmd raw command is missing cmdType",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-raw-blank-cmd raw cmdType is required",
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
    public void RecoveryValidatorRejectsMissingSpectatorReplaySnapshot()
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
                SpectatorSnapshot = null!
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
            error => error.Contains("spectator replay frame snapshot is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingSpectatorReplaySnapshotTiming()
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
                SpectatorSnapshot = MatchReplayRedactor.BuildSpectatorFrame(
                    "room-a",
                    3,
                    2,
                    events.Select(recoveredEvent => recoveredEvent.Event).ToArray(),
                    authoritativeState).SpectatorSnapshot with
                    {
                        Timing = null!
                    }
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
            error => error.Contains("spectator replay frame snapshot timing is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingSpectatorReplaySnapshotPlayers()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Players = null!
            }
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
            error => error.Contains("spectator replay frame snapshot players are required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingSpectatorReplaySnapshotLanes()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Lanes = null!
            }
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
            error => error.Contains("spectator replay frame snapshot lanes are required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingSpectatorReplaySnapshotStack()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Stack = null!
            }
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
            error => error.Contains("spectator replay frame snapshot stack is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackCountMismatch()
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
            },
            stackItems:
            [
                new StackItemState(
                    "stack-1",
                    "alice",
                    sourceObjectId: "spell-1",
                    effectKind: "DRAW",
                    cardNo: "SFD-001")
            ]);
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Stack = []
            }
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
            error => error.Contains("spectator replay frame snapshot stack count 0 does not match authoritative state stack count 1", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemIdsMismatch()
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
            },
            stackItems:
            [
                new StackItemState(
                    "stack-1",
                    "alice",
                    sourceObjectId: "spell-1",
                    effectKind: "DRAW",
                    cardNo: "SFD-001")
            ]);
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
        var stack = spectatorReplayFrame.SpectatorSnapshot.Stack.ToArray();
        var firstStackItem = Assert.IsType<Dictionary<string, object?>>(stack[0]);
        var corruptedStackItem = new Dictionary<string, object?>(firstStackItem, StringComparer.Ordinal)
        {
            ["stackItemId"] = "stack-2"
        };
        stack[0] = corruptedStackItem;
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Stack = stack
            }
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
            error => error.Contains("spectator replay frame snapshot stack item ids disagree with authoritative state stack item ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingSpectatorReplaySnapshotTurnState()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                TurnState = ""
            }
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
            error => error.Contains("spectator replay frame snapshot turn state is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotTickMismatch()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Tick = 2
            }
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
            error => error.Contains("spectator replay frame snapshot tick 2 does not match frame tick 3", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotTurnNumberMismatch()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                TurnNumber = 2
            }
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
            error => error.Contains("spectator replay frame snapshot turn number 2 does not match authoritative state turn number 1", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotActivePlayerMismatch()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                ActivePlayerId = "bob"
            }
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
            error => error.Contains("spectator replay frame snapshot active player bob does not match authoritative state active player alice", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotSeatMismatch()
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
        var players = spectatorReplayFrame.SpectatorSnapshot.Players.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        players["bob"] = new Dictionary<string, object?>
        {
            ["id"] = "bob",
            ["seat"] = "P1"
        };
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Players = players
            }
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
            error => error.Contains("spectator replay frame snapshot seats disagree with authoritative state seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPhaseMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["phase"] = MatchPhases.Room;
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing phase does not match authoritative state phase", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingStateMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["timingState"] = TimingStates.Room;
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing state does not match authoritative state timing state", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingTurnPlayerMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["turnPlayerId"] = "bob";
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing turn player does not match authoritative state turn player", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPriorityPlayerMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["priorityPlayerId"] = "bob";
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing priority player does not match authoritative state priority player", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingFocusPlayerMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["focusPlayerId"] = "bob";
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing focus player does not match authoritative state focus player", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingWinnerPlayerMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["winnerPlayerId"] = "bob";
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing winner player does not match authoritative state winner player", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPassedPriorityPlayersMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["passedPriorityPlayerIds"] = new[] { "bob" };
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing passed priority players do not match authoritative state passed priority players", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPassedFocusPlayersMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["passedFocusPlayerIds"] = new[] { "bob" };
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing passed focus players do not match authoritative state passed focus players", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingDestroyedUnitOwnersMismatch()
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["destroyedUnitOwnerIdsThisTurn"] = new[] { "bob" };
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Timing = timing
            }
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
            error => error.Contains("spectator replay frame timing destroyed unit owners do not match authoritative state destroyed unit owners", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotTurnStateMismatch()
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
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                TurnState = TimingStates.Room
            }
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
            error => error.Contains("spectator replay frame snapshot turn state does not match authoritative state timing state", StringComparison.Ordinal));
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

    private static JsonElement RawJson(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
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
