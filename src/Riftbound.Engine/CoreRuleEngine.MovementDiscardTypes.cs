using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string MovementDiscardInlineEffect = "MOVEMENT_DISCARD_TYPE_INLINE";
    internal const string MovementDiscardRuneChoiceWindow = "MOVEMENT_DISCARD_RUNE_TARGETS";

    private static bool ValidMovementDiscardInline(MovementDiscardInlineContext c)
        => c.DiscardedCard is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(c.DiscardedCard.ObjectId)
            && !string.IsNullOrWhiteSpace(c.CardNo) && c.CreatedAtTick > 0
            && c.BranchType is 1 or 2 or 4 && (c.Types & c.BranchType) != 0
            && PrintedPlayTypes.Value.TryGetValue(c.CardNo, out var printed) && c.Types == printed;

    internal static bool ValidMovementDiscardState(MatchState state)
    {
        if (state.TriggerQueue.Any(t => t.TriggeredByEventKind == "CARD_DISCARDED"
            && (t.EffectKind != MovementDiscardInlineEffect || t.MovementObserver?.DiscardInline is null)
            && !(t.HandDiscardObserver is { } captured && ValidHandDiscardObserver(state, captured,
                t.EffectKind, t.SourceObjectId, t.ControllerId, t.SourceCardNo)))) return false;
        if (state.TriggerQueue.Any(t => t.EffectKind == MovementDiscardInlineEffect
            && (t.MovementObserver?.DiscardInline is null || t.SourceCardNo != t.MovementObserver.CardNo))) return false;
        if (state.PendingCardChoice is { } pending && state.StackItems.Any(item => item.StackItemId == pending.ResolvingStackItemId
            && item.EffectKind == MovementDiscardInlineEffect) && !ValidMovementDiscardRuneChoice(state, pending)) return false;
        var contexts = state.TriggerQueue.Select(t => t.MovementObserver?.DiscardInline)
            .Concat(state.StackItems.Select(s => s.MovementObserver?.DiscardInline)).OfType<MovementDiscardInlineContext>().ToArray();
        if (contexts.Any(c => !ValidMovementDiscardInline(c) || c.CreatedAtTick > state.Tick
            || !state.CardObjects.TryGetValue(c.DiscardedCard.ObjectId, out var card)
            || card.CardNo != c.CardNo || card.ObjectGeneration <= c.DiscardedCard.Generation)) return false;
        if (contexts.GroupBy(c => (c.CreatedAtTick, c.DiscardedCard))
            .Any(group => group.Select(c => c.BranchType).Distinct().Count() != group.Count())) return false;
        foreach (var item in state.StackItems.Where(s => s.MovementObserver?.Kind == TriggerKinds.UnitMovedDrawDiscardTypes))
        {
            if (item.MovementObserver!.DiscardInline?.BranchType != 2)
            {
                if (item.TargetObjectIds.Count != 0 || item.TargetGenerations is not null) return false;
            }
            else if (item.TargetObjectIds.Count > 2 || item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() != item.TargetObjectIds.Count
                || (item.TargetGenerations is null ? item.TargetObjectIds.Count != 0
                    : item.TargetGenerations.Count != item.TargetObjectIds.Count
                        || item.TargetObjectIds.Any(id => !item.TargetGenerations.TryGetValue(id, out var generation) || generation < 0))) return false;
        }
        return true;
    }

    private static StackResolutionResult ResolveMovementDrawDiscardTypes(MatchState state, StackItemState item, GameEvent resolved)
    {
        // Use the ordinary draw path, including burnout, before deciding which
        // hand cards can be discarded. A newly drawn card is a legal choice.
        var draw = ResolveLastBreathDrawStackItem(state, item, 1);
        var events = draw.Events.Select(e => e.Kind == "TRIGGER_RESOLVED" ? resolved : e).ToList();
        if (draw.WinnerPlayerId is not null) return draw with { Events = events };
        var hand = draw.PlayerZones[item.ControllerId].Hand.Where(id =>
            IsCardObjectControlledByPlayerOrLegacyOwned(draw.CardObjects, item.ControllerId, id)).ToArray();
        if (hand.Length == 0) return draw with { Events = events };
        var choice = new PendingHandChoiceState("CHOICE-" + item.StackItemId, TriggerKinds.UnitMovedDrawDiscardTypes,
            item.ControllerId, 1, 1, hand, TriggerKinds.UnitMovedDrawDiscardTypes, item.SourceObjectId,
            TriggerKinds.UnitMovedDrawDiscardTypes, 0) { MovementObserver = item.MovementObserver };
        events.Add(new("HAND_CHOICE_REQUESTED", "已抽牌，选择一张手牌弃置", new Dictionary<string,object?> {
            ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow, ["playerId"] = choice.PlayerId,
            ["sourceObjectId"] = choice.SourceObjectId, ["effectKind"] = choice.EffectKind,
            ["requiredCount"] = 1, ["maxCount"] = 1, ["legalCount"] = hand.Length }));
        return draw with { Events = events, PendingHandChoice = choice };
    }

    private static MatchState QueueMovementDiscardInlines(MatchState before, MatchState state,
        PendingHandChoiceState choice, IReadOnlyList<string> discarded, List<GameEvent> events)
    {
        if (choice.EffectKind != TriggerKinds.UnitMovedDrawDiscardTypes) return state;
        if (choice.MovementObserver is not { DiscardInline: null } source
            || !ValidMovementObserver(source, MovementObserverEffect, choice.PlayerId, choice.SourceObjectId)
            || discarded.Count != 1) throw new InvalidOperationException("Invalid typed movement discard continuation.");
        var card = before.CardObjects[discarded[0]];
        if (card.CardNo is not { } cardNo || !PrintedPlayTypes.Value.TryGetValue(cardNo, out var types)) return state;
        var queue = state.TriggerQueue.ToList();
        // Unit/gear is deliberately two entries. There is no choose-one step.
        foreach (var type in new[] { 1, 2, 4 }.Where(type => (types & type) != 0))
        {
            var context = source with { DiscardInline = new(new(card.ObjectId, card.ObjectGeneration),
                cardNo, types, type, state.Tick) };
            var trigger = new TriggerQueueItemState($"movement-discard-{state.Tick}-{choice.SourceObjectId}-{card.ObjectId}-{type}",
                choice.PlayerId, choice.SourceObjectId, MovementDiscardInlineEffect, "CARD_DISCARDED") {
                MovementObserver = context, SourceCardNo = source.CardNo };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        return state with { TriggerQueue = queue };
    }

    private static bool NeedsMovementDiscardRuneConfirmation(StackItemState item)
        => item.EffectKind == MovementDiscardInlineEffect && item.MovementObserver?.DiscardInline?.BranchType == 2
            && item.TargetGenerations is null;

    private static ResolutionResult PrepareMovementDiscardRunes(ResolutionResult result, StackItemState item)
        => PrepareRuneTargets(result, item, 2, MovementDiscardRuneChoiceWindow, "MOVE-DISCARD-RUNES:" + item.StackItemId);

    private static ResolutionResult ResolveMovementDiscardRuneChoice(MatchState state, PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        var item = state.StackItems.LastOrDefault(s => s.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsMovementDiscardRuneConfirmation(item) || item.MovementObserver is not { } c
            || !ValidMovementObserver(c, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
            || selected.Count > 2 || selected.Any(id => !EndRuneTargets(state, item.ControllerId).Contains(id)))
            return RejectWithCorePrompts(state, "弃置装备后的符文选择已失效。", ErrorCodes.InvalidTarget);
        return ConfirmRuneTargets(state, item, selected, "MOVEMENT_DISCARD_RUNES_CONFIRMED", "符文目标已确认，双方可以响应");
    }

    internal static bool ValidMovementDiscardRuneChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.LastOrDefault(s => s.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsMovementDiscardRuneConfirmation(item) || item.MovementObserver is not { } c
            || !ValidMovementObserver(c, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)) return false;
        var ids = EndRuneTargets(state, item.ControllerId);
        return choice.ChoiceWindow == MovementDiscardRuneChoiceWindow
            && choice.ChoiceId == "MOVE-DISCARD-RUNES:" + item.StackItemId && choice.PlayerId == item.ControllerId
            && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == Math.Min(ids.Length, 2)
            && ids.SequenceEqual(choice.LegalObjectIds) && ids.SequenceEqual(choice.ContextObjectIds);
    }

    private static StackResolutionResult ResolveMovementDiscardInline(MatchState state, StackItemState item)
    {
        var context = item.MovementObserver!;
        var branch = context.DiscardInline!;
        var resolved = BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId, item.SourceObjectId,
            item.EffectKind, "INLINE_DISCARD_TYPE") { MovementObserver = context });
        if (branch.BranchType == 4)
        {
            var draw = ResolveLastBreathDrawStackItem(state, item, 1);
            return draw with { Events = draw.Events.Select(e => e.Kind == "TRIGGER_RESOLVED" ? resolved : e).ToArray() };
        }
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent> { resolved };
        if (branch.BranchType == 2)
        {
            foreach (var id in item.TargetObjectIds)
                if (EndRuneTargets(state, item.ControllerId).Contains(id)
                    && cards[id].ObjectGeneration == item.TargetGenerations!.GetValueOrDefault(id, -1) && cards[id].IsExhausted)
                {
                    cards[id] = cards[id] with { IsExhausted = false };
                    events.Add(new("RUNE_READIED", "弃置装备后让所选符文变为活跃状态", new Dictionary<string,object?> {
                        ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                        ["objectIds"] = new[] { id }, ["count"] = 1 }));
                }
        }
        else if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.SourceGeneration
            && source.Tags.Contains(CardObjectTags.UnitCard) && IsObjectOnField(state.PlayerZones, source.ObjectId))
        {
            var behavior = new CardBehaviorDefinition(context.CardNo, OfficialCardSourceIdentityGroups.DisplayName(context.CardNo),
                0, TriggerKinds.UnitMovedDrawDiscardTypes, 0, 0, PowerModifierAmount: 3);
            cards[source.ObjectId] = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, 3, out var powerEvent);
            events.Add(powerEvent);
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
