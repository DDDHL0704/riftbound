namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Explicit, logged QA positions, followed by ordinary authoritative commands.
    // They exercise CN 432.2.d/432.3.c.1 and are not ordinary match openings.
    private static MatchState BuildBurnoutActionScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        if (branch == "conquest")
            return BuildFinalConquestScenario(current, seed, "burnout") with {
                PlayerScores = new Dictionary<string, int> { [seed.P1] = 7, [seed.P2] = 7 } };

        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, cardNo, out var card))
                throw new InvalidOperationException("Burnout QA requires an actual catalog printing.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("HOST", "UNL-080/219", seed.P1, "BASE");
        Put("HAND", branch == "portal-tie" ? "OGN·071/298" : "OGN·083/298", seed.P1, "HAND");
        Put("F", "OGN·278/298", seed.P1, "BATTLEFIELD", "F");
        Put("OTHER", "OGN·294/298", seed.P2, "BATTLEFIELD", "OTHER");
        var graveyard = Enumerable.Range(0, branch is "second-burnout" or "portal-tie" ? 1 : 3)
            .Select(i => "REFILL" + i).ToArray();
        foreach (var id in graveyard) Put(id, "OGN·083/298", seed.P1, "GRAVEYARD");
        if (branch == "portal-tie") Put("P2-REFILL", "OGN·083/298", seed.P2, "GRAVEYARD");
        return BuildScenarioState(current, seed, 2026102303, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(20, 20), [seed.P2] = new(20, 20) },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(hand: ["HAND"], graveyard: graveyard, baseZone: ["HOST"], battlefields: ["F"]),
                [seed.P2] = Zones(graveyard: branch == "portal-tie" ? ["P2-REFILL"] : [], battlefields: ["OTHER"]) }, cards) with {
            ObjectLocations = locations,
            PlayerScores = new Dictionary<string, int> { [seed.P1] = branch == "portal-tie" ? 7 : 0, [seed.P2] = 7 } };
    }
}
