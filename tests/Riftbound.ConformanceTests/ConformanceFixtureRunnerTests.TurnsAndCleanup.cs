using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEngineResolvesP2TurnStartPreflightFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.NotNull(fixture.Expected.FinalState);
        Assert.Equal(fixture.Expected.FinalState.TurnNumber, result.FinalState.TurnNumber);
        Assert.Equal(fixture.Expected.FinalState.ActivePlayerId, result.FinalState.ActivePlayerId);
        Assert.Equal(fixture.Expected.FinalState.TurnPlayerId, result.FinalState.TurnPlayerId);
        Assert.Equal(fixture.Expected.FinalState.Phase, result.FinalState.Phase);
        Assert.Equal(fixture.Expected.FinalState.TimingState, result.FinalState.TimingState);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Empty(result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Empty(result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            new[] { "P2-RUNE-001", "P2-RUNE-002" },
            result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEngineStopsTurnStartRuneCallAtOpponentControlledRuneDeckObject()
    {
        var state = new MatchState(
            "p2-turn-start-dirty-rune-room",
            0,
            4,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
            phase: MatchPhases.TurnStart,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 1)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-DIRTY-RUNE-P1-OWNED", "P2-RUNE-003"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-RUNE-001"] = new(
                    "P2-RUNE-001",
                    cardNo: "RUN·001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-RUNE-P1-OWNED"] = new(
                    "P2-DIRTY-RUNE-P1-OWNED",
                    cardNo: "RUN·002",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1"),
                ["P2-RUNE-003"] = new(
                    "P2-RUNE-003",
                    cardNo: "RUN·003",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-MAIN-001"] = new(
                    "P2-MAIN-001",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-turn-start-dirty-rune", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(
            ["P2-DIRTY-RUNE-P1-OWNED", "P2-RUNE-003"],
            result.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(["P2-RUNE-001"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(["P2-MAIN-001"], result.State.PlayerZones["P2"].Hand);

        var runeCallEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal(1, runeCallEvent.Payload["count"]);
        Assert.Equal("BASE", result.State.ObjectLocations["P2-RUNE-001"].Zone);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations["P2-DIRTY-RUNE-P1-OWNED"].Zone);
        Assert.False(result.State.CardObjects["P2-DIRTY-RUNE-P1-OWNED"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineStopsTurnStartDrawAtOpponentControlledMainDeckObject()
    {
        var state = new MatchState(
            "p2-turn-start-dirty-draw-room",
            0,
            4,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
            phase: MatchPhases.TurnStart,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 1)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-DIRTY-MAIN-P1-OWNED", "P2-MAIN-002"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-MAIN-P1-OWNED"] = new(
                    "P2-DIRTY-MAIN-P1-OWNED",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1"),
                ["P2-MAIN-002"] = new(
                    "P2-MAIN-002",
                    cardNo: "SFD·126/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUNE-001"] = new(
                    "P2-RUNE-001",
                    cardNo: "RUN·001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUNE-002"] = new(
                    "P2-RUNE-002",
                    cardNo: "RUN·002",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-turn-start-dirty-draw", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(
            ["P2-DIRTY-MAIN-P1-OWNED", "P2-MAIN-002"],
            result.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-RUNE-001", "P2-RUNE-002"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(0, result.State.PlayerScores["P1"]);
        Assert.Equal(0, result.State.PlayerScores["P2"]);

        var drawEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(0, drawEvent.Payload["count"]);
        Assert.Equal("MAIN_DECK", result.State.ObjectLocations["P2-DIRTY-MAIN-P1-OWNED"].Zone);
    }

    [Fact]
    public async Task CoreRuleEngineBurnoutRecyclesOnlyControlledGraveyardCards()
    {
        var state = new MatchState(
            "p2-turn-start-dirty-graveyard-room",
            0,
            4,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
            phase: MatchPhases.TurnStart,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 1)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Graveyard = ["P2-GRAVE-OWNED", "P2-DIRTY-GRAVE-P1-OWNED"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-GRAVE-OWNED"] = new(
                    "P2-GRAVE-OWNED",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-GRAVE-P1-OWNED"] = new(
                    "P2-DIRTY-GRAVE-P1-OWNED",
                    cardNo: "SFD·126/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1"),
                ["P2-RUNE-001"] = new(
                    "P2-RUNE-001",
                    cardNo: "RUN·001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUNE-002"] = new(
                    "P2-RUNE-002",
                    cardNo: "RUN·002",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-turn-start-dirty-graveyard", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Empty(result.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P2-GRAVE-OWNED"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-DIRTY-GRAVE-P1-OWNED"], result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal(0, result.State.PlayerScores["P2"]);

        Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BURNOUT_APPLIED", StringComparison.Ordinal));
        var drawEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P2-DIRTY-GRAVE-P1-OWNED"].Zone);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsTurnStartAdvanceFromNonTurnPlayer()
    {
        var state = new MatchState(
            "p2-turn-start-guard-room",
            0,
            4,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P2",
            MatchPhases.TurnStart,
            TimingStates.NeutralClosed,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 1)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002"]
                }
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-turn-start-advance-rejected", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("回合开始只能由当前回合玩家推进。", result.ErrorMessage);
        Assert.DoesNotContain("TURN_START", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal("P2", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.TurnStart, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal(new RunePool(1, 1), result.State.RunePools["P2"]);
        Assert.Equal(["P2-RUNE-001", "P2-RUNE-002"], result.State.PlayerZones["P2"].RuneDeck);
        Assert.Empty(result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsGameCommandAfterMatchFinished()
    {
        var state = PunishmentState(mana: 0) with
        {
            Status = MatchStatuses.Finished,
            WinnerPlayerId = "P1"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-end-turn-after-finished", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("对局已经结束，不能继续提交行动。", result.ErrorMessage);
        Assert.DoesNotContain("END_TURN", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task CoreRuleEngineSurrenderFinishesMatchAndOpponentWins()
    {
        var state = PunishmentState(mana: 0);
        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.Contains("SURRENDER", prompts["P1"].EnabledActions());
        Assert.Contains("SURRENDER", prompts["P2"].EnabledActions());
        Assert.Contains(prompts["P2"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, "SURRENDER", StringComparison.Ordinal)
            && candidate.Enabled);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-surrender", "P1", "SURRENDER"),
            new SurrenderCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.Equal("P2", result.State.WinnerPlayerId);
        var winEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        Assert.Equal("P2", winEvent.Payload["winnerPlayerId"]);
        Assert.Equal("P1", winEvent.Payload["surrenderedPlayerId"]);
        Assert.Equal("SURRENDER", winEvent.Payload["reason"]);
        Assert.Equal("P2", result.Snapshots["P1"].Timing["winnerPlayerId"]);
        Assert.Equal("P2", result.Snapshots["P2"].Timing["winnerPlayerId"]);
        Assert.All(result.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAcceptedSurrenderReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 0);
        var command = new SurrenderCommand();

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p1-surrender-before-replay", "P1", CommandTypes.Surrender),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.Finished, accepted.State.Status);
        Assert.Equal("P2", accepted.State.WinnerPlayerId);
        Assert.Equal(["MATCH_WON"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.All(accepted.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        AssertSurrenderFinishedPromptQueueAudit(accepted);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-p1-surrender-stale-replay", "P1", CommandTypes.Surrender),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(MatchStatuses.Finished, replay.State.Status);
        Assert.Equal("P2", replay.State.WinnerPlayerId);
        Assert.All(replay.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        Assert.DoesNotContain(CommandTypes.Surrender, replay.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Surrender, replay.Prompts["P2"].EnabledActions());
        AssertSurrenderFinishedPromptQueueAudit(replay);
    }

    [Fact]
    public async Task SurrenderStalePromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        var state = PunishmentState(mana: 0);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new SurrenderCommand();

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.Surrender, prompt.EnabledActions());
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.Surrender, prompt);
        const string acceptedClientIntentId = "intent-p1-surrender-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-p1-surrender-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(MatchStatuses.Finished, accepted.State.Status);
        Assert.Equal("P2", accepted.State.WinnerPlayerId);
        Assert.Equal(["MATCH_WON"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.All(accepted.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        AssertSurrenderFinishedPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.Surrender, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.Surrender, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(MatchStatuses.Finished, replay.State.Status);
        Assert.Equal("P2", replay.State.WinnerPlayerId);
        Assert.All(replay.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        Assert.DoesNotContain(CommandTypes.Surrender, replay.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Surrender, replay.Prompts["P2"].EnabledActions());
        AssertSurrenderFinishedPromptQueueAudit(replay);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.Surrender, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.Surrender, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(accepted.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(MatchStatuses.Finished, duplicateRejected.State.Status);
        Assert.Equal("P2", duplicateRejected.State.WinnerPlayerId);
        Assert.All(duplicateRejected.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        Assert.DoesNotContain(CommandTypes.Surrender, duplicateRejected.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Surrender, duplicateRejected.Prompts["P2"].EnabledActions());
        AssertSurrenderFinishedPromptQueueAudit(duplicateRejected);
        Assert.Equal(2, journal.Entries.Count);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.Surrender,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
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
        Assert.Equal(MatchStatuses.Finished, conflict.State.Status);
        Assert.Equal("P2", conflict.State.WinnerPlayerId);
        Assert.All(conflict.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        Assert.DoesNotContain(CommandTypes.Surrender, conflict.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Surrender, conflict.Prompts["P2"].EnabledActions());
        AssertSurrenderFinishedPromptQueueAudit(conflict);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SurrenderDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var state = PunishmentState(mana: 0);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new SurrenderCommand();

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.Surrender, prompt.EnabledActions());
        var rawCommand = PromptScopedRawCommand(CommandTypes.Surrender, prompt);
        const string clientIntentId = "intent-p1-surrender-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(MatchStatuses.Finished, accepted.State.Status);
        Assert.Equal("P2", accepted.State.WinnerPlayerId);
        Assert.Equal(["MATCH_WON"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.All(accepted.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        AssertSurrenderFinishedPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.Surrender, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.Surrender, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
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
        Assert.Equal(MatchStatuses.Finished, replay.State.Status);
        Assert.Equal("P2", replay.State.WinnerPlayerId);
        Assert.All(replay.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        Assert.DoesNotContain(CommandTypes.Surrender, replay.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Surrender, replay.Prompts["P2"].EnabledActions());
        AssertSurrenderFinishedPromptQueueAudit(replay);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.Surrender,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
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
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(MatchStatuses.Finished, conflict.State.Status);
        Assert.Equal("P2", conflict.State.WinnerPlayerId);
        Assert.All(conflict.Prompts.Values, prompt => Assert.Equal(["WAIT"], prompt.EnabledActions()));
        Assert.DoesNotContain(CommandTypes.Surrender, conflict.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Surrender, conflict.Prompts["P2"].EnabledActions());
        AssertSurrenderFinishedPromptQueueAudit(conflict);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertSurrenderFinishedPromptQueueAudit(ResolutionResult result)
    {
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.Equal("P2", result.State.WinnerPlayerId);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);

        Assert.Equal("P2", result.Snapshots["P1"].Timing["winnerPlayerId"]);
        Assert.Equal("P2", result.Snapshots["P2"].Timing["winnerPlayerId"]);
        Assert.Empty(result.Snapshots["P1"].Stack);
        Assert.Empty(result.Snapshots["P2"].Stack);

        var p1Queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.False(Assert.IsType<bool>(p1Queue["hasTasks"]));
        Assert.False(Assert.IsType<bool>(p1Queue["isBlocking"]));
        Assert.Equal("IDLE", Assert.IsType<string>(p1Queue["phase"]));
        Assert.Null(p1Queue["activeTaskId"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p1Queue["tasks"]));

        foreach (var prompt in result.Prompts.Values)
        {
            Assert.False(prompt.Actionable);
            Assert.Equal(PromptTypes.MatchResult, prompt.View?.Type);
            Assert.Equal(["WAIT"], prompt.EnabledActions());
            Assert.DoesNotContain(CommandTypes.Surrender, prompt.EnabledActions());
        }

        var promptsJson = JsonSerializer.Serialize(result.Prompts);
        Assert.DoesNotContain(CommandTypes.Surrender, promptsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSurrenderWithoutOpponent()
    {
        var state = PunishmentState(mana: 0) with
        {
            Seats = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1"
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-surrender-no-opponent", "P1", "SURRENDER"),
            new SurrenderCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("投降需要存在对手。", result.ErrorMessage);
        Assert.DoesNotContain("SURRENDER", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Null(result.State.WinnerPlayerId);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMulliganAfterMatchFinished()
    {
        var state = PunishmentState(mana: 0) with
        {
            Status = MatchStatuses.Finished,
            WinnerPlayerId = "P1",
            Phase = MatchPhases.Mulligan,
            TimingState = TimingStates.Mulligan
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-mulligan-after-finished", "P1", "MULLIGAN"),
            new MulliganCommand([]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("对局已经结束，不能继续提交行动。", result.ErrorMessage);
        Assert.DoesNotContain("MULLIGAN", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Equal(MatchPhases.Mulligan, result.State.Phase);
        Assert.Equal(TimingStates.Mulligan, result.State.TimingState);
        Assert.Empty(result.State.MulliganCompletedPlayerIds);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMulliganWhenSelectedHandCardIsNotControlledByPlayer()
    {
        var state = PunishmentState(mana: 0) with
        {
            Phase = MatchPhases.Mulligan,
            TimingState = TimingStates.Mulligan,
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand =
                    [
                        "P1-MULLIGAN-HAND-KEEP-001",
                        "P1-MULLIGAN-DIRTY-P2-OWNED",
                        "P1-MULLIGAN-HAND-KEEP-002"
                    ],
                    MainDeck = ["P1-MULLIGAN-DRAW-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MULLIGAN-HAND-KEEP-001"] = new(
                    "P1-MULLIGAN-HAND-KEEP-001",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MULLIGAN-DIRTY-P2-OWNED"] = new(
                    "P1-MULLIGAN-DIRTY-P2-OWNED",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2"),
                ["P1-MULLIGAN-HAND-KEEP-002"] = new(
                    "P1-MULLIGAN-HAND-KEEP-002",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var mulliganCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MULLIGAN", StringComparison.Ordinal));
        Assert.Equal(
            ["P1-MULLIGAN-HAND-KEEP-001", "P1-MULLIGAN-HAND-KEEP-002"],
            (mulliganCandidate.Sources ?? []).Select(source => source.Id).ToArray());

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-mulligan-dirty-hand-card", "P1", "MULLIGAN"),
            new MulliganCommand(["P1-MULLIGAN-DIRTY-P2-OWNED"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("起手调整只能选择自己起手手牌中的牌。", result.ErrorMessage);
        Assert.DoesNotContain("MULLIGAN", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.MulliganCompletedPlayerIds);
        Assert.Equal(
            ["P1-MULLIGAN-HAND-KEEP-001", "P1-MULLIGAN-DIRTY-P2-OWNED", "P1-MULLIGAN-HAND-KEEP-002"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MULLIGAN-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMulliganWhenReplacementDrawIsNotControlledByPlayer()
    {
        var state = PunishmentState(mana: 0) with
        {
            Phase = MatchPhases.Mulligan,
            TimingState = TimingStates.Mulligan,
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-MULLIGAN-HAND-RETURN-001", "P1-MULLIGAN-HAND-KEEP-001"],
                    MainDeck = ["P1-MULLIGAN-DIRTY-P2-OWNED-DRAW"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MULLIGAN-HAND-RETURN-001"] = new(
                    "P1-MULLIGAN-HAND-RETURN-001",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MULLIGAN-HAND-KEEP-001"] = new(
                    "P1-MULLIGAN-HAND-KEEP-001",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MULLIGAN-DIRTY-P2-OWNED-DRAW"] = new(
                    "P1-MULLIGAN-DIRTY-P2-OWNED-DRAW",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-mulligan-dirty-draw-card", "P1", "MULLIGAN"),
            new MulliganCommand(["P1-MULLIGAN-HAND-RETURN-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("起手调整只能从自己的主牌堆抽取替换牌。", result.ErrorMessage);
        Assert.DoesNotContain("MULLIGAN", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.MulliganCompletedPlayerIds);
        Assert.Equal(
            ["P1-MULLIGAN-HAND-RETURN-001", "P1-MULLIGAN-HAND-KEEP-001"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MULLIGAN-DIRTY-P2-OWNED-DRAW"], result.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task P4EphemeralKeywordDestroysControlledObjectsAtTurnStart()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ephemeral-destroys-controlled-objects-turn-start.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        if (Environment.GetEnvironmentVariable("RIFTBOUND_EPHEMERAL_FIXTURE") is { } output)
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
        Assert.DoesNotContain("P2-EPHEMERAL-BASE", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-EPHEMERAL-BATTLEFIELD", result.FinalState.CardObjects.Keys);
        Assert.Contains("P1-EPHEMERAL-OTHER", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield()
    {
        var state = new MatchState(
            "p4-ephemeral-leblanc-static-room",
            0,
            9,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
            phase: MatchPhases.TurnStart,
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
                    Base = ["P1-EPHEMERAL-OTHER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    Base = ["P2-EPHEMERAL-BASE", "P2-KEEP-BASE"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-LEBLANC",
                        "P2-LEBLANC-STATIC",
                        "P2-EPHEMERAL-PROTECTED",
                        "P2-BATTLEFIELD-OTHER",
                        "P2-EPHEMERAL-OTHER-BATTLEFIELD"
                    ]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EPHEMERAL-OTHER"] = new(
                    "P1-EPHEMERAL-OTHER",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-EPHEMERAL-BASE"] = new(
                    "P2-EPHEMERAL-BASE",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-KEEP-BASE"] = new(
                    "P2-KEEP-BASE",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-LEBLANC"] = new(
                    "P2-BATTLEFIELD-LEBLANC",
                    cardNo: "UNL·T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LEBLANC-STATIC"] = new(
                    "P2-LEBLANC-STATIC",
                    cardNo: "UNL-090a/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.BackRow],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-EPHEMERAL-PROTECTED"] = new(
                    "P2-EPHEMERAL-PROTECTED",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-OTHER"] = new(
                    "P2-BATTLEFIELD-OTHER",
                    cardNo: "UNL·T03",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-EPHEMERAL-OTHER-BATTLEFIELD"] = new(
                    "P2-EPHEMERAL-OTHER-BATTLEFIELD",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-EPHEMERAL-OTHER"] = new("P1", "BASE"),
                ["P2-MAIN-001"] = new("P2", "MAIN_DECK"),
                ["P2-EPHEMERAL-BASE"] = new("P2", "BASE"),
                ["P2-KEEP-BASE"] = new("P2", "BASE"),
                ["P2-BATTLEFIELD-LEBLANC"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-LEBLANC-STATIC"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-EPHEMERAL-PROTECTED"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-BATTLEFIELD-OTHER"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-OTHER"),
                ["P2-EPHEMERAL-OTHER-BATTLEFIELD"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-OTHER")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ephemeral-leblanc-static", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_DESTROYED");
        result = await TurnSequenceTestDriver.Complete(result);
        var destroyedObjectIds = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "EPHEMERAL_TURN_START", StringComparison.Ordinal))
            .Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["targetObjectId"]))
            .ToArray();
        Assert.Equal(["P2-EPHEMERAL-BASE", "P2-EPHEMERAL-OTHER-BATTLEFIELD"], destroyedObjectIds);
        Assert.DoesNotContain("P2-EPHEMERAL-BASE", result.State.CardObjects.Keys);
        Assert.DoesNotContain("P2-EPHEMERAL-OTHER-BATTLEFIELD", result.State.CardObjects.Keys);
        Assert.Contains("P2-EPHEMERAL-PROTECTED", result.State.CardObjects.Keys);
        Assert.Contains("P2-LEBLANC-STATIC", result.State.CardObjects.Keys);
        Assert.Equal(["P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P2-EPHEMERAL-PROTECTED"].Zone);
        Assert.Equal("P2-BATTLEFIELD-LEBLANC", result.State.ObjectLocations["P2-EPHEMERAL-PROTECTED"].BattlefieldObjectId);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P2-EPHEMERAL-BASE"].Zone);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P2-EPHEMERAL-OTHER-BATTLEFIELD"].Zone);
        var leblancBattlefield = result.State.BattlefieldStates["P2-BATTLEFIELD-LEBLANC"];
        Assert.Equal(["P2-EPHEMERAL-PROTECTED", "P2-LEBLANC-STATIC"], leblancBattlefield.OccupantObjectIds);
        Assert.Empty(result.State.BattlefieldStates["P2-BATTLEFIELD-OTHER"].OccupantObjectIds);
    }

    [Fact]
    public async Task CoreRuleEngineCallsAvailableRunesWhenRuneDeckIsShort()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-short-rune-deck.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Empty(result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Equal(new[] { "P2-RUNE-ONLY" }, result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineCallsExtraRuneForSecondActionPlayersFirstTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-first-p2-extra-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(new[] { "P2-RUNE-004" }, result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Equal(
            new[] { "P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003" },
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineAppliesBurnoutDuringTurnStartDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-burnout.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(1, result.FinalState.PlayerScores["P1"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P2"]);
        Assert.Empty(result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(new[] { "P2-RECYCLE-001" }, result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEngineAppliesRepeatedBurnoutWinImmediately()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-burnout-empty-graveyard-wins.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal(MatchStatuses.Finished, result.FinalState.Status);
        Assert.Equal("P1", result.FinalState.WinnerPlayerId);
        Assert.Equal(9, result.FinalState.PlayerScores["P1"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P2"]);
        Assert.Equal("TURN_START", result.FinalState.Phase);
        Assert.Empty(result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(new[] { "WAIT" }, result.Prompts["P1"].EnabledActions());
        Assert.Equal(new[] { "WAIT" }, result.Prompts["P2"].EnabledActions());
    }

    [Fact]
    public async Task CoreRuleEngineAdvancesEndTurnToNextTurnStart()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-end-turn-advances-to-next-start.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.NotNull(fixture.Expected.FinalState);
        Assert.Equal(fixture.Expected.FinalState.TurnNumber, result.FinalState.TurnNumber);
        Assert.Equal(fixture.Expected.FinalState.ActivePlayerId, result.FinalState.ActivePlayerId);
        Assert.Equal(fixture.Expected.FinalState.TurnPlayerId, result.FinalState.TurnPlayerId);
        Assert.Equal(fixture.Expected.FinalState.Phase, result.FinalState.Phase);
        Assert.Equal(fixture.Expected.FinalState.TimingState, result.FinalState.TimingState);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P1"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P2"]);
        Assert.Equal(new[] { "P2-RUNE-004" }, result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            new[] { "P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003" },
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Contains("END_TURN", result.Prompts["P2"].EnabledActions());
        Assert.False(result.Prompts["P1"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAcceptedEndTurnReplayWithoutMutation()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-end-turn-advances-to-next-start.fixture.json"),
            CancellationToken.None);
        var accepted = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);
        var acceptedHash = MatchStateHasher.Hash(accepted.FinalState);

        Assert.Equal(1, accepted.FinalTick);
        Assert.Equal("P2", accepted.FinalState.ActivePlayerId);
        Assert.Equal("P2", accepted.FinalState.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, accepted.FinalState.Phase);
        Assert.Equal(TimingStates.NeutralOpen, accepted.FinalState.TimingState);
        Assert.Contains("TURN_PLAYER_ADVANCED", accepted.EventKinds);
        Assert.Contains("END_TURN", accepted.Prompts["P2"].EnabledActions());
        Assert.False(accepted.Prompts["P1"].Actionable);
        AssertEndTurnNextPlayerPromptQueueAudit(accepted.FinalState, accepted.Prompts);

        var replay = await new CoreRuleEngine().ResolveAsync(
            accepted.FinalState,
            new PlayerIntent("intent-p1-end-turn-stale-replay", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal("P2", replay.State.ActivePlayerId);
        Assert.Equal("P2", replay.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, replay.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, replay.State.TimingState);
        Assert.DoesNotContain(CommandTypes.EndTurn, replay.Prompts["P1"].EnabledActions());
        Assert.Contains(CommandTypes.EndTurn, replay.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(replay);
    }

    [Fact]
    public async Task EndTurnStalePromptReplayAfterNextPlayerStartsRejectsWithoutMutation()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = new(0, 1)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PUNISHMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003", "P2-RUNE-004"]
                }
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new EndTurnCommand();

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, prompt.EnabledActions());
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.EndTurn, prompt);
        const string acceptedClientIntentId = "intent-p1-end-turn-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-p1-end-turn-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(1, accepted.State.Tick);
        Assert.Equal("P2", accepted.State.ActivePlayerId);
        Assert.Equal("P2", accepted.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, accepted.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, accepted.State.TimingState);
        Assert.Contains("TURN_PLAYER_ADVANCED", accepted.Events.Select(gameEvent => gameEvent.Kind));
        Assert.False(accepted.Prompts["P1"].Actionable);
        Assert.Contains(CommandTypes.EndTurn, accepted.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.EndTurn, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.EndTurn, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(1, replay.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal("P2", replay.State.ActivePlayerId);
        Assert.Equal("P2", replay.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, replay.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, replay.State.TimingState);
        Assert.False(replay.Prompts["P1"].Actionable);
        Assert.DoesNotContain(CommandTypes.EndTurn, replay.Prompts["P1"].EnabledActions());
        Assert.Contains(CommandTypes.EndTurn, replay.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(replay);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.EndTurn, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.EndTurn, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal("P2", duplicateRejected.State.ActivePlayerId);
        Assert.Equal("P2", duplicateRejected.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, duplicateRejected.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, duplicateRejected.State.TimingState);
        Assert.False(duplicateRejected.Prompts["P1"].Actionable);
        Assert.DoesNotContain(CommandTypes.EndTurn, duplicateRejected.Prompts["P1"].EnabledActions());
        Assert.Contains(CommandTypes.EndTurn, duplicateRejected.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(duplicateRejected);
        Assert.Equal(2, journal.Entries.Count);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.EndTurn,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal("P2", conflict.State.ActivePlayerId);
        Assert.Equal("P2", conflict.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, conflict.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, conflict.State.TimingState);
        Assert.False(conflict.Prompts["P1"].Actionable);
        Assert.DoesNotContain(CommandTypes.EndTurn, conflict.Prompts["P1"].EnabledActions());
        Assert.Contains(CommandTypes.EndTurn, conflict.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(conflict);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EndTurnDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = new(0, 1)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PUNISHMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003", "P2-RUNE-004"]
                }
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new EndTurnCommand();

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, prompt.EnabledActions());
        var rawCommand = PromptScopedRawCommand(CommandTypes.EndTurn, prompt);
        const string clientIntentId = "intent-p1-end-turn-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(1, accepted.State.Tick);
        Assert.Equal("P2", accepted.State.ActivePlayerId);
        Assert.Equal("P2", accepted.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, accepted.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, accepted.State.TimingState);
        Assert.Contains("TURN_PLAYER_ADVANCED", accepted.Events.Select(gameEvent => gameEvent.Kind));
        Assert.False(accepted.Prompts["P1"].Actionable);
        Assert.Contains(CommandTypes.EndTurn, accepted.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.EndTurn, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.EndTurn, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
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
        Assert.Equal("P2", replay.State.ActivePlayerId);
        Assert.Equal("P2", replay.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, replay.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, replay.State.TimingState);
        Assert.False(replay.Prompts["P1"].Actionable);
        Assert.DoesNotContain(CommandTypes.EndTurn, replay.Prompts["P1"].EnabledActions());
        Assert.Contains(CommandTypes.EndTurn, replay.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(replay);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.EndTurn,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
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
        Assert.Equal("P2", conflict.State.ActivePlayerId);
        Assert.Equal("P2", conflict.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, conflict.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, conflict.State.TimingState);
        Assert.False(conflict.Prompts["P1"].Actionable);
        Assert.DoesNotContain(CommandTypes.EndTurn, conflict.Prompts["P1"].EnabledActions());
        Assert.Contains(CommandTypes.EndTurn, conflict.Prompts["P2"].EnabledActions());
        AssertEndTurnNextPlayerPromptQueueAudit(conflict);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertEndTurnNextPlayerPromptQueueAudit(ResolutionResult result)
    {
        AssertEndTurnNextPlayerPromptQueueAudit(result.State, result.Prompts);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal("P2", snapshot.ActivePlayerId);
            Assert.Equal("P2", Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }
    }

    private static void AssertEndTurnNextPlayerPromptQueueAudit(
        MatchState state,
        IReadOnlyDictionary<string, ActionPromptDto> prompts)
    {
        Assert.Equal("P2", state.ActivePlayerId);
        Assert.Equal("P2", state.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, state.Phase);
        Assert.Equal(TimingStates.NeutralOpen, state.TimingState);
        Assert.Empty(state.StackItems);
        Assert.Null(state.PriorityPlayerId);
        Assert.Empty(state.PassedPriorityPlayerIds);
        Assert.Null(state.FocusPlayerId);
        Assert.Empty(state.PassedFocusPlayerIds);
        Assert.Empty(state.BattlefieldTasks);
        Assert.False(state.PendingTaskQueue.HasTasks);
        Assert.False(state.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", state.PendingTaskQueue.Phase);
        Assert.Null(state.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(state.PendingTaskQueue.Tasks);

        Assert.Equal("P1", prompts["P1"].PlayerId);
        Assert.False(prompts["P1"].Actionable);
        Assert.DoesNotContain(CommandTypes.EndTurn, prompts["P1"].EnabledActions());
        Assert.Equal(state.Tick, prompts["P1"].SnapshotTick);
        Assert.DoesNotContain(
            CommandTypes.EndTurn,
            JsonSerializer.Serialize(prompts["P1"]),
            StringComparison.Ordinal);

        Assert.Equal("P2", prompts["P2"].PlayerId);
        Assert.True(prompts["P2"].Actionable);
        Assert.Equal(PromptTypes.MainAction, prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.EndTurn, prompts["P2"].EnabledActions());
        Assert.Equal(state.Tick, prompts["P2"].SnapshotTick);
    }

    [Fact]
    public async Task CoreRuleEngineAcceptsGenericPassInOrdinaryMainWindow()
    {
        var state = PunishmentState(mana: 0);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-generic-pass", "P1", CommandTypes.Pass),
            new PassCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new[] { "TURN_ENDED" }, result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(["P1"], result.State.PassedPriorityPlayerIds);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(CommandTypes.EndTurn, result.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Pass, result.Prompts["P1"].EnabledActions());
        Assert.False(result.Prompts["P2"].Actionable);
        AssertGenericPassOrdinaryMainPromptQueueAudit(result);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAcceptedGenericPassReplayWithoutMutation()
    {
        var state = PunishmentState(mana: 0);
        var engine = new CoreRuleEngine();
        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p1-generic-pass-before-replay", "P1", CommandTypes.Pass),
            new PassCommand(),
            CancellationToken.None);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        Assert.True(accepted.Accepted);
        Assert.Equal(1, accepted.State.Tick);
        Assert.Equal(["P1"], accepted.State.PassedPriorityPlayerIds);
        AssertGenericPassOrdinaryMainPromptQueueAudit(accepted);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-p1-generic-pass-stale-replay", "P1", CommandTypes.Pass),
            new PassCommand(),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(1, replay.State.Tick);
        Assert.Equal(["P1"], replay.State.PassedPriorityPlayerIds);
        Assert.Equal("P1", replay.State.ActivePlayerId);
        Assert.Equal("P1", replay.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, replay.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, replay.State.TimingState);
        Assert.DoesNotContain(CommandTypes.Pass, replay.Prompts["P1"].EnabledActions());
        Assert.Contains(CommandTypes.EndTurn, replay.Prompts["P1"].EnabledActions());
        Assert.False(replay.Prompts["P2"].Actionable);
        AssertGenericPassOrdinaryMainPromptQueueAudit(replay);
    }

    [Fact]
    public async Task PassDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var state = PunishmentState(mana: 0);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PassCommand();

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, prompt.EnabledActions());
        Assert.DoesNotContain(CommandTypes.Pass, prompt.EnabledActions());
        var rawCommand = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.Pass, snapshotTick = prompt.SnapshotTick });
        const string clientIntentId = "intent-p1-pass-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["TURN_ENDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, accepted.State.Tick);
        Assert.Contains(CommandTypes.EndTurn, accepted.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Pass, accepted.Prompts["P1"].EnabledActions());
        Assert.False(accepted.Prompts["P2"].Actionable);
        AssertGenericPassOrdinaryMainPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.Pass, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.Pass, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("promptId", out _));
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
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
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["TURN_ENDED"], replay.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Contains(CommandTypes.EndTurn, replay.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Pass, replay.Prompts["P1"].EnabledActions());
        Assert.False(replay.Prompts["P2"].Actionable);
        AssertGenericPassOrdinaryMainPromptQueueAudit(replay);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.Pass,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
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
        Assert.Equal("该客户端行动编号已用于其他命令。", conflict.ErrorMessage);
        Assert.DoesNotContain(clientIntentId, conflict.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(accepted.State.RngCursor, conflict.State.RngCursor);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Contains(CommandTypes.EndTurn, conflict.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Pass, conflict.Prompts["P1"].EnabledActions());
        Assert.False(conflict.Prompts["P2"].Actionable);
        AssertGenericPassOrdinaryMainPromptQueueAudit(conflict);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertGenericPassOrdinaryMainPromptQueueAudit(ResolutionResult result)
    {
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Equal(["P1"], result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal("P1", snapshot.ActivePlayerId);
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.MainAction, result.Prompts["P1"].View?.Type);
        Assert.Contains(CommandTypes.EndTurn, result.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.Pass, result.Prompts["P1"].EnabledActions());
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.Pass, result.Prompts["P2"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.EndTurn, result.Prompts["P2"].EnabledActions());
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEndTurnFromNonActivePlayer()
    {
        var state = PunishmentState(mana: 0);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-end-turn-out-of-turn", "P2", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("结束回合只能由当前玩家在开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("END_TURN", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEndTurnDuringClosedPriorityWindow()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-END-TURN-GUARD",
                    "P1",
                    "P1-SPELL-PUNISHMENT",
                    "UNKNOWN_NOOP_EFFECT",
                    "UNL-007/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-end-turn-closed-window", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("结束回合只能由当前玩家在开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("END_TURN", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("STACK-END-TURN-GUARD", stackItem.StackItemId);
    }

    [Fact]
    public async Task CoreRuleEngineAppliesTurnEndSpecialCleanup()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-end-turn-special-cleanup.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "CLEANUP_REPEATED", StringComparison.Ordinal));
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "UNTIL_END_OF_TURN_EXPIRED", StringComparison.Ordinal));
        Assert.Equal(4, result.FinalState.TurnNumber);
        Assert.Equal("P2", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-DAMAGED"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P1-UNIT-DAMAGED"].UntilEndOfTurnEffects);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-BUFFED"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P1-UNIT-BUFFED"].UntilEndOfTurnEffects);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineDoesNotDestroyUndamagedZeroPowerUnitAfterTurnEndPowerExpires()
    {
        var state = new MatchState(
            "p7-turn-end-zero-power-cleanup-room",
            0,
            7,
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
                    Base = ["P1-TURN-END-ZERO-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TURN-END-ZERO-UNIT"] = new(
                    "P1-TURN-END-ZERO-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    untilEndOfTurnPowerModifier: 1),
                ["P2-MAIN-001"] = new(
                    "P2-MAIN-001",
                    cardNo: "SFD·125/221",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUNE-001"] = new(
                    "P2-RUNE-001",
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUNE-002"] = new(
                    "P2-RUNE-002",
                    cardNo: "UNL-R02",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-TURN-END-ZERO-UNIT"] = new("P1", "BASE"),
                ["P2-MAIN-001"] = new("P2", "MAIN_DECK"),
                ["P2-RUNE-001"] = new("P2", "RUNE_DECK"),
                ["P2-RUNE-002"] = new("P2", "RUNE_DECK")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-end-turn-zero-power-cleanup", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("P2", result.State.TurnPlayerId);
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(["P1-TURN-END-ZERO-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("BASE", result.State.ObjectLocations["P1-TURN-END-ZERO-UNIT"].Zone);
        Assert.Equal(0, result.State.CardObjects["P1-TURN-END-ZERO-UNIT"].Power);
        Assert.Equal(0, result.State.CardObjects["P1-TURN-END-ZERO-UNIT"].Damage);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIER_EXPIRED", StringComparison.Ordinal));
        var powerModifierExpiredIndex = EventIndex("POWER_MODIFIER_EXPIRED");
        var turnPlayerAdvancedIndex = EventIndex("TURN_PLAYER_ADVANCED");
        var turnStartBeganIndex = EventIndex("TURN_START_BEGAN");
        Assert.True(powerModifierExpiredIndex < turnPlayerAdvancedIndex);
        Assert.True(turnPlayerAdvancedIndex < turnStartBeganIndex);

        int EventIndex(string kind)
        {
            return result.Events
                .Select((gameEvent, index) => (gameEvent, index))
                .First(item => string.Equals(item.gameEvent.Kind, kind, StringComparison.Ordinal))
                .index;
        }
    }

    [Fact]
    public async Task CoreRuleEngineRepeatsCleanupUntilStable()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-cleanup-repeats-until-stable.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.EventKinds, eventKind => string.Equals(eventKind, "UNTIL_END_OF_TURN_EXPIRED", StringComparison.Ordinal));
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "CLEANUP_REPEATED", StringComparison.Ordinal));
        Assert.Equal(6, result.FinalState.TurnNumber);
        Assert.Equal("P2", result.FinalState.TurnPlayerId);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-DAMAGED"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P1-UNIT-DAMAGED"].UntilEndOfTurnEffects);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task P7PostStackCleanupDestroysPreExistingLethalFieldUnit()
    {
        var state = new MatchState(
            "p7-post-stack-cleanup-room",
            1,
            1,
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
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-LETHAL-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LETHAL-UNIT"] = new(
                    "P1-LETHAL-UNIT",
                    damage: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new StackItemState(
                    "STACK-NOOP-CLEANUP",
                    "P1",
                    "P1-SPELL-NOOP",
                    "UNKNOWN_NOOP_EFFECT")
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-LETHAL-UNIT"] = new("P1", "BASE")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-cleanup", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain("P1-LETHAL-UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-LETHAL-UNIT"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P1-LETHAL-UNIT"].Zone);
        Assert.Equal(["P1"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P7PostStackCleanupDoesNotDestroyUndamagedZeroPowerFieldUnit()
    {
        var state = new MatchState(
            "p7-post-stack-zero-power-cleanup-room",
            1,
            1,
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
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-ZERO-POWER-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-ZERO-POWER-UNIT"] = new(
                    "P1-ZERO-POWER-UNIT",
                    power: 0,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new StackItemState(
                    "STACK-NOOP-ZERO-POWER-CLEANUP",
                    "P1",
                    "P1-SPELL-NOOP",
                    "UNKNOWN_NOOP_EFFECT")
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-ZERO-POWER-UNIT"] = new("P1", "BASE")
            });

        Assert.Empty(state.PendingCleanupTasks);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-zero-power-cleanup", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P1-ZERO-POWER-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("BASE", result.State.ObjectLocations["P1-ZERO-POWER-UNIT"].Zone);
        Assert.Equal(0, result.State.CardObjects["P1-ZERO-POWER-UNIT"].Power);
        Assert.Empty(result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P7PostStackCleanupDoesNotDestroyUndamagedUnitReducedToZeroPower()
    {
        var state = new MatchState(
            "p7-post-stack-zero-power-modifier-cleanup-room",
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
            timingState: TimingStates.NeutralClosed,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ZERO-MODIFIER-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-ZERO-MODIFIER-UNIT"] = new(
                    "P2-ZERO-MODIFIER-UNIT",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new StackItemState(
                    "STACK-PERFECT-FINALE-ZERO-POWER",
                    "P1",
                    "P1-SPELL-PERFECT-FINALE",
                    "PERFECT_FINALE_BATTLEFIELD_POWER_MINUS_4",
                    "UNL-182/219",
                    ["P2-ZERO-MODIFIER-UNIT"])
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P2-ZERO-MODIFIER-UNIT"] = new("P2", "BATTLEFIELD", "BF-ZERO-POWER")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-zero-power-modifier-cleanup", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P2-ZERO-MODIFIER-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P2-ZERO-MODIFIER-UNIT"].Zone);
        Assert.Equal("BF-ZERO-POWER", result.State.ObjectLocations["P2-ZERO-MODIFIER-UNIT"].BattlefieldObjectId);
        Assert.Empty(result.State.DestroyedUnitOwnerIdsThisTurn);
        var modifiedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-ZERO-MODIFIER-UNIT", StringComparison.Ordinal));
        Assert.Equal(0, Assert.IsType<int>(modifiedEvent.Payload["resultingPower"]));
        Assert.Equal(0, result.State.CardObjects["P2-ZERO-MODIFIER-UNIT"].Power);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P7PostStackCleanupDoesNotDestroyUndamagedNegativePowerFieldUnit()
    {
        var state = PostStackCleanupPowerState(
            "P1-NEGATIVE-POWER-UNIT",
            power: -2,
            damage: 0);

        Assert.Empty(state.PendingCleanupTasks);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-negative-power-cleanup", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P1-NEGATIVE-POWER-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(-2, result.State.CardObjects["P1-NEGATIVE-POWER-UNIT"].Power);
        Assert.Equal(0, result.State.CardObjects["P1-NEGATIVE-POWER-UNIT"].Damage);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-ZERO-DAMAGED-UNIT", 0)]
    [InlineData("P1-NEGATIVE-DAMAGED-UNIT", -2)]
    public async Task P7PostStackCleanupDestroysZeroOrNegativePowerUnitWithDamage(
        string objectId,
        int power)
    {
        var state = PostStackCleanupPowerState(
            objectId,
            power,
            damage: 1);

        var cleanupTask = Assert.Single(state.PendingCleanupTasks);
        Assert.Equal("DESTROY_LETHAL_UNIT", cleanupTask.Kind);
        Assert.Equal("LETHAL_DAMAGE", cleanupTask.Reason);
        Assert.Equal(objectId, cleanupTask.ObjectId);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-p2-pass-{objectId}", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(objectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal([objectId], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations[objectId].Zone);
        Assert.Equal(["P1"], result.State.DestroyedUnitOwnerIdsThisTurn);
        var destroyedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, objectId, StringComparison.Ordinal));
        Assert.Equal("LETHAL_DAMAGE", destroyedEvent.Payload["reason"]);
    }

    [Fact]
    public async Task P4DeclareBattleNegativePowerAttackerDealsNoCombatDamageAndRetainsTruePower()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-NEGATIVE-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ZERO-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-NEGATIVE-ATTACKER"] = new(
                    "P1-NEGATIVE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: -2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-ZERO-DEFENDER"] = new(
                    "P2-ZERO-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 0,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-negative-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-NEGATIVE-ATTACKER"],
                ["P2-ZERO-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-NEGATIVE-ATTACKER", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Equal(["P1-NEGATIVE-ATTACKER"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(-2, result.State.CardObjects["P1-NEGATIVE-ATTACKER"].Power);
        Assert.Equal(0, result.State.CardObjects["P1-NEGATIVE-ATTACKER"].Damage);
        Assert.Equal(["P2-ZERO-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.State.CardObjects["P2-ZERO-DEFENDER"].Power);
    }

    [Fact]
    public async Task P7PostStackCleanupRemovesIllegalStandbyFromBattlefield()
    {
        var state = new MatchState(
            "p7-post-stack-illegal-standby-cleanup-room",
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
            timingState: TimingStates.NeutralClosed,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-ILLEGAL-STANDBY", "P1-ILLEGAL-STANDBY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-ILLEGAL-STANDBY"] = new(
                    "BF-ILLEGAL-STANDBY",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2"),
                ["P1-ILLEGAL-STANDBY"] = new(
                    "P1-ILLEGAL-STANDBY",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: "P1")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new StackItemState(
                    "STACK-NOOP-ILLEGAL-STANDBY-CLEANUP",
                    "P1",
                    "P1-SPELL-NOOP",
                    "UNKNOWN_NOOP_EFFECT")
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-ILLEGAL-STANDBY"] = new("P1", "BATTLEFIELD", "BF-ILLEGAL-STANDBY"),
                ["P1-ILLEGAL-STANDBY"] = new("P1", "BATTLEFIELD", "BF-ILLEGAL-STANDBY")
            });

        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "P1-ILLEGAL-STANDBY", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-illegal-standby-cleanup", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain("P1-ILLEGAL-STANDBY", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-ILLEGAL-STANDBY"], result.State.PlayerZones["P1"].Graveyard);
        Assert.False(result.State.CardObjects["P1-ILLEGAL-STANDBY"].IsFaceDown);
        Assert.Equal("P1", result.State.CardObjects["P1-ILLEGAL-STANDBY"].ControllerId);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P1-ILLEGAL-STANDBY"].Zone);
        Assert.DoesNotContain(
            result.State.PendingCleanupTasks,
            task => string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal));
        var standbyRemovedEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_STANDBY_REMOVED", StringComparison.Ordinal));
        Assert.Equal("BF-ILLEGAL-STANDBY", standbyRemovedEvent.Payload["battlefieldObjectId"]);
        Assert.Equal(string.Empty, standbyRemovedEvent.Payload["controllerId"]);
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", standbyRemovedEvent.Payload["reason"]);
        Assert.Equal(["P1-ILLEGAL-STANDBY"], Assert.IsType<object[]>(standbyRemovedEvent.Payload["removedObjectIds"]));
        var removedCards = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(standbyRemovedEvent.Payload["removedCards"]);
        var removedCard = Assert.Single(removedCards);
        Assert.Equal("P1", removedCard["previousControllerId"]);
    }

    [Fact]
    public async Task P7PostStackCleanupRecallsUnattachedBattlefieldEquipmentToControllerBase()
    {
        var state = new MatchState(
            "p7-post-stack-unattached-equipment-cleanup-room",
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
            timingState: TimingStates.NeutralClosed,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-UNATTACHED-EQUIPMENT", "P1-UNATTACHED-EQUIPMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-UNATTACHED-EQUIPMENT"] = new(
                    "BF-UNATTACHED-EQUIPMENT",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNATTACHED-EQUIPMENT"] = new(
                    "P1-UNATTACHED-EQUIPMENT",
                    cardNo: "SFD·135/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P2")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new StackItemState(
                    "STACK-NOOP-UNATTACHED-EQUIPMENT-CLEANUP",
                    "P1",
                    "P1-SPELL-NOOP",
                    "UNKNOWN_NOOP_EFFECT")
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-UNATTACHED-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-UNATTACHED-EQUIPMENT"),
                ["P1-UNATTACHED-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-UNATTACHED-EQUIPMENT")
            });

        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "P1-UNATTACHED-EQUIPMENT", StringComparison.Ordinal)
                && string.Equals(task.PlayerId, "P2", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-unattached-equipment-cleanup", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain("P1-UNATTACHED-EQUIPMENT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-UNATTACHED-EQUIPMENT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal("P1", result.State.CardObjects["P1-UNATTACHED-EQUIPMENT"].OwnerId);
        Assert.Equal("P2", result.State.CardObjects["P1-UNATTACHED-EQUIPMENT"].ControllerId);
        Assert.Equal("BASE", result.State.ObjectLocations["P1-UNATTACHED-EQUIPMENT"].Zone);
        Assert.Equal("P2", result.State.ObjectLocations["P1-UNATTACHED-EQUIPMENT"].PlayerId);
        Assert.Null(result.State.ObjectLocations["P1-UNATTACHED-EQUIPMENT"].BattlefieldObjectId);
        Assert.DoesNotContain(
            result.State.PendingCleanupTasks,
            task => string.Equals(task.Kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal));
        var recalledEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_RECALLED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal("P1-UNATTACHED-EQUIPMENT", recalledEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1", recalledEvent.Payload["ownerPlayerId"]);
        Assert.Equal("P2", recalledEvent.Payload["controllerId"]);
        Assert.Equal("BF-UNATTACHED-EQUIPMENT", recalledEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", recalledEvent.Payload["reason"]);
    }

    [Fact]
    public async Task P7BattleCleanupReconcilesAuthoritativeObjectLocations()
    {
        var state = new MatchState(
            "p7-battle-cleanup-location-room",
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
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLE-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-ATTACKER"] = new(
                    "P1-BATTLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLE-DEFENDER"] = new(
                    "P2-BATTLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD"),
                ["P2-BATTLE-DEFENDER"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-battle-location", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLE-ATTACKER"],
                ["P2-BATTLE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain("P2-BATTLE-DEFENDER", result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLE-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P2-BATTLE-DEFENDER"].Zone);
        Assert.Null(result.State.ObjectLocations["P2-BATTLE-DEFENDER"].BattlefieldObjectId);
    }
}
