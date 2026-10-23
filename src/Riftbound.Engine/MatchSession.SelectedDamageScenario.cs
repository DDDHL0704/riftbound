namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Six fixed backend QA positions; choices, responses and damage remain real commands.
    private static MatchState BuildSelectedDamageScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var gate = branch.EndsWith("-gate", StringComparison.Ordinal);
        var card = branch.StartsWith("bullet", StringComparison.Ordinal) ? "OGN·268/298"
            : branch.StartsWith("drop", StringComparison.Ordinal) ? "OGN·250/298" : "UNL-072/219";
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var printed))
                throw new InvalidOperationException("Selected-field QA requires actual catalog printings.");
            cards[id] = printed;
            locations[id] = new(owner, zone, field);
        }
        Put("A", gate ? "OGN·296/298" : "OGN·278/298", seed.P2, "BATTLEFIELD", "A");
        Put("B", "OGN·278/298", seed.P2, "BATTLEFIELD", "B");
        cards["A"] = cards["A"] with { ControllerId = seed.P2 };
        cards["B"] = cards["B"] with { ControllerId = seed.P2 };
        Put("P2-A1", "OGN·142/298", seed.P2, "BATTLEFIELD", "A");
        Put("P2-A2", "OGN·142/298", seed.P2, "BATTLEFIELD", "A");
        Put("P2-B", "OGN·142/298", seed.P2, "BATTLEFIELD", "B");
        Put("P1-BASE", "OGN·219/298", seed.P1, "BASE");
        Put("P2-BASE", "OGN·219/298", seed.P2, "BASE");
        Put("ANNIE", "OGS·001/024", seed.P1, "BASE");
        Put("SPELL", card, seed.P1, "HAND");
        Put("P1-RETURN", "OGN·104/298", seed.P1, "HAND");
        Put("P2-RETURN", "OGN·104/298", seed.P2, "HAND");
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(20, 20), [seed.P2] = new(20, 20) },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(baseZone: ["ANNIE", "P1-BASE"], hand: ["SPELL", "P1-RETURN"]),
                [seed.P2] = Zones(baseZone: ["P2-BASE"], hand: ["P2-RETURN"], battlefields: ["A", "B", "P2-A1", "P2-A2", "P2-B"]) }, cards)
            with { ObjectLocations = locations, CardPlays = new(5, [], []) };
    }
}
