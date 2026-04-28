using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed class CoreRuleEngine : IRuleEngine
{
    private readonly IRuleEngine fallback = new PlaceholderRuleEngine();

    public ValueTask<ResolutionResult> ResolveAsync(
        MatchState state,
        PlayerIntent intent,
        GameCommand command,
        CancellationToken cancellationToken)
    {
        if (command is EndTurnCommand
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(ResolveEndTurn(state, intent));
        }

        if (string.Equals(state.Phase, MatchPhases.TurnStart, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(ResolveTurnStart(state));
        }

        return fallback.ResolveAsync(state, intent, command, cancellationToken);
    }

    private static ResolutionResult ResolveEndTurn(MatchState state, PlayerIntent intent)
    {
        var nextPlayerId = NextPlayerId(state);
        var nextTurnState = state with
        {
            TurnNumber = state.TurnNumber + 1,
            ActivePlayerId = nextPlayerId,
            TurnPlayerId = nextPlayerId,
            Phase = MatchPhases.TurnStart,
            TimingState = TimingStates.NeutralClosed,
            RunePools = ClearRunePools(state)
        };
        var turnStartResult = ResolveTurnStart(nextTurnState);
        var events = BuildTurnEndEvents(state, intent.PlayerId, nextPlayerId)
            .Concat(turnStartResult.Events)
            .ToArray();

        return turnStartResult with
        {
            Events = events
        };
    }

    private static ResolutionResult ResolveTurnStart(MatchState state)
    {
        var turnPlayerId = state.TurnPlayerId;
        var playerZones = NormalizeZonesForSeats(state);
        var currentZones = playerZones.TryGetValue(turnPlayerId, out var zones)
            ? zones
            : PlayerZones.Empty;

        var calledRuneTarget = RuneCallCount(state);
        var calledRunes = currentZones.RuneDeck.Take(calledRuneTarget).ToArray();
        var remainingRuneDeck = currentZones.RuneDeck.Skip(calledRunes.Length).ToArray();
        var drawResult = DrawOne(state, turnPlayerId, currentZones);

        playerZones[turnPlayerId] = currentZones with
        {
            MainDeck = drawResult.MainDeck,
            RuneDeck = remainingRuneDeck,
            Hand = currentZones.Hand.Concat(drawResult.DrawnCards).ToArray(),
            Graveyard = drawResult.Graveyard,
            Base = currentZones.Base.Concat(calledRunes).ToArray()
        };

        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = turnPlayerId,
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = ClearRunePools(state),
            PlayerZones = playerZones,
            PlayerScores = drawResult.PlayerScores
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            BuildTurnStartEvents(state, calledRunes.Length, drawResult),
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static Dictionary<string, PlayerZones> NormalizeZonesForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerZones.TryGetValue(playerId, out var zones) ? zones : PlayerZones.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, RunePool> ClearRunePools(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            _ => RunePool.Empty,
            StringComparer.Ordinal);
    }

    private static string NextPlayerId(MatchState state)
    {
        var players = state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
        if (players.Length == 0)
        {
            return state.TurnPlayerId;
        }

        var turnPlayerIndex = Array.IndexOf(players, state.TurnPlayerId);
        if (turnPlayerIndex < 0)
        {
            return players[0];
        }

        return players[(turnPlayerIndex + 1) % players.Length];
    }

    private static int RuneCallCount(MatchState state)
    {
        return IsSecondActionPlayersFirstTurn(state) ? 3 : 2;
    }

    private static bool IsSecondActionPlayersFirstTurn(MatchState state)
    {
        return state.TurnNumber == 2
            && state.Seats.TryGetValue(state.TurnPlayerId, out var seat)
            && string.Equals(seat, "P2", StringComparison.Ordinal);
    }

    private static DrawResult DrawOne(MatchState state, string playerId, PlayerZones zones)
    {
        var playerScores = NormalizeScoresForSeats(state);
        if (zones.MainDeck.Count > 0)
        {
            return new DrawResult(
                zones.MainDeck.Skip(1).ToArray(),
                zones.Graveyard,
                [zones.MainDeck[0]],
                false,
                null,
                playerScores);
        }

        var opponentId = OpponentOf(state, playerId);
        if (opponentId is not null)
        {
            playerScores[opponentId] = playerScores.TryGetValue(opponentId, out var score) ? score + 1 : 1;
        }

        var recycledMainDeck = zones.Graveyard.ToArray();
        var drawnCards = recycledMainDeck.Take(1).ToArray();
        return new DrawResult(
            recycledMainDeck.Skip(drawnCards.Length).ToArray(),
            [],
            drawnCards,
            true,
            opponentId,
            playerScores);
    }

    private static Dictionary<string, int> NormalizeScoresForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerScores.TryGetValue(playerId, out var score) ? score : 0,
            StringComparer.Ordinal);
    }

    private static string? OpponentOf(MatchState state, string playerId)
    {
        return state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .FirstOrDefault(candidate => !string.Equals(candidate, playerId, StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, ActionPromptDto> BuildCorePrompts(MatchState state)
    {
        if (state.Status != MatchStatuses.InProgress)
        {
            return ResolutionResult.BuildPrompts(state);
        }

        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
            playerId,
            playerId == state.ActivePlayerId,
            playerId == state.ActivePlayerId ? "当前玩家普通开环行动" : "等待对手行动",
            playerId == state.ActivePlayerId ? ["END_TURN"] : ["WAIT"]));
    }

    private static IReadOnlyList<GameEvent> BuildTurnEndEvents(
        MatchState state,
        string playerId,
        string nextTurnPlayerId)
    {
        return
        [
            new GameEvent(
                "TURN_END_DECLARED",
                $"{playerId} 声明结束回合",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["turnPlayerId"] = state.TurnPlayerId
                }),
            new GameEvent(
                "TURN_END_CLEANUP_STARTED",
                $"{state.TurnPlayerId} 回合结束特殊清理开始",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId,
                    ["phase"] = state.Phase
                }),
            new GameEvent(
                "RUNE_POOL_CLEARED",
                "回合结束时所有玩家的符文池已清空",
                new Dictionary<string, object?>
                {
                    ["playerIds"] = state.Seats.Keys.ToArray(),
                    ["timing"] = MatchPhases.TurnEnd
                }),
            new GameEvent(
                "TURN_PLAYER_ADVANCED",
                $"回合推进至 {nextTurnPlayerId}",
                new Dictionary<string, object?>
                {
                    ["previousTurnPlayerId"] = state.TurnPlayerId,
                    ["turnPlayerId"] = nextTurnPlayerId,
                    ["turnNumber"] = state.TurnNumber + 1
                })
        ];
    }

    private static IReadOnlyList<GameEvent> BuildTurnStartEvents(
        MatchState state,
        int calledRuneCount,
        DrawResult drawResult)
    {
        List<GameEvent> events =
        [
            new GameEvent(
                "TURN_START_BEGAN",
                $"{state.TurnPlayerId} 开始回合开始流程",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId
                }),
            new GameEvent(
                "RUNES_CALLED",
                $"{state.TurnPlayerId} 召出 {calledRuneCount} 张符文",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["count"] = calledRuneCount
                })
        ];

        if (drawResult.BurnoutApplied)
        {
            events.Add(new GameEvent(
                "BURNOUT_APPLIED",
                $"{state.TurnPlayerId} 执行燃尽",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["scoredPlayerId"] = drawResult.ScoredPlayerId
                }));
        }

        events.Add(new GameEvent(
            "CARD_DRAWN",
            $"{state.TurnPlayerId} 抽 {drawResult.DrawnCards.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = state.TurnPlayerId,
                ["count"] = drawResult.DrawnCards.Count
            }));
        events.Add(new GameEvent(
            "RUNE_POOL_CLEARED",
            "所有玩家的符文池已清空",
            new Dictionary<string, object?>
            {
                ["playerIds"] = state.Seats.Keys.ToArray()
            }));
        events.Add(new GameEvent(
            "MAIN_PHASE_BEGAN",
            $"{state.TurnPlayerId} 进入主阶段",
            new Dictionary<string, object?>
            {
                ["turnPlayerId"] = state.TurnPlayerId
            }));

        return events;
    }

    private sealed record DrawResult(
        IReadOnlyList<string> MainDeck,
        IReadOnlyList<string> Graveyard,
        IReadOnlyList<string> DrawnCards,
        bool BurnoutApplied,
        string? ScoredPlayerId,
        IReadOnlyDictionary<string, int> PlayerScores);
}
