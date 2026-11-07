using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 383.3.b / 417.3: this leading instruction is a confirmation cost.
    internal static bool HasLastBreathSourceRecycleCost(StackItemState item)
        => item.LastBreathRuneCall is { Source: { Generation: >= 0 and < long.MaxValue } } context
            && context.Source.ObjectId == item.SourceObjectId && context.ControllerId == item.ControllerId
            && context.CardNo == item.CardNo && context.Kind == item.EffectKind
            && TryGetLastBreathRuneCallSpec(context, out var spec)
            && UnitDestroyedTriggerSpecRules.IsLastBreathRecycleSelfReadyAllRunesTrigger(spec);

    private static string[] LastBreathSourceRecycleChoices(MatchState state, StackItemState item)
    {
        if (item.TriggerCost is not null || !HasLastBreathSourceRecycleCost(item)
            || !ValidLastBreathRuneCallStack(state, item)
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || IsTokenObject(source) || source.ObjectGeneration != TriggerCostSourceGeneration(item)
            || source.OwnerId is not { } ownerId || !state.PlayerZones.TryGetValue(ownerId, out var owner)
            || !owner.Graveyard.Contains(source.ObjectId, StringComparer.Ordinal)) return [];
        return [source.ObjectId];
    }

    private static ResolutionResult PayLastBreathSourceRecycleCost(MatchState state, StackItemState item,
        IReadOnlyList<string> selected)
    {
        if (selected.Count != 1 || !selected.SequenceEqual(LastBreathSourceRecycleChoices(state, item)))
            return RejectWithCorePrompts(state, "只能回收这次死亡后的原来源来支付费用。", ErrorCodes.InvalidTarget);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var source = cards[item.SourceObjectId];
        var paid = new ObjectBinding(source.ObjectId, source.ObjectGeneration);
        var recycled = RecycleGraveyardCards(state, zones, cards, source.OwnerId!, source.ObjectId,
            selected, TriggerCostWindow, recyclingPlayerId: item.ControllerId);
        var events = recycled.Events.Select(ev => ev with {
            Payload = new Dictionary<string, object?>(ev.Payload) {
                ["ownerPlayerId"] = source.OwnerId, ["sourceGeneration"] = paid.Generation,
                ["stackItemId"] = item.StackItemId, ["deathId"] = item.LastBreathRuneCall!.DeathId,
                ["choiceId"] = "TRIGGER-COST:" + item.StackItemId, ["choiceWindow"] = TriggerCostWindow,
                ["sourceZone"] = TriggerZones.Graveyard, ["destinationZone"] = TriggerZones.MainDeck } }).ToList();
        return CompleteTriggerCost(state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RngCursor = recycled.RngCursor },
            item, new(paid) { SourceWasRecycled = true }, events);
    }
}
