using System.Text.Json;

namespace Riftbound.Contracts;

public enum MessageType
{
    JOIN,
    RECONNECT,
    START,
    READY,
    ACT,
    REACT,
    PASS,
    END_TURN,
    PROMPT,
    SNAPSHOT,
    EVENTS,
    ERROR,
    PING,
    PONG
}

public sealed record WsClientMessage(
    MessageType Type,
    string RoomId,
    string PlayerId,
    string? ClientIntentId = null,
    string? ReconnectToken = null,
    JsonElement? Cmd = null);

public sealed record WsServerMessage(
    MessageType Type,
    string RoomId,
    string PlayerId,
    long ServerTick,
    object? Payload);

public static class ErrorCodes
{
    public const string PlayerIdRequired = "PLAYER_ID_REQUIRED";
    public const string PlayerNotInRoom = "PLAYER_NOT_IN_ROOM";
    public const string RoomFull = "ROOM_FULL";
    public const string InvalidReconnectToken = "INVALID_RECONNECT_TOKEN";
    public const string ClientIntentConflict = "CLIENT_INTENT_CONFLICT";
    public const string UnsupportedCommand = "UNSUPPORTED_COMMAND";
}

public sealed record ErrorDto(
    string Code,
    string Message);

public sealed record PlayerSessionDto(
    string PlayerId,
    string Seat,
    string ReconnectToken);

public abstract record GameCommand(string CmdType);

public sealed record PassPriorityCommand() : GameCommand("PASS_PRIORITY");

public sealed record PassFocusCommand() : GameCommand("PASS_FOCUS");

public sealed record PassCommand() : GameCommand("PASS");

public sealed record EndTurnCommand() : GameCommand("END_TURN");

public sealed record UnsupportedCommand(string RawCmdType, JsonElement? Payload = null)
    : GameCommand(RawCmdType);

public sealed record PlayerIntent(
    string IntentId,
    string PlayerId,
    string CommandType);

public sealed record GameEvent(
    string Kind,
    string Description,
    IReadOnlyDictionary<string, object?> Payload);

public sealed record SnapshotDto(
    long Tick,
    int TurnNumber,
    string ActivePlayerId,
    IReadOnlyDictionary<string, object?> Players,
    IReadOnlyDictionary<string, object?> Lanes,
    IReadOnlyList<object?> Stack,
    IReadOnlyDictionary<string, object?> Timing,
    string TurnState);

public sealed record ActionPromptDto(
    string PlayerId,
    bool Actionable,
    string Reason,
    IReadOnlyList<string> Actions);

public sealed record ClientIntentDto(
    string RoomId,
    string PlayerId,
    string ClientIntentId,
    JsonElement Cmd);
