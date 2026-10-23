namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // A newly logged current-rule QA position, not an ordinary deck opening or
    // a replacement origin for any previously accepted action history.
    private static MatchState BuildVenFlowCombatKeywordScenario(MatchState current, DevScenarioSeed seed, bool flow)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("N3 QA requires a registered current exact printing: " + no);
            cards[id] = card; locations[id] = new(owner, zone);
        }
        Put("N3-ALLY", "OGN·219/298", seed.P1, "BASE");
        cards["N3-ALLY"] = cards["N3-ALLY"] with { IsExhausted = true };
        Put("N3-SPELL", "VEN·012", seed.P1, flow ? "GRAVEYARD" : "HAND");
        Put("N3-RETURN", "OGN·104/298", seed.P1, "HAND");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { seed.P1, seed.P2 }) {
            var seat = player == seed.P1 ? "P1" : "P2";
            var hand = "PRIVATE-" + seat + "-HAND";
            Put(hand, "OGN·219/298", player, "HAND");
            var deck = Enumerable.Range(1, 4).Select(i => "PRIVATE-" + seat + "-DECK-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with {
                Hand = player == seed.P1 ? (flow ? ["N3-RETURN", hand] : ["N3-SPELL", "N3-RETURN", hand]) : [hand],
                Base = player == seed.P1 ? ["N3-ALLY"] : [], MainDeck = deck,
                Graveyard = player == seed.P1 && flow ? ["N3-SPELL"] : [] };
        }
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> {
                [seed.P1] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 10 }),
                [seed.P2] = new(20, 20) }, zones, cards) with {
            ObjectLocations = locations, CardPlays = new(5, [], []), RuleInputs = current.RuleInputs };
    }
}
