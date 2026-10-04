using Riftbound.CardCatalog;
using Riftbound.Engine;

namespace Riftbound.ConformanceTests;

// Effect/trigger fixtures historically supplied only mana. Explicitly add the
// printed cost of the listed planned plays; never infer actions or bypass costs.
internal static class PrintedCostFixture
{
    private static readonly Lazy<IReadOnlyDictionary<string, int>> Printed = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .ToDictionary(card => card.CardNo, card => card.ReturnEnergy ?? 0));

    public static int Power(string cardNo) => Printed.Value[cardNo];

    public static MatchState FundHand(MatchState state)
    {
        foreach (var (playerId, zones) in state.PlayerZones)
            state = Add(state, playerId, zones.Hand.Select(id => state.CardObjects[id].CardNo!)
                .Where(cardNo => cardNo is not null && Printed.Value.ContainsKey(cardNo)).ToArray());
        return state;
    }

    public static MatchState Add(MatchState state, string playerId, params string[] plannedCardNos)
    {
        var pools = state.RunePools.ToDictionary(x => x.Key, x => x.Value);
        pools[playerId] = pools[playerId] with { Power = pools[playerId].Power + plannedCardNos.Sum(cardNo => Printed.Value[cardNo]) };
        return state with { RunePools = pools };
    }
}
