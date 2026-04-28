using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public interface IRuleEngine
{
    ValueTask<ResolutionResult> ResolveAsync(
        MatchState state,
        PlayerIntent intent,
        GameCommand command,
        CancellationToken cancellationToken);
}

public sealed record MatchState(
    string RoomId,
    long Tick,
    int TurnNumber,
    string ActivePlayerId,
    IReadOnlyDictionary<string, string> Seats)
{
    public static MatchState Create(string roomId)
    {
        return new MatchState(roomId, 0, 1, "P1", new Dictionary<string, string>());
    }
}

public sealed record ResolutionResult(
    bool Accepted,
    string? ErrorMessage,
    MatchState State,
    IReadOnlyList<GameEvent> Events,
    IReadOnlyDictionary<string, SnapshotDto> Snapshots,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts,
    string? ErrorCode = null)
{
    public static ResolutionResult Rejected(
        MatchState state,
        string error,
        string errorCode = ErrorCodes.UnsupportedCommand)
    {
        return new ResolutionResult(false, error, state, [], BuildSnapshots(state), BuildPrompts(state), errorCode);
    }

    public static IReadOnlyDictionary<string, SnapshotDto> BuildSnapshots(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new SnapshotDto(
            state.Tick,
            state.TurnNumber,
            state.ActivePlayerId,
            state.Seats.ToDictionary(
                entry => entry.Key,
                entry => (object?)new Dictionary<string, object?>
                {
                    ["id"] = entry.Key,
                    ["name"] = entry.Key,
                    ["seat"] = entry.Value,
                    ["handSize"] = 0,
                    ["score"] = 0
                }),
            new Dictionary<string, object?>(),
            [],
            new Dictionary<string, object?>
            {
                ["phase"] = "ACTION"
            },
            "NEUTRAL_OPEN"));
    }

    public static IReadOnlyDictionary<string, ActionPromptDto> BuildPrompts(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
            playerId,
            playerId == state.ActivePlayerId,
            playerId == state.ActivePlayerId ? "当前玩家普通开环行动" : "等待对手行动",
            playerId == state.ActivePlayerId
                ? [
                    "PLAY_CARD",
                    "ACTIVATE_ABILITY",
                    "ASSEMBLE_EQUIPMENT",
                    "MOVE_UNIT",
                    "HIDE_CARD",
                    "TAP_RUNE",
                    "LEGEND_ACT",
                    "PASS",
                    "END_TURN"
                ]
                : ["WAIT"]));
    }
}

public sealed class PlaceholderRuleEngine : IRuleEngine
{
    public ValueTask<ResolutionResult> ResolveAsync(
        MatchState state,
        PlayerIntent intent,
        GameCommand command,
        CancellationToken cancellationToken)
    {
        if (command is UnsupportedCommand unsupported)
        {
            return ValueTask.FromResult(ResolutionResult.Rejected(
                state,
                $"Unsupported command: {unsupported.RawCmdType}",
                ErrorCodes.UnsupportedCommand));
        }

        var nextState = BuildNextState(state, command);
        var events = BuildAcceptedEvents(intent, command, nextState);

        return ValueTask.FromResult(new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            ResolutionResult.BuildPrompts(nextState)));
    }

    private static MatchState BuildNextState(MatchState state, GameCommand command)
    {
        if (command is EndTurnCommand)
        {
            return state with
            {
                Tick = state.Tick + 1,
                TurnNumber = state.TurnNumber + 1,
                ActivePlayerId = NextPlayerId(state)
            };
        }

        return state with
        {
            Tick = state.Tick + 1
        };
    }

    private static string NextPlayerId(MatchState state)
    {
        var players = state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
        if (players.Length == 0)
        {
            return state.ActivePlayerId;
        }

        var activeIndex = Array.IndexOf(players, state.ActivePlayerId);
        if (activeIndex < 0)
        {
            return players[0];
        }

        return players[(activeIndex + 1) % players.Length];
    }

    private static IReadOnlyList<GameEvent> BuildAcceptedEvents(
        PlayerIntent intent,
        GameCommand command,
        MatchState nextState)
    {
        if (command is PassCommand)
        {
            return
            [
                new GameEvent(
                    "TURN_ENDED",
                    $"{intent.PlayerId} 选择暂不行动",
                    new Dictionary<string, object?>())
            ];
        }

        if (command is EndTurnCommand)
        {
            return
            [
                new GameEvent(
                    "TURN_ENDED",
                    $"{intent.PlayerId} 结束回合",
                    new Dictionary<string, object?>()),
                new GameEvent(
                    "TURN_BEGAN",
                    "轮到下一位行动",
                    new Dictionary<string, object?>
                    {
                        ["active"] = nextState.ActivePlayerId
                    }),
                new GameEvent(
                    "RUNE_CHANNELLED",
                    $"{nextState.ActivePlayerId} 通道 1 点占位符能",
                    new Dictionary<string, object?>()),
                new GameEvent(
                    "RUNE_CHANNELLED",
                    $"{nextState.ActivePlayerId} 通道 1 点占位符能",
                    new Dictionary<string, object?>()),
                new GameEvent(
                    "CARD_DRAWN",
                    $"{nextState.ActivePlayerId} 抽 1 张牌",
                    new Dictionary<string, object?>())
            ];
        }

        return
        [
            new GameEvent(
                command.CmdType,
                "占位规则引擎接受了命令",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["intentId"] = intent.IntentId,
                    ["cmdType"] = command.CmdType
                })
        ];
    }
}

public interface IMatchSession
{
    string RoomId { get; }

    PlayerSessionDto EnsurePlayer(string playerId);

    ValueTask<PlayerSessionDto> EnsurePlayerAsync(string playerId, CancellationToken cancellationToken);

    PlayerSessionDto ReconnectPlayer(string playerId, string reconnectToken);

    ValueTask<PlayerSessionDto> ReconnectPlayerAsync(
        string playerId,
        string reconnectToken,
        CancellationToken cancellationToken);

    SnapshotDto SnapshotFor(string playerId);

    ActionPromptDto PromptFor(string playerId);

    ValueTask<ResolutionResult> SubmitAsync(
        string playerId,
        string clientIntentId,
        GameCommand command,
        JsonElement? rawCommand,
        CancellationToken cancellationToken);
}

public interface IMatchSessionRegistry
{
    ValueTask<IMatchSession> GetOrCreateAsync(string roomId, CancellationToken cancellationToken);
}

public sealed class InMemoryMatchSessionRegistry : IMatchSessionRegistry
{
    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly IMatchRecoveryStore recoveryStore;
    private readonly IMatchPlayerStore playerStore;
    private readonly Dictionary<string, IMatchSession> sessions = new();
    private readonly SemaphoreSlim gate = new(1, 1);

    public InMemoryMatchSessionRegistry(IRuleEngine ruleEngine, IMatchJournal journal)
        : this(ruleEngine, journal, NoopMatchRecoveryStore.Instance, NoopMatchPlayerStore.Instance)
    {
    }

    public InMemoryMatchSessionRegistry(
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchRecoveryStore recoveryStore)
        : this(ruleEngine, journal, recoveryStore, NoopMatchPlayerStore.Instance)
    {
    }

    public InMemoryMatchSessionRegistry(
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchRecoveryStore recoveryStore,
        IMatchPlayerStore playerStore)
    {
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.recoveryStore = recoveryStore;
        this.playerStore = playerStore;
    }

    public async ValueTask<IMatchSession> GetOrCreateAsync(string roomId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!sessions.TryGetValue(roomId, out var session))
            {
                session = await CreateSessionAsync(roomId, cancellationToken).ConfigureAwait(false);
                sessions.Add(roomId, session);
            }

            return session;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<IMatchSession> CreateSessionAsync(string roomId, CancellationToken cancellationToken)
    {
        var recovery = await recoveryStore.LoadAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (recovery is null)
        {
            return new MatchSession(roomId, ruleEngine, journal, playerStore);
        }

        if (!string.Equals(recovery.RoomId, roomId, StringComparison.Ordinal))
        {
            throw new MatchSessionException(
                ErrorCodes.RecoveryInconsistent,
                $"match recovery returned room {recovery.RoomId} for requested room {roomId}");
        }

        return MatchSession.Restore(recovery, ruleEngine, journal, playerStore);
    }
}

public sealed class MatchSession : IMatchSession
{
    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly IMatchPlayerStore playerStore;
    private readonly object seatGate = new();
    private readonly SemaphoreSlim serialGate = new(1, 1);
    private readonly Dictionary<string, string> seats = new();
    private readonly Dictionary<string, string?> reconnectTokens = new();
    private readonly Dictionary<string, CachedResolution> intentCache = new();
    private MatchState state;
    private long lastEventSequence;

    public MatchSession(string roomId, IRuleEngine ruleEngine)
        : this(roomId, ruleEngine, NoopMatchJournal.Instance)
    {
    }

    public MatchSession(string roomId, IRuleEngine ruleEngine, IMatchJournal journal)
        : this(roomId, ruleEngine, journal, NoopMatchPlayerStore.Instance)
    {
    }

    public MatchSession(
        string roomId,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore)
    {
        RoomId = roomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.playerStore = playerStore;
        state = MatchState.Create(roomId);
    }

    private MatchSession(
        MatchState restoredState,
        long restoredLastEventSequence,
        IReadOnlyDictionary<string, string> restoredSeats,
        IReadOnlyList<RecoveredCommand> restoredCommands,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore)
    {
        RoomId = restoredState.RoomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.playerStore = playerStore;
        state = restoredState;
        lastEventSequence = restoredLastEventSequence;
        foreach (var (playerId, seat) in restoredSeats)
        {
            seats[playerId] = seat;
            reconnectTokens[playerId] = null;
        }

        foreach (var command in restoredCommands)
        {
            var result = new ResolutionResult(
                command.Accepted,
                command.ErrorMessage,
                restoredState,
                [],
                ResolutionResult.BuildSnapshots(restoredState),
                ResolutionResult.BuildPrompts(restoredState),
                command.Accepted ? null : ErrorCodes.UnsupportedCommand);
            intentCache[$"{command.PlayerId}:{command.ClientIntentId}"] = new CachedResolution(
                command.CommandType,
                result);
        }
    }

    public string RoomId { get; }

    public static MatchSession Restore(
        MatchRecoveryFrame recovery,
        IRuleEngine ruleEngine,
        IMatchJournal journal)
    {
        return Restore(recovery, ruleEngine, journal, NoopMatchPlayerStore.Instance);
    }

    public static MatchSession Restore(
        MatchRecoveryFrame recovery,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore)
    {
        if (!recovery.IsConsistent)
        {
            throw new MatchSessionException(
                ErrorCodes.RecoveryInconsistent,
                $"match recovery is inconsistent: {string.Join("; ", recovery.ValidationErrors)}");
        }

        var state = RestoreState(recovery);
        return new MatchSession(
            state,
            recovery.LastEventSequence,
            state.Seats,
            recovery.Commands,
            ruleEngine,
            journal,
            playerStore);
    }

    public PlayerSessionDto EnsurePlayer(string playerId)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        lock (seatGate)
        {
            if (seats.ContainsKey(normalizedPlayerId))
            {
                return PlayerSessionFor(normalizedPlayerId);
            }

            if (seats.Count >= 2)
            {
                throw new MatchSessionException(ErrorCodes.RoomFull, "room already has two players");
            }

            seats[normalizedPlayerId] = NextOpenSeat();
            reconnectTokens[normalizedPlayerId] = NewReconnectToken();
            state = state with
            {
                ActivePlayerId = seats.ContainsKey(state.ActivePlayerId) ? state.ActivePlayerId : normalizedPlayerId,
                Seats = new Dictionary<string, string>(seats)
            };
            return PlayerSessionFor(normalizedPlayerId);
        }
    }

    public async ValueTask<PlayerSessionDto> EnsurePlayerAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        lock (seatGate)
        {
            if (seats.ContainsKey(normalizedPlayerId)
                && (!reconnectTokens.TryGetValue(normalizedPlayerId, out var liveToken)
                    || string.IsNullOrWhiteSpace(liveToken)))
            {
                throw new MatchSessionException(
                    ErrorCodes.InvalidReconnectToken,
                    "reconnect token required for existing player");
            }
        }

        var playerSession = EnsurePlayer(normalizedPlayerId);
        await PersistPlayerSessionAsync(playerSession, cancellationToken).ConfigureAwait(false);
        return playerSession;
    }

    public PlayerSessionDto ReconnectPlayer(string playerId, string reconnectToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        lock (seatGate)
        {
            if (!reconnectTokens.TryGetValue(normalizedPlayerId, out var expectedToken)
                || string.IsNullOrWhiteSpace(reconnectToken)
                || !string.Equals(expectedToken, reconnectToken.Trim(), StringComparison.Ordinal))
            {
                throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "invalid reconnect token");
            }

            return PlayerSessionFor(normalizedPlayerId);
        }
    }

    public async ValueTask<PlayerSessionDto> ReconnectPlayerAsync(
        string playerId,
        string reconnectToken,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        var normalizedToken = reconnectToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "invalid reconnect token");
        }

        bool valid;
        lock (seatGate)
        {
            RequirePlayer(normalizedPlayerId);
            valid = reconnectTokens.TryGetValue(normalizedPlayerId, out var expectedToken)
                && !string.IsNullOrWhiteSpace(expectedToken)
                && string.Equals(expectedToken, normalizedToken, StringComparison.Ordinal);
        }

        if (!valid)
        {
            var tokenHash = ReconnectTokenHasher.Hash(normalizedToken);
            valid = await playerStore.HasReconnectTokenHashAsync(
                    RoomId,
                    normalizedPlayerId,
                    tokenHash,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!valid)
        {
            throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "invalid reconnect token");
        }

        return await RotateReconnectTokenAsync(normalizedPlayerId, cancellationToken).ConfigureAwait(false);
    }

    public SnapshotDto SnapshotFor(string playerId)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        return ResolutionResult.BuildSnapshots(state)[normalizedPlayerId];
    }

    public ActionPromptDto PromptFor(string playerId)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        return ResolutionResult.BuildPrompts(state)[normalizedPlayerId];
    }

    public async ValueTask<ResolutionResult> SubmitAsync(
        string playerId,
        string clientIntentId,
        GameCommand command,
        JsonElement? rawCommand,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        var normalizedIntentId = string.IsNullOrWhiteSpace(clientIntentId)
            ? Guid.NewGuid().ToString("N")
            : clientIntentId.Trim();
        var cacheKey = $"{normalizedPlayerId}:{normalizedIntentId}";

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (!string.Equals(cached.CommandType, command.CmdType, StringComparison.Ordinal))
                {
                    return ResolutionResult.Rejected(
                        state,
                        "clientIntentId already belongs to another command",
                        ErrorCodes.ClientIntentConflict);
                }

                return cached.Result;
            }

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            var intent = new PlayerIntent(normalizedIntentId, normalizedPlayerId, command.CmdType);
            var result = await ruleEngine.ResolveAsync(state, intent, command, cancellationToken)
                .ConfigureAwait(false);
            var completedEventSequence = startedEventSequence + result.Events.Count;
            await journal.RecordAsync(new MatchJournalEntry(
                    RoomId,
                    normalizedPlayerId,
                    normalizedIntentId,
                    command.CmdType,
                    rawCommand,
                    startedTick,
                    result.State.Tick,
                    startedEventSequence,
                    completedEventSequence,
                    result.Accepted,
                    result.ErrorMessage,
                    result.State,
                    result.Events,
                    result.Snapshots,
                    result.Prompts,
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);

            if (result.Accepted)
            {
                state = result.State;
            }
            lastEventSequence = completedEventSequence;

            intentCache[cacheKey] = new CachedResolution(command.CmdType, result);
            return result;
        }
        finally
        {
            serialGate.Release();
        }
    }

    private sealed record CachedResolution(string CommandType, ResolutionResult Result);

    private static MatchState RestoreState(MatchRecoveryFrame recovery)
    {
        if (recovery.AuthoritativeState is not null)
        {
            return recovery.AuthoritativeState with
            {
                Tick = recovery.CurrentTick
            };
        }

        if (recovery.PlayerViews.Count == 0)
        {
            return MatchState.Create(recovery.RoomId) with
            {
                Tick = recovery.CurrentTick
            };
        }

        var baseline = recovery.PlayerViews.Values
            .OrderBy(view => view.PlayerId, StringComparer.Ordinal)
            .First()
            .Snapshot;
        var seats = MatchRecoveryValidator.ExtractSeats(baseline);
        return new MatchState(
            recovery.RoomId,
            recovery.CurrentTick,
            baseline.TurnNumber,
            baseline.ActivePlayerId,
            seats);
    }

    private string NextOpenSeat()
    {
        return seats.ContainsValue("P1") ? "P2" : "P1";
    }

    private PlayerSessionDto PlayerSessionFor(string playerId)
    {
        if (!reconnectTokens.TryGetValue(playerId, out var reconnectToken)
            || string.IsNullOrWhiteSpace(reconnectToken))
        {
            reconnectToken = NewReconnectToken();
            reconnectTokens[playerId] = reconnectToken;
        }

        return new PlayerSessionDto(playerId, seats[playerId], reconnectToken);
    }

    private async ValueTask<PlayerSessionDto> RotateReconnectTokenAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        PlayerSessionDto playerSession;
        lock (seatGate)
        {
            reconnectTokens[playerId] = NewReconnectToken();
            playerSession = PlayerSessionFor(playerId);
        }

        await PersistPlayerSessionAsync(playerSession, cancellationToken).ConfigureAwait(false);
        return playerSession;
    }

    private ValueTask PersistPlayerSessionAsync(
        PlayerSessionDto playerSession,
        CancellationToken cancellationToken)
    {
        return playerStore.SavePlayerSessionAsync(
            RoomId,
            playerSession.PlayerId,
            playerSession.Seat,
            ReconnectTokenHasher.Hash(playerSession.ReconnectToken),
            cancellationToken);
    }

    private void RequirePlayer(string playerId)
    {
        lock (seatGate)
        {
            if (!seats.ContainsKey(playerId))
            {
                throw new MatchSessionException(ErrorCodes.PlayerNotInRoom, "player is not in room");
            }
        }
    }

    private static string NewReconnectToken()
    {
        return $"rt_{Guid.NewGuid():N}";
    }

    private static string NormalizePlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new MatchSessionException(ErrorCodes.PlayerIdRequired, "playerId is required");
        }

        return playerId.Trim();
    }
}

public sealed class MatchSessionException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
