using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool IsGuessedDeckTopObservation(StackItemState item)
        => item.BorrowedExhaust is { Donor: { CardNo: not null } } borrowed
            && BorrowedExhaustRules.Spec(borrowed.Donor.CardNo, borrowed.AbilityIndex)?.Kind == "GUESS_DECK_TOP"
            || item.SimpleActivation is not null && P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
            && ability.SimpleEffect?.OpponentGuessesDeckTopType == true;

    // Guess before observing any card. A synchronous continuation reproduces
    // both decisions, including a revealed card's optional banish replacement.
    private static StackResolutionResult ResolveGuessedDeckTop(MatchState state, StackItemState item,
        P4ActivatedAbilityDefinition ability)
        => ResolveGuessedDeckTop(state, item, ability.DisplayName, ability.AbilityId);

    private static StackResolutionResult ResolveGuessedDeckTop(MatchState state, StackItemState item,
        string displayName, string abilityId)
    {
        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Deck-top guessing requires a resolution choice.");
        var opponent = item.TargetObjectIds.Single();
        var guessed = frame.Choose(opponent, "猜测主牌堆顶部卡牌的类型。",
            [new("UNIT", "单位"), new("EQUIPMENT", "装备"), new("SPELL", "法术")],
            kind: "DECK_TOP_TYPE_GUESS", preview: state);
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        var events = new List<GameEvent> {
            new("ABILITY_RESOLVED", $"{displayName}技能结算", new Dictionary<string, object?> {
                ["sourceObjectId"] = visibleSource, ["abilityId"] = abilityId }),
            new("CARD_TYPE_GUESSED", "对手猜测卡牌类型", new Dictionary<string, object?> {
                ["playerId"] = opponent, ["sourceObjectId"] = visibleSource, ["cardType"] = guessed }) };
        var result = NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var player = item.ControllerId;
        var seen = TakeControlledMainDeckPrefix(cards, player, zones[player].MainDeck, 1);
        if (seen.Length == 0) return result with { Events = events }; // CN432.1.c.1: no absent card to draw.
        var top = seen[0];
        var tag = guessed switch { "UNIT" => CardObjectTags.UnitCard, "EQUIPMENT" => CardObjectTags.EquipmentCard,
            _ => CardObjectTags.SpellCard };
        var matched = cards[top].Tags.Contains(tag);
        events.Add(new("MAIN_DECK_CARDS_REVEALED", "展示主牌堆顶部的一张牌", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = visibleSource, ["cardIds"] = seen, ["count"] = 1 }));
        var remaining = ObserveDeckCards(state, zones, cards, item, player, seen, "REVEAL", 0, events);
        if (matched)
        {
            var recycled = RecycleLookedMainDeckCards(state, zones, cards, player, item.SourceObjectId, remaining, state.RngCursor);
            events.AddRange(recycled.Events);
            result = result with { RngCursor = recycled.RngCursor };
        }
        else if (remaining.Contains(top) && zones[player].MainDeck.FirstOrDefault() == top)
        {
            var draw = ApplyDrawToPlayer(state, zones, cards, NormalizeScoresForSeats(state), player, 1,
                state.RngCursor, events, deferNormalVictory: true);
            result = result with { PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor };
        }
        return result with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
