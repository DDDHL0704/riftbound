using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Finite backend positions. Deaths, ordering, response and terminal outcomes
    // must be produced by subsequent ordinary commands and preserved journals.
    private static MatchState BuildDravenBattleDeathScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var alternate = branch.Split('-').Contains("alt", StringComparer.Ordinal);
        var mutual = branch.Contains("mutual", StringComparison.Ordinal);
        var score = branch.Contains("winning", StringComparison.Ordinal) ? 7 : 0;
        var mixed = branch.StartsWith("mixed", StringComparison.Ordinal);
        var attackingDraven = branch != "mixed-defender";
        static CardObjectState Printed(string id, string no, string owner)
            => PrintedCardFactory.TryCreate(id, owner, no, out var card) ? card
                : throw new InvalidOperationException("Death QA requires official catalog printings.");
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        var print = alternate ? "SFD·148a/221" : "SFD·148/221";
        Put("P1-UNIT", attackingDraven ? print : "OGN·142/298", seed.P1, "BASE");
        Put("P2-UNIT", mutual || !attackingDraven ? print : "OGN·142/298", seed.P2, "BATTLEFIELD", "FIELD");
        Put("FIELD", branch.StartsWith("altar", StringComparison.Ordinal) ? "UNL-206/219" : "OGN·278/298", seed.P2, "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = seed.P2 };
        Put("DISPOSAL", "UNL-103/219", seed.P2, "HAND");
        var winner = attackingDraven ? seed.P2 : seed.P1;
        if (mixed) Put("LEGEND", "SFD·185/221", winner, "LEGEND");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { seed.P1, seed.P2 })
        {
            var deck = Enumerable.Range(1, 4).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Base = player == seed.P1 ? ["P1-UNIT"] : [],
                Battlefields = player == seed.P2 ? ["FIELD", "P2-UNIT"] : [], MainDeck = deck,
                LegendZone = mixed && player == winner ? ["LEGEND"] : [], Hand = player == seed.P2 ? ["DISPOSAL"] : [] };
        }
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(20, 20), [seed.P2] = new(20, 20) }, zones, cards) with
        {
            ObjectLocations = locations, CardPlays = new(5, [], []),
            PlayerScores = new Dictionary<string, int> { [seed.P1] = score, [seed.P2] = score }
        };
    }
}
