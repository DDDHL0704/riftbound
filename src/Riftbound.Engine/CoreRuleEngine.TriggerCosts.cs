using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TriggerCostReceipt(ObjectBinding Source, ObjectBinding? Discarded = null, int Power = 0, int Mana = 0);

public sealed partial class CoreRuleEngine
{
    internal const string TriggerCostWindow = "TRIGGER_COST_CONFIRMATION";
    internal const string OptionalTriggerWindow = "TRIGGER_OPTIONAL_CONFIRMATION";
    private sealed record LeadingCost(bool Exhaust = false, bool Discard = false, int Power = 0, int Mana = 0);

    // CN 204.3.a: leading instruction costs are paid while confirming a trigger,
    // before responses. A paid receipt follows the captured stack item, not its live source.
    private static LeadingCost? LeadingTriggerCost(StackItemState item) => item.LegendConquest?.Kind switch {
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

    private static bool ValidLeadingCostContext(StackItemState item) => item.LegendConquest is { } conquest ? LeadingTriggerCost(item) is not null && ValidLegendConquest(conquest, item.EffectKind, item.CardNo)
        : item.HeldContext is { } held
        && LeadingTriggerCost(item) is not null && item.CardNo == held.CardNo
        && (HeldDefinition(held.CardNo) is { } definition && definition.Kind == held.Kind && definition.Amount == held.Amount
            || held.Kind == "BRUSH_RETURN" && P6TokenFactoryCatalog.IsBrushBattlefieldToken(held.CardNo) && held.Amount == 0)
        && item.EffectKind == "HOLD_" + held.Kind && held.SourceGeneration >= 0;

    private static string[] TriggerCostChoices(MatchState state, StackItemState item)
    {
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
        => new("TRIGGER-COST:" + item.StackItemId, LeadingTriggerCost(item) is { Exhaust: false, Power: 0 } ? OptionalTriggerWindow : TriggerCostWindow, item.ControllerId, 0, 1,
            TriggerCostChoices(state, item), [item.LegendConquest?.BattlefieldId ?? item.HeldContext!.BattlefieldObjectId],
            item.HeldContext?.Kind == "CHANNEL_OPTIONAL" ? "确认召出一枚休眠符文；不选则放弃。确认后双方可以响应，结算时才召出符文。" : item.HeldContext?.Kind == "BRUSH_RETURN" ? "选择此草丛以确认换回原战场的技能；不选则保留草丛。确认后双方可以响应。" : LeadingTriggerCost(item)!.Discard
                ? "选择弃置一张手牌并横置乐芙兰以确认技能；不选则放弃。确认后双方响应，结算时在此战场打出活跃映像，再选择复制对象。"
                : "选择横置此传奇以确认触发技能；不选则放弃。费用支付后双方可以响应，效果随后结算。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static long TriggerCostSourceGeneration(StackItemState item) => item.LegendConquest?.SourceGeneration ?? item.HeldContext!.SourceGeneration;

    private static PendingPaymentState TriggerCostPayment(StackItemState item)
        => new("TRIGGER-COST:" + item.StackItemId, TriggerCostWindow, item.ControllerId,
            manaCost: LeadingTriggerCost(item)!.Mana, powerCost: LeadingTriggerCost(item)!.Power, legalPaymentChoiceIds: ["PAY", "DECLINE"],
            reason: item.LegendConquest is not null ? "支付 1 法力以确认征服技能，或放弃；双方响应后传奇才变为活跃。" : "支付 4 点任意符能以确认据守技能；确认后双方可以响应，结算时额外获得 1 分。")
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
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidTriggerCostChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || item.HeldContext is null && item.LegendConquest?.Kind != "EXHAUST_DECK_PLAY" || !NeedsTriggerCostConfirmation(item) || LeadingTriggerCost(item) is not { Power: 0, Mana: 0 }) return false;
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
            || LeadingTriggerCost(item) is not { } cost || cost.Power + cost.Mana <= 0) return false;
        var expected = TriggerCostPayment(item);
        return payment.PaymentId == expected.PaymentId && payment.PaymentWindow == expected.PaymentWindow
            && payment.PlayerId == item.ControllerId && payment.PowerCost == expected.PowerCost && payment.ManaCost == expected.ManaCost
            && payment.PowerCostByTrait.Count == 0 && payment.PaymentResourceActionIds.Count == 0
            && payment.LegalPaymentChoiceIds.SequenceEqual(expected.LegalPaymentChoiceIds);
    }

    internal static bool ValidTriggerCostReceipt(MatchState state, StackItemState item)
    {
        if (LeadingTriggerCost(item) is not null && !ValidLeadingCostContext(item)) return false;
        if (item.TriggerCost is not { } receipt) return true;
        if (!ValidLeadingCostContext(item) || receipt.Source is null || receipt.Source.ObjectId != item.SourceObjectId
            || receipt.Source.Generation != TriggerCostSourceGeneration(item)) return false;
        var cost = LeadingTriggerCost(item)!;
        return receipt.Power == cost.Power && receipt.Mana == cost.Mana && (cost.Discard
            ? receipt.Discarded is { } discarded && discarded.ObjectId != item.SourceObjectId && discarded.Generation >= 0
                && state.CardObjects.TryGetValue(discarded.ObjectId, out var card) && discarded.Generation < card.ObjectGeneration
            : receipt.Discarded is null);
    }

    private static ResolutionResult CompleteTriggerCost(MatchState state, StackItemState item,
        TriggerCostReceipt? receipt, List<GameEvent> events)
    {
        var paid = receipt is null ? null : item with { TriggerCost = receipt };
        var next = RestoreAfterTriggerConfirmation(state with { Tick = state.Tick + 1,
            PendingCardChoice = null, PendingPayment = null,
            StackItems = state.StackItems.Where(i => paid is not null || i.StackItemId != item.StackItemId)
                .Select(i => i.StackItemId == item.StackItemId ? paid! : i).ToArray() }, item);
        events.Add(new(receipt is null ? "TRIGGER_PAYMENT_DECLINED" : "TRIGGER_CONFIRMED",
            receipt is null ? "放弃并移除触发技能" : "已确认触发技能，双方可以响应", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static ResolutionResult ResolveTriggerCostChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidTriggerCostChoice(state, choice))
            return RejectWithCorePrompts(state, "触发技能的费用选择已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        if (selected.Count == 0) return CompleteTriggerCost(state, item, null, []);
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

    private static ResolutionResult ResolveTriggerCostPayment(MatchState state, PlayerIntent intent, PendingPaymentState pending,
        IReadOnlyList<string> choices, int rawCount)
    {
        if (!ValidTriggerCostPayment(state, pending) || choices.Count != 1 || rawCount != 1 || choices[0] is not ("PAY" or "DECLINE"))
            return RejectWithCorePrompts(state, "请选择支付或放弃。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == pending.ResolvingStackItemId);
        if (choices[0] == "DECLINE") return CompleteTriggerCost(state, item, null, []);
        var plan = new PaymentCostRules.PaymentPlan(pending.PaymentId, pending.PaymentWindow, intent.PlayerId,
            baseManaCost: pending.ManaCost, totalManaCost: pending.ManaCost,
            genericPowerCost: pending.PowerCost, totalPowerCost: pending.PowerCost,
            reason: pending.Reason, sourceObjectId: item.SourceObjectId);
        var committed = PaymentCostRules.TryCommitPayment(plan, state.RunePools, state.PlayerExperience);
        if (!committed.Accepted) return RejectWithCorePrompts(state, "资源不足，可以先使用符文或反应资源技能，或放弃此技能。", ErrorCodes.InsufficientCost);
        return CompleteTriggerCost(state with { RunePools = committed.RunePools, PlayerExperience = committed.PlayerExperience }, item,
            new(new(item.SourceObjectId, TriggerCostSourceGeneration(item)), Power: pending.PowerCost, Mana: pending.ManaCost),
            [new("COST_PAID", "支付触发技能的基础费用", PaymentCostRules.BuildCostPaidPayload(plan, committed.RunePools,
                committed.PlayerExperience, new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId }))]);
    }
}
