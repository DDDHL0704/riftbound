namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Finite, explicitly logged development positions for the adopted CN156
    // consumer. These do not replace ordinary openings or regional deck checks.
    private static MatchState BuildLightningRushScenario(MatchState current, DevScenarioSeed seed, string branch)
    {
        var state = BuildDeckPendingScenario(current, seed, "VEN·156");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        var deck = new[] { "P1_FIRST", "P1_FIRST_FILLER_1", "P1_FIRST_FILLER_2",
            "P1_SECOND", "P1_SECOND_FILLER_1", "P1_SECOND_FILLER_2", "P1_TAIL" };
        foreach (var id in zones[seed.P1].MainDeck.Except(deck)) { cards.Remove(id); locations.Remove(id); }
        zones[seed.P1] = zones[seed.P1] with { MainDeck = deck };
        void Put(string id, string cardNo, string owner, string zone)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, cardNo, out var card))
                throw new InvalidOperationException("Lightning Rush QA requires an actual catalog printing.");
            cards[id] = card; locations[id] = new(owner, zone);
        }
        var flow = branch.StartsWith("flow", StringComparison.Ordinal);
        Put("PARENT", "VEN·156", seed.P1, flow ? "GRAVEYARD" : "HAND");
        Put("P1_FIRST_FILLER_1", "OGN·083/298", seed.P1, "MAIN_DECK");
        zones[seed.P1] = zones[seed.P1] with { Hand = flow ? [] : ["PARENT"], Graveyard = flow ? ["PARENT"] : [] };
        if (branch == "observed") Put("P1_FIRST", "OGN·194/298", seed.P1, "MAIN_DECK");
        if (branch == "flow-empty") {
            foreach (var id in deck) { cards.Remove(id); locations.Remove(id); }
            zones[seed.P1] = zones[seed.P1] with { MainDeck = [] };
        }
        if (branch is "flow-counter" or "flow-return-counter" or "flow-control") {
            Put("COUNTER", branch == "flow-control" ? "OGN·080/298" : branch == "flow-counter" ? "OGN·064/298" : "UNL-131/219", seed.P2, "HAND");
            zones[seed.P2] = zones[seed.P2] with { Hand = ["COUNTER"] };
        }
        return state with { Seed = 2026102301, CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            UntilEndOfTurnEffects = branch == "flow-echo" ? [EchoCostRules.GrantPrefix + seed.P1] : [] };
    }
}
