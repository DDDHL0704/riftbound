namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Controlled QA entry is itself logged. Assembly, death and the subsequent
    // copied skills are ordinary commands, never an imported accepted history.
    private static MatchState BuildCopiedDeathScenario(MatchState current, DevScenarioSeed seed)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Copied death QA requires official printings.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("HOST", "UNL-129/219", seed.P2, "BASE"); Put("GEAR", "SFD·059/221", seed.P2, "BASE");
        Put("AOE", "OGN·133/298", seed.P1, "HAND"); Put("PAY", "OGN·007/298", seed.P1, "BASE");
        Put("D", "OGN·219/298", seed.P2, "BATTLEFIELD", "FIELD");
        Put("GRAVE", "OGN·219/298", seed.P2, "GRAVEYARD");
        Put("FIELD", "OGN·278/298", seed.P2, "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = seed.P2 };
        // A low-Might QA target survives the intervening turn cleanup. Damage
        // preloaded here would be removed before the ordinary lethal spell.
        cards["D"] = cards["D"] with { Power = 1, Damage = 0 };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { seed.P1, seed.P2 })
        {
            var deck = Enumerable.Range(1, 4).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Base = player == seed.P1 ? ["PAY"] : ["HOST", "GEAR"],
                Hand = player == seed.P1 ? ["AOE"] : [], Battlefields = player == seed.P2 ? ["FIELD", "D"] : [],
                Graveyard = player == seed.P2 ? ["GRAVE"] : [], MainDeck = deck };
        }
        return BuildScenarioState(current, seed, 424242, 5, new Dictionary<string, RunePool> {
            [seed.P1] = RunePool.Empty, [seed.P2] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) }, zones, cards)
            with { ActivePlayerId = seed.P2, TurnPlayerId = seed.P2, ObjectLocations = locations, CardPlays = new(5, [], []) };
    }
}
