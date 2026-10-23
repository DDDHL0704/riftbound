namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Finite, explicitly journaled backend QA positions. Damage is always
    // applied by subsequent real commands, never seeded into a dead unit.
    private static MatchState BuildVictoryCleanupScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Victory-cleanup QA requires actual catalog printings.");
            cards[id] = card;
            locations[id] = new(owner, zone, field);
        }
        if (branch is "garen-one" or "garen-four")
        {
            var movingCount = branch == "garen-four" ? 4 : 1;
            var units = new[] { "U1", "U2", "U3", "U4" };
            Put("GAREN", "OGS·023/024", seed.P1, "LEGEND");
            Put("TARGET", "OGN·296/298", seed.P2, "BATTLEFIELD", "TARGET");
            Put("OTHER", "OGN·280/298", seed.P1, "BATTLEFIELD", "OTHER");
            cards["OTHER"] = cards["OTHER"] with { ControllerId = seed.P1 };
            for (var i = 0; i < units.Length; i++)
                Put(units[i], i == 0 ? "OGN·142/298" : "OGN·219/298", seed.P1,
                    i < movingCount ? "BASE" : "BATTLEFIELD", i < movingCount ? null : "OTHER");
            Put("DRAW1", "OGN·142/298", seed.P1, "MAIN_DECK");
            Put("DRAW2", "OGN·142/298", seed.P1, "MAIN_DECK");
            return BuildScenarioState(current, seed, 2026102304, 5,
                new Dictionary<string, RunePool> { [seed.P1] = RunePool.Empty, [seed.P2] = RunePool.Empty },
                new Dictionary<string, PlayerZones>
                {
                    [seed.P1] = Zones(mainDeck: ["DRAW1", "DRAW2"], baseZone: units.Take(movingCount).ToArray(),
                        battlefields: new[] { "OTHER" }.Concat(units.Skip(movingCount)).ToArray(), legendZone: ["GAREN"]),
                    [seed.P2] = Zones(battlefields: ["TARGET"])
                }, cards) with { ObjectLocations = locations, CardPlays = new(5, [], []) };
        }

        Put("MUNDO", "OGN·109/298", seed.P1, "BASE");
        Put("DAMAGE", "OGS·022/024", seed.P1, "HAND");
        Put("DRAW", "OGN·047/298", seed.P1, "HAND");
        Put("RUNE", "SFD·R03", seed.P1, "RUNE_DECK");
        Put("F", "OGN·278/298", seed.P1, "BATTLEFIELD", "F");
        Put("OTHER", "OGN·294/298", seed.P2, "BATTLEFIELD", "OTHER");
        foreach (var id in new[] { "G0", "G1", "G2" }) Put(id, "OGN·083/298", seed.P1, "GRAVEYARD");
        return BuildScenarioState(current, seed, 2026102305, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(30, 30), [seed.P2] = new(30, 30) },
            new Dictionary<string, PlayerZones>
            {
                [seed.P1] = Zones(baseZone: ["MUNDO"], hand: ["DAMAGE", "DRAW"], graveyard: ["G0", "G1", "G2"], runeDeck: ["RUNE"], battlefields: ["F"]),
                [seed.P2] = Zones(battlefields: ["OTHER"])
            }, cards) with
        {
            ObjectLocations = locations, CardPlays = new(5, [], []),
            PlayerScores = new Dictionary<string, int> { [seed.P1] = 0, [seed.P2] = branch.EndsWith("-low", StringComparison.Ordinal) ? 6 : 7 }
        };
    }
}
