using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Lower-box keywords belong to the attached unit (CN 719). Keep each
    // grant so multiple numeric keywords add, and derive them again on detach.
    private static IReadOnlyDictionary<string, string[]> AttachedArmamentKeywordGrants(MatchState state)
        => state.CardObjects.Values.Where(gear => gear.AttachedToObjectId is { } host
                && !gear.IsFaceDown && gear.Tags.Contains(CardObjectTags.EquipmentCard)
                && OfficialArmamentCatalog.Find(gear.CardNo)?.GrantedKeywords is { Count: > 0 }
                && IsObjectOnField(state.PlayerZones, gear.ObjectId)
                && state.CardObjects.TryGetValue(host, out var unit) && IsFaceUpNonStandbyUnit(unit)
                && IsObjectOnField(state.PlayerZones, host)
                && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host))
            .GroupBy(gear => gear.AttachedToObjectId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(gear => gear.ObjectId, StringComparer.Ordinal)
                .SelectMany(gear => OfficialArmamentCatalog.Find(gear.CardNo)!.GrantedKeywords!).ToArray(), StringComparer.Ordinal);
}
