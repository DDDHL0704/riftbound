namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // These finite positions are explicitly recorded as DEV_SEED_SCENARIO.
    // They do not replace an ordinary opening or regional deck validation.
    private static MatchState BuildWildClawScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var state = BuildDeckPendingScenario(current, seed, "VEN·089", branch == "observed-second");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        void Put(string id, string no, string zone)
        {
            if (!CardBehaviorRegistry.TryGetByCardNo(no, out var definition)) throw new InvalidOperationException("A current registered QA card is required.");
            cards[id] = new(id, cardNo: no, ownerId: seed.P1, controllerId: seed.P1,
                manaCost: definition.ManaCost, power: definition.SourceUnitPower,
                tags: definition.PlaysSourceToBaseAsUnit ? [CardObjectTags.UnitCard]
                    : definition.PlaysSourceToBaseAsEquipment ? [CardObjectTags.EquipmentCard] : [CardObjectTags.SpellCard]);
            locations[id] = new(seed.P1, zone);
        }
        if (branch is not ("echo" or "observed-second")) state = state with { UntilEndOfTurnEffects = [] };
        if (branch == "gear") Put("P1_FIRST", "SFD·042/221", "MAIN_DECK");
        if (branch == "unpayable") {
            Put("P1_FIRST", "UNL-137/219", "MAIN_DECK");
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                [seed.P1] = new(7, 0, new Dictionary<string, int> { ["orange"] = 1 }) } };
        }
        if (branch == "observed-first") Put("P1_FIRST", "OGN·194/298", "MAIN_DECK");
        if (branch == "insight") Put("P1_FIRST", "VEN·047", "MAIN_DECK");
        if (branch == "mandatory") {
            Put("P1_FIRST", "SFD·044/221", "MAIN_DECK"); Put("COST_GEAR", "SFD·042/221", "BASE");
            zones[seed.P1] = zones[seed.P1] with { Base = zones[seed.P1].Base.Append("COST_GEAR").ToArray() };
        }
        if (branch is "recall" or "reentry") {
            Put("RESPONSE", branch == "recall" ? "OGN·104/298" : "UNL-184/219", "HAND");
            zones[seed.P1] = zones[seed.P1] with { Hand = zones[seed.P1].Hand.Append("RESPONSE").ToArray() };
        }
        if (branch == "reentry") {
            cards["FIELD"] = new("FIELD", cardNo: "OGN·277/298", ownerId: seed.P1, controllerId: seed.P1,
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag]);
            locations["FIELD"] = new(seed.P1, "BATTLEFIELD", "FIELD");
            zones[seed.P1] = zones[seed.P1] with { Battlefields = zones[seed.P1].Battlefields.Append("FIELD").ToArray() };
        }
        if (branch == "empty") {
            foreach (var id in zones[seed.P1].MainDeck) { cards.Remove(id); locations.Remove(id); }
            zones[seed.P1] = zones[seed.P1] with { MainDeck = [] };
        }
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
