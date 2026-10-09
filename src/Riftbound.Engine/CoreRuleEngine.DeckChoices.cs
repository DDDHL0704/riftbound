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
        => behavior.DrawsSelectedMainDeckTarget && behavior.RequiredTargetCount > 0;

    internal static bool TryGetDeckChoiceBehavior(StackItemState item, out CardBehaviorDefinition behavior)
    {
        if (item.HeldContext is { Kind: "LOOK_EQUIPMENT" } held
            && HeldDefinition(held.CardNo)?.Kind == held.Kind)
            return CardBehaviorRegistry.TryGetByCardNo(held.CardNo, out behavior!) && IsDeferredDeckChoice(behavior);
        return CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out behavior!) && IsDeferredDeckChoice(behavior);
    }

    private static StackResolutionResult BeginDeckChoice(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        var viewed = state.PlayerZones[item.ControllerId].MainDeck.Take(behavior.MainDeckLookCount).ToArray();
        if (viewed.Length == 0)
            return ResolveStackItemEffect(state, item with { DeckChoiceCompleted = true });
        var legal = viewed.Where(id => string.IsNullOrEmpty(behavior.MainDeckTargetRequiredTag)
            || CardObjectHasTag(state.CardObjects, id, behavior.MainDeckTargetRequiredTag)).ToArray();
        var mandatory = behavior.MinTargetCount != 0 && legal.Length > 0;
        var choice = new PendingCardChoiceState($"DECK-{item.StackItemId}-{item.CompletedDeckExecutions}", "DECK_EFFECT", item.ControllerId,
            mandatory ? 1 : 0, legal.Length > 0 ? 1 : 0, legal, viewed,
            reason: $"查看牌库并选择卡牌（第 {item.CompletedDeckExecutions + 1}/{item.EffectRepeatCount} 次）",
            sourceObjectId: item.SourceObjectId, effectKind: item.EffectKind)
            { DeckContext = new(item.StackItemId, item.CompletedDeckExecutions) };
        return new(state.PlayerZones, state.CardObjects, state.PlayerScores, state.PlayerExperience, state.RunePools,
            state.UntilEndOfTurnEffects, null,
            [new("CARD_CHOICE_REQUESTED", "结算暂停，等待玩家私下选择所查看的牌", new Dictionary<string, object?>
            {
                ["playerId"] = item.ControllerId, ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow,
                ["sourceObjectId"] = item.SourceObjectId, ["contextCount"] = viewed.Length
            })], [], state.StackItems, [], null, [], state.RngCursor, PendingCardChoice: choice);
    }

    private static ResolutionResult ResolveDeckChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        var context = choice.DeckContext;
        var item = state.StackItems.LastOrDefault();
        if (context is null || item is null || context.StackItemId != item.StackItemId
            || context.ExecutionIndex != item.CompletedDeckExecutions
            || !TryGetDeckChoiceBehavior(item, out var behavior)
            || !state.PlayerZones[item.ControllerId].MainDeck.Take(behavior.MainDeckLookCount).SequenceEqual(choice.ContextObjectIds))
            return RejectWithCorePrompts(state, "牌库选择上下文已失效。", ErrorCodes.InvalidTarget);
        var zones = NormalizeZonesForSeats(state);
        var result = DrawSelectedMainDeckTargetsAndRecycleRest(state, zones, item.ControllerId, item.SourceObjectId,
            selected, behavior.MainDeckLookCount, behavior.RecyclesUnselectedMainDeckLookCards);
        var events = result.Events.Select(e => e.Kind == "CARDS_RECYCLED"
            ? e with { Payload = e.Payload.Where(field => field.Key != "cardIds").ToDictionary(field => field.Key, field => field.Value) }
            : e).ToList();
        // Card Trick says 'put into hand', not 'draw'. Filtered choices explicitly
        // reveal the selected card; unselected looked cards remain private.
        if (!behavior.MainDeckSelectionIsDraw)
            events = events.Select(e => e.Kind == "CARD_DRAWN" ? e with { Kind = "CARD_ADDED_TO_HAND", Description = $"{item.ControllerId} 将 {selected.Count} 张牌加入手牌" } : e).ToList();
        if (!string.IsNullOrEmpty(behavior.MainDeckTargetRequiredTag) && selected.Count > 0)
            events.Add(new("CARD_REVEALED", "展示选择的卡牌", new Dictionary<string, object?>
            {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["objectId"] = selected[0], ["cardNo"] = state.CardObjects[selected[0]].CardNo
            }));
        events.Add(new("CARD_CHOICE_RESOLVED", "已完成本次牌库选择", new Dictionary<string, object?>
        { ["choiceId"] = choice.ChoiceId, ["playerId"] = item.ControllerId, ["chosenCount"] = selected.Count }));
        var nextItem = item with { CompletedDeckExecutions = item.CompletedDeckExecutions + 1,
            DeckChoiceCompleted = item.CompletedDeckExecutions + 1 >= item.EffectRepeatCount };
        var next = state with
        {
            PendingCardChoice = null, PlayerZones = zones, RngCursor = result.RngCursor,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(nextItem).ToArray()
        };
        var resumed = ResolvePassPriority(next, new("deck-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }
}
