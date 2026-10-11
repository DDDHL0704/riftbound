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
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent>();
        var sourceController = EffectiveFieldControllerId(zones, gear.ObjectId, gear);
        var targetLocation = locations[targetId];
        if (!string.IsNullOrWhiteSpace(gear.AttachedToObjectId))
            events.Add(new("EQUIPMENT_DETACHED", "武装从原宿主卸除", new Dictionary<string, object?> {
                ["sourceObjectId"] = gear.ObjectId, ["equipmentObjectId"] = gear.ObjectId,
                ["unitObjectId"] = gear.AttachedToObjectId, ["controllerId"] = sourceController,
                ["ownerId"] = gear.OwnerId, ["reason"] = "REATTACH" }));
        var previousLocation = locations[gear.ObjectId];
        if (previousLocation.Zone != targetLocation.Zone || previousLocation.BattlefieldObjectId != targetLocation.BattlefieldObjectId)
        {
            foreach (var (player, zone) in zones.ToArray())
                zones[player] = zone with { Base = zone.Base.Where(id => id != gear.ObjectId).ToArray(),
                    Battlefields = zone.Battlefields.Where(id => id != gear.ObjectId).ToArray() };
            var sourceZones = zones[sourceController];
            zones[sourceController] = targetLocation.Zone == "BASE"
                ? sourceZones with { Base = [..sourceZones.Base, gear.ObjectId] }
                : sourceZones with { Battlefields = [..sourceZones.Battlefields, gear.ObjectId] };
        }
        locations[gear.ObjectId] = new(sourceController, targetLocation.Zone, targetLocation.BattlefieldObjectId);
        cards[gear.ObjectId] = gear with { AttachedToObjectId = targetId };
        events.Add(new("EQUIPMENT_ATTACHED", "装配技能结算，武装贴附至目标单位", new Dictionary<string, object?> {
            ["sourceObjectId"] = gear.ObjectId, ["equipmentObjectId"] = gear.ObjectId, ["unitObjectId"] = targetId,
            ["controllerId"] = sourceController, ["ownerId"] = gear.OwnerId, ["attachedToObjectId"] = targetId,
            ["equipmentCardNo"] = gear.CardNo, ["targetPower"] = ResolveCurrentFieldUnitPower(state, target),
            ["optionalCosts"] = item.OptionalCosts.ToArray(),
            ["paymentResourceActions"] = item.OptionalCosts.Where(c => c.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal)).ToArray(), ["reason"] = EquipEffect }));
        // The existing attach-trigger payment surface starts only after attachment.
        // Its separate trigger confirmation/response migration is tracked in the audit.
        TryOpenJaxWeaponAttachPaymentWindow(zones, cards, item.ControllerId, targetId, gear.ObjectId,
            state.Tick + 1, events, out var payment);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, Events = events, PendingPayment = payment };
    }
}
