using System.Text.Json;
using System.Text.Json.Serialization;

namespace Riftbound.Contracts;

public static class ProtocolDefaults
{
    public const int ProtocolVersion = 1;
    public const int SchemaVersion = 1;
}

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
    JsonElement? Cmd = null,
    int ProtocolVersion = ProtocolDefaults.ProtocolVersion,
    int SchemaVersion = ProtocolDefaults.SchemaVersion);

public sealed record WsServerMessage(
    MessageType Type,
    string RoomId,
    string PlayerId,
    long ServerTick,
    object? Payload,
    int ProtocolVersion = ProtocolDefaults.ProtocolVersion,
    int SchemaVersion = ProtocolDefaults.SchemaVersion);

public static class ErrorCodes
{
    public const string PlayerIdRequired = "PLAYER_ID_REQUIRED";
    public const string PlayerNotInRoom = "PLAYER_NOT_IN_ROOM";
    public const string RoomFull = "ROOM_FULL";
    public const string InvalidReconnectToken = "INVALID_RECONNECT_TOKEN";
    public const string ClientIntentIdRequired = "CLIENT_INTENT_ID_REQUIRED";
    public const string ClientIntentConflict = "CLIENT_INTENT_CONFLICT";
    public const string MatchNotStarted = "MATCH_NOT_STARTED";
    public const string MatchFinished = "MATCH_FINISHED";
    public const string UnsupportedCommand = "UNSUPPORTED_COMMAND";
    public const string PhaseNotAllowed = "PHASE_NOT_ALLOWED";
    public const string InsufficientCost = "INSUFFICIENT_COST";
    public const string InvalidTarget = "INVALID_TARGET";
    public const string CardNotInHand = "CARD_NOT_IN_HAND";
    public const string InvalidDeck = "INVALID_DECK";
    public const string UnsupportedCardBehavior = "UNSUPPORTED_CARD_BEHAVIOR";
    public const string RecoveryInconsistent = "RECOVERY_INCONSISTENT";
}

public sealed record ErrorDto(
    string Code,
    string Message);

public sealed record PlayerSessionDto(
    string PlayerId,
    string Seat,
    string ReconnectToken);

public abstract record GameCommand(string CmdType);

public sealed record ReadyCommand() : GameCommand("READY");

public sealed record SubmitDeckCommand(
    string LegendCardNo,
    string ChampionCardNo,
    IReadOnlyList<string> MainDeck,
    IReadOnlyList<string> RuneDeck,
    IReadOnlyList<string> Battlefields) : GameCommand("SUBMIT_DECK");

public sealed record MulliganCommand(
    IReadOnlyList<string> HandObjectIds) : GameCommand("MULLIGAN");

public sealed record PassPriorityCommand() : GameCommand("PASS_PRIORITY");

public sealed record PassFocusCommand() : GameCommand("PASS_FOCUS");

public sealed record PassCommand() : GameCommand("PASS");

public sealed record EndTurnCommand() : GameCommand("END_TURN");

public sealed record PlayCardCommand(
    string SourceObjectId,
    string CardNo,
    IReadOnlyList<string> TargetObjectIds,
    string Mode = "",
    IReadOnlyList<string>? OptionalCosts = null,
    string Destination = "") : GameCommand("PLAY_CARD");

public sealed record ActivateAbilityCommand(
    string SourceObjectId,
    string AbilityId,
    IReadOnlyList<string> TargetObjectIds,
    IReadOnlyList<string>? OptionalCosts = null) : GameCommand("ACTIVATE_ABILITY");

public sealed record LegendActCommand(
    string SourceObjectId,
    string AbilityId,
    IReadOnlyList<string> TargetObjectIds,
    IReadOnlyList<string>? OptionalCosts = null) : GameCommand("LEGEND_ACT");

public sealed record HideCardCommand(
    string SourceObjectId,
    string CardNo,
    string Destination = "",
    IReadOnlyList<string>? OptionalCosts = null) : GameCommand("HIDE_CARD");

public sealed record RevealCardCommand(
    string SourceObjectId,
    string CardNo,
    IReadOnlyList<string> TargetObjectIds,
    string Mode = "",
    IReadOnlyList<string>? OptionalCosts = null,
    string Destination = "") : GameCommand("REVEAL_CARD");

public sealed record MoveUnitCommand(
    string SourceObjectId,
    string Origin = "",
    string Destination = "",
    IReadOnlyList<string>? OptionalCosts = null) : GameCommand("MOVE_UNIT");

public sealed record AssembleEquipmentCommand(
    string SourceObjectId,
    string TargetObjectId = "",
    IReadOnlyList<string>? OptionalCosts = null) : GameCommand("ASSEMBLE_EQUIPMENT");

public sealed record DeclareBattleCommand(
    string BattlefieldId = "",
    IReadOnlyList<string>? AttackerObjectIds = null,
    IReadOnlyList<string>? DefenderObjectIds = null,
    IReadOnlyList<string>? OptionalCosts = null,
    IReadOnlyList<string>? BattlefieldTargetObjectIds = null) : GameCommand("DECLARE_BATTLE");

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
    IReadOnlyList<string> Actions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PromptId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? SnapshotTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ActionPromptCandidateDto>? Candidates = null);

public sealed record ActionPromptCandidateDto(
    string Action,
    string Label,
    bool Enabled,
    string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ActionPromptChoiceDto>? Sources = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ActionPromptChoiceDto>? Targets = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ActionPromptChoiceDto>? Destinations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ActionPromptChoiceDto>? Modes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ActionPromptChoiceDto>? OptionalCosts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record ActionPromptChoiceDto(
    string Id,
    string Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null);

public sealed record ClientIntentDto(
    string RoomId,
    string PlayerId,
    string ClientIntentId,
    JsonElement Cmd);
