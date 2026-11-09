namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 107.1.c.1: an attached non-unit equipment can be in its host's
    // base while another player controls it. Location does not change control.
    private static bool HasValidFieldEquipmentAttachment(MatchState state, string objectId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var gear) || gear.IsFaceDown
            || !gear.Tags.Contains(CardObjectTags.EquipmentCard) || FieldObjectTypeRules.HasUnitType(gear)
            || gear.AttachedToObjectId is not { Length: > 0 } hostId
            || !state.CardObjects.TryGetValue(hostId, out var host) || !FieldObjectTypeRules.IsVisibleUnit(host)
            || !string.IsNullOrWhiteSpace(host.AttachedToObjectId)
            || FindFieldObjectLocation(state.PlayerZones, objectId) is not { } location
            || FindFieldObjectLocation(state.PlayerZones, hostId) != location
            || !state.Seats.ContainsKey(location.PlayerId)
            || !SourceObjectControlledByPlayerOrLegacyOwned(host, location.PlayerId)
            || !state.Seats.ContainsKey(EffectiveFieldControllerId(state.PlayerZones, objectId, gear))) return false;
        if (location.Zone == "BASE") return true;
        return state.ObjectLocations.TryGetValue(objectId, out var equipmentAt)
            && state.ObjectLocations.TryGetValue(hostId, out var hostAt)
            && equipmentAt.Zone == "BATTLEFIELD" && hostAt.Zone == "BATTLEFIELD"
            && !string.IsNullOrWhiteSpace(equipmentAt.BattlefieldObjectId)
            && equipmentAt.BattlefieldObjectId == hostAt.BattlefieldObjectId;
    }
}
