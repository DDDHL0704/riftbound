using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.Api.Hubs;

public interface IGameClient
{
    Task Joined(WsServerMessage message);

    Task Snapshot(WsServerMessage message);

    Task Prompt(WsServerMessage message);

    Task Events(WsServerMessage message);

    Task Error(WsServerMessage message);

    Task Matchmaking(WsServerMessage message);
}

public sealed class GameHub(
    IMatchSessionRegistry sessions,
    IHostEnvironment? hostEnvironment = null,
    IConfiguration? configuration = null,
    PlayerIdentityService? playerIdentity = null,
    IMatchmakingQueue? matchmakingQueue = null,
    IPublicMatchDirectory? publicMatches = null) : Hub<IGameClient>
{
    private const string AuthenticatedHandleItemKey = "riftbound:authenticatedHandle";

    public async Task<AuthResultDto> Authenticate(string handle, string playerKey)
    {
        if (playerIdentity is null)
        {
            return new AuthResultDto(false, "IDENTITY_NOT_CONFIGURED", PlayerIdentityService.NormalizeHandle(handle));
        }

        var result = await playerIdentity.AuthenticateAsync(handle, playerKey, Context.ConnectionAborted);
        if (result.Authenticated)
        {
            Context.Items[AuthenticatedHandleItemKey] = result.NormalizedHandle;
        }

        return new AuthResultDto(result.Authenticated, result.Status.ToString(), result.NormalizedHandle);
    }

    public async Task<MatchmakingStatusDto> EnqueueMatchmaking(string playerId)
    {
        var normalizedPlayerId = PlayerIdentityService.NormalizeHandle(playerId);
        if (TryRejectUnauthenticatedOrMismatchedIdentity(
                normalizedPlayerId,
                out var rejected,
                out var rejection))
        {
            await rejection;
            return rejected;
        }

        if (matchmakingQueue is null)
        {
            var result = RejectedMatchmakingStatus(
                normalizedPlayerId,
                ErrorCodes.UnsupportedCommand,
                "匹配队列未配置。");
            await SendMatchmakingError(normalizedPlayerId, result);
            return result;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            MatchmakingPlayerGroup(normalizedPlayerId),
            Context.ConnectionAborted);

        MatchmakingQueueResult queueResult;
        try
        {
            queueResult = await matchmakingQueue.EnqueueAsync(normalizedPlayerId, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or MatchSessionException)
        {
            var result = RejectedMatchmakingStatus(
                normalizedPlayerId,
                ErrorCodeFor(ex),
                ex.Message);
            await SendMatchmakingError(normalizedPlayerId, result);
            return result;
        }

        var callerStatus = MatchmakingStatusFor(queueResult, normalizedPlayerId);
        await Clients.Caller.Matchmaking(new WsServerMessage(
            MessageType.MATCHMAKING,
            callerStatus.RoomId ?? string.Empty,
            normalizedPlayerId,
            0,
            callerStatus));

        if (string.Equals(queueResult.State, MatchmakingStates.Matched, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(queueResult.OpponentPlayerId))
        {
            var opponentStatus = MatchmakingStatusFor(queueResult, queueResult.OpponentPlayerId);
            await Clients.Group(MatchmakingPlayerGroup(queueResult.OpponentPlayerId)).Matchmaking(new WsServerMessage(
                MessageType.MATCHMAKING,
                opponentStatus.RoomId ?? string.Empty,
                queueResult.OpponentPlayerId,
                0,
                opponentStatus));
        }

        return callerStatus;
    }

    public async Task<MatchmakingStatusDto> CancelMatchmaking(string playerId)
    {
        var normalizedPlayerId = PlayerIdentityService.NormalizeHandle(playerId);
        if (TryRejectUnauthenticatedOrMismatchedIdentity(
                normalizedPlayerId,
                out var rejected,
                out var rejection))
        {
            await rejection;
            return rejected;
        }

        if (matchmakingQueue is null)
        {
            var result = RejectedMatchmakingStatus(
                normalizedPlayerId,
                ErrorCodes.UnsupportedCommand,
                "匹配队列未配置。");
            await SendMatchmakingError(normalizedPlayerId, result);
            return result;
        }

        MatchmakingQueueResult queueResult;
        try
        {
            queueResult = await matchmakingQueue.CancelAsync(normalizedPlayerId, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            var result = RejectedMatchmakingStatus(
                normalizedPlayerId,
                ErrorCodeFor(ex),
                ex.Message);
            await SendMatchmakingError(normalizedPlayerId, result);
            return result;
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            MatchmakingPlayerGroup(normalizedPlayerId),
            Context.ConnectionAborted);

        var callerStatus = MatchmakingStatusFor(queueResult, normalizedPlayerId);
        await Clients.Caller.Matchmaking(new WsServerMessage(
            MessageType.MATCHMAKING,
            string.Empty,
            normalizedPlayerId,
            0,
            callerStatus));
        return callerStatus;
    }

    public async Task<CreatePublicMatchResultDto?> CreatePublicMatch(string playerId)
    {
        var normalizedPlayerId = PlayerIdentityService.NormalizeHandle(playerId);
        if (TryRejectPublicMatchIdentity(normalizedPlayerId, out var rejection))
        {
            await rejection;
            return null;
        }

        if (publicMatches is null)
        {
            await SendClientError(
                string.Empty,
                normalizedPlayerId,
                ErrorCodes.UnsupportedCommand,
                "公开对局目录未配置。");
            return null;
        }

        var roomId = RoomCodeGenerator.NewRoomId();
        IMatchSession session;
        PlayerSessionDto playerSession;
        PublicMatchDto match;
        try
        {
            session = await sessions.GetOrCreateAsync(roomId, Context.ConnectionAborted);
            playerSession = await session.EnsurePlayerAsync(normalizedPlayerId, Context.ConnectionAborted);
            match = await publicMatches.CreateAsync(roomId, normalizedPlayerId, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is MatchSessionException or ArgumentException or InvalidOperationException)
        {
            await SendError(roomId, normalizedPlayerId, 0, ex);
            return null;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PlayerGroup(roomId, normalizedPlayerId),
            Context.ConnectionAborted);

        await Clients.Caller.Joined(new WsServerMessage(
            MessageType.JOIN,
            roomId,
            normalizedPlayerId,
            0,
            playerSession));

        await SendSnapshotAndPrompt(session, roomId, normalizedPlayerId);
        return new CreatePublicMatchResultDto(match, playerSession);
    }

    public async Task<IReadOnlyList<PublicMatchDto>> ListPublicMatches()
    {
        return publicMatches is null
            ? []
            : await publicMatches.ListOpenAsync(Context.ConnectionAborted);
    }

    public async Task JoinRoom(string roomId, string playerId, string? reconnectToken = null)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
        if (TryRejectIdentityMismatch(roomId, normalizedPlayerId, out var rejection))
        {
            await rejection;
            return;
        }

        IMatchSession session;
        PlayerSessionDto playerSession;
        try
        {
            session = await sessions.GetOrCreateAsync(roomId, Context.ConnectionAborted);
            playerSession = await session.EnsurePlayerAsync(normalizedPlayerId, Context.ConnectionAborted);
            if (publicMatches is not null)
            {
                await publicMatches.NotifyPlayerJoinedAsync(roomId, normalizedPlayerId, Context.ConnectionAborted);
            }
        }
        catch (Exception ex) when (ex is MatchSessionException or ArgumentException or InvalidOperationException)
        {
            await SendError(roomId, normalizedPlayerId, 0, ex);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(roomId, normalizedPlayerId));

        await Clients.Caller.Joined(new WsServerMessage(
            MessageType.JOIN,
            roomId,
            normalizedPlayerId,
            0,
            playerSession));

        await SendSnapshotAndPrompt(session, roomId, normalizedPlayerId);
    }

    public async Task Reconnect(string roomId, string playerId, string reconnectToken)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
        if (TryRejectIdentityMismatch(roomId, normalizedPlayerId, out var rejection))
        {
            await rejection;
            return;
        }

        IMatchSession session;
        PlayerSessionDto playerSession;
        try
        {
            session = await sessions.GetOrCreateAsync(roomId, Context.ConnectionAborted);
            playerSession = await session.ReconnectPlayerAsync(
                normalizedPlayerId,
                reconnectToken,
                Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is MatchSessionException or ArgumentException or InvalidOperationException)
        {
            await SendError(roomId, normalizedPlayerId, 0, ex);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(roomId, normalizedPlayerId));

        await Clients.Caller.Joined(new WsServerMessage(
            MessageType.RECONNECT,
            roomId,
            normalizedPlayerId,
            0,
            playerSession));

        await SendSnapshotAndPrompt(session, roomId, normalizedPlayerId);
    }

    public async Task RequestSnapshot(string roomId, string playerId)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
        if (TryRejectIdentityMismatch(roomId, normalizedPlayerId, out var rejection))
        {
            await rejection;
            return;
        }

        try
        {
            var session = await sessions.GetOrCreateAsync(roomId, Context.ConnectionAborted);
            await SendSnapshotAndPrompt(session, roomId, normalizedPlayerId);
        }
        catch (Exception ex) when (ex is MatchSessionException or ArgumentException or InvalidOperationException)
        {
            await SendError(roomId, normalizedPlayerId, 0, ex);
        }
    }

    private async Task SendSnapshotAndPrompt(IMatchSession session, string roomId, string playerId)
    {
        var snapshot = session.SnapshotFor(playerId);
        var prompt = session.PromptFor(playerId);

        await Clients.Caller.Snapshot(new WsServerMessage(
            MessageType.SNAPSHOT,
            roomId,
            playerId,
            snapshot.Tick,
            snapshot));

        await Clients.Caller.Prompt(new WsServerMessage(
            MessageType.PROMPT,
            roomId,
            playerId,
            snapshot.Tick,
            prompt));
    }

    public Task<CommandReceiptDto> Pass(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(
            roomId,
            playerId,
            clientIntentId,
            new PassCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "PASS" }));
    }

    public Task<CommandReceiptDto> EndTurn(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(
            roomId,
            playerId,
            clientIntentId,
            new EndTurnCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "END_TURN" }));
    }

    public Task<CommandReceiptDto> Ready(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(
            roomId,
            playerId,
            clientIntentId,
            new ReadyCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "READY" }));
    }

    public Task<CommandReceiptDto> SubmitIntent(string roomId, string playerId, string clientIntentId, JsonElement cmd)
    {
        return SubmitCommand(roomId, playerId, clientIntentId, GameCommandJsonMapper.Map(cmd), cmd.Clone());
    }

    public async Task SeedScenario(string roomId, string playerId, string scenarioId, string clientIntentId)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
        if (TryRejectIdentityMismatch(roomId, normalizedPlayerId, out var rejection))
        {
            await rejection;
            return;
        }

        var allowDevSeedScenarios = configuration?.GetValue<bool>("Riftbound:AllowDevSeedScenarios") == true;
        if (!allowDevSeedScenarios && hostEnvironment is not null && !hostEnvironment.IsDevelopment())
        {
            await Clients.Caller.Error(new WsServerMessage(
                MessageType.ERROR,
                roomId,
                normalizedPlayerId,
                0,
                new ErrorDto(ErrorCodes.UnsupportedCommand, "载入测试状态仅在开发环境可用。")));
            return;
        }

        ResolutionResult result;
        try
        {
            var session = await sessions.GetOrCreateAsync(roomId, Context.ConnectionAborted);
            result = await session.SeedScenarioAsync(
                normalizedPlayerId,
                clientIntentId,
                scenarioId,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId }),
                Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is MatchSessionException or ArgumentException or InvalidOperationException)
        {
            await SendError(roomId, normalizedPlayerId, 0, ex);
            return;
        }

        if (result.Events.Count > 0)
        {
            var events = ProjectEvents(result.Events, result.State);
            await Clients.Group(RoomGroup(roomId)).Events(new WsServerMessage(
                MessageType.EVENTS,
                roomId,
                normalizedPlayerId,
                result.State.Tick,
                events));
        }

        foreach (var (snapshotPlayerId, snapshot) in result.Snapshots)
        {
            await Clients.Group(PlayerGroup(roomId, snapshotPlayerId)).Snapshot(new WsServerMessage(
                MessageType.SNAPSHOT,
                roomId,
                snapshotPlayerId,
                snapshot.Tick,
                snapshot));
        }

        foreach (var (promptPlayerId, prompt) in result.Prompts)
        {
            await Clients.Group(PlayerGroup(roomId, promptPlayerId)).Prompt(new WsServerMessage(
                MessageType.PROMPT,
                roomId,
                promptPlayerId,
                result.State.Tick,
                prompt));
        }
    }

    private async Task<CommandReceiptDto> SubmitCommand(
        string roomId,
        string playerId,
        string clientIntentId,
        GameCommand command,
        JsonElement rawCommand)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
        if (TryRejectIdentityMismatch(roomId, normalizedPlayerId, out var rejection))
        {
            await rejection;
            return CommandReceipt(
                roomId,
                normalizedPlayerId,
                clientIntentId,
                command,
                rawCommand,
                accepted: false,
                serverTick: 0,
                state: "REJECTED",
                message: "已认证身份与请求的玩家不一致，已拒绝以他人身份提交命令。",
                errorCode: ErrorCodes.IdentityMismatch,
                followup: CommandReceiptFollowups.Create(
                    accepted: false,
                    serverTick: 0,
                    eventCount: 0,
                    snapshotCount: 0,
                    promptCount: 0,
                    receiptState: "REJECTED"));
        }

        ResolutionResult result;
        try
        {
            var session = await sessions.GetOrCreateAsync(roomId, Context.ConnectionAborted);
            result = command is ReadyCommand
                ? await session.ReadyAsync(
                    normalizedPlayerId,
                    clientIntentId,
                    rawCommand,
                    Context.ConnectionAborted)
                : command is SubmitDeckCommand submitDeckCommand
                    ? await session.SubmitDeckAsync(
                        normalizedPlayerId,
                        clientIntentId,
                        submitDeckCommand,
                        rawCommand,
                        Context.ConnectionAborted)
                : await session.SubmitAsync(
                    normalizedPlayerId,
                    clientIntentId,
                    command,
                    rawCommand,
                    Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is MatchSessionException or ArgumentException or InvalidOperationException)
        {
            await SendError(roomId, normalizedPlayerId, 0, ex);
            return CommandReceipt(
                roomId,
                normalizedPlayerId,
                clientIntentId,
                command,
                rawCommand,
                accepted: false,
                serverTick: 0,
                state: "FAILED",
                message: "服务端未能接收命令，已返回错误。",
                errorCode: ErrorCodeFor(ex),
                followup: CommandReceiptFollowups.Create(
                    accepted: false,
                    serverTick: 0,
                    eventCount: 0,
                    snapshotCount: 0,
                    promptCount: 0,
                    receiptState: "FAILED"));
        }

        if (!result.Accepted)
        {
            var errorCode = result.ErrorCode ?? ErrorCodes.UnsupportedCommand;
            var errorMessage = result.ErrorMessage ?? "command rejected";
            await Clients.Caller.Error(new WsServerMessage(
                MessageType.ERROR,
                roomId,
                normalizedPlayerId,
                result.State.Tick,
                new ErrorDto(
                    errorCode,
                    errorMessage)));
            return CommandReceipt(
                roomId,
                normalizedPlayerId,
                clientIntentId,
                command,
                rawCommand,
                accepted: false,
                serverTick: result.State.Tick,
                state: "REJECTED",
                message: errorMessage,
                errorCode: errorCode,
                followup: CommandReceiptFollowups.Create(
                    accepted: false,
                    serverTick: result.State.Tick,
                    eventCount: 0,
                    snapshotCount: 0,
                    promptCount: 0,
                    receiptState: "REJECTED"));
        }

        var projectedEvents = result.Events.Count > 0
            ? ProjectEvents(result.Events, result.State)
            : Array.Empty<GameEvent>();
        if (projectedEvents.Count > 0)
        {
            await Clients.Group(RoomGroup(roomId)).Events(new WsServerMessage(
                EventMessageType(command, result),
                roomId,
                normalizedPlayerId,
                result.State.Tick,
                projectedEvents));
        }

        foreach (var (snapshotPlayerId, snapshot) in result.Snapshots)
        {
            await Clients.Group(PlayerGroup(roomId, snapshotPlayerId)).Snapshot(new WsServerMessage(
                MessageType.SNAPSHOT,
                roomId,
                snapshotPlayerId,
                snapshot.Tick,
                snapshot));
        }

        foreach (var (promptPlayerId, prompt) in result.Prompts)
        {
            await Clients.Group(PlayerGroup(roomId, promptPlayerId)).Prompt(new WsServerMessage(
                MessageType.PROMPT,
                roomId,
                promptPlayerId,
                result.State.Tick,
                prompt));
        }

        return CommandReceipt(
            roomId,
            normalizedPlayerId,
            clientIntentId,
            command,
            rawCommand,
            accepted: true,
            serverTick: result.State.Tick,
            state: "ACCEPTED",
            message: "服务端已接受命令，后续以快照和规则事件为准。",
            followup: CommandReceiptFollowups.Create(
                accepted: true,
                serverTick: result.State.Tick,
                eventCount: projectedEvents.Count,
                snapshotCount: result.Snapshots.Count,
                promptCount: result.Prompts.Count,
                receiptState: "ACCEPTED",
                eventKinds: projectedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
                eventRefs: CommandReceiptFollowups.EventRefs(projectedEvents, result.State.Tick)));
    }

    private static MessageType EventMessageType(GameCommand command, ResolutionResult result)
    {
        if (command is not ReadyCommand)
        {
            return MessageType.EVENTS;
        }

        return result.Events.Any(gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal))
            ? MessageType.START
            : MessageType.READY;
    }

    private static IReadOnlyList<GameEvent> ProjectEvents(IReadOnlyList<GameEvent> events, MatchState state)
    {
        return GameEventObjectRefProjector.ProjectEvents(events, state);
    }

    // When the connection has authenticated as a handle, every room action must use that handle.
    // Unauthenticated connections are unaffected (legacy path) until identity is required end to end.
    private bool TryRejectIdentityMismatch(string roomId, string normalizedPlayerId, out Task rejection)
    {
        rejection = Task.CompletedTask;
        if (!Context.Items.TryGetValue(AuthenticatedHandleItemKey, out var bound) || bound is not string boundHandle)
        {
            return false;
        }

        if (string.Equals(boundHandle, PlayerIdentityService.NormalizeHandle(normalizedPlayerId), StringComparison.Ordinal))
        {
            return false;
        }

        rejection = Clients.Caller.Error(new WsServerMessage(
            MessageType.ERROR,
            roomId,
            normalizedPlayerId,
            0,
            new ErrorDto(ErrorCodes.IdentityMismatch, "已认证身份与请求的玩家不一致，已拒绝以他人身份操作。")));
        return true;
    }

    private static string RoomGroup(string roomId)
    {
        return $"room:{roomId}";
    }

    private static string PlayerGroup(string roomId, string playerId)
    {
        return $"room:{roomId}:player:{playerId}";
    }

    private static string MatchmakingPlayerGroup(string playerId)
    {
        return $"matchmaking:player:{playerId}";
    }

    private bool TryRejectUnauthenticatedOrMismatchedIdentity(
        string normalizedPlayerId,
        out MatchmakingStatusDto rejected,
        out Task rejection)
    {
        rejection = Task.CompletedTask;
        rejected = new MatchmakingStatusDto(MatchmakingStates.Rejected, normalizedPlayerId);

        if (!Context.Items.TryGetValue(AuthenticatedHandleItemKey, out var bound) || bound is not string boundHandle)
        {
            rejected = RejectedMatchmakingStatus(
                normalizedPlayerId,
                ErrorCodes.AuthenticationRequired,
                "快速匹配需要先完成身份认证。");
            rejection = SendMatchmakingError(normalizedPlayerId, rejected);
            return true;
        }

        if (string.Equals(boundHandle, normalizedPlayerId, StringComparison.Ordinal))
        {
            return false;
        }

        rejected = RejectedMatchmakingStatus(
            normalizedPlayerId,
            ErrorCodes.IdentityMismatch,
            "已认证身份与请求的玩家不一致，已拒绝以他人身份匹配。");
        rejection = SendMatchmakingError(normalizedPlayerId, rejected);
        return true;
    }

    private Task SendMatchmakingError(string playerId, MatchmakingStatusDto result)
    {
        return SendClientError(
            string.Empty,
            playerId,
            result.ErrorCode ?? ErrorCodes.UnsupportedCommand,
            result.Message ?? "匹配请求被拒绝。");
    }

    private bool TryRejectPublicMatchIdentity(string normalizedPlayerId, out Task rejection)
    {
        rejection = Task.CompletedTask;
        if (!Context.Items.TryGetValue(AuthenticatedHandleItemKey, out var bound) || bound is not string boundHandle)
        {
            rejection = SendClientError(
                string.Empty,
                normalizedPlayerId,
                ErrorCodes.AuthenticationRequired,
                "创建公开对局需要先完成身份认证。");
            return true;
        }

        if (string.Equals(boundHandle, normalizedPlayerId, StringComparison.Ordinal))
        {
            return false;
        }

        rejection = SendClientError(
            string.Empty,
            normalizedPlayerId,
            ErrorCodes.IdentityMismatch,
            "已认证身份与请求的玩家不一致，已拒绝以他人身份创建公开对局。");
        return true;
    }

    private Task SendClientError(string roomId, string playerId, string code, string message)
    {
        return Clients.Caller.Error(new WsServerMessage(
            MessageType.ERROR,
            roomId,
            playerId,
            0,
            new ErrorDto(code, message)));
    }

    private static MatchmakingStatusDto RejectedMatchmakingStatus(
        string playerId,
        string errorCode,
        string message)
    {
        return new MatchmakingStatusDto(
            MatchmakingStates.Rejected,
            playerId,
            ErrorCode: errorCode,
            Message: message);
    }

    private static MatchmakingStatusDto MatchmakingStatusFor(
        MatchmakingQueueResult result,
        string playerId)
    {
        var playerSession = string.Equals(result.PlayerId, playerId, StringComparison.Ordinal)
            ? result.PlayerSession
            : result.OpponentSession;
        var opponentPlayerId = string.Equals(result.PlayerId, playerId, StringComparison.Ordinal)
            ? result.OpponentPlayerId
            : result.PlayerId;

        return new MatchmakingStatusDto(
            result.State,
            playerId,
            result.RoomId,
            string.Equals(result.State, MatchmakingStates.Matched, StringComparison.Ordinal)
                ? opponentPlayerId
                : null,
            playerSession);
    }

    private Task SendError(string roomId, string playerId, long serverTick, Exception ex)
    {
        var code = ex is MatchSessionException matchSessionException
            ? matchSessionException.Code
            : ErrorCodes.UnsupportedCommand;
        return Clients.Caller.Error(new WsServerMessage(
            MessageType.ERROR,
            roomId,
            playerId,
            serverTick,
            new ErrorDto(code, ex.Message)));
    }

    private static CommandReceiptDto CommandReceipt(
        string roomId,
        string playerId,
        string clientIntentId,
        GameCommand command,
        JsonElement rawCommand,
        bool accepted,
        long serverTick,
        string state,
        string message,
        string? errorCode = null,
        CommandReceiptFollowupDto? followup = null)
    {
        return new CommandReceiptDto(
            roomId,
            playerId,
            clientIntentId,
            command.CmdType,
            accepted,
            serverTick,
            state,
            message,
            errorCode,
            PromptIdFromRawCommand(rawCommand),
            SnapshotTickFromRawCommand(rawCommand),
            followup);
    }

    private static string ErrorCodeFor(Exception ex)
    {
        return ex is MatchSessionException matchSessionException
            ? matchSessionException.Code
            : ErrorCodes.UnsupportedCommand;
    }

    private static string? PromptIdFromRawCommand(JsonElement rawCommand)
    {
        return rawCommand.ValueKind == JsonValueKind.Object
            && rawCommand.TryGetProperty("promptId", out var promptId)
            && promptId.ValueKind == JsonValueKind.String
            ? promptId.GetString()
            : null;
    }

    private static long? SnapshotTickFromRawCommand(JsonElement rawCommand)
    {
        if (rawCommand.ValueKind != JsonValueKind.Object
            || !rawCommand.TryGetProperty("snapshotTick", out var snapshotTick))
        {
            return null;
        }

        return snapshotTick.ValueKind == JsonValueKind.Number && snapshotTick.TryGetInt64(out var value)
            ? value
            : null;
    }
}
