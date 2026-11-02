using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    private GameEvent BuildDeckSubmittedEvent(string playerId, OfficialDecklist decklist)
    {
        var payload = new Dictionary<string, object?>
        {
            ["playerId"] = playerId,
            ["mainDeckCount"] = decklist.MainDeck.Count,
            ["runeDeckCount"] = decklist.RuneDeck.Count,
            ["battlefieldCount"] = decklist.Battlefields.Count
        };
        // A shared event is also retained in spectator replay and journal views.
        // Private deck identities stay in the authoritative blueprint/command;
        // public legend and champion objects appear during the core opening.
        if (!options.HideSubmittedDeckIdentities)
        {
            payload["legendCardNo"] = decklist.LegendCardNo;
            payload["championCardNo"] = decklist.ChampionCardNo;
        }
        return new GameEvent("DECK_SUBMITTED", $"{playerId} 提交正式卡组", payload);
    }
}
