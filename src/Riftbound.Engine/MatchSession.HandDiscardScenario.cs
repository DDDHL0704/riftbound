namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // A logged controlled QA position. Discard, ordinary trigger admission and
    // response use real commands; it is not an ordinary-opening certificate.
    private static MatchState BuildHandDiscardScenario(MatchState current, DevScenarioSeed seed)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Discard QA requires official catalog printings.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("SPELL", "VEN·008", seed.P1, "HAND"); Put("FODDER", "VEN·049", seed.P1, "HAND");
        Put("JINX", "OGN·202/298", seed.P1, "BASE"); Put("GRAVE", "OGN·219/298", seed.P1, "GRAVEYARD");
        Put("ENEMY", "OGN·219/298", seed.P2, "BATTLEFIELD", "OTHER");
        Put("OTHER", "OGN·278/298", seed.P2, "BATTLEFIELD", "OTHER");
        cards["OTHER"] = cards["OTHER"] with { ControllerId = seed.P2 };
        cards["JINX"] = cards["JINX"] with { IsExhausted = true };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { seed.P1, seed.P2 })
        {
            var deck = Enumerable.Range(1, 4).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Hand = player == seed.P1 ? ["SPELL", "FODDER"] : [],
                Base = player == seed.P1 ? ["JINX"] : [], Graveyard = player == seed.P1 ? ["GRAVE"] : [],
                Battlefields = player == seed.P2 ? ["OTHER", "ENEMY"] : [], MainDeck = deck };
        }
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(20, 20), [seed.P2] = new(20, 20) }, zones, cards)
            with { ObjectLocations = locations, CardPlays = new(5, [], []) };
    }
}
