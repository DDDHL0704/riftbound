using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static MatchState ApplyOpeningRoleDraw(MatchState state, List<GameEvent> events)
    {
        var player = state.TurnPlayerId;
        if (state.OpeningRoleDrawPendingPlayerIds?.Contains(player, StringComparer.Ordinal) != true)
            return state;

        var pending = state.OpeningRoleDrawPendingPlayerIds.Where(id => id != player).ToArray();
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var draw = DrawCards(state, cards, player, zones[player], 1);
        zones[player] = zones[player] with
        {
            MainDeck = draw.MainDeck,
            Graveyard = draw.Graveyard,
            Hand = zones[player].Hand.Concat(draw.DrawnCards).ToArray()
        };
        events.AddRange(BuildTurnStartEvents(state, 0, draw, []).Where(item => item.Kind is not
            ("TURN_START_BEGAN" or "RUNES_CALLED" or "RUNE_POOL_CLEARED" or "MAIN_PHASE_BEGAN")));
        return state with
        {
            PlayerZones = zones,
            CardObjects = cards,
            PlayerScores = draw.PlayerScores,
            RngCursor = draw.RngCursor,
            WinnerPlayerId = draw.WinnerPlayerId,
            Status = draw.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            OpeningRoleDrawPendingPlayerIds = pending.Length == 0 ? null : pending
        };
    }
}

public static partial class MatchRecoveryValidator
{
    private static void ValidateOpeningRoleDraw(MatchState state, List<string> errors)
    {
        if (state.OpeningRoleDrawPendingPlayerIds is not { } pending) return;
        if (pending.Count == 0 || pending.Distinct(StringComparer.Ordinal).Count() != pending.Count)
            errors.Add("invalid pending opening role draw players");
        foreach (var player in pending)
            if (string.IsNullOrEmpty(player) || !state.Seats.ContainsKey(player)
                || !state.PlayerDecklists.TryGetValue(player, out var deck)
                || !string.IsNullOrEmpty(deck.LegendCardNo) && !string.IsNullOrEmpty(deck.ChampionCardNo))
                errors.Add("pending opening role draw requires a seated player with an omitted deck role");
    }
}
