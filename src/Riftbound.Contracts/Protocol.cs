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
    public const string InvalidPayload = "INVALID_PAYLOAD";
    public const string CardNotInHand = "CARD_NOT_IN_HAND";
    public const string InvalidDeck = "INVALID_DECK";
    public const string UnsupportedCardBehavior = "UNSUPPORTED_CARD_BEHAVIOR";
    public const string RecoveryInconsistent = "RECOVERY_INCONSISTENT";
    public const string PromptExpired = "PROMPT_EXPIRED";
}

public static class CommandTypes
{
    public const string Ready = "READY";
    public const string SubmitDeck = "SUBMIT_DECK";
    public const string Mulligan = "MULLIGAN";
    public const string PassPriority = "PASS_PRIORITY";
    public const string PassFocus = "PASS_FOCUS";
    public const string Pass = "PASS";
    public const string EndTurn = "END_TURN";
    public const string Surrender = "SURRENDER";
    public const string PlayCard = "PLAY_CARD";
    public const string ActivateAbility = "ACTIVATE_ABILITY";
    public const string LegendAct = "LEGEND_ACT";
    public const string HideCard = "HIDE_CARD";
    public const string TapRune = "TAP_RUNE";
    public const string RecycleRune = "RECYCLE_RUNE";
    public const string RevealCard = "REVEAL_CARD";
    public const string MoveUnit = "MOVE_UNIT";
    public const string AssembleEquipment = "ASSEMBLE_EQUIPMENT";
    public const string DeclareBattle = "DECLARE_BATTLE";
    public const string PayCost = "PAY_COST";
    public const string AssignCombatDamage = "ASSIGN_COMBAT_DAMAGE";
    public const string OrderTriggers = "ORDER_TRIGGERS";
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

public sealed record SurrenderCommand() : GameCommand("SURRENDER");

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

public sealed record TapRuneCommand(
    string SourceObjectId) : GameCommand("TAP_RUNE");

public sealed record RecycleRuneCommand(
    string SourceObjectId) : GameCommand("RECYCLE_RUNE");

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

public sealed record PayCostCommand(
    string PaymentId = "",
    string PaymentWindow = "",
    IReadOnlyList<string>? PaymentChoiceIds = null) : GameCommand(CommandTypes.PayCost);

public sealed record CombatDamageAssignmentDto(
    string SourceObjectId,
    string TargetObjectId,
    int Damage);

public sealed record AssignCombatDamageCommand(
    string BattleId = "",
    string BattlefieldId = "",
    IReadOnlyList<CombatDamageAssignmentDto>? Assignments = null) : GameCommand(CommandTypes.AssignCombatDamage);

public sealed record OrderTriggersCommand(
    IReadOnlyList<string>? TriggerIds = null,
    IReadOnlyList<string>? OrderedTriggerIds = null) : GameCommand(CommandTypes.OrderTriggers);

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

public static class PromptTypes
{
    public const string RoomSetup = "ROOM_SETUP";
    public const string Mulligan = "MULLIGAN";
    public const string MainAction = "MAIN_ACTION";
    public const string StackPriority = "STACK_PRIORITY";
    public const string SpellDuelFocus = "SPELL_DUEL_FOCUS";
    public const string SpellDuelAction = "SPELL_DUEL_ACTION";
    public const string BattleDeclaration = "BATTLE_DECLARATION";
    public const string AssignCombatDamage = "ASSIGN_COMBAT_DAMAGE";
    public const string PayCost = "PAY_COST";
    public const string OrderTriggers = "ORDER_TRIGGERS";
    public const string TaskQueue = "TASK_QUEUE";
    public const string Wait = "WAIT";
    public const string MatchResult = "MATCH_RESULT";
}

public sealed record ActionPromptContractDto(
    string PromptKind,
    string CandidateAction,
    IReadOnlyList<string> RequiredPayload,
    IReadOnlyList<string> LegalChoices,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> VisibleMetadata,
    IReadOnlyList<string> HiddenMetadata);

public static class ActionPromptContracts
{
    public static ActionPromptContractDto PayCost { get; } = new(
        PromptTypes.PayCost,
        CommandTypes.PayCost,
        ["paymentId", "paymentWindow", "paymentChoiceIds"],
        ["candidate.metadata.paymentChoices", "candidate.metadata.paymentResourceChoices"],
        [ErrorCodes.InvalidPayload, ErrorCodes.PhaseNotAllowed, ErrorCodes.InsufficientCost],
        ["paymentId", "paymentWindow", "cost", "paymentChoices", "paymentResourceChoices"],
        ["serverPaymentState", "resourceLedgerBeforePayment"]);

    public static ActionPromptContractDto AssignCombatDamage { get; } = new(
        PromptTypes.AssignCombatDamage,
        CommandTypes.AssignCombatDamage,
        ["battleId", "battlefieldId", "assignments[].sourceObjectId", "assignments[].targetObjectId", "assignments[].damage"],
        ["candidate.metadata.assignmentChoices", "candidate.metadata.legalTargets", "candidate.metadata.battleParticipants"],
        [ErrorCodes.InvalidPayload, ErrorCodes.PhaseNotAllowed, ErrorCodes.InvalidTarget],
        ["battleId", "battlefieldId", "assigningPlayerId", "damagePool", "legalTargets", "existingDamage", "lethalDamageThreshold", "requiredAssignments", "assignmentChoices", "battleParticipants"],
        ["battleState", "participantControllerIds", "damageLedger"]);

    public static ActionPromptContractDto OrderTriggers { get; } = new(
        PromptTypes.OrderTriggers,
        CommandTypes.OrderTriggers,
        ["orderedTriggerIds"],
        ["candidate.metadata.triggerChoices", "candidate.metadata.orderedTriggerIds"],
        [ErrorCodes.InvalidPayload, ErrorCodes.PhaseNotAllowed, ErrorCodes.InvalidTarget],
        ["orderingPlayerId", "triggerIds", "triggers", "triggerChoices", "legalOrderingConstraints", "triggeredByEventKind"],
        ["triggerQueue"]);

    public static IReadOnlyDictionary<string, ActionPromptContractDto> ByPromptKind { get; } =
        new Dictionary<string, ActionPromptContractDto>(StringComparer.Ordinal)
        {
            [PayCost.PromptKind] = PayCost,
            [AssignCombatDamage.PromptKind] = AssignCombatDamage,
            [OrderTriggers.PromptKind] = OrderTriggers
        };
}

public sealed record PromptViewDto(
    string Type,
    string Title,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RelatedBattlefieldId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RelatedStackItemId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RelatedBattleId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RelatedSpellDuelId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MinSelection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxSelection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record ActionPromptDto(
    string PlayerId,
    bool Actionable,
    string Reason,
    IReadOnlyList<string> Actions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PromptId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? SnapshotTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ActionPromptCandidateDto>? Candidates = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PromptViewDto? View = null);

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
