using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEngineRejectsPassPriorityOutsidePriorityWindow()
    {
        var state = new MatchState(
            "p2-pass-priority-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-pass-priority", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("让过优先权只能在优先行动窗口中提交。", result.ErrorMessage);
        Assert.DoesNotContain("PASS_PRIORITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal("MAIN", result.State.Phase);
        Assert.Equal(new[] { "END_TURN", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEngineTransfersPriorityWhenStackItemIsPending()
    {
        var state = new MatchState(
            "p2-fepr-priority-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralClosed,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            "P1",
            [],
            [new StackItemState("STACK-001", "P1", "P1-ABILITY-001", "TEST_RESOLVE")]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-pass-priority", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new[] { "PRIORITY_PASSED" }, result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal("P2", result.State.PriorityPlayerId);
        Assert.Equal(new[] { "P1" }, result.State.PassedPriorityPlayerIds);
        Assert.Single(result.State.StackItems);
        Assert.Equal(new[] { "WAIT", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
        Assert.Equal(new[] { "PASS_PRIORITY", "SURRENDER" }, result.Prompts["P2"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEnginePassPriorityDoesNotEndTurnInOrdinaryMainPhase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-pass-priority-does-not-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal(7, result.FinalState.TurnNumber);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_OPEN", result.FinalState.TimingState);
        Assert.Equal(new[] { "END_TURN", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
        Assert.False(result.Prompts["P2"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineResolvesStackWhenAllPlayersPassPriority()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-fepr-priority-pass-resolves-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal(7, result.FinalState.TurnNumber);
        Assert.Equal("P1", result.FinalState.ActivePlayerId);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_OPEN", result.FinalState.TimingState);
        Assert.Null(result.FinalState.PriorityPlayerId);
        Assert.Empty(result.FinalState.PassedPriorityPlayerIds);
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(new[] { "END_TURN", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
        Assert.False(result.Prompts["P2"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineReturnsPriorityToLatestRemainingStackController()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-fepr-resolves-latest-keeps-remaining-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("P1", result.FinalState.ActivePlayerId);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_CLOSED", result.FinalState.TimingState);
        Assert.Equal("P1", result.FinalState.PriorityPlayerId);
        Assert.Empty(result.FinalState.PassedPriorityPlayerIds);
        var remaining = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-OLDER", remaining.StackItemId);
        Assert.Equal("P1", remaining.ControllerId);
        Assert.Equal(new[] { "PASS_PRIORITY", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
        Assert.False(result.Prompts["P2"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPassFocusOutsideSpellDuel()
    {
        var state = new MatchState(
            "p2-pass-focus-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-pass-focus", "P1", "PASS_FOCUS"),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("让过焦点只能在法术对决焦点窗口中提交。", result.ErrorMessage);
        Assert.DoesNotContain("PASS_FOCUS", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new[] { "END_TURN", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEngineTransfersFocusInSpellDuel()
    {
        var state = new MatchState(
            "p2-spell-duel-focus-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.SpellDuelOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            null,
            [],
            [],
            "P1",
            []);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-pass-focus", "P1", "PASS_FOCUS"),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new[] { "FOCUS_PASSED" }, result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Equal(new[] { "P1" }, result.State.PassedFocusPlayerIds);
        Assert.Equal(new[] { "WAIT", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
        Assert.Equal(new[] { "PASS_FOCUS", "SURRENDER" }, result.Prompts["P2"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEngineRejectedSpellDuelFocusPromptIncludesPlayableSwiftCard()
    {
        var state = new MatchState(
            "spell-duel-playable-focus-room",
            1,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-HEXTECH-RAY-001"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-HEXTECH-RAY-001"] = new(
                    "P2-UNIT-HEXTECH-RAY-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            focusPlayerId: "P1",
            passedFocusPlayerIds: []);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-invalid-move", "P1", "MOVE_UNIT"),
            new MoveUnitCommand("P1-SPELL-HEXTECH-RAY", "BASE", "BATTLEFIELD", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(["PLAY_CARD", "PASS_FOCUS", "SURRENDER"], result.Prompts["P1"].EnabledActions());
        var playCandidate = Assert.Single(
            result.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-SPELL-HEXTECH-RAY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineClosesSpellDuelWhenAllPlayersPassFocus()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-spell-duel-pass-focus-closes-window.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("P1", result.FinalState.ActivePlayerId);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_OPEN", result.FinalState.TimingState);
        Assert.Null(result.FinalState.FocusPlayerId);
        Assert.Empty(result.FinalState.PassedFocusPlayerIds);
        Assert.Equal(new[] { "END_TURN", "SURRENDER" }, result.Prompts["P1"].EnabledActions());
        Assert.False(result.Prompts["P2"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineRunsStateBasedCleanupAfterSpellDuelCloses()
    {
        var state = new MatchState(
            "spell-duel-close-cleanup-room",
            4,
            2,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-SPELL-DUEL-LETHAL-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPELL-DUEL-LETHAL-UNIT"] = new(
                    "P2-SPELL-DUEL-LETHAL-UNIT",
                    cardNo: "SFD·125/221",
                    damage: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            focusPlayerId: "P1",
            passedFocusPlayerIds: ["P2"],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P2-SPELL-DUEL-LETHAL-UNIT"] = new("P2", "BASE")
            });

        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "P2-SPELL-DUEL-LETHAL-UNIT", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-pass-focus-closes-and-cleans", "P1", "PASS_FOCUS"),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.FocusPlayerId);
        Assert.DoesNotContain("P2-SPELL-DUEL-LETHAL-UNIT", result.State.PlayerZones["P2"].Base);
        Assert.Contains("P2-SPELL-DUEL-LETHAL-UNIT", result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P2-SPELL-DUEL-LETHAL-UNIT"].Zone);
        Assert.Equal(["P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.DoesNotContain(
            result.State.PendingCleanupTasks,
            task => string.Equals(task.Kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal));
        Assert.Equal(["END_TURN", "SURRENDER"], result.Prompts["P1"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEngineStartsBattlefieldSpellDuelAfterStackResolutionLeavesContestedBattlefield()
    {
        var state = new MatchState(
            "battlefield-contest-starts-spell-duel-room",
            7,
            3,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-UNIT-1"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-1"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-1"] = new(
                    "P1-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-1"] = new(
                    "P2-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new StackItemState(
                    "STACK-CONTEST-1",
                    "P1",
                    "P1-SPELL-CONTEST",
                    "TEST_RESOLVE",
                    "TEST-000",
                    [])
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-UNIT-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-UNIT-1"] = new("P2", "BATTLEFIELD", "BF-1")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-priority-contested-battlefield", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(8, result.State.Tick);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED", "BATTLE_ROLES_ESTABLISHED"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.StackItems);
        Assert.Collection(
            result.State.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("ACTIVE", task.Status);
                Assert.Equal("P2", task.ActingPlayerId);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("ACTIVE", task.Status);
            });
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-1", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(["PASS_FOCUS", "SURRENDER"], result.Prompts["P2"].EnabledActions());
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P1"].EnabledActions());

        var startedEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
        Assert.Equal("BF-1", startedEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("P2", startedEvent.Payload["focusPlayerId"]);
        Assert.Equal(["P1", "P2"], Assert.IsType<string[]>(startedEvent.Payload["participantControllerIds"]));
        Assert.Equal(["P1-UNIT-1", "P2-UNIT-1"], Assert.IsType<string[]>(startedEvent.Payload["participantObjectIds"]));
    }

    [Fact]
    public async Task CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus()
    {
        var state = new MatchState(
            "battlefield-contest-closes-spell-duel-room",
            8,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-UNIT-1"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-1"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-1"] = new(
                    "P1-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-1"] = new(
                    "P2-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            focusPlayerId: "P1",
            passedFocusPlayerIds: ["P2"],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-UNIT-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-UNIT-1"] = new("P2", "BATTLEFIELD", "BF-1")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-pass-focus-contested-battlefield", "P1", "PASS_FOCUS"),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(9, result.State.Tick);
        Assert.Equal(["FOCUS_PASSED", "SPELL_DUEL_CLOSED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-1"), result.State.UntilEndOfTurnEffects);
        Assert.Collection(
            result.State.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("COMPLETED", task.Status);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("PENDING", task.Status);
            });
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], result.Prompts["P2"].EnabledActions());
        Assert.True(result.Prompts["P2"].Actionable);
        Assert.Contains("争夺战场", result.Prompts["P2"].Reason, StringComparison.Ordinal);

        var closedEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_CLOSED", StringComparison.Ordinal));
        Assert.Equal(["BF-1"], Assert.IsType<string[]>(closedEvent.Payload["completedBattlefieldObjectIds"]));
    }

    [Fact]
    public async Task CoreRuleEngineSkipsStartBattleWhenSpellDuelCleanupRemovesParticipant()
    {
        var state = new MatchState(
            "battlefield-contest-spell-duel-cleanup-removes-participant-room",
            8,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-UNIT-1"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-1"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-1"] = new(
                    "P1-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-1"] = new(
                    "P2-UNIT-1",
                    cardNo: "SFD·125/221",
                    damage: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            focusPlayerId: "P1",
            passedFocusPlayerIds: ["P2"],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-UNIT-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-UNIT-1"] = new("P2", "BATTLEFIELD", "BF-1")
            });

        Assert.Contains(
            state.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "P2-UNIT-1", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-pass-focus-contested-battlefield-cleanup-removes-participant", "P1", "PASS_FOCUS"),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(9, result.State.Tick);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTROL_RESOLVED"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-1"), result.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain("P2-UNIT-1", result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-UNIT-1", result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P2-UNIT-1"].Zone);
        Assert.Null(result.State.ObjectLocations["P2-UNIT-1"].BattlefieldObjectId);

        var battlefield = Assert.Single(result.State.BattlefieldStates.Values);
        Assert.False(battlefield.Contested);
        Assert.Equal("P1", battlefield.ControllerId);
        Assert.Equal(["P1-UNIT-1"], battlefield.OccupantObjectIds);
        Assert.Equal(["P1"], battlefield.OccupantControllerIds);
        Assert.Empty(battlefield.StandbyObjectIds);

        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.State.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.DoesNotContain("DECLARE_BATTLE", result.Prompts["P1"].EnabledActions());
        Assert.Equal(["MOVE_UNIT", "END_TURN", "SURRENDER"], result.Prompts["P1"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask()
    {
        var state = new MatchState(
            "battlefield-contest-start-battle-room",
            9,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-UNIT-1"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-1"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-1"] = new(
                    "P1-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1"),
                ["P2-UNIT-1"] = new(
                    "P2-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-UNIT-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-UNIT-1"] = new("P2", "BATTLEFIELD", "BF-1")
            });

        Assert.Equal("BATTLE_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], ResolutionResult.BuildPrompts(state)["P1"].EnabledActions());

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-declare-active-start-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BF-1",
                ["P1-UNIT-1"],
                ["P2-UNIT-1"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(10, result.State.Tick);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.DoesNotContain("P2-UNIT-1", result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.False(result.State.BattleState.IsActive);
        Assert.False(result.State.CardObjects["P1-UNIT-1"].IsAttacking);
        Assert.Equal("P1", result.State.CardObjects["BF-1"].ControllerId);
        Assert.DoesNotContain("START_BATTLE", result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains("P2-UNIT-1", result.State.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task DeclareBattleDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var state = ActiveStartBattleTaskStateForCommandGuards();
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new DeclareBattleCommand(
            "BF-1",
            ["P1-UNIT-1"],
            ["P2-UNIT-1"],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.DeclareBattle, prompt.EnabledActions());
        var rawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.DeclareBattle,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            battlefieldId = "BF-1",
            attackerObjectIds = new[] { "P1-UNIT-1" },
            defenderObjectIds = new[] { "P2-UNIT-1" },
            optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
        });
        const string clientIntentId = "intent-p1-declare-battle-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(10, accepted.State.Tick);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.DoesNotContain("P2-UNIT-1", accepted.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-UNIT-1", accepted.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("IDLE", accepted.State.PendingTaskQueue.Phase);
        Assert.False(accepted.State.BattleState.IsActive);
        Assert.False(accepted.State.CardObjects["P1-UNIT-1"].IsAttacking);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.DeclareBattle, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.DeclareBattle, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal("BF-1", journalEntry.RawCommand.Value.GetProperty("battlefieldId").GetString());
        Assert.Equal(
            ["P1-UNIT-1"],
            journalEntry.RawCommand.Value.GetProperty("attackerObjectIds")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal(
            ["P2-UNIT-1"],
            journalEntry.RawCommand.Value.GetProperty("defenderObjectIds")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal(
            ["COMBAT_ASSIGNMENT"],
            journalEntry.RawCommand.Value.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.DoesNotContain("P2-UNIT-1", replay.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-UNIT-1", replay.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("IDLE", replay.State.PendingTaskQueue.Phase);
        Assert.False(replay.State.BattleState.IsActive);
        Assert.Single(journal.Entries);

        var reorderedRawCommand = JsonSerializer.SerializeToElement(new
        {
            optionalCosts = new[] { "COMBAT_ASSIGNMENT" },
            defenderObjectIds = new[] { "P2-UNIT-1" },
            attackerObjectIds = new[] { "P1-UNIT-1" },
            battlefieldId = "BF-1",
            snapshotTick = prompt.SnapshotTick,
            promptId = prompt.PromptId,
            cmdType = CommandTypes.DeclareBattle
        });
        var reorderedReplay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            reorderedRawCommand,
            CancellationToken.None);

        Assert.True(reorderedReplay.Accepted, reorderedReplay.ErrorMessage);
        Assert.Null(reorderedReplay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(reorderedReplay.State));
        Assert.Equal(accepted.State.Tick, reorderedReplay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(reorderedReplay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(reorderedReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(reorderedReplay.Snapshots));
        Assert.DoesNotContain("P2-UNIT-1", reorderedReplay.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-UNIT-1", reorderedReplay.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("IDLE", reorderedReplay.State.PendingTaskQueue.Phase);
        Assert.False(reorderedReplay.State.BattleState.IsActive);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.DeclareBattle,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            battlefieldId = "BF-1",
            attackerObjectIds = new[] { "P1-UNIT-1" },
            defenderObjectIds = new[] { "P2-UNIT-1" },
            optionalCosts = new[] { "COMBAT_ASSIGNMENT" },
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.DoesNotContain("P2-UNIT-1", conflict.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-UNIT-1", conflict.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("IDLE", conflict.State.PendingTaskQueue.Phase);
        Assert.False(conflict.State.BattleState.IsActive);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsNonActivePlayerDeclareBattleForActiveStartBattleTask()
    {
        var state = ActiveStartBattleTaskStateForCommandGuards();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-declare-active-start-battle", "P2", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BF-1",
                ["P2-UNIT-1"],
                ["P1-UNIT-1"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Contains("开始战斗", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("START_BATTLE", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Empty(result.Events);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P2"].EnabledActions());
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], result.Prompts["P1"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDeclareBattleThatDoesNotMatchActiveStartBattleTask()
    {
        var state = ActiveStartBattleTaskStateForCommandGuards(includeOtherBattlefield: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-declare-wrong-start-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BF-2",
                ["P1-UNIT-1"],
                ["P2-UNIT-1"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("声明战斗必须匹配当前争夺战场的开始战斗任务。", result.ErrorMessage);
        Assert.DoesNotContain("DECLARE_BATTLE", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("START_BATTLE", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Empty(result.Events);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], result.Prompts["P1"].EnabledActions());
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", result.State.PendingTaskQueue.ActiveTaskId);
    }

    private static MatchState ActiveStartBattleTaskStateForCommandGuards(bool includeOtherBattlefield = false)
    {
        var p2Battlefields = includeOtherBattlefield
            ? new[] { "P2-UNIT-1", "BF-2" }
            : ["P2-UNIT-1"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF-1"] = new(
                "BF-1",
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-UNIT-1"] = new(
                "P1-UNIT-1",
                cardNo: "SFD·125/221",
                power: 4,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P2-UNIT-1"] = new(
                "P2-UNIT-1",
                cardNo: "SFD·125/221",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-UNIT-1"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P2-UNIT-1"] = new("P2", "BATTLEFIELD", "BF-1")
        };
        if (includeOtherBattlefield)
        {
            cardObjects["BF-2"] = new(
                "BF-2",
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2");
            objectLocations["BF-2"] = new("P2", "BATTLEFIELD", "BF-2");
        }

        return new MatchState(
            "battlefield-contest-start-battle-command-guard-room",
            9,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-UNIT-1"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")],
            objectLocations: objectLocations);
    }

    [Fact]
    public async Task CoreRuleEngineChangesBattlefieldControllerAfterBattle()
    {
        var state = new MatchState(
            "battlefield-control-resolution-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-2", "P2-DEFENDER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-2"] = new(
                    "BF-2",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-ATTACKER"] = new(
                    "P1-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-2")],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-2"] = new("P2", "BATTLEFIELD", "BF-2"),
                ["P1-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-2"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-2")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-battlefield-control-resolution", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BF-2",
                ["P1-ATTACKER"],
                ["P2-DEFENDER"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P1", result.State.CardObjects["BF-2"].ControllerId);
        Assert.False(result.State.BattleState.IsActive);
        var controlEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal));
        Assert.Equal("P2", controlEvent.Payload["previousControllerId"]);
        Assert.Equal("P1", controlEvent.Payload["controllerId"]);
        Assert.Equal("CONTROL_CHANGED", controlEvent.Payload["resolution"]);
        Assert.True(Assert.IsType<bool>(controlEvent.Payload["changed"]));
        var controlResolution = Assert.Single(
            result.State.BattlefieldResolutions,
            resolution => string.Equals(resolution.Kind, "CONTROL_RESOLVED", StringComparison.Ordinal));
        Assert.Equal(result.State.Tick, controlResolution.Tick);
        Assert.Equal("BF-2", controlResolution.BattlefieldObjectId);
        Assert.Equal("P2", controlResolution.PreviousControllerId);
        Assert.Equal("P1", controlResolution.ControllerId);
        Assert.Equal("CONTROL_CHANGED", controlResolution.Reason);
        var snapshotResolutions = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["battlefieldResolutions"]);
        var snapshotControlResolution = Assert.Single(
            snapshotResolutions,
            resolution => string.Equals(resolution["kind"] as string, "CONTROL_RESOLVED", StringComparison.Ordinal));
        Assert.Equal("BF-2", snapshotControlResolution["battlefieldObjectId"]);
        Assert.Equal("P1", snapshotControlResolution["controllerId"]);
    }

    [Fact]
    public async Task CoreRuleEngineRemovesIllegalStandbyAfterBattlefieldControlChanges()
    {
        var state = new MatchState(
            "battlefield-control-standby-cleanup-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-2", "P1-ATTACKER", "P1-STANDBY-1"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DEFENDER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-2"] = new(
                    "BF-2",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ATTACKER"] = new(
                    "P1-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STANDBY-1"] = new(
                    "P1-STANDBY-1",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: "P1"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-2")],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-2"] = new("P1", "BATTLEFIELD", "BF-2"),
                ["P1-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-2"),
                ["P1-STANDBY-1"] = new("P1", "BATTLEFIELD", "BF-2"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-2")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-battlefield-control-standby-cleanup", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BF-2",
                ["P1-ATTACKER"],
                ["P2-DEFENDER"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.CardObjects["BF-2"].ControllerId);
        Assert.DoesNotContain("P1-STANDBY-1", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("P1-STANDBY-1", result.State.PlayerZones["P1"].Graveyard);
        Assert.False(result.State.CardObjects["P1-STANDBY-1"].IsFaceDown);
        Assert.Equal("P1", result.State.CardObjects["P1-STANDBY-1"].ControllerId);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P1-STANDBY-1"].Zone);
        var standbyRemovedEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_STANDBY_REMOVED", StringComparison.Ordinal));
        Assert.Equal("BF-2", standbyRemovedEvent.Payload["battlefieldObjectId"]);
        Assert.Equal(["P1-STANDBY-1"], Assert.IsType<object[]>(standbyRemovedEvent.Payload["removedObjectIds"]));
        var removedCards = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(standbyRemovedEvent.Payload["removedCards"]);
        var removedCard = Assert.Single(removedCards);
        Assert.Equal("P1", removedCard["previousControllerId"]);
    }

    [Fact]
    public async Task CoreRuleEngineKeepsLegacyOwnedStandbyAfterBattlefieldControlConfirmed()
    {
        var state = new MatchState(
            "battlefield-control-legacy-standby-kept-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-2", "P2-DEFENDER", "P2-STANDBY-LEGAL"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-2"] = new(
                    "BF-2",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-ATTACKER"] = new(
                    "P1-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-STANDBY-LEGAL"] = new(
                    "P2-STANDBY-LEGAL",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: "P2")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-2")],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-2"] = new("P2", "BATTLEFIELD", "BF-2"),
                ["P1-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-2"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-2"),
                ["P2-STANDBY-LEGAL"] = new("P2", "BATTLEFIELD", "BF-2")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-battlefield-control-legacy-standby-kept", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BF-2",
                ["P1-ATTACKER"],
                ["P2-DEFENDER"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("P2", result.State.CardObjects["BF-2"].ControllerId);
        Assert.Contains("P2-STANDBY-LEGAL", result.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("P2-STANDBY-LEGAL", result.State.PlayerZones["P2"].Graveyard);
        Assert.True(result.State.CardObjects["P2-STANDBY-LEGAL"].IsFaceDown);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_STANDBY_REMOVED", StringComparison.Ordinal));
    }
}
