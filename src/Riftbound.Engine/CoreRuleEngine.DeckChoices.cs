using Riftbound.Contracts;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public sealed record DeckChoiceContext(string StackItemId, int ExecutionIndex);

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> DeckCardNames = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(card => card.CardNo, card => card.CardName));
    internal static string DeckChoiceLabel(MatchState state, string id)
    {
        var number = state.CardObjects.GetValueOrDefault(id)?.CardNo ?? "";
        return DeckCardNames.Value.GetValueOrDefault(number, number);
    }

    internal static bool IsDeferredDeckChoice(CardBehaviorDefinition behavior)
        => (behavior.DrawsSelectedMainDeckTarget || behavior.RecyclesSelectedMainDeckTargets)
            && behavior.MainDeckLookCount>0 && behavior.RequiredTargetCount > 0;

    internal static bool TryGetDeckChoiceBehavior(MatchState state, StackItemState item, out CardBehaviorDefinition behavior)
    {
        if (item.HeldContext is { Kind: "LOOK_EQUIPMENT" } held
            && HeldDefinition(held.CardNo)?.Kind == held.Kind)
            return CardBehaviorRegistry.TryGetByCardNo(held.CardNo, out behavior!) && IsDeferredDeckChoice(behavior);
        if(!CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind,out behavior!))return false;
        return IsDeferredDeckChoice(behavior);
    }

    private static StackResolutionResult BeginDeckChoice(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        var viewed = state.PlayerZones[item.ControllerId].MainDeck.Take(behavior.MainDeckLookCount).ToArray();
        var zones=NormalizeZonesForSeats(state);var cards=state.CardObjects.ToDictionary();var observationEvents=new List<GameEvent>();
        viewed=ObserveDeckCards(state,zones,cards,item,item.ControllerId,viewed,"LOOK",item.CompletedDeckExecutions,observationEvents);
        state=state with {PlayerZones=zones,CardObjects=cards,ObjectLocations=ReconcileObjectLocations(state.ObjectLocations,zones)};
        if (viewed.Length == 0)
        {
            var nextItem = item with { CompletedDeckExecutions = item.CompletedDeckExecutions + 1,
                DeckChoiceCompleted = item.CompletedDeckExecutions + 1 >= item.EffectRepeatCount };
            state = state with { StackItems = state.StackItems.Select(slot =>
                slot.StackItemId == item.StackItemId ? nextItem : slot).ToArray() };
            var finished=ResolveStackItemEffect(state, nextItem);
            return finished with {Events=observationEvents.Concat(finished.Events).ToArray()};
        }
        var legal = viewed.Where(id => (string.IsNullOrEmpty(behavior.MainDeckTargetRequiredTag)
            || CardObjectHasTag(state.CardObjects, id, behavior.MainDeckTargetRequiredTag))
            && (behavior.MainDeckTargetMinimumManaCost <= 0
                || state.CardObjects.TryGetValue(id, out var card) && card.ManaCost >= behavior.MainDeckTargetMinimumManaCost)).ToArray();
        var mandatory = behavior.MinTargetCount != 0 && legal.Length > 0;
        var choice = new PendingCardChoiceState($"DECK-{item.StackItemId}-{item.CompletedDeckExecutions}", "DECK_EFFECT", item.ControllerId,
            mandatory ? 1 : 0, legal.Length > 0 ? 1 : 0, legal, viewed,
            reason: $"查看牌库并选择卡牌（第 {item.CompletedDeckExecutions + 1}/{item.EffectRepeatCount} 次）",
            sourceObjectId: item.SourceObjectId, effectKind: item.EffectKind)
            { DeckContext = new(item.StackItemId, item.CompletedDeckExecutions) };
        return new(state.PlayerZones, state.CardObjects, state.PlayerScores, state.PlayerExperience, state.RunePools,
            state.UntilEndOfTurnEffects, null,
            observationEvents.Concat([new GameEvent("CARD_CHOICE_REQUESTED", "结算暂停，等待玩家私下选择所查看的牌", new Dictionary<string, object?>
            {
                ["playerId"] = item.ControllerId, ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow,
                ["sourceObjectId"] = item.SourceObjectId, ["contextCount"] = viewed.Length
            })]).ToArray(), [], state.StackItems, [], null, [], state.RngCursor, PendingCardChoice: choice);
    }

    private static ResolutionResult ResolveDeckChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        var context = choice.DeckContext;
        var item = state.StackItems.LastOrDefault();
        if (context is null || item is null || context.StackItemId != item.StackItemId
            || context.ExecutionIndex != item.CompletedDeckExecutions
            || !TryGetDeckChoiceBehavior(state,item, out var behavior)
            || !(ObservedRemainingDeckCards(state,item,item.ControllerId,item.CompletedDeckExecutions)
                ?? state.PlayerZones[item.ControllerId].MainDeck.Take(behavior.MainDeckLookCount).ToArray()).SequenceEqual(choice.ContextObjectIds))
            return RejectWithCorePrompts(state, "牌库选择上下文已失效。", ErrorCodes.InvalidTarget);
        var zones = NormalizeZonesForSeats(state);
        var cards=state.CardObjects.ToDictionary();var revealEvents=new List<GameEvent>();
        var remainingCount=choice.ContextObjectIds.Count;
        if(!string.IsNullOrEmpty(behavior.MainDeckTargetRequiredTag) && selected.Count>0)
        {
            revealEvents.Add(new("CARD_REVEALED","展示选择的卡牌",new Dictionary<string,object?> {
                ["playerId"]=item.ControllerId,["sourceObjectId"]=item.SourceObjectId,["objectId"]=selected[0],["cardNo"]=cards[selected[0]].CardNo}));
            var kept=ObserveDeckCards(state,zones,cards,item,item.ControllerId,selected,"REVEAL",item.CompletedDeckExecutions,revealEvents,selectionReveal:true);
            remainingCount-=selected.Count-kept.Length;selected=kept;
        }
        var result = behavior.RecyclesSelectedMainDeckTargets
            ? RecycleSelectedMainDeckTargets(state,zones, cards,item.ControllerId,item.SourceObjectId,selected,remainingCount)
            : DrawSelectedMainDeckTargetsAndRecycleRest(state, zones, cards, item.ControllerId, item.SourceObjectId,
                selected, remainingCount, behavior.RecyclesUnselectedMainDeckLookCards);
        var events = revealEvents.Concat(result.Events.Select(e => e.Kind == "CARDS_RECYCLED"
            ? e with { Payload = e.Payload.Where(field => field.Key != "cardIds").ToDictionary(field => field.Key, field => field.Value) }
            : e)).ToList();
        // Card Trick says 'put into hand', not 'draw'. Filtered choices explicitly
        // reveal the selected card; unselected looked cards remain private.
        if (!behavior.MainDeckSelectionIsDraw)
            events = events.Select(e => e.Kind == "CARD_DRAWN" ? e with { Kind = "CARD_ADDED_TO_HAND", Description = $"{item.ControllerId} 将 {selected.Count} 张牌加入手牌" } : e).ToList();
        events.Add(new("CARD_CHOICE_RESOLVED", "已完成本次牌库选择", new Dictionary<string, object?>
        { ["choiceId"] = choice.ChoiceId, ["playerId"] = item.ControllerId, ["chosenCount"] = selected.Count }));
        var nextItem = item with { CompletedDeckExecutions = item.CompletedDeckExecutions + 1,
            DeckChoiceCompleted = item.CompletedDeckExecutions + 1 >= item.EffectRepeatCount };
        var next = state with
        {
            PendingCardChoice = null, PlayerZones = zones, CardObjects=cards, RngCursor = result.RngCursor,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(nextItem).ToArray()
        };
        var resumed = ResolvePassPriority(next, new("deck-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }
}
