namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Finite backend QA positions. The actual move, combat, response, draw,
    // control and terminal result must all arise from subsequent commands.
    private static MatchState BuildBattleResultScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var burnout = branch.EndsWith("-burnout", StringComparison.Ordinal);
        var lateDefender = branch.EndsWith("-late-defender", StringComparison.Ordinal);
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Battle-result QA requires actual catalog printings.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("SOURCE", branch.StartsWith("nidalee", StringComparison.Ordinal) ? "UNL-114/219" : "SFD·123/221", seed.P1, "BASE");
        Put("DEFENDER", "OGN·175/298", seed.P2, "BATTLEFIELD", "FIELD");
        Put("FIELD", "OGN·296/298", seed.P2, "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = seed.P2 };
        Put("DRAW", "OGN·219/298", seed.P1, burnout ? "GRAVEYARD" : "MAIN_DECK");
        if (lateDefender)
        {
            Put("DRAW2", "OGN·219/298", seed.P1, "MAIN_DECK");
            Put("LATE", "SFD·025/221", seed.P2, "HAND");
        }
        Put("RETURN", "OGN·104/298", seed.P1, "HAND");
        Put("BARRAGE1", "OGN·127/298", seed.P2, "HAND");
        Put("BARRAGE2", "OGN·127/298", seed.P2, "HAND");
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(10, 10), [seed.P2] = new(10, 10) },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(baseZone: ["SOURCE"], hand: ["RETURN"], mainDeck: burnout ? [] : lateDefender ? ["DRAW", "DRAW2"] : ["DRAW"], graveyard: burnout ? ["DRAW"] : []),
                [seed.P2] = Zones(battlefields: ["FIELD", "DEFENDER"], hand: lateDefender ? ["BARRAGE1", "BARRAGE2", "LATE"] : ["BARRAGE1", "BARRAGE2"]) }, cards) with
        {
            ObjectLocations = locations, CardPlays = new(5, [], []),
            PlayerScores = new Dictionary<string, int> { [seed.P1] = 0, [seed.P2] = burnout ? 7 : 0 }
        };
    }
}
