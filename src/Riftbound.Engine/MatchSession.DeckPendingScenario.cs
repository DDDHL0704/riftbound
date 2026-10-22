namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Explicitly logged controlled QA positions. Subsequent choices, pending
    // cards, payments, observation permissions and replay use the ordinary engine.
    private static MatchState BuildDeckPendingScenario(MatchState current, DevScenarioSeed seed, string spell, bool observed = false)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Add(string id, string cardNo, string owner, string zone)
        {
            if (!CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition))
                throw new InvalidOperationException("Deck-pending QA requires an actual registered printing.");
            cards[id] = new(id, cardNo: cardNo, ownerId: owner, controllerId: owner, manaCost: definition.ManaCost,
                power: definition.SourceUnitPower, tags: definition.PlaysSourceToBaseAsUnit ? [CardObjectTags.UnitCard]
                    : definition.PlaysSourceToBaseAsEquipment ? [CardObjectTags.EquipmentCard] : [CardObjectTags.SpellCard]);
            locations[id] = new(owner, zone);
        }
        Add("PARENT", spell, seed.P1, "HAND");
        var count = spell == "OGN·025/298" ? 1 : spell == "SFD·188/221" ? 2 : 5;
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var (seat, owner) in new[] { ("P1", seed.P1), ("P2", seed.P2) })
        {
            var deck = new List<string>();
            foreach (var execution in new[] { "FIRST", "SECOND" })
            {
                var id = seat + "_" + execution;
                Add(id, observed && seat == "P1" && execution == "SECOND" ? "OGN·194/298" : "OGN·096/298", owner, "MAIN_DECK");
                deck.Add(id);
                for (var i = 1; i < count; i++)
                {
                    var filler = id + "_FILLER_" + i;
                    Add(filler, "SFD·042/221", owner, "MAIN_DECK"); deck.Add(filler);
                }
            }
            Add(seat + "_TAIL", "OGN·096/298", owner, "MAIN_DECK"); deck.Add(seat + "_TAIL");
            zones[owner] = Zones(mainDeck: deck, hand: owner == seed.P1 ? ["PARENT"] : []);
        }
        return BuildScenarioState(current, seed, 2026102201, 1,
            new Dictionary<string, RunePool> { [seed.P1] = new(30, 30), [seed.P2] = new(30, 30) }, zones, cards)
            with { ObjectLocations = locations, UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + seed.P1] };
    }
}
