using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExhaustedUnitFact(ObjectBinding Unit, string CardNo, long FaceRevision, string? TokenFactoryCardNo);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExhaustionObserverOrigin(ObjectBinding Source, string Actor, string CauseId, IReadOnlyList<ExhaustedUnitFact> Units,
    int TurnNumber, long CapturedTick, long FaceRevision, string InstanceId, bool Confirmed = false,
    CeasedEmpoweredSource? CeasedSource = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExhaustionDrawUse(string SourceId, FieldTriggerContext Context);

public sealed partial class CoreRuleEngine
{
    internal const string ExhaustionDrawKind = "SIMULTANEOUS_FRIENDLY_EXHAUSTION_DRAW";
    internal const string ExhaustionDrawWindow = "EXHAUSTION_DRAW_CONFIRMATION";
    internal const string ExhaustionDrawUsePrefix = "EXHAUSTION_DRAW_USED|";
    private const string ExhaustionObservationKey = "internalExhaustionObservers";
    private sealed record ExhaustionCapture(string SourceId, string Controller, FieldTriggerContext Context);

    private static TriggerSpec? ExhaustionDrawSpec(string? no)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(no).FirstOrDefault(t =>
            t.Kind == TriggerKinds.ControllerExhaustedUnitGroupDraw && t.Timing == TriggerTimings.ControllerExhaustedUnitGroup
            && t.RequiredUnitCount is > 0 && t.DrawCount is > 0 && t.Optional == true && t.OncePerTurn == true);

    // Capture one instruction, before any of its units change state. Entering
    // exhausted and several successive one-unit instructions do not qualify.
    private static void CaptureExhaustionInstruction(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string actor, IEnumerable<string> ids,
        string cause, List<GameEvent> events)
    {
        var units = ids.Distinct(StringComparer.Ordinal).Where(id => cards.TryGetValue(id, out var unit)
                && IsFaceUpNonStandbyUnit(unit) && !unit.IsExhausted && IsObjectOnField(zones, id)
                && EffectiveFieldControllerId(zones, id, unit) == actor)
            .Select(id => cards[id]).Select(unit => new ExhaustedUnitFact(new(unit.ObjectId, unit.ObjectGeneration),
                AttachedRuleTextRules.Face(unit)!, unit.CopyFaceTransitions?.Count ?? 0, unit.TokenFactoryCardNo)).ToArray();
        if (units.Length == 0) return;
        var captures = cards.Values.Where(source => IsFaceUpNonStandbyUnit(source) && IsObjectOnField(zones, source.ObjectId)
                && EffectiveFieldControllerId(zones, source.ObjectId, source) == actor)
            .SelectMany(source => AttachedRuleTextRules.Instances(zones, cards, source)
                .Where(instance => ExhaustionDrawSpec(instance.CardNo) is { RequiredUnitCount: { } count } && units.Length >= count)
                .Select(instance => new ExhaustionCapture(source.ObjectId, actor, new(instance.CardNo, ExhaustionDrawKind, source.ObjectGeneration) {
                    Origin = instance.Origin, SourceTokenFactoryCardNo = source.TokenFactoryCardNo,
                    ExhaustionObserver = new(new(source.ObjectId, source.ObjectGeneration), actor, cause, units, 0, 0,
                        instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0, instance.Id)
                }))).ToArray();
        if (captures.Length > 0) events.Add(new("INTERNAL_EXHAUSTION_INSTRUCTION", "",
            new Dictionary<string, object?> { [ExhaustionObservationKey] = captures }));
    }

    private static ResolutionResult QueueExhaustionObservers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = new List<GameEvent>();
        foreach (var (ev, ordinal) in result.Events.Select((ev, index) => (ev, index))) {
            if (ev.Payload.GetValueOrDefault(ExhaustionObservationKey) is not ExhaustionCapture[] captures) { events.Add(ev); continue; }
            foreach (var (capture, instance) in captures.Select((capture, index) => (capture, index))) {
                var context = capture.Context with { ExhaustionObserver = capture.Context.ExhaustionObserver! with {
                    TurnNumber = result.State.TurnNumber, CapturedTick = result.State.Tick } };
                if (result.State.Status != MatchStatuses.InProgress
                    || ExhaustionDrawUsed(result.State, capture.SourceId, context)) continue;
                var identity = MatchStateHasher.HashValue(new { capture.SourceId, Context = context });
                var suffix = 0; string id;
                do { id = $"exhaustion-{result.State.Tick}-{ordinal}-{instance}-{identity}-{suffix++}"; }
                while (queue.Any(t => t.TriggerId == id) || result.State.StackItems.Any(i => i.StackItemId == "ordered-" + id));
                var trigger = new TriggerQueueItemState(id,
                    capture.Controller, capture.SourceId, FieldTriggerEffect, "UNITS_EXHAUSTED",
                    before.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : before.BattleState.IsActive ? TimingStates.NeutralClosed : null) {
                    SourceCardNo = context.CardNo, FieldContext = context };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, capture.SourceId, "__spectator__") } });
            }
        }
        return result with { State = result.State with { TriggerQueue = queue }, Events = events };
    }

    private static bool ValidExhaustionObserverShape(FieldTriggerContext c, string effect, string? no)
        => effect == FieldTriggerEffect && (no is null || no == c.CardNo) && c.Kind == ExhaustionDrawKind
            && c.SourceGeneration >= 0 && ExhaustionDrawSpec(c.CardNo) is { RequiredUnitCount: { } count }
            && c.ExhaustionObserver is { Source.Generation: >= 0, CapturedTick: > 0, FaceRevision: >= 0, TurnNumber: > 0, Units: not null } birth
            && !string.IsNullOrWhiteSpace(birth.Source.ObjectId) && birth.Source.Generation == c.SourceGeneration
            && !string.IsNullOrWhiteSpace(birth.Actor) && !string.IsNullOrWhiteSpace(birth.CauseId)
            && !string.IsNullOrWhiteSpace(birth.InstanceId)
            && birth.Units.Count >= count && birth.Units.All(u => u is { Unit.Generation: >= 0, FaceRevision: >= 0 }
                && !string.IsNullOrWhiteSpace(u.Unit.ObjectId) && !string.IsNullOrWhiteSpace(u.CardNo)
                && (u.TokenFactoryCardNo is null || P6TokenFactoryCatalog.TryGetByCardNo(u.TokenFactoryCardNo, out var unitFactory)
                    && unitFactory.Tags.Contains(CardObjectTags.UnitCard)))
            && birth.Units.Select(u => u.Unit.ObjectId).Distinct(StringComparer.Ordinal).Count() == birth.Units.Count
            && (c.SourceTokenFactoryCardNo is null ? !c.SourceCeasedToExist && birth.CeasedSource is null
                : P6TokenFactoryCatalog.TryGetByCardNo(c.SourceTokenFactoryCardNo, out var factory)
                    && factory.Tags.Contains(CardObjectTags.UnitCard) && c.SourceCeasedToExist == (birth.CeasedSource is not null))
            && c == new FieldTriggerContext(c.CardNo, ExhaustionDrawKind, c.SourceGeneration) {
                Origin = c.Origin, SourceTokenFactoryCardNo = c.SourceTokenFactoryCardNo,
                SourceCeasedToExist = c.SourceCeasedToExist, ExhaustionObserver = birth };

    private static bool ValidExhaustionObserverSource(MatchState state, FieldTriggerContext c, string source, string controller)
    {
        var birth = c.ExhaustionObserver!;
        if (birth.Source.ObjectId != source || birth.Actor != controller || !state.Seats.ContainsKey(controller) || birth.TurnNumber != state.TurnNumber
            || birth.CapturedTick > state.Tick || c.SourceCeasedToExist && (state.CardObjects.ContainsKey(source)
                || birth.CeasedSource is not { } ceased || ceased.CardNo != c.CardNo
                || ceased.Tick < birth.CapturedTick || ceased.Tick > state.Tick)
            || !c.SourceCeasedToExist && birth.CeasedSource is not null
            || !birth.Units.All(u => PredictBindingMatches(state, u.Unit, u.CardNo, u.FaceRevision,
                birth.CapturedTick, u.TokenFactoryCardNo))) return false;
        return c.Origin is { } origin
            ? birth.FaceRevision == origin.FaceRevision
                && birth.InstanceId == $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, origin, source, c.SourceGeneration, c.CardNo)
            : birth.InstanceId == $"PRINTED:{source}:{c.SourceGeneration}"
                && PredictBindingMatches(state, new(source, c.SourceGeneration), c.CardNo,
                    birth.FaceRevision, birth.CapturedTick, c.SourceTokenFactoryCardNo);
    }

    private static string ExhaustionDrawMarker(ExhaustionDrawUse use)
        => ExhaustionDrawUsePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(use)));

    private static ExhaustionDrawUse? ReadExhaustionDrawUse(string value)
    {
        if (!value.StartsWith(ExhaustionDrawUsePrefix, StringComparison.Ordinal)) return null;
        try {
            var use = JsonSerializer.Deserialize<ExhaustionDrawUse>(Encoding.UTF8.GetString(Convert.FromBase64String(value[ExhaustionDrawUsePrefix.Length..])));
            return use is { SourceId.Length: > 0, Context.ExhaustionObserver: not null } && value == ExhaustionDrawMarker(use) ? use : null;
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException) { return null; }
    }

    private static bool SameExhaustionInstance(string source, FieldTriggerContext c, ExhaustionDrawUse use)
        => source == use.SourceId && c.SourceGeneration == use.Context.SourceGeneration && c.CardNo == use.Context.CardNo
            && c.ExhaustionObserver!.InstanceId == use.Context.ExhaustionObserver!.InstanceId
            && c.ExhaustionObserver.FaceRevision == use.Context.ExhaustionObserver.FaceRevision;

    private static bool ExhaustionDrawUsed(MatchState state, string source, FieldTriggerContext context)
        => state.UntilEndOfTurnEffects.Select(ReadExhaustionDrawUse).OfType<ExhaustionDrawUse>()
            .Any(use => SameExhaustionInstance(source, context, use));

    internal static bool ValidExhaustionDrawUses(MatchState state)
    {
        var uses = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(ExhaustionDrawUsePrefix, StringComparison.Ordinal))
            .Select(ReadExhaustionDrawUse).ToArray();
        return uses.All(u => u is { Context.ExhaustionObserver.Confirmed: true }
                && ValidFieldContextState(state, u.Context, u.SourceId, u.Context.ExhaustionObserver.Actor, FieldTriggerEffect, u.Context.CardNo))
            && uses.Select(u => (u!.SourceId, u.Context.SourceGeneration, u.Context.CardNo,
                u.Context.ExhaustionObserver!.InstanceId, u.Context.ExhaustionObserver.FaceRevision)).Distinct().Count() == uses.Length;
    }

    private static bool HasMatchingExhaustionDrawReceipt(MatchState state, string source, FieldTriggerContext context)
    {
        // Token cessation is a later fact, not a change to the instruction that
        // was accepted during confirmation. All original instruction facts bind.
        static FieldTriggerContext Captured(FieldTriggerContext c) => c with {
            SourceCeasedToExist = false, ExhaustionObserver = c.ExhaustionObserver! with { CeasedSource = null } };
        var expected = ExhaustionDrawMarker(new(source, Captured(context)));
        return state.UntilEndOfTurnEffects.Select(ReadExhaustionDrawUse).OfType<ExhaustionDrawUse>()
            .Any(use => expected == ExhaustionDrawMarker(use with { Context = Captured(use.Context) }));
    }

    internal static bool ValidExhaustionDrawStack(MatchState state, StackItemState item)
        => item.FieldContext?.Kind != ExhaustionDrawKind
            || item.FieldContext.ExhaustionObserver is { } birth
                && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
                && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && item.Destination.Length == 0 && !item.SourceConfirmed
                && (!birth.Confirmed || HasMatchingExhaustionDrawReceipt(state, item.SourceObjectId, item.FieldContext));

    private static PendingCardChoiceState ExhaustionDrawChoice(StackItemState item)
        => new("EXHAUSTION-DRAW:" + item.StackItemId, ExhaustionDrawWindow, item.ControllerId, 0, 1,
            ["DRAW"], [], "可选择抽牌；放弃保留本回合次数，确认后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareExhaustionDraw(ResolutionResult result, StackItemState item)
    {
        if (ExhaustionDrawUsed(result.State, item.SourceObjectId, item.FieldContext!))
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var next = result.State with { PendingCardChoice = ExhaustionDrawChoice(item), PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidExhaustionDrawChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.FieldContext is not { Kind: ExhaustionDrawKind, ExhaustionObserver.Confirmed: false } context
            || !ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            || ExhaustionDrawUsed(state, item.SourceObjectId, context)) return false;
        var expected = ExhaustionDrawChoice(item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveExhaustionDrawChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidExhaustionDrawChoice(state, choice)) return RejectWithCorePrompts(state, "抽牌技能确认已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null };
        if (selected.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(
            new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item));
        var context = item.FieldContext! with { ExhaustionObserver = item.FieldContext!.ExhaustionObserver! with { Confirmed = true } };
        var changed = item with { FieldContext = context };
        next = RestoreAfterTriggerConfirmation(next with {
            UntilEndOfTurnEffects = [..next.UntilEndOfTurnEffects, ExhaustionDrawMarker(new(item.SourceObjectId, context))],
            StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? changed : i).ToArray() }, item);
        return PrepareTriggerConfirmation(new(true, null, next, [new("TRIGGER_CONFIRMED", "已确认抽牌技能", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["playerId"] = item.ControllerId,
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(next, item.SourceObjectId, "__spectator__") })],
            ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)));
    }

    private static StackResolutionResult ResolveExhaustionDraw(MatchState state, StackItemState item)
    {
        if (item.FieldContext is not { ExhaustionObserver.Confirmed: true } context
            || !ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            || !HasMatchingExhaustionDrawReceipt(state, item.SourceObjectId, context)) throw new InvalidOperationException("Invalid exhaustion draw confirmation.");
        var drawn = ResolveLastBreathDrawStackItem(state, item, ExhaustionDrawSpec(context.CardNo)!.DrawCount!.Value);
        return drawn with { Events = drawn.Events.Select(ev => ev.Kind == "TRIGGER_RESOLVED"
            ? ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
                ["triggeredByEventKind"] = "UNITS_EXHAUSTED",
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") } }
            : ev).ToArray() };
    }
}
