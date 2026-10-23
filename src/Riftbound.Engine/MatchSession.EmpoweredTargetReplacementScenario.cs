namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Two finite, recorded QA positions. Ordinary cases play and Empower both
    // exact printings through real commands; only the numeric QA starts at an
    // explicit representation boundary, never presented as a natural deck path.
    private static MatchState BuildEmpoweredTargetReplacementScenario(
        MatchState current, DevScenarioSeed seed, string scenario)
    {
        if (scenario is not ("empowered-target-action-replacement" or "empowered-target-action-overflow"))
            throw new InvalidOperationException("Unknown finite target-replacement QA position.");
        var boundary = scenario == "empowered-target-action-overflow";
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Target replacement QA requires the exact registered printing.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("PLANK-A", "VEN·086", seed.P1, boundary ? "BATTLEFIELD" : "HAND", boundary ? "HOME" : null);
        Put("PLANK-B", "VEN·181", seed.P1, boundary ? "BATTLEFIELD" : "HAND", boundary ? "HOME" : null);
        if (boundary)
            foreach (var id in new[] { "PLANK-A", "PLANK-B" })
                cards[id] = cards[id] with { Power = int.MaxValue - 1, IsEmpowered = true };
        Put("HOME", "UNL-214/219", seed.P1, "BATTLEFIELD", "HOME");
        Put("OTHER", "OGN·278/298", seed.P2, "BATTLEFIELD", "OTHER");
        cards["HOME"] = cards["HOME"] with { ControllerId = seed.P1 };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = seed.P2 };
        Put("ALLY", "OGN·219/298", seed.P1, "BASE");
        Put("ENEMY", "OGN·219/298", seed.P2, "BATTLEFIELD", "OTHER");
        Put("OBSERVER", "SFD·180/221", seed.P1, "BASE");
        Put("SMOKE", "OGN·093/298", seed.P1, "HAND");
        Put("STUN-P1", "OGN·050/298", seed.P1, "HAND");
        Put("RETURN", "OGN·104/298", seed.P1, "HAND");
        Put("PRIVATE-SOURCE", "OGN·051/298", seed.P1, "HAND");
        Put("GEAR", "SFD·059/221", seed.P1, "HAND");
        Put("READY", "OGN·146/298", seed.P1, "HAND");
        Put("STUN-P2", "OGN·050/298", seed.P2, "HAND");
        Put("SHAKEDOWN", "OGN·095/298", seed.P2, "HAND");
        Put("ECHO", "SFD·040/221", seed.P2, "HAND");
        Put("MEL", "VEN·069", seed.P2, "HAND");
        var manaRunes = Enumerable.Range(1, 8).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var id in manaRunes) Put(id, "UNL-R03", seed.P2, "BASE");
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal);
        foreach (var player in new[] { seed.P1, seed.P2 }) {
            var key = player == seed.P1 ? "P1" : "P2";
            var deck = Enumerable.Range(1, 6).Select(i => key + "-DRAW-" + i).Append("PRIVATE-" + key).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = new[] { key + "-RUNE-1", key + "-RUNE-2" };
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = Zones(mainDeck: deck, runeDeck: runes,
                hand: player == seed.P1
                    ? boundary ? ["SMOKE", "STUN-P1", "RETURN", "PRIVATE-SOURCE", "GEAR", "READY"]
                        : ["PLANK-A", "PLANK-B", "SMOKE", "STUN-P1", "RETURN", "PRIVATE-SOURCE", "GEAR", "READY"]
                    : ["STUN-P2", "SHAKEDOWN", "ECHO", "MEL"],
                baseZone: player == seed.P1 ? ["ALLY", "OBSERVER"] : manaRunes,
                battlefields: player == seed.P1 ? boundary ? ["HOME", "PLANK-A", "PLANK-B"] : ["HOME"] : ["OTHER", "ENEMY"]);
        }
        RunePool Pool() => new(70, 0, new Dictionary<string, int> {
            [RuneTrait.Red] = 10, [RuneTrait.Blue] = 10, [RuneTrait.Green] = 10,
            [RuneTrait.Yellow] = 10, [RuneTrait.Purple] = 10, [RuneTrait.Orange] = 10 });
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = Pool(), [seed.P2] = Pool() }, zones, cards) with {
                RuleInputs = current.RuleInputs, ObjectLocations = locations, CardPlays = new(5, [], []) };
    }
}
