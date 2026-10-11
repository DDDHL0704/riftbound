using Riftbound.Contracts;
namespace Riftbound.Engine;

public sealed record EquipContext(string CardNo, long SourceGeneration);

public sealed partial class CoreRuleEngine
{
    internal const string EquipEffect = "ASSEMBLE_EQUIPMENT";

    internal static bool ValidEquipContext(StackItemState item)
        => item.Equip is null ? item.EffectKind != EquipEffect
            : item.EffectKind == EquipEffect && item.Equip.CardNo == item.CardNo
                && AssembleEquipmentProfileCatalog.TryGet(item.CardNo, out _)
                && item.Equip.SourceGeneration >= 0 && !item.SourceConfirmed
                && item.TargetObjectIds.Count == 1 && item.TargetObjectIds[0] != item.SourceObjectId
                && item.TargetGenerations is { Count: 1 }
                && item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0;

    private static StackResolutionResult ResolveEquip(MatchState state, StackItemState item)
    {
        if (!ValidEquipContext(item)) return NoopStackResolutionResult(state);
        var targetId = item.TargetObjectIds[0];
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var gear)
            || gear.ObjectGeneration != item.Equip!.SourceGeneration || gear.IsFaceDown
            || !gear.Tags.Contains(CardObjectTags.EquipmentCard) || !IsObjectOnField(state.PlayerZones, gear.ObjectId)
            || !state.CardObjects.TryGetValue(targetId, out var target)
            || target.ObjectGeneration != item.TargetGenerations![targetId] || target.IsFaceDown
            || !target.Tags.Contains(CardObjectTags.UnitCard) || !IsObjectOnField(state.PlayerZones, targetId)
            || EffectiveFieldControllerId(state.PlayerZones, targetId, target) != item.ControllerId
            || !IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, targetId))
            return NoopStackResolutionResult(state);
        if (gear.AttachedToObjectId == targetId) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        TryAttachEquipment(zones, cards, state.ObjectLocations, item.ControllerId, gear.ObjectId, gear.ObjectId,
            targetId, EquipEffect, events, out var locations, new Dictionary<string, object?> {
                ["targetPower"] = ResolveCurrentFieldUnitPower(state, target), ["optionalCosts"] = item.OptionalCosts.ToArray(),
                ["paymentResourceActions"] = item.OptionalCosts.Where(c => c.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal)).ToArray() });
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, Events = events };
    }
}
