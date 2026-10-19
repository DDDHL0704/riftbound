using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveMovementReveal(MatchState state, StackItemState item,
        TriggerSpec spec, List<GameEvent> events)
    {
        var context = item.MovementObserver!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var player = item.ControllerId;
        var execution = item.CompletedDeckExecutions;
        var previous = (state.ObservedDeckPlays ?? []).FirstOrDefault(c => c.Instruction.Parent.StackItemId == item.StackItemId
            && c.Instruction.PlayerId == player && c.Instruction.ExecutionIndex == execution);
        var seen = previous?.Instruction.Seen.Select(b => b.ObjectId).ToArray()
            ?? TakeControlledMainDeckPrefix(cards, player, zones[player].MainDeck, 1);
        var top = seen.FirstOrDefault();
        var unit = top is not null && cards.TryGetValue(top, out var revealed) && revealed.Tags.Contains(CardObjectTags.UnitCard);
        if (seen.Length > 0 && previous is null)
            events.Add(new("CARDS_REVEALED", "移动技能展示主牌堆顶部一张牌", new Dictionary<string, object?> {
                ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["cardIds"] = seen,
                ["count"] = seen.Length, ["zone"] = TriggerZones.MainDeck }));
        var remaining = ObserveDeckCards(state, zones, cards, item, player, seen, "REVEAL", execution, events);
        var result = NoopStackResolutionResult(state);
        if (unit)
        {
            // Keep the original revealed identity if its observation ability banishes it.
            // Never draw the replacement top card, or turn that removal into the else branch.
            if (top is not null && remaining.Contains(top))
            {
                var drawState = state with { PlayerZones = zones, CardObjects = cards,
                    ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
                var draw = ApplyDrawToPlayer(drawState, zones, state.PlayerScores, player, 1, state.RngCursor, events);
                result = result with { PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor };
            }
        }
        else
        {
            if (top is not null && remaining.Contains(top))
            {
                zones[player] = zones[player] with { MainDeck = RemoveFromZone(zones[player].MainDeck, top),
                    Graveyard = zones[player].Graveyard.Append(top).ToArray() };
                events.Add(new("CARDS_MILLED", "将展示的非单位牌放入废牌堆", new Dictionary<string, object?> {
                    ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["cardIds"] = new[] { top },
                    ["count"] = 1, ["sourceZone"] = TriggerZones.MainDeck, ["destinationZone"] = TriggerZones.Graveyard }));
            }
            // CN055: impossible reveal/trash instructions do not remove the remaining Might instruction.
            if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.SourceGeneration
                && source.Tags.Contains(CardObjectTags.UnitCard) && IsObjectOnField(zones, source.ObjectId))
            {
                var amount = spec.PowerDelta!.Value;
                var behavior = new CardBehaviorDefinition(context.CardNo, OfficialCardSourceIdentityGroups.DisplayName(context.CardNo),
                    0, spec.Kind, 0, 0, PowerModifierAmount: amount);
                cards[source.ObjectId] = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, amount, out var powerEvent);
                events.Add(powerEvent);
            }
        }
        return result with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
