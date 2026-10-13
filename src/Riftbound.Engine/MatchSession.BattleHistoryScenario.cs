namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Logged QA setup only. All destruction, draw, burnout, role and battle
    // resolution after this position use the ordinary core command flow.
    private static MatchState BuildBattleHistoryBurnoutScenario(MatchState current, DevScenarioSeed seed, bool attackerEmpty, bool defenderEmpty)
    {
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        void Add(string id, string cardNo, string player, string zone, string? field = null)
        {
            if (!CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition))
                throw new InvalidOperationException("Battle-history QA requires an actual registered printing.");
            cards[id] = new(id, cardNo: cardNo, ownerId: player, controllerId: player, manaCost: definition.ManaCost,
                power: definition.PlaysSourceToBaseAsUnit ? definition.SourceUnitPower : 0,
                tags: definition.PlaysSourceToBaseAsUnit ? [CardObjectTags.UnitCard] : [CardObjectTags.SpellCard]);
            locations[id] = new(player, zone, field);
        }
        Add("HOST", "OGN·041/298", seed.P1, "BASE");
        Add("ENEMY1", "OGN·001/298", seed.P2, "BATTLEFIELD", "F");
        Add("P1-ALLY", "OGN·001/298", seed.P1, "BASE");
        Add("P2-ALLY", "OGN·001/298", seed.P2, "BASE");
        Add("P1-SOUL", "SFD·163/221", seed.P1, "HAND");
        Add("P2-SOUL", "SFD·163/221", seed.P2, "HAND");
        if (!attackerEmpty) Add("P1-DRAW", "OGN·001/298", seed.P1, "MAIN_DECK");
        if (!defenderEmpty) Add("P2-DRAW", "OGN·001/298", seed.P2, "MAIN_DECK");
        cards["F"] = new("F", cardNo: "OGN·278/298", ownerId: seed.P2, controllerId: seed.P2, tags: [P6TokenFactoryCatalog.BattlefieldCardTag]);
        locations["F"] = new(seed.P2, "BATTLEFIELD", "F");
        return BuildScenarioState(current, seed, 2026101301, 1,
            new Dictionary<string, RunePool> { [seed.P1] = new(20, 20), [seed.P2] = new(20, 20) },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(mainDeck: attackerEmpty ? [] : ["P1-DRAW"], hand: ["P1-SOUL"], baseZone: ["HOST", "P1-ALLY"]),
                [seed.P2] = Zones(mainDeck: defenderEmpty ? [] : ["P2-DRAW"], hand: ["P2-SOUL"], baseZone: ["P2-ALLY"], battlefields: ["F", "ENEMY1"]) }, cards)
            with { ObjectLocations = locations };
    }
}
