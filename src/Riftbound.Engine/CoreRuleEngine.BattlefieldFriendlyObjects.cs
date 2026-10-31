namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static string[] BattlefieldOtherFriendlyObjectParticipants(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, string sourceId, string controller)
    {
        if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.IsFaceDown
            || !IsFieldObjectControlledByZonePlayer(zones, state.CardObjects, sourceId)
            || state.ObjectLocations.GetValueOrDefault(sourceId) is not { Zone: "BATTLEFIELD", BattlefieldObjectId: { } field }
            || BattlefieldLocalRules.Battlefield(state, field) is null) return [];
        return zones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal)
            .Where(id => id != sourceId && state.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown
                && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))
                && IsFieldObjectControlledByZonePlayer(zones, state.CardObjects, id)
                && EffectiveFieldControllerId(zones, id, card) == controller
                && IsObjectLocatedAtBattlefield(zones, state.ObjectLocations, id, field))
            .Order(StringComparer.Ordinal).ToArray();
    }
}
