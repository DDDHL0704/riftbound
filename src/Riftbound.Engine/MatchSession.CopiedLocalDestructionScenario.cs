namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Three finite recorded QA positions. Every unit and equipment starts in hand;
    // the leaf must actually play, assemble and choose replacements. Preset pools
    // and runes are QA resources, never a natural-opening or whole-card proof.
    private static MatchState BuildCopiedLocalDestructionScenario(
        MatchState current, DevScenarioSeed seed, string scenario)
    {
        var no = scenario switch {
            "copied-local-destruction-soraka-173" => "SFD·173/221",
            "copied-local-destruction-soraka-239" => "SFD·239/221",
            "copied-local-destruction-soraka-239-star" => "SFD·239*/221",
            _ => throw new InvalidOperationException("Unknown finite copied-local-destruction QA position.")
        };
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        void Put(string id, string number, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, number, out var card))
                throw new InvalidOperationException("Copied destruction QA requires the exact registered printing.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("HOME", "OGN·278/298", seed.P1, "BATTLEFIELD", "HOME");
        cards["HOME"] = cards["HOME"] with { ControllerId = seed.P1 };
        var hand1 = new List<string>(); var hand2 = new List<string>();
        void Hand(string id, string number, string owner) {
            Put(id, number, owner, "HAND"); (owner == seed.P1 ? hand1 : hand2).Add(id);
        }
        Hand("S", no, seed.P1); Hand("G1", "OGN·077/298", seed.P1);
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }) Hand(id, "OGN·210/298", seed.P1);
        foreach (var id in new[] { "SONG", "SONG-2" }) Hand(id, "SFD·059/221", seed.P1);
        Hand("DETACH", "SFD·011/221", seed.P1); Hand("RETURN-GEAR", "SFD·135/221", seed.P1);
        Hand("RETURN-SOURCE", "OGN·104/298", seed.P1); Hand("GLASSES", "VEN·137", seed.P1);
        Hand("AOE", "UNL-180/219", seed.P2); Hand("TAKE", "OGN·203/298", seed.P2); Hand("KILL", "OGN·229/298", seed.P2);
        foreach (var id in new[] { "P2-BASE-1", "P2-BASE-2" }) Hand(id, "OGN·210/298", seed.P2);
        var funding = Enumerable.Range(1, 18).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var id in funding) Put(id, "UNL-R06", seed.P2, "BASE");
        var purple = Enumerable.Range(1, 3).Select(i => "P2-PURPLE-" + i).ToArray();
        foreach (var id in purple) Put(id, "UNL-R05", seed.P2, "BASE");
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal);
        foreach (var player in new[] { seed.P1, seed.P2 }) {
            var key = player == seed.P1 ? "P1" : "P2";
            var deck = Enumerable.Range(1, 6).Select(i => key + "-DRAW-" + i).Append("PRIVATE-" + key).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = new[] { key + "-RUNE-1", key + "-RUNE-2" };
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = Zones(mainDeck: deck, runeDeck: runes, hand: player == seed.P1 ? hand1 : hand2,
                baseZone: player == seed.P1 ? [] : [..funding, ..purple], battlefields: player == seed.P1 ? ["HOME"] : []);
        }
        RunePool Pool() => new(70, 0, new Dictionary<string, int> {
            [RuneTrait.Red] = 10, [RuneTrait.Blue] = 10, [RuneTrait.Green] = 10,
            [RuneTrait.Yellow] = 10, [RuneTrait.Purple] = 10, [RuneTrait.Orange] = 10 });
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = Pool(), [seed.P2] = Pool() }, zones, cards) with {
                RuleInputs = current.RuleInputs, ObjectLocations = locations, CardPlays = new(5, [], []) };
    }
}
