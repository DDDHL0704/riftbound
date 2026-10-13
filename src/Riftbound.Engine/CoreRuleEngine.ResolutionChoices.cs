using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record ResolutionDamageChoiceContext(ObjectBinding Source, ObjectBinding Target, string CardNo,
    string TargetControllerId, int ExecutionIndex, bool? DrawInstead = null);

public sealed partial class CoreRuleEngine
{
    internal const string ResolutionDamageChoiceWindow = "TARGET_CONTROLLER_DRAW_REPLACEMENT";
    internal const string DrawInsteadChoice = "ALLOW_CASTER_DRAW";

    private static StackItemState DamageChoiceExecution(StackItemState parent)
        => parent.RepeatExecutions is { } executions && parent.CompletedRepeatExecutions < executions.Count
            ? parent with { EffectKind = executions[parent.CompletedRepeatExecutions].EffectKind,
                TargetObjectIds = executions[parent.CompletedRepeatExecutions].TargetObjectIds,
                TargetGenerations = executions[parent.CompletedRepeatExecutions].TargetGenerations } : parent;

    private static PendingCardChoiceState DamageAlternativeChoice(StackItemState item, CardBehaviorDefinition behavior)
        => new($"RESOLUTION-ALTERNATIVE:{item.StackItemId}:{item.CompletedRepeatExecutions}", ResolutionDamageChoiceWindow,
            item.ResolutionDamageChoice!.TargetControllerId, 0, 1, [DrawInsteadChoice], [item.ResolutionDamageChoice.Target.ObjectId],
            $"可让法术控制者抽{behavior.DamageAlternativeDrawCount}张牌；不选则对该单位造成{behavior.DamageAmount}点伤害。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    internal static bool ValidResolutionDamageChoice(MatchState state, StackItemState parent)
    {
        if (parent.ResolutionDamageChoice is not { } context)
            return state.PendingCardChoice is not { ChoiceWindow: ResolutionDamageChoiceWindow } pending
                || pending.ResolvingStackItemId != parent.StackItemId;
        var item = DamageChoiceExecution(parent);
        if (context.DrawInstead is not null || context.ExecutionIndex != parent.CompletedRepeatExecutions
            || !CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var b) || b.DamageAlternativeDrawCount <= 0
            || context.CardNo != b.CardNo || item.CardNo != b.CardNo
            || context.Source is not { Generation: >= 0 } || context.Source.ObjectId != item.SourceObjectId
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.CardNo != context.CardNo
            || source.ObjectGeneration != context.Source.Generation
            || !state.ObjectLocations.TryGetValue(source.ObjectId, out var sourceLocation) || sourceLocation.Zone != "STACK"
            || item.TargetObjectIds.Count != 1 || context.Target is not { Generation: >= 0 }
            || item.TargetObjectIds[0] != context.Target.ObjectId
            || item.TargetGenerations?.GetValueOrDefault(context.Target.ObjectId, -1) != context.Target.Generation
            || !state.CardObjects.TryGetValue(context.Target.ObjectId, out var target) || target.ObjectGeneration != context.Target.Generation
            || !IsFieldUnitObject(state, target.ObjectId) || target.IsFaceDown
            || EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target) != context.TargetControllerId
            || context.TargetControllerId == parent.ControllerId || !state.Seats.ContainsKey(context.TargetControllerId)
            || state.PriorityPlayerId is not null || state.PendingCardChoice is not { } choice) return false;
        var expected = DamageAlternativeChoice(parent, b);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.ResolvingStackItemId == parent.StackItemId && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.Reason == expected.Reason
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static StackResolutionResult BeginDamageAlternative(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        var target = state.CardObjects[item.TargetObjectIds[0]]; var source = state.CardObjects[item.SourceObjectId];
        item = item with { ResolutionDamageChoice = new(new(source.ObjectId, source.ObjectGeneration),
            new(target.ObjectId, target.ObjectGeneration), item.CardNo!, EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target),
            item.CompletedRepeatExecutions) };
        return NoopStackResolutionResult(state) with {
            StackItems = state.StackItems.Where(i => i.StackItemId != item.StackItemId).Append(item).ToArray(),
            PendingCardChoice = DamageAlternativeChoice(item, behavior), Events = [new("RESOLUTION_ALTERNATIVE_REQUESTED",
                "等待目标单位控制者选择", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target.ObjectId,
                    ["playerId"] = item.ResolutionDamageChoice.TargetControllerId, ["executionIndex"] = item.CompletedRepeatExecutions })] };
    }

    private static ResolutionResult ResolveDamageAlternativeChoice(MatchState state, PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        var parent = state.StackItems.LastOrDefault();
        if (parent is null || parent.StackItemId != choice.ResolvingStackItemId || !ValidResolutionDamageChoice(state, parent))
            return RejectWithCorePrompts(state, "结算选择上下文已失效。", ErrorCodes.InvalidTarget);
        var context = parent.ResolutionDamageChoice!;
        var next = state with { PendingCardChoice = null, StackItems = state.StackItems.Take(state.StackItems.Count - 1)
            .Append(parent with { ResolutionDamageChoice = context with { DrawInstead = selected.Count != 0 } }).ToArray() };
        var result = ResolvePassPriority(next, new("resolution-alternative", parent.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return result with { Events = new[] { new GameEvent("RESOLUTION_ALTERNATIVE_SELECTED", "目标控制者完成结算选择",
            new Dictionary<string, object?> { ["sourceObjectId"] = parent.SourceObjectId, ["targetObjectId"] = context.Target.ObjectId,
                ["playerId"] = choice.PlayerId, ["drawInstead"] = selected.Count != 0, ["executionIndex"] = context.ExecutionIndex }) }.Concat(result.Events).ToArray() };
    }
}
