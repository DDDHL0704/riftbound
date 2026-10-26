namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    // Explicit QA state is a logged DEV command. All later assembly, movement,
    // skill order and responses use the ordinary production command path.
    private static MatchState BuildCopiedMovementScenario(MatchState current, DevScenarioSeed seed)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            if (!PrintedCardFactory.TryCreate(id, owner, no, out var card))
                throw new InvalidOperationException("Copied movement QA requires official printings.");
            cards[id] = card; locations[id] = new(owner, zone, field);
        }
        Put("HOST", "UNL-115/219", seed.P2, "BASE"); Put("GEAR", "SFD·059/221", seed.P2, "BASE");
        Put("FIELD", "OGN·278/298", seed.P2, "BATTLEFIELD", "FIELD");
        Put("OLD-FIELD", "OGN·278/298", seed.P2, "BATTLEFIELD", "OLD-FIELD");
        Put("KEEPER", "OGN·219/298", seed.P2, "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = seed.P2 };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { seed.P1, seed.P2 })
        {
            var deck = Enumerable.Range(1, 5).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Base = player == seed.P2 ? ["HOST", "GEAR"] : [],
                Battlefields = player == seed.P2 ? ["FIELD", "OLD-FIELD", "KEEPER"] : [], MainDeck = deck };
        }
        return BuildScenarioState(current, seed, 424242, 5, new Dictionary<string, RunePool> {
            [seed.P1] = RunePool.Empty, [seed.P2] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) }, zones, cards)
            with { ActivePlayerId = seed.P2, TurnPlayerId = seed.P2, ObjectLocations = locations, CardPlays = new(5, [], []) };
    }
}
