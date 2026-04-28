using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.Api.Hubs;

public interface IGameClient
{
    Task Snapshot(WsServerMessage message);

    Task Prompt(WsServerMessage message);

    Task Events(WsServerMessage message);

    Task Error(WsServerMessage message);
}

public sealed class GameHub(IMatchSessionRegistry sessions) : Hub<IGameClient>
{
    public async Task JoinRoom(string roomId, string playerId, string? reconnectToken = null)
    {
        var session = sessions.GetOrCreate(roomId);
        session.EnsurePlayer(playerId);

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(roomId, playerId));

        await Clients.Caller.Snapshot(new WsServerMessage(
            MessageType.SNAPSHOT,
            roomId,
            playerId,
            session.SnapshotFor(playerId).Tick,
            session.SnapshotFor(playerId)));

        await Clients.Caller.Prompt(new WsServerMessage(
            MessageType.PROMPT,
            roomId,
            playerId,
            session.SnapshotFor(playerId).Tick,
            session.PromptFor(playerId)));
    }

    public Task Pass(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(roomId, playerId, clientIntentId, new PassCommand());
    }

    public Task EndTurn(string roomId, string playerId, string clientIntentId)
    {
        return SubmitCommand(roomId, playerId, clientIntentId, new EndTurnCommand());
    }

    public Task SubmitIntent(string roomId, string playerId, string clientIntentId, JsonElement cmd)
    {
        return SubmitCommand(roomId, playerId, clientIntentId, GameCommandJsonMapper.Map(cmd));
    }

    private async Task SubmitCommand(string roomId, string playerId, string clientIntentId, GameCommand command)
    {
        var session = sessions.GetOrCreate(roomId);
        var result = await session.SubmitAsync(
            playerId,
            clientIntentId,
            command,
            Context.ConnectionAborted);

        if (!result.Accepted)
        {
            await Clients.Caller.Error(new WsServerMessage(
                MessageType.ERROR,
                roomId,
                playerId,
                result.State.Tick,
                result.ErrorMessage));
            return;
        }

        await Clients.Group(RoomGroup(roomId)).Events(new WsServerMessage(
            MessageType.EVENTS,
            roomId,
            playerId,
            result.State.Tick,
            result.Events));

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

    private static string RoomGroup(string roomId)
    {
        return $"room:{roomId}";
    }

    private static string PlayerGroup(string roomId, string playerId)
    {
        return $"room:{roomId}:player:{playerId}";
    }
}
