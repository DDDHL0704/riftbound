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
            new RecoveredEvent(
                1,
                2,
                0,
                new GameEvent("TURN_ENDED", "TURN_ENDED", new Dictionary<string, object?>())),
            new RecoveredEvent(
                2,
                2,
                1,
                new GameEvent("TURN_BEGAN", "TURN_BEGAN", new Dictionary<string, object?>()))
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-end-turn",
                "END_TURN",
                null,
                0,
                2,
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
    public void RecoveryValidatorRejectsRecoveredEventNotCoveredByAcceptedCommand()
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
                1,
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
                "event sequence 2 is not covered by an accepted command",
                StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsPromptRowsThatDriftFromSnapshotRows()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(2, "TURN_BEGAN")
        };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 1, 1) with
            {
                PromptTick = 2,
                PromptEventSequence = 2
            }
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            [],
            events,
            playerViews,
            currentTick: 2);

        Assert.Contains(
            errors,
            error => error.Contains(
                "prompt for alice has row tick 2 but snapshot row tick 1",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "prompt for alice has event sequence 2 but snapshot event sequence 1",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsPlayerSnapshotRowsThatDisagreeAcrossPlayers()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(2, "TURN_BEGAN")
        };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 1, 1),
            ["bob"] = PlayerView("bob", 2, 2)
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            [],
            events,
            playerViews,
            currentTick: 2);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for bob disagrees on row tick", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for bob disagrees on event sequence", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsPlayerSnapshotRowsBehindRecoveryTail()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED"),
            RecoveredEvent(2, "TURN_BEGAN")
        };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 1, 1)
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            2,
            [],
            events,
            playerViews,
            currentTick: 2);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice has row tick 1 but recovery tick 2",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice has event sequence 1 but recovery event sequence 2",
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
    public void RecoveryValidatorRejectsMissingSnapshotStructuralFields()
    {
        var alice = PlayerView("alice", 0, 0);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Lanes = null!,
                    Stack = null!,
                    Timing = null!,
                    TurnState = " "
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice lanes are required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice stack is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice turn state is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTurnStateTimingStateMismatch()
    {
        var alice = PlayerView("alice", 0, 0);
        var missingTimingState = alice.Snapshot.Timing
            .Where(entry => entry.Key != "timingState")
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = missingTimingState
                }
            },
            ["bob"] = PlayerView("bob", 0, 0) with
            {
                Snapshot = PlayerView("bob", 0, 0).Snapshot with
                {
                    Timing = new Dictionary<string, object?>
                    {
                        ["timingState"] = TimingStates.Room
                    }
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing state is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for bob turn state NEUTRAL_OPEN does not match timing state ROOM",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMissingSnapshotTimingCoreFields()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .Where(entry => entry.Key == "timingState")
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing phase is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing turn player is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing room status is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTimingUnknownScalarValues()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["phase"] = "UNKNOWN_PHASE";
        timing["timingState"] = "UNKNOWN_TIMING_STATE";
        timing["roomStatus"] = "UNKNOWN_STATUS";
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing,
                    TurnState = "UNKNOWN_TIMING_STATE"
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice turn state UNKNOWN_TIMING_STATE is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing state UNKNOWN_TIMING_STATE is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing phase UNKNOWN_PHASE is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing room status UNKNOWN_STATUS is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTimingTurnPlayerOutsidePlayerMap()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["turnPlayerId"] = "charlie";
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing turn player charlie is missing from players",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTimingOptionalPlayersOutsidePlayerMap()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["priorityPlayerId"] = "charlie";
        timing["focusPlayerId"] = "diana";
        timing["winnerPlayerId"] = "eve";
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing priority player charlie is missing from players",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing focus player diana is missing from players",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing winner player eve is missing from players",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMalformedSnapshotTimingOptionalPlayers()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["priorityPlayerId"] = RawJson("""{"playerId":"alice"}""");
        timing["focusPlayerId"] = " ";
        timing["winnerPlayerId"] = RawJson("[]");
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing priority player id is invalid",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing focus player id is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing winner player id is invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTimingPlayerIdsWithSurroundingWhitespace()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["turnPlayerId"] = " alice ";
        timing["priorityPlayerId"] = " bob ";
        timing["readyPlayerIds"] = new[] { " alice " };
        timing["passedPriorityPlayerIds"] = new[] { " bob " };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing turn player alice has surrounding whitespace",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing priority player bob has surrounding whitespace",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing ready player alice has surrounding whitespace",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing passed priority player bob has surrounding whitespace",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTimingPlayerListsOutsidePlayerMap()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["readyPlayerIds"] = new[] { "charlie" };
        timing["passedPriorityPlayerIds"] = new[] { "diana" };
        timing["passedFocusPlayerIds"] = new[] { "eve" };
        timing["destroyedUnitOwnerIdsThisTurn"] = new[] { "frank" };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing ready player charlie is missing from players",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing passed priority player diana is missing from players",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing passed focus player eve is missing from players",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing destroyed unit owner frank is missing from players",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTimingPlayerListsWithDuplicates()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["readyPlayerIds"] = new[] { "alice", "alice" };
        timing["passedPriorityPlayerIds"] = new[] { "bob", "bob" };
        timing["passedFocusPlayerIds"] = new[] { "alice", "alice" };
        timing["destroyedUnitOwnerIdsThisTurn"] = new[] { "bob", "bob" };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing ready player alice is duplicated",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing passed priority player bob is duplicated",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing passed focus player alice is duplicated",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing destroyed unit owner bob is duplicated",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMalformedSnapshotTimingPlayerLists()
    {
        var alice = PlayerView("alice", 0, 0);
        var timing = alice.Snapshot.Timing
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        timing["readyPlayerIds"] = "alice";
        timing["passedPriorityPlayerIds"] = new object?[] { "bob" };
        timing["passedFocusPlayerIds"] = RawJson("""["alice",1]""");
        timing["destroyedUnitOwnerIdsThisTurn"] = RawJson("""{"playerId":"bob"}""");
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Timing = timing
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice timing ready player list is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing passed priority player list is invalid",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing passed focus player list is invalid",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice timing destroyed unit owner list is invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMalformedSnapshotPlayerPayloads()
    {
        var alice = PlayerView("alice", 0, 0);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Players = new Dictionary<string, object?>
                    {
                        ["alice"] = new Dictionary<string, object?>
                        {
                            ["id"] = "wrong-alice",
                            ["seat"] = "P1"
                        },
                        ["bob"] = new Dictionary<string, object?>
                        {
                            ["id"] = "bob",
                            ["seat"] = " "
                        },
                        ["charlie"] = "not-a-player-payload",
                        ["diana"] = new Dictionary<string, object?>
                        {
                            ["seat"] = "P4"
                        },
                        ["eve"] = RawJson("""{"id":"eve","seat":"P2"}""")
                    }
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice player alice payload id wrong-alice does not match player key",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice player bob seat is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice player charlie payload is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice player diana id is required", StringComparison.Ordinal));
        Assert.DoesNotContain(
            errors,
            error => error.Contains("snapshot for alice player eve", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotPlayerSeatValueDrift()
    {
        var alice = PlayerView("alice", 0, 0);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Players = new Dictionary<string, object?>
                    {
                        ["alice"] = new Dictionary<string, object?>
                        {
                            ["id"] = "alice",
                            ["seat"] = " P1 "
                        },
                        ["bob"] = new Dictionary<string, object?>
                        {
                            ["id"] = "bob",
                            ["seat"] = "P3"
                        },
                        ["charlie"] = RawJson("""{"id":"charlie","seat":"P1"}""")
                    }
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice player alice seat P1 has surrounding whitespace",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice player bob seat P3 is invalid",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "snapshot for alice player charlie seat P1 is duplicated",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotPlayerMapsMissingRecoveredPlayers()
    {
        var alice = PlayerView("alice", 0, 0);
        var bob = PlayerView("bob", 0, 0);
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    Players = alice.Snapshot.Players
                        .Where(player => player.Key != "bob")
                        .ToDictionary(player => player.Key, player => player.Value, StringComparer.Ordinal)
                }
            },
            ["bob"] = bob with
            {
                Snapshot = bob.Snapshot with
                {
                    Players = bob.Snapshot.Players
                        .Where(player => player.Key != "alice")
                        .ToDictionary(player => player.Key, player => player.Value, StringComparer.Ordinal)
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate("room-a", 0, [], [], playerViews);

        Assert.Contains(
            errors,
            error => error.Contains("snapshot for alice is missing player bob", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("snapshot for bob is missing player alice", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotActivePlayerOutsidePlayerMap()
    {
        var alice = PlayerView("alice", 0, 0);
        var missingActivePlayerView = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    ActivePlayerId = "charlie"
                }
            }
        };
        var blankActivePlayerView = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    ActivePlayerId = " "
                }
            }
        };

        var missingErrors = MatchRecoveryValidator.Validate("room-a", 0, [], [], missingActivePlayerView);
        var blankErrors = MatchRecoveryValidator.Validate("room-a", 0, [], [], blankActivePlayerView);

        Assert.Contains(
            missingErrors,
            error => error.Contains(
                "snapshot for alice active player charlie is missing from players",
                StringComparison.Ordinal));
        Assert.Contains(
            blankErrors,
            error => error.Contains("snapshot for alice active player is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSnapshotTurnNumbersBelowOne()
    {
        var alice = PlayerView("alice", 0, 0);
        var zeroTurnView = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    TurnNumber = 0
                }
            }
        };
        var negativeTurnView = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = alice with
            {
                Snapshot = alice.Snapshot with
                {
                    TurnNumber = -1
                }
            }
        };

        var zeroErrors = MatchRecoveryValidator.Validate("room-a", 0, [], [], zeroTurnView);
        var negativeErrors = MatchRecoveryValidator.Validate("room-a", 0, [], [], negativeTurnView);

        Assert.Contains(
            zeroErrors,
            error => error.Contains("snapshot for alice has invalid turn number 0", StringComparison.Ordinal));
        Assert.Contains(
            negativeErrors,
            error => error.Contains("snapshot for alice has invalid turn number -1", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsRecoveredCommandsThatStartBeforePreviousCompletedTick()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-first",
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
                "intent-backward-tick",
                "PASS",
                RawCommand("PASS"),
                3,
                3,
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
                "command intent-backward-tick starts at tick 3 before previous command completed tick 4",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRecoveredCommandTickGap()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED")
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-first",
                "END_TURN",
                RawCommand("END_TURN"),
                0,
                1,
                0,
                1,
                true,
                null),
            new RecoveredCommand(
                "bob",
                "intent-tick-gap",
                "PASS",
                RawCommand("PASS"),
                3,
                3,
                1,
                1,
                false,
                "test rejection")
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
                "command intent-tick-gap starts at tick 3 but previous command completed at tick 1; command ticks must be contiguous",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRecoveredCommandEventSpanGap()
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
                1,
                0,
                1,
                true,
                null),
            new RecoveredCommand(
                "bob",
                "intent-gap",
                "PASS",
                RawCommand("PASS"),
                1,
                1,
                2,
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
                "command intent-gap starts at event sequence 2 but previous command completed at 1; command event spans must be contiguous",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAcceptedCommandRecoveredEventOrderMismatch()
    {
        var events = new[]
        {
            new RecoveredEvent(
                1,
                1,
                1,
                new GameEvent("TURN_ENDED", "TURN_ENDED", new Dictionary<string, object?>()))
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-order-mismatch",
                "END_TURN",
                RawCommand("END_TURN"),
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

        Assert.Contains(
            errors,
            error => error.Contains(
                "event sequence 1 has order 1 but command intent-order-mismatch expects order 0",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAcceptedCommandRecoveredEventTickOutsideCommandSpan()
    {
        var events = new[]
        {
            new RecoveredEvent(
                1,
                4,
                0,
                new GameEvent("TURN_ENDED", "TURN_ENDED", new Dictionary<string, object?>()))
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-tick-mismatch",
                "END_TURN",
                RawCommand("END_TURN"),
                1,
                3,
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

        Assert.Contains(
            errors,
            error => error.Contains(
                "event sequence 1 has tick 4 outside command intent-tick-mismatch tick span 1->3",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAcceptedCommandCompletedTickMismatchCoveredEventTail()
    {
        var events = new[]
        {
            new RecoveredEvent(
                1,
                2,
                0,
                new GameEvent("TURN_ENDED", "TURN_ENDED", new Dictionary<string, object?>()))
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-tail-mismatch",
                "END_TURN",
                RawCommand("END_TURN"),
                1,
                3,
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

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-tail-mismatch completes at tick 3 but covered event tick tail is 2",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAcceptedCommandRecoveredEventTickBeforeCompletedTick()
    {
        var events = new[]
        {
            new RecoveredEvent(
                1,
                2,
                0,
                new GameEvent("TURN_ENDED", "TURN_ENDED", new Dictionary<string, object?>())),
            new RecoveredEvent(
                2,
                3,
                1,
                new GameEvent("TURN_BEGAN", "TURN_BEGAN", new Dictionary<string, object?>()))
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-event-tick-drift",
                "END_TURN",
                RawCommand("END_TURN"),
                1,
                3,
                0,
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
                "event sequence 1 has tick 2 but command intent-event-tick-drift completed tick is 3",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAcceptedCommandTickAdvanceWithoutEvents()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-empty-advance",
                "PASS",
                RawCommand("PASS"),
                1,
                2,
                0,
                0,
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
                "accepted command intent-empty-advance advances tick 1->2 without events",
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
    public void RecoveryValidatorRejectsCommandStartedEventSequenceAfterMatchSequence()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-start-after-tail",
                "PASS",
                RawCommand("PASS"),
                1,
                1,
                2,
                2,
                false,
                "command starts beyond event tail")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            1,
            commands,
            [RecoveredEvent(1, "TURN_ENDED")],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal));

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-start-after-tail starts at 2 after match sequence 1",
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
    public void RecoveryValidatorRejectsRecoveredCommandTickTailBeforeRecoveryTick()
    {
        var events = new[]
        {
            RecoveredEvent(1, "TURN_ENDED")
        };
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-tail-behind",
                "END_TURN",
                RawCommand("END_TURN"),
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
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            currentTick: 3);

        Assert.Contains(
            errors,
            error => error.Contains(
                "command tick tail 1 does not match recovery tick 3",
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
    public void RecoveryValidatorRejectsRecoveredCommandIdentityAndTypeWithSurroundingWhitespace()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                " alice ",
                " intent-trim ",
                " PASS ",
                RawCommand("PASS"),
                0,
                0,
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
            error => error.Contains("command player id alice has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("command client intent id intent-trim has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("command type PASS has surrounding whitespace", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRecoveredCommandPlayerOutsidePlayerViews()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "charlie",
                "intent-unknown-player",
                "PASS",
                RawCommand("PASS"),
                0,
                0,
                0,
                0,
                false,
                "unknown recovered command player")
        };
        var playerViews = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal)
        {
            ["alice"] = PlayerView("alice", 0, 0),
            ["bob"] = PlayerView("bob", 0, 0)
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            commands,
            [],
            playerViews);

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-unknown-player player charlie is missing from recovered player views",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRecoveredCommandPlayerOutsideAuthoritativeSeats()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "charlie",
                "intent-unknown-seat-player",
                "PASS",
                RawCommand("PASS"),
                0,
                0,
                0,
                0,
                false,
                "unknown authoritative command player")
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            commands,
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            ReplayInitialState());

        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-unknown-seat-player player charlie is missing from authoritative state seats",
                StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsRejectedCommandDiagnosticWithSurroundingWhitespace()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-rejected-trim",
                "PASS",
                RawCommand("PASS"),
                0,
                0,
                0,
                0,
                false,
                " rejected diagnostic ")
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
                "rejected command intent-rejected-trim error message has surrounding whitespace",
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
    public void RecoveryValidatorRejectsRawCommandTypeWithSurroundingWhitespace()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-raw-trim",
                "PASS",
                RawJson("""{"cmdType":" PASS "}"""),
                0,
                0,
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
                "command intent-raw-trim raw cmdType PASS has surrounding whitespace",
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
    public void RecoveryValidatorRejectsPayloadCommandWithoutRawCommand()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-paycost-missing-raw",
                CommandTypes.PayCost,
                null,
                0,
                0,
                0,
                0,
                true,
                null),
            new RecoveredCommand(
                "alice",
                "intent-choice-missing-raw",
                CommandTypes.ChooseHandCards,
                null,
                0,
                0,
                0,
                0,
                false,
                "invalid hand choice"),
            new RecoveredCommand(
                "alice",
                "intent-pass-without-raw",
                CommandTypes.Pass,
                null,
                0,
                0,
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
                "command intent-paycost-missing-raw raw command is required for PAY_COST",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-choice-missing-raw raw command is required for CHOOSE_HAND_CARDS",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            errors,
            error => error.Contains(
                "intent-pass-without-raw raw command is required",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsP0PayloadCommandRawPayloadShapeDrift()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-paycost-cmdtype-only",
                CommandTypes.PayCost,
                RawCommand(CommandTypes.PayCost),
                0,
                0,
                0,
                0,
                false,
                "missing payment payload"),
            new RecoveredCommand(
                "alice",
                "intent-assign-malformed",
                CommandTypes.AssignCombatDamage,
                RawJson("""
                    {
                      "cmdType": "ASSIGN_COMBAT_DAMAGE",
                      "battleId": "battle-1",
                      "battlefieldId": "battlefield-1",
                      "assignments": [
                        { "sourceObjectId": "attacker-1" }
                      ]
                    }
                    """),
                0,
                0,
                0,
                0,
                false,
                "malformed assignment payload"),
            new RecoveredCommand(
                "alice",
                "intent-order-cmdtype-only",
                CommandTypes.OrderTriggers,
                RawCommand(CommandTypes.OrderTriggers),
                0,
                0,
                0,
                0,
                false,
                "missing trigger order payload"),
            new RecoveredCommand(
                "alice",
                "intent-choice-malformed",
                CommandTypes.ChooseHandCards,
                RawJson("""
                    {
                      "cmdType": "CHOOSE_HAND_CARDS",
                      "choiceId": "choice-1",
                      "choiceWindow": "TEST_CHOICE",
                      "chosenObjectIds": ["object-1", " "]
                    }
                    """),
                0,
                0,
                0,
                0,
                false,
                "malformed choice payload")
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
                "command intent-paycost-cmdtype-only raw PAY_COST paymentId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-paycost-cmdtype-only raw PAY_COST paymentChoiceIds must be an array",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-assign-malformed raw ASSIGN_COMBAT_DAMAGE assignments[0].targetObjectId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-assign-malformed raw ASSIGN_COMBAT_DAMAGE assignments[0].damage is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-order-cmdtype-only raw ORDER_TRIGGERS orderedTriggerIds or triggerIds must be an array",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-choice-malformed raw CHOOSE_HAND_CARDS chosenObjectIds[1] is required",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsPrimaryActionCommandRawPayloadShapeDrift()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-play-cmdtype-only",
                CommandTypes.PlayCard,
                RawCommand(CommandTypes.PlayCard),
                0,
                0,
                0,
                0,
                false,
                "missing play payload"),
            new RecoveredCommand(
                "alice",
                "intent-activate-malformed",
                CommandTypes.ActivateAbility,
                RawJson("""
                    {
                      "cmdType": "ACTIVATE_ABILITY",
                      "sourceObjectId": "ability-source-1",
                      "abilityId": "ability-1",
                      "targetObjectIds": ["target-1", " "]
                    }
                    """),
                0,
                0,
                0,
                0,
                false,
                "malformed ability payload"),
            new RecoveredCommand(
                "alice",
                "intent-legend-cmdtype-only",
                CommandTypes.LegendAct,
                RawCommand(CommandTypes.LegendAct),
                0,
                0,
                0,
                0,
                false,
                "missing legend payload")
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
                "command intent-play-cmdtype-only raw PLAY_CARD sourceObjectId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-play-cmdtype-only raw PLAY_CARD cardNo is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-play-cmdtype-only raw PLAY_CARD targetObjectIds must be an array",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-activate-malformed raw ACTIVATE_ABILITY targetObjectIds[1] is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-legend-cmdtype-only raw LEGEND_ACT sourceObjectId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-legend-cmdtype-only raw LEGEND_ACT abilityId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-legend-cmdtype-only raw LEGEND_ACT targetObjectIds must be an array",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsRuneActionCommandRawPayloadShapeDrift()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-tap-rune-cmdtype-only",
                CommandTypes.TapRune,
                RawCommand(CommandTypes.TapRune),
                0,
                0,
                0,
                0,
                false,
                "missing tap rune payload"),
            new RecoveredCommand(
                "alice",
                "intent-recycle-rune-malformed",
                CommandTypes.RecycleRune,
                RawJson("""
                    {
                      "cmdType": "RECYCLE_RUNE",
                      "sourceObjectId": " "
                    }
                    """),
                0,
                0,
                0,
                0,
                false,
                "malformed recycle rune payload")
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
                "command intent-tap-rune-cmdtype-only raw TAP_RUNE sourceObjectId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-recycle-rune-malformed raw RECYCLE_RUNE sourceObjectId is required",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsCardActionCommandRawPayloadShapeDrift()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-hide-card-cmdtype-only",
                CommandTypes.HideCard,
                RawCommand(CommandTypes.HideCard),
                0,
                0,
                0,
                0,
                false,
                "missing hide card payload"),
            new RecoveredCommand(
                "alice",
                "intent-reveal-card-malformed",
                CommandTypes.RevealCard,
                RawJson("""
                    {
                      "cmdType": "REVEAL_CARD",
                      "sourceObjectId": "source-1",
                      "cardNo": "CARD-1",
                      "targetObjectIds": ["target-1", " "]
                    }
                    """),
                0,
                0,
                0,
                0,
                false,
                "malformed reveal card payload")
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
                "command intent-hide-card-cmdtype-only raw HIDE_CARD sourceObjectId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-hide-card-cmdtype-only raw HIDE_CARD cardNo is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-reveal-card-malformed raw REVEAL_CARD targetObjectIds[1] is required",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsMovementAndEquipmentCommandRawPayloadShapeDrift()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-move-unit-cmdtype-only",
                CommandTypes.MoveUnit,
                RawCommand(CommandTypes.MoveUnit),
                0,
                0,
                0,
                0,
                false,
                "missing move unit payload"),
            new RecoveredCommand(
                "alice",
                "intent-assemble-equipment-malformed",
                CommandTypes.AssembleEquipment,
                RawJson("""
                    {
                      "cmdType": "ASSEMBLE_EQUIPMENT",
                      "sourceObjectId": "equipment-1",
                      "targetObjectId": " "
                    }
                    """),
                0,
                0,
                0,
                0,
                false,
                "malformed assemble equipment payload")
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
                "command intent-move-unit-cmdtype-only raw MOVE_UNIT sourceObjectId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-move-unit-cmdtype-only raw MOVE_UNIT origin is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-move-unit-cmdtype-only raw MOVE_UNIT destination is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-assemble-equipment-malformed raw ASSEMBLE_EQUIPMENT targetObjectId is required",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsDeclareBattleCommandRawPayloadShapeDrift()
    {
        var commands = new[]
        {
            new RecoveredCommand(
                "alice",
                "intent-declare-battle-cmdtype-only",
                CommandTypes.DeclareBattle,
                RawCommand(CommandTypes.DeclareBattle),
                0,
                0,
                0,
                0,
                false,
                "missing declare battle payload"),
            new RecoveredCommand(
                "alice",
                "intent-declare-battle-malformed",
                CommandTypes.DeclareBattle,
                RawJson("""
                    {
                      "cmdType": "DECLARE_BATTLE",
                      "battlefieldId": "battlefield-1",
                      "attackerObjectIds": ["attacker-1", " "],
                      "defenderObjectIds": ["defender-1"]
                    }
                    """),
                0,
                0,
                0,
                0,
                false,
                "malformed declare battle payload")
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
                "command intent-declare-battle-cmdtype-only raw DECLARE_BATTLE battlefieldId is required",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-declare-battle-cmdtype-only raw DECLARE_BATTLE attackerObjectIds must be an array",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-declare-battle-cmdtype-only raw DECLARE_BATTLE defenderObjectIds must be an array",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "command intent-declare-battle-malformed raw DECLARE_BATTLE attackerObjectIds[1] is required",
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
    public void RecoveryValidatorRejectsAuthoritativeStateSeatValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P1",
                ["charlie"] = "P3"
            });

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state seat P1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state seat P3 for charlie is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStatePlayerPointersOutsideSeats()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "charlie",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "diana");

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state active player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state turn player diana is missing from seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateOptionalPlayerPointersOutsideSeats()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            priorityPlayerId: "charlie",
            focusPlayerId: "diana",
            winnerPlayerId: "eve",
            extraTurnPlayerId: "frank",
            openingSecondActionPlayerId: "gina");

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state priority player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state focus player diana is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state winner player eve is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state opening second action player gina is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state extra turn player frank is missing from seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStatePlayerListsOutsideSeats()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            readyPlayerIds: ["charlie"],
            passedPriorityPlayerIds: ["diana"],
            passedFocusPlayerIds: ["eve"],
            destroyedUnitOwnerIdsThisTurn: ["frank"],
            mulliganCompletedPlayerIds: ["gina"]);

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state ready player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state passed priority player diana is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state passed focus player eve is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state destroyed unit owner frank is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state mulligan completed player gina is missing from seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStatePlayerMapsOutsideSeats()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["charlie"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["diana"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["eve"] = 1
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["frank"] = 1
            },
            playerCardsPlayedThisTurn: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["gina"] = 1
            },
            playerDecklists: new Dictionary<string, OfficialDecklist>(StringComparer.Ordinal)
            {
                ["henry"] = new("LEGEND-001", "CHAMPION-001", ["MAIN-001"], ["RUNE-001"], ["BATTLEFIELD-001"])
            });

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state rune pool player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state zone player diana is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state score player eve is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state experience player frank is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state cards played player gina is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state decklist player henry is missing from seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateObjectPlayersOutsideSeats()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("obj-1", ownerId: "charlie", controllerId: "diana")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("eve", "HAND")
            });

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 owner player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 controller player diana is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location obj-1 player eve is missing from seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateStackAndTriggerControllersOutsideSeats()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            stackItems:
            [
                new StackItemState(
                    "stack-1",
                    controllerId: "charlie",
                    sourceObjectId: "obj-1",
                    effectKind: "SPELL")
            ],
            triggerQueue:
            [
                new TriggerQueueItemState(
                    "trigger-1",
                    controllerId: "diana",
                    sourceObjectId: "obj-2",
                    effectKind: "LAST_BREATH",
                    triggeredByEventKind: "OBJECT_DESTROYED")
            ]);

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 controller player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 controller player diana is missing from seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateStackAndTriggerValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            StackItems =
            [
                new StackItemState(
                    "stack-1",
                    "alice",
                    sourceObjectId: "obj-1",
                    effectKind: "SPELL")
                {
                    StackItemId = " stack-1 ",
                    SourceObjectId = " obj-1 ",
                    EffectKind = " SPELL ",
                    CardNo = " CARD-1 ",
                    TargetObjectIds = [" target-1 ", ""],
                    DamageAmount = -1,
                    EffectRepeatCount = 0,
                    OptionalCosts = null!,
                    Destination = " GRAVEYARD ",
                    TimingContext = " OPEN_MAIN "
                },
                new StackItemState(
                    "stack-1",
                    "bob",
                    sourceObjectId: "obj-2",
                    effectKind: "SPELL")
                {
                    SourceObjectId = null!,
                    EffectKind = "",
                    CardNo = null!,
                    Destination = " ",
                    TimingContext = null!
                }
            ],
            TriggerQueue =
            [
                new TriggerQueueItemState(
                    "trigger-1",
                    "alice",
                    sourceObjectId: "obj-1",
                    effectKind: "LAST_BREATH",
                    triggeredByEventKind: "OBJECT_DESTROYED")
                {
                    TriggerId = " trigger-1 ",
                    SourceObjectId = " obj-1 ",
                    EffectKind = " LAST_BREATH ",
                    TriggeredByEventKind = " OBJECT_DESTROYED "
                },
                new TriggerQueueItemState(
                    "trigger-1",
                    "bob",
                    sourceObjectId: "obj-2",
                    effectKind: "LAST_BREATH",
                    triggeredByEventKind: "OBJECT_DESTROYED")
                {
                    SourceObjectId = " ",
                    EffectKind = "",
                    TriggeredByEventKind = ""
                }
            ]
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item id stack-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 effect kind SPELL has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 effect kind is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 source object obj-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 source object value is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 card no CARD-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 card no value is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 destination GRAVEYARD has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 destination is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 timing context OPEN_MAIN has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 timing context value is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 target object target-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 target object is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 optional cost list is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 damage amount -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 effect repeat count 0 is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item id trigger-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 effect kind LAST_BREATH has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 source object obj-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 source object is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 triggered event kind OBJECT_DESTROYED has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 triggered event kind is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStatePendingPlayersOutsideSeats()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            pendingPayment: new PendingPaymentState(
                "payment-1",
                "PAY_COST",
                "charlie",
                manaCost: 1,
                legalPaymentChoiceIds: ["SPEND_MANA:1"],
                reason: "test"),
            pendingHandChoice: new PendingHandChoiceState(
                "choice-1",
                "CHOOSE_HAND_CARDS",
                "diana",
                requiredCount: 1,
                maxCount: 1,
                legalObjectIds: ["obj-1"],
                reason: "test",
                sourceObjectId: "obj-2",
                effectKind: "DRAW_DISCARD"),
            temporaryPaymentResources:
            [
                new TemporaryPaymentResourceState(
                    "temp-1",
                    "eve",
                    "obj-3",
                    "ability-1",
                    "PAY_COST",
                    generatedPower: 1,
                    remainingPower: 1,
                    allowedPaymentKinds: ["power"],
                    createdTick: 0)
            ]);

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice player diana is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 owner player eve is missing from seats", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStatePendingPaymentValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            PendingPayment = new PendingPaymentState(
                "payment-1",
                "PAY_COST",
                "alice",
                manaCost: 1,
                powerCost: 1,
                powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["red"] = 1
                },
                legalPaymentChoiceIds: ["choice-1"],
                reason: "test",
                paymentResourceActionIds: ["TEMP_PAYMENT_RESOURCE:temp-1"])
            {
                PaymentId = " payment-1 ",
                PaymentWindow = " PAY_COST ",
                Reason = " test ",
                ManaCost = -1,
                PowerCost = -2,
                PowerCostByTrait = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [" red "] = 0,
                    ["red"] = 1
                },
                LegalPaymentChoiceIds = [" choice-1 ", "choice-1", ""],
                PaymentResourceActionIds = null!
            }
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment id payment-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 window PAY_COST has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 reason test has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 mana cost -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 power cost -2 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 power cost trait red value 0 must be positive", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 power cost trait red is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 legal payment choice choice-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 legal payment choice choice-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 legal payment choice is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending payment payment-1 payment resource action list is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStatePendingHandChoiceValueDrift()
    {
        var malformedListState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            PendingHandChoice = new PendingHandChoiceState(
                "choice-1",
                "CHOOSE_HAND_CARDS",
                "alice",
                requiredCount: 2,
                maxCount: 2,
                legalObjectIds: ["hand-1", "hand-2"],
                reason: "test",
                sourceObjectId: "source-1",
                effectKind: "DRAW_DISCARD")
            {
                ChoiceId = " choice-1 ",
                ChoiceWindow = " CHOOSE_HAND_CARDS ",
                Reason = " ",
                SourceObjectId = null!,
                EffectKind = " DRAW_DISCARD ",
                RequiredCount = 2,
                MaxCount = 1,
                LegalObjectIds = [" hand-1 ", "hand-1", ""]
            }
        };
        var invalidRequiredState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            PendingHandChoice = new PendingHandChoiceState(
                "choice-2",
                "CHOOSE_HAND_CARDS",
                "alice",
                requiredCount: 1,
                maxCount: 1,
                legalObjectIds: ["hand-1"],
                reason: "test",
                sourceObjectId: "source-1",
                effectKind: "DRAW_DISCARD")
            {
                RequiredCount = 0,
                MaxCount = -1,
                LegalObjectIds = null!
            }
        };

        var errors = new List<string>();
        errors.AddRange(MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            malformedListState,
            currentTick: 0));
        errors.AddRange(MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            invalidRequiredState,
            currentTick: 0));

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice id choice-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 window CHOOSE_HAND_CARDS has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 reason is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 source object value is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 effect kind DRAW_DISCARD has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 max count 1 is less than required count 2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 legal object hand-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 legal object hand-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 legal object is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 legal object count 1 is less than required count 2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-2 required count 0 is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-2 max count -1 is less than required count 0", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-2 legal object list is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateUntilEndOfTurnEffectValueDrift()
    {
        var malformedListState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            untilEndOfTurnEffects: ["STUNNED"])
        {
            UntilEndOfTurnEffects = [" STUNNED ", "STUNNED", ""]
        };
        var nullListState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            UntilEndOfTurnEffects = null!
        };

        var errors = new List<string>();
        errors.AddRange(MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            malformedListState,
            currentTick: 0));
        errors.AddRange(MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            nullListState,
            currentTick: 0));

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state until end of turn effect STUNNED has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state until end of turn effect STUNNED is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state until end of turn effect is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state until end of turn effect list is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateCardObjectValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new(
                    "obj-1",
                    damage: 1,
                    untilEndOfTurnEffects: ["STUNNED"],
                    tags: ["attacker"],
                    manaCost: 1)
                {
                    CardNo = " CARD-1 ",
                    AttachedToObjectId = "",
                    Damage = -1,
                    ManaCost = -2,
                    UntilEndOfTurnEffects = [" STUNNED ", "STUNNED", ""],
                    Tags = [" attacker ", "attacker", ""]
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 damage -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 card no CARD-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 attached object is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 mana cost -2 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 until end of turn effect STUNNED has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 until end of turn effect STUNNED is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 until end of turn effect is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 tag attacker has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 tag attacker is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 tag is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateCardObjectPowerModifierValueDrift()
    {
        var malformedLedgerState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new(
                    "obj-1",
                    power: 3,
                    untilEndOfTurnPowerModifier: 1,
                    tags: ["unit"],
                    untilEndOfTurnPowerModifiers:
                    [
                        new PowerModifierLedgerEntry(
                            "power-1",
                            "TEST_POWER",
                            "UNTIL_END_OF_TURN",
                            "obj-1",
                            "source-1",
                            "TEST-SOURCE",
                            1,
                            2,
                            3,
                            "Test.Path",
                            1,
                            0,
                            3,
                            1)
                    ])
                {
                    UntilEndOfTurnPowerModifiers =
                    [
                        new PowerModifierLedgerEntry(
                            "power-1",
                            "TEST_POWER",
                            "UNTIL_END_OF_TURN",
                            "obj-1",
                            "source-1",
                            "TEST-SOURCE",
                            1,
                            2,
                            3,
                            "Test.Path",
                            1,
                            0,
                            3,
                            1)
                        {
                            EffectId = " power-1 ",
                            EffectKind = " TEST_POWER ",
                            Duration = " UNTIL_END_OF_TURN ",
                            TargetObjectId = " obj-1 ",
                            SourceObjectId = " source-1 ",
                            SourceCardNo = " TEST-SOURCE ",
                            SourcePath = " Test.Path ",
                            PowerDelta = 0,
                            MinimumPower = -1,
                            AppliedOrder = 1
                        },
                        new PowerModifierLedgerEntry(
                            "power-1",
                            "TEST_POWER",
                            "UNTIL_END_OF_TURN",
                            "other-1",
                            "source-1",
                            "TEST-SOURCE",
                            1,
                            2,
                            0,
                            "Test.Path",
                            1,
                            2,
                            1,
                            1)
                        {
                            SourceObjectId = "",
                            SourceCardNo = " "
                        },
                        new PowerModifierLedgerEntry(
                            "power-2",
                            "TEST_POWER",
                            "UNTIL_END_OF_TURN",
                            "obj-1",
                            "source-1",
                            "TEST-SOURCE",
                            1,
                            2,
                            3,
                            "Test.Path",
                            1,
                            0,
                            3,
                            1)
                        {
                            AppliedOrder = 0
                        },
                        null!
                    ]
                }
            }
        };
        var nullLedgerState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["obj-2"] = new("obj-2")
                {
                    UntilEndOfTurnPowerModifiers = null!
                }
            }
        };

        var errors = new List<string>();
        errors.AddRange(MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            malformedLedgerState,
            currentTick: 0));
        errors.AddRange(MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            nullLedgerState,
            currentTick: 0));

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier id power-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 effect kind TEST_POWER has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 duration UNTIL_END_OF_TURN has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 target object obj-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 source path Test.Path has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 source object source-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 source object is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 source card no TEST-SOURCE has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 source card no is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 target object other-1 does not match card object", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 power delta 0 is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 minimum power -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-1 resulting power 1 is less than minimum power 2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier power-2 applied order 0 is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier applied order 1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-1 power modifier is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object obj-2 power modifier list is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateObjectRegistryMapKeyDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [" obj-1 "] = new("obj-1"),
                ["obj-1"] = new("obj-1"),
                [""] = new("blank-object")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [" obj-1 "] = new("alice", "HAND"),
                ["obj-1"] = new("alice", "HAND"),
                [""] = new("alice", "HAND")
            }
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object map key obj-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object map key obj-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object map key is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location map key obj-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location map key obj-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location map key is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateObjectReferencesOutsideRegistry()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["known-1"] = new("known-1", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["known-1"] = new("alice", "HAND")
            },
            stackItems:
            [
                new StackItemState(
                    "stack-1",
                    "alice",
                    sourceObjectId: "missing-stack-source",
                    effectKind: "DAMAGE",
                    targetObjectIds: ["missing-stack-target"])
            ],
            triggerQueue:
            [
                new TriggerQueueItemState(
                    "trigger-1",
                    "bob",
                    sourceObjectId: "missing-trigger-source",
                    effectKind: "LAST_BREATH",
                    triggeredByEventKind: "OBJECT_DESTROYED")
            ],
            pendingHandChoice: new PendingHandChoiceState(
                "choice-1",
                "CHOOSE_HAND_CARDS",
                "alice",
                requiredCount: 1,
                maxCount: 1,
                legalObjectIds: ["missing-choice-legal"],
                reason: "test",
                sourceObjectId: "missing-choice-source",
                effectKind: "DRAW_DISCARD"),
            temporaryPaymentResources:
            [
                new TemporaryPaymentResourceState(
                    "temp-1",
                    "bob",
                    "missing-temp-source",
                    "ability-1",
                    "PAY_COST",
                    generatedPower: 1,
                    remainingPower: 1,
                    allowedPaymentKinds: ["power"],
                    createdTick: 0)
            ]);

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 source object missing-stack-source is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state stack item stack-1 target object missing-stack-target is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state trigger queue item trigger-1 source object missing-trigger-source is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 source object missing-choice-source is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state pending hand choice choice-1 legal object missing-choice-legal is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 source object missing-temp-source is missing from object registry", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateScalarValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            RoomId = " room-a ",
            Tick = -1,
            TurnNumber = 0,
            Status = " IN_PROGRESS ",
            Phase = "BAD_PHASE",
            TimingState = " BAD_TIMING ",
            RngCursor = -1
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state room id room-a has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state tick -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state turn number 0 is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state status IN_PROGRESS has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state phase BAD_PHASE is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state timing state BAD_TIMING has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state timing state BAD_TIMING is invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state rng cursor -1 cannot be negative", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateResourceValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            2,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["alice"] = new RunePool(0, 0)
                {
                    Mana = -1,
                    Power = -2,
                    PowerByTrait = new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [" blue "] = -1,
                        ["blue"] = 1
                    }
                }
            },
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = -1
            },
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = -2
            },
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = 0
            },
            TemporaryPaymentResources =
            [
                new TemporaryPaymentResourceState("temp-1", "alice", remainingPower: 1)
                {
                    SourceObjectId = " source-1 ",
                    AbilityId = null!,
                    PaymentWindow = " ",
                    GeneratedPower = -1,
                    RemainingPower = -2,
                    CreatedTick = 3,
                    GeneratedPowerByTrait = new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [" red "] = 0,
                        ["red"] = 1
                    },
                    RemainingPowerByTrait = new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [" green "] = -1,
                        ["green"] = 1
                    },
                    AllowedPaymentKinds = [" power ", "power", ""]
                }
            ]
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 2);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state rune pool for alice mana -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state rune pool for alice power -2 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state rune pool for alice power trait blue value -1 must be positive", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state rune pool for alice power trait blue is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state score for alice -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state experience for alice -2 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state cards played this turn for alice 0 must be positive", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 generated power -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 source object source-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 ability id value is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 payment window is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 remaining power -2 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 created tick 3 is after authoritative state tick 2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 generated power trait red value 0 must be positive", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 generated power trait red is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 remaining power trait green value -1 must be positive", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 remaining power trait green is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state temporary payment resource temp-1 allowed payment kind power is duplicated", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateObjectIdentityAndLocationReferenceDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["known-1"] = new(
                    "known-mismatch",
                    attachedToObjectId: "missing-attach",
                    ownerId: "alice",
                    controllerId: "alice"),
                ["unit-1"] = new("unit-1", ownerId: "alice", controllerId: "alice"),
                ["blank-battlefield"] = new("blank-battlefield", ownerId: "alice", controllerId: "alice")
            })
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["known-1"] = new("alice", "HAND"),
                ["unit-1"] = new("alice", "BATTLEFIELD", "missing-battlefield")
                {
                    BattlefieldObjectId = " missing-battlefield "
                },
                ["blank-battlefield"] = new("alice", "BATTLEFIELD")
                {
                    BattlefieldObjectId = ""
                }
            }
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object known-1 object id known-mismatch does not match map key", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state card object known-1 attached object missing-attach is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location unit-1 battlefield object missing-battlefield has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location unit-1 battlefield object missing-battlefield is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location blank-battlefield battlefield object is blank", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateObjectLocationUnknownZone()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("obj-1", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("alice", "UNKNOWN_ZONE")
            });

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location obj-1 zone UNKNOWN_ZONE is not supported", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStatePlayerZoneValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob")
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Hand = [" obj-1 ", "obj-1", ""],
                    Graveyard = null!
                },
                ["bob"] = PlayerZones.Empty
            }
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state player zones alice/HAND object obj-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state player zones object obj-1 is duplicated between alice/HAND and alice/HAND", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state player zones alice/HAND object id is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state player zones alice/GRAVEYARD list is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateObjectLocationPlayerZoneDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Hand = ["obj-1", "dup-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["dup-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("obj-1", ownerId: "alice", controllerId: "alice"),
                ["dup-1"] = new("dup-1", ownerId: "alice", controllerId: "alice"),
                ["missing-zone-1"] = new("missing-zone-1", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("alice", "GRAVEYARD"),
                ["dup-1"] = new("alice", "HAND"),
                ["missing-zone-1"] = new("alice", "HAND")
            });

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state player zones object dup-1 is duplicated between alice/HAND and bob/BATTLEFIELD", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location missing-zone-1 is missing from player zones", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state object location obj-1 alice/GRAVEYARD disagrees with player zones alice/HAND", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateResolutionPlayerAndObjectReferenceDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            0,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["battlefield-1", "attacker-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["defender-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("battlefield-1", ownerId: "alice", controllerId: "alice"),
                ["attacker-1"] = new("attacker-1", ownerId: "alice", controllerId: "alice"),
                ["defender-1"] = new("defender-1", ownerId: "bob", controllerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("alice", "BATTLEFIELD", "battlefield-1"),
                ["attacker-1"] = new("alice", "BATTLEFIELD", "battlefield-1"),
                ["defender-1"] = new("bob", "BATTLEFIELD", "battlefield-1")
            },
            battlefieldResolutions:
            [
                new(
                    "battlefield-resolution-1",
                    0,
                    "CONTROL_CHANGED",
                    "test",
                    "missing-battlefield",
                    "charlie",
                    "diana",
                    "eve",
                    "missing-source",
                    ["missing-participant"],
                    ["BATTLEFIELD_CONTROL_CHANGED"])
            ],
            battleResolutions:
            [
                new(
                    "battle-resolution-1",
                    0,
                    "CLOSED",
                    "test",
                    "missing-battlefield-2",
                    "frank",
                    "gina",
                    "henry",
                    ["missing-attacker"],
                    ["missing-defender"],
                    ["missing-surviving-attacker"],
                    ["missing-surviving-defender"],
                    ["missing-destroyed"],
                    ["BATTLE_CLOSED"])
            ]);

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 0);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 player charlie is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 controller player eve is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 attacking player frank is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 winner player henry is missing from seats", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 battlefield object missing-battlefield is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 source object missing-source is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 battlefield object missing-battlefield-2 is missing from object registry", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 destroyed object missing-destroyed is missing from object registry", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryValueDrift()
    {
        var authoritativeState = new MatchState(
            "room-a",
            2,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            turnPlayerId: "bob",
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("battlefield-1", ownerId: "alice", controllerId: "alice")
            })
        {
            BattlefieldResolutions =
            [
                new(
                    " battlefield-resolution-1 ",
                    -1,
                    " HELD ",
                    " ",
                    "battlefield-1",
                    "alice",
                    null,
                    "alice",
                    " missing-source ",
                    [" participant-1 ", "participant-1", ""],
                    [" BATTLEFIELD_HELD ", "BATTLEFIELD_HELD"]),
                new(
                    "battlefield-resolution-1",
                    3,
                    "CONQUERED",
                    "test",
                    "battlefield-1",
                    "alice",
                    null,
                    "alice",
                    "",
                    [],
                    ["BATTLEFIELD_CONQUERED"])
            ],
            BattleResolutions =
            [
                new(
                    " battle-resolution-1 ",
                    -2,
                    " CLOSED ",
                    "",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    [" attacker-1 ", "attacker-1", ""],
                    [],
                    [],
                    [],
                    [],
                    [" BATTLE_CLOSED ", "BATTLE_CLOSED"]),
                new(
                    "battle-resolution-1",
                    4,
                    "NO_RESULT",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    null,
                    [],
                    [],
                    [],
                    [],
                    [],
                    ["BATTLE_NO_RESULT"])
            ]
        };

        var errors = MatchRecoveryValidator.Validate(
            "room-a",
            0,
            [],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            authoritativeState,
            currentTick: 2);

        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 id has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 tick -1 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 tick 3 is after authoritative state tick 2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 id is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 kind HELD has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 reason is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 source object missing-source has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 source object is blank", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 participant object participant-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 participant object participant-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 participant object is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battlefield resolution battlefield-resolution-1 related event kind BATTLEFIELD_HELD is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 tick -2 cannot be negative", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 tick 4 is after authoritative state tick 2", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 id is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 kind CLOSED has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 reason is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 attacker object attacker-1 has surrounding whitespace", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 attacker object attacker-1 is duplicated", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 attacker object is required", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("authoritative state battle resolution battle-resolution-1 related event kind BATTLE_CLOSED is duplicated", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotLanePayloadMismatch()
    {
        const string battlefieldObjectId = "battlefield-a";
        const string aliceUnitObjectId = "alice-unit-a";
        const string aliceHiddenStandbyObjectId = "alice-standby-a";
        const string bobUnitObjectId = "bob-unit-a";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, aliceUnitObjectId, aliceHiddenStandbyObjectId]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = [bobUnitObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "TEST-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [aliceUnitObjectId] = new(
                    aliceUnitObjectId,
                    power: 3,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard]),
                [aliceHiddenStandbyObjectId] = new(
                    aliceHiddenStandbyObjectId,
                    power: 2,
                    isFaceDown: true,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                [bobUnitObjectId] = new(
                    bobUnitObjectId,
                    power: 2,
                    ownerId: "bob",
                    controllerId: "bob",
                    tags: [CardObjectTags.UnitCard])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [aliceUnitObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [aliceHiddenStandbyObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [bobUnitObjectId] = new("bob", "BATTLEFIELD", battlefieldObjectId)
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
        var lanes = spectatorReplayFrame.SpectatorSnapshot.Lanes.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        lanes["battlefieldCount"] = 1;

        var battlefieldObjectItems = Assert.IsAssignableFrom<IReadOnlyList<object?>>(lanes["battlefieldObjectIds"])
            .ToArray();
        battlefieldObjectItems[0] = new Dictionary<string, object?>
        {
            ["playerId"] = "bob",
            ["objectId"] = "wrong-object"
        };
        lanes["battlefieldObjectIds"] = battlefieldObjectItems;

        var battlefieldItems = Assert.IsAssignableFrom<IReadOnlyList<object?>>(lanes["battlefields"])
            .ToArray();
        var firstBattlefield = Assert.IsType<Dictionary<string, object?>>(battlefieldItems[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        firstBattlefield["battlefieldObjectId"] = "wrong-battlefield";
        battlefieldItems[0] = firstBattlefield;
        lanes["battlefields"] = battlefieldItems;
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Lanes = lanes
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
            error => error.Contains("spectator replay frame snapshot lane battlefield count 1 does not match authoritative state battlefield object count 3", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield object ids disagree with authoritative state battlefield object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefields disagree with authoritative state battlefields", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldScalarMismatch()
    {
        const string battlefieldObjectId = "battlefield-a";
        const string aliceHiddenStandbyObjectId = "alice-standby-a";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, aliceHiddenStandbyObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "TEST-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [aliceHiddenStandbyObjectId] = new(
                    aliceHiddenStandbyObjectId,
                    power: 2,
                    isFaceDown: true,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [aliceHiddenStandbyObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId)
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
        var lanes = spectatorReplayFrame.SpectatorSnapshot.Lanes.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var battlefieldItems = Assert.IsAssignableFrom<IReadOnlyList<object?>>(lanes["battlefields"])
            .ToArray();
        var battlefieldPayload = Assert.IsType<Dictionary<string, object?>>(battlefieldItems[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        battlefieldPayload["zonePlayerId"] = "bob";
        battlefieldPayload["cardNo"] = "OTHER-BATTLEFIELD";
        battlefieldPayload["controllerId"] = "bob";
        battlefieldPayload["status"] = "CONTESTED";
        battlefieldPayload["contested"] = true;
        battlefieldPayload["standbySlotCount"] = 0;
        battlefieldPayload["faceDownStandbyCount"] = 0;
        battlefieldPayload["hiddenStandbyCount"] = 0;
        battlefieldItems[0] = battlefieldPayload;
        lanes["battlefields"] = battlefieldItems;
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Lanes = lanes
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
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a zone player does not match authoritative state zone player", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a card number does not match authoritative state card number", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a controller does not match authoritative state controller", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a status does not match authoritative state status", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a contested does not match authoritative state contested", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot count does not match authoritative state standby slot count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a face-down standby count does not match authoritative state face-down standby count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a hidden standby count does not match authoritative state hidden standby count", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldListMismatch()
    {
        const string battlefieldObjectId = "battlefield-a";
        const string aliceUnitObjectId = "alice-unit-a";
        const string bobUnitObjectId = "bob-unit-a";
        const string aliceHiddenStandbyObjectId = "alice-standby-a";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, aliceUnitObjectId, aliceHiddenStandbyObjectId]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = [bobUnitObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "TEST-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [aliceUnitObjectId] = new(
                    aliceUnitObjectId,
                    power: 3,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard]),
                [bobUnitObjectId] = new(
                    bobUnitObjectId,
                    power: 2,
                    ownerId: "bob",
                    controllerId: "bob",
                    tags: [CardObjectTags.UnitCard]),
                [aliceHiddenStandbyObjectId] = new(
                    aliceHiddenStandbyObjectId,
                    power: 2,
                    isFaceDown: true,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [aliceUnitObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [bobUnitObjectId] = new("bob", "BATTLEFIELD", battlefieldObjectId),
                [aliceHiddenStandbyObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.ScoreGainedThisTurn(battlefieldObjectId, "alice")]);
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
        var lanes = spectatorReplayFrame.SpectatorSnapshot.Lanes.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var battlefieldItems = Assert.IsAssignableFrom<IReadOnlyList<object?>>(lanes["battlefields"])
            .ToArray();
        var battlefieldPayload = Assert.IsType<Dictionary<string, object?>>(battlefieldItems[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        battlefieldPayload["occupantObjectIds"] = new[] { "wrong-unit" };
        battlefieldPayload["occupantControllerIds"] = new[] { "alice" };
        battlefieldPayload["unitsBySide"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["alice"] = new[] { "wrong-unit" }
        };
        battlefieldPayload["standbyObjectIds"] = new[] { aliceHiddenStandbyObjectId };
        battlefieldPayload["scoredThisTurnPlayerIds"] = new[] { "bob" };
        battlefieldPayload["pendingTaskKinds"] = new[] { "wrong-task" };
        battlefieldItems[0] = battlefieldPayload;
        lanes["battlefields"] = battlefieldItems;
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Lanes = lanes
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
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a occupant object ids do not match authoritative state occupant object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a occupant controller ids do not match authoritative state occupant controller ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a units by side do not match authoritative state units by side", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby object ids do not match authoritative state visible standby object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a scored player ids do not match authoritative state scored player ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a pending task kinds do not match authoritative state pending task kinds", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStandbySlotMismatch()
    {
        const string battlefieldObjectId = "battlefield-a";
        const string hiddenStandbyObjectId = "standby-hidden-a";
        const string visibleStandbyObjectId = "standby-visible-a";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, hiddenStandbyObjectId, visibleStandbyObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "TEST-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [hiddenStandbyObjectId] = new(
                    hiddenStandbyObjectId,
                    power: 2,
                    isFaceDown: true,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                [visibleStandbyObjectId] = new(
                    visibleStandbyObjectId,
                    power: 2,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [hiddenStandbyObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [visibleStandbyObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId)
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
        var lanes = spectatorReplayFrame.SpectatorSnapshot.Lanes.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var battlefieldItems = Assert.IsAssignableFrom<IReadOnlyList<object?>>(lanes["battlefields"])
            .ToArray();
        var battlefieldPayload = Assert.IsType<Dictionary<string, object?>>(battlefieldItems[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var standbySlots = Assert.IsAssignableFrom<IReadOnlyList<object?>>(battlefieldPayload["standbySlots"])
            .ToArray();
        var hiddenSlot = Assert.IsType<Dictionary<string, object?>>(standbySlots[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        hiddenSlot["slotId"] = "wrong-slot";
        hiddenSlot["battlefieldObjectId"] = "wrong-battlefield";
        hiddenSlot["sidePlayerId"] = "bob";
        hiddenSlot["controllerId"] = "bob";
        hiddenSlot["visible"] = true;
        hiddenSlot["state"] = "VISIBLE";
        hiddenSlot["isFaceDown"] = false;
        hiddenSlot["objectId"] = hiddenStandbyObjectId;
        standbySlots[0] = hiddenSlot;

        var visibleSlot = Assert.IsType<Dictionary<string, object?>>(standbySlots[1])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        visibleSlot["objectId"] = "wrong-visible-standby";
        standbySlots[1] = visibleSlot;
        battlefieldPayload["standbySlots"] = standbySlots;
        battlefieldItems[0] = battlefieldPayload;
        lanes["battlefields"] = battlefieldItems;
        spectatorReplayFrame = spectatorReplayFrame with
        {
            SpectatorSnapshot = spectatorReplayFrame.SpectatorSnapshot with
            {
                Lanes = lanes
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
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 slot id does not match authoritative state slot id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 battlefield id does not match authoritative state battlefield id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 side player does not match authoritative state side player", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 controller does not match authoritative state controller", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 visibility does not match authoritative spectator visibility", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 state does not match authoritative spectator state", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 face-down flag does not match authoritative state face-down flag", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:1 hidden object id must be redacted", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot lane battlefield battlefield-a standby slot battlefield-a:standby:2 object id does not match authoritative visible standby object id", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackControllerIdsMismatch()
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
            ["controllerId"] = "bob"
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
            error => error.Contains("spectator replay frame snapshot stack controller ids disagree with authoritative state stack controller ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackSourceObjectIdsMismatch()
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
            ["sourceObjectId"] = "spell-2"
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
            error => error.Contains("spectator replay frame snapshot stack source object ids disagree with authoritative state stack source object ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackEffectKindsMismatch()
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
            ["effectKind"] = "DISCARD"
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
            error => error.Contains("spectator replay frame snapshot stack effect kinds disagree with authoritative state stack effect kinds", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackCardNumbersMismatch()
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
            ["cardNo"] = "SFD-002"
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
            error => error.Contains("spectator replay frame snapshot stack card numbers disagree with authoritative state stack card numbers", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackTargetObjectIdsMismatch()
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
                    cardNo: "SFD-001",
                    targetObjectIds: ["target-1"])
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
            ["targetObjectIds"] = new[] { "target-2" }
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
            error => error.Contains("spectator replay frame snapshot stack target object ids disagree with authoritative state stack target object ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackDamageAmountsMismatch()
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
                    effectKind: "DAMAGE",
                    cardNo: "SFD-001",
                    targetObjectIds: ["target-1"],
                    damageAmount: 3)
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
            ["damageAmount"] = 4
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
            error => error.Contains("spectator replay frame snapshot stack damage amounts disagree with authoritative state stack damage amounts", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotStackDestinationsMismatch()
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
                    effectKind: "MOVE",
                    cardNo: "SFD-001",
                    destination: "discard")
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
            ["destination"] = "banish"
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
            error => error.Contains("spectator replay frame snapshot stack destinations disagree with authoritative state stack destinations", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerScalarMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = new(
                    MainDeck: [],
                    RuneDeck: [],
                    Hand: ["alice-hand-1", "alice-hand-2"],
                    Base: [],
                    Battlefields: [],
                    Graveyard: [],
                    Banished: [],
                    LegendZone: [],
                    ChampionZone: []),
                ["bob"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = 2
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = 3
            },
            playerCardsPlayedThisTurn: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = 1
            },
            playerDecklists: new Dictionary<string, OfficialDecklist>(StringComparer.Ordinal)
            {
                ["alice"] = new("legend", "champion", [], [], [])
            },
            mulliganCompletedPlayerIds: ["alice"]);
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
        var alicePayload = Assert.IsType<Dictionary<string, object?>>(players["alice"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        alicePayload["id"] = "other-alice";
        alicePayload["name"] = "other-alice";
        alicePayload["ready"] = false;
        alicePayload["handSize"] = 0;
        alicePayload["score"] = 0;
        alicePayload["experience"] = 0;
        alicePayload["cardsPlayedThisTurn"] = 0;
        alicePayload["deckSubmitted"] = false;
        alicePayload["mulliganCompleted"] = false;
        players["alice"] = alicePayload;
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
            error => error.Contains("spectator replay frame snapshot player alice payload id does not match player key", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice name does not match player id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice ready does not match authoritative state ready", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice hand size does not match authoritative state hand size", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice score does not match authoritative state score", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice experience does not match authoritative state experience", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice cards played this turn does not match authoritative state cards played this turn", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice deck submitted does not match authoritative state deck submitted", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice mulligan completed does not match authoritative state mulligan completed", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerRunePoolMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["alice"] = new(
                    mana: 2,
                    power: 3,
                    powerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 4,
                        [RuneTrait.Blue] = 1
                    })
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
        var alicePayload = Assert.IsType<Dictionary<string, object?>>(players["alice"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var runePool = Assert.IsType<Dictionary<string, object?>>(alicePayload["runePool"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        runePool["mana"] = 9;
        runePool["power"] = 10;
        runePool["untypedPower"] = 11;
        runePool["powerByTrait"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [RuneTrait.Red] = 99,
            [RuneTrait.Green] = 1
        };
        alicePayload["runePool"] = runePool;
        players["alice"] = alicePayload;
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
            error => error.Contains("spectator replay frame snapshot player alice rune pool mana does not match authoritative rune pool mana", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice rune pool total power does not match authoritative rune pool total power", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice rune pool untyped power does not match authoritative rune pool untyped power", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice rune pool power by trait does not match authoritative rune pool power by trait", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerZoneMismatch()
    {
        const string hiddenStandbyObjectId = "alice-hidden-standby-1";
        const string visibleBattlefieldObjectId = "alice-visible-battlefield-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    MainDeck = ["alice-main-1"],
                    RuneDeck = ["alice-rune-1"],
                    Hand = ["alice-hand-1"],
                    Base = ["alice-base-1"],
                    Battlefields = [visibleBattlefieldObjectId, hiddenStandbyObjectId],
                    Graveyard = ["alice-grave-1"],
                    Banished = ["alice-banished-1"],
                    LegendZone = ["alice-legend-1"],
                    ChampionZone = ["alice-champion-1"]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["alice-base-1"] = new("alice-base-1", cardNo: "SFD-BASE", ownerId: "alice", controllerId: "alice"),
                [visibleBattlefieldObjectId] = new(visibleBattlefieldObjectId, cardNo: "SFD-VISIBLE", ownerId: "alice", controllerId: "alice"),
                [hiddenStandbyObjectId] = new(
                    hiddenStandbyObjectId,
                    isFaceDown: true,
                    cardNo: "SFD-HIDDEN",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["alice-grave-1"] = new("alice-grave-1", cardNo: "SFD-GRAVE", ownerId: "alice", controllerId: "alice"),
                ["alice-banished-1"] = new("alice-banished-1", cardNo: "SFD-BANISHED", ownerId: "alice", controllerId: "alice"),
                ["alice-legend-1"] = new("alice-legend-1", cardNo: "SFD-LEGEND", ownerId: "alice", controllerId: "alice"),
                ["alice-champion-1"] = new("alice-champion-1", cardNo: "SFD-CHAMPION", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [hiddenStandbyObjectId] = new("alice", "BATTLEFIELD", visibleBattlefieldObjectId)
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
        var alicePayload = Assert.IsType<Dictionary<string, object?>>(players["alice"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var zones = Assert.IsType<Dictionary<string, object?>>(alicePayload["zones"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        zones["mainDeckCount"] = 2;
        zones["runeDeckCount"] = 2;
        zones["hand"] = new[] { "alice-hand-1" };
        zones["handHidden"] = 2;
        zones["base"] = new[] { "alice-base-drift" };
        zones["battlefields"] = new[] { visibleBattlefieldObjectId, hiddenStandbyObjectId };
        zones["battlefieldHiddenStandbyCount"] = 0;
        zones["graveyard"] = new[] { "alice-grave-drift" };
        zones["banished"] = new[] { "alice-banished-drift" };
        zones["legendZone"] = new[] { "alice-legend-drift" };
        zones["championZone"] = new[] { "alice-champion-drift" };
        alicePayload["zones"] = zones;
        players["alice"] = alicePayload;
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
            error => error.Contains("spectator replay frame snapshot player alice main deck count does not match authoritative state main deck count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice rune deck count does not match authoritative state rune deck count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice hand objects must be redacted for spectator view", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice hidden hand count does not match authoritative state hand count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice base objects do not match authoritative state base objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice battlefield objects do not match authoritative state battlefield objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice hidden battlefield standby count does not match authoritative state hidden battlefield standby count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice graveyard objects do not match authoritative state graveyard objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice banished objects do not match authoritative state banished objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice legend zone objects do not match authoritative state legend zone objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice champion zone objects do not match authoritative state champion zone objects", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectCoverageAndRedactionMismatch()
    {
        const string handObjectId = "alice-hand-1";
        const string hiddenBaseObjectId = "alice-facedown-base-1";
        const string visibleBattlefieldObjectId = "alice-visible-battlefield-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Hand = [handObjectId],
                    Base = [hiddenBaseObjectId],
                    Battlefields = [visibleBattlefieldObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [handObjectId] = new(
                    handObjectId,
                    power: 4,
                    cardNo: "SFD-HAND",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard]),
                [hiddenBaseObjectId] = new(
                    hiddenBaseObjectId,
                    power: 5,
                    isFaceDown: true,
                    cardNo: "SFD-HIDDEN",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                [visibleBattlefieldObjectId] = new(
                    visibleBattlefieldObjectId,
                    power: 3,
                    cardNo: "SFD-VISIBLE",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard])
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
        var alicePayload = Assert.IsType<Dictionary<string, object?>>(players["alice"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objects = Assert.IsType<Dictionary<string, object?>>(alicePayload["objects"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        objects.Remove(visibleBattlefieldObjectId);
        objects[handObjectId] = new Dictionary<string, object?>
        {
            ["objectId"] = handObjectId,
            ["isFaceDown"] = false
        };
        var hiddenBasePayload = Assert.IsType<Dictionary<string, object?>>(objects[hiddenBaseObjectId])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        hiddenBasePayload["objectId"] = "wrong-hidden-object";
        hiddenBasePayload["isFaceDown"] = false;
        hiddenBasePayload["cardNo"] = "SFD-HIDDEN";
        hiddenBasePayload["tags"] = new[] { CardObjectTags.UnitCard };
        hiddenBasePayload["power"] = 5;
        objects[hiddenBaseObjectId] = hiddenBasePayload;
        alicePayload["objects"] = objects;
        players["alice"] = alicePayload;
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
            error => error.Contains("spectator replay frame snapshot player alice is missing visible object alice-visible-battlefield-1", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-hand-1 is not visible in authoritative spectator view", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-facedown-base-1 object id does not match authoritative object id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-facedown-base-1 face-down flag does not match authoritative spectator redaction", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice hidden face-down object alice-facedown-base-1 exposes private metadata", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectScalarMismatch()
    {
        const string visibleBattlefieldObjectId = "alice-visible-battlefield-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [visibleBattlefieldObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [visibleBattlefieldObjectId] = new(
                    visibleBattlefieldObjectId,
                    damage: 1,
                    isAttacking: true,
                    power: 6,
                    untilEndOfTurnPowerModifier: 2,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3,
                    attachedToObjectId: "alice-parent-1",
                    cardNo: "SFD-VISIBLE",
                    ownerId: "alice",
                    controllerId: "alice")
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
        var alicePayload = Assert.IsType<Dictionary<string, object?>>(players["alice"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objects = Assert.IsType<Dictionary<string, object?>>(alicePayload["objects"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var visiblePayload = Assert.IsType<Dictionary<string, object?>>(objects[visibleBattlefieldObjectId])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        visiblePayload["cardNo"] = "SFD-DRIFT";
        visiblePayload["ownerId"] = "bob";
        visiblePayload["controllerId"] = "bob";
        visiblePayload["attachedToObjectId"] = "alice-parent-drift";
        visiblePayload["damage"] = 2;
        visiblePayload["power"] = 7;
        visiblePayload["basePower"] = 8;
        visiblePayload["effectivePower"] = 9;
        visiblePayload["untilEndOfTurnPowerModifier"] = 3;
        visiblePayload["manaCost"] = 4;
        visiblePayload["isExhausted"] = false;
        visiblePayload["isAttacking"] = false;
        visiblePayload["isDefending"] = true;
        objects[visibleBattlefieldObjectId] = visiblePayload;
        alicePayload["objects"] = objects;
        players["alice"] = alicePayload;
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
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 card number does not match authoritative object card number", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 owner id does not match authoritative object owner id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 controller id does not match authoritative object controller id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 attached object id does not match authoritative object attached object id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 damage does not match authoritative object damage", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 power does not match authoritative object power", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 base power does not match authoritative object base power", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 effective power does not match authoritative object effective power", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 until-end-of-turn power modifier does not match authoritative object until-end-of-turn power modifier", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 mana cost does not match authoritative object mana cost", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 exhausted state does not match authoritative object exhausted state", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 attacking state does not match authoritative object attacking state", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 defending state does not match authoritative object defending state", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectListMismatch()
    {
        const string visibleBattlefieldObjectId = "alice-visible-battlefield-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [visibleBattlefieldObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [visibleBattlefieldObjectId] = new(
                    visibleBattlefieldObjectId,
                    power: 6,
                    untilEndOfTurnEffects: ["EOT_POWER", "EOT_STUN"],
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    cardNo: "SFD-VISIBLE",
                    ownerId: "alice",
                    controllerId: "alice")
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
        var alicePayload = Assert.IsType<Dictionary<string, object?>>(players["alice"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objects = Assert.IsType<Dictionary<string, object?>>(alicePayload["objects"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var visiblePayload = Assert.IsType<Dictionary<string, object?>>(objects[visibleBattlefieldObjectId])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        visiblePayload["tags"] = new[] { CardObjectTags.UnitCard, CardObjectTags.Spellshield };
        visiblePayload["untilEndOfTurnEffects"] = new[] { "EOT_STUN", "EOT_DRIFT" };
        objects[visibleBattlefieldObjectId] = visiblePayload;
        alicePayload["objects"] = objects;
        players["alice"] = alicePayload;
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
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 tags do not match authoritative object tags", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 until-end-of-turn effects do not match authoritative object until-end-of-turn effects", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectLocationMismatch()
    {
        const string battlefieldObjectId = "alice-battlefield-card-1";
        const string visibleBattlefieldObjectId = "alice-visible-battlefield-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [visibleBattlefieldObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "SFD-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice"),
                [visibleBattlefieldObjectId] = new(
                    visibleBattlefieldObjectId,
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "SFD-VISIBLE",
                    ownerId: "alice",
                    controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [visibleBattlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId)
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
        var alicePayload = Assert.IsType<Dictionary<string, object?>>(players["alice"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objects = Assert.IsType<Dictionary<string, object?>>(alicePayload["objects"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var visiblePayload = Assert.IsType<Dictionary<string, object?>>(objects[visibleBattlefieldObjectId])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var locationPayload = Assert.IsType<Dictionary<string, object?>>(visiblePayload["location"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        locationPayload["playerId"] = "bob";
        locationPayload["zone"] = "BASE";
        locationPayload["battlefieldObjectId"] = "alice-battlefield-drift";
        visiblePayload["location"] = locationPayload;
        objects[visibleBattlefieldObjectId] = visiblePayload;
        alicePayload["objects"] = objects;
        players["alice"] = alicePayload;
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
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 location player id does not match authoritative object location player id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 location zone does not match authoritative object location zone", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame snapshot player alice object alice-visible-battlefield-1 location battlefield object id does not match authoritative object location battlefield object id", StringComparison.Ordinal));
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
    public void RecoveryValidatorRejectsSpectatorReplayTimingRoomStatusMismatch()
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
            status: MatchStatuses.InProgress);
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
        timing["roomStatus"] = MatchStatuses.Seating;
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
            error => error.Contains("spectator replay frame timing room status does not match authoritative state room status", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingReadyPlayersMismatch()
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
            status: MatchStatuses.Seating,
            readyPlayerIds: ["alice"]);
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
        timing["readyPlayerIds"] = new[] { "bob" };
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
            error => error.Contains("spectator replay frame timing ready players do not match authoritative state ready players", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingWinningScoreMismatch()
    {
        const string scoringBattlefieldObjectId = "alice-winning-score-battlefield-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [scoringBattlefieldObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [scoringBattlefieldObjectId] = new(
                    scoringBattlefieldObjectId,
                    cardNo: "OGN·276/298",
                    ownerId: "alice",
                    controllerId: "alice")
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
        timing["winningScore"] = 8;
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
            error => error.Contains("spectator replay frame timing winning score does not match authoritative state winning score", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskPayloadMismatch()
    {
        const string battlefieldObjectId = "alice-contested-battlefield-1";
        const string aliceUnitObjectId = "alice-battlefield-unit-1";
        const string bobUnitObjectId = "bob-battlefield-unit-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, aliceUnitObjectId]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = [bobUnitObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "SFD-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [aliceUnitObjectId] = new(
                    aliceUnitObjectId,
                    cardNo: "SFD-ALICE-UNIT",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard]),
                [bobUnitObjectId] = new(
                    bobUnitObjectId,
                    cardNo: "SFD-BOB-UNIT",
                    ownerId: "bob",
                    controllerId: "bob",
                    tags: [CardObjectTags.UnitCard])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [aliceUnitObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [bobUnitObjectId] = new("bob", "BATTLEFIELD", battlefieldObjectId)
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
        var battlefieldTasks = Assert.IsAssignableFrom<IEnumerable<object?>>(timing["battlefieldTasks"]).ToArray();
        var firstTask = Assert.IsType<Dictionary<string, object?>>(battlefieldTasks[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        firstTask["taskId"] = "task:drift";
        firstTask["kind"] = "DRIFT_KIND";
        firstTask["status"] = "DRIFT_STATUS";
        firstTask["reason"] = "DRIFT_REASON";
        firstTask["battlefieldObjectId"] = "battlefield-drift";
        firstTask["participantControllerIds"] = new[] { "alice" };
        firstTask["participantObjectIds"] = new[] { aliceUnitObjectId };
        firstTask["actingPlayerId"] = "bob";
        firstTask["stackItemIds"] = new[] { "stack-drift" };
        battlefieldTasks[0] = firstTask;
        timing["battlefieldTasks"] = battlefieldTasks;
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
            error => error.Contains("spectator replay frame timing battlefield task ids disagree with authoritative state battlefield task ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task kinds disagree with authoritative state battlefield task kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task statuses disagree with authoritative state battlefield task statuses", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task reasons disagree with authoritative state battlefield task reasons", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task battlefield object ids disagree with authoritative state battlefield task battlefield object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task participant controller ids disagree with authoritative state battlefield task participant controller ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task participant object ids disagree with authoritative state battlefield task participant object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task acting players disagree with authoritative state battlefield task acting players", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task stack item ids disagree with authoritative state battlefield task stack item ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskDerivedIdMismatch()
    {
        const string battlefieldObjectId = "alice-contested-battlefield-1";
        const string aliceUnitObjectId = "alice-battlefield-unit-1";
        const string bobUnitObjectId = "bob-battlefield-unit-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, aliceUnitObjectId]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = [bobUnitObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "SFD-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [aliceUnitObjectId] = new(
                    aliceUnitObjectId,
                    cardNo: "SFD-ALICE-UNIT",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard]),
                [bobUnitObjectId] = new(
                    bobUnitObjectId,
                    cardNo: "SFD-BOB-UNIT",
                    ownerId: "bob",
                    controllerId: "bob",
                    tags: [CardObjectTags.UnitCard])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [aliceUnitObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [bobUnitObjectId] = new("bob", "BATTLEFIELD", battlefieldObjectId)
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
        var battlefieldTasks = Assert.IsAssignableFrom<IEnumerable<object?>>(timing["battlefieldTasks"])
            .Select(task => Assert.IsType<Dictionary<string, object?>>(task)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal))
            .ToArray();
        var battleTask = battlefieldTasks.First(task => task.ContainsKey("battleId"));
        var spellDuelTask = battlefieldTasks.First(task => task.ContainsKey("spellDuelId"));
        battleTask["battleId"] = "battle:drift";
        spellDuelTask["spellDuelId"] = "spell-duel:drift";
        timing["battlefieldTasks"] = battlefieldTasks.Cast<object?>().ToArray();
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
            error => error.Contains("spectator replay frame timing battlefield task spell duel ids disagree with authoritative state battlefield task spell duel ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield task battle ids disagree with authoritative state battlefield task battle ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMismatch()
    {
        const string battlefieldObjectId = "alice-contested-battlefield-1";
        const string aliceUnitObjectId = "alice-battlefield-unit-1";
        const string bobUnitObjectId = "bob-battlefield-unit-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, aliceUnitObjectId]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = [bobUnitObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "SFD-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [aliceUnitObjectId] = new(
                    aliceUnitObjectId,
                    cardNo: "SFD-ALICE-UNIT",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard]),
                [bobUnitObjectId] = new(
                    bobUnitObjectId,
                    cardNo: "SFD-BOB-UNIT",
                    ownerId: "bob",
                    controllerId: "bob",
                    tags: [CardObjectTags.UnitCard])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [aliceUnitObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [bobUnitObjectId] = new("bob", "BATTLEFIELD", battlefieldObjectId)
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
        var pendingTaskQueue = Assert.IsType<Dictionary<string, object?>>(timing["pendingTaskQueue"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var metadata = Assert.IsType<Dictionary<string, object?>>(pendingTaskQueue["metadata"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        pendingTaskQueue["hasTasks"] = false;
        pendingTaskQueue["isBlocking"] = false;
        pendingTaskQueue["phase"] = "IDLE";
        pendingTaskQueue["activeTaskId"] = "task:drift";
        pendingTaskQueue["tasks"] = Array.Empty<object?>();
        metadata["taskCount"] = 0;
        metadata["stateBasedTaskKinds"] = new[] { "DESTROY_LETHAL_UNIT" };
        pendingTaskQueue["metadata"] = metadata;
        timing["pendingTaskQueue"] = pendingTaskQueue;
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
            error => error.Contains("spectator replay frame timing pending task queue has tasks does not match authoritative state pending task queue has tasks", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue blocking state does not match authoritative state pending task queue blocking state", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue phase does not match authoritative state pending task queue phase", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue active task id does not match authoritative state pending task queue active task id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task count does not match authoritative state pending task queue task count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue metadata task count does not match authoritative state pending task queue task count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue metadata state-based task kinds do not match authoritative state pending task queue state-based task kinds", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueTaskItemMismatch()
    {
        const string battlefieldObjectId = "alice-cleanup-battlefield-1";
        const string hiddenStandbyObjectId = "bob-hidden-standby-1";
        const string equipmentObjectId = "alice-unattached-equipment-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, equipmentObjectId]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = [hiddenStandbyObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "SFD-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [hiddenStandbyObjectId] = new(
                    hiddenStandbyObjectId,
                    power: 2,
                    isFaceDown: true,
                    ownerId: "bob",
                    controllerId: "bob",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                [equipmentObjectId] = new(
                    equipmentObjectId,
                    cardNo: "SFD-EQUIPMENT",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.EquipmentCard])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [hiddenStandbyObjectId] = new("bob", "BATTLEFIELD", battlefieldObjectId),
                [equipmentObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId)
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
        var pendingTaskQueue = Assert.IsType<Dictionary<string, object?>>(timing["pendingTaskQueue"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var tasks = Assert.IsAssignableFrom<IReadOnlyList<object?>>(pendingTaskQueue["tasks"])
            .ToArray();
        Assert.Equal(2, tasks.Length);

        var hiddenTask = Assert.IsType<Dictionary<string, object?>>(tasks[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        hiddenTask["taskId"] = $"cleanup:illegal-standby:{battlefieldObjectId}:{hiddenStandbyObjectId}";
        hiddenTask["kind"] = "DRIFT_KIND";
        hiddenTask["reason"] = "DRIFT_REASON";
        hiddenTask["playerId"] = "alice";
        hiddenTask["battlefieldObjectId"] = "wrong-battlefield";
        hiddenTask["objectId"] = hiddenStandbyObjectId;
        hiddenTask["hiddenObject"] = false;
        hiddenTask["hiddenObjectKind"] = "VISIBLE_OBJECT";
        tasks[0] = hiddenTask;

        var equipmentTask = Assert.IsType<Dictionary<string, object?>>(tasks[1])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        equipmentTask["objectId"] = "wrong-equipment";
        equipmentTask["hiddenObject"] = true;
        equipmentTask["hiddenObjectKind"] = "BATTLEFIELD_STANDBY";
        tasks[1] = equipmentTask;
        pendingTaskQueue["tasks"] = tasks;
        timing["pendingTaskQueue"] = pendingTaskQueue;
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
            error => error.Contains("spectator replay frame timing pending task queue task ids do not match authoritative state pending task queue task ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task kinds do not match authoritative state pending task queue task kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task reasons do not match authoritative state pending task queue task reasons", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task players do not match authoritative state pending task queue task players", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task battlefield object ids do not match authoritative state pending task queue task battlefield object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task object ids do not match authoritative state pending task queue task object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task hidden object flags do not match authoritative state pending task queue task hidden object flags", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending task queue task hidden object kinds do not match authoritative state pending task queue task hidden object kinds", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            pendingPayment: new PendingPaymentState(
                "payment-1",
                "PAY_COST",
                "alice",
                manaCost: 2,
                powerCost: 1,
                powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["blue"] = 1
                },
                legalPaymentChoiceIds: ["SPEND_MANA:2", "SPEND_POWER:1"],
                reason: "test-payment"));
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
        var pendingPayment = Assert.IsType<Dictionary<string, object?>>(timing["pendingPayment"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var cost = Assert.IsType<Dictionary<string, object?>>(pendingPayment["cost"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        pendingPayment["paymentId"] = "wrong-payment";
        pendingPayment["paymentWindow"] = "WRONG_WINDOW";
        pendingPayment["playerId"] = "bob";
        cost["mana"] = 9;
        cost["power"] = 8;
        cost["powerByTrait"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["green"] = 2
        };
        pendingPayment["cost"] = cost;
        pendingPayment["paymentChoices"] = new[] { "SPEND_MANA:9" };
        timing["pendingPayment"] = pendingPayment;
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
            error => error.Contains("spectator replay frame timing pending payment id does not match authoritative state pending payment id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending payment window does not match authoritative state pending payment window", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending payment player does not match authoritative state pending payment player", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending payment mana cost does not match authoritative state pending payment mana cost", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending payment power cost does not match authoritative state pending payment power cost", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending payment power cost traits do not match authoritative state pending payment power cost traits", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending payment choices do not match authoritative state pending payment choices", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentResourceActionsMismatch()
    {
        const string temporaryResourceId = "temp-payment-resource-1";
        var temporaryResourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResourceId);
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["alice"] = RunePool.Empty,
                ["bob"] = RunePool.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["source-1"] = new("source-1", ownerId: "alice", controllerId: "alice")
            },
            pendingPayment: new PendingPaymentState(
                "payment-1",
                "PAY_COST",
                "alice",
                powerCost: 2,
                legalPaymentChoiceIds: ["SPEND_POWER:any:2", "RECYCLE_RUNE:rune-1"],
                paymentResourceActionIds: ["MANUAL_RESOURCE_ACTION"],
                reason: "test-payment-resource-actions"),
            temporaryPaymentResources:
            [
                new TemporaryPaymentResourceState(
                    temporaryResourceId,
                    "alice",
                    "source-1",
                    "TEST_TEMP_RESOURCE_ABILITY",
                    "PAY_COST",
                    generatedPower: 2,
                    remainingPower: 2,
                    allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind],
                    createdTick: 1)
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var pendingPayment = Assert.IsType<Dictionary<string, object?>>(timing["pendingPayment"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Assert.Equal(
            ["MANUAL_RESOURCE_ACTION", "RECYCLE_RUNE:rune-1", temporaryResourceAction],
            Assert.IsType<string[]>(pendingPayment["paymentResourceActions"]));

        pendingPayment["paymentResourceActions"] = new[] { "wrong-resource-action" };
        timing["pendingPayment"] = pendingPayment;
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
            error => error.Contains("spectator replay frame timing pending payment resource actions do not match authoritative state pending payment resource actions", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Base = ["source-1", "target-1"]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["source-1"] = new("source-1", ownerId: "alice", controllerId: "alice"),
                ["target-1"] = new(
                    "target-1",
                    power: 5,
                    untilEndOfTurnPowerModifier: 2,
                    ownerId: "alice",
                    controllerId: "alice",
                    untilEndOfTurnPowerModifiers:
                    [
                        new PowerModifierLedgerEntry(
                            "effect-1",
                            "TEST_POWER_MODIFIER",
                            "UNTIL_END_OF_TURN",
                            "target-1",
                            "source-1",
                            "SRC-001",
                            powerDelta: 2,
                            basePower: 3,
                            effectivePower: 5,
                            sourcePath: "test-source-path",
                            requestedPowerDelta: 4,
                            minimumPower: 1,
                            resultingPower: 5,
                            appliedOrder: 7)
                    ])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["source-1"] = new("alice", "BASE"),
                ["target-1"] = new("alice", "BASE")
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
        var continuousEffects = Assert.IsAssignableFrom<IEnumerable<object?>>(timing["continuousEffects"])
            .ToArray();
        var effect = Assert.IsType<Dictionary<string, object?>>(continuousEffects[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Assert.Equal("FOUNDATION_ONLY", Assert.IsType<string>(effect["layerEngineStatus"]));
        Assert.Equal(7, Assert.IsType<int>(effect["appliedOrder"]));

        effect["effectId"] = "wrong-effect";
        effect["scope"] = "WRONG_SCOPE";
        effect["layer"] = "WRONG_LAYER";
        effect["duration"] = "WRONG_DURATION";
        effect["targetObjectId"] = "wrong-target";
        effect["sourceObjectId"] = "wrong-source";
        effect["powerDelta"] = 99;
        effect["basePower"] = 98;
        effect["effectivePower"] = 97;
        effect["sequence"] = 96;
        effect["effectKind"] = "WRONG_EFFECT_KIND";
        effect["sourceCardNo"] = "WRONG_CARD";
        effect["sourcePath"] = "wrong-source-path";
        effect["layerEngineStatus"] = "WRONG_LAYER_ENGINE_STATUS";
        effect["requestedPowerDelta"] = 95;
        effect["appliedPowerDelta"] = 94;
        effect["minimumPower"] = 93;
        effect["resultingPower"] = 92;
        effect["appliedOrder"] = 91;
        effect["sourceOrder"] = 90;
        effect["condition"] = "unexpected-condition";
        effect["lifecycle"] = "unexpected-lifecycle";
        effect["participantObjectIds"] = new[] { "unexpected-participant" };
        effect["sourceDependencyObjectIds"] = new[] { "unexpected-source-dependency" };
        effect["targetDependencyObjectIds"] = new[] { "unexpected-target-dependency" };
        effect["participantDependencyObjectIds"] = new[] { "unexpected-participant-dependency" };
        effect["deferredLayerEngineResiduals"] = new[] { "unexpected-residual" };
        continuousEffects[0] = effect;
        timing["continuousEffects"] = continuousEffects;
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
            error => error.Contains("spectator replay frame timing continuous effect ids disagree with authoritative state continuous effect ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect scopes disagree with authoritative state continuous effect scopes", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect layers disagree with authoritative state continuous effect layers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect durations disagree with authoritative state continuous effect durations", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect target objects disagree with authoritative state continuous effect target objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect source objects disagree with authoritative state continuous effect source objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect power deltas disagree with authoritative state continuous effect power deltas", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect base powers disagree with authoritative state continuous effect base powers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect effective powers disagree with authoritative state continuous effect effective powers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect sequences disagree with authoritative state continuous effect sequences", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect kinds disagree with authoritative state continuous effect kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect source card numbers disagree with authoritative state continuous effect source card numbers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect source paths disagree with authoritative state continuous effect source paths", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect layer engine statuses disagree with authoritative state continuous effect layer engine statuses", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect requested power deltas disagree with authoritative state continuous effect requested power deltas", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect applied power deltas disagree with authoritative state continuous effect applied power deltas", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect minimum powers disagree with authoritative state continuous effect minimum powers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect resulting powers disagree with authoritative state continuous effect resulting powers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect applied orders disagree with authoritative state continuous effect applied orders", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect source orders disagree with authoritative state continuous effect source orders", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect conditions disagree with authoritative state continuous effect conditions", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect lifecycles disagree with authoritative state continuous effect lifecycles", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect participant object ids disagree with authoritative state continuous effect participant object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect source dependency object ids disagree with authoritative state continuous effect source dependency object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect target dependency object ids disagree with authoritative state continuous effect target dependency object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect participant dependency object ids disagree with authoritative state continuous effect participant dependency object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing continuous effect deferred LayerEngine residuals disagree with authoritative state continuous effect deferred LayerEngine residuals", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMismatch()
    {
        const string battlefieldObjectId = "battlefield-a";
        const string visibleSourceObjectId = "visible-source-1";
        const string hiddenSourceObjectId = "hidden-source-1";
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Base = [visibleSourceObjectId],
                    Battlefields = [battlefieldObjectId, hiddenSourceObjectId]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "TEST-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                [visibleSourceObjectId] = new(
                    visibleSourceObjectId,
                    ownerId: "alice",
                    controllerId: "alice"),
                [hiddenSourceObjectId] = new(
                    hiddenSourceObjectId,
                    power: 2,
                    isFaceDown: true,
                    ownerId: "alice",
                    controllerId: "alice",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [visibleSourceObjectId] = new("alice", "BASE"),
                [hiddenSourceObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId)
            },
            triggerQueue:
            [
                new TriggerQueueItemState(
                    "trigger-visible",
                    "alice",
                    visibleSourceObjectId,
                    "LAST_BREATH",
                    "OBJECT_DESTROYED"),
                new TriggerQueueItemState(
                    "trigger-hidden",
                    "bob",
                    hiddenSourceObjectId,
                    "AMBUSH_REVEALED",
                    "BATTLEFIELD_HELD")
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var triggerQueue = Assert.IsAssignableFrom<IEnumerable<object?>>(timing["triggerQueue"])
            .ToArray();
        var visibleTrigger = Assert.IsType<Dictionary<string, object?>>(triggerQueue[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var hiddenTrigger = Assert.IsType<Dictionary<string, object?>>(triggerQueue[1])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Assert.Equal(visibleSourceObjectId, Assert.IsType<string>(visibleTrigger["sourceObjectId"]));
        Assert.Equal("VISIBLE", Assert.IsType<string>(visibleTrigger["sourceVisibility"]));
        Assert.Equal("HIDDEN", Assert.IsType<string>(hiddenTrigger["sourceObjectId"]));
        Assert.Equal("HIDDEN", Assert.IsType<string>(hiddenTrigger["sourceVisibility"]));
        Assert.Equal("HIDDEN", Assert.IsType<string>(hiddenTrigger["effectKind"]));

        visibleTrigger["triggerId"] = "wrong-trigger";
        visibleTrigger["controllerId"] = "bob";
        visibleTrigger["sourceObjectId"] = "wrong-source";
        visibleTrigger["effectKind"] = "WRONG_EFFECT";
        visibleTrigger["triggeredByEventKind"] = "WRONG_EVENT";
        hiddenTrigger["sourceVisibility"] = "VISIBLE";
        triggerQueue[0] = visibleTrigger;
        triggerQueue[1] = hiddenTrigger;
        timing["triggerQueue"] = triggerQueue;
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
            error => error.Contains("spectator replay frame timing trigger queue ids disagree with authoritative state trigger queue ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing trigger queue controller ids disagree with authoritative state trigger queue controller ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing trigger queue source object ids disagree with authoritative state trigger queue source object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing trigger queue source visibilities disagree with authoritative state trigger queue source visibilities", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing trigger queue effect kinds disagree with authoritative state trigger queue effect kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing trigger queue triggered event kinds disagree with authoritative state trigger queue triggered event kinds", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcesMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["source-1"] = new("source-1", ownerId: "alice", controllerId: "alice")
            },
            temporaryPaymentResources:
            [
                new TemporaryPaymentResourceState(
                    "temp-payment-resource-1",
                    "alice",
                    "source-1",
                    "TEST_TEMP_RESOURCE_ABILITY",
                    "PAY_COST",
                    generatedPower: 3,
                    remainingPower: 1,
                    allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind],
                    createdTick: 2,
                    generatedPowerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["blue"] = 2
                    },
                    remainingPowerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["blue"] = 1
                    })
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var temporaryResources = Assert.IsAssignableFrom<IEnumerable<object?>>(timing["temporaryPaymentResources"])
            .ToArray();
        var resource = Assert.IsType<Dictionary<string, object?>>(temporaryResources[0])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Assert.Equal(
            P4ActivatedAbilityCatalog.MalzaharPaymentOnlyResourceRestriction,
            Assert.IsType<string>(resource["resourceRestriction"]));

        resource["resourceId"] = "wrong-resource";
        resource["ownerPlayerId"] = "bob";
        resource["sourceObjectId"] = "wrong-source";
        resource["abilityId"] = "WRONG_ABILITY";
        resource["paymentWindow"] = "WRONG_WINDOW";
        resource["generatedPower"] = 9;
        resource["remainingPower"] = 8;
        resource["generatedPowerByTrait"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["green"] = 7
        };
        resource["remainingPowerByTrait"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["red"] = 6
        };
        resource["allowedPaymentKinds"] = new[] { "WRONG_PAYMENT_KIND" };
        resource["paymentOnly"] = false;
        resource["resourceRestriction"] = "WRONG_RESTRICTION";
        resource["createdTick"] = 99L;
        temporaryResources[0] = resource;
        timing["temporaryPaymentResources"] = temporaryResources;
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
            error => error.Contains("spectator replay frame timing temporary payment resource ids disagree with authoritative state temporary payment resource ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource owners disagree with authoritative state temporary payment resource owners", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource source objects disagree with authoritative state temporary payment resource source objects", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource ability ids disagree with authoritative state temporary payment resource ability ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource payment windows disagree with authoritative state temporary payment resource payment windows", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource generated powers disagree with authoritative state temporary payment resource generated powers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource remaining powers disagree with authoritative state temporary payment resource remaining powers", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource generated power traits disagree with authoritative state temporary payment resource generated power traits", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource remaining power traits disagree with authoritative state temporary payment resource remaining power traits", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource allowed payment kinds disagree with authoritative state temporary payment resource allowed payment kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource payment-only flags disagree with authoritative state temporary payment resource payment-only flags", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource restrictions disagree with authoritative state temporary payment resource restrictions", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing temporary payment resource created ticks disagree with authoritative state temporary payment resource created ticks", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            pendingHandChoice: new PendingHandChoiceState(
                "choice-1",
                "CHOOSE_HAND_CARDS",
                "alice",
                requiredCount: 1,
                maxCount: 2,
                legalObjectIds: ["alice-hand-1", "alice-hand-2"],
                reason: "test-choice",
                sourceObjectId: "source-1",
                effectKind: "DRAW_DISCARD"));
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
        var pendingHandChoice = Assert.IsType<Dictionary<string, object?>>(timing["pendingHandChoice"])
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        pendingHandChoice["choiceId"] = "wrong-choice";
        pendingHandChoice["choiceWindow"] = "WRONG_WINDOW";
        pendingHandChoice["playerId"] = "bob";
        pendingHandChoice["requiredCount"] = 3;
        pendingHandChoice["maxCount"] = 4;
        pendingHandChoice["reason"] = "wrong-reason";
        pendingHandChoice["sourceObjectId"] = "wrong-source";
        pendingHandChoice["effectKind"] = "WRONG_EFFECT";
        pendingHandChoice["choiceState"] = "PENDING_CHOICE";
        pendingHandChoice["legalObjectIds"] = new[] { "alice-hand-1" };
        timing["pendingHandChoice"] = pendingHandChoice;
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
            error => error.Contains("spectator replay frame timing pending hand choice id does not match authoritative state pending hand choice id", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice window does not match authoritative state pending hand choice window", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice player does not match authoritative state pending hand choice player", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice required count does not match authoritative state pending hand choice required count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice max count does not match authoritative state pending hand choice max count", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice reason does not match authoritative state pending hand choice reason", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice source object does not match authoritative state pending hand choice source object", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice effect kind does not match authoritative state pending hand choice effect kind", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice state does not match authoritative spectator pending hand choice state", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing pending hand choice legal object ids must be redacted", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingTurnWindowMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen);
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
        var turnWindow = Assert.IsType<Dictionary<string, object?>>(timing["turnWindow"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        turnWindow["state"] = TimingStates.NeutralClosed;
        turnWindow["isClosed"] = true;
        timing["turnWindow"] = turnWindow;
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
            error => error.Contains("spectator replay frame timing turn window does not match authoritative state turn window", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingSpellDuelMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            focusPlayerId: "alice",
            passedFocusPlayerIds: ["bob"]);
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
        var spellDuel = Assert.IsType<Dictionary<string, object?>>(timing["spellDuel"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        spellDuel["focusPlayerId"] = "bob";
        spellDuel["passedFocusPlayerIds"] = Array.Empty<string>();
        timing["spellDuel"] = spellDuel;
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
            error => error.Contains("spectator replay frame timing spell duel does not match authoritative state spell duel", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen);
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        battle["isActive"] = true;
        battle["attackerObjectIds"] = new[] { "unit-a" };
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryCountMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("battlefield-1", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("alice", "BATTLEFIELD", "battlefield-1")
            },
            battlefieldResolutions:
            [
                new(
                    "battlefield-resolution-1",
                    3,
                    "HELD",
                    "test",
                    "battlefield-1",
                    "alice",
                    null,
                    "alice",
                    null,
                    [],
                    ["BATTLEFIELD_HELD"])
            ],
            battleResolutions:
            [
                new(
                    "battle-resolution-1",
                    3,
                    "CLOSED",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    [],
                    [],
                    [],
                    [],
                    [],
                    ["BATTLE_CLOSED"])
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["battlefieldResolutions"] = Array.Empty<object?>();
        timing["battleResolutions"] = Array.Empty<object?>();
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
            error => error.Contains("spectator replay frame timing battlefield resolution count 0 does not match authoritative state battlefield resolution count 1", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution count 0 does not match authoritative state battle resolution count 1", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryIdMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("battlefield-1", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("alice", "BATTLEFIELD", "battlefield-1")
            },
            battlefieldResolutions:
            [
                new(
                    "battlefield-resolution-1",
                    3,
                    "HELD",
                    "test",
                    "battlefield-1",
                    "alice",
                    null,
                    "alice",
                    null,
                    [],
                    ["BATTLEFIELD_HELD"])
            ],
            battleResolutions:
            [
                new(
                    "battle-resolution-1",
                    3,
                    "CLOSED",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    [],
                    [],
                    [],
                    [],
                    [],
                    ["BATTLE_CLOSED"])
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["battlefieldResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "other-battlefield-resolution"
            }
        };
        timing["battleResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "other-battle-resolution"
            }
        };
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
            error => error.Contains("spectator replay frame timing battlefield resolution ids disagree with authoritative state battlefield resolution ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution ids disagree with authoritative state battle resolution ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryScalarMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("battlefield-1", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("alice", "BATTLEFIELD", "battlefield-1")
            },
            battlefieldResolutions:
            [
                new(
                    "battlefield-resolution-1",
                    3,
                    "HELD",
                    "test",
                    "battlefield-1",
                    "alice",
                    null,
                    "alice",
                    null,
                    [],
                    ["BATTLEFIELD_HELD"])
            ],
            battleResolutions:
            [
                new(
                    "battle-resolution-1",
                    3,
                    "CLOSED",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    [],
                    [],
                    [],
                    [],
                    [],
                    ["BATTLE_CLOSED"])
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["battlefieldResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "battlefield-resolution-1",
                ["tick"] = 4,
                ["kind"] = "OTHER",
                ["reason"] = "other"
            }
        };
        timing["battleResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "battle-resolution-1",
                ["tick"] = 4,
                ["kind"] = "OTHER",
                ["reason"] = "other"
            }
        };
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
            error => error.Contains("spectator replay frame timing battlefield resolution ticks disagree with authoritative state battlefield resolution ticks", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield resolution kinds disagree with authoritative state battlefield resolution kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield resolution reasons disagree with authoritative state battlefield resolution reasons", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution ticks disagree with authoritative state battle resolution ticks", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution kinds disagree with authoritative state battle resolution kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution reasons disagree with authoritative state battle resolution reasons", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryScalarReferenceMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("battlefield-1", ownerId: "alice", controllerId: "alice"),
                ["source-1"] = new("source-1", ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("alice", "BATTLEFIELD", "battlefield-1"),
                ["source-1"] = new("alice", "BATTLEFIELD", "source-1")
            },
            battlefieldResolutions:
            [
                new(
                    "battlefield-resolution-1",
                    3,
                    "HELD",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    "source-1",
                    [],
                    ["BATTLEFIELD_HELD"])
            ],
            battleResolutions:
            [
                new(
                    "battle-resolution-1",
                    3,
                    "CLOSED",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    [],
                    [],
                    [],
                    [],
                    [],
                    ["BATTLE_CLOSED"])
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["battlefieldResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "battlefield-resolution-1",
                ["tick"] = 3,
                ["kind"] = "HELD",
                ["reason"] = "test",
                ["battlefieldObjectId"] = "other-battlefield",
                ["playerId"] = "bob",
                ["previousControllerId"] = "alice",
                ["controllerId"] = "bob",
                ["sourceObjectId"] = "other-source"
            }
        };
        timing["battleResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "battle-resolution-1",
                ["tick"] = 3,
                ["kind"] = "CLOSED",
                ["reason"] = "test",
                ["battlefieldId"] = "other-battlefield",
                ["attackingPlayerId"] = "bob",
                ["defendingPlayerId"] = "alice",
                ["winnerPlayerId"] = "bob"
            }
        };
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
            error => error.Contains("spectator replay frame timing battlefield resolution battlefield object ids disagree with authoritative state battlefield resolution battlefield object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield resolution player ids disagree with authoritative state battlefield resolution player ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield resolution previous controller ids disagree with authoritative state battlefield resolution previous controller ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield resolution controller ids disagree with authoritative state battlefield resolution controller ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield resolution source object ids disagree with authoritative state battlefield resolution source object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution battlefield ids disagree with authoritative state battle resolution battlefield ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution attacking player ids disagree with authoritative state battle resolution attacking player ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution defending player ids disagree with authoritative state battle resolution defending player ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution winner player ids disagree with authoritative state battle resolution winner player ids", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryObjectListMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("battlefield-1", ownerId: "alice", controllerId: "alice"),
                ["source-1"] = new("source-1", ownerId: "alice", controllerId: "alice"),
                ["participant-1"] = new("participant-1", ownerId: "alice", controllerId: "alice"),
                ["attacker-1"] = new("attacker-1", ownerId: "alice", controllerId: "alice"),
                ["defender-1"] = new("defender-1", ownerId: "bob", controllerId: "bob"),
                ["destroyed-1"] = new("destroyed-1", ownerId: "bob", controllerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["battlefield-1"] = new("alice", "BATTLEFIELD", "battlefield-1"),
                ["source-1"] = new("alice", "BATTLEFIELD", "source-1"),
                ["participant-1"] = new("alice", "BATTLEFIELD", "participant-1"),
                ["attacker-1"] = new("alice", "BATTLEFIELD", "attacker-1"),
                ["defender-1"] = new("bob", "BATTLEFIELD", "defender-1"),
                ["destroyed-1"] = new("bob", "GRAVEYARD", "destroyed-1")
            },
            battlefieldResolutions:
            [
                new(
                    "battlefield-resolution-1",
                    3,
                    "HELD",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    "source-1",
                    ["participant-1"],
                    ["BATTLEFIELD_HELD"])
            ],
            battleResolutions:
            [
                new(
                    "battle-resolution-1",
                    3,
                    "CLOSED",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    ["attacker-1"],
                    ["defender-1"],
                    ["attacker-1"],
                    ["defender-1"],
                    ["destroyed-1"],
                    ["BATTLE_CLOSED"])
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
        var timing = spectatorReplayFrame.SpectatorSnapshot.Timing.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        timing["battlefieldResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "battlefield-resolution-1",
                ["tick"] = 3,
                ["kind"] = "HELD",
                ["reason"] = "test",
                ["battlefieldObjectId"] = "battlefield-1",
                ["playerId"] = "alice",
                ["previousControllerId"] = "bob",
                ["controllerId"] = "alice",
                ["sourceObjectId"] = "source-1",
                ["participantObjectIds"] = new[] { "other-participant" },
                ["relatedEventKinds"] = new[] { "OTHER_EVENT" }
            }
        };
        timing["battleResolutions"] = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resolutionId"] = "battle-resolution-1",
                ["tick"] = 3,
                ["kind"] = "CLOSED",
                ["reason"] = "test",
                ["battlefieldId"] = "battlefield-1",
                ["attackingPlayerId"] = "alice",
                ["defendingPlayerId"] = "bob",
                ["winnerPlayerId"] = "alice",
                ["attackerObjectIds"] = new[] { "other-attacker" },
                ["defenderObjectIds"] = new[] { "other-defender" },
                ["survivingAttackerObjectIds"] = new[] { "other-surviving-attacker" },
                ["survivingDefenderObjectIds"] = new[] { "other-surviving-defender" },
                ["destroyedObjectIds"] = new[] { "other-destroyed" },
                ["relatedEventKinds"] = new[] { "OTHER_EVENT" }
            }
        };
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
            error => error.Contains("spectator replay frame timing battlefield resolution participant object ids disagree with authoritative state battlefield resolution participant object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battlefield resolution related event kinds disagree with authoritative state battlefield resolution related event kinds", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution attacker object ids disagree with authoritative state battle resolution attacker object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution defender object ids disagree with authoritative state battle resolution defender object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution surviving attacker object ids disagree with authoritative state battle resolution surviving attacker object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution surviving defender object ids disagree with authoritative state battle resolution surviving defender object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution destroyed object ids disagree with authoritative state battle resolution destroyed object ids", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("spectator replay frame timing battle resolution related event kinds disagree with authoritative state battle resolution related event kinds", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPendingMismatch()
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
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen);
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        damageAssignment["isPending"] = true;
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPhaseMismatch()
    {
        var authoritativeState = BattleDamageAssignmentState();
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        Assert.True(Assert.IsType<bool>(damageAssignment["isPending"]));
        damageAssignment["phase"] = "WAIT";
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentIdentityMismatch()
    {
        var authoritativeState = BattleDamageAssignmentState();
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        Assert.Equal("alice", Assert.IsType<string>(damageAssignment["assigningPlayerId"]));
        damageAssignment["assigningPlayerId"] = "bob";
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentDamagePoolMismatch()
    {
        var authoritativeState = BattleDamageAssignmentState();
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damagePool = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(damageAssignment["damagePool"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        Assert.Equal(3, damagePool["attacker-a"]);
        damagePool["attacker-a"] = 4;
        damageAssignment["damagePool"] = damagePool;
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentLegalTargetsMismatch()
    {
        var authoritativeState = BattleDamageAssignmentState();
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var legalTargets = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            damageAssignment["legalTargets"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        Assert.Equal(["defender-a"], legalTargets["attacker-a"]);
        legalTargets["attacker-a"] = Array.Empty<string>();
        damageAssignment["legalTargets"] = legalTargets;
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentExistingDamageMismatch()
    {
        var authoritativeState = BattleDamageAssignmentState();
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var existingDamage = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            damageAssignment["existingDamage"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        Assert.Equal(0, existingDamage["defender-a"]);
        existingDamage["defender-a"] = 1;
        damageAssignment["existingDamage"] = existingDamage;
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentLethalThresholdMismatch()
    {
        var authoritativeState = BattleDamageAssignmentState();
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var lethalDamageThreshold = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            damageAssignment["lethalDamageThreshold"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        Assert.Equal(2, lethalDamageThreshold["defender-a"]);
        lethalDamageThreshold["defender-a"] = 1;
        damageAssignment["lethalDamageThreshold"] = lethalDamageThreshold;
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentRequiredAssignmentsMismatch()
    {
        var authoritativeState = BattleDamageAssignmentState();
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
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var damageAssignment = Assert.IsType<Dictionary<string, object?>>(battle["damageAssignment"]).ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var requiredAssignments = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
            damageAssignment["requiredAssignments"])
            .Select(entry => entry.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal))
            .ToArray();
        var attackerAssignment = Assert.Single(
            requiredAssignments,
            assignment => string.Equals(
                Assert.IsType<string>(assignment["sourceObjectId"]),
                "attacker-a",
                StringComparison.Ordinal));
        Assert.Equal(3, Assert.IsType<int>(attackerAssignment["damage"]));
        attackerAssignment["damage"] = 4;
        damageAssignment["requiredAssignments"] = requiredAssignments;
        battle["damageAssignment"] = damageAssignment;
        timing["battle"] = battle;
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
            error => error.Contains("spectator replay frame timing battle does not match authoritative state battle", StringComparison.Ordinal));
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
        var recoveredEvents = ToRecoveredEvents(journal.Entries);

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            expectedFinalState,
            new PlaceholderRuleEngine(),
            CancellationToken.None,
            recoveredEvents);

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
    public async Task ActionLogReplayerRejectsReplayInitialStateSeatValueDrift()
    {
        var replayInitialState = MatchReplayInitialStateBuilder.FromSeats(
            "room-a",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P1",
                ["charlie"] = "P3"
            });
        var frame = new MatchRecoveryFrame(
            "room-a",
            0,
            0,
            [
                new RecoveredCommand(
                    "alice",
                    "intent-rejected-seat-drift",
                    "PASS",
                    RawCommand("PASS"),
                    0,
                    0,
                    0,
                    0,
                    false,
                    "rejected command for seat drift audit")
            ],
            [],
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            replayInitialState,
            replayInitialState);

        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(
            frame,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.Contains(
            errors,
            error => error.Contains(
                "action-log replay initial state seat P1 is duplicated",
                StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains(
                "action-log replay initial state seat P3 for charlie is invalid",
                StringComparison.Ordinal));
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
    public async Task ActionLogReplayerReportsRecoveredEventKindMismatch()
    {
        var initialState = ReplayInitialState();
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var expectedFinalState = journal.Entries[^1].AuthoritativeState;
        var recoveredCommands = journal.Entries.Select(ToRecoveredCommand).ToArray();
        var recoveredEvents = ToRecoveredEvents(journal.Entries);
        Assert.NotEmpty(recoveredEvents);
        var tamperedEvents = recoveredEvents
            .Select((recoveredEvent, index) => index == 0
                ? recoveredEvent with
                {
                    Event = recoveredEvent.Event with
                    {
                        Kind = "TAMPERED_EVENT_KIND"
                    }
                }
                : recoveredEvent)
            .ToArray();

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            expectedFinalState,
            new PlaceholderRuleEngine(),
            CancellationToken.None,
            tamperedEvents);

        Assert.False(replay.IsMatch);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Contains(
            replay.Errors,
            error => error.Contains("command intent-pass replayed event 1 kind", StringComparison.Ordinal)
                && error.Contains("TAMPERED_EVENT_KIND", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionLogReplayerReportsRecoveredEventPayloadMismatch()
    {
        var initialState = ReplayInitialState();
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var expectedFinalState = journal.Entries[^1].AuthoritativeState;
        var recoveredCommands = journal.Entries.Select(ToRecoveredCommand).ToArray();
        var recoveredEvents = ToRecoveredEvents(journal.Entries);
        Assert.NotEmpty(recoveredEvents);
        var tamperedEvents = recoveredEvents
            .Select((recoveredEvent, index) => index == 0
                ? recoveredEvent with
                {
                    Event = recoveredEvent.Event with
                    {
                        Payload = recoveredEvent.Event.Payload
                            .Append(new KeyValuePair<string, object?>("tampered", true))
                            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
                    }
                }
                : recoveredEvent)
            .ToArray();

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            expectedFinalState,
            new PlaceholderRuleEngine(),
            CancellationToken.None,
            tamperedEvents);

        Assert.False(replay.IsMatch);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Contains(
            replay.Errors,
            error => error.Contains("command intent-pass replayed event 1 payload hash", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionLogReplayerReportsRecoveredEventDescriptionMismatch()
    {
        var initialState = ReplayInitialState();
        var journal = new RecordingMatchJournal();
        var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
        await liveSession.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var expectedFinalState = journal.Entries[^1].AuthoritativeState;
        var recoveredCommands = journal.Entries.Select(ToRecoveredCommand).ToArray();
        var recoveredEvents = ToRecoveredEvents(journal.Entries);
        Assert.NotEmpty(recoveredEvents);
        var tamperedEvents = recoveredEvents
            .Select((recoveredEvent, index) => index == 0
                ? recoveredEvent with
                {
                    Event = recoveredEvent.Event with
                    {
                        Description = "tampered recovered description"
                    }
                }
                : recoveredEvent)
            .ToArray();

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            recoveredCommands,
            expectedFinalState,
            new PlaceholderRuleEngine(),
            CancellationToken.None,
            tamperedEvents);

        Assert.False(replay.IsMatch);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Contains(
            replay.Errors,
            error => error.Contains("command intent-pass replayed event 1 description", StringComparison.Ordinal)
                && error.Contains("tampered recovered description", StringComparison.Ordinal));
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
            ToRecoveredEvents(journal.Entries),
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
            ToRecoveredEvents(journal.Entries),
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
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStateRoomMismatches()
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
        var wrongInitialState = initialState with
        {
            RoomId = "room-other"
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state room room-other does not match recovery room room-a",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStateTickMismatches()
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
        var wrongInitialState = initialState with
        {
            Tick = 7
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state tick 7 must be 0",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStateScalarBaselineMismatches()
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
        var wrongInitialState = initialState with
        {
            TurnNumber = 3,
            Status = MatchStatuses.InProgress,
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            Seed = 42,
            RngCursor = 9
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("action-log replay initial state turn number 3 must be 1", error.Message, StringComparison.Ordinal);
        Assert.Contains("action-log replay initial state status IN_PROGRESS must be SEATING", error.Message, StringComparison.Ordinal);
        Assert.Contains("action-log replay initial state phase MAIN must be ROOM", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state timing state NEUTRAL_OPEN must be ROOM",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains("action-log replay initial state seed 42 must be 0", error.Message, StringComparison.Ordinal);
        Assert.Contains("action-log replay initial state rng cursor 9 must be 0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerBaselineMismatches()
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
        var wrongInitialState = initialState with
        {
            ActivePlayerId = "bob",
            TurnPlayerId = "bob"
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state active player bob must be alice",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state turn player bob must be alice",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStateSeatsMismatchFinalState()
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
        var wrongInitialState = initialState with
        {
            ActivePlayerId = "bob",
            TurnPlayerId = "bob",
            Seats = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P2",
                ["bob"] = "P1"
            }
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state seats do not match authoritative final state seats",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerResourceBaselineMismatches()
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
        var wrongInitialState = initialState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["alice"] = new(1, 0),
                ["bob"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty,
                ["bob"] = PlayerZones.Empty with
                {
                    Hand = ["bob-hand-1"]
                }
            }
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state rune pool for alice must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state zones for bob must be empty",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerCounterBaselineMismatches()
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
        var wrongInitialState = initialState with
        {
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = 1,
                ["bob"] = 0
            },
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = 0,
                ["bob"] = 2
            },
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alice"] = 0,
                ["bob"] = 1
            }
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state score for alice must be 0",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state experience for bob must be 0",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state cards played this turn for bob must be 0",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerListBaselineMismatches()
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
        var wrongInitialState = initialState with
        {
            ReadyPlayerIds = ["alice"],
            PassedPriorityPlayerIds = ["alice"],
            PassedFocusPlayerIds = ["bob"],
            MulliganCompletedPlayerIds = ["alice"],
            DestroyedUnitOwnerIdsThisTurn = ["bob"]
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state ready players must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state passed priority players must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state passed focus players must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state mulligan completed players must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state destroyed unit owners this turn must be empty",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStateOptionalPlayerBaselineMismatches()
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
        var wrongInitialState = initialState with
        {
            PriorityPlayerId = "alice",
            FocusPlayerId = "bob",
            WinnerPlayerId = "alice",
            OpeningSecondActionPlayerId = "bob",
            ExtraTurnPlayerId = "alice"
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state priority player must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state focus player must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state winner player must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state opening second action player must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state extra turn player must be empty",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryRejectsRecoveryFrameWhenReplayInitialStateStructuralBaselineMismatches()
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
        var wrongInitialState = initialState with
        {
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("obj-1", cardNo: "TEST-001", ownerId: "alice", controllerId: "alice")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["obj-1"] = new("alice", "HAND")
            },
            PlayerDecklists = new Dictionary<string, OfficialDecklist>(StringComparer.Ordinal)
            {
                ["alice"] = new("LEGEND-001", "CHAMPION-001", ["MAIN-001"], ["RUNE-001"], ["BATTLEFIELD-001"])
            },
            StackItems =
            [
                new("stack-1", "alice", "obj-1", "TEST_EFFECT")
            ],
            TriggerQueue =
            [
                new("trigger-1", "alice", "obj-1", "TEST_EFFECT", "TEST_EVENT")
            ],
            BattlefieldResolutions =
            [
                new(
                    "battlefield-resolution-1",
                    0,
                    "TEST",
                    "test",
                    "battlefield-1",
                    "alice",
                    null,
                    "alice",
                    "obj-1",
                    ["obj-1"],
                    ["TEST_EVENT"])
            ],
            BattleResolutions =
            [
                new(
                    "battle-resolution-1",
                    0,
                    "TEST",
                    "test",
                    "battlefield-1",
                    "alice",
                    "bob",
                    "alice",
                    ["attacker-1"],
                    ["defender-1"],
                    ["attacker-1"],
                    [],
                    ["defender-1"],
                    ["TEST_EVENT"])
            ],
            UntilEndOfTurnEffects = ["effect-1"],
            TemporaryPaymentResources =
            [
                new("resource-1", "alice", remainingPower: 1)
            ],
            PendingPayment = new(
                "payment-1",
                "MAIN_ACTION",
                "alice",
                manaCost: 1,
                legalPaymentChoiceIds: ["choice-1"],
                reason: "test"),
            PendingHandChoice = new(
                "choice-1",
                "TEST",
                "alice",
                1,
                1,
                ["obj-1"],
                "test")
        };
        var frame = new MatchRecoveryFrame(
            "room-a",
            expectedFinalState.Tick,
            journal.Entries[^1].CompletedEventSequence,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            ToRecoveredEvents(journal.Entries),
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal),
            [],
            expectedFinalState,
            wrongInitialState);
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            new FixedRecoveryStore(frame));

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync("room-a", CancellationToken.None));

        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state card objects must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state object locations must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state player decklists must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state stack items must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state trigger queue must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state battlefield resolutions must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state battle resolutions must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state until end of turn effects must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state temporary payment resources must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state pending payment must be empty",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "action-log replay initial state pending hand choice must be empty",
            error.Message,
            StringComparison.Ordinal);
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

    private static IReadOnlyList<RecoveredEvent> ToRecoveredEvents(IEnumerable<MatchJournalEntry> entries)
    {
        var recoveredEvents = new List<RecoveredEvent>();
        foreach (var entry in entries)
        {
            for (var index = 0; index < entry.Events.Count; index++)
            {
                recoveredEvents.Add(new RecoveredEvent(
                    entry.StartedEventSequence + index + 1,
                    entry.CompletedTick,
                    index,
                    entry.Events[index]));
            }
        }

        return recoveredEvents;
    }

    private static MatchState BattleDamageAssignmentState()
    {
        const string battlefieldObjectId = "battlefield-a";
        const string attackerObjectId = "attacker-a";
        const string defenderObjectId = "defender-a";

        return new MatchState(
            "room-a",
            3,
            1,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            turnPlayerId: "alice",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = [battlefieldObjectId, attackerObjectId]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = [defenderObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "TEST-BATTLEFIELD",
                    ownerId: "alice",
                    controllerId: "alice"),
                [attackerObjectId] = new(
                    attackerObjectId,
                    isAttacking: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice",
                    controllerId: "alice"),
                [defenderObjectId] = new(
                    defenderObjectId,
                    isDefending: true,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob",
                    controllerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [battlefieldObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [attackerObjectId] = new("alice", "BATTLEFIELD", battlefieldObjectId),
                [defenderObjectId] = new("bob", "BATTLEFIELD", battlefieldObjectId)
            });
    }

    private static RecoveredEvent RecoveredEvent(long sequence, string kind)
    {
        return new RecoveredEvent(
            sequence,
            sequence,
            (int)sequence - 1,
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
            new Dictionary<string, object?>
            {
                ["phase"] = MatchPhases.Main,
                ["timingState"] = "NEUTRAL_OPEN",
                ["turnPlayerId"] = activePlayerId,
                ["roomStatus"] = MatchStatuses.InProgress
            },
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
