using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record RuleChoiceOption(string Id, string Label, IReadOnlyList<string>? ObjectIds = null);
public sealed record RuleChoiceRequest(string Id, string PlayerId, string Reason, IReadOnlyList<RuleChoiceOption> Options, int PowerCost = 0, string Kind = "DESTRUCTION_REPLACEMENT", MatchState? Preview = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? PrivateReason = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int ManaCost = 0);
public sealed record RuleChoiceAnswer(string RequestHash, string OptionId);
// Deterministic command checkpoint for synchronous decisions.
// The outer action is uncommitted; completed resource children settle independently.
public sealed record RuleChoiceContinuation(MatchState Origin, PlayerIntent Intent, JsonElement Command,
    IReadOnlyList<RuleChoiceAnswer> Answers, RuleChoiceRequest Request);

public sealed partial class CoreRuleEngine
{
    internal const string RuleChoiceWindow = "RULE_REPLACEMENT";
    private static readonly AsyncLocal<RuleChoiceFrame?> RuleChoices = new();
    private sealed class RuleChoiceRequired(RuleChoiceRequest request) : Exception { public RuleChoiceRequest Request { get; } = request; }
    private sealed class RuleChoiceFrame(MatchState origin, IReadOnlyList<RuleChoiceAnswer> answers)
    {
        public MatchState Origin { get; } = origin;
        public long CompletionTick { get; set; } = origin.Tick + 1;
        public long AnswerTick => Origin.Tick + cursor + 1;
        public TurnDeathLedger? ResourceDeathLedger { get; set; }
        public HashSet<string> DestructionCandidates { get; set; } = new(StringComparer.Ordinal);
        public HashSet<string> CurrentDestructions { get; set; } = [];
        public HashSet<string> AppliedDestructionReplacements { get; set; } = new(StringComparer.Ordinal);
        public int DeathSequence { get; set; }
        public int DiscardSequence { get; set; }
        public int DestructionBatchSequence { get; set; }
        public int? CurrentDestructionBatch { get; set; }
        public LastKnownMightFrame? CurrentDestructionMightFrame { get; set; }
        public IReadOnlyDictionary<string, ObjectLocationState>? CurrentDestructionLocations { get; set; }
        public MatchState? ResolutionState { get; set; }
        public StackItemState? ResolutionItem { get; set; }
        public List<AppliedDamageFact> AppliedDamages { get; } = [];
        public List<TriggerQueueItemState> NestedTriggers { get; } = [];
        public List<ObservedDeckPlayContext> ObservedPlays {get;}=[];
        public HashSet<string> NestedDestroyedOwners { get; } = [];
        private int cursor;
        public string Choose(string player, string reason, IReadOnlyList<RuleChoiceOption> options, int powerCost = 0, string kind = "DESTRUCTION_REPLACEMENT", MatchState? preview = null, string? privateReason = null, int manaCost = 0)
        {
            var request = new RuleChoiceRequest($"REPLACEMENT:{Origin.Tick}:{cursor}", player, reason, options, powerCost, kind, preview, privateReason, manaCost);
            if (cursor == answers.Count) throw new RuleChoiceRequired(request);
            var answer = answers[cursor++];
            if (answer.RequestHash != MatchStateHasher.HashValue(request) || !options.Any(o => o.Id == answer.OptionId))
                throw new InvalidOperationException("Replacement decision transcript no longer matches its command.");
            return answer.OptionId;
        }
        public bool Complete => cursor == answers.Count;
    }

    public async ValueTask<ResolutionResult> ResolveAsync(MatchState state, PlayerIntent intent, GameCommand command, CancellationToken cancellationToken)
    {
        if (!RuleInputIdentity.IsCurrentState(state))
            return RejectWithCorePrompts(state, "对局规则输入身份与当前规则、目录或实现不匹配。", ErrorCodes.RecoveryInconsistent);
        var pending = state.PendingRuleChoice;
        if (!TryGetCommittedRuleChoiceResources(state, out var committedResources))
            return RejectWithCorePrompts(state, "结算选择与已提交资源记录不一致。", ErrorCodes.RecoveryInconsistent);
        if (pending is not null && command is SurrenderCommand)
            return await ResolveCoreAsync(state with { PendingRuleChoice = null }, intent, command, cancellationToken);
        var origin = pending?.Origin ?? state; var originalIntent = pending?.Intent ?? intent;
        var answers = pending?.Answers ?? [];
        if (pending is not null) {
            if (intent.PlayerId != pending.Request.PlayerId || command is not PayCostCommand choice
                || choice.PaymentId != pending.Request.Id || choice.PaymentWindow != RuleChoiceWindow
                || choice.PaymentChoiceIds is not { Count: 1 } selected || !pending.Request.Options.Any(o => o.Id == selected[0]))
                return RejectWithCorePrompts(state, "请由当前结算选择的玩家确认。", ErrorCodes.InvalidTarget);
            answers = answers.Append(new RuleChoiceAnswer(MatchStateHasher.HashValue(pending.Request), selected[0])).ToArray();
            command = GameCommandJsonMapper.Map(pending.Command);
        }
        var frame = new RuleChoiceFrame(origin, answers) { CompletionTick = state.Tick + 1 }; var previous = RuleChoices.Value; RuleChoices.Value = frame;
        try {
            var result = await ResolveCoreAsync(origin, originalIntent, command, cancellationToken);
            if (!frame.Complete) return RejectWithCorePrompts(state, "替换选择记录未完整消费。", ErrorCodes.InvalidTarget);
            if (pending is null) return result;
            if (!result.Accepted) return RejectWithCorePrompts(state, result.ErrorMessage!, result.ErrorCode!);
            var next = result.State with { Tick = state.Tick + 1, PendingRuleChoice = null };
            return result with { State = next, Events = UncommittedRuleChoiceEvents(result.Events, committedResources),
                Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
        }
        catch (RuleChoiceRequired required) {
            var raw = pending?.Command ?? CaptureRuleChoiceCommand(command);
            var resources = GetCompletedRuleChoiceResources(frame);
            var next = CommitRuleChoiceResources(origin, resources) with {
                Tick = state.Tick + 1, PendingRuleChoice = new(origin, originalIntent, raw, answers, required.Request) };
            var events = UncommittedRuleChoiceEvents(resources.SelectMany(step => step.Events).ToArray(), committedResources)
                .Append(new GameEvent("RULE_CHOICE_REQUESTED", required.Request.Reason,
                    new Dictionary<string,object?> { ["playerId"] = required.Request.PlayerId, ["choiceId"] = required.Request.Id })).ToArray();
            return new(true, null, next, events,
                ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
        }
        finally { RuleChoices.Value = previous; }
    }

    private static JsonElement CaptureRuleChoiceCommand(GameCommand command)
    {
        // Continuations replay through the wire mapper. Capture that same form
        // so equivalent typed nulls and wire defaults produce one checkpoint.
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var serialized = JsonSerializer.SerializeToElement(command, command.GetType(), json);
        var replayed = GameCommandJsonMapper.Map(serialized);
        return JsonSerializer.SerializeToElement(replayed, replayed.GetType(), json);
    }

    // Intermediate effects are displayed through the normal per-viewer privacy
    // projection. The original action remains the deterministic recovery checkpoint.
    internal static MatchState VisibleRuleChoiceState(MatchState state)
        => state.PendingRuleChoice is { Request.Preview: { } preview } pending
            ? preview with { Tick = state.Tick, PendingRuleChoice = pending } : state;

    internal static PendingPaymentState? VisiblePendingPayment(MatchState state)
        => state.PendingRuleChoice is { } pending ? RuleChoicePayment(pending) : state.PendingPayment;

    internal static PendingPaymentState RuleChoicePayment(RuleChoiceContinuation pending)
        => new(pending.Request.Id, RuleChoiceWindow, pending.Request.PlayerId, manaCost: pending.Request.ManaCost, powerCost: pending.Request.PowerCost,
            legalPaymentChoiceIds: pending.Request.Options.Select(o => o.Id).ToArray(), reason: pending.Request.Reason);

    internal static IReadOnlyDictionary<string, ActionPromptDto> BuildRuleChoicePrompts(MatchState state)
    {
        var pending = state.PendingRuleChoice!;
        var projection = VisibleRuleChoiceState(state) with { PendingPayment = RuleChoicePayment(pending) };
        return state.Seats.Keys.ToDictionary(id => id, id => ActionPromptBuilder.Build(projection, id,
            id == pending.Request.PlayerId, id == pending.Request.PlayerId ? pending.Request.PrivateReason ?? pending.Request.Reason : "等待对手完成结算选择",
            id == pending.Request.PlayerId ? [CommandTypes.PayCost, CommandTypes.Surrender] : ["WAIT", CommandTypes.Surrender]));
    }

    internal static bool ValidRuleChoice(MatchState state)
        => TryGetCommittedRuleChoiceResources(state, out _);

    private static bool TryGetCommittedRuleChoiceResources(MatchState state, out IReadOnlyList<RuleChoiceResourceStep> resources)
    {
        resources = [];
        if (state.PendingRuleChoice is not { } p) return true;
        if (p.Origin is null || p.Origin.PendingRuleChoice is not null || p.Answers is null || p.Request is null
            || p.Intent is null || p.Request.Options is null || p.Request.Options.Any(o => o is null) || p.Answers.Any(a => a is null)
            || p.Command.ValueKind != JsonValueKind.Object
            || state.Tick <= p.Origin.Tick || !state.Seats.ContainsKey(p.Intent.PlayerId)) return false;
        var reproduced = ReproduceRuleChoice(p);
        if (reproduced is not { } current
            || MatchStateHasher.HashValue(current.Request) != MatchStateHasher.HashValue(p.Request)) return false;
        resources = GetCompletedRuleChoiceResources(current.Frame);
        var expected = CommitRuleChoiceResources(p.Origin, resources);
        return MatchStateHasher.Hash(state with { Tick = p.Origin.Tick, PendingRuleChoice = null }) == MatchStateHasher.Hash(expected);
    }

    private static (RuleChoiceRequest Request, RuleChoiceFrame Frame)? ReproduceRuleChoice(RuleChoiceContinuation pending)
    {
        var frame = new RuleChoiceFrame(pending.Origin, pending.Answers);
        var previous = RuleChoices.Value; RuleChoices.Value = frame;
        try {
            _ = new CoreRuleEngine().ResolveCoreAsync(pending.Origin, pending.Intent,
                GameCommandJsonMapper.Map(pending.Command), default).GetAwaiter().GetResult();
            return null;
        } catch (RuleChoiceRequired required) { return (required.Request, frame); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException) { return null; }
        finally { RuleChoices.Value = previous; }
    }
}
