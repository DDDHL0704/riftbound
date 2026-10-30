using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SelfActionOrigin(string Actor, string Action, string CauseId, long CapturedTick,
    long FaceRevision, string InstanceId, CeasedEmpoweredSource? CeasedSource = null);

public sealed partial class CoreRuleEngine
{
    internal const string SelfActionPowerKind = "CONTROLLER_TARGETED_OR_READIED_SELF_POWER";
    private const string SelfReadyCaptureKey = "internalSelfActionObservers";
    private sealed record SelfActionCapture(string SourceId, string Controller, FieldTriggerContext Context);

    private static TriggerSpec? SelfActionSpec(string? no)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(no).FirstOrDefault(t =>
            t.Kind == TriggerKinds.ControllerTargetedOrReadiedSourcePower
            && t.Timing == TriggerTimings.ControllerTargetedOrReadiedSource
            && t.TargetScope == TriggerTargetScopes.SourceUnit && t.PowerDelta is > 0
            && t.Duration == TriggerDurations.UntilEndOfTurn);

    private static SelfActionCapture[] CaptureSelfActions(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState unit, string actor, string action, string cause)
    {
        if (!IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(zones, unit.ObjectId)
            || EffectiveFieldControllerId(zones, unit.ObjectId, unit) != actor) return [];
        return AttachedRuleTextRules.Instances(zones, cards, unit).Where(i => SelfActionSpec(i.CardNo) is not null)
            .Select(i => new SelfActionCapture(unit.ObjectId, actor,
                new(i.CardNo, SelfActionPowerKind, unit.ObjectGeneration) {
                    Origin = i.Origin, SourceTokenFactoryCardNo = unit.TokenFactoryCardNo,
                    SelfAction = new(actor, action, cause, 0,
                        i.Origin?.FaceRevision ?? unit.CopyFaceTransitions?.Count ?? 0, i.Id)
                })).ToArray();
    }

    // Capture at the ready instruction, before later movement or text changes.
    // Entering active is not a ready action; an already active object cannot ready (416.1).
    private static GameEvent CaptureSelfReady(GameEvent ev, CardObjectState before, string actor,
        IReadOnlyDictionary<string, PlayerZones>? zones = null, IReadOnlyDictionary<string, CardObjectState>? cards = null)
    {
        if (!before.IsExhausted) return ev;
        var frame = RuleChoices.Value?.ResolutionState;
        zones ??= frame?.PlayerZones; cards ??= frame?.CardObjects;
        if (zones is null || cards is null) return ev;
        ev = WithUnitEvents(ev, CaptureUnitEvents(zones, cards, [before.ObjectId], "BECAME_READY"));
        var captures = CaptureSelfActions(zones, cards, before, actor, "READIED",
            RuleChoices.Value?.ResolutionItem?.StackItemId ?? $"READY:{before.ObjectId}");
        return captures.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [SelfReadyCaptureKey] = (ev.Payload.GetValueOrDefault(SelfReadyCaptureKey) as SelfActionCapture[] ?? []).Concat(captures).ToArray() } };
    }

    private static void CaptureImmediateTargetSelection(List<GameEvent> events,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        IReadOnlyList<string> targets, string actor, string cause)
    {
        var captures = targets.Distinct(StringComparer.Ordinal).Where(cards.ContainsKey)
            .SelectMany(id => CaptureSelfActions(zones, cards, cards[id], actor, "TARGETED", cause)).ToArray();
        if (captures.Length > 0) events.Add(new("INTERNAL_TARGET_CONFIRMATION", "",
            new Dictionary<string, object?> { [SelfReadyCaptureKey] = captures }));
    }

    private static bool HasConfirmedTargets(MatchState state, StackItemState item)
        => item.TargetObjectIds.Count > 0 && !NeedsTriggerConfirmation(item)
            && state.PendingPayment?.ResolvingStackItemId != item.StackItemId
            && state.PendingCardChoice?.ResolvingStackItemId != item.StackItemId;

    private static ResolutionResult QueueSelfActionObservers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList(); var ordinal = 0;
        void Add(SelfActionCapture capture, string eventKind)
        {
            if (result.State.Status != MatchStatuses.InProgress) return;
            var context = capture.Context; var birth = context.SelfAction!;
            var generation = context.SourceGeneration;
            if (before.CardObjects.TryGetValue(capture.SourceId, out var old) && !IsObjectOnField(before.PlayerZones, capture.SourceId))
                generation = Math.Max(generation, old.ObjectGeneration + 1);
            context = context with { SourceGeneration = generation,
                SelfAction = birth with { CapturedTick = result.State.Tick,
                    InstanceId = context.Origin is null ? $"PRINTED:{capture.SourceId}:{generation}" : birth.InstanceId } };
            var binding = MatchStateHasher.HashValue(new { Source = capture.SourceId, capture.Controller, Context = context });
            string triggerId;
            do { triggerId = $"self-action-{result.State.Tick}-{binding}-{ordinal++}"; }
            while (queue.Any(t => t.TriggerId == triggerId)
                || result.State.StackItems.Any(i => i.StackItemId == "ordered-" + triggerId));
            var trigger = new TriggerQueueItemState(triggerId,
                capture.Controller, capture.SourceId, FieldTriggerEffect, eventKind) {
                    FieldContext = context, SourceCardNo = context.CardNo };
            queue.Add(trigger);
            var queued = BuildTriggerQueuedEvent(trigger);
            events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, capture.SourceId, "__spectator__") } });
        }
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index))) {
            if (ev.Payload.GetValueOrDefault(SelfReadyCaptureKey) is not SelfActionCapture[] captures) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != SelfReadyCaptureKey).ToDictionary(p => p.Key, p => p.Value) };
            foreach (var capture in captures) Add(capture, capture.Context.SelfAction!.Action == "READIED" ? "UNIT_READY" : "TARGET_CONFIRMED");
        }
        foreach (var item in result.State.StackItems.Where(i => HasConfirmedTargets(result.State, i))) {
            var old = before.StackItems.FirstOrDefault(i => i.StackItemId == item.StackItemId);
            var remade = result.Events.Any(ev => ev.Kind == "SPELL_CHOICES_REMADE"
                && Equals(ev.Payload.GetValueOrDefault("stackItemId"), item.StackItemId));
            if (old is not null && HasConfirmedTargets(before, old) && !remade) continue;
            var executions = item.RepeatExecutions?.Select(e => (Targets: e.TargetObjectIds, Bindings: (IReadOnlyDictionary<string, long>?)e.TargetGenerations))
                ?? [(item.TargetObjectIds, item.TargetGenerations)];
            foreach (var execution in executions)
            foreach (var id in execution.Targets.Distinct(StringComparer.Ordinal))
                if (result.State.CardObjects.TryGetValue(id, out var unit)
                    && (execution.Bindings is null || execution.Bindings.GetValueOrDefault(id, -1) == unit.ObjectGeneration))
                    foreach (var capture in CaptureSelfActions(result.State.PlayerZones, result.State.CardObjects,
                        unit, item.ControllerId, "TARGETED", item.StackItemId)) Add(capture, "TARGET_CONFIRMED");
        }
        if (ordinal == 0 && events.Count == result.Events.Count
            && !result.Events.Any(ev => ev.Payload.ContainsKey(SelfReadyCaptureKey))) return result;
        events.RemoveAll(ev => ev.Kind == "INTERNAL_TARGET_CONFIRMATION");
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidSelfActionShape(FieldTriggerContext c, string effect, string? no)
        => effect == FieldTriggerEffect && c.Kind == SelfActionPowerKind && (no is null || no == c.CardNo)
            && SelfActionSpec(c.CardNo) is not null && c.SourceGeneration >= 0
            && c.SelfAction is { CapturedTick: > 0, FaceRevision: >= 0 } birth
            && birth.Action is "TARGETED" or "READIED" && !string.IsNullOrWhiteSpace(birth.Actor)
            && !string.IsNullOrWhiteSpace(birth.CauseId) && !string.IsNullOrWhiteSpace(birth.InstanceId)
            && c.BattlefieldId is null && c.ReturnFocusPlayerId is null && c.SourceLocation is null
            && c.RecycledBoon is null && c.EmpoweredTransition is null && c.AffectedUnit is null
            && !c.AffectedUnitCeasedToExist && c.AttackConditionFrame is null
            && (c.SourceTokenFactoryCardNo is null ? !c.SourceCeasedToExist && birth.CeasedSource is null
                : P6TokenFactoryCatalog.TryGetByCardNo(c.SourceTokenFactoryCardNo, out var factory)
                    && factory.Tags.Contains(CardObjectTags.UnitCard) && c.SourceCeasedToExist == (birth.CeasedSource is not null));

    private static bool ValidSelfActionSource(MatchState state, FieldTriggerContext c, string source, string controller)
    {
        var birth = c.SelfAction!;
        if (birth.Actor != controller || !state.Seats.ContainsKey(controller) || birth.CapturedTick > state.Tick) return false;
        if (c.SourceCeasedToExist && (state.CardObjects.ContainsKey(source) || birth.CeasedSource is not { } ceased
            || ceased.Tick < birth.CapturedTick || ceased.Tick > state.Tick || ceased.CardNo != c.CardNo)) return false;
        if (!c.SourceCeasedToExist && !state.CardObjects.ContainsKey(source)) return false;
        return c.Origin is { } origin
            ? birth.InstanceId == $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, origin, source, c.SourceGeneration, c.CardNo)
            : birth.InstanceId == $"PRINTED:{source}:{c.SourceGeneration}"
                && PredictBindingMatches(state, new(source, c.SourceGeneration), c.CardNo,
                    birth.FaceRevision, birth.CapturedTick, c.SourceTokenFactoryCardNo);
    }

    private static StackResolutionResult ResolveSelfActionObserver(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (!ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid captured self action observer.");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var visible = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "被己方选为目标或活跃的技能结算",
            new Dictionary<string, object?> { ["sourceObjectId"] = visible, ["effectKind"] = SelfActionPowerKind }) };
        if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.SourceGeneration
            && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId)) {
            var amount = SelfActionSpec(context.CardNo)!.PowerDelta!.Value;
            var behavior = new CardBehaviorDefinition(context.CardNo, "被己方选为目标或活跃", 0, SelfActionPowerKind, 0, 0,
                PowerModifierAmount: amount);
            cards[source.ObjectId] = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, amount, out var power);
            events.Add(power);
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
