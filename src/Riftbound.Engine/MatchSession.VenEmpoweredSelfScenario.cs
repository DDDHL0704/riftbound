namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // A fresh logged QA position. Accepted history always starts at SEATING;
    // the live play, activation, return and reveal commands follow this setup.
    private static MatchState BuildVenEmpoweredSelfScenario(MatchState current, DevScenarioSeed seed, string suffix)
    {
        if (suffix is not ("043" or "050" or "070"))
            throw new ArgumentOutOfRangeException(nameof(suffix), "N9 QA supports only the three reviewed exact printings.");
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("N9 QA requires a registered current exact printing: " + no);
            cards[id] = card; locations[id] = new(owner, zone);
        }
        Put("N9-UNIT", "VEN·" + suffix, seed.P1, "HAND");
        Put("N9-RETURN", "OGN·104/298", seed.P1, "HAND");
        Put("N9-REMOVE", "SFD·163/221", seed.P2, "HAND");
        Put("N9-CRAB", "UNL-053/219", seed.P2, "BASE");
        Put("N9-ENEMY", "OGN·219/298", seed.P2, "BASE");
        var controlledRunes = suffix == "050" ? Enumerable.Range(0, 5).Select(i => "N9-RUNE-" + i).ToArray() : [];
        foreach (var id in controlledRunes) {
            Put(id, "UNL-R03", seed.P1, "BASE");
            cards[id] = cards[id] with { IsExhausted = id.EndsWith('0') || id.EndsWith('2') || id.EndsWith('4') };
        }
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { seed.P1, seed.P2 }) {
            var seat = player == seed.P1 ? "P1" : "P2";
            var hand = "PRIVATE-" + seat + "-HAND";
            Put(hand, "OGN·219/298", player, "HAND");
            var deck = Enumerable.Range(1, 8).Select(i => "PRIVATE-" + seat + "-DECK-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = Enumerable.Range(1, 2).Select(i => "PRIVATE-" + seat + "-RUNE-" + i).ToArray();
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = PlayerZones.Empty with {
                Hand = player == seed.P1 ? ["N9-UNIT", "N9-RETURN", hand] : ["N9-REMOVE", hand],
                MainDeck = deck, RuneDeck = runes,
                Base = player == seed.P1 ? controlledRunes : ["N9-CRAB", "N9-ENEMY"] };
        }
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> {
                [seed.P1] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 4, [RuneTrait.Red] = 4 }),
                [seed.P2] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Green] = 4 }) }, zones, cards) with {
            ObjectLocations = locations, CardPlays = new(5, [], []), RuleInputs = current.RuleInputs };
    }
}
