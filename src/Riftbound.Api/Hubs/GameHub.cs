using System.Collections;
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
}

public sealed class GameHub(
    IMatchSessionRegistry sessions,
    IHostEnvironment? hostEnvironment = null,
    IConfiguration? configuration = null) : Hub<IGameClient>
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
            var events = ProjectEvents(result.Events, result.State);
            await Clients.Group(RoomGroup(roomId)).Events(new WsServerMessage(
                EventMessageType(command, result),
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

    private static readonly IReadOnlyDictionary<string, string> SingularEventObjectRefRoles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["attachedToObjectId"] = "贴附",
        ["attackerObjectId"] = "攻击",
        ["battlefieldId"] = "战场",
        ["battlefieldObjectId"] = "战场",
        ["cardObjectId"] = "卡牌",
        ["defenderObjectId"] = "防守",
        ["destroyedObjectId"] = "被摧毁",
        ["equipmentObjectId"] = "装备",
        ["hostObjectId"] = "贴附",
        ["objectId"] = "对象",
        ["runeObjectId"] = "符文",
        ["sourceObjectId"] = "来源",
        ["targetObjectId"] = "目标",
        ["unitObjectId"] = "单位"
    };

    private static readonly IReadOnlyDictionary<string, string> ArrayEventObjectRefRoles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["attackerObjectIds"] = "攻击",
        ["banishedObjectIds"] = "放逐",
        ["cardObjectIds"] = "卡牌",
        ["chosenObjectIds"] = "已选",
        ["defenderObjectIds"] = "防守",
        ["destroyedObjectIds"] = "被摧毁",
        ["discardedObjectIds"] = "弃置",
        ["exhaustedObjectIds"] = "横置",
        ["objectIds"] = "对象",
        ["participantObjectIds"] = "参与",
        ["paymentObjectIds"] = "费用",
        ["readyObjectIds"] = "重置",
        ["revealedObjectIds"] = "展示",
        ["runeObjectIds"] = "符文",
        ["sourceObjectIds"] = "来源",
        ["targetObjectIds"] = "目标",
        ["unitObjectIds"] = "单位"
    };

    private static IReadOnlyList<GameEvent> ProjectEvents(IReadOnlyList<GameEvent> events, MatchState state)
    {
        return events
            .Select(gameEvent => gameEvent.ObjectRefs is { Count: > 0 }
                ? gameEvent
                : gameEvent with { ObjectRefs = BuildEventObjectRefs(gameEvent.Payload, state) })
            .ToArray();
    }

    private static IReadOnlyList<GameEventObjectRef>? BuildEventObjectRefs(
        IReadOnlyDictionary<string, object?> payload,
        MatchState state)
    {
        var rawRefs = new List<(string Role, string ObjectId)>();
        CollectEventObjectRefs(payload, rawRefs, 0);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var refs = new List<GameEventObjectRef>();
        foreach (var (role, objectId) in rawRefs)
        {
            var normalizedObjectId = NormalizeOptionalText(objectId);
            if (normalizedObjectId is null || !seen.Add(normalizedObjectId))
            {
                continue;
            }

            refs.Add(BuildEventObjectRef(normalizedObjectId, role, state));
        }

        return refs.Count > 0 ? refs : null;
    }

    private static void CollectEventObjectRefs(
        IReadOnlyDictionary<string, object?> record,
        List<(string Role, string ObjectId)> refs,
        int depth)
    {
        if (depth > 2)
        {
            return;
        }

        foreach (var (key, value) in record)
        {
            if (SingularEventObjectRefRoles.TryGetValue(key, out var singularRole)
                && TryReadString(value, out var objectId))
            {
                refs.Add((singularRole, objectId));
                continue;
            }

            if (ArrayEventObjectRefRoles.TryGetValue(key, out var arrayRole))
            {
                refs.AddRange(ReadStringList(value).Select(objectId => (arrayRole, objectId)));
                continue;
            }

            foreach (var nested in ReadNestedRecords(value))
            {
                CollectEventObjectRefs(nested, refs, depth + 1);
            }
        }
    }

    private static GameEventObjectRef BuildEventObjectRef(string objectId, string role, MatchState state)
    {
        if (string.Equals(objectId, "HIDDEN", StringComparison.Ordinal))
        {
            return new GameEventObjectRef(objectId, role, IsHidden: true);
        }

        if (!state.CardObjects.TryGetValue(objectId, out var cardObject))
        {
            return new GameEventObjectRef(objectId, role);
        }

        state.ObjectLocations.TryGetValue(objectId, out var location);
        return new GameEventObjectRef(
            objectId,
            role,
            cardObject.IsFaceDown ? null : cardObject.CardNo,
            cardObject.OwnerId,
            cardObject.ControllerId,
            location?.Zone,
            location?.BattlefieldObjectId,
            cardObject.IsFaceDown);
    }

    private static bool TryReadString(object? value, out string text)
    {
        switch (value)
        {
            case string stringValue when !string.IsNullOrWhiteSpace(stringValue):
                text = stringValue.Trim();
                return true;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                var elementText = element.GetString();
                if (!string.IsNullOrWhiteSpace(elementText))
                {
                    text = elementText.Trim();
                    return true;
                }

                break;
        }

        text = string.Empty;
        return false;
    }

    private static IEnumerable<string> ReadStringList(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (TryReadString(item, out var text))
                {
                    yield return text;
                }
            }

            yield break;
        }

        if (value is string)
        {
            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryReadString(item, out var text))
                {
                    yield return text;
                }
            }
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> ReadNestedRecords(object? value)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object?> typed:
                yield return typed;
                yield break;
            case JsonElement { ValueKind: JsonValueKind.Object } jsonObject:
            {
                var record = jsonObject
                    .EnumerateObject()
                    .ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal);
                yield return record;
                break;
            }
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
