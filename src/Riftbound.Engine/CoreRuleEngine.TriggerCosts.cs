using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TriggerCostReceipt(ObjectBinding Source, ObjectBinding? Discarded = null, int Power = 0, int Mana = 0, ObjectBinding? DestructionTarget = null);

public sealed partial class CoreRuleEngine
{
    internal const string TriggerCostWindow = "TRIGGER_COST_CONFIRMATION";
    internal const string OptionalTriggerWindow = "TRIGGER_OPTIONAL_CONFIRMATION";
    private sealed record LeadingCost(bool Exhaust = false, bool Discard = false, int Power = 0, int Mana = 0, bool DestroyLocalUnit = false, string? PowerTrait = null);

    // CN 204.1.c: leading instruction costs are paid while confirming a trigger,
    // before responses. A paid receipt follows the captured stack item, not its live source.
    private static LeadingCost? LeadingTriggerCost(StackItemState item) => item.UnitConquestReturn is { } unitReturn && UnitConquestReturnSpec(unitReturn.CardNo) is { } returnSpec ? new(Mana: returnSpec.ManaCost ?? 0) : item.GraveyardPlay is { } graveyard && GraveyardPlaySpec(graveyard.CardNo) is { } graveyardSpec ? new(Power: graveyardSpec.PowerCost!.Value, Mana: graveyardSpec.ManaCost!.Value, PowerTrait: graveyardSpec.PowerCostTrait) : CreatedTokenReadyHasPayment(item) ? new(Power: 1, PowerTrait: CardBehaviorRegistry.TryGetByCardNo(item.CreatedTokenReady!.CardNo, out var ready) ? ready.CreatedTokenReadyPowerTrait : null) : item.SivirRuneRecycle is not null ? new(Exhaust: true) : item.BattlefieldConquestMight is { } conquestMight ? new(Mana: BattlefieldConquestMightDefinition(conquestMight.CardNo)?.ManaCost ?? 0) : item.AttachmentObserver is { } attached && AttachmentDrawSpec(attached.CardNo) is { } drawSpec ? new(Mana: drawSpec.ManaCost ?? 0) : item.BattlefieldStart is { DrawCount: > 0 } ? new(DestroyLocalUnit: true) : item.LegendConquest?.Kind switch {
        "PAY_READY_SELF" => new(Mana: LegendConquestDefinition(item.LegendConquest.CardNo)?.Spec.ManaCost ?? 0),
        "EXHAUST_READY_UNIT" or "EXHAUST_DECK_PLAY" => new(Exhaust: true),
        _ => item.HeldContext?.Kind switch {
        "LEBLANC_DISCARD" => new(Exhaust: true, Discard: true),
        "VEX" or "RENATA" or "IVERN" => new(Exhaust: true),
        "BRUSH_RETURN" or "CHANNEL_OPTIONAL" => new(),
        "PAY_POWER_SCORE" => new(Power: 4),
        _ => null
        }
    };
    internal static bool NeedsTriggerCostConfirmation(StackItemState item) => LeadingTriggerCost(item) is not null && item.TriggerCost is null;

    private static bool ValidLeadingCostContext(StackItemState item) => item.UnitConquestReturn is { } unitReturn ? ValidUnitConquestReturnCostShape(item, unitReturn) : item.GraveyardPlay is { } graveyard ? item.EffectKind == GraveyardPlayEffect && item.CardNo == graveyard.CardNo && item.ControllerId == graveyard.ControllerId && item.SourceObjectId == graveyard.Source.ObjectId && GraveyardPlaySpec(graveyard.CardNo) is not null : item.CreatedTokenReady is not null ? CreatedTokenReadyHasPayment(item) && item.EffectKind == CreatedTokenReadyEffect && item.CardNo == item.CreatedTokenReady.CardNo : item.SivirRuneRecycle is { } sivir ? ValidSivirRuneContext(sivir, item.EffectKind, item.CardNo) : item.BattlefieldConquestMight is { } conquestMight ? ValidBattlefieldConquestMightFrame(conquestMight) && item.CardNo == conquestMight.CardNo && item.SourceObjectId == conquestMight.Source.ObjectId && item.ControllerId == conquestMight.ControllerId && item.EffectKind == TriggerKinds.BattlefieldConquerPowerfulPayDraw  : item.AttachmentObserver is not null ? ValidAttachmentObserver(item.AttachmentObserver, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo) : item.BattlefieldStart is { DrawCount: > 0 } start ? ValidBattlefieldStartDefinition(start, item.EffectKind, item.CardNo) : item.LegendConquest is { } conquest ? LeadingTriggerCost(item) is not null && ValidLegendConquest(conquest, item.EffectKind, item.CardNo)
        : item.HeldContext is { } held
        && LeadingTriggerCost(item) is not null && item.CardNo == held.CardNo
        && (HeldDefinition(held.CardNo) is { } definition && definition.Kind == held.Kind && definition.Amount == held.Amount
            || held.Kind == "BRUSH_RETURN" && P6TokenFactoryCatalog.IsBrushBattlefieldToken(held.CardNo) && held.Amount == 0)
        && item.EffectKind == "HOLD_" + held.Kind && held.SourceGeneration >= 0;

    private static bool ValidUnitConquestReturnCostShape(StackItemState item, UnitConquestReturnContext context)
        => context.Source is { Generation: >= 0 } source && !string.IsNullOrWhiteSpace(source.ObjectId)
            && source.ObjectId == item.SourceObjectId && context.CardNo == item.CardNo
            && context.ControllerId == item.ControllerId && !string.IsNullOrWhiteSpace(context.ControllerId)
            && !string.IsNullOrWhiteSpace(context.BattlefieldObjectId)
            && context.ConquestTick >= 0 && context.Occurrence >= 0 && context.FaceRevision >= 0
            && UnitConquestReturnSpec(context.CardNo) is { ManaCost: > 0 } spec
            && RuntimeTriggerEffectKind(spec) == item.EffectKind;

    private static string[] TriggerCostChoices(MatchState state, StackItemState item)
    {
        if (LeadingTriggerCost(item) is { DestroyLocalUnit: true })
            return ValidLeadingCostContext(item) && item.TriggerCost is null
                ? HeldUnitsAt(state, item.BattlefieldStart!.BattlefieldObjectId, item.ControllerId)
                    .Where(id => IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, id)).ToArray() : [];
        if (item.HeldContext?.Kind == "CHANNEL_OPTIONAL")
            return ValidLeadingCostContext(item) && item.TriggerCost is null ? [item.SourceObjectId] : [];
        if (item.HeldContext?.Kind == "BRUSH_RETURN")
            return ValidLeadingCostContext(item) && item.TriggerCost is null && CanReturnBattlefield(state, item) ? [item.SourceObjectId] : [];
        if (!ValidLeadingCostContext(item) || item.TriggerCost is not null || LeadingTriggerCost(item) is not { Exhaust: true } cost
            || !state.PlayerZones.TryGetValue(item.ControllerId, out var zones)
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != TriggerCostSourceGeneration(item) || source.ControllerId != item.ControllerId
            || source.IsExhausted || source.IsFaceDown || !zones.LegendZone.Contains(source.ObjectId)) return [];
        return cost.Discard ? zones.Hand.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && card.ControllerId == item.ControllerId).ToArray() : [source.ObjectId];
    }

    private static PendingCardChoiceState TriggerCostChoice(MatchState state, StackItemState item)
        => new("TRIGGER-COST:" + item.StackItemId, LeadingTriggerCost(item) is { Exhaust: false, Power: 0, DestroyLocalUnit: false } ? OptionalTriggerWindow : TriggerCostWindow, item.ControllerId, 0, 1,
            TriggerCostChoices(state, item), [item.BattlefieldStart?.BattlefieldObjectId ?? item.LegendConquest?.BattlefieldId ?? (item.SivirRuneRecycle is not null ? item.SourceObjectId : item.HeldContext!.BattlefieldObjectId)],
            LeadingTriggerCost(item) is { DestroyLocalUnit: true } ? "选择摧毁此战场的一名友方单位以确认技能；不选则放弃。费用支付后双方可响应，随后才抽牌。" : item.HeldContext?.Kind == "CHANNEL_OPTIONAL" ? "确认召出一枚休眠符文；不选则放弃。确认后双方可以响应，结算时才召出符文。" : item.HeldContext?.Kind == "BRUSH_RETURN" ? "选择此草丛以确认换回原战场的技能；不选则保留草丛。确认后双方可以响应。" : LeadingTriggerCost(item)!.Discard
                ? "选择弃置一张手牌并横置乐芙兰以确认技能；不选则放弃。确认后双方响应，结算时在此战场打出活跃映像，再选择复制对象。"
                : "选择横置此传奇以确认触发技能；不选则放弃。费用支付后双方可以响应，效果随后结算。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static long TriggerCostSourceGeneration(StackItemState item) => item.UnitConquestReturn?.Source.Generation ?? item.GraveyardPlay?.Source.Generation ?? item.CreatedTokenReady?.Source.Generation ?? item.SivirRuneRecycle?.SourceGeneration ?? item.BattlefieldConquestMight?.Source.Generation ?? item.AttachmentObserver?.SourceGeneration ?? item.BattlefieldStart?.SourceGeneration ?? item.LegendConquest?.SourceGeneration ?? item.HeldContext!.SourceGeneration;

    private static PendingPaymentState TriggerCostPayment(StackItemState item)
        => item.UnitConquestReturn is { } unitReturn
            ? new PendingPaymentState("TRIGGER-COST:" + item.StackItemId, TriggerPaymentWindow, item.ControllerId,
                manaCost: LeadingTriggerCost(item)!.Mana,
                legalPaymentChoiceIds: [BuildSpendManaPaymentChoiceId(LeadingTriggerCost(item)!.Mana), DeclinePaymentChoiceId],
                reason: BuildUnitConquestPayReturnSelfToHandPaymentReason(item.EffectKind,
                    unitReturn.BattlefieldObjectId, unitReturn.BattlefieldObjectId, item.SourceObjectId))
                { ResolvingStackItemId = item.StackItemId }
            : new PendingPaymentState("TRIGGER-COST:" + item.StackItemId, TriggerCostWindow, item.ControllerId,
            manaCost: LeadingTriggerCost(item)!.Mana, powerCost: LeadingTriggerCost(item)!.Power,
            powerCostByTrait: LeadingTriggerCost(item)!.PowerTrait is { } trait ? new Dictionary<string, int> { [trait] = LeadingTriggerCost(item)!.Power } : null,
            legalPaymentChoiceIds: ["PAY", "DECLINE"],
            reason: item.GraveyardPlay is not null ? "支付 1 法力和 1 炽烈符能确认技能，或放弃；双方响应后，从废牌堆打出原卡牌。" : item.CreatedTokenReady is not null ? "黄沙士兵已进场：支付 1 黄色符能确认后续活跃技能，或放弃；双方响应后才活跃。" : item.BattlefieldConquestMight is not null ? "支付法力以确认征服抽牌技能，或放弃；双方响应后才抽牌。" : item.AttachmentObserver is not null ? "支付法力以确认贴附后的抽牌技能，或放弃；双方响应后才抽牌。" : item.LegendConquest is not null ? "支付 1 法力以确认征服技能，或放弃；双方响应后传奇才变为活跃。" : "支付 4 点任意符能以确认据守技能；确认后双方可以响应，结算时额外获得 1 分。")
            { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareTriggerCostConfirmation(ResolutionResult result, StackItemState item)
    {
        var state = result.State;
        var cost = LeadingTriggerCost(item)!;
        if (!ValidLeadingCostContext(item) || cost.Power + cost.Mana == 0 && TriggerCostChoices(state, item).Length == 0)
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        state = state with { PendingCardChoice = cost.Power + cost.Mana == 0 ? TriggerCostChoice(state, item) : null,
            PendingPayment = cost.Power + cost.Mana > 0 ? TriggerCostPayment(item) : null,
            PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Events = item.AttachmentObserver is not null
            ? result.Events.Append(AttachmentPaymentOpened(item, state.PendingPayment!)).ToArray()
            : item.BattlefieldConquestMight is not null ? result.Events.Append(BattlefieldConquestMightPaymentOpened(item, state.PendingPayment!)).ToArray()
            : item.UnitConquestReturn is not null ? result.Events.Append(UnitConquestReturnPaymentOpened(item, state.PendingPayment!)).ToArray() : result.Events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static GameEvent UnitConquestReturnPaymentOpened(StackItemState item, PendingPaymentState payment)
    {
        var context = item.UnitConquestReturn!;
        return new("PAYMENT_WINDOW_OPENED", $"{item.ControllerId} 征服战场后等待支付回手触发费用",
            new Dictionary<string, object?> {
                ["paymentId"] = payment.PaymentId, ["paymentWindow"] = payment.PaymentWindow,
                ["playerId"] = item.ControllerId, ["battlefieldId"] = context.BattlefieldObjectId,
                ["battlefieldObjectId"] = context.BattlefieldObjectId, ["trigger"] = item.EffectKind,
                ["sourceObjectId"] = item.SourceObjectId, ["mana"] = payment.ManaCost, ["power"] = 0,
                ["cost"] = new Dictionary<string, object?> { ["mana"] = payment.ManaCost, ["power"] = 0,
                    ["powerByTrait"] = new Dictionary<string, int>(StringComparer.Ordinal) },
                ["paymentChoices"] = payment.LegalPaymentChoiceIds.ToArray(), ["reason"] = item.EffectKind });
    }

    internal static bool ValidTriggerCostChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || item.HeldContext is null && item.BattlefieldStart is null && item.LegendConquest?.Kind != "EXHAUST_DECK_PLAY" && item.SivirRuneRecycle is null || !NeedsTriggerCostConfirmation(item) || LeadingTriggerCost(item) is not { Power: 0, Mana: 0 }) return false;
        var expected = TriggerCostChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.ResolvingStackItemId == item.StackItemId
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.LegalObjectIds.Count > 0
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    internal static bool ValidTriggerCostPayment(MatchState state, PendingPaymentState payment)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == payment.ResolvingStackItemId);
        if (item is null || !NeedsTriggerCostConfirmation(item) || !ValidLeadingCostContext(item)
            || item.BattlefieldConquestMight is { } captured && !ValidBattlefieldConquestMightContext(state, captured,
                item.EffectKind, item.SourceObjectId, item.ControllerId)
            || item.UnitConquestReturn is { } unitReturn && !ValidUnitConquestReturnContext(state, unitReturn,
                item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            || LeadingTriggerCost(item) is not { } cost || cost.Power + cost.Mana <= 0) return false;
        var expected = TriggerCostPayment(item);
        return payment.PaymentId == expected.PaymentId && payment.PaymentWindow == expected.PaymentWindow
            && payment.PlayerId == item.ControllerId && payment.PowerCost == expected.PowerCost && payment.ManaCost == expected.ManaCost
            && payment.PowerCostByTrait.Count == expected.PowerCostByTrait.Count
            && payment.PowerCostByTrait.All(p => expected.PowerCostByTrait.GetValueOrDefault(p.Key, -1) == p.Value)
            && payment.PaymentResourceActionIds.Count == 0
            && payment.LegalPaymentChoiceIds.SequenceEqual(expected.LegalPaymentChoiceIds)
            && (item.UnitConquestReturn is null || payment.Reason == expected.Reason);
    }

    internal static bool ValidTriggerCostReceipt(MatchState state, StackItemState item)
    {
        if (item.UnitConquestReturn is { } unitReturn && !ValidUnitConquestReturnContext(state, unitReturn,
                item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)) return false;
        if (LeadingTriggerCost(item) is not null && !ValidLeadingCostContext(item)) return false;
        if (item.TriggerCost is not { } receipt) return true;
        if (!ValidLeadingCostContext(item) || receipt.Source is null || receipt.Source.ObjectId != item.SourceObjectId
            || receipt.Source.Generation != TriggerCostSourceGeneration(item)) return false;
        var cost = LeadingTriggerCost(item)!;
        if (cost.DestroyLocalUnit ? receipt.DestructionTarget is not { Generation: >= 0 } paid
                || string.IsNullOrWhiteSpace(paid.ObjectId) || paid.ObjectId == item.SourceObjectId
            : receipt.DestructionTarget is not null) return false;
        return receipt.Power == cost.Power && receipt.Mana == cost.Mana && (cost.Discard
            ? receipt.Discarded is { } discarded && discarded.ObjectId != item.SourceObjectId && discarded.Generation >= 0
                && state.CardObjects.TryGetValue(discarded.ObjectId, out var card) && discarded.Generation < card.ObjectGeneration
            : receipt.Discarded is null);
    }

    private static ResolutionResult CompleteTriggerCost(MatchState state, StackItemState item,
        TriggerCostReceipt? receipt, List<GameEvent> events)
    {
        var unitReturnPayment = item.UnitConquestReturn is not null ? state.PendingPayment : null;
        if ((item.AttachmentObserver is not null || item.BattlefieldConquestMight is not null) && state.PendingPayment is { } payment) {
            var closed = BuildPaymentWindowClosedEvent(payment, item.ControllerId, declined: receipt is null);
            if (item.BattlefieldConquestMight is not null) closed = closed with { Payload = new Dictionary<string, object?>(closed.Payload) {
                ["trigger"] = item.EffectKind, ["sourceObjectId"] = item.SourceObjectId } };
            events.Add(closed);
        }
        if (unitReturnPayment is not null && receipt is not null)
            events.Add(BuildPaymentWindowClosedEvent(unitReturnPayment, item.ControllerId, declined: false));
        var paid = receipt is null ? null : item with { TriggerCost = receipt };
        var next = RestoreAfterTriggerConfirmation(state with { Tick = state.Tick + 1,
            PendingCardChoice = null, PendingPayment = null,
            StackItems = state.StackItems.Where(i => paid is not null || i.StackItemId != item.StackItemId)
                .Select(i => i.StackItemId == item.StackItemId ? paid! : i).ToArray() }, item);
        var confirmation = unitReturnPayment is not null
            ? BuildTriggerPaymentPayload(unitReturnPayment, item.ControllerId)
            : new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId };
        confirmation["stackItemId"] = item.StackItemId;
        if (item.BattlefieldConquestMight is not null && state.PendingPayment is { } conquestPayment) {
            confirmation["trigger"] = item.EffectKind; confirmation["paymentId"] = conquestPayment.PaymentId;
            confirmation["paymentWindow"] = conquestPayment.PaymentWindow;
        }
        events.Add(new(receipt is null ? "TRIGGER_PAYMENT_DECLINED" : "TRIGGER_CONFIRMED",
            receipt is null ? "放弃并移除触发技能" : "已确认触发技能，双方可以响应", confirmation));
        if (unitReturnPayment is not null && receipt is null)
            events.Add(BuildPaymentWindowClosedEvent(unitReturnPayment, item.ControllerId, declined: true));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static ResolutionResult ResolveTriggerCostChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidTriggerCostChoice(state, choice))
            return RejectWithCorePrompts(state, "触发技能的费用选择已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        if (selected.Count == 0) return CompleteTriggerCost(state, item, null, []);
        if (LeadingTriggerCost(item) is { DestroyLocalUnit: true })
            return PayTriggerDestructionCost(state, item, selected[0]);
        if (LeadingTriggerCost(item) is { Exhaust: false })
            return CompleteTriggerCost(state, item, new(new(item.SourceObjectId, TriggerCostSourceGeneration(item))), []);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var source = cards[item.SourceObjectId];
        var events = new List<GameEvent>();
        ObjectBinding? discarded = null;
        if (LeadingTriggerCost(item)!.Discard)
        {
            var card = cards[selected[0]];
            if (!TryDiscardCardFromHand(zones, cards, item.ControllerId, card.ObjectId))
                return RejectWithCorePrompts(state, "无法支付所选弃牌费用。", ErrorCodes.InvalidTarget);
            discarded = new(card.ObjectId, card.ObjectGeneration);
            events.Add(new("CARD_DISCARDED", "弃置所选手牌作为触发技能费用", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = card.ObjectId,
                ["destinationZone"] = "GRAVEYARD", ["reason"] = TriggerCostWindow }));
        }
        cards[source.ObjectId] = source with { IsExhausted = true };
        events.Add(new("LEGEND_EXHAUSTED", "支付横置费用", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId, ["reason"] = TriggerCostWindow }));
        if (discarded is not null)
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, item.ControllerId, "CARD_DISCARDED", source.ObjectId, selected, events);
        var next = state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            UntilEndOfTurnEffects = discarded is null ? state.UntilEndOfTurnEffects
                : MarkPlayerDiscardedHandCardsThisTurn(state.UntilEndOfTurnEffects, item.ControllerId, selected) };
        return CompleteTriggerCost(next, item, new(new(source.ObjectId, source.ObjectGeneration), discarded), events);
    }

    private static ResolutionResult PayTriggerDestructionCost(MatchState state, StackItemState item, string id)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var paid = new ObjectBinding(id, cards[id].ObjectGeneration);
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), state.RunePools,
            objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { id });
        if (removal.Events.Count == 0)
            return RejectWithCorePrompts(state, "无法支付所选摧毁费用。", ErrorCodes.InvalidTarget);
        var events = removal.Events.ToList();
        var next = state with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            TriggerQueue = state.TriggerQueue.Concat(removal.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, removal.DestroyedUnitOwnerIds) };
        return CompleteTriggerCost(next, item, new(new(item.SourceObjectId, TriggerCostSourceGeneration(item)), DestructionTarget: paid), events);
    }

    private static ResolutionResult ResolveTriggerCostPayment(MatchState state, PlayerIntent intent, PendingPaymentState pending,
        IReadOnlyList<string> choices, int rawCount)
    {
        if (!ValidTriggerCostPayment(state, pending) || choices.Count != 1 || rawCount != 1)
            return RejectWithCorePrompts(state, "请选择支付或放弃。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == pending.ResolvingStackItemId);
        if (choices[0] is not ("PAY" or "DECLINE")
            && !(item.UnitConquestReturn is not null && choices[0] == BuildSpendManaPaymentChoiceId(pending.ManaCost)))
            return RejectWithCorePrompts(state, "请选择支付或放弃。", ErrorCodes.InvalidTarget);
        if (choices[0] == "DECLINE") return CompleteTriggerCost(state, item, null, []);
        var plan = new PaymentCostRules.PaymentPlan(pending.PaymentId, pending.PaymentWindow, intent.PlayerId,
            baseManaCost: pending.ManaCost, totalManaCost: pending.ManaCost,
            genericPowerCost: pending.PowerCost - pending.PowerCostByTrait.Values.Sum(), totalPowerCost: pending.PowerCost,
            powerCostByTrait: pending.PowerCostByTrait,
            reason: pending.Reason, sourceObjectId: item.SourceObjectId);
        var committed = PaymentCostRules.TryCommitPayment(plan, state.RunePools, state.PlayerExperience, state: state);
        if (!committed.Accepted) return RejectWithCorePrompts(state, "资源不足，可以先使用符文或反应资源技能，或放弃此技能。", ErrorCodes.InsufficientCost);
        var details = new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId };
        if (item.BattlefieldConquestMight is not null || item.UnitConquestReturn is not null) {
            details["reason"] = item.EffectKind; details["paymentChoiceIds"] = choices.ToArray();
            details["mana"] = pending.ManaCost; details["power"] = pending.PowerCost;
            if (item.UnitConquestReturn is not null) details["powerByTrait"] = pending.PowerCostByTrait;
        }
        return CompleteTriggerCost(state with { RunePools = committed.RunePools, PlayerExperience = committed.PlayerExperience }, item,
            new(new(item.SourceObjectId, TriggerCostSourceGeneration(item)), Power: pending.PowerCost, Mana: pending.ManaCost),
            [new("COST_PAID", "支付触发技能的基础费用", PaymentCostRules.BuildCostPaidPayload(plan, committed.RunePools,
                committed.PlayerExperience, details))]);
    }
}
