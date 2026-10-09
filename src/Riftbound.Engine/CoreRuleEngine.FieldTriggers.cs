using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TurnDrawLedger(int TurnNumber, IReadOnlyDictionary<string, int> Counts);
public sealed record FieldTriggerContext(string CardNo, string Kind, long SourceGeneration, string? BattlefieldId = null, string? ReturnFocusPlayerId = null);
internal sealed record CapturedDrawSource(string SourceId, string ControllerId, FieldTriggerContext Context);

public sealed partial class CoreRuleEngine
{
    internal const string FieldTriggerEffect = "FIELD_TARGETED_TRIGGER";
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> FieldTriggerFamilies = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-074/219", "SFD·128/221"]));
    private static bool HasFieldTrigger(string? cardNo, string kind) => FieldTriggerFamilies.Value[kind == "SECOND_DRAW" ? "UNL-074/219" : "SFD·128/221"]
        .Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), StringComparer.Ordinal);
    internal static bool ValidFieldContext(FieldTriggerContext context, string effect, string? cardNo = null)
        => effect == FieldTriggerEffect && context.SourceGeneration >= 0 && context.Kind is "SECOND_DRAW" or "DEFEND"
            && HasFieldTrigger(context.CardNo, context.Kind) && (cardNo is null || cardNo == context.CardNo)
            && (context.Kind == "SECOND_DRAW" ? context.BattlefieldId is null : !string.IsNullOrWhiteSpace(context.BattlefieldId));

    private static CapturedDrawSource[] CaptureDrawTriggerSources(MatchState state, string player) => CaptureDrawTriggerSources(state.PlayerZones, state.CardObjects, player);

    private static CapturedDrawSource[] CaptureDrawTriggerSources(IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, string player) => cards.Values
        .Where(c => c.ControllerId == player && !c.IsFaceDown && !c.Tags.Contains(CardObjectTags.Standby)
            && IsObjectOnField(zones, c.ObjectId) && HasFieldTrigger(c.CardNo, "SECOND_DRAW"))
        .OrderBy(c => c.ObjectId, StringComparer.Ordinal)
        .Select(c => new CapturedDrawSource(c.ObjectId, player, new(c.CardNo!, "SECOND_DRAW", c.ObjectGeneration))).ToArray();

    private static ResolutionResult RecordDrawTriggers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || result.State.Phase is not (MatchPhases.Main or MatchPhases.TurnStart or MatchPhases.TurnEnd)) return result;
        var state = result.State;
        var counts = (state.DrawLedger.TurnNumber == state.TurnNumber ? state.DrawLedger.Counts : new Dictionary<string, int>())
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        // The source snapshot is internal to this resolution, never part of the public event log.
        var events = result.Events.Select(e => e.Payload.ContainsKey("drawTriggerSources")
            ? e with { Payload = e.Payload.Where(p => p.Key != "drawTriggerSources").ToDictionary(p => p.Key, p => p.Value) } : e).ToList();
        var queue = state.TriggerQueue.ToList();
        for (var index = 0; index < result.Events.Count; index++)
        {
            var ev = result.Events[index];
            if (ev.Kind != "CARD_DRAWN" || !ev.Payload.TryGetValue("playerId", out var p) || p is not string player
                || !ev.Payload.TryGetValue("count", out var n) || n is not int amount || amount <= 0) continue;
            var prior = counts.GetValueOrDefault(player); counts[player] = checked(prior + amount);
            if (prior >= 2 || counts[player] < 2 || state.Status != MatchStatuses.InProgress) continue;
            var sources = ev.Payload.TryGetValue("drawTriggerSources", out var captured) && captured is CapturedDrawSource[] recorded
                ? recorded : CaptureDrawTriggerSources(before, player);
            foreach (var source in sources)
            {
                var trigger = new TriggerQueueItemState($"draw-{state.TurnNumber}-{state.Tick}-{index}-{source.SourceId}", source.ControllerId,
                    source.SourceId, FieldTriggerEffect, "SECOND_CARD_DRAWN",
                    before.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : before.BattleState.IsActive ? TimingStates.NeutralClosed : null)
                    { FieldContext = source.Context with { ReturnFocusPlayerId = state.FocusPlayerId } };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        state = state with { DrawLedger = new(state.TurnNumber, counts), TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static string[] FieldTriggerTargets(MatchState state, StackItemState item) => state.CardObjects.Values
        .Where(c => c.Tags.Contains(CardObjectTags.UnitCard) && !c.IsFaceDown && !c.Tags.Contains(CardObjectTags.Standby)
            && IsObjectOnField(state.PlayerZones, c.ObjectId) && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId)
            && (item.FieldContext!.Kind == "SECOND_DRAW" ? c.ControllerId == item.ControllerId
                : c.ControllerId != item.ControllerId && state.BattleState.AttackerObjectIds.Contains(c.ObjectId)
                    && state.ObjectLocations.TryGetValue(c.ObjectId, out var location) && location.BattlefieldObjectId == item.FieldContext.BattlefieldId))
        .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static ResolutionResult PrepareTriggerConfirmation(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var state = result.State;
        if (state.PendingCardChoice is not null || state.PendingPayment is not null || state.PendingHandChoice is not null
            || state.PendingEffectPlay is not null || state.TriggerQueue.Any(IsImmediateTrigger)) return result;
        // Confirm pending items in insertion order, regardless of which rule family
        // supplies their choices or costs (CN 337.1.b).
        var item = state.StackItems.FirstOrDefault(i =>
            NeedsTriggerCostConfirmation(i)
            || i.ReflexiveCopy is { TargetConfirmed: false }
            || i.SpellContext is { } spell && NeedsSpellTriggerChoice(spell) && i.TargetGenerations is null
            || i.FieldContext is not null && i.TargetGenerations is null
            || i.InsightContext is { Kind: "DUEL", PaymentAccepted: false });
        if (item is null) return result;
        if (NeedsTriggerCostConfirmation(item)) return PrepareTriggerCostConfirmation(result, item);
        if (item.ReflexiveCopy is { TargetConfirmed: false }) return PrepareReflexiveCopyConfirmation(result);
        if (item.SpellContext is not null) return PrepareSpellTriggerConfirmation(result);
        PendingCardChoiceState? choice = null; PendingPaymentState? payment = null;
        if (item.InsightContext is not null)
            payment = new("INSIGHT-PAY:" + item.StackItemId, "INSIGHT_EFFECT", item.ControllerId,
                manaCost: 1, legalPaymentChoiceIds: ["PAY", "DECLINE"], reason: "黛安娜：支付 1 法力确认技能，随后对手可以响应")
                { ResolvingStackItemId = item.StackItemId };
        else
        {
            var legal = FieldTriggerTargets(state, item);
            if (legal.Length == 0 || item.FieldContext!.Kind == "DEFEND"
                && (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration != item.FieldContext.SourceGeneration
                    || source.ControllerId != item.ControllerId || !IsObjectOnField(state.PlayerZones, item.SourceObjectId)))
                return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
            choice = new("TRIGGER-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
                item.FieldContext.Kind == "DEFEND" ? 0 : 1, 1, legal, legal,
                item.FieldContext.Kind == "DEFEND" ? "选择一名进攻单位并摧毁狂热粉丝，或不选以放弃；确认后开放响应" : "选择一名友方单位获得本回合 +2 战力；确认后开放响应",
                item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
        }
        state = state with { PendingCardChoice = choice, PendingPayment = payment, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static MatchState RestoreAfterTriggerConfirmation(MatchState state, StackItemState removed)
    {
        if (state.StackItems.Count > 0) return state with { ActivePlayerId = state.StackItems[^1].ControllerId,
            PriorityPlayerId = state.StackItems[^1].ControllerId, PassedPriorityPlayerIds = [] };
        if (removed.InsightContext is { Kind: "DUEL" } insight)
            return state with { TimingState = TimingStates.SpellDuelOpen, ActivePlayerId = insight.ReturnFocusPlayerId!,
                FocusPlayerId = insight.ReturnFocusPlayerId, PriorityPlayerId = null, PassedPriorityPlayerIds = [], PassedFocusPlayerIds = [] };
        if (removed.TimingContext == TimingStates.SpellDuelOpen)
        {
            var focus = removed.FieldContext?.ReturnFocusPlayerId ?? state.FocusPlayerId ?? NextPlayerIdAfter(state, removed.ControllerId);
            return state with { TimingState = TimingStates.SpellDuelOpen, ActivePlayerId = focus,
                FocusPlayerId = focus, PriorityPlayerId = null, PassedPriorityPlayerIds = [], PassedFocusPlayerIds = [] };
        }
        if (state.BattleState.IsActive) return state with { TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = BattleResponsePriorityPlayerId(state, removed.ControllerId), PassedPriorityPlayerIds = [] };
        return state with { TimingState = TimingStates.NeutralOpen, ActivePlayerId = state.TurnPlayerId, PriorityPlayerId = null };
    }

    private static ResolutionResult DiscardUnconfirmedTrigger(ResolutionResult result, StackItemState item)
    {
        var state = RestoreAfterTriggerConfirmation(result.State with { StackItems = result.State.StackItems.Where(i => i.StackItemId != item.StackItemId).ToArray() }, item);
        return result with { State = state, Events = result.Events.Append(new GameEvent("TRIGGER_NOT_CONFIRMED", "未确认触发技能",
            new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId })).ToArray(),
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidTriggerChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { FieldContext: { } context } item
            && ValidFieldContext(context, item.EffectKind, item.CardNo) && item.TargetGenerations is null
            && item.ControllerId == choice.PlayerId && item.SourceObjectId == choice.SourceObjectId
            && choice.ChoiceId == "TRIGGER-TARGET:" + item.StackItemId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == (context.Kind == "DEFEND" ? 0 : 1) && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(FieldTriggerTargets(state, item))
            || ValidReflexiveCopyChoice(state, choice);

    private static ResolutionResult ResolveTriggerTargetConfirmation(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidTriggerChoice(state, choice)) return RejectWithCorePrompts(state, "触发技能确认上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null };
        if (selected.Count == 0) return DiscardUnconfirmedTrigger(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        var events = new List<GameEvent>();
        var tax = ResolveSpellshieldTargetTaxPower(state, item.ControllerId, selected, out _);
        if (tax > 0)
        {
            // CN 809: the additional cost is generic power, not mana.
            var plan = new PaymentCostRules.PaymentPlan("TRIGGER-COST:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
                genericPowerCost: tax, totalPowerCost: tax, sourceObjectId: item.SourceObjectId);
            var payment = PaymentCostRules.TryCommitPayment(plan, state.RunePools, state.PlayerExperience);
            if (!payment.Accepted) return RejectWithCorePrompts(state, "需要支付法盾的任意特性符能，可先回收符文。", ErrorCodes.InsufficientCost);
            next = next with { RunePools = payment.RunePools, PlayerExperience = payment.PlayerExperience };
            events.Add(new("COST_PAID", "支付法盾费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience, new Dictionary<string, object?>())));
        }
        if (item.FieldContext?.Kind == "DEFEND")
        {
            var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
            if (!cards.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration != item.FieldContext.SourceGeneration
                || source.ControllerId != item.ControllerId || !IsObjectOnField(zones, item.SourceObjectId))
                return RejectWithCorePrompts(state, "狂热粉丝已无法支付摧毁费用。", ErrorCodes.InvalidTarget);
            var removal = ResolveFieldDestructions(zones, cards, item, new HashSet<string>(), state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(),
                next.RunePools, objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { item.SourceObjectId });
            if (removal.Events.Count == 0) return RejectWithCorePrompts(state, "无法支付摧毁费用。", ErrorCodes.InvalidTarget);
            next = next with { PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                RunePools = removal.RunePools, TriggerQueue = state.TriggerQueue.Concat(removal.TriggerQueue).ToArray(),
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, removal.DestroyedUnitOwnerIds) };
            events.AddRange(removal.Events);
        }
        var confirmed = item with { TargetObjectIds = selected.ToArray(), TargetGenerations = selected.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration) };
        if (item.ReflexiveCopy is { } copy)
            confirmed = confirmed with { ReflexiveCopy = copy with {
                CopySource = new(selected[0], state.CardObjects[selected[0]].ObjectGeneration), TargetConfirmed = true } };
        next = RestoreAfterTriggerConfirmation(next with { StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? confirmed : i).ToArray() }, item);
        events.Add(new("TRIGGER_CONFIRMED", "已确认触发技能目标与费用，等待响应", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectIds"] = selected.ToArray() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveFieldTrigger(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var events = new List<GameEvent>(); var triggers = new List<TriggerQueueItemState>();
        var target = item.TargetObjectIds.SingleOrDefault();
        if (target is not null && item.TargetGenerations?.GetValueOrDefault(target, -1) == cards.GetValueOrDefault(target)?.ObjectGeneration
            && FieldTriggerTargets(state, item).Contains(target))
        {
            var card = cards[target];
            if (item.FieldContext!.Kind == "SECOND_DRAW")
            {
                cards[target] = ApplyDirectUntilEndPowerModifier(card, target, item.SourceObjectId, item.CardNo,
                    item.EffectKind, "SECOND_CARD_DRAWN", 2, card.Power + 2);
                events.Add(new("UNIT_POWER_CHANGED", "冰封宝石：本回合战力 +2", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target, ["powerDelta"] = 2 }));
            }
            else
            {
                var controller = card.ControllerId!; var equipment = AttachedEquipmentObjectIds(cards, target).ToArray();
                if (!BattlefieldLocalRules.PreventsMoveToBase(state, target) && TryMoveTargetToOwnerBase(zones, cards, item.ControllerId, target, out _))
                {
                    cards[target] = card with { IsAttacking = false, IsDefending = false };
                    events.Add(new("UNIT_MOVED_TO_BASE", "狂热粉丝将进攻单位移动到其基地", new Dictionary<string, object?> {
                        ["playerId"] = controller, ["sourceObjectId"] = target, ["targetObjectId"] = target,
                        ["effectSourceObjectId"] = item.SourceObjectId, ["originZone"] = "BATTLEFIELD", ["destinationZone"] = "BASE",
                        ["origin"] = "BATTLEFIELD:" + item.FieldContext.BattlefieldId, ["destination"] = "BASE" }));
                    events.AddRange(MoveAttachedEquipmentWithHost(zones, equipment, controller, target, "BASE"));
                    events.AddRange(ResolveUnitMovedCreateDormantGoldTrigger(zones, cards, controller, target, "BATTLEFIELD", "BASE"));
                    var moved = state with { PlayerZones = zones, CardObjects = cards };
                    var resource = BuildJhinMovementResourceTrigger(moved, controller, target, card, "BATTLEFIELD:" + item.FieldContext.BattlefieldId, "BASE");
                    if (resource is not null) { triggers.Add(resource); events.Add(BuildTriggerQueuedEvent(resource)); }
                }
            }
        }
        events.Add(new("TRIGGER_RESOLVED", "触发技能结算完成", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events, TriggerQueue = triggers,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
