using Riftbound.CardCatalog;
namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Four exact finite QA labels only. There are no caller-supplied cards,
    // balances, generations or serialized states; replay retains real birth.
    private static MatchState BuildImposedMovementScenario(MatchState current, DevScenarioSeed seed, bool effect)
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo);
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Add(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialCardObject(id, player, catalog[no]); locations[id] = new(player, zone, field);
        }
        foreach (var id in new[] { "QA-A", "QA-B" }) Add(id, "OGN·044/298", seed.P1, "BASE");
        foreach (var id in new[] { "QA-D", "QA-D2" }) Add(id, "OGN·044/298", seed.P2, "BATTLEFIELD", "QA-FIELD1");
        Add("QA-FIELD1", "OGN·278/298", seed.P2, "BATTLEFIELD", "QA-FIELD1");
        Add("QA-FIELD2", "OGN·287/298", seed.P2, "BATTLEFIELD", "QA-FIELD2");
        foreach (var id in new[] { "QA-FIELD1", "QA-FIELD2" }) cards[id] = cards[id] with { ControllerId = seed.P2 };
        Add("QA-PATROL", "UNL-163/219", seed.P2, "BATTLEFIELD", "QA-FIELD2");
        Add("QA-LUX", "OGS·014/024", seed.P1, "BASE"); Add("QA-RUNE", "OGN·007/298", seed.P1, "BASE");
        Add("QA-SPELL", "UNL-054/219", seed.P1, "HAND");
        Add("QA-BULLET", "OGN·268/298", seed.P1, "HAND");
        Add("QA-STELE", "SFD·117/221", seed.P1, "BASE");
        Add("QA-PRIVATE", "OGN·268/298", seed.P2, "HAND");
        Add("QA-DRAW", "OGN·044/298", seed.P2, "MAIN_DECK");
        return BuildScenarioState(current, seed, 2026102407, 3,
            new Dictionary<string, RunePool> { [seed.P1] = effect ? new(70, 1) : new(66, 0), [seed.P2] = RunePool.Empty },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(hand: ["QA-SPELL", "QA-BULLET"], baseZone: ["QA-A", "QA-B", "QA-LUX", "QA-RUNE", "QA-STELE"]),
                [seed.P2] = Zones(hand: ["QA-PRIVATE"], mainDeck: ["QA-DRAW"],
                    battlefields: ["QA-FIELD1", "QA-D", "QA-D2", "QA-FIELD2", "QA-PATROL"]) }, cards)
            with { ObjectLocations = locations, RuleInputs = current.RuleInputs };
    }
}
