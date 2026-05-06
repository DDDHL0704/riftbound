using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
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
}

public sealed class GameHub(IMatchSessionRegistry sessions, IHostEnvironment? hostEnvironment = null) : Hub<IGameClient>
{
    public async Task JoinRoom(string roomId, string playerId, string? reconnectToken = null)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
        IMatchSession session;
        PlayerSessionDto playerSession;
        try
        {
            session = await sessions.GetOrCreateAsync(roomId, Context.ConnectionAborted);
            playerSession = await session.EnsurePlayerAsync(normalizedPlayerId, Context.ConnectionAborted);
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

    public Task Pass(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(
            roomId,
            playerId,
            clientIntentId,
            new PassCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "PASS" }));
    }

    public Task EndTurn(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(
            roomId,
            playerId,
            clientIntentId,
            new EndTurnCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "END_TURN" }));
    }

    public Task Ready(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(
            roomId,
            playerId,
            clientIntentId,
            new ReadyCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "READY" }));
    }

    public Task SubmitIntent(string roomId, string playerId, string clientIntentId, JsonElement cmd)
    {
        return SubmitCommand(roomId, playerId, clientIntentId, GameCommandJsonMapper.Map(cmd), cmd.Clone());
    }

    public async Task SeedScenario(string roomId, string playerId, string scenarioId, string clientIntentId)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
        if (hostEnvironment is not null && !hostEnvironment.IsDevelopment())
        {
            await Clients.Caller.Error(new WsServerMessage(
                MessageType.ERROR,
                roomId,
                normalizedPlayerId,
                0,
                new ErrorDto(ErrorCodes.UnsupportedCommand, "SeedScenario is only available in Development.")));
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
            await Clients.Group(RoomGroup(roomId)).Events(new WsServerMessage(
                MessageType.EVENTS,
                roomId,
                normalizedPlayerId,
                result.State.Tick,
                result.Events));
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

    private async Task SubmitCommand(
        string roomId,
        string playerId,
        string clientIntentId,
        GameCommand command,
        JsonElement rawCommand)
    {
        var normalizedPlayerId = playerId?.Trim() ?? string.Empty;
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
            return;
        }

        if (!result.Accepted)
        {
            await Clients.Caller.Error(new WsServerMessage(
                MessageType.ERROR,
                roomId,
                normalizedPlayerId,
                result.State.Tick,
                new ErrorDto(
                    result.ErrorCode ?? ErrorCodes.UnsupportedCommand,
                    result.ErrorMessage ?? "command rejected")));
            return;
        }

        if (result.Events.Count > 0)
        {
            await Clients.Group(RoomGroup(roomId)).Events(new WsServerMessage(
                EventMessageType(command, result),
                roomId,
                normalizedPlayerId,
                result.State.Tick,
                result.Events));
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

    private static string RoomGroup(string roomId)
    {
        return $"room:{roomId}";
    }

    private static string PlayerGroup(string roomId, string playerId)
    {
        return $"room:{roomId}:player:{playerId}";
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
}
