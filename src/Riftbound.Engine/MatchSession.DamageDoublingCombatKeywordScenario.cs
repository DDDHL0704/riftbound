namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Finite, logged backend QA positions, never an arbitrary state import.
    // The extreme Might values exercise the server's representation boundary;
    // they are not evidence of reaching those values through an ordinary deck.
    private static MatchState BuildDamageDoublingCombatKeywordScenario(
        MatchState current, DevScenarioSeed seed, string scenario)
    {
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Damage/keyword QA requires an exact registered printing.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        void Token(string id)
        {
            if (!P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory))
                throw new InvalidOperationException("Damage/keyword QA requires the existing token factory.");
            cards[id] = factory.CreateObject(id, seed.P1, seed.P1);
            locations[id] = new(seed.P1, "BASE");
        }
        Put("LOTUS1", "UNL-013/219", seed.P1, "HAND");
        Put("LOTUS2", "UNL-013/219", seed.P1, "HAND");
        Put("PRIVATE-P1", "OGN·219/298", seed.P1, "MAIN_DECK");
        Put("PRIVATE-P2", "OGN·219/298", seed.P2, "MAIN_DECK");
        Put("FIELD", "OGN·278/298", seed.P2, "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = seed.P2 };
        IReadOnlyDictionary<string, PlayerZones> zones;
        if (scenario == "damage-doubling-combat-keywords")
        {
            Put("SPELLFIELD", "OGN·275/298", seed.P2, "BATTLEFIELD", "SPELLFIELD");
            cards["SPELLFIELD"] = cards["SPELLFIELD"] with { ControllerId = seed.P2 };
            Put("TARGET", "OGN·219/298", seed.P2, "BATTLEFIELD", "SPELLFIELD");
            cards["TARGET"] = cards["TARGET"] with { Power = 80 };
            Put("RAY", "OGN·009/298", seed.P1, "HAND");
            Put("ALLY", "OGN·210/298", seed.P1, "HAND");
            Put("READY", "UNL-009/219", seed.P1, "HAND");
            Put("RETURN", "OGN·104/298", seed.P1, "HAND");
            Put("KILL1", "OGN·014/298", seed.P1, "HAND");
            Put("KILL2", "OGN·014/298", seed.P1, "HAND");
            Put("FARRON", "OGN·015/298", seed.P1, "BASE");
            Put("ENEMY1", "OGN·219/298", seed.P2, "BATTLEFIELD", "FIELD");
            Put("ENEMY2", "OGN·219/298", seed.P2, "BATTLEFIELD", "FIELD");
            Token("TOKEN1"); Token("TOKEN2");
            zones = new Dictionary<string, PlayerZones>
            {
                [seed.P1] = Zones(mainDeck: ["PRIVATE-P1"], hand: ["LOTUS1", "LOTUS2", "RAY", "ALLY", "READY", "RETURN", "KILL1", "KILL2"],
                    baseZone: ["FARRON", "TOKEN1", "TOKEN2"]),
                [seed.P2] = Zones(mainDeck: ["PRIVATE-P2"], battlefields: ["FIELD", "SPELLFIELD", "TARGET", "ENEMY1", "ENEMY2"])
            };
        }
        else
        {
            Put("ATTACKER", "OGN·219/298", seed.P1, "BASE");
            cards["ATTACKER"] = cards["ATTACKER"] with { Power = scenario == "damage-doubling-overflow" ? 536870911 : 536870910 };
            Token("HELPER");
            Put("TARGET", "OGN·219/298", seed.P2, "BATTLEFIELD", "FIELD");
            cards["TARGET"] = cards["TARGET"] with { Power = int.MaxValue, Damage = scenario == "damage-marked-overflow" ? 4 : 0 };
            zones = new Dictionary<string, PlayerZones>
            {
                [seed.P1] = Zones(mainDeck: ["PRIVATE-P1"], hand: ["LOTUS1", "LOTUS2"], baseZone: ["ATTACKER", "HELPER"]),
                [seed.P2] = Zones(mainDeck: ["PRIVATE-P2"], battlefields: ["FIELD", "TARGET"])
            };
        }
        RunePool Pool() => new(60, 0, new Dictionary<string, int>
        {
            [RuneTrait.Red] = 4, [RuneTrait.Blue] = 4, [RuneTrait.Green] = 4,
            [RuneTrait.Yellow] = 4, [RuneTrait.Purple] = 4, [RuneTrait.Orange] = 4
        });
        return BuildScenarioState(current, seed, 424242, 5,
            new Dictionary<string, RunePool> { [seed.P1] = Pool(), [seed.P2] = Pool() }, zones, cards) with
        {
            RuleInputs = current.RuleInputs, ObjectLocations = locations, CardPlays = new(5, [], [])
        };
    }
}
