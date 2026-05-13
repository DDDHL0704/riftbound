using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SpellDuelBattleStateMachineTests
{
    [Fact]
    public void MultipleContestedBattlefieldsExposeOneActiveSpellDuelTaskInDeterministicOrder()
    {
        var state = MultiContestSpellDuelState();

        Assert.Equal("BF-A", state.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", state.SpellDuelState.SpellDuelId);
        Assert.Equal("SPELL_DUEL_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-A", state.PendingTaskQueue.ActiveTaskId);

        var spellDuelTasks = state.BattlefieldTasks
            .Where(task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["BF-A", "BF-B"], spellDuelTasks.Select(task => task.BattlefieldObjectId).ToArray());
        Assert.Single(spellDuelTasks, task => string.Equals(task.Status, "ACTIVE", StringComparison.Ordinal));
        Assert.Equal("ACTIVE", spellDuelTasks[0].Status);
        Assert.Equal("PENDING", spellDuelTasks[1].Status);
        Assert.Equal(["P1-A", "P2-A"], spellDuelTasks[0].ParticipantObjectIds);
        Assert.Equal(["P1", "P2"], spellDuelTasks[0].ParticipantControllerIds);

        var startBattleTasks = state.BattlefieldTasks
            .Where(task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["BF-A", "BF-B"], startBattleTasks.Select(task => task.BattlefieldObjectId).ToArray());
        Assert.All(startBattleTasks, task => Assert.Equal("WAITING_FOR_SPELL_DUEL", task.Status));
    }

    [Fact]
    public async Task PassFocusByNonFocusPlayerOrWrongTimingRejectsWithoutMutation()
    {
        var state = MultiContestSpellDuelState();
        var nonFocusResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-non-focus-pass-focus", "P2", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        AssertRejectedWithoutMutation(state, nonFocusResult, ErrorCodes.PhaseNotAllowed);

        var neutralState = IdleNeutralState();
        var wrongTimingResult = await new CoreRuleEngine().ResolveAsync(
            neutralState,
            new PlayerIntent("intent-wrong-timing-pass-focus", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        AssertRejectedWithoutMutation(neutralState, wrongTimingResult, ErrorCodes.PhaseNotAllowed);
    }

    [Fact]
    public async Task SpellDuelStackResolutionReturnsToSameActiveTaskUntilBothPlayersPassFocus()
    {
        var state = SpellDuelStackState();

        Assert.Equal("BF-A", state.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", state.SpellDuelState.SpellDuelId);
        Assert.Equal(["STACK-SWIFT-A"], state.SpellDuelState.StackItemIds);
        Assert.Equal("task:start-spell-duel:BF-A", state.PendingTaskQueue.ActiveTaskId);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-resolve-spell-duel-stack", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), result.State.UntilEndOfTurnEffects);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["stackItemId"] as string, "STACK-SWIFT-A", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ClosingSpellDuelWithCleanupRemovedParticipantSkipsOnlyMatchingBattleAndAdvancesNextTask()
    {
        var state = MultiContestSpellDuelState(lethalFirstDefender: true, passedFocusPlayerIds: ["P2"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-close-first-spell-duel-cleanup-next-task", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Contains("P2-A", result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-A", result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), result.State.UntilEndOfTurnEffects);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("BF-B", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-A", StringComparison.Ordinal));

        var started = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
        Assert.Equal("BF-B", started.Payload["battlefieldObjectId"]);
        Assert.Equal(["P1", "P2"], Assert.IsType<string[]>(started.Payload["participantControllerIds"]));
        Assert.Equal(["P1-B", "P2-B"], Assert.IsType<string[]>(started.Payload["participantObjectIds"]));
    }

    [Fact]
    public void ReconnectDuringSpellDuelTasksPreservesTaskMetadataAndHiddenRedaction()
    {
        var state = MultiContestSpellDuelState(includeOpponentHiddenStandby: true);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1 = session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var reconnect = session.ReconnectPlayer("P1", p1.ReconnectToken);
        var snapshot = session.SnapshotFor("P1");
        var prompt = session.PromptFor("P1");
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("P1", reconnect.PlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        Assert.Equal("P1", snapshot.Timing["focusPlayerId"]);
        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        var activeTask = Assert.Single(battlefieldTasks, task =>
            string.Equals(task["kind"] as string, "START_SPELL_DUEL", StringComparison.Ordinal)
            && string.Equals(task["status"] as string, "ACTIVE", StringComparison.Ordinal));
        Assert.Equal("BF-A", Assert.IsType<string>(activeTask["battlefieldObjectId"]));
        Assert.Equal("spell-duel:BF-A", Assert.IsType<string>(activeTask["spellDuelId"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantControllerIds"]));
        Assert.Equal(["P1-A", "P2-A"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        AssertOpponentHiddenStandbyRedacted(snapshot, "P2-HIDDEN-STANDBY");
    }

    [Fact]
    public void ReconnectDuringBattleTasksPreservesBattleMetadataAndHiddenRedaction()
    {
        var state = StartBattleTaskState(includeOpponentHiddenStandby: true);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1 = session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var reconnect = session.ReconnectPlayer("P1", p1.ReconnectToken);
        var snapshot = session.SnapshotFor("P1");
        var prompt = session.PromptFor("P1");
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("P1", reconnect.PlayerId);
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-battle:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        var activeTask = Assert.Single(battlefieldTasks, task =>
            string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal)
            && string.Equals(task["status"] as string, "PENDING", StringComparison.Ordinal)
            && string.Equals(task["battlefieldObjectId"] as string, "BF-A", StringComparison.Ordinal));
        Assert.Equal("battle:BF-A", Assert.IsType<string>(activeTask["battleId"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantControllerIds"]));
        Assert.Equal(["P1-A", "P2-A"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));
        Assert.Equal(PromptTypes.BattleDeclaration, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-A", prompt.View?.RelatedBattleId);
        AssertOpponentHiddenStandbyRedacted(snapshot, "P2-HIDDEN-STANDBY");
    }

    private static void AssertRejectedWithoutMutation(
        MatchState state,
        ResolutionResult result,
        string expectedErrorCode)
    {
        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static void AssertOpponentHiddenStandbyRedacted(SnapshotDto snapshot, string hiddenObjectId)
    {
        var opponentZones = ZoneView(PlayerView(snapshot, "P2"));
        Assert.DoesNotContain(hiddenObjectId, StringList(opponentZones["battlefields"]));
        var opponentObjects = ObjectView(PlayerView(snapshot, "P2"));
        if (opponentObjects.TryGetValue(hiddenObjectId, out var hiddenObject))
        {
            var hiddenView = Assert.IsType<Dictionary<string, object?>>(hiddenObject);
            Assert.True(Assert.IsType<bool>(hiddenView["isFaceDown"]));
            Assert.DoesNotContain("power", hiddenView.Keys);
            Assert.DoesNotContain("tags", hiddenView.Keys);
            Assert.DoesNotContain("cardNo", hiddenView.Keys);
        }
    }

    private static MatchState IdleNeutralState()
    {
        return new MatchState(
            "spell-duel-battle-idle-room",
            1,
            1,
            "P1",
            Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            });
    }

    private static MatchState SpellDuelStackState()
    {
        var state = MultiContestSpellDuelState();
        return state with
        {
            TimingState = TimingStates.SpellDuelClosed,
            FocusPlayerId = null,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = ["P2"],
            StackItems =
            [
                new StackItemState(
                    "STACK-SWIFT-A",
                    "P1",
                    "P1-SWIFT-SOURCE",
                    "UNKNOWN_NOOP_EFFECT",
                    timingContext: TimingStates.SpellDuelOpen)
            ]
        };
    }

    private static MatchState StartBattleTaskState(bool includeOpponentHiddenStandby = false)
    {
        return MultiContestSpellDuelState(includeOpponentHiddenStandby: includeOpponentHiddenStandby) with
        {
            TimingState = TimingStates.NeutralOpen,
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            ActivePlayerId = "P1",
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF-A")]
        };
    }

    private static MatchState MultiContestSpellDuelState(
        bool lethalFirstDefender = false,
        bool includeOpponentHiddenStandby = false,
        IReadOnlyList<string>? passedFocusPlayerIds = null)
    {
        var p2Battlefields = includeOpponentHiddenStandby
            ? new[] { "P2-A", "P2-B", "BF-HIDDEN", "P2-HIDDEN-STANDBY" }
            : ["P2-A", "P2-B"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF-A"] = Battlefield("BF-A", "P1"),
            ["BF-B"] = Battlefield("BF-B", "P1"),
            ["P1-A"] = Unit("P1-A", "P1", power: 4),
            ["P2-A"] = Unit("P2-A", "P2", power: 3, damage: lethalFirstDefender ? 3 : 0),
            ["P1-B"] = Unit("P1-B", "P1", power: 2),
            ["P2-B"] = Unit("P2-B", "P2", power: 2)
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF-A"] = new("P1", "BATTLEFIELD", "BF-A"),
            ["BF-B"] = new("P1", "BATTLEFIELD", "BF-B"),
            ["P1-A"] = new("P1", "BATTLEFIELD", "BF-A"),
            ["P2-A"] = new("P2", "BATTLEFIELD", "BF-A"),
            ["P1-B"] = new("P1", "BATTLEFIELD", "BF-B"),
            ["P2-B"] = new("P2", "BATTLEFIELD", "BF-B")
        };
        if (includeOpponentHiddenStandby)
        {
            cardObjects["BF-HIDDEN"] = Battlefield("BF-HIDDEN", "P2");
            cardObjects["P2-HIDDEN-STANDBY"] = new CardObjectState(
                "P2-HIDDEN-STANDBY",
                isFaceDown: true,
                power: 1,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: "P2",
                controllerId: "P2");
            objectLocations["BF-HIDDEN"] = new("P2", "BATTLEFIELD", "BF-HIDDEN");
            objectLocations["P2-HIDDEN-STANDBY"] = new("P2", "BATTLEFIELD", "BF-HIDDEN");
        }

        return new MatchState(
            "spell-duel-battle-state-machine-room",
            10,
            3,
            "P1",
            Seats(),
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
                    Battlefields = ["BF-A", "BF-B", "P1-A", "P1-B"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            cardObjects: cardObjects,
            focusPlayerId: "P1",
            passedFocusPlayerIds: passedFocusPlayerIds ?? [],
            objectLocations: objectLocations);
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
        int power,
        int damage = 0)
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

    private static Dictionary<string, object?> PlayerView(SnapshotDto snapshot, string playerId)
    {
        return Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
    }

    private static Dictionary<string, object?> ZoneView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["zones"]);
    }

    private static Dictionary<string, object?> ObjectView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["objects"]);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
    }
}
