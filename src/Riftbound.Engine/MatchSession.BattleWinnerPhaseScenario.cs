namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Finite midgame QA only. Every combatant, Guardian and spell starts in
    // hand. The final-point case has one existing noncombat occupant so its
    // other battlefield is genuinely held by subsequent normal turn actions.
    private static MatchState BuildBattleWinnerPhaseScenario(MatchState current, DevScenarioSeed seed, string scenario)
    {
        if (scenario is not ("battle-winner-phase-plain" or "battle-winner-phase-guardians"
            or "battle-winner-phase-final-conquest" or "battle-winner-phase-result-burnout"
            or "battle-winner-phase-defender" or "battle-winner-phase-skipped-damage" or "battle-winner-phase-control-observer"))
            throw new InvalidOperationException("Unknown finite battle-winner-phase QA position.");
        var final = scenario == "battle-winner-phase-final-conquest";
        var burnout = scenario == "battle-winner-phase-result-burnout";
        var defender = scenario == "battle-winner-phase-defender";
        var skipped = scenario == "battle-winner-phase-skipped-damage";
        var control = scenario == "battle-winner-phase-control-observer";
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        void Put(string id, string no, string owner, string zone, string? field = null) {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Battle winner QA requires exact registered printings.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("HOME", control ? "OGN·298/298" : "OGN·278/298", seed.P1, "BATTLEFIELD", "HOME"); cards["HOME"] = cards["HOME"] with { ControllerId = seed.P1 };
        Put("OTHER", "OGN·295/298", final ? seed.P2 : seed.P1, "BATTLEFIELD", "OTHER");
        cards["OTHER"] = cards["OTHER"] with { ControllerId = final ? seed.P2 : null };
        if (final) {
            Put("OTHER-KEEPER", "OGN·219/298", seed.P2, "BATTLEFIELD", "OTHER");
            cards["OTHER-KEEPER"] = cards["OTHER-KEEPER"] with { ControllerId = seed.P2 };
        }
        foreach (var id in new[] { "ANGEL", "ANGEL-2" }) Put(id, "SFD·051/221", seed.P1, "HAND");
        Put("HOST", defender ? "OGN·142/298" : "OGN·210/298", seed.P1, "HAND");
        Put("ATTACKER", defender ? "OGN·210/298" : "OGN·142/298", seed.P2, "HAND"); Put("READY", "OGN·146/298", seed.P2, "HAND");
        if (skipped) { Put("RAY", "OGN·009/298", seed.P2, "HAND"); Put("P2-RED", "UNL-R01", seed.P2, "BASE"); }
        if (burnout || control) { Put("LEGEND", "SFD·185/221", seed.P2, "LEGEND"); cards["LEGEND"] = cards["LEGEND"] with { ControllerId = seed.P2 }; }
        var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var id in funding) Put(id, "UNL-R06", seed.P2, "BASE");
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal);
        foreach (var player in new[] { seed.P1, seed.P2 }) {
            var key = player == seed.P1 ? "P1" : "P2";
            var deck = player == seed.P2 && burnout ? new[] { "P2-DRAW-1" }
                : Enumerable.Range(1, 6).Select(i => key + "-DRAW-" + i).Append("PRIVATE-" + key).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = new[] { key + "-RUNE-1", "PRIVATE-" + key + "-RUNE-TAIL" };
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = Zones(mainDeck: deck, runeDeck: runes,
                hand: player == seed.P1 ? ["HOST", "ANGEL", "ANGEL-2"] : skipped ? ["ATTACKER", "READY", "RAY"] : ["ATTACKER", "READY"],
                baseZone: player == seed.P1 ? [] : skipped ? [..funding, "P2-RED"] : funding, legendZone: player == seed.P2 && (burnout || control) ? ["LEGEND"] : [],
                battlefields: player == seed.P1 ? final ? ["HOME"] : ["HOME", "OTHER"] : final ? ["OTHER", "OTHER-KEEPER"] : []);
        }
        var pool = new RunePool(40, 0, new Dictionary<string, int> { [RuneTrait.Green] = 10, [RuneTrait.Purple] = 10 });
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = pool, [seed.P2] = RunePool.Empty }, zones, cards) with {
                RuleInputs = current.RuleInputs, ObjectLocations = locations, CardPlays = new(5, [], []),
                PlayerScores = new Dictionary<string, int> { [seed.P1] = burnout ? 7 : 0, [seed.P2] = final ? 6 : 0 } };
    }
}
