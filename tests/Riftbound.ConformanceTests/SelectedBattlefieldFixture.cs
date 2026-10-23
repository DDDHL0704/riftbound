using Riftbound.Engine;

namespace Riftbound.ConformanceTests;

// Legacy fixtures omitted physical fields. Give existing units explicit locations
// and keep the main-action state uncontested; retain stats and controller errors.
internal static class SelectedBattlefieldFixture
{
    internal const string Field = "N54-SELECTED-FIELD";

    internal const string FriendlyField = "N54-FRIENDLY-FIELD";

    internal static MatchState WithExplicitFields(MatchState state)
    {
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [Field] = new(Field, cardNo: "OGN·278/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2")
        };
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal);
        foreach (var (player, zone) in zones)
            foreach (var id in zone.Battlefields)
                locations[id] = new(player, "BATTLEFIELD", player == "P1" ? FriendlyField : Field);
        if (zones["P1"].Battlefields.Count > 0)
        {
            cards[FriendlyField] = new(FriendlyField, cardNo: "OGN·278/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: "P1");
            zones["P1"] = zones["P1"] with { Battlefields = zones["P1"].Battlefields.Append(FriendlyField).ToArray() };
            locations[FriendlyField] = new("P1", "BATTLEFIELD", FriendlyField);
        }
        zones["P2"] = zones["P2"] with { Battlefields = zones["P2"].Battlefields.Append(Field).Distinct().ToArray() };
        locations[Field] = new("P2", "BATTLEFIELD", Field);
        return state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
    }
}
