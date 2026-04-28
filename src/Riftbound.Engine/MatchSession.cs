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
    IReadOnlyDictionary<string, ActionPromptDto> Prompts)
{
    public static ResolutionResult Rejected(MatchState state, string error)
    {
        return new ResolutionResult(false, error, state, [], BuildSnapshots(state), BuildPrompts(state));
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
                    ["name"] = entry.Value,
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
                $"Unsupported command: {unsupported.RawCmdType}"));
        }

        var nextState = state with
        {
            Tick = state.Tick + 1
        };
        var events = new[]
        {
            BuildAcceptedEvent(intent, command)
        };

        return ValueTask.FromResult(new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            ResolutionResult.BuildPrompts(nextState)));
    }

    private static GameEvent BuildAcceptedEvent(PlayerIntent intent, GameCommand command)
    {
        if (command is PassCommand)
        {
            return new GameEvent(
                "TURN_ENDED",
                $"{intent.PlayerId} 选择暂不行动",
                new Dictionary<string, object?>());
        }

        if (command is EndTurnCommand)
        {
            return new GameEvent(
                "TURN_ENDED",
                $"{intent.PlayerId} 结束回合",
                new Dictionary<string, object?>());
        }

        return new GameEvent(
            command.CmdType,
            "占位规则引擎接受了命令",
            new Dictionary<string, object?>
            {
                ["playerId"] = intent.PlayerId,
                ["intentId"] = intent.IntentId,
                ["cmdType"] = command.CmdType
            });
    }
}

public interface IMatchSession
{
    string RoomId { get; }

    void EnsurePlayer(string playerId);

    SnapshotDto SnapshotFor(string playerId);

    ActionPromptDto PromptFor(string playerId);

    ValueTask<ResolutionResult> SubmitAsync(
        string playerId,
        string clientIntentId,
        GameCommand command,
        CancellationToken cancellationToken);
}

public interface IMatchSessionRegistry
{
    IMatchSession GetOrCreate(string roomId);
}

public sealed class InMemoryMatchSessionRegistry(IRuleEngine ruleEngine, IMatchJournal journal) : IMatchSessionRegistry
{
    private readonly Dictionary<string, IMatchSession> sessions = new();
    private readonly object gate = new();

    public IMatchSession GetOrCreate(string roomId)
    {
        lock (gate)
        {
            if (!sessions.TryGetValue(roomId, out var session))
            {
                session = new MatchSession(roomId, ruleEngine, journal);
                sessions.Add(roomId, session);
            }

            return session;
        }
    }
}

public sealed class MatchSession : IMatchSession
{
    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly SemaphoreSlim serialGate = new(1, 1);
    private readonly Dictionary<string, string> seats = new();
    private readonly Dictionary<string, CachedResolution> intentCache = new();
    private MatchState state;

    public MatchSession(string roomId, IRuleEngine ruleEngine)
        : this(roomId, ruleEngine, NoopMatchJournal.Instance)
    {
    }

    public MatchSession(string roomId, IRuleEngine ruleEngine, IMatchJournal journal)
    {
        RoomId = roomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        state = MatchState.Create(roomId);
    }

    public string RoomId { get; }

    public void EnsurePlayer(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("playerId is required", nameof(playerId));
        }

        if (seats.ContainsKey(playerId))
        {
            return;
        }

        seats[playerId] = playerId;
        state = state with
        {
            ActivePlayerId = seats.ContainsKey(state.ActivePlayerId) ? state.ActivePlayerId : playerId,
            Seats = new Dictionary<string, string>(seats)
        };
    }

    public SnapshotDto SnapshotFor(string playerId)
    {
        EnsurePlayer(playerId);
        return ResolutionResult.BuildSnapshots(state)[playerId];
    }

    public ActionPromptDto PromptFor(string playerId)
    {
        EnsurePlayer(playerId);
        return ResolutionResult.BuildPrompts(state)[playerId];
    }

    public async ValueTask<ResolutionResult> SubmitAsync(
        string playerId,
        string clientIntentId,
        GameCommand command,
        CancellationToken cancellationToken)
    {
        EnsurePlayer(playerId);
        var normalizedIntentId = string.IsNullOrWhiteSpace(clientIntentId)
            ? Guid.NewGuid().ToString("N")
            : clientIntentId.Trim();
        var cacheKey = $"{playerId}:{normalizedIntentId}";

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (!string.Equals(cached.CommandType, command.CmdType, StringComparison.Ordinal))
                {
                    return ResolutionResult.Rejected(state, "clientIntentId already belongs to another command");
                }

                return cached.Result;
            }

            var startedTick = state.Tick;
            var intent = new PlayerIntent(normalizedIntentId, playerId, command.CmdType);
            var result = await ruleEngine.ResolveAsync(state, intent, command, cancellationToken)
                .ConfigureAwait(false);
            await journal.RecordAsync(new MatchJournalEntry(
                    RoomId,
                    playerId,
                    normalizedIntentId,
                    command.CmdType,
                    startedTick,
                    result.State.Tick,
                    result.Accepted,
                    result.ErrorMessage,
                    result.Events,
                    result.Snapshots,
                    result.Prompts,
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);

            if (result.Accepted)
            {
                state = result.State;
            }

            intentCache[cacheKey] = new CachedResolution(command.CmdType, result);
            return result;
        }
        finally
        {
            serialGate.Release();
        }
    }

    private sealed record CachedResolution(string CommandType, ResolutionResult Result);
}
