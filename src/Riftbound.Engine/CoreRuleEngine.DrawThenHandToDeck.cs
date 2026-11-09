using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string HandToDeckCardChoiceKind = "HAND_TO_DECK_CARD";
    internal const string HandToDeckPositionChoiceKind = "HAND_TO_DECK_POSITION";

    // CN355.10.a/359.3.e: the hidden card is chosen during resolution,
    // after drawing. The ordinary instruction transcript binds both decisions
    // to the original command and replays the draw exactly once.
    private static StackResolutionResult ResolveDrawThenHandToDeck(MatchState state, StackItemState item, int count)
    {
        var drawn = ResolveLastBreathDrawStackItem(state, item, count);
        if (drawn.WinnerPlayerId is not null) return drawn;
        var zones = drawn.PlayerZones.ToDictionary();
        var cards = drawn.CardObjects.ToDictionary();
        var player = item.ControllerId;
        var hand = zones[player].Hand.Order(StringComparer.Ordinal).ToArray();
        if (hand.Length == 0) return drawn;
        var preview = state with { PlayerZones = zones.ToDictionary(), CardObjects = cards.ToDictionary(),
            PlayerScores = drawn.PlayerScores, RngCursor = drawn.RngCursor,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        var options = hand.Select((id, index) => new RuleChoiceOption("HAND:" + index,
            DeckChoiceLabel(preview, id), [id])).ToArray();
        var selected = RuleChoices.Value!.Choose(player, "选择一张手牌放入主牌堆。", options,
            kind: HandToDeckCardChoiceKind, preview: preview);
        var id = options.Single(option => option.Id == selected).ObjectIds![0];
        var position = RuleChoices.Value.Choose(player, "选择放到主牌堆的顶部或底部。",
            [new("TOP", "主牌堆顶部"), new("BOTTOM", "主牌堆底部")],
            kind: HandToDeckPositionChoiceKind, preview: preview);
        var own = zones[player];
        zones[player] = own with { Hand = RemoveFromZone(own.Hand, id),
            MainDeck = position == "TOP" ? new[] { id }.Concat(own.MainDeck).ToArray() : own.MainDeck.Append(id).ToArray() };
        ResetCardOutsidePlay(zones, cards, id, cards[id], player);
        // Hand and deck are private. Neither the card identity nor the chosen
        // end of the deck is published to the other player.
        var placed = new GameEvent("HAND_CARD_PUT_IN_MAIN_DECK", "将一张手牌放入主牌堆",
            new Dictionary<string, object?> { ["playerId"] = player, ["count"] = 1,
                ["sourceHash"] = MatchStateHasher.HashValue(item.SourceObjectId), ["destinationZone"] = "MAIN_DECK" });
        return drawn with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            Events = drawn.Events.Append(placed).ToArray() };
    }
}
