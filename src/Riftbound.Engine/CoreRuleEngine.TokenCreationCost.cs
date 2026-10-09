using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record DiscardExhaustCostReceipt(ObjectBinding Source, ObjectBinding Discarded);

public sealed partial class CoreRuleEngine
{
    internal const string TokenCreationCostWindow = "TOKEN_CREATION_COST";

    private static IEnumerable<TriggerQueueItemState> CaptureLegendImageTriggers(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string field, long tick) => zones[player].LegendZone
        .Where(id => cards.TryGetValue(id, out var c) && c.ControllerId == player && !c.IsFaceDown
            && LegendCardHasIdentity(c.CardNo, LeblancLegendIdentityId))
        .Select(id => new TriggerQueueItemState($"conquest-image-{tick}-{field}-{id}", player, id,
            "HOLD_LEBLANC_DISCARD", "BATTLEFIELD_CONQUERED") {
            HeldContext = new(cards[id].CardNo!, field, "LEBLANC_DISCARD", 1, cards[id].ObjectGeneration) });

    private static string[] DiscardTokenCreationCosts(MatchState state, StackItemState item)
        => item.HeldContext is { Kind: "LEBLANC_DISCARD" } held && item.DiscardExhaustCost is null
            && LegendCardHasIdentity(held.CardNo, LeblancLegendIdentityId) && item.CardNo == held.CardNo
            && state.PlayerZones.TryGetValue(item.ControllerId, out var zones)
            && state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == held.SourceGeneration && source.ControllerId == item.ControllerId
            && !source.IsExhausted && !source.IsFaceDown && zones.LegendZone.Contains(source.ObjectId)
            && TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, held.BattlefieldObjectId, out _, out _)
                ? zones.Hand.Where(id => state.CardObjects.TryGetValue(id, out var card)
                    && card.ControllerId == item.ControllerId).ToArray() : [];

    private static PendingCardChoiceState DiscardTokenCreationChoice(MatchState state, StackItemState item)
        => new("TOKEN-COST:" + item.StackItemId, TokenCreationCostWindow, item.ControllerId, 0, 1,
            DiscardTokenCreationCosts(state, item), [item.HeldContext!.BattlefieldObjectId],
            "选择弃置一张手牌并横置乐芙兰以确认技能；不选则放弃。确认后双方响应，结算时在此战场打出活跃映像，再选择复制对象。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareTokenCreationConfirmation(ResolutionResult result)
    {
        var state = result.State;
        var item = state.StackItems.FirstOrDefault(i => i.HeldContext is { Kind: "LEBLANC_DISCARD" } && i.DiscardExhaustCost is null);
        if (item is null) return result;
        if (DiscardTokenCreationCosts(state, item).Length == 0)
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        state = state with { PendingCardChoice = DiscardTokenCreationChoice(state, item),
            PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveDiscardTokenCreation(MatchState state, StackItemState item)
    {
        if (item.DiscardExhaustCost is null) return NoopStackResolutionResult(state);
        var field = item.HeldContext!.BattlefieldObjectId;
        var created = CreateUnitTokenBatch(state, item, P6TokenFactoryCatalog.ImageTokenCardNo,
            1 + AdditionalUnitTokens(item), true, field);
        var recipients = created.Events.Where(e => e.Kind == "UNIT_TOKEN_CREATED")
            .Select(e => created.CardObjects[(string)e.Payload["tokenObjectId"]!]).ToArray();
        if (recipients.Length == 0) return created;
        var events = created.Events.ToList();
        AddReflexiveCopyEvent(events, item.ControllerId, item.SourceObjectId, item.CardNo, null, recipients, field);
        return created with { Events = events };
    }

    internal static bool ValidDiscardTokenCreationChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.HeldContext?.Kind != "LEBLANC_DISCARD" || item.DiscardExhaustCost is not null) return false;
        var expected = DiscardTokenCreationChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.ResolvingStackItemId == item.StackItemId
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.LegalObjectIds.Count > 0
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    internal static bool ValidDiscardExhaustReceipt(MatchState state, StackItemState item)
        => item.DiscardExhaustCost is not { } cost || item.HeldContext is { Kind: "LEBLANC_DISCARD" } held
            && LegendCardHasIdentity(item.CardNo, LeblancLegendIdentityId) && item.CardNo == held.CardNo
            && cost.Source is not null && cost.Discarded is not null
            && cost.Source.ObjectId == item.SourceObjectId && cost.Source.Generation == held.SourceGeneration
            && cost.Discarded.ObjectId != item.SourceObjectId && cost.Discarded.Generation >= 0
            && state.CardObjects.TryGetValue(cost.Discarded.ObjectId, out var discarded)
            && cost.Discarded.Generation < discarded.ObjectGeneration;

    private static ResolutionResult ResolveDiscardTokenCreationChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidDiscardTokenCreationChoice(state, choice))
            return RejectWithCorePrompts(state, "创建映像的费用选择已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        if (selected.Count == 0)
        {
            var declined = RestoreAfterTriggerConfirmation(state with { Tick = state.Tick + 1, PendingCardChoice = null,
                StackItems = state.StackItems.Where(i => i.StackItemId != item.StackItemId).ToArray() }, item);
            return new(true, null, declined, [new("TRIGGER_PAYMENT_DECLINED", "放弃弃牌及横置费用", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId })], ResolutionResult.BuildSnapshots(declined), BuildCorePrompts(declined));
        }
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var source = cards[item.SourceObjectId]; var discarded = cards[selected[0]];
        if (!TryDiscardCardFromHand(zones, cards, item.ControllerId, discarded.ObjectId))
            return RejectWithCorePrompts(state, "无法支付所选弃牌费用。", ErrorCodes.InvalidTarget);
        cards[source.ObjectId] = source with { IsExhausted = true };
        item = item with { DiscardExhaustCost = new(new(source.ObjectId, source.ObjectGeneration), new(discarded.ObjectId, discarded.ObjectGeneration)) };
        var events = new List<GameEvent> {
            new("CARD_DISCARDED", "弃置所选手牌作为创建费用", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = discarded.ObjectId,
                ["destinationZone"] = "GRAVEYARD", ["reason"] = TokenCreationCostWindow }),
            new("LEGEND_EXHAUSTED", "支付横置费用", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId, ["reason"] = TokenCreationCostWindow }) };
        ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, item.ControllerId, "CARD_DISCARDED", source.ObjectId, selected, events);
        var paid = state with { PendingCardChoice = null, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            UntilEndOfTurnEffects = MarkPlayerDiscardedHandCardsThisTurn(state.UntilEndOfTurnEffects, item.ControllerId, selected),
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() };
        paid = RestoreAfterTriggerConfirmation(paid with { Tick = state.Tick + 1 }, item);
        events.Add(new("TRIGGER_CONFIRMED", "费用已支付，双方可响应创建映像的技能", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId }));
        return new(true, null, paid, events, ResolutionResult.BuildSnapshots(paid), BuildCorePrompts(paid));
    }
}
