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
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var secondMulligan = await session.SubmitAsync(
            secondPlayerId,
            "b0-mulligan-second",
            new MulliganCommand([]),
            RawCommand(CommandTypes.Mulligan),
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
        for (var index = 0; index < 10; index++)
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
                RawCommand(CommandTypes.TapRune),
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
                RawCommand(CommandTypes.PlayCard),
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
                RawCommand(CommandTypes.PassPriority),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded stack pass guard.");
    }

    private static string BattlefieldDestinationFor(MatchState state, string playerId)
    {
        var battlefieldObjectId = state.PlayerZones[playerId].Battlefields
            .FirstOrDefault(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find a battlefield card for {playerId}.");
        return $"BATTLEFIELD:{battlefieldObjectId}";
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
            RawCommand(CommandTypes.EndTurn),
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
            RawCommand(CommandTypes.MoveUnit),
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
                RawCommand(CommandTypes.PassFocus),
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

    private static bool IsMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardCategoryName is "单位" or "英雄单位"
            && !card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal)
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
}
