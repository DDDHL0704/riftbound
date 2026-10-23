using Riftbound.CardCatalog;
namespace Riftbound.Engine;
public sealed partial class MatchSession
{
    // Explicit finite development QA only; birth log retains ROOM_SEAT and DEV_SEED_SCENARIO.
    private static MatchState BuildRestrictedManaScenario(MatchState current, DevScenarioSeed seed, bool alternate)
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo);
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Add(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialCardObject(id, owner, catalog[no]);
            locations[id] = new(owner, zone, field);
        }
        var lux = alternate ? "VEN·SP6" : "OGS·014/024";
        Add("QA-LUX", lux, seed.P1, "BASE"); Add("QA-SECOND-LUX", lux, seed.P1, "BASE");
        Add("QA-PEER-LUX", lux, seed.P2, "BASE");
        Add("QA-FIRST", "OGN·268/298", seed.P1, "HAND"); Add("QA-SECOND", "OGN·268/298", seed.P1, "HAND");
        Add("QA-UNIT", "OGN·044/298", seed.P1, "HAND");
        Add("QA-FIELD", "OGN·278/298", seed.P2, "BATTLEFIELD", "QA-FIELD");
        cards["QA-FIELD"] = cards["QA-FIELD"] with { ControllerId = seed.P2 };
        Add("QA-TARGET", "OGN·142/298", seed.P2, "BATTLEFIELD", "QA-FIELD");
        Add("QA-DRAW", "SFD·125/221", seed.P2, "MAIN_DECK");
        return BuildScenarioState(current, seed, 2026102406, 3,
            new Dictionary<string, RunePool> { [seed.P1] = new(1, 0), [seed.P2] = RunePool.Empty },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(hand: ["QA-FIRST", "QA-SECOND", "QA-UNIT"], baseZone: ["QA-LUX", "QA-SECOND-LUX"]),
                [seed.P2] = Zones(mainDeck: ["QA-DRAW"], baseZone: ["QA-PEER-LUX"], battlefields: ["QA-FIELD", "QA-TARGET"]) },
            cards) with { ObjectLocations = locations, RuleInputs = current.RuleInputs };
    }
}
