using Riftbound.Engine;

namespace Riftbound.ConformanceTests;

internal static class RadUnitEffectTestFixture
{
    internal static MatchState Place(MatchState state, string id, string controller, string zone, string? field = null)
    {
        string[] Without(IReadOnlyList<string> ids) => ids.Where(value => value != id).ToArray();
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            MainDeck = Without(p.Value.MainDeck), RuneDeck = Without(p.Value.RuneDeck),
            Hand = Without(p.Value.Hand), Base = Without(p.Value.Base),
            Battlefields = Without(p.Value.Battlefields), Graveyard = Without(p.Value.Graveyard),
            Banished = Without(p.Value.Banished), LegendZone = Without(p.Value.LegendZone),
            ChampionZone = Without(p.Value.ChampionZone) });
        var destination = zones[controller];
        zones[controller] = zone switch {
            "HAND" => destination with { Hand = [..destination.Hand, id] },
            "MAIN_DECK" => destination with { MainDeck = [..destination.MainDeck, id] },
            "BASE" => destination with { Base = [..destination.Base, id] },
            "BATTLEFIELD" => destination with { Battlefields = [..destination.Battlefields, id] },
            "GRAVEYARD" => destination with { Graveyard = [..destination.Graveyard, id] },
            "BANISHED" => destination with { Banished = [..destination.Banished, id] },
            _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "Unsupported fixture zone.") };
        return state with {
            PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { ControllerId = controller } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                [id] = new(controller, zone, field) } };
    }
}
