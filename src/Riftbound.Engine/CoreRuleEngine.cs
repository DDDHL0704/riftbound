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
        if (string.Equals(state.Phase, MatchPhases.TurnStart, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(ResolveTurnStart(state));
        }

        return fallback.ResolveAsync(state, intent, command, cancellationToken);
    }

    private static ResolutionResult ResolveTurnStart(MatchState state)
    {
        var turnPlayerId = state.TurnPlayerId;
        var playerZones = NormalizeZonesForSeats(state);
        var currentZones = playerZones.TryGetValue(turnPlayerId, out var zones)
            ? zones
            : PlayerZones.Empty;

        var calledRunes = currentZones.RuneDeck.Take(2).ToArray();
        var remainingRuneDeck = currentZones.RuneDeck.Skip(calledRunes.Length).ToArray();
        var mainDeck = currentZones.MainDeck.ToArray();
        var drawnCard = mainDeck.Take(1).ToArray();
        var remainingMainDeck = mainDeck.Skip(drawnCard.Length).ToArray();

        playerZones[turnPlayerId] = currentZones with
        {
            MainDeck = remainingMainDeck,
            RuneDeck = remainingRuneDeck,
            Hand = currentZones.Hand.Concat(drawnCard).ToArray(),
            Base = currentZones.Base.Concat(calledRunes).ToArray()
        };

        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = turnPlayerId,
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = ClearRunePools(state),
            PlayerZones = playerZones
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            BuildTurnStartEvents(state, calledRunes.Length, drawnCard.Length),
            ResolutionResult.BuildSnapshots(nextState),
            ResolutionResult.BuildPrompts(nextState));
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

    private static IReadOnlyList<GameEvent> BuildTurnStartEvents(
        MatchState state,
        int calledRuneCount,
        int drawnCardCount)
    {
        return
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
                }),
            new GameEvent(
                "CARD_DRAWN",
                $"{state.TurnPlayerId} 抽 {drawnCardCount} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["count"] = drawnCardCount
                }),
            new GameEvent(
                "RUNE_POOL_CLEARED",
                "所有玩家的符文池已清空",
                new Dictionary<string, object?>
                {
                    ["playerIds"] = state.Seats.Keys.ToArray()
                }),
            new GameEvent(
                "MAIN_PHASE_BEGAN",
                $"{state.TurnPlayerId} 进入主阶段",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId
                })
        ];
    }
}
