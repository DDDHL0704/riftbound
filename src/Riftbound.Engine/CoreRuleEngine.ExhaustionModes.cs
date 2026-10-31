using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExhaustionModeUse(string SourceId, FieldTriggerContext Context,
    IReadOnlyList<ExhaustedUnitFact> Targets, long ConfirmedTick);

public sealed partial class CoreRuleEngine
{
    internal const string SourceExhaustionModeKind = "SOURCE_EXHAUSTED_CHOOSE_UNUSED_MODE";
    internal const string ExhaustionModeWindow = "EXHAUSTION_MODE_CONFIRMATION";
    internal const string ExhaustionModeTargetWindow = "EXHAUSTION_MODE_TARGET";
    internal const string ExhaustionModeUsePrefix = "EXHAUSTION_MODE_USED|";

    private static TriggerSpec? ExhaustionModeSpec(string? no)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(no).FirstOrDefault(t =>
            t.Kind == TriggerKinds.SourceExhaustedChooseUnusedMode && t.Timing == TriggerTimings.SourceExhausted
            && t.DistinctModesPerTurn == true && t.Modes is { Count: > 0 });

    internal static string ExhaustionModeAbilityLabel(FieldTriggerContext c)
        => SelectedExhaustionMode(c)?.Label ?? "休眠：选择此技能实例本回合未选过的效果";

    private static TriggerModeSpec? SelectedExhaustionMode(FieldTriggerContext c)
        => ExhaustionModeSpec(c.CardNo)?.Modes?.FirstOrDefault(m => m.Id == c.ExhaustionObserver?.Mode);

    private static string ExhaustionModeMarker(ExhaustionModeUse use)
        => ExhaustionModeUsePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(use)));

    private static ExhaustionModeUse? ReadExhaustionModeUse(string value)
    {
        if (!value.StartsWith(ExhaustionModeUsePrefix, StringComparison.Ordinal)) return null;
        try {
            var use = JsonSerializer.Deserialize<ExhaustionModeUse>(Encoding.UTF8.GetString(Convert.FromBase64String(value[ExhaustionModeUsePrefix.Length..])));
            return use is { SourceId.Length: > 0, Context.ExhaustionObserver: not null, Targets: not null }
                && value == ExhaustionModeMarker(use) ? use : null;
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException) { return null; }
    }

    private static bool SameExhaustionModeInstance(string source, FieldTriggerContext c, ExhaustionModeUse use)
        => source == use.SourceId && c.SourceGeneration == use.Context.SourceGeneration && c.CardNo == use.Context.CardNo
            && c.ExhaustionObserver!.InstanceId == use.Context.ExhaustionObserver!.InstanceId
            && c.ExhaustionObserver.FaceRevision == use.Context.ExhaustionObserver.FaceRevision;

    private static bool ExhaustionModeUsed(MatchState state, StackItemState item, string mode)
        => state.UntilEndOfTurnEffects.Select(ReadExhaustionModeUse).OfType<ExhaustionModeUse>()
            .Any(u => SameExhaustionModeInstance(item.SourceObjectId, item.FieldContext!, u) && u.Context.ExhaustionObserver!.Mode == mode);

    private static bool CanPayExhaustionModeBoon(MatchState state, StackItemState item)
        => state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == item.FieldContext!.SourceGeneration && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) == item.ControllerId
            && source.Tags.Contains(CardObjectTags.Boon);

    private static string[] ExhaustionModeTargets(MatchState state, StackItemState item)
    {
        return state.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c) && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && (!state.ObjectLocations.TryGetValue(c.ObjectId, out var location)
                || location.Zone is "BASE" or "BATTLEFIELD")
            && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] AvailableExhaustionModes(MatchState state, StackItemState item)
        => ExhaustionModeSpec(item.CardNo)!.Modes!.Where(m => !ExhaustionModeUsed(state, item, m.Id)
            && (!m.ConsumeSourceBoon || CanPayExhaustionModeBoon(state, item))
            && (!m.StunTarget || ExhaustionModeTargets(state, item).Length > 0)).Select(m => m.Id).ToArray();

    private static PendingCardChoiceState ExhaustionModeChoice(MatchState state, StackItemState item)
    {
        var choosingMode = item.FieldContext!.ExhaustionObserver!.Mode is null;
        var legal = choosingMode ? AvailableExhaustionModes(state, item) : ExhaustionModeTargets(state, item);
        return new("EXHAUSTION-MODE:" + item.StackItemId, choosingMode ? ExhaustionModeWindow : ExhaustionModeTargetWindow,
            item.ControllerId, 1, 1, legal, [], choosingMode
                ? "选择此技能实例本回合尚未选过的效果。" : "选择一名敌方单位，然后确认增益和法盾费用。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    internal static string ExhaustionModeLabel(MatchState state, PendingCardChoiceState choice, string mode)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { } item
            ? ExhaustionModeSpec(item.CardNo)?.Modes?.FirstOrDefault(m => m.Id == mode)?.Label ?? mode : mode;

    private static ResolutionResult PrepareExhaustionMode(ResolutionResult result, StackItemState item)
    {
        var choice = ExhaustionModeChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var next = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidExhaustionModeChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.FieldContext is not { Kind: SourceExhaustionModeKind, ExhaustionObserver.Confirmed: false }
            || !ValidFieldContextState(state, item.FieldContext, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            || !ValidExhaustionModeStack(state, item) || item.TargetGenerations is not null
            || item.FieldContext.ExhaustionObserver!.Mode is not null && SelectedExhaustionMode(item.FieldContext)?.StunTarget != true) return false;
        var expected = ExhaustionModeChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 1 && choice.MaxCount == 1 && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.LegalObjectIds.Count > 0 && choice.ContextObjectIds.Count == 0;
    }

    private static ResolutionResult ResolveExhaustionModeChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidExhaustionModeChoice(state, choice)) return RejectWithCorePrompts(state, "休眠技能的模式或目标已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null };
        var selectionEvent = new GameEvent(
            choice.ChoiceWindow == ExhaustionModeWindow ? "TRIGGER_MODE_SELECTED" : "TRIGGER_TARGET_SELECTED",
            choice.ChoiceWindow == ExhaustionModeWindow ? "已选择休眠技能模式" : "已选择休眠技能目标",
            new Dictionary<string, object?> {
                ["choiceId"] = choice.ChoiceId,
                ["playerId"] = choice.PlayerId,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
                ["objectIds"] = selected.ToArray()
            });
        var events = new List<GameEvent> { selectionEvent };
        if (choice.ChoiceWindow == ExhaustionModeWindow) {
            item = item with { FieldContext = item.FieldContext! with { ExhaustionObserver = item.FieldContext!.ExhaustionObserver! with { Mode = selected.Single() } } };
            next = next with { StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() };
            var mode = SelectedExhaustionMode(item.FieldContext!)!;
            if (mode.StunTarget) return PrepareExhaustionMode(new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
            if (!mode.ConsumeSourceBoon) return CompleteExhaustionMode(next, item, events);
        } else {
            item = item with { TargetObjectIds = selected.ToArray(), TargetGenerations = selected.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration) };
            next = next with { StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() };
        }
        var tax = ResolveSpellshieldTargetTaxPower(next, item.ControllerId, item.TargetObjectIds, out _);
        var payment = new PendingPaymentState("TRIGGER-WARD:" + item.StackItemId, TriggerTargetCostWindow, item.ControllerId,
            powerCost: tax, legalPaymentChoiceIds: ["PAY", "DECLINE"], reason: $"消耗来源增益并支付 {tax} 任意符能，或放弃此技能。") { ResolvingStackItemId = item.StackItemId };
        next = next with { PendingPayment = payment, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    internal static bool ValidExhaustionModePayment(MatchState state, PendingPaymentState payment)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == payment.ResolvingStackItemId) is { FieldContext: { Kind: SourceExhaustionModeKind, ExhaustionObserver.Confirmed: false } c } item
            && SelectedExhaustionMode(c) is { ConsumeSourceBoon: true } mode && ValidExhaustionModeStack(state, item)
            && ValidFieldContextState(state, c, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            && item.TargetObjectIds.Count == (mode.StunTarget ? 1 : 0)
            && payment.PaymentId == "TRIGGER-WARD:" + item.StackItemId && payment.PlayerId == item.ControllerId
            && payment.PaymentWindow == TriggerTargetCostWindow && payment.ManaCost == 0 && payment.ExperienceCost == 0
            && payment.PowerCost == ResolveSpellshieldTargetTaxPower(state, item.ControllerId, item.TargetObjectIds, out _)
            && payment.PowerCostByTrait.Count == 0 && payment.PaymentResourceActionIds.Count == 0
            && payment.LegalPaymentChoiceIds.SequenceEqual(["PAY", "DECLINE"])
            && state.PriorityPlayerId is null && state.PendingCardChoice is null;

    private static ResolutionResult ResolveExhaustionModePayment(MatchState state, PendingPaymentState payment, IReadOnlyList<string> choices, int rawCount)
    {
        if (!ValidExhaustionModePayment(state, payment) || rawCount != 1 || choices.Count != 1 || choices[0] is not ("PAY" or "DECLINE"))
            return RejectWithCorePrompts(state, "请选择支付休眠技能费用或放弃。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == payment.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingPayment = null };
        if (choices[0] == "DECLINE" || item.TargetObjectIds.Any(id => !ExhaustionModeTargets(state, item).Contains(id)
            || state.CardObjects[id].ObjectGeneration != item.TargetGenerations![id]))
            return DiscardUnconfirmedTrigger(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        if (!CanPayExhaustionModeBoon(state, item)) return RejectWithCorePrompts(state, "来源无法支付增益费用。", ErrorCodes.InvalidTarget);
        var plan = new PaymentCostRules.PaymentPlan(payment.PaymentId, payment.PaymentWindow, item.ControllerId,
            genericPowerCost: payment.PowerCost, totalPowerCost: payment.PowerCost, sourceObjectId: item.SourceObjectId);
        var paid = PaymentCostRules.TryCommitPayment(plan, state.RunePools, state.PlayerExperience, state: state);
        if (!paid.Accepted) return RejectWithCorePrompts(state, "资源不足；可使用资源技能或放弃。", ErrorCodes.InsufficientCost);
        next = next with { RunePools = paid.RunePools, PlayerExperience = paid.PlayerExperience };
        return CompleteExhaustionMode(next, item, [new("COST_PAID", "支付休眠技能费用", PaymentCostRules.BuildCostPaidPayload(plan,
            paid.RunePools, paid.PlayerExperience, new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["consumedBoonObjectIds"] = new[] { item.SourceObjectId } }))]);
    }

    private static ResolutionResult CompleteExhaustionMode(MatchState state, StackItemState item, List<GameEvent> events)
    {
        var c = item.FieldContext!; var mode = SelectedExhaustionMode(c)!;
        if (ExhaustionModeUsed(state, item, mode.Id) || mode.ConsumeSourceBoon && !CanPayExhaustionModeBoon(state, item))
            return RejectWithCorePrompts(state, "此模式已使用或无法支付增益费用。", ErrorCodes.InvalidTarget);
        c = c with { ExhaustionObserver = c.ExhaustionObserver! with { Confirmed = true } };
        item = item with { FieldContext = c };
        var use = new ExhaustionModeUse(item.SourceObjectId, c, item.TargetObjectIds.Select(id => {
            var target = state.CardObjects[id];
            return new ExhaustedUnitFact(new(id, target.ObjectGeneration), AttachedRuleTextRules.Face(target)!, target.CopyFaceTransitions?.Count ?? 0, target.TokenFactoryCardNo);
        }).ToArray(), state.Tick);
        var next = state with { PendingCardChoice = null, PendingPayment = null,
            UntilEndOfTurnEffects = [..state.UntilEndOfTurnEffects, ExhaustionModeMarker(use)],
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() };
        if (mode.ConsumeSourceBoon) {
            var zones = NormalizeZonesForSeats(next); var cards = next.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var locations = ReconcileObjectLocations(next.ObjectLocations, zones); var source = cards[item.SourceObjectId];
            using (new MightInstructionScope(next, zones, cards, events, locations))
                cards[source.ObjectId] = source with { Power = source.Power - 1, Tags = source.Tags.Where(t => t != CardObjectTags.Boon).ToArray() };
            events.Add(new("BOON_CONSUMED", "支付来源增益费用", new Dictionary<string, object?> {
                ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId, ["power"] = source.Power - 1 }));
            var cleanup = RunStateBasedCleanupLoop(next, zones, cards, item, next.RunePools,
                objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: next.DestroyedUnitOwnerIdsThisTurn);
            events.AddRange(cleanup.Events);
            next = next with { PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones),
                RunePools = cleanup.RunePools, TriggerQueue = next.TriggerQueue.Concat(cleanup.TriggerQueue).ToArray(),
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(next.DestroyedUnitOwnerIdsThisTurn, cleanup.DestroyedUnitOwnerIds) };
        }
        next = RestoreAfterTriggerConfirmation(next, item);
        events.Add(new("TRIGGER_MODE_CONFIRMED", "已确认休眠技能模式及费用", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(next, item.SourceObjectId, "__spectator__"), ["mode"] = mode.Id,
            ["targetObjectIds"] = item.TargetObjectIds.ToArray() }));
        return PrepareTriggerConfirmation(new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)));
    }

    private static FieldTriggerContext OriginalExhaustionModeContext(FieldTriggerContext c)
        => c with { SourceCeasedToExist = false, ExhaustionObserver = c.ExhaustionObserver! with { CeasedSource = null } };

    internal static bool ValidExhaustionModeUses(MatchState state)
    {
        var uses = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(ExhaustionModeUsePrefix, StringComparison.Ordinal)).Select(ReadExhaustionModeUse).ToArray();
        return uses.All(u => u is { Context.Kind: SourceExhaustionModeKind, Context.ExhaustionObserver.Confirmed: true }
            && u.ConfirmedTick >= u.Context.ExhaustionObserver.CapturedTick && u.ConfirmedTick <= state.Tick
            && ValidFieldContextState(state, u.Context, u.SourceId, u.Context.ExhaustionObserver.Actor, FieldTriggerEffect, u.Context.CardNo)
            && u.Targets.Count == (SelectedExhaustionMode(u.Context)!.StunTarget ? 1 : 0)
            && u.Targets.All(t => t is { Unit.Generation: >= 0, FaceRevision: >= 0 } && !string.IsNullOrWhiteSpace(t.CardNo)
                && PredictBindingMatches(state, t.Unit, t.CardNo, t.FaceRevision, u.ConfirmedTick, t.TokenFactoryCardNo)))
            && uses.Select(u => (u!.SourceId, u.Context.SourceGeneration, u.Context.CardNo, u.Context.ExhaustionObserver!.InstanceId,
                u.Context.ExhaustionObserver.FaceRevision, u.Context.ExhaustionObserver.Mode)).Distinct().Count() == uses.Length;
    }

    internal static bool ValidExhaustionModeStack(MatchState state, StackItemState item)
    {
        if (item.FieldContext?.Kind != SourceExhaustionModeKind) return true;
        var c = item.FieldContext; var birth = c.ExhaustionObserver; var mode = SelectedExhaustionMode(c);
        if (birth is null || item.OptionalCosts.Count != 0 || item.DamageAmount != 0 || item.EffectRepeatCount != 1
            || item.Destination.Length != 0 || item.SourceConfirmed) return false;
        if (item.TargetObjectIds.Count == 0 ? item.TargetGenerations is not null
            : mode?.StunTarget != true || item.TargetObjectIds.Count != 1 || item.TargetGenerations is null
                || item.TargetGenerations.Count != 1 || !item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0) return false;
        if (!birth.Confirmed) return (birth.Mode is null || mode is not null && !ExhaustionModeUsed(state, item, mode.Id))
            && (birth.Mode is not null || item.TargetObjectIds.Count == 0)
            && (item.TargetGenerations is null || state.PendingPayment is { PaymentWindow: TriggerTargetCostWindow } pending
                && pending.ResolvingStackItemId == item.StackItemId);
        return mode is not null && item.TargetObjectIds.Count == (mode.StunTarget ? 1 : 0)
            && state.UntilEndOfTurnEffects.Select(ReadExhaustionModeUse).OfType<ExhaustionModeUse>().Any(u =>
                JsonSerializer.Serialize(OriginalExhaustionModeContext(c)) == JsonSerializer.Serialize(OriginalExhaustionModeContext(u.Context))
                && u.SourceId == item.SourceObjectId && item.TargetObjectIds.SequenceEqual(u.Targets.Select(t => t.Unit.ObjectId))
                && u.Targets.All(t => item.TargetGenerations![t.Unit.ObjectId] == t.Unit.Generation));
    }

    private static StackResolutionResult ResolveExhaustionMode(MatchState state, StackItemState item)
    {
        if (!ValidExhaustionModeStack(state, item) || item.FieldContext!.ExhaustionObserver?.Confirmed != true)
            throw new InvalidOperationException("Invalid confirmed exhaustion mode.");
        var mode = SelectedExhaustionMode(item.FieldContext)!;
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal); var events = new List<GameEvent>();
        if (mode.StunTarget) {
            var id = item.TargetObjectIds.Single();
            if (ExhaustionModeTargets(state, item).Contains(id) && cards[id].ObjectGeneration == item.TargetGenerations![id] && CanExecuteStun(cards[id])) {
                cards[id] = cards[id] with { UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(cards[id].UntilEndOfTurnEffects, "STUNNED") };
                events.Add(CaptureStunObservers(new("STATUS_EFFECT_APPLIED", "休眠技能眩晕敌方单位", new Dictionary<string, object?> {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
                    ["targetObjectId"] = id, ["effectId"] = "STUNNED" }), state.PlayerZones, cards, item, id));
            }
        } else if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == item.FieldContext.SourceGeneration
            && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId)) {
            if (mode.BoonCount is not null) GrantLegendBoon(cards, source.ObjectId, item.ControllerId, source.ObjectId, item.EffectKind, events);
            if (mode.ReadySource) {
                cards[source.ObjectId] = ApplyReadyState(source, new(item.CardNo!, "休眠技能", 0, item.EffectKind, 0, 0), item, source.ObjectId,
                    out var ready, state.PlayerZones, cards); if (ready is not null) events.Add(ready);
            }
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
