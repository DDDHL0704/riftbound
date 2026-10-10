using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record RuleChoiceOption(string Id, string Label, IReadOnlyList<string>? ObjectIds = null);
public sealed record RuleChoiceRequest(string Id, string PlayerId, string Reason, IReadOnlyList<RuleChoiceOption> Options, int PowerCost = 0);
public sealed record RuleChoiceAnswer(string RequestHash, string OptionId);
// Deterministic command checkpoint for synchronous decisions.
// Until all synchronous replacement decisions are known, the original action is uncommitted.
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
        public HashSet<string> DestructionCandidates { get; set; } = new(StringComparer.Ordinal);
        public List<TriggerQueueItemState> ResourceTriggers { get; } = [];
        public HashSet<string> ResourceDestroyedOwners { get; } = [];
        private int cursor;
        public string Choose(string player, string reason, IReadOnlyList<RuleChoiceOption> options, int powerCost = 0)
        {
            var request = new RuleChoiceRequest($"REPLACEMENT:{Origin.Tick}:{cursor}", player, reason, options, powerCost);
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
        var pending = state.PendingRuleChoice;
        if (pending is not null && command is SurrenderCommand)
            return await ResolveCoreAsync(state with { PendingRuleChoice = null }, intent, command, cancellationToken);
        var origin = pending?.Origin ?? state; var originalIntent = pending?.Intent ?? intent;
        var answers = pending?.Answers ?? [];
        if (pending is not null) {
            if (intent.PlayerId != pending.Request.PlayerId || command is not PayCostCommand choice
                || choice.PaymentId != pending.Request.Id || choice.PaymentWindow != RuleChoiceWindow
                || choice.PaymentChoiceIds is not { Count: 1 } selected || !pending.Request.Options.Any(o => o.Id == selected[0]))
                return RejectWithCorePrompts(state, "请由替换效果的控制者确认当前选择。", ErrorCodes.InvalidTarget);
            answers = answers.Append(new RuleChoiceAnswer(MatchStateHasher.HashValue(pending.Request), selected[0])).ToArray();
            command = GameCommandJsonMapper.Map(pending.Command);
        }
        var frame = new RuleChoiceFrame(origin, answers); var previous = RuleChoices.Value; RuleChoices.Value = frame;
        try {
            var result = await ResolveCoreAsync(origin, originalIntent, command, cancellationToken);
            if (!frame.Complete) return RejectWithCorePrompts(state, "替换选择记录未完整消费。", ErrorCodes.InvalidTarget);
            if (pending is null) return result;
            if (!result.Accepted) return RejectWithCorePrompts(state, result.ErrorMessage!, result.ErrorCode!);
            var next = result.State with { Tick = state.Tick + 1, PendingRuleChoice = null };
            return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
        }
        catch (RuleChoiceRequired required) {
            var raw = pending?.Command ?? JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var next = origin with { Tick = state.Tick + 1, PendingRuleChoice = new(origin, originalIntent, raw, answers, required.Request) };
            return new(true, null, next, [new("RULE_CHOICE_REQUESTED", required.Request.Reason,
                new Dictionary<string,object?> { ["playerId"] = required.Request.PlayerId, ["choiceId"] = required.Request.Id })],
                ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
        }
        finally { RuleChoices.Value = previous; }
    }

    internal static PendingPaymentState? VisiblePendingPayment(MatchState state)
        => state.PendingRuleChoice is { } pending ? RuleChoicePayment(pending) : state.PendingPayment;

    internal static PendingPaymentState RuleChoicePayment(RuleChoiceContinuation pending)
        => new(pending.Request.Id, RuleChoiceWindow, pending.Request.PlayerId, powerCost: pending.Request.PowerCost,
            legalPaymentChoiceIds: pending.Request.Options.Select(o => o.Id).ToArray(), reason: pending.Request.Reason);

    internal static IReadOnlyDictionary<string, ActionPromptDto> BuildRuleChoicePrompts(MatchState state)
    {
        var pending = state.PendingRuleChoice!;
        var projection = state with { PendingPayment = RuleChoicePayment(pending) };
        return state.Seats.Keys.ToDictionary(id => id, id => ActionPromptBuilder.Build(projection, id,
            id == pending.Request.PlayerId, id == pending.Request.PlayerId ? pending.Request.Reason : "等待对手选择摧毁替换",
            id == pending.Request.PlayerId ? [CommandTypes.PayCost, CommandTypes.Surrender] : ["WAIT", CommandTypes.Surrender]));
    }

    internal static bool ValidRuleChoice(MatchState state)
    {
        if (state.PendingRuleChoice is not { } p) return true;
        if (p.Origin is null || p.Origin.PendingRuleChoice is not null || p.Answers is null || p.Request is null
            || p.Intent is null || p.Request.Options is null || p.Request.Options.Any(o => o is null) || p.Answers.Any(a => a is null)
            || state.Tick <= p.Origin.Tick || !state.Seats.ContainsKey(p.Intent.PlayerId)
            || MatchStateHasher.Hash(state with { Tick = p.Origin.Tick, PendingRuleChoice = null }) != MatchStateHasher.Hash(p.Origin)) return false;
        var previous = RuleChoices.Value; RuleChoices.Value = new(p.Origin, p.Answers);
        try {
            _ = new CoreRuleEngine().ResolveCoreAsync(p.Origin, p.Intent, GameCommandJsonMapper.Map(p.Command), default).GetAwaiter().GetResult();
            return false;
        } catch (RuleChoiceRequired required) { return MatchStateHasher.HashValue(required.Request) == MatchStateHasher.HashValue(p.Request); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException) { return false; }
        finally { RuleChoices.Value = previous; }
    }
}
