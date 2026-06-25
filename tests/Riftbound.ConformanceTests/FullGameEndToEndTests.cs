using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FullGameEndToEndTests
{
    private const string JhinLegendCardNo = "UNL-181/219";
    private const string JhinChampionCardNo = "UNL-022/219";
    private const string RumbleLegendCardNo = "SFD·181/221";
    private const string RumbleChampionCardNo = "SFD·026/221";
    private const string PoppyLegendCardNo = "UNL-203/219";
    private const string PoppyChampionCardNo = "UNL-116/219";
    private const string LilliaLegendCardNo = "UNL-189/219";
    private const string LilliaChampionCardNo = "UNL-082/219";
    private const string MutantKittenCardNo = "UNL-036/219";
    private const string LeblancCardNo = "UNL-090/219";
    private const string VexLegendCardNo = "UNL-232/219";
    private const string VexChampionCardNo = "UNL-055/219";
    private const string ShadowCardNo = "UNL-194/219";
    private const long LowCurveReplaySeed = 424242;

    [Fact]
    public async Task OfficialLowCurveDecksSkipNoLegalBattleAndReachMatchResultThroughServerPrompts()
    {
        var (session, result) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            "b0-full-game-official-low-curve-room");

        var winner = OpponentOf(result.State, result.State.ActivePlayerId);
        var surrender = await session.SubmitAsync(
            result.State.ActivePlayerId,
            "b0-surrender-after-battle",
            new SurrenderCommand(),
            RawCommand(CommandTypes.Surrender),
            CancellationToken.None);
        AssertAccepted(surrender);
        Assert.Equal(MatchStatuses.Finished, surrender.State.Status);
        Assert.Equal(winner, surrender.State.WinnerPlayerId);
        Assert.Contains(surrender.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(surrender);
    }

    [Fact]
    public async Task OfficialLowCurveDecksReopenContestedBattleAfterSkippedCombatantsReadyAcrossTurns()
    {
        var (_, battleReady, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-official-low-curve-reopen-room");

        AssertNoHiddenZoneLeak(battleResult);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(battleResult.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
            && string.Equals(task.BattlefieldObjectId, battleReady.State.PendingTaskQueue.Tasks.Single(activeTask =>
                string.Equals(activeTask.TaskId, battleReady.State.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal)).BattlefieldObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts()
    {
        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-official-low-curve-score-room");

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-score");

        AssertScoreVictory(result);
    }

    [Fact]
    public async Task DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-distinct-low-curve-score-room",
            p1Deck,
            p2Deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-distinct-score");

        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var (_, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-official-low-curve-score-room");
        var initialState = battleResult.State;
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-replay-score");

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            result.State,
            new CoreRuleEngine(),
            CancellationToken.None,
            ToRecoveredEvents(journal.Entries));

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(result.State), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildLowCurveOfficialDeck(catalog);

        await AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
            "b0-full-game-official-low-curve-replay-room",
            "b0-full-replay-score",
            deck,
            deck);
    }

    [Fact]
    public async Task DistinctOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        await AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
            "b0-full-game-distinct-low-curve-replay-room",
            "b0-full-distinct-replay-score",
            p1Deck,
            p2Deck);
    }

    [Fact]
    public async Task StandbyHeavyOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, PoppyLegendCardNo, PoppyChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        await AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
            "b0-full-game-standby-heavy-low-curve-replay-room",
            "b0-full-standby-heavy-replay-score",
            p1Deck,
            p2Deck);
    }

    [Fact]
    public async Task StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, PoppyLegendCardNo, PoppyChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-standby-heavy-low-curve-score-room",
            p1Deck,
            p2Deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-standby-heavy-score");

        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDecksResolveMultiDefenderBattleDamageAssignmentThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildDamageAssignmentOfficialDeck(catalog);

        var (_, assignmentOpened, battleResult) = await DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
            "b0-full-game-damage-assignment-room",
            deck,
            deck);

        Assert.Contains(assignmentOpened.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.AssignCombatDamage, assignmentOpened.Prompts[assignmentOpened.State.ActivePlayerId].View?.Type);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.False(battleResult.State.BattleState.IsActive);
        AssertNoHiddenZoneLeak(assignmentOpened);
        AssertNoHiddenZoneLeak(battleResult);
    }

    [Fact]
    public async Task OfficialDecksResolveShadowBattleResponseActivationThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildShadowResponseOfficialDeck(catalog);

        var (_, openedResponse, activated, stackResolved, battleResult, targetObjectId) =
            await DriveOfficialDecksToShadowResponseBattleCloseAsync(
                "b0-full-game-shadow-response-room",
                deck,
                deck);

        Assert.Contains(openedResponse.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.StackPriority, openedResponse.Prompts[openedResponse.State.PriorityPlayerId!].View?.Type);
        Assert.Contains(CommandTypes.ActivateAbility, openedResponse.Prompts[openedResponse.State.PriorityPlayerId!].Actions);

        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Single(activated.State.StackItems);

        Assert.Contains(stackResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains("STUNNED", stackResolved.State.CardObjects[targetObjectId].UntilEndOfTurnEffects);

        Assert.Contains(battleResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.False(battleResult.State.BattleState.IsActive);
        AssertNoHiddenZoneLeak(openedResponse);
        AssertNoHiddenZoneLeak(activated);
        AssertNoHiddenZoneLeak(stackResolved);
        AssertNoHiddenZoneLeak(battleResult);
    }

    private static async ValueTask<ResolutionResult> DriveBattleCloseToScoreVictoryAsync(
        MatchSession session,
        ResolutionResult battleResult,
        string intentPrefix)
    {
        var result = battleResult;
        var scoreEvents = result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        for (var turnIndex = 0; turnIndex < 24 && !string.Equals(result.State.Status, MatchStatuses.Finished, StringComparison.Ordinal); turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                scoreEvents += result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException(JsonSerializer.Serialize(new
                {
                    MatchStatus = result.State.Status,
                    MatchPhase = result.State.Phase,
                    result.State.TimingState,
                    result.State.ActivePlayerId,
                    result.State.TurnPlayerId,
                    result.State.FocusPlayerId,
                    PendingTaskPhase = result.State.PendingTaskQueue.Phase,
                    result.State.PendingTaskQueue.ActiveTaskId,
                    TaskKinds = result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray(),
                    PromptActions = result.Prompts[result.State.ActivePlayerId].Actions
                }));
            }

            Assert.Equal(result.State.TurnPlayerId, result.State.ActivePlayerId);
            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-turn-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
            scoreEvents += result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        }

        Assert.True(scoreEvents > 0, "Expected the prompt-driven game to gain battlefield score before match win.");
        return result;
    }

    private static async ValueTask AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
        string roomId,
        string scoreIntentPrefix,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var initialState = BuildSeatedInitialState(roomId, LowCurveReplaySeed);
        var journal = new RecordingMatchJournal();
        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            initialState,
            journal,
            p1Deck,
            p2Deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            scoreIntentPrefix);

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            result.State,
            new CoreRuleEngine(),
            CancellationToken.None,
            ToRecoveredEvents(journal.Entries));

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(result.State), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.Ready, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.Mulligan, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    private static void AssertScoreVictory(ResolutionResult result)
    {
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.State.WinnerPlayerId));
        var winEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        var winningScore = Assert.IsType<int>(winEvent.Payload["winningScore"]);
        Assert.True(
            result.State.PlayerScores[result.State.WinnerPlayerId!] >= winningScore,
            $"Expected winner score to satisfy winningScore={winningScore}; scores={JsonSerializer.Serialize(result.State.PlayerScores)}.");
        AssertNoHiddenZoneLeak(result);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveOfficialLowCurveDecksToBattleCloseAsync(
        string roomId)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildLowCurveOfficialDeck(catalog);
        return await DriveOfficialLowCurveDecksToBattleCloseAsync(roomId, deck, deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveOfficialLowCurveDecksToBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var (session, skipped) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(roomId, p1Deck, p2Deck);
        return await DriveSkippedOfficialLowCurveDecksToBattleCloseAsync(session, skipped);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveOfficialLowCurveDecksToBattleCloseAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var (session, skipped) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            initialState,
            journal,
            p1Deck,
            p2Deck);
        return await DriveSkippedOfficialLowCurveDecksToBattleCloseAsync(session, skipped);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveSkippedOfficialLowCurveDecksToBattleCloseAsync(
        MatchSession session,
        ResolutionResult skipped)
    {
        var current = skipped;
        ResolutionResult? battleReady = null;
        var skippedBattleCount = 0;
        for (var turnIndex = 0; turnIndex < 4; turnIndex++)
        {
            var turnStart = await EndTurnAsync(
                session,
                current.State.ActivePlayerId,
                $"b0-end-after-no-legal-battle-skip-{turnIndex}");
            AssertNoHiddenZoneLeak(turnStart);
            Assert.DoesNotContain(turnStart.State.UntilEndOfTurnEffects, effectId =>
                effectId.StartsWith(BattlefieldTaskMarkers.BattleSkippedPrefix, StringComparison.Ordinal));
            Assert.Equal(TimingStates.SpellDuelOpen, turnStart.State.TimingState);
            Assert.NotNull(turnStart.State.FocusPlayerId);
            Assert.Equal("SPELL_DUEL_TASKS", turnStart.State.PendingTaskQueue.Phase);
            Assert.Contains(turnStart.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));

            current = await PassOpenSpellDuelAsync(session, turnStart, $"b0-reopen-pass-focus-{turnIndex}");
            AssertNoHiddenZoneLeak(current);
            if (string.Equals(current.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && current.Prompts[current.State.ActivePlayerId].Actions.Contains("DECLARE_BATTLE", StringComparer.Ordinal))
            {
                battleReady = current;
                break;
            }

            skippedBattleCount++;
            Assert.Contains(current.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));
            Assert.Equal("IDLE", current.State.PendingTaskQueue.Phase);
            Assert.DoesNotContain("DECLARE_BATTLE", current.Prompts["P1"].Actions);
            Assert.DoesNotContain("DECLARE_BATTLE", current.Prompts["P2"].Actions);
        }

        Assert.True(skippedBattleCount > 0);
        Assert.NotNull(battleReady);
        Assert.Equal("BATTLE_TASKS", battleReady.State.PendingTaskQueue.Phase);
        Assert.Equal("START_BATTLE", battleReady.State.PendingTaskQueue.Tasks.Single(task =>
            string.Equals(task.TaskId, battleReady.State.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal)).Kind);
        Assert.Equal(PromptTypes.BattleDeclaration, battleReady.Prompts[battleReady.State.ActivePlayerId].View?.Type);
        Assert.Contains("DECLARE_BATTLE", battleReady.Prompts[battleReady.State.ActivePlayerId].Actions);

        var battleResult = await SubmitFirstDeclareBattleCandidateAsync(
            session,
            battleReady,
            "b0-declare-reopened-official-battle");
        return (session, battleReady, battleResult);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult AssignmentOpened, ResolutionResult BattleResult)> DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var initialState = MatchState.Create(roomId) with { Seed = 424242 };
        var session = new MatchSession(initialState, new CoreRuleEngine(), NoopMatchJournal.Instance);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-damage-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-damage-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-damage-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-damage-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-damage-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var current = await session.SubmitAsync(
            secondPlayerId,
            "b0-damage-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(current);
        AssertNoHiddenZoneLeak(current);
        Assert.Equal(MatchPhases.Main, current.State.Phase);

        var battlefieldOwnerId = current.State.ActivePlayerId;
        current = await TapAllAvailableRunesAsync(session, battlefieldOwnerId, current, "b0-damage-owner-tap");
        current = await TryPlayFirstUnitAsync(session, battlefieldOwnerId, current, "b0-damage-owner-play-attacker", playUnitToBattlefield: true);

        var invadingPlayerId = OpponentOf(current.State, battlefieldOwnerId);
        current = await EndTurnAsync(session, battlefieldOwnerId, "b0-damage-end-owner-setup");
        AssertNoHiddenZoneLeak(current);

        current = await DriveTwoAssignmentDefendersOntoBattlefieldAsync(
            session,
            current,
            invadingPlayerId,
            battlefieldOwnerId);

        var assignmentOpened = await DriveContestedBattlefieldToDamageAssignmentAsync(
            session,
            current,
            battlefieldOwnerId,
            invadingPlayerId);
        var battleResult = await ResolveOpenBattleDamageAssignmentsAsync(
            session,
            assignmentOpened,
            "b0-damage-assignment");
        return (session, assignmentOpened, battleResult);
    }

    private static async ValueTask<(
        MatchSession Session,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult StackResolved,
        ResolutionResult BattleResult,
        string TargetObjectId)> DriveOfficialDecksToShadowResponseBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in new[] { 7, 11, 17, 23, 31, 42, 101, 404, 20260624, 424242 })
        {
            try
            {
                return await DriveOfficialDecksToShadowResponseBattleCloseAsync(
                    $"{roomId}-{seed}",
                    p1Deck,
                    p2Deck,
                    seed);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 shadow-response driver", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: {ex.Message}");
            }
            catch (MatchSessionException ex) when (ex.Message.Contains("对局已经结束", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: match ended before Shadow response path");
            }
        }

        throw new InvalidOperationException(
            "B0 shadow-response driver could not find a deterministic official-deck Shadow response path. "
            + string.Join(" | ", failures));
    }

    private static async ValueTask<(
        MatchSession Session,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult StackResolved,
        ResolutionResult BattleResult,
        string TargetObjectId)> DriveOfficialDecksToShadowResponseBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck,
        int seed)
    {
        var initialState = MatchState.Create(roomId) with { Seed = seed };
        var session = new MatchSession(initialState, new CoreRuleEngine(), NoopMatchJournal.Instance);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-shadow-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-shadow-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-shadow-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-shadow-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-shadow-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var current = await session.SubmitAsync(
            secondPlayerId,
            "b0-shadow-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(current);
        AssertNoHiddenZoneLeak(current);
        Assert.Equal(MatchPhases.Main, current.State.Phase);

        var battlefieldOwnerId = current.State.ActivePlayerId;
        current = await TapAllAvailableRunesAsync(session, battlefieldOwnerId, current, "b0-shadow-owner-tap");
        current = await TryPlayFirstUnitAsync(session, battlefieldOwnerId, current, "b0-shadow-owner-play-attacker", playUnitToBattlefield: true);

        var shadowControllerId = OpponentOf(current.State, battlefieldOwnerId);
        current = await EndTurnAsync(session, battlefieldOwnerId, "b0-shadow-end-owner-setup");
        AssertNoHiddenZoneLeak(current);

        current = await DriveShadowOntoBattlefieldAsync(
            session,
            current,
            shadowControllerId,
            battlefieldOwnerId);

        var openedResponse = await DriveContestedBattlefieldToShadowResponseAsync(
            session,
            current,
            battlefieldOwnerId,
            shadowControllerId);
        var (activated, targetObjectId) = await ActivateCurrentShadowResponseAsync(
            session,
            openedResponse,
            openedResponse.State.PriorityPlayerId!,
            "b0-shadow-activate-response");
        var stackResolved = await ResolveCurrentStackOnlyAsync(session, activated, "b0-shadow-resolve-stack");
        var battleResult = await PassOpenBattleResponseAsync(session, stackResolved, "b0-shadow-pass-returned-response");
        return (session, openedResponse, activated, stackResolved, battleResult, targetObjectId);
    }

    private static async ValueTask<ResolutionResult> DriveShadowOntoBattlefieldAsync(
        MatchSession session,
        ResolutionResult current,
        string shadowControllerId,
        string battlefieldOwnerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 40; turnIndex++)
        {
            if (!string.Equals(result.State.ActivePlayerId, shadowControllerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-shadow-wait-for-controller-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, shadowControllerId, result, $"b0-shadow-controller-tap-{turnIndex}");
            var pool = result.State.RunePools[shadowControllerId];
            if (!PlayerHandContainsCardNo(result.State, shadowControllerId, ShadowCardNo)
                || pool.Mana < 4)
            {
                result = await EndTurnAsync(session, shadowControllerId, $"b0-shadow-wait-for-card-resources-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            var battlefieldDestination = BattlefieldDestinationFor(result.State, battlefieldOwnerId);
            result = await PlaySpecificUnitToBattlefieldAsync(
                session,
                shadowControllerId,
                result,
                ShadowCardNo,
                battlefieldDestination,
                "b0-shadow-play-shadow-to-battlefield");
            result = await PassOpenSpellDuelAsync(session, result, "b0-shadow-pass-shadow-contest");
            return result;
        }

        throw new InvalidOperationException("B0 shadow-response driver could not stage Shadow with response resources.");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToShadowResponseAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string shadowControllerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 12; turnIndex++)
        {
            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                var declared = await SubmitShadowResponseDeclareBattleAsync(
                    session,
                    result,
                    battlefieldOwnerId,
                    shadowControllerId,
                    $"b0-shadow-declare-response-battle-{turnIndex}");
                AssertAccepted(declared);
                return declared;
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-shadow-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"b0-shadow-reopen-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                var declared = await SubmitShadowResponseDeclareBattleAsync(
                    session,
                    result,
                    battlefieldOwnerId,
                    shadowControllerId,
                    "b0-shadow-declare-response-battle");
                AssertAccepted(declared);
                return declared;
            }
        }

        throw new InvalidOperationException("B0 shadow-response driver could not open a Shadow response battle task.");
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        string roomId)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildLowCurveOfficialDeck(catalog);
        return await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(roomId, deck, deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var session = new MatchSession(roomId, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        return await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(session, p1Deck, p2Deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        return await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(session, p1Deck, p2Deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        MatchSession session,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var secondMulligan = await session.SubmitAsync(
            secondPlayerId,
            "b0-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(secondMulligan);
        AssertNoHiddenZoneLeak(secondMulligan);
        Assert.Equal(MatchPhases.Main, secondMulligan.State.Phase);

        var result = secondMulligan;
        result = await PreparePlayerBoardAsync(session, result.State.ActivePlayerId, result, "first", playUnitToBattlefield: true);
        var nextPlayerId = OpponentOf(result.State, result.State.ActivePlayerId);
        result = await EndTurnAsync(session, result.State.ActivePlayerId, "b0-end-first-player");
        AssertNoHiddenZoneLeak(result);

        Assert.Equal(nextPlayerId, result.State.ActivePlayerId);
        result = await PreparePlayerBoardAsync(session, result.State.ActivePlayerId, result, "second", playUnitToBattlefield: false);
        result = await MoveBaseUnitToOpponentBattlefieldAsync(session, result.State.ActivePlayerId, result);
        result = await PassOpenSpellDuelAsync(session, result, "b0-initial-pass-focus");
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.DoesNotContain("DECLARE_BATTLE", result.Prompts["P1"].Actions);
        Assert.DoesNotContain("DECLARE_BATTLE", result.Prompts["P2"].Actions);
        Assert.True(result.Prompts[result.State.ActivePlayerId].Actionable);
        Assert.DoesNotContain("WAIT", result.Prompts[result.State.ActivePlayerId].Actions);
        AssertNoHiddenZoneLeak(result);
        return (session, result);
    }

    private static async ValueTask<ResolutionResult> DriveTwoAssignmentDefendersOntoBattlefieldAsync(
        MatchSession session,
        ResolutionResult current,
        string invadingPlayerId,
        string battlefieldOwnerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 18; turnIndex++)
        {
            if (!string.Equals(result.State.ActivePlayerId, invadingPlayerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-damage-wait-for-invader-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, invadingPlayerId, result, $"b0-damage-invader-tap-{turnIndex}");
            if (!PlayerHandContainsCardNo(result.State, invadingPlayerId, MutantKittenCardNo)
                || !PlayerHandContainsCardNo(result.State, invadingPlayerId, LeblancCardNo)
                || result.State.RunePools[invadingPlayerId].Mana < 6)
            {
                result = await EndTurnAsync(session, invadingPlayerId, $"b0-damage-wait-for-defenders-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            var battlefieldDestination = BattlefieldDestinationFor(result.State, battlefieldOwnerId);
            result = await PlaySpecificUnitToBaseAndMoveToBattlefieldAsync(
                session,
                invadingPlayerId,
                result,
                MutantKittenCardNo,
                battlefieldDestination,
                "b0-damage-play-move-kitten");
            result = await PassOpenSpellDuelAsync(session, result, "b0-damage-pass-kitten-contest");
            Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));
            Assert.Equal(invadingPlayerId, result.State.ActivePlayerId);

            result = await PlaySpecificUnitToBaseAndMoveToBattlefieldAsync(
                session,
                invadingPlayerId,
                result,
                LeblancCardNo,
                battlefieldDestination,
                "b0-damage-play-move-leblanc");
            result = await PassOpenSpellDuelAsync(session, result, "b0-damage-pass-leblanc-contest");
            Assert.Equal(invadingPlayerId, result.State.ActivePlayerId);
            return result;
        }

        throw new InvalidOperationException("B0 damage-assignment driver could not stage two assignment defenders.");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToDamageAssignmentAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string invadingPlayerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 12; turnIndex++)
        {
            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-damage-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"b0-damage-reopen-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                var declared = await SubmitMultiDefenderDeclareBattleAsync(
                    session,
                    result,
                    battlefieldOwnerId,
                    invadingPlayerId,
                    "b0-damage-declare-multi-defender-battle");
                AssertAccepted(declared);
                return declared;
            }
        }

        throw new InvalidOperationException("B0 damage-assignment driver could not open a multi-defender battle task.");
    }

    private static async ValueTask<ResolutionResult> PreparePlayerBoardAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string label,
        bool playUnitToBattlefield)
    {
        var result = current;
        result = await TapAllAvailableRunesAsync(session, playerId, result, $"b0-{label}-tap");
        result = await TryPlayFirstUnitAsync(session, playerId, result, $"b0-{label}-play-unit", playUnitToBattlefield);
        return result;
    }

    private static async ValueTask<ResolutionResult> PlaySpecificUnitToBaseAndMoveToBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string cardNo,
        string battlefieldDestination,
        string intentPrefix)
    {
        var sourceObjectId = FindHandCardObjectByCardNo(current.State, playerId, cardNo)
            ?? throw new InvalidOperationException($"B0 damage-assignment driver could not find {cardNo} in {playerId}'s hand.");
        var play = await session.SubmitAsync(
            playerId,
            $"{intentPrefix}-play",
            new PlayCardCommand(sourceObjectId, cardNo, [], Destination: "BASE"),
            RawCommand(new PlayCardCommand(sourceObjectId, cardNo, [], Destination: "BASE")),
            CancellationToken.None);
        AssertAccepted(play);
        AssertNoHiddenZoneLeak(play);

        var resolved = await ResolveStackPassPassAsync(session, play, $"{intentPrefix}-resolve");
        var baseObjectId = resolved.State.PlayerZones[playerId].Base
            .FirstOrDefault(objectId => resolved.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal)
                && !cardObject.IsExhausted
                && !cardObject.IsFaceDown)
            ?? throw new InvalidOperationException($"B0 damage-assignment driver could not find ready base {cardNo} for {playerId}.");
        var move = await session.SubmitAsync(
            playerId,
            $"{intentPrefix}-move",
            new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, []),
            RawCommand(new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, [])),
            CancellationToken.None);
        AssertAccepted(move);
        AssertNoHiddenZoneLeak(move);
        return move;
    }

    private static async ValueTask<ResolutionResult> PlaySpecificUnitToBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string cardNo,
        string battlefieldDestination,
        string intentPrefix)
    {
        var sourceObjectId = FindHandCardObjectByCardNo(current.State, playerId, cardNo)
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find {cardNo} in {playerId}'s hand.");
        var play = await session.SubmitAsync(
            playerId,
            $"{intentPrefix}-play",
            new PlayCardCommand(sourceObjectId, cardNo, [], Destination: battlefieldDestination),
            RawCommand(new PlayCardCommand(sourceObjectId, cardNo, [], Destination: battlefieldDestination)),
            CancellationToken.None);
        AssertAccepted(play);
        AssertNoHiddenZoneLeak(play);

        var resolved = await ResolveStackPassPassAsync(session, play, $"{intentPrefix}-resolve");
        var battlefieldObjectId = resolved.State.PlayerZones[playerId].Battlefields
            .FirstOrDefault(objectId => IsObjectLocatedAtBattlefield(resolved.State, objectId, battlefieldDestination)
                && resolved.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find battlefield {cardNo} for {playerId}.");
        Assert.False(resolved.State.CardObjects[battlefieldObjectId].IsExhausted);
        return resolved;
    }

    private static async ValueTask<ResolutionResult> SubmitMultiDefenderDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string invadingPlayerId,
        string intentId)
    {
        Assert.Equal(battlefieldOwnerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 damage-assignment driver could not find DECLARE_BATTLE for {playerId}.");
        var battlefieldId = candidate.Destinations?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 damage-assignment driver could not find battle destination.");
        var attackerObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 damage-assignment driver could not find battle attacker.");
        var defenderObjectIds = current.State.PlayerZones[invadingPlayerId].Battlefields
            .Where(objectId => IsObjectLocatedAtBattlefield(current.State, objectId, battlefieldId))
            .Where(objectId => IsReadyUnit(current.State, objectId))
            .Where(objectId => current.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && (string.Equals(cardObject.CardNo, MutantKittenCardNo, StringComparison.Ordinal)
                    || string.Equals(cardObject.CardNo, LeblancCardNo, StringComparison.Ordinal)))
            .OrderBy(objectId => current.State.CardObjects[objectId].CardNo, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, defenderObjectIds.Length);

        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            defenderObjectIds,
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        return await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds,
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
    }

    private static async ValueTask<ResolutionResult> SubmitShadowResponseDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string shadowControllerId,
        string intentId)
    {
        Assert.Equal(battlefieldOwnerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find DECLARE_BATTLE for {playerId}.");
        var battlefieldId = candidate.Destinations?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 shadow-response driver could not find battle destination.");
        var attackerObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 shadow-response driver could not find battle attacker.");
        var shadowObjectId = current.State.PlayerZones[shadowControllerId].Battlefields
            .FirstOrDefault(objectId => IsObjectLocatedAtBattlefield(current.State, objectId, battlefieldId)
                && IsReadyUnit(current.State, objectId)
                && current.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, ShadowCardNo, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("B0 shadow-response driver could not find ready Shadow defender.");

        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [shadowObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { shadowObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        if (declared.Accepted)
        {
            Assert.Contains(declared.Events, gameEvent =>
                string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
            Assert.Equal(shadowControllerId, declared.State.PriorityPlayerId);
        }

        return declared;
    }

    private static async ValueTask<(ResolutionResult Result, string TargetObjectId)> ActivateCurrentShadowResponseAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentId)
    {
        var prompt = current.Prompts[playerId];
        Assert.Equal(PromptTypes.StackPriority, prompt.View?.Type);
        var candidate = EnabledCandidate(prompt, CommandTypes.ActivateAbility)
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find ACTIVATE_ABILITY for {playerId}.");
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                StringComparison.Ordinal));
        var sourceObjectId = Assert.IsType<string>(requirement["sourceObjectId"]);
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        var targetObjectId = Assert.Single(targetChoicesByIndex["0"]).Id;
        var optionalCosts = ActivateAbilityPaymentResourceChoicesForRequirement(requirement);
        var command = new ActivateAbilityCommand(
            sourceObjectId,
            P4ActivatedAbilityCatalog.ShadowStunAbilityId,
            [targetObjectId],
            optionalCosts);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.ActivateAbility,
                sourceObjectId,
                abilityId = P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                targetObjectIds = new[] { targetObjectId },
                optionalCosts
            }),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return (result, targetObjectId);
    }

    private static IReadOnlyList<string> ActivateAbilityPaymentResourceChoicesForRequirement(
        IReadOnlyDictionary<string, object?> requirement)
    {
        var powerCost = Assert.IsType<int>(requirement["powerCost"]);
        var availablePower = Assert.IsType<int>(requirement["availablePower"]);
        if (availablePower >= powerCost)
        {
            return [];
        }

        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]);
        var choice = paymentResourceChoices.FirstOrDefault()
            ?? throw new InvalidOperationException("B0 shadow-response driver expected a payment resource choice for Shadow power cost.");
        return [choice.Id];
    }

    private static async ValueTask<ResolutionResult> PassOpenBattleResponseAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 8; index++)
        {
            if (!result.State.BattleState.IsActive || string.IsNullOrWhiteSpace(result.State.PriorityPlayerId))
            {
                return result;
            }

            if (result.State.StackItems.Count > 0)
            {
                result = await ResolveStackPassPassAsync(session, result, $"{intentPrefix}-stack-{index}");
                continue;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 shadow-response driver exceeded battle response pass guard.");
    }

    private static async ValueTask<ResolutionResult> ResolveOpenBattleDamageAssignmentsAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 4; index++)
        {
            var assigningPlayerId = PlayerWithEnabledCandidate(result, CommandTypes.AssignCombatDamage);
            if (assigningPlayerId is null)
            {
                return result;
            }

            result = await SubmitCurrentBattleDamageAssignmentAsync(
                session,
                result,
                assigningPlayerId,
                $"{intentPrefix}-{index}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 damage-assignment driver exceeded ASSIGN_COMBAT_DAMAGE guard.");
    }

    private static async ValueTask<ResolutionResult> SubmitCurrentBattleDamageAssignmentAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentId)
    {
        var prompt = current.Prompts[playerId];
        Assert.Equal(PromptTypes.AssignCombatDamage, prompt.View?.Type);
        var view = Assert.IsType<PromptViewDto>(prompt.View);
        var metadata = view.Metadata
            ?? throw new InvalidOperationException("ASSIGN_COMBAT_DAMAGE prompt missing metadata.");
        var battleId = Assert.IsType<string>(metadata["battleId"]);
        var battlefieldId = Assert.IsType<string>(metadata["battlefieldId"]);
        var damagePool = IntMap(metadata["assignableDamagePool"]);
        var legalTargets = StringListMap(metadata["legalTargets"]);
        var lethalThreshold = IntMap(metadata["lethalDamageThreshold"]);
        var assignments = new List<CombatDamageAssignmentDto>();
        foreach (var (sourceObjectId, damage) in damagePool)
        {
            if (damage <= 0 || !legalTargets.TryGetValue(sourceObjectId, out var targets) || targets.Count == 0)
            {
                continue;
            }

            var remainingDamage = damage;
            for (var targetIndex = 0; targetIndex < targets.Count && remainingDamage > 0; targetIndex++)
            {
                var targetObjectId = targets[targetIndex];
                var isLastTarget = targetIndex == targets.Count - 1;
                var assignDamage = isLastTarget
                    ? remainingDamage
                    : Math.Min(remainingDamage, Math.Max(0, lethalThreshold.GetValueOrDefault(targetObjectId)));
                if (assignDamage <= 0)
                {
                    continue;
                }

                assignments.Add(new CombatDamageAssignmentDto(sourceObjectId, targetObjectId, assignDamage));
                remainingDamage -= assignDamage;
            }
        }

        Assert.NotEmpty(assignments);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            new AssignCombatDamageCommand(battleId, battlefieldId, assignments),
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.AssignCombatDamage,
                battleId,
                battlefieldId,
                assignments = assignments.Select(assignment => new
                {
                    assignment.SourceObjectId,
                    assignment.TargetObjectId,
                    assignment.Damage
                }).ToArray()
            }),
            CancellationToken.None);
        AssertAccepted(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitFirstDeclareBattleCandidateAsync(
        MatchSession session,
        ResolutionResult current,
        string intentId)
    {
        var playerId = current.State.ActivePlayerId;
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 auto-driver could not find an enabled DECLARE_BATTLE candidate for {playerId}.");
        var battlefieldId = candidate.Destinations?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 auto-driver could not find a DECLARE_BATTLE battlefield choice.");
        var attackerObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 auto-driver could not find a DECLARE_BATTLE attacker choice.");
        var defenderObjectId = candidate.Targets?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 auto-driver could not find a DECLARE_BATTLE defender choice.");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> TapAllAvailableRunesAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            var prompt = result.Prompts[playerId];
            var candidate = EnabledCandidate(prompt, CommandTypes.TapRune);
            var sourceObjectId = candidate?.Sources?.FirstOrDefault()?.Id;
            if (sourceObjectId is null)
            {
                return result;
            }

            result = await session.SubmitAsync(
                playerId,
                $"{intentPrefix}-{index}",
                new TapRuneCommand(sourceObjectId),
                RawCommand(new TapRuneCommand(sourceObjectId)),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded tap-rune guard.");
    }

    private static async ValueTask<ResolutionResult> TryPlayFirstUnitAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string intentPrefix,
        bool playUnitToBattlefield)
    {
        var playCandidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.PlayCard);
        if (playCandidate?.Sources is not { Count: > 0 } sources)
        {
            throw new InvalidOperationException($"B0 auto-driver could not find an enabled PLAY_CARD source for {playerId}.");
        }

        var destination = playUnitToBattlefield
            ? BattlefieldDestinationFor(current.State, playerId)
            : playCandidate.Destinations?.FirstOrDefault(choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal))?.Id ?? "BASE";
        for (var index = 0; index < sources.Count; index++)
        {
            var sourceObjectId = sources[index].Id;
            if (!current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
                || string.IsNullOrWhiteSpace(cardObject.CardNo)
                || IsDriverStandbyUnit(cardObject))
            {
                continue;
            }

            var attempted = await session.SubmitAsync(
                playerId,
                $"{intentPrefix}-attempt-{index}",
                new PlayCardCommand(sourceObjectId, cardObject.CardNo, [], Destination: destination),
                RawCommand(new PlayCardCommand(sourceObjectId, cardObject.CardNo, [], Destination: destination)),
                CancellationToken.None);
            if (!attempted.Accepted)
            {
                continue;
            }

            AssertNoHiddenZoneLeak(attempted);
            return await ResolveStackPassPassAsync(session, attempted, $"{intentPrefix}-resolve-{index}");
        }

        throw new InvalidOperationException($"B0 auto-driver could not play any exposed PLAY_CARD source for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> ResolveStackPassPassAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (result.State.StackItems.Count == 0 && string.IsNullOrWhiteSpace(result.State.PriorityPlayerId))
            {
                return result;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            if (string.IsNullOrWhiteSpace(priorityPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found stack items without a priority player.");
            }

            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-pass-priority-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded stack pass guard.");
    }

    private static async ValueTask<ResolutionResult> ResolveCurrentStackOnlyAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (result.State.StackItems.Count == 0)
            {
                return result;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            if (string.IsNullOrWhiteSpace(priorityPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found stack items without a priority player.");
            }

            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-pass-priority-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded current stack pass guard.");
    }

    private static string BattlefieldDestinationFor(MatchState state, string playerId)
    {
        var battlefieldObjectId = state.PlayerZones[playerId].Battlefields
            .FirstOrDefault(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find a battlefield card for {playerId}.");
        return $"BATTLEFIELD:{battlefieldObjectId}";
    }

    private static bool IsObjectLocatedAtBattlefield(MatchState state, string objectId, string battlefieldDestination)
    {
        var normalizedBattlefieldObjectId = battlefieldDestination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            ? battlefieldDestination["BATTLEFIELD:".Length..]
            : battlefieldDestination;
        return state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, normalizedBattlefieldObjectId, StringComparison.Ordinal);
    }

    private static async ValueTask<ResolutionResult> EndTurnAsync(
        MatchSession session,
        string playerId,
        string intentId)
    {
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            new EndTurnCommand(),
            RawCommand(new EndTurnCommand()),
            CancellationToken.None);
        AssertAccepted(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> MoveBaseUnitToOpponentBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current)
    {
        var zones = current.State.PlayerZones[playerId];
        var sourceObjectId = zones.Base.FirstOrDefault(objectId => IsReadyUnit(current.State, objectId))
            ?? throw new InvalidOperationException(
                $"B0 auto-driver could not find a ready base unit for {playerId}: "
                + JsonSerializer.Serialize(zones.Base.Select(objectId =>
                {
                    current.State.CardObjects.TryGetValue(objectId, out var cardObject);
                    return new
                    {
                        ObjectId = objectId,
                        cardObject?.CardNo,
                        Tags = cardObject?.Tags,
                        cardObject?.IsExhausted,
                        cardObject?.IsFaceDown
                    };
                }).ToArray()));
        var opponentId = OpponentOf(current.State, playerId);
        var opponentBattlefieldObjectId = current.State.PlayerZones[opponentId].Battlefields
            .FirstOrDefault(objectId => current.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find opponent battlefield for {playerId}.");

        var result = await session.SubmitAsync(
            playerId,
            "b0-move-unit-to-opponent-battlefield",
            new MoveUnitCommand(sourceObjectId, "BASE", $"BATTLEFIELD:{opponentBattlefieldObjectId}", []),
            RawCommand(new MoveUnitCommand(sourceObjectId, "BASE", $"BATTLEFIELD:{opponentBattlefieldObjectId}", [])),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> PassOpenSpellDuelAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (!string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                return result;
            }

            var focusPlayerId = result.State.FocusPlayerId;
            if (string.IsNullOrWhiteSpace(focusPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found spell duel without a focus player.");
            }

            result = await session.SubmitAsync(
                focusPlayerId,
                $"{intentPrefix}-{index}",
                new PassFocusCommand(),
                RawCommand(new PassFocusCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded spell-duel pass guard.");
    }

    private static ActionPromptCandidateDto? EnabledCandidate(ActionPromptDto prompt, string action)
    {
        return prompt.Candidates?.FirstOrDefault(candidate =>
            candidate.Enabled && string.Equals(candidate.Action, action, StringComparison.Ordinal));
    }

    private static string? PlayerWithEnabledCandidate(ResolutionResult result, string action)
    {
        return result.Prompts
            .Where(entry => EnabledCandidate(entry.Value, action) is not null)
            .Select(entry => entry.Key)
            .FirstOrDefault();
    }

    private static bool IsReadyUnit(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !IsDriverStandbyUnit(cardObject)
            && !cardObject.IsExhausted
            && !cardObject.IsFaceDown;
    }

    private static bool PlayerHandContainsCardNo(MatchState state, string playerId, string cardNo)
    {
        return FindHandCardObjectByCardNo(state, playerId, cardNo) is not null;
    }

    private static string? FindHandCardObjectByCardNo(MatchState state, string playerId, string cardNo)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Hand.FirstOrDefault(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal))
            : null;
    }

    private static IReadOnlyDictionary<string, int> IntMap(object? value)
    {
        return value switch
        {
            IReadOnlyDictionary<string, int> typed => typed,
            IReadOnlyDictionary<string, object?> objects => objects.ToDictionary(
                entry => entry.Key,
                entry => Assert.IsType<int>(entry.Value),
                StringComparer.Ordinal),
            _ => throw new InvalidOperationException($"Expected string/int metadata map, got {value?.GetType().FullName ?? "null"}.")
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> StringListMap(object? value)
    {
        return value switch
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> typed => typed,
            IReadOnlyDictionary<string, string[]> arrays => arrays.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<string>)entry.Value,
                StringComparer.Ordinal),
            IReadOnlyDictionary<string, object?> objects => objects.ToDictionary(
                entry => entry.Key,
                entry => entry.Value switch
                {
                    IReadOnlyList<string> list => list,
                    _ => throw new InvalidOperationException($"Expected string list metadata for {entry.Key}.")
                },
                StringComparer.Ordinal),
            _ => throw new InvalidOperationException($"Expected string/list metadata map, got {value?.GetType().FullName ?? "null"}.")
        };
    }

    private static bool IsDriverStandbyUnit(CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            || (CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo ?? string.Empty, out var behavior)
                && behavior.SourceUnitTags
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(CardObjectTags.Standby, StringComparer.Ordinal));
    }

    private static string OpponentOf(MatchState state, string playerId)
    {
        return state.Seats.Keys.Single(seatPlayerId => !string.Equals(seatPlayerId, playerId, StringComparison.Ordinal));
    }

    private static async ValueTask<ResolutionResult> SubmitDeckAsync(
        MatchSession session,
        string playerId,
        OfficialDecklist deck,
        string intentId)
    {
        return await session.SubmitDeckAsync(
            playerId,
            intentId,
            new SubmitDeckCommand(
                deck.LegendCardNo,
                deck.ChampionCardNo,
                deck.MainDeck,
                deck.RuneDeck,
                deck.Battlefields),
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.SubmitDeck,
                legendCardNo = deck.LegendCardNo,
                championCardNo = deck.ChampionCardNo,
                mainDeck = deck.MainDeck,
                runeDeck = deck.RuneDeck,
                battlefields = deck.Battlefields
            }),
            CancellationToken.None);
    }

    private static OfficialDecklist BuildLowCurveOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
    }

    private static OfficialDecklist BuildLowCurveOfficialDeck(
        OfficialCardCatalog catalog,
        string legendCardNo,
        string championCardNo)
    {
        return BuildLowCurveOfficialDeck(catalog, legendCardNo, championCardNo, []);
    }

    private static OfficialDecklist BuildLowCurveOfficialDeck(
        OfficialCardCatalog catalog,
        string legendCardNo,
        string championCardNo,
        IReadOnlyList<string> requiredMainDeckCardNos)
    {
        var legend = catalog.Cards.Single(card => string.Equals(card.CardNo, legendCardNo, StringComparison.Ordinal));
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var cardsByNo = catalog.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.CardNo))
            .ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        var implementedLowCurveUnits = CardBehaviorRegistry.GetAll()
            .Where(behavior => behavior.PlaysSourceToBaseAsUnit)
            .Where(behavior => behavior.RequiredTargetCount == 0 && behavior.MinTargetCount <= 0)
            .Where(behavior => string.IsNullOrWhiteSpace(behavior.Mode))
            .Where(behavior => behavior.ManaCost <= 2)
            .Select(behavior => behavior.CardNo)
            .Distinct(StringComparer.Ordinal)
            .Where(cardsByNo.ContainsKey)
            .Select(cardNo => cardsByNo[cardNo])
            .Where(card => IsMainDeckCandidate(card, allowedColors))
            .OrderBy(card => card.Energy ?? 0)
            .ThenBy(card => card.CardNo, StringComparer.Ordinal)
            .ToArray();

        var mainDeck = new List<string> { championCardNo };
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [cardsByNo[championCardNo].CardName] = 1
        };
        foreach (var cardNo in requiredMainDeckCardNos)
        {
            Assert.True(cardsByNo.TryGetValue(cardNo, out var requiredCard), $"Required card {cardNo} was not found in the official catalog.");
            Assert.True(IsRequiredMainDeckCandidate(requiredCard, allowedColors), $"Required card {cardNo} is not legal for {legendCardNo}.");
            mainDeck.Add(cardNo);
            nameCounts[requiredCard.CardName] = nameCounts.TryGetValue(requiredCard.CardName, out var current) ? current + 1 : 1;
        }

        foreach (var card in implementedLowCurveUnits)
        {
            while (mainDeck.Count < OfficialDeckValidator.MinimumMainDeckCount
                && (!nameCounts.TryGetValue(card.CardName, out var count)
                    || count < OfficialDeckValidator.DefaultMaxCopiesByName))
            {
                mainDeck.Add(card.CardNo);
                nameCounts[card.CardName] = nameCounts.TryGetValue(card.CardName, out var current) ? current + 1 : 1;
            }

            if (mainDeck.Count >= OfficialDeckValidator.MinimumMainDeckCount)
            {
                break;
            }
        }

        Assert.Equal(OfficialDeckValidator.MinimumMainDeckCount, mainDeck.Count);

        var runeDeck = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .Where(card => TraitsAllowed(card, allowedColors))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(card => card.CardNo)
            .Take(OfficialDeckValidator.RuneDeckCount)
            .ToArray();
        Assert.Equal(OfficialDeckValidator.RuneDeckCount, runeDeck.Length);

        var battlefields = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
            .GroupBy(card => card.CardName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(card => card.CardNo, StringComparer.Ordinal).First())
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Take(OfficialDeckValidator.BattlefieldCount)
            .Select(card => card.CardNo)
            .ToArray();
        Assert.Equal(OfficialDeckValidator.BattlefieldCount, battlefields.Length);

        var deck = new OfficialDecklist(legendCardNo, championCardNo, mainDeck, runeDeck, battlefields);
        var validation = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        return deck;
    }

    private static OfficialDecklist BuildDamageAssignmentOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(
            catalog,
            LilliaLegendCardNo,
            LilliaChampionCardNo,
            [
                MutantKittenCardNo,
                MutantKittenCardNo,
                MutantKittenCardNo,
                LeblancCardNo,
                LeblancCardNo,
                LeblancCardNo
            ]);
    }

    private static OfficialDecklist BuildShadowResponseOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(
            catalog,
            VexLegendCardNo,
            VexChampionCardNo,
            [
                ShadowCardNo,
                ShadowCardNo,
                ShadowCardNo
            ]);
    }

    private static bool IsMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardCategoryName is "单位" or "英雄单位"
            && !card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal)
            && card.CardGroupLimit != 1
            && !card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal)
            && TraitsAllowed(card, allowedColors);
    }

    private static bool IsRequiredMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardCategoryName is "单位" or "英雄单位" or "专属单位"
            && card.CardGroupLimit != 1
            && !card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal)
            && TraitsAllowed(card, allowedColors);
    }

    private static bool TraitsAllowed(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardColorList.All(color => string.Equals(color, "colorless", StringComparison.Ordinal)
            || allowedColors.Contains(color));
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonSerializer.SerializeToElement(new { cmdType });
    }

    private static JsonElement RawCommand(GameCommand command)
    {
        return command switch
        {
            ReadyCommand => RawCommand(command.CmdType),
            PassPriorityCommand => RawCommand(command.CmdType),
            PassFocusCommand => RawCommand(command.CmdType),
            EndTurnCommand => RawCommand(command.CmdType),
            SurrenderCommand => RawCommand(command.CmdType),
            MulliganCommand mulligan => JsonSerializer.SerializeToElement(new
            {
                cmdType = mulligan.CmdType,
                handObjectIds = mulligan.HandObjectIds
            }),
            TapRuneCommand tapRune => JsonSerializer.SerializeToElement(new
            {
                cmdType = tapRune.CmdType,
                sourceObjectId = tapRune.SourceObjectId
            }),
            PlayCardCommand playCard => JsonSerializer.SerializeToElement(new
            {
                cmdType = playCard.CmdType,
                sourceObjectId = playCard.SourceObjectId,
                cardNo = playCard.CardNo,
                targetObjectIds = playCard.TargetObjectIds,
                mode = playCard.Mode,
                optionalCosts = playCard.OptionalCosts ?? [],
                destination = playCard.Destination
            }),
            MoveUnitCommand moveUnit => JsonSerializer.SerializeToElement(new
            {
                cmdType = moveUnit.CmdType,
                sourceObjectId = moveUnit.SourceObjectId,
                origin = moveUnit.Origin,
                destination = moveUnit.Destination,
                optionalCosts = moveUnit.OptionalCosts ?? []
            }),
            DeclareBattleCommand declareBattle => JsonSerializer.SerializeToElement(new
            {
                cmdType = declareBattle.CmdType,
                battlefieldId = declareBattle.BattlefieldId,
                attackerObjectIds = declareBattle.AttackerObjectIds ?? [],
                defenderObjectIds = declareBattle.DefenderObjectIds ?? [],
                optionalCosts = declareBattle.OptionalCosts ?? [],
                battlefieldTargetObjectIds = declareBattle.BattlefieldTargetObjectIds ?? []
            }),
            ActivateAbilityCommand activateAbility => JsonSerializer.SerializeToElement(new
            {
                cmdType = activateAbility.CmdType,
                sourceObjectId = activateAbility.SourceObjectId,
                abilityId = activateAbility.AbilityId,
                targetObjectIds = activateAbility.TargetObjectIds,
                optionalCosts = activateAbility.OptionalCosts ?? []
            }),
            AssignCombatDamageCommand assignCombatDamage => JsonSerializer.SerializeToElement(new
            {
                cmdType = assignCombatDamage.CmdType,
                battleId = assignCombatDamage.BattleId,
                battlefieldId = assignCombatDamage.BattlefieldId,
                assignments = (assignCombatDamage.Assignments ?? []).Select(assignment => new
                {
                    assignment.SourceObjectId,
                    assignment.TargetObjectId,
                    assignment.Damage
                }).ToArray()
            }),
            _ => RawCommand(command.CmdType)
        };
    }

    private static void AssertAccepted(ResolutionResult result)
    {
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
    }

    private static void AssertNoHiddenZoneLeak(ResolutionResult result)
    {
        foreach (var viewerId in result.State.Seats.Keys)
        {
            var snapshotJson = JsonSerializer.Serialize(result.Snapshots[viewerId]);
            foreach (var (playerId, zones) in result.State.PlayerZones)
            {
                if (string.Equals(playerId, viewerId, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var objectId in zones.Hand.Concat(zones.MainDeck).Concat(zones.RuneDeck))
                {
                    Assert.DoesNotContain(objectId, snapshotJson, StringComparison.Ordinal);
                }
            }
        }
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

    private static MatchState BuildSeatedInitialState(string roomId, long seed)
    {
        return MatchReplayInitialStateBuilder.FromSeats(
            roomId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            }) with
        {
            Seed = seed
        };
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
