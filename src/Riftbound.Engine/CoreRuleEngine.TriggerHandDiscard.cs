using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TriggerHandDiscardCost(ObjectBinding Card, long SelectedTick);

public sealed partial class CoreRuleEngine
{
    internal const string TriggerHandDiscardWindow = "TRIGGER_HAND_DISCARD_COST";

    private static string[] TriggerHandDiscardChoices(MatchState state, string player)
        => state.PlayerZones.TryGetValue(player, out var zones) ? zones.Hand
            .Where(id => state.CardObjects.TryGetValue(id, out var card) && card.ObjectId == id
                && card.OwnerId == player && SourceObjectControlledByPlayerOrLegacyOwned(card, player)
                && !card.IsFaceDown && !TokenObjectRules.IsToken(card)
                && state.ObjectLocations.TryGetValue(id, out var location)
                && location.PlayerId == player && location.Zone == "HAND")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : [];

    private static bool ValidTriggerHandDiscardCost(MatchState state, PaidUnitTriggerOrigin origin, string player)
    {
        if (origin.DiscardCost is not { } cost) return !origin.CostPaid;
        if (cost.Card is not { Generation: >= 0 } || cost.SelectedTick < origin.CapturedTick
            || cost.SelectedTick > state.Tick || !state.CardObjects.TryGetValue(cost.Card.ObjectId, out var card)
            || card.OwnerId != player || TokenObjectRules.IsToken(card)) return false;
        return origin.CostPaid ? card.ObjectGeneration > cost.Card.Generation
            : card.ObjectGeneration == cost.Card.Generation
                && TriggerHandDiscardChoices(state, player).Contains(cost.Card.ObjectId, StringComparer.Ordinal);
    }

    private static PendingCardChoiceState TriggerHandDiscardChoice(MatchState state, StackItemState item)
        => new("TRIGGER-DISCARD:" + item.StackItemId, TriggerHandDiscardWindow, item.ControllerId, 0, 1,
            TriggerHandDiscardChoices(state, item.ControllerId), item.TargetObjectIds,
            "选择弃置一张手牌作为技能费用；不选则放弃。若目标有法盾，还须确认支付符能，费用全部支付后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareTriggerHandDiscard(MatchState state, StackItemState item)
    {
        if (TriggerHandDiscardChoices(state, item.ControllerId).Length == 0)
            return DiscardUnconfirmedTrigger(new(true, null, state, [], ResolutionResult.BuildSnapshots(state), BuildCorePrompts(state)), item);
        var next = state with { StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray(),
            PendingCardChoice = TriggerHandDiscardChoice(state, item), PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    internal static bool ValidTriggerHandDiscardChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.FieldContext is not { Kind: PaidUnitDestroyKind, PaidUnitTrigger: { CostPaid: false, DiscardCost: null } }
            || !ValidFieldContextState(state, item.FieldContext, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            || item.TargetGenerations is not { Count: 1 } || item.TargetObjectIds.Count != 1) return false;
        var expected = TriggerHandDiscardChoice(state, item);
        return state.PriorityPlayerId is null && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.ChoiceId == expected.ChoiceId && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.DeckContext is null && choice.HandContext is null && choice.Reason == expected.Reason
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.LegalObjectIds.Count > 0
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveTriggerHandDiscardChoice(MatchState state, PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        if (!ValidTriggerHandDiscardChoice(state, choice))
            return RejectWithCorePrompts(state, "触发技能的弃牌费用选择已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null };
        if (selected.Count == 0)
            return DiscardUnconfirmedTrigger(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        var card = state.CardObjects[selected.Single()];
        item = item with { FieldContext = item.FieldContext! with { PaidUnitTrigger = item.FieldContext!.PaidUnitTrigger! with {
            DiscardCost = new(new(card.ObjectId, card.ObjectGeneration), next.Tick) } } };
        var tax = ResolveSpellshieldTargetTaxPower(state, item.ControllerId, item.TargetObjectIds, out _);
        if (tax == 0) return CompleteTriggerTargetConfirmation(next, item, []);
        // Confirm the private card first, then commit every cost together. A
        // declined or unfunded ward must leave the selected hand card intact.
        var payment = new PendingPaymentState("TRIGGER-WARD:" + item.StackItemId, TriggerTargetCostWindow, item.ControllerId,
            powerCost: tax, legalPaymentChoiceIds: ["PAY", "DECLINE"],
            reason: $"确认弃置所选手牌并支付 {tax} 任意符能；放弃则不支付任何费用。") { ResolvingStackItemId = item.StackItemId };
        next = next with { PendingPayment = payment, PriorityPlayerId = null,
            StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() };
        return new(true, null, next, [new("PAYMENT_WINDOW_OPENED", "等待原子支付弃牌及法盾费用",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(next, item.SourceObjectId, "__spectator__"),
                ["stackItemId"] = item.StackItemId, ["paymentId"] = payment.PaymentId,
                ["paymentWindow"] = payment.PaymentWindow, ["manaCost"] = 0, ["powerCost"] = tax,
                ["targetObjectIds"] = item.TargetObjectIds })],
            ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static bool TryPayTriggerHandDiscard(MatchState state, StackItemState item,
        List<GameEvent> events, out MatchState next, out StackItemState paid)
    {
        next = state; paid = item;
        var origin = item.FieldContext!.PaidUnitTrigger!;
        if (origin.CostPaid || origin.DiscardCost is not { } cost || !ValidTriggerHandDiscardCost(state, origin, item.ControllerId)) return false;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (!TryDiscardCardFromHand(zones, cards, item.ControllerId, cost.Card.ObjectId)) return false;
        var source = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        events.Add(new("CARD_DISCARDED", "支付触发技能的弃牌费用", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = source,
            ["targetObjectId"] = cost.Card.ObjectId, ["destinationZone"] = "GRAVEYARD", ["isCost"] = true }));
        var plan = new PaymentCostRules.PaymentPlan("TRIGGER-DISCARD:" + item.StackItemId, TriggerHandDiscardWindow,
            item.ControllerId, baseManaCost: 0, totalManaCost: 0, genericPowerCost: 0, totalPowerCost: 0,
            sourceObjectId: item.SourceObjectId);
        events.Add(new("COST_PAID", "支付触发技能的弃牌费用",
            PaymentCostRules.BuildCostPaidPayload(plan, state.RunePools, state.PlayerExperience,
                new Dictionary<string, object?> { ["sourceObjectId"] = source,
                    ["discardedObjectIds"] = new[] { cost.Card.ObjectId }, ["stackItemId"] = item.StackItemId })));
        ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, item.ControllerId, "CARD_DISCARDED", item.SourceObjectId,
            [cost.Card.ObjectId], events);
        next = state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            UntilEndOfTurnEffects = MarkPlayerDiscardedHandCardsThisTurn(state.UntilEndOfTurnEffects, item.ControllerId, [cost.Card.ObjectId]) };
        paid = item with { FieldContext = item.FieldContext with { PaidUnitTrigger = origin with { CostPaid = true } } };
        return true;
    }

    internal static bool ValidDiscardDestroyStack(MatchState state, StackItemState item)
    {
        if (item.FieldContext is not { Kind: PaidUnitDestroyKind, PaidUnitTrigger: { } origin }) return true;
        if (item.SourceConfirmed || item.OptionalCosts.Count != 0 || item.DamageAmount != 0 || item.EffectRepeatCount != 1
            || item.Destination.Length != 0 || item.TargetedPowerAbility is not null || item.DamageDistribution is not null) return false;
        if (item.TargetGenerations is null) return item.TargetObjectIds.Count == 0 && !origin.CostPaid && origin.DiscardCost is null;
        if (item.TargetObjectIds.Count != 1 || item.TargetGenerations.Count != 1) return false;
        if (origin.CostPaid) return state.PendingPayment?.ResolvingStackItemId != item.StackItemId
            && state.PendingCardChoice?.ResolvingStackItemId != item.StackItemId;
        return state.PriorityPlayerId is null && (origin.DiscardCost is null
            ? state.PendingCardChoice is { ChoiceWindow: TriggerHandDiscardWindow } choice && choice.ResolvingStackItemId == item.StackItemId
            : state.PendingPayment is { PaymentWindow: TriggerTargetCostWindow } payment && payment.ResolvingStackItemId == item.StackItemId);
    }

    private static string[] DiscardDestroyTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(card => !card.IsFaceDown
            && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static StackResolutionResult ResolveDiscardDestroy(MatchState state, StackItemState item)
    {
        if (item.FieldContext?.PaidUnitTrigger is not { CostPaid: true }
            || !ValidFieldContextState(state, item.FieldContext, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid paid permanent destruction trigger.");
        var source = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var done = NoopStackResolutionResult(state) with { Events = [new("TRIGGER_RESOLVED", "弃牌摧毁技能结算",
            new Dictionary<string, object?> { ["sourceObjectId"] = source, ["effectKind"] = PaidUnitDestroyKind })] };
        if (item.TargetObjectIds.SingleOrDefault() is not { } id
            || item.TargetGenerations?.GetValueOrDefault(id, -1) != state.CardObjects.GetValueOrDefault(id)?.ObjectGeneration
            || !DiscardDestroyTargets(state, item).Contains(id, StringComparer.Ordinal)) return done;
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), state.RunePools,
            objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { id },
            explicitDestroyReason: "SKILL_EFFECT");
        return done with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            TriggerQueue = removal.TriggerQueue, DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds,
            Events = done.Events.Concat(removal.Events).ToArray() };
    }
}
