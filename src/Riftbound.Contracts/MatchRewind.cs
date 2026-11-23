namespace Riftbound.Contracts;

public abstract record RewindCommand(string CmdType) : GameCommand(CmdType);
public sealed record RequestRewindCommand(long TargetStep) : RewindCommand(CommandTypes.RequestRewind);

public abstract record AnswerRewindCommand(string CmdType, long RequestStep) : RewindCommand(CmdType);
public sealed record AcceptRewindCommand(long RequestStep) : AnswerRewindCommand(CommandTypes.AcceptRewind, RequestStep);
public sealed record RejectRewindCommand(long RequestStep) : AnswerRewindCommand(CommandTypes.RejectRewind, RequestStep);
public sealed record CancelRewindCommand(long RequestStep) : AnswerRewindCommand(CommandTypes.CancelRewind, RequestStep);

public sealed record RewindTargetDto(long Step, int GameNumber, int TurnNumber, string PlayerId, string Description);
public sealed record RewindRequestDto(long RequestStep, string RequesterPlayerId, long TargetStep);
public sealed record RewindCandidateDto(string Action,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    long? RequestStep = null);
