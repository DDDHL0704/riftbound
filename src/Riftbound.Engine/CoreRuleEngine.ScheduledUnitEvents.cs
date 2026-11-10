using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScheduledUnitEventReceipt(string Id, string CreatingStackItemId, long CreatedTick)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FiredTick { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScheduledUnitEvent(ScheduledUnitEventReceipt Receipt, string ControllerId,
    string SourceObjectId, UnitEventContext Context);

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextMainEffect { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CapturesTriggeringBattlefield { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScheduledUnitEventReceipt? DelayedReceipt { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? TriggeringBattlefield { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string ScheduleNextMainUnitEventEffect = "SCHEDULE_NEXT_MAIN_UNIT_EVENT";
    internal const string MoveEnemyToCapturedBattlefieldEffect = "MOVE_ENEMY_TO_CAPTURED_BATTLEFIELD";

    private static string ScheduledUnitEventId(string creatingStack, long tick, string source,
        string controller, UnitEventContext context)
        => "scheduled-unit-event-" + MatchStateHasher.HashValue(new {
            creatingStack, tick, source, controller, Context = context with { DelayedReceipt = null } });

    internal static long ObservedUnitEventOccurrenceTick(UnitEventContext context)
        => HasArmamentPlayAttachment(context) && context.ReflexiveTick is { } tick
            ? tick : context.RevealedDeckSelection?.RevealedTick ?? context.DelayedReceipt?.FiredTick ?? context.CapturedTick;

    private static bool ValidScheduledUnitEventReceipt(MatchState state, UnitEventContext context,
        string source, string? controller)
        => UnitEventSpec(context.CardNo)?.NextMainEffect is null ? context.DelayedReceipt is null
            : context.DelayedReceipt is not { } receipt
                || !string.IsNullOrWhiteSpace(receipt.Id) && !string.IsNullOrWhiteSpace(receipt.CreatingStackItemId)
                    && receipt.CreatedTick >= context.CapturedTick
                    && receipt.FiredTick is { } fired && fired >= receipt.CreatedTick && fired <= state.Tick
                    && (controller is null || receipt.Id == ScheduledUnitEventId(receipt.CreatingStackItemId, receipt.CreatedTick,
                        source, controller, context));

    internal static bool ValidPendingScheduledUnitEvent(MatchState state, ScheduledUnitEvent schedule)
        => schedule is { Receipt: { FiredTick: null } receipt, Context: { DelayedReceipt: null } context }
            && !string.IsNullOrWhiteSpace(schedule.ControllerId) && state.Seats.ContainsKey(schedule.ControllerId)
            && !string.IsNullOrWhiteSpace(schedule.SourceObjectId) && !string.IsNullOrWhiteSpace(receipt.Id)
            && !string.IsNullOrWhiteSpace(receipt.CreatingStackItemId)
            && receipt.CreatedTick >= context.CapturedTick && receipt.CreatedTick <= state.Tick
            && UnitEventSpec(context.CardNo) is { Effect: ScheduleNextMainUnitEventEffect, NextMainEffect: not null }
            && receipt.Id == ScheduledUnitEventId(receipt.CreatingStackItemId, receipt.CreatedTick,
                schedule.SourceObjectId, schedule.ControllerId, context)
            && ValidUnitEvent(state, context, UnitEventEffect, schedule.SourceObjectId, context.CardNo, schedule.ControllerId);

    internal static bool ValidCurrentScheduledUnitEvents(MatchState state)
        => state.ScheduledUnitEvents is not { } schedules
            || schedules.Count > 0 && schedules.All(schedule => ValidPendingScheduledUnitEvent(state, schedule))
                && schedules.Select(schedule => schedule.Receipt.Id).Distinct(StringComparer.Ordinal).Count() == schedules.Count;

    private static bool ValidTriggeringBattlefield(UnitEventContext context, UnitEventAbilitySpec spec)
        => !spec.CapturesTriggeringBattlefield ? context.TriggeringBattlefield is null
            : context.TriggeringBattlefield is { Generation: >= 0 } field
                && !string.IsNullOrWhiteSpace(field.ObjectId);

    private static UnitEventObservation CaptureTriggeredUnitEventBattlefield(MatchState state,
        UnitEventObservation observation, string battlefield)
        => UnitEventSpec(observation.Context.CardNo)?.CapturesTriggeringBattlefield != true ? observation
            : observation with { Context = observation.Context with {
                TriggeringBattlefield = new(battlefield, state.CardObjects[battlefield].ObjectGeneration) } };

    private static StackResolutionResult ScheduleNextMainUnitEvent(MatchState state, StackItemState item)
    {
        var context = item.UnitEvent!;
        if (context.DelayedReceipt is not null || UnitEventSpec(context.CardNo)?.NextMainEffect is null)
            throw new InvalidOperationException("Invalid delayed unit-event creation.");
        var tick = RuleChoices.Value?.CompletionTick ?? state.Tick;
        var receipt = new ScheduledUnitEventReceipt(ScheduledUnitEventId(item.StackItemId, tick,
            item.SourceObjectId, item.ControllerId, context), item.StackItemId, tick);
        var schedule = new ScheduledUnitEvent(receipt, item.ControllerId, item.SourceObjectId, context);
        return NoopStackResolutionResult(state) with {
            ScheduledUnitEvents = [..(state.ScheduledUnitEvents ?? []), schedule],
            Events = [new("DELAYED_UNIT_EVENT_CREATED", "已建立下一个己方主阶段的独立延迟技能",
                new Dictionary<string, object?> { ["scheduleId"] = receipt.Id,
                    ["scheduledUnitEventHash"] = MatchStateHasher.HashValue(schedule) })] };
    }

    private static MatchState QueueNextMainUnitEvents(MatchState state, List<GameEvent> events)
    {
        // CN392/393: consume at the actual next own MAIN transition, after pool
        // clearing. A preceding HOLD can create the delay in this same command.
        // A delay created after MAIN began stays until a later own MAIN.
        if (state.Status != MatchStatuses.InProgress) return state;
        var due = (state.ScheduledUnitEvents ?? []).Where(schedule =>
            schedule.ControllerId == state.TurnPlayerId).ToArray();
        if (due.Length == 0) return state;
        var queue = state.TriggerQueue.ToList();
        var ordinal = RuleChoices.Value?.UnitEventOrdinal ?? 0;
        foreach (var schedule in due)
        {
            var receipt = schedule.Receipt with { FiredTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
            var observation = new UnitEventObservation(schedule.SourceObjectId, schedule.ControllerId,
                schedule.Context with { DelayedReceipt = receipt });
            var trigger = UnitEventTrigger(observation, receipt.FiredTick!.Value,
                ordinal++, "MAIN_PHASE_BEGAN", TimingStates.NeutralOpen);
            queue.Add(trigger);
            events.Add(new("DELAYED_UNIT_EVENT_TRIGGERED", "延迟技能在己方主阶段开始时触发",
                new Dictionary<string, object?> { ["scheduleId"] = schedule.Receipt.Id }));
            var queued = BuildTriggerQueuedEvent(trigger);
            events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state,
                    schedule.SourceObjectId, ProjectionViewers.Spectator),
                ["timingSourceHash"] = MatchStateHasher.HashValue(schedule.SourceObjectId) } });
        }
        if (RuleChoices.Value is { } frame) frame.UnitEventOrdinal = ordinal;
        var remaining = (state.ScheduledUnitEvents ?? []).Except(due).ToArray();
        return state with { TriggerQueue = queue,
            ScheduledUnitEvents = remaining.Length == 0 ? null : remaining };
    }

    private static string? UnitEventCapturedBattlefieldDestination(MatchState state, StackItemState item)
        => item.UnitEvent?.TriggeringBattlefield is { } binding
            && state.CardObjects.TryGetValue(binding.ObjectId, out var field)
            && field.ObjectGeneration == binding.Generation
            && BattlefieldLocalRules.Battlefield(state, binding.ObjectId) is not null
                ? "BATTLEFIELD:" + binding.ObjectId : null;
}
