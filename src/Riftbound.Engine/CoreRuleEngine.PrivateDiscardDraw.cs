using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Discard is an instruction, not a cost: an empty hand does not prevent
    // the subsequent draw. Collect private decisions before publishing cards.
    private static StackResolutionResult ResolvePrivateDiscardDraw(MatchState state, StackItemState item,
        int discardCount, int drawCount)
    {
        var player = item.ControllerId;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var hand = zones[player].Hand.Order(StringComparer.Ordinal).ToList();
        var chosen = new List<string>();
        for (var i = 0; i < Math.Min(discardCount, zones[player].Hand.Count); i++)
        {
            var selected = RuleChoices.Value!.Choose(player, "选择弃置的手牌，然后抽牌。", hand.Select((id, index) =>
                new RuleChoiceOption("HAND:" + index, cards[id].CardNo is { } no
                    ? OfficialCardSourceIdentityGroups.DisplayName(no) : "手牌", [id])).ToArray(),
                kind: "PRIVATE_HAND_DISCARD", preview: state);
            var id = hand[int.Parse(selected["HAND:".Length..], System.Globalization.CultureInfo.InvariantCulture)];
            chosen.Add(id); hand.Remove(id);
        }
        var events = new List<GameEvent>();
        foreach (var id in chosen)
        {
            if (!TryDiscardCardFromHand(zones, cards, player, id))
                throw new InvalidOperationException("Chosen discard is no longer in hand.");
            events.Add(new("CARD_DISCARDED", "技能弃置手牌", new Dictionary<string, object?> {
                ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = id, ["destinationZone"] = "GRAVEYARD" }));
        }
        ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, player, "CARD_DISCARDED", item.SourceObjectId, chosen, events);
        var effects = MarkPlayerDiscardedHandCardsThisTurn(state.UntilEndOfTurnEffects, player, chosen, zones);
        var current = state with { PlayerZones = zones, CardObjects = cards, UntilEndOfTurnEffects = effects };
        var draw = ApplyDrawToPlayer(current, zones, cards, state.PlayerScores, player, drawCount,
            state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            UntilEndOfTurnEffects = effects, PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId,
            RngCursor = draw.RngCursor, Events = events, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
