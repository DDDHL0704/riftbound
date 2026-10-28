using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MightySkillContext(string CardNo, string Kind, ObjectBinding Source, ObjectBinding Unit,
    string ControllerId, int BeforeMight, int AfterMight, LastKnownMightFrame Before, LastKnownMightFrame After,
    string InstanceId, RuleTextInstanceBinding? Origin = null, bool Confirmed = false);

public sealed partial class CoreRuleEngine
{
    internal const string MightyReadyEffect = "MIGHTY_TRANSITION_READY";
    internal const string MightyRuneEffect = "MIGHTY_TRANSITION_CHANNEL";
    internal const string MightyCostWindow = "MIGHTY_SKILL_COST";
    internal const string MightyChoiceWindow = "MIGHTY_SKILL_CONFIRMATION";
    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> MightyRuneSpecs = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Select(c => (c.CardNo, Spec: RuleTextParser.Parse(c).Triggers.FirstOrDefault(t =>
                t.Kind == TriggerKinds.ControlledUnitBecamePowerfulExhaustCallRune)))
            .Where(p => p.Spec is not null).ToDictionary(p => p.CardNo, p => p.Spec!, StringComparer.Ordinal));
    private static TriggerSpec? MightySkillDefinition(string? cardNo)
        => UnitTriggerPaymentSpecRules.TryGetTrigger(cardNo,
            UnitTriggerPaymentSpecRules.IsUnitControlledUnitPowerfulPayPowerReadyTrigger, out var unit)
            ? unit : cardNo is null ? null : MightyRuneSpecs.Value.GetValueOrDefault(cardNo);

    private static int MightyBeforeEntry(CardObjectState after)
        => PrintedCardFactory.TryCreate(after.ObjectId, after.OwnerId ?? after.ControllerId ?? "", after.CardNo, out var printed)
            ? printed.Power : after.Power;

    private const string CapturedMightInstruction = "INTERNAL_STABLE_MIGHT_INSTRUCTION";
    private sealed record MightInstructionFrames(LastKnownMightFrame Before, LastKnownMightFrame After);

    // Only complete instruction results are observed. Numeric-layer iterations
    // are instantaneous and never produce intermediate Mighty crossings.
    private sealed class MightInstructionScope(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        List<GameEvent> events, IReadOnlyDictionary<string, ObjectLocationState>? locations = null) : IDisposable
    {
        private readonly LastKnownMightFrame before = CapturePublicMightFrame(state, zones, cards, locations);
        public void Dispose()
        {
            if (zones is Dictionary<string, PlayerZones> mutableZones && cards is Dictionary<string, CardObjectState> mutableCards)
                EndDetachedUnitCopies(state, mutableZones, mutableCards, events);
            var after = CapturePublicMightFrame(state, zones, cards, locations);
            events.Add(new(CapturedMightInstruction, "记录完整指令后的公开战力输入",
                new Dictionary<string, object?> { ["frames"] = new MightInstructionFrames(before, after) }));
        }
    }

    private static (CardObjectState Source, RuleTextInstance Instance, TriggerSpec Spec)[] MightySources(LastKnownMightFrame frame)
        => frame.CardObjects.Values.Where(c => !c.IsFaceDown
            && (IsObjectOnField(frame.PlayerZones, c.ObjectId) && c.Tags.Contains(CardObjectTags.UnitCard)
                || frame.PlayerZones.Values.Any(z => z.LegendZone.Contains(c.ObjectId))))
            .SelectMany(source => AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, source).Select(instance =>
                (Source: source, Instance: instance, Spec: MightySkillDefinition(instance.CardNo))))
            .Where(c => c.Spec is not null).Select(c => (c.Source, c.Instance, c.Spec!)).ToArray();

    private static IEnumerable<MightySkillContext> MightyCrossings(LastKnownMightFrame before, LastKnownMightFrame after)
    {
        var sources = MightySources(after); if (sources.Length == 0) yield break;
        var priorState = MightFrameState(before); var afterState = MightFrameState(after);
        foreach (var target in after.CardObjects.Values.Where(c => IsVisibleFieldUnitObject(after.CardObjects, c.ObjectId)
            && IsObjectOnField(after.PlayerZones, c.ObjectId))) {
            var previous = before.CardObjects.TryGetValue(target.ObjectId, out var old)
                && old.ObjectGeneration == target.ObjectGeneration && IsVisibleFieldUnitObject(before.CardObjects, old.ObjectId)
                && IsObjectOnField(before.PlayerZones, old.ObjectId)
                ? ResolveCurrentFieldUnitPower(priorState, old) : MightyBeforeEntry(target);
            var current = ResolveCurrentFieldUnitPower(afterState, target);
            if (previous >= PowerfulUnitPowerThreshold || current < PowerfulUnitPowerThreshold) continue;
            foreach (var observer in sources.Where(c => c.Source.ControllerId == target.ControllerId)) {
                var source = observer.Source; var instance = observer.Instance; var spec = observer.Spec;
                if (previous >= spec.RequiredPowerThreshold || current < spec.RequiredPowerThreshold) continue;
                var kind = spec.ExhaustsSource == true ? MightyRuneEffect : MightyReadyEffect;
                yield return new(instance.CardNo, kind, new(source.ObjectId, source.ObjectGeneration),
                    new(target.ObjectId, target.ObjectGeneration), target.ControllerId!, previous, current,
                    before, after, instance.Id, instance.Origin);
            }
        }
    }

    private static ResolutionResult RecordMightyTransitions(MatchState before, ResolutionResult result)
    {
        var steps = result.Events.Where(e => e.Kind == CapturedMightInstruction)
            .Select(e => (MightInstructionFrames)e.Payload["frames"]!).ToArray();
        var events = result.Events.Where(e => e.Kind != CapturedMightInstruction).ToList();
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress)
            return result with { Events = events };
        var state = result.State; var queue = state.TriggerQueue.ToList();
        if (steps.Length == 0 && !state.CardObjects.Values.Any(c =>
            (IsObjectOnField(state.PlayerZones, c.ObjectId) || state.PlayerZones.Values.Any(z => z.LegendZone.Contains(c.ObjectId)))
            && AttachedRuleTextRules.Instances(state, c).Any(i => MightySkillDefinition(i.CardNo) is not null)))
            return result;
        var previous = CapturePublicMightFrame(before, before.PlayerZones, before.CardObjects);
        var index = 0;
        void Record(LastKnownMightFrame after)
        {
            foreach (var context in MightyCrossings(previous, after)) {
                var trigger = new TriggerQueueItemState($"MIGHTY:{state.Tick}:{index}:{context.InstanceId}:{context.Unit.ObjectId}",
                    context.ControllerId, context.Source.ObjectId, context.Kind, "UNIT_BECAME_POWERFUL") {
                        SourceCardNo = context.CardNo, MightySkill = context };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
            previous = after; index++;
        }
        foreach (var step in steps) { Record(step.Before); Record(step.After); }
        Record(CapturePublicMightFrame(state, state.PlayerZones, state.CardObjects));
        return result with { State = state with { TriggerQueue = queue }, Events = events };
    }

    internal static bool ValidMightySkillFrame(MightySkillContext context)
    {
        try {
            if (context.Kind == PowerfulPlayedRuneEffect) return ValidPowerfulPlayedRuneFrame(context);
            if (MightySkillDefinition(context.CardNo) is not { } spec
                || context.Kind != (spec.ExhaustsSource == true ? MightyRuneEffect : MightyReadyEffect)
                || context.BeforeMight >= spec.RequiredPowerThreshold || context.AfterMight < spec.RequiredPowerThreshold
                || context.Source.Generation < 0 || context.Unit.Generation < 0
                || !context.After.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
                || source.ObjectGeneration != context.Source.Generation || source.ControllerId != context.ControllerId
                || !context.After.CardObjects.TryGetValue(context.Unit.ObjectId, out var target)
                || target.ObjectGeneration != context.Unit.Generation || target.ControllerId != context.ControllerId
                || !IsObjectOnField(context.After.PlayerZones, target.ObjectId)
                || !IsVisibleFieldUnitObject(context.After.CardObjects, target.ObjectId)
                || (spec.ExhaustsSource == true ? !context.After.PlayerZones[context.ControllerId].LegendZone.Contains(source.ObjectId)
                    : !IsObjectOnField(context.After.PlayerZones, source.ObjectId) || !IsVisibleFieldUnitObject(context.After.CardObjects, source.ObjectId))
                || !AttachedRuleTextRules.Instances(MightFrameState(context.After), source).Any(i =>
                    i.Id == context.InstanceId && i.CardNo == context.CardNo && i.Origin == context.Origin)) return false;
            foreach (var frame in new[] { context.Before, context.After }) {
                var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                    .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
                if (!ValidMightFrameExperience(frame)
                    || frame.PlayerZones.Values.Any(z => z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                    || frame.CardObjects.Any(p => p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                        || !publicIds.Contains(p.Key))
                    || frame.ObjectLocations.Keys.Any(id => !frame.CardObjects.ContainsKey(id))
                    || frame.BattleDeclarations.Any(e => !e.StartsWith(BattleResponseDeclarationContextPrefix, StringComparison.Ordinal))) return false;
            }
            var previous = context.Before.CardObjects.TryGetValue(target.ObjectId, out var old)
                && old.ObjectGeneration == target.ObjectGeneration && IsObjectOnField(context.Before.PlayerZones, old.ObjectId)
                ? ResolveCurrentFieldUnitPower(MightFrameState(context.Before), old) : MightyBeforeEntry(target);
            return previous == context.BeforeMight && ResolveCurrentFieldUnitPower(MightFrameState(context.After), target) == context.AfterMight;
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    internal static bool ValidMightySkill(MatchState state, MightySkillContext context, string effect, string source, string controller)
        => ValidMightySkillFrame(context) && effect == context.Kind && source == context.Source.ObjectId
            && controller == context.ControllerId && state.Seats.ContainsKey(controller)
            && (context.Kind != PowerfulPlayedRuneEffect || AttachedRuleTextRules.ValidCaptured(state, context.Origin,
                source, context.Source.Generation, context.CardNo))
            && new[] { context.Before, context.After }.All(frame => frame.PlayerZones.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Seats.Keys)
                && frame.CardObjects.All(p => state.CardObjects.TryGetValue(p.Key, out var current)
                    ? current.ObjectGeneration >= p.Value.ObjectGeneration
                        && (current.ObjectGeneration != p.Value.ObjectGeneration || current.CardNo == p.Value.CardNo)
                    : IsTokenObject(p.Value)));

    private static ResolutionResult PrepareMightySkill(ResolutionResult result, StackItemState item)
    {
        var context = item.MightySkill!; var state = result.State; var spec = MightySkillDefinition(context.CardNo);
        if (!ValidMightySkill(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId))
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        if (context.Kind == PowerfulPlayedRuneEffect || spec!.ExhaustsSource == true) {
            var legal = state.CardObjects.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.Source.Generation
                && source.ControllerId == item.ControllerId && !source.IsExhausted && !source.IsFaceDown
                && state.PlayerZones[item.ControllerId].LegendZone.Contains(source.ObjectId) ? new[] { source.ObjectId } : [];
            if (legal.Length == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
            state = state with { PendingCardChoice = new("MIGHTY-COST:" + item.StackItemId, MightyChoiceWindow, item.ControllerId,
                0, 1, legal, context.Kind == PowerfulPlayedRuneEffect ? [] : [context.Unit.ObjectId], "横置此传奇以确认召出符文；不选则放弃。确认后双方响应。", item.SourceObjectId,
                item.EffectKind) { ResolvingStackItemId = item.StackItemId }, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        } else {
            var costs = new Dictionary<string, int> { [spec!.PowerCostTrait!] = spec.PowerCost!.Value };
            state = state with { PendingPayment = new("MIGHTY-COST:" + item.StackItemId, MightyCostWindow, item.ControllerId,
                powerCostByTrait: costs, legalPaymentChoiceIds: [MightySpendChoice(spec), "DECLINE"],
                paymentResourceActionIds: RecycleRunePaymentResourceActionIds(state, item.ControllerId),
                reason: "支付符能以确认活跃技能；支付后双方响应。") {
                    ResolvingStackItemId = item.StackItemId }, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        }
        var events = result.Events.ToList();
        if (state.PendingPayment is { } payment) events.Add(new("PAYMENT_WINDOW_OPENED", "确认强力技能费用",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["paymentId"] = payment.PaymentId,
                ["paymentWindow"] = payment.PaymentWindow, ["trigger"] = context.Kind, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = context.Unit.ObjectId, ["previousPower"] = context.BeforeMight,
                ["resultingPower"] = context.AfterMight, ["paymentChoices"] = payment.LegalPaymentChoiceIds.ToArray(),
                ["paymentResourceActions"] = payment.PaymentResourceActionIds.ToArray() }));
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static string MightySpendChoice(TriggerSpec spec) => $"SPEND_POWER:{spec.PowerCostTrait}:{spec.PowerCost}";

    private static MatchState RefreshMightyPaymentResources(MatchState before, MatchState after)
    {
        if (before.PendingPayment is not { PaymentWindow: MightyCostWindow } prior
            || after.PendingPayment is not { PaymentWindow: MightyCostWindow } current
            || current.PaymentId != prior.PaymentId || !ValidMightySkillPayment(before, prior)) return after;
        return after with { PendingPayment = current with {
            PaymentResourceActionIds = RecycleRunePaymentResourceActionIds(after, current.PlayerId) } };
    }

    internal static bool ValidMightySkillPayment(MatchState state, PendingPaymentState pending)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == pending.ResolvingStackItemId);
        if (item?.MightySkill is not { Confirmed: false } context || context.Kind != MightyReadyEffect
            || !ValidMightySkill(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId)
            || MightySkillDefinition(context.CardNo) is not { } spec) return false;
        return pending.PaymentWindow == MightyCostWindow && pending.PaymentId == "MIGHTY-COST:" + item.StackItemId
            && pending.PlayerId == item.ControllerId && pending.ManaCost == 0 && pending.PowerCost == 0
            && pending.PowerCostByTrait.Count == 1 && pending.PowerCostByTrait.GetValueOrDefault(spec.PowerCostTrait!) == spec.PowerCost
            && pending.ExperienceCost == 0 && pending.LegalPaymentChoiceIds.SequenceEqual(new[] { MightySpendChoice(spec), "DECLINE" })
            && pending.PaymentResourceActionIds.SequenceEqual(RecycleRunePaymentResourceActionIds(state, item.ControllerId));
    }

    internal static bool ValidMightySkillChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.MightySkill is not { Confirmed: false } context || context.Kind is not (MightyRuneEffect or PowerfulPlayedRuneEffect)
            || !ValidMightySkill(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId)
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.IsExhausted
            || source.ObjectGeneration != context.Source.Generation || source.ControllerId != item.ControllerId
            || source.IsFaceDown || !state.PlayerZones[item.ControllerId].LegendZone.Contains(source.ObjectId)) return false;
        return choice.ChoiceWindow == MightyChoiceWindow && choice.ChoiceId == "MIGHTY-COST:" + item.StackItemId
            && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.LegalObjectIds.SequenceEqual(new[] { item.SourceObjectId })
            && choice.ContextObjectIds.SequenceEqual(context.Kind == PowerfulPlayedRuneEffect ? [] : new[] { context.Unit.ObjectId });
    }

    private static ResolutionResult ConfirmMightySkill(MatchState state, StackItemState item, bool confirm,
        Dictionary<string, CardObjectState>? cards = null, IReadOnlyDictionary<string, RunePool>? pools = null,
        List<GameEvent>? events = null, IReadOnlyDictionary<string, PlayerZones>? zones = null,
        IReadOnlyDictionary<string, ObjectLocationState>? locations = null)
    {
        var next = RestoreAfterTriggerConfirmation(state with { Tick = state.Tick + 1, PendingPayment = null, PendingCardChoice = null,
            CardObjects = cards ?? state.CardObjects, RunePools = pools ?? state.RunePools,
            PlayerZones = zones ?? state.PlayerZones, ObjectLocations = locations ?? state.ObjectLocations,
            StackItems = state.StackItems.Where(i => confirm || i.StackItemId != item.StackItemId)
                .Select(i => i.StackItemId == item.StackItemId ? i with { MightySkill = item.MightySkill! with { Confirmed = true } } : i).ToArray() }, item);
        events ??= [];
        if (!confirm) events.Add(new("TRIGGER_PAYMENT_DECLINED", "放弃强力技能",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["trigger"] = item.EffectKind }));
        if (state.PendingPayment is { } payment) events.Add(BuildPaymentWindowClosedEvent(payment, item.ControllerId, !confirm));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static ResolutionResult ResolveMightySkillChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (!ValidMightySkillChoice(state, choice) || item?.MightySkill is not { Confirmed: false } context || context.Kind is not (MightyRuneEffect or PowerfulPlayedRuneEffect)
            || selected.Count > 1 || selected.Any(id => id != context.Source.ObjectId)
            || !ValidMightySkill(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId))
            return RejectWithCorePrompts(state, "强力技能确认选择无效。", ErrorCodes.InvalidTarget);
        if (selected.Count == 0) return ConfirmMightySkill(state, item, false);
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.IsExhausted
            || source.ObjectGeneration != context.Source.Generation || source.ControllerId != item.ControllerId)
            return RejectWithCorePrompts(state, "来源无法支付休眠费用。", ErrorCodes.InvalidTarget);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) { [source.ObjectId] = source with { IsExhausted = true } };
        if (context.Kind == PowerfulPlayedRuneEffect)
            return ConfirmMightySkill(state, item, true, cards, events: [new("LEGEND_EXHAUSTED", "横置传奇以确认强力技能",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId,
                    ["legendObjectId"] = source.ObjectId, ["legendCardNo"] = context.CardNo, ["reason"] = item.EffectKind })]);
        return ConfirmMightySkill(state, item, true, cards, events: [new("LEGEND_EXHAUSTED", "横置传奇以确认强力技能",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId, ["reason"] = item.EffectKind })]);
    }

    private static ResolutionResult ResolveMightySkillPayment(MatchState state, PlayerIntent intent, PendingPaymentState pending,
        IReadOnlyList<string> choices, int rawCount)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == pending.ResolvingStackItemId);
        var spec = item?.MightySkill is { } captured ? MightySkillDefinition(captured.CardNo) : null;
        var resources = choices.Where(IsPaymentResourceActionId).ToArray();
        var spend = choices.Where(c => !IsPaymentResourceActionId(c)).ToArray();
        if (!ValidMightySkillPayment(state, pending) || item?.MightySkill is not { Confirmed: false } context || context.Kind != MightyReadyEffect
            || rawCount != choices.Count || choices.Count != choices.Distinct(StringComparer.Ordinal).Count()
            || spec is null || spend.Length != 1 || spend[0] != MightySpendChoice(spec) && spend[0] != "DECLINE"
            || resources.Any(c => !pending.PaymentResourceActionIds.Contains(c)) || spend[0] == "DECLINE" && resources.Length != 0
            || !ValidMightySkill(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId))
            return RejectWithCorePrompts(state, "强力技能付款选择无效。", ErrorCodes.InvalidTarget);
        if (spend[0] == "DECLINE") return ConfirmMightySkill(state, item, false);
        if (!TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, resources, out var optional, out var actions, out var runeIds)
            || optional.Count != 0)
            return RejectWithCorePrompts(state, "支付资源动作无效。", ErrorCodes.InvalidTarget);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value); var zones = NormalizeZonesForSeats(state);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones); var events = new List<GameEvent>();
        var pools = ApplyRecycleRunePaymentResourceActions(state.RunePools, zones, cards, locations, intent.PlayerId, runeIds,
            events, pending.PaymentWindow, pending.PaymentId);
        var plan = BuildPendingPaymentPlan(pending, intent.PlayerId, item.EffectKind, item.SourceObjectId, paymentResourceActions: actions);
        var paid = PaymentCostRules.TryCommitPayment(plan, pools, state: state);
        if (!paid.Accepted) return RejectWithCorePrompts(state, "符能不足，可先使用符文资源或放弃。", ErrorCodes.InsufficientCost);
        events.Add(new("COST_PAID", "支付强力技能费用", PaymentCostRules.BuildCostPaidPayload(plan, paid.RunePools, null,
            new Dictionary<string, object?> { ["reason"] = item.EffectKind, ["paymentChoiceIds"] = choices.ToArray(),
                ["legalPaymentChoiceIds"] = pending.LegalPaymentChoiceIds.ToArray(), ["recycledRuneObjectIds"] = runeIds.ToArray(),
                ["powerByTrait"] = pending.PowerCostByTrait, ["targetObjectId"] = context.Unit.ObjectId })));
        return ConfirmMightySkill(state, item, true, cards, paid.RunePools, events, zones, ReconcileObjectLocations(locations, zones));
    }

    private static StackResolutionResult ResolveMightySkill(MatchState state, StackItemState item)
    {
        var context = item.MightySkill!;
        if (!context.Confirmed || !ValidMightySkill(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId))
            throw new InvalidOperationException("Invalid confirmed Mighty skill.");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value); var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent>();
        if (context.Kind == MightyReadyEffect) {
            if (cards.TryGetValue(context.Unit.ObjectId, out var target) && target.ObjectGeneration == context.Unit.Generation
                && IsVisibleFieldUnitObject(cards, target.ObjectId) && IsObjectOnField(zones, target.ObjectId)) {
                cards[target.ObjectId] = target with { IsExhausted = false };
                events.Add(new("UNIT_READIED", "强力技能结算活跃单位", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["targetObjectId"] = target.ObjectId, ["reason"] = item.EffectKind }));
            }
        } else {
            var called = CallRunes(zones, cards, item.ControllerId,
                context.Kind == PowerfulPlayedRuneEffect ? 1 : MightySkillDefinition(context.CardNo)!.RuneCallCount!.Value);
            events.Add(new("RUNES_CALLED", "强力技能结算召出休眠符文", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = context.Kind == PowerfulPlayedRuneEffect
                    ? ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") : item.SourceObjectId,
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray() }));
        }
        var payload = new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["trigger"] = item.EffectKind, ["sourceObjectId"] = item.SourceObjectId,
            ["targetObjectId"] = context.Unit.ObjectId };
        if (context.Kind == PowerfulPlayedRuneEffect) {
            payload["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
            payload.Remove("targetObjectId");
            events.Add(new("LEGEND_TRIGGER_RESOLVED", "强力单位传奇技能结算", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId,
                ["legendObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
                ["legendCardNo"] = context.CardNo, ["trigger"] = "POWERFUL_UNIT_PLAYED_CALL_RUNE",
                ["playedUnitObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, context.Unit.ObjectId, "__spectator__"),
                ["playedUnitPower"] = context.AfterMight }));
        }
        events.Add(new("TRIGGER_RESOLVED", "强力技能结算", payload));
        return new(zones, cards, state.PlayerScores, NormalizeExperienceForSeats(state), state.RunePools,
            state.UntilEndOfTurnEffects, null, events, [], null, [], null, [], state.RngCursor);
    }
}
