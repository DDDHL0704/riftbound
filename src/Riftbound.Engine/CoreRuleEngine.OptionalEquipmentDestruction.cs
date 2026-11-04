using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The equipment is paid during trigger confirmation. The reward waits for
    // resolution so a response can still remove the captured source.
    private static StackResolutionResult ResolvePaidEquipmentDestructionMight(
        MatchState state, StackItemState item, int amount)
    {
        if (item.TriggerCost?.DestroyedPermanents is not { Count: 1 }
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != item.UnitEvent!.SourceGeneration || !IsObjectOnField(state.PlayerZones, source.ObjectId))
            return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        cards[source.ObjectId] = ApplyDirectUntilEndPowerModifier(source, source.ObjectId, item.SourceObjectId,
            item.CardNo, item.EffectKind, "DESTROY_FRIENDLY_EQUIPMENT", amount, source.Power + amount);
        var ev = new GameEvent("UNIT_POWER_CHANGED", "摧毁友方装备后，来源单位本回合获得战力", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = source.ObjectId, ["powerDelta"] = amount });
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [ev] };
    }
}
