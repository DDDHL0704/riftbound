using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolvePaidBoonRuneInstruction(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        if (item.TriggerCost?.ConsumedBoons is not { Count: > 0 } consumed || !ValidTriggerCostReceipt(state, item))
            throw new InvalidOperationException("Invalid confirmed boon consumption cost.");
        // CN 383.4.a.2: the paid skill survives its source leaving the field.
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var called = CallRunes(zones, cards, item.ControllerId, consumed.Count * spec.Amount);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            Events = [new("RUNES_CALLED", "触发技能按已消耗的增益数量召出休眠符文", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds })] };
    }
}
