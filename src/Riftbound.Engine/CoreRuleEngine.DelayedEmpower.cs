using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DelayedEmpowerContext(string InstanceId, string CreatingStackItemId, string CardNo,
    string CreatingEffectKind, string ControllerId, ObjectBinding Source, ObjectBinding Unit,
    int TurnNumber, string TurnPlayerId, int ExecutionIndex, long CreatedAtTick,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UnitTokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string TemporaryEmpowerEffect = "TEMPORARY_EMPOWER_UNIT";
    internal const string TemporaryRemoveEmpowerEffect = "TEMPORARY_REMOVE_EMPOWER_UNIT";
    internal const string DelayedEmpowerEffect = "DELAYED_EMPOWER_STATUS_ACTION";
    internal const string DelayedEmpowerPrefix = "DELAYED_EMPOWER:";
    private const string DelayedEmpowerTriggerKind = "TURN_END_STEP_STARTED";

    internal static string DelayedEmpowerLabel(DelayedEmpowerContext context)
        => context.CreatingEffectKind == TemporaryRemoveEmpowerEffect ? "回合结束：强化此前的单位" : "回合结束：解除此前单位的强化";

    internal static string DelayedEmpowerMarker(DelayedEmpowerContext context)
        => DelayedEmpowerPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(context));

    internal static DelayedEmpowerContext? ReadDelayedEmpower(string marker)
    {
        try
        {
            if (!marker.StartsWith(DelayedEmpowerPrefix, StringComparison.Ordinal)) return null;
            var context = JsonSerializer.Deserialize<DelayedEmpowerContext>(Convert.FromBase64String(marker[DelayedEmpowerPrefix.Length..]));
            return context is not null && DelayedEmpowerMarker(context) == marker ? context : null;
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException) { return null; }
    }

    internal static bool ValidDelayedEmpower(MatchState state, DelayedEmpowerContext context)
        => context.Source is { Generation: >= 0 } && context.Unit is { Generation: >= 0 }
            && !string.IsNullOrWhiteSpace(context.Source.ObjectId) && !string.IsNullOrWhiteSpace(context.Unit.ObjectId)
            && !string.IsNullOrWhiteSpace(context.ControllerId)
            && !string.IsNullOrWhiteSpace(context.CreatingStackItemId) && context.ExecutionIndex >= 0
            && context.InstanceId == $"{context.CreatingStackItemId}:END-EMPOWER:{context.ExecutionIndex}:{context.Unit.ObjectId}"
            && context.CreatedAtTick > 0 && context.CreatedAtTick <= state.Tick
            && context.TurnNumber == state.TurnNumber && context.TurnPlayerId == state.TurnPlayerId
            && state.Seats.ContainsKey(context.ControllerId)
            && (context.CreatingEffectKind == EmpowerInteractionEffect
                ? EmpowerInteractionDefinition(context.CardNo)?.Effect == "TEMPORARY_EMPOWER_HERE"
                : context.CreatingEffectKind is TemporaryEmpowerEffect or TemporaryRemoveEmpowerEffect
                    && CardBehaviorRegistry.TryGetByEffectKind(context.CreatingEffectKind, out var behavior) && behavior.CardNo == context.CardNo)
            && state.CardObjects.TryGetValue(context.Source.ObjectId, out var source) && source.ObjectGeneration >= context.Source.Generation
            && (source.ObjectGeneration != context.Source.Generation || source.CardNo == context.CardNo)
            && (state.CardObjects.TryGetValue(context.Unit.ObjectId, out var unit)
                ? unit.ObjectGeneration >= context.Unit.Generation && unit.TokenFactoryCardNo == context.UnitTokenFactoryCardNo
                : context.UnitTokenFactoryCardNo is not null && P6TokenFactoryCatalog.TryGetByCardNo(context.UnitTokenFactoryCardNo, out var token)
                    && (token.Tags.Contains(CardObjectTags.UnitCard) || token.Tags.Contains(CardObjectTags.EquipmentCard)));

    internal static bool ValidCurrentDelayedEmpowers(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(e => e is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(DelayedEmpowerPrefix, StringComparison.Ordinal)).ToArray();
        var pending = markers.Select(ReadDelayedEmpower).ToArray();
        if (pending.Any(c => c is null || !ValidDelayedEmpower(state, c))
            || markers.Length > 0 && (state.EndStep is not null || state.Phase == MatchPhases.TurnEnd)) return false;
        var all = pending.OfType<DelayedEmpowerContext>().Concat(state.StackItems.Select(i => i.DelayedEmpower).OfType<DelayedEmpowerContext>())
            .Concat(state.TriggerQueue.Select(i => i.DelayedEmpower).OfType<DelayedEmpowerContext>()).ToArray();
        return all.Select(c => c.InstanceId).Distinct(StringComparer.Ordinal).Count() == all.Length;
    }

    internal static bool ValidDelayedEmpowerStack(MatchState state, StackItemState item)
        => item.DelayedEmpower is { } context && item.EffectKind == DelayedEmpowerEffect
            && ValidDelayedEmpower(state, context) && ValidDelayedEmpowerEndWindow(state)
            && item.StackItemId == "ordered-" + context.InstanceId + ":END"
            && item.SourceObjectId == context.Source.ObjectId && item.ControllerId == context.ControllerId && item.CardNo == context.CardNo
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null;

    internal static bool ValidDelayedEmpowerTrigger(MatchState state, TriggerQueueItemState item)
        => item.DelayedEmpower is { } context && item.EffectKind == DelayedEmpowerEffect
            && ValidDelayedEmpower(state, context) && ValidDelayedEmpowerEndWindow(state)
            && item.TriggerId == context.InstanceId + ":END" && item.TriggeredByEventKind == DelayedEmpowerTriggerKind
            && item.SourceObjectId == context.Source.ObjectId && item.ControllerId == context.ControllerId && item.SourceCardNo == context.CardNo;

    private static bool ValidDelayedEmpowerEndWindow(MatchState state)
        => state.EndStep is { } end && end.PlayerId == state.TurnPlayerId && end.TurnNumber == state.TurnNumber
            && state.Phase == MatchPhases.TurnEnd;

    private static void ApplyTemporaryEmpowerInstruction(MatchState state, StackItemState item,
        Dictionary<string, CardObjectState> cards, List<string> effects, List<GameEvent> events)
    {
        if (item.EffectKind is not (TemporaryEmpowerEffect or TemporaryRemoveEmpowerEffect)) return;
        var target = item.TargetObjectIds.SingleOrDefault();
        if (target is null || !cards.TryGetValue(target, out var unit) || !IsFaceUpNonStandbyUnit(unit)
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != unit.ObjectGeneration
            || !IsObjectOnField(state.PlayerZones, target)
            || item.EffectKind == TemporaryRemoveEmpowerEffect && !unit.IsEmpowered) return;
        var changed = ApplyEmpowerStatusAction(state with { CardObjects = cards }, new(target, unit.ObjectGeneration),
            item.EffectKind == TemporaryEmpowerEffect);
        cards[target] = changed.CardObjects[target];
        events.AddRange(changed.Events);
        ScheduleDelayedEmpower(state, item, target, item.EffectKind == TemporaryRemoveEmpowerEffect, effects, events);
    }

    private static void ScheduleDelayedEmpower(MatchState state, StackItemState item, string target, bool empower,
        List<string> effects, List<GameEvent> events)
    {
        var unit = state.CardObjects[target];
        // CN 359.3.e.16: a window already passed does not create a delayed ability.
        // A no-op immediate action, however, does not suppress the independent instruction.
        if (state.EndStep is not null || state.Phase == MatchPhases.TurnEnd) return;
        var context = new DelayedEmpowerContext($"{item.StackItemId}:END-EMPOWER:{item.CompletedRepeatExecutions}:{target}",
            item.StackItemId, item.CardNo!, item.EffectKind, item.ControllerId,
            item.EmpowerInteraction?.Source ?? new(item.SourceObjectId, state.CardObjects[item.SourceObjectId].ObjectGeneration), new(target, unit.ObjectGeneration),
            state.TurnNumber, state.TurnPlayerId, item.CompletedRepeatExecutions, RuleChoices.Value!.CompletionTick, unit.TokenFactoryCardNo);
        effects.Add(DelayedEmpowerMarker(context));
        events.Add(new("DELAYED_EMPOWER_SCHEDULED", "生成本回合结束时的强化状态逆操作", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target, ["targetGeneration"] = unit.ObjectGeneration,
            ["turnNumber"] = state.TurnNumber, ["empower"] = empower }));
    }

    private static IEnumerable<TriggerQueueItemState> CaptureDelayedEmpowerEndAbilities(MatchState state)
    {
        foreach (var marker in state.UntilEndOfTurnEffects.Where(e => e.StartsWith(DelayedEmpowerPrefix, StringComparison.Ordinal)))
        {
            var context = ReadDelayedEmpower(marker) ?? throw new InvalidOperationException("Invalid delayed empower instruction.");
            if (!ValidDelayedEmpower(state, context)) throw new InvalidOperationException("Invalid delayed empower birth context.");
            yield return new(context.InstanceId + ":END", context.ControllerId, context.Source.ObjectId,
                DelayedEmpowerEffect, DelayedEmpowerTriggerKind, TimingStates.NeutralClosed) {
                    SourceCardNo = context.CardNo, DelayedEmpower = context };
        }
    }

    private static StackItemState DelayedEmpowerStack(TriggerQueueItemState trigger)
    {
        var context = trigger.DelayedEmpower!;
        // This is an instruction referring to the old object, not a new target
        // selection. Ordinary target binding must never rebind it on entry.
        return new("ordered-" + trigger.TriggerId, trigger.ControllerId, trigger.SourceObjectId, trigger.EffectKind,
            context.CardNo, [], 0, 0, [], timingContext: trigger.TimingContext) { DelayedEmpower = context };
    }

    private static StackResolutionResult ResolveDelayedEmpower(MatchState state, StackItemState item)
    {
        if (!ValidDelayedEmpowerStack(state, item)) throw new InvalidOperationException("Invalid delayed empower skill.");
        var context = item.DelayedEmpower!;
        return ApplyEmpowerStatusAction(state, context.Unit, context.CreatingEffectKind == TemporaryRemoveEmpowerEffect);
    }
}
