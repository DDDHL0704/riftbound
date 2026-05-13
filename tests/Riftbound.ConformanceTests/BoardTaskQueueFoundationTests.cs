using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BoardTaskQueueFoundationTests
{
    [Fact]
    public async Task BaseToBattlefieldMoveIntoEmptyBattlefieldKeepsTaskQueueIdle()
    {
        var state = BaseMoveState(occupied: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-base-to-empty-battlefield", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-BASE-MOVER", "BASE", "BATTLEFIELD:BF-EMPTY", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-EMPTY", StringComparison.Ordinal));
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF-EMPTY"), result.State.ObjectLocations["P1-BASE-MOVER"]);
        Assert.False(result.State.BattlefieldStates["BF-EMPTY"].Contested);
        Assert.Equal(["P1"], result.State.BattlefieldStates["BF-EMPTY"].OccupantControllerIds);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BaseToBattlefieldMoveIntoOccupiedEnemyBattlefieldStartsSpellDuelAfterCleanupGate()
    {
        var state = BaseMoveState(occupied: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-base-to-occupied-battlefield", "P2", CommandTypes.MoveUnit),
            new MoveUnitCommand("P2-BASE-MOVER", "BASE", "BATTLEFIELD:BF-CONTEST", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-CONTEST", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-CONTEST", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["taskId"] as string, "task:start-spell-duel:BF-CONTEST", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StateBasedCleanupBlocksOrdinaryActionsBeforeBattlefieldTasks()
    {
        var state = CleanupPriorityContestState();

        Assert.Equal("STATE_BASED_CLEANUP", state.PendingTaskQueue.Phase);
        Assert.Equal("cleanup:lethal:P1-LETHAL-CONTESTER", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["DESTROY_LETHAL_UNIT", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.False(prompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.DoesNotContain("DESTROY_LETHAL_UNIT", prompt.Reason, StringComparison.Ordinal);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-blocked-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.Equal("STATE_BASED_CLEANUP", result.State.PendingTaskQueue.Phase);
    }

    [Fact]
    public async Task StackResolutionMoveToBaseCanRemoveContestAndReturnQueueToIdle()
    {
        var state = StackMoveToBaseContestState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-p1-pass-priority", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P2-CONTEST-DEFENDER"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), result.State.ObjectLocations["P2-CONTEST-DEFENDER"]);
        Assert.False(result.State.BattlefieldStates["BF-CONTEST"].Contested);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                || string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-CONTEST-DEFENDER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CleanupLoopRepeatsWhenDestroyedHostCreatesUnattachedEquipmentCandidate()
    {
        var state = CleanupRepeatState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-cleanup-repeat-pass-priority", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_DESTROYED", "EQUIPMENT_RECALLED_TO_BASE"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Contains("P1-LETHAL-HOST", result.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-ATTACHED-EQUIPMENT", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-ATTACHED-EQUIPMENT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), result.State.ObjectLocations["P1-ATTACHED-EQUIPMENT"]);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
    }

    [Fact]
    public void IllegalStandbyAndUnattachedEquipmentTasksRedactHiddenAndRawPromptDetails()
    {
        var illegalStandbyState = IllegalStandbyState();
        var opponentSnapshot = ResolutionResult.BuildSnapshots(illegalStandbyState)["P2"];
        var opponentQueue = Assert.IsType<Dictionary<string, object?>>(opponentSnapshot.Timing["pendingTaskQueue"]);
        var opponentTask = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(opponentQueue["tasks"]));

        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(opponentQueue["phase"]));
        Assert.True(Assert.IsType<bool>(opponentTask["hiddenObject"]));
        Assert.Equal("BATTLEFIELD_STANDBY", Assert.IsType<string>(opponentTask["hiddenObjectKind"]));
        Assert.DoesNotContain("P1-HIDDEN-STANDBY", Assert.IsType<string>(opponentQueue["activeTaskId"]), StringComparison.Ordinal);
        Assert.DoesNotContain("objectId", opponentTask.Keys);

        var illegalStandbyPrompt = ResolutionResult.BuildPrompts(illegalStandbyState)["P2"];
        Assert.False(illegalStandbyPrompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], illegalStandbyPrompt.Actions);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", illegalStandbyPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:", illegalStandbyPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-HIDDEN-STANDBY", illegalStandbyPrompt.Reason, StringComparison.Ordinal);

        var equipmentState = UnattachedEquipmentState();
        var equipmentPrompt = ResolutionResult.BuildPrompts(equipmentState)["P1"];
        Assert.False(equipmentPrompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], equipmentPrompt.Actions);
        Assert.Contains("装备清理", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("RECALL_UNATTACHED_EQUIPMENT", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("UNATTACHED_EQUIPMENT_CLEANUP", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:", equipmentPrompt.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PassFocusClosesSpellDuelAndPromotesStartBattleWithParticipantData()
    {
        var state = SpellDuelReadyToCloseState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-pass-focus-promote-battle", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["FOCUS_PASSED", "SPELL_DUEL_CLOSED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-CONTEST"), result.State.UntilEndOfTurnEffects);
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-CONTEST", result.State.PendingTaskQueue.ActiveTaskId);

        Assert.Collection(
            result.State.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("COMPLETED", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-CONTEST-ATTACKER", "P2-CONTEST-DEFENDER"], task.ParticipantObjectIds);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("PENDING", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-CONTEST-ATTACKER", "P2-CONTEST-DEFENDER"], task.ParticipantObjectIds);
            });
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], result.Prompts["P1"].Actions);
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task PreciseRoamPreservesDestinationCasingAndQueuesOnlyDestinationContestTasks()
    {
        const string originBattlefieldObjectId = "P1-Origin-MiXeD-BF";
        const string destinationBattlefieldObjectId = "P1-Dest-MiXeD-BF";
        const string origin = $"BATTLEFIELD:{originBattlefieldObjectId}";
        const string destination = $"BATTLEFIELD:{destinationBattlefieldObjectId}";
        var state = PreciseRoamContestState(originBattlefieldObjectId, destinationBattlefieldObjectId);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-precise-roam-contest", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-ROAMER-001", origin, destination, ["ROAM"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var moveEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal(origin, Assert.IsType<string>(moveEvent.Payload["origin"]));
        Assert.Equal(destination, Assert.IsType<string>(moveEvent.Payload["destination"]));
        Assert.Equal(
            destinationBattlefieldObjectId,
            result.State.ObjectLocations["P1-ROAMER-001"].BattlefieldObjectId);

        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{destinationBattlefieldObjectId}", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.DoesNotContain(originBattlefieldObjectId, result.State.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.All(
            result.State.PendingTaskQueue.Tasks,
            task => Assert.Equal(destinationBattlefieldObjectId, task.BattlefieldObjectId));
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.BattlefieldObjectId, originBattlefieldObjectId, StringComparison.Ordinal));
        Assert.All(
            result.State.BattlefieldTasks,
            task => Assert.Equal(destinationBattlefieldObjectId, task.BattlefieldObjectId));
        Assert.Collection(
            result.State.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("ACTIVE", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-ROAMER-001", "P2-DEST-DEFENDER"], task.ParticipantObjectIds);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("WAITING_FOR_SPELL_DUEL", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-ROAMER-001", "P2-DEST-DEFENDER"], task.ParticipantObjectIds);
            });

        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, destinationBattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["focusPlayerId"] as string, "P1", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            (string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
                || string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal))
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, originBattlefieldObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconnectWithPendingCleanupTaskPreservesQueueAndOpponentRedaction()
    {
        var session = new MatchSession("board-task-reconnect-redaction-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        var p2Join = session.EnsurePlayer("P2");
        var seed = await session.SeedScenarioAsync(
            "P1",
            "seed-board-task-reconnect-redaction",
            "battlefield-illegal-standby",
            JsonSerializer.SerializeToElement(new
            {
                cmdType = "DEV_SEED_SCENARIO",
                scenarioId = "battlefield-illegal-standby"
            }),
            CancellationToken.None);

        Assert.True(seed.Accepted, seed.ErrorMessage);
        var reconnect = session.ReconnectPlayer("P2", p2Join.ReconnectToken);
        Assert.Equal("P2", reconnect.PlayerId);
        var snapshot = session.SnapshotFor("P2");
        var prompt = session.PromptFor("P2");
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        Assert.True(Assert.IsType<bool>(taskQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        Assert.DoesNotContain(
            "P1-STANDBY-ILLEGAL-001",
            Assert.IsType<string>(taskQueue["activeTaskId"]),
            StringComparison.Ordinal);
        var task = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]));
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", Assert.IsType<string>(task["kind"]));
        Assert.True(Assert.IsType<bool>(task["hiddenObject"]));
        Assert.Equal("BATTLEFIELD_STANDBY", Assert.IsType<string>(task["hiddenObjectKind"]));
        Assert.DoesNotContain("objectId", task.Keys);

        Assert.False(prompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:illegal-standby", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-STANDBY-ILLEGAL-001", prompt.Reason, StringComparison.Ordinal);
    }

    private static MatchState BaseMoveState(bool occupied)
    {
        var p1Battlefields = occupied
            ? new[] { "BF-CONTEST", "P1-CONTEST-DEFENDER" }
            : ["BF-EMPTY"];
        var p2Base = occupied ? new[] { "P2-BASE-MOVER" } : [];
        var p1Base = occupied ? [] : new[] { "P1-BASE-MOVER" };
        var activePlayerId = occupied ? "P2" : "P1";

        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [occupied ? "BF-CONTEST" : "BF-EMPTY"] = Battlefield(occupied ? "BF-CONTEST" : "BF-EMPTY", "P1"),
            ["P1-BASE-MOVER"] = Unit("P1-BASE-MOVER", "P1"),
            ["P2-BASE-MOVER"] = Unit("P2-BASE-MOVER", "P2"),
            ["P1-CONTEST-DEFENDER"] = Unit("P1-CONTEST-DEFENDER", "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [occupied ? "BF-CONTEST" : "BF-EMPTY"] = new("P1", "BATTLEFIELD", occupied ? "BF-CONTEST" : "BF-EMPTY"),
            ["P1-BASE-MOVER"] = new("P1", "BASE"),
            ["P2-BASE-MOVER"] = new("P2", "BASE"),
            ["P1-CONTEST-DEFENDER"] = new("P1", "BATTLEFIELD", "BF-CONTEST")
        };

        return new MatchState(
            roomId: occupied ? "board-task-base-move-contest" : "board-task-base-move-empty",
            tick: 0,
            turnNumber: 1,
            activePlayerId: activePlayerId,
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: activePlayerId,
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = p1Base,
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static MatchState CleanupPriorityContestState()
    {
        return new MatchState(
            roomId: "board-task-cleanup-priority-contest",
            tick: 4,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-CONTEST", "P1-LETHAL-CONTESTER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONTEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = Battlefield("BF-CONTEST", "P1"),
                ["P1-LETHAL-CONTESTER"] = Unit("P1-LETHAL-CONTESTER", "P1", damage: 3, power: 3),
                ["P2-CONTEST-DEFENDER"] = Unit("P2-CONTEST-DEFENDER", "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P1-LETHAL-CONTESTER"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P2-CONTEST-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-CONTEST")
            });
    }

    private static MatchState StackMoveToBaseContestState()
    {
        return new MatchState(
            roomId: "board-task-stack-removes-contest",
            tick: 7,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Graveyard = ["P1-SPELL-BATTLE-OR-FLIGHT"],
                    Battlefields = ["BF-CONTEST", "P1-CONTEST-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONTEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BATTLE-OR-FLIGHT"] = new(
                    "P1-SPELL-BATTLE-OR-FLIGHT",
                    cardNo: "OGN·168/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["BF-CONTEST"] = Battlefield("BF-CONTEST", "P1"),
                ["P1-CONTEST-ATTACKER"] = Unit("P1-CONTEST-ATTACKER", "P1"),
                ["P2-CONTEST-DEFENDER"] = Unit("P2-CONTEST-DEFENDER", "P2")
            },
            priorityPlayerId: "P1",
            passedPriorityPlayerIds: ["P2"],
            stackItems:
            [
                new StackItemState(
                    "STACK-BATTLE-OR-FLIGHT",
                    "P1",
                    "P1-SPELL-BATTLE-OR-FLIGHT",
                    "BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE",
                    "OGN·168/298",
                    ["P2-CONTEST-DEFENDER"])
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BATTLE-OR-FLIGHT"] = new("P1", "GRAVEYARD"),
                ["BF-CONTEST"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P1-CONTEST-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P2-CONTEST-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-CONTEST")
            });
    }

    private static MatchState CleanupRepeatState()
    {
        return new MatchState(
            roomId: "board-task-cleanup-repeat",
            tick: 3,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-LETHAL-HOST", "P1-ATTACHED-EQUIPMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P1"),
                ["P1-LETHAL-HOST"] = Unit("P1-LETHAL-HOST", "P1", damage: 3, power: 3),
                ["P1-ATTACHED-EQUIPMENT"] = new(
                    "P1-ATTACHED-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    attachedToObjectId: "P1-LETHAL-HOST")
            },
            priorityPlayerId: "P1",
            passedPriorityPlayerIds: ["P2"],
            stackItems:
            [
                new StackItemState(
                    "STACK-CLEANUP-REPEAT",
                    "P1",
                    "P1-NOOP-SOURCE",
                    "TEST_RESOLVE",
                    "TEST-000",
                    [])
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-LETHAL-HOST"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-ATTACHED-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState IllegalStandbyState()
    {
        return new MatchState(
            roomId: "board-task-illegal-standby-redaction",
            tick: 5,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-HIDDEN-STANDBY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P2"),
                ["P1-HIDDEN-STANDBY"] = new(
                    "P1-HIDDEN-STANDBY",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-HIDDEN-STANDBY"] = new("P1", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState UnattachedEquipmentState()
    {
        return new MatchState(
            roomId: "board-task-unattached-equipment-redaction",
            tick: 5,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-UNATTACHED-EQUIPMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P1"),
                ["P1-UNATTACHED-EQUIPMENT"] = new(
                    "P1-UNATTACHED-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-UNATTACHED-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState SpellDuelReadyToCloseState()
    {
        return new MatchState(
            roomId: "board-task-pass-focus-promote-start-battle",
            tick: 8,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-CONTEST", "P1-CONTEST-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONTEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = Battlefield("BF-CONTEST", "P1"),
                ["P1-CONTEST-ATTACKER"] = Unit("P1-CONTEST-ATTACKER", "P1"),
                ["P2-CONTEST-DEFENDER"] = Unit("P2-CONTEST-DEFENDER", "P2")
            },
            focusPlayerId: "P1",
            passedFocusPlayerIds: ["P2"],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P1-CONTEST-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P2-CONTEST-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-CONTEST")
            });
    }

    private static MatchState PreciseRoamContestState(
        string originBattlefieldObjectId,
        string destinationBattlefieldObjectId)
    {
        return new MatchState(
            roomId: "board-task-precise-roam-contest",
            tick: 6,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [originBattlefieldObjectId, destinationBattlefieldObjectId, "P1-ROAMER-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [originBattlefieldObjectId] = Battlefield(originBattlefieldObjectId, "P1"),
                [destinationBattlefieldObjectId] = Battlefield(destinationBattlefieldObjectId, "P1"),
                ["P1-ROAMER-001"] = RoamUnit("P1-ROAMER-001", "P1"),
                ["P2-DEST-DEFENDER"] = Unit("P2-DEST-DEFENDER", "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [originBattlefieldObjectId] = new("P1", "BATTLEFIELD", originBattlefieldObjectId),
                [destinationBattlefieldObjectId] = new("P1", "BATTLEFIELD", destinationBattlefieldObjectId),
                ["P1-ROAMER-001"] = new("P1", "BATTLEFIELD", originBattlefieldObjectId),
                ["P2-DEST-DEFENDER"] = new("P2", "BATTLEFIELD", destinationBattlefieldObjectId)
            });
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static Dictionary<string, RunePool> EmptyPools()
    {
        return new Dictionary<string, RunePool>(StringComparer.Ordinal)
        {
            ["P1"] = RunePool.Empty,
            ["P2"] = RunePool.Empty
        };
    }

    private static CardObjectState Battlefield(string objectId, string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·275/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: controllerId,
            controllerId: controllerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int damage = 0,
        int power = 3)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            damage: damage,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState RoamUnit(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·096/221",
            power: 3,
            tags: [CardObjectTags.UnitCard, "游走"],
            ownerId: playerId,
            controllerId: playerId);
    }
}
