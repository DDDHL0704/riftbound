using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static void ConsumeChosenBoonsAndReady(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        using var instruction = new MightInstructionScope(state, zones, cards, events);
        var current = state with { PlayerZones = zones, CardObjects = cards };
        foreach (var id in item.TargetObjectIds) {
            if (!IsPlayerControlledFieldUnitObject(current, item.ControllerId, id)
                || !cards.TryGetValue(id, out var unit) || !unit.Tags.Contains(CardObjectTags.Boon)
                || item.TargetGenerations is null || !item.TargetGenerations.TryGetValue(id, out var generation)
                || unit.ObjectGeneration != generation) continue;
            AddReadiedObservation(zones, cards, unit, events);
            cards[id] = unit with { Power = unit.Power - 1,
                Tags = unit.Tags.Where(tag => tag != CardObjectTags.Boon).ToArray(), IsExhausted = false };
            events.Add(new("BOON_CONSUMED", "消耗所选单位的增益", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = id, ["previousPower"] = unit.Power, ["power"] = unit.Power - 1 }));
            if (unit.IsExhausted) events.Add(CaptureUnitReadied(new GameEvent("UNIT_READIED", "消耗增益后让所选单位活跃", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = id, ["reason"] = item.EffectKind, ["wasExhausted"] = unit.IsExhausted }), zones, cards, id, unit.IsExhausted));
        }
    }
}
