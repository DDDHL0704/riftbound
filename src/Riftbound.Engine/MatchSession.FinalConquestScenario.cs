namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Finite logged QA positions. Subsequent scoring, draws, triggers and
    // recovery use ordinary commands; these are not ordinary match openings.
    private static MatchState BuildFinalConquestScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, cardNo, out var card))
                throw new InvalidOperationException("Final conquest QA requires an actual catalog printing.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("UNIT", "SFD·125/221", seed.P1, "BASE");
        Put("BF", "OGN·296/298", seed.P2, "BATTLEFIELD", "BF");
        Put("OTHER", "OGN·280/298", seed.P1, "BATTLEFIELD", "OTHER");
        var deck = new[] { "DRAW1", "DRAW2", "DRAW3" };
        foreach (var (id, no) in new[] { ("DRAW1", "OGN·004/298"), ("DRAW2", "OGN·083/298"), ("DRAW3", "OGN·096/298") })
            Put(id, no, seed.P1, branch == "burnout" ? "GRAVEYARD" : "MAIN_DECK");
        Put("P2-DRAW", "OGN·004/298", seed.P2, "MAIN_DECK");
        if (branch == "combat")
        {
            Put("DEFENDER", "OGN·052/298", seed.P2, "BATTLEFIELD", "BF");
            cards["BF"] = cards["BF"] with { ControllerId = seed.P2 };
        }
        if (branch == "brush")
        {
            if (!P6TokenFactoryCatalog.TryGetByCardNo("UNL·T03", out var factory))
                throw new InvalidOperationException("The actual Brush token is required.");
            Put("BF-REPLACED", "OGN·282/298", seed.P2, "BANISHED");
            cards["BF-REPLACED"] = cards["BF-REPLACED"] with { ReplacedAtBattlefieldId = "BF" };
            cards["BF"] = factory.CreateObject("BF", seed.P2, seed.P2) with {
                ControllerId = null, ReplacedBattlefieldCard = new("BF-REPLACED", cards["BF-REPLACED"].ObjectGeneration) };
        }
        var zones = new Dictionary<string, PlayerZones> {
            [seed.P1] = Zones(mainDeck: branch == "burnout" ? [] : deck,
                graveyard: branch == "burnout" ? deck : [], baseZone: ["UNIT"], battlefields: ["OTHER"]),
            [seed.P2] = Zones(mainDeck: ["P2-DRAW"], battlefields: branch == "combat" ? ["BF", "DEFENDER"] : ["BF"],
                banished: branch == "brush" ? ["BF-REPLACED"] : []) };
        return BuildScenarioState(current, seed, 2026102302, 5,
            new Dictionary<string, RunePool> { [seed.P1] = RunePool.Empty, [seed.P2] = RunePool.Empty }, zones, cards) with {
            ObjectLocations = locations,
            PlayerScores = new Dictionary<string, int> { [seed.P1] = 7, [seed.P2] = 0 },
            UntilEndOfTurnEffects = branch == "win" ? [BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", seed.P1)] : [] };
    }
}
