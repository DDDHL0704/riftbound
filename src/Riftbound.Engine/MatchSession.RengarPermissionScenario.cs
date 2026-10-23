using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // The scenario starts before the battle. Ordinary commands must establish
    // the attacker, resolve combat, play the reaction, and close the battle.
    private static MatchState BuildRengarPermissionScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Rengar QA requires official printings.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("SOURCE", "UNL-114/219", seed.P1, "BASE");
        Put("DEFENDER", "OGN·175/298", seed.P2, "BATTLEFIELD", "FIELD");
        Put("FIELD", branch.Contains("blocked", StringComparison.Ordinal) ? "SFD·216/221" : "OGN·278/298", seed.P2, "BATTLEFIELD", "FIELD");
        Put("OTHER", "OGN·278/298", seed.P2, "BATTLEFIELD", "OTHER");
        Put("GUARD", "OGN·219/298", seed.P2, "BATTLEFIELD", "OTHER");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = seed.P2 };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = seed.P2 };
        Put("RENGAR", branch.Contains("alt", StringComparison.Ordinal) ? "SFD·025a/221·P" : "SFD·025/221", seed.P1, "HAND");
        Put("RETURN", "OGN·104/298", seed.P1, "HAND");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { seed.P1, seed.P2 })
        {
            var deck = Enumerable.Range(1, 4).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Base = player == seed.P1 ? ["SOURCE"] : [],
                Battlefields = player == seed.P2 ? ["FIELD", "DEFENDER", "OTHER", "GUARD"] : [],
                Hand = player == seed.P1 ? ["RENGAR", "RETURN"] : [], MainDeck = deck };
        }
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(branch.Contains("poor", StringComparison.Ordinal) ? 2 : 20, 20), [seed.P2] = new(20, 20) }, zones, cards)
            with { ObjectLocations = locations, CardPlays = new(5, [], []) };
    }
}
