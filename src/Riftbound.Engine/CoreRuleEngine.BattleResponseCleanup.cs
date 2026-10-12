using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 465.1, 466: once the duel closes, a missing side skips combat
    // damage. Battle cleanup, result, control and conquest still apply.
    private static ResolutionResult ResolveBattleResponseWithoutCombatDamage(MatchState state,
        string attackingPlayer, string battlefield, IReadOnlyList<string> originalAttackers,
        IReadOnlyList<string> originalDefenders, IReadOnlyList<string> attackers,
        IReadOnlyList<string> defenders, IReadOnlyList<GameEvent> priorityEvents)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var defendingPlayer = originalDefenders.Select(id => cards.GetValueOrDefault(id)?.ControllerId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        var winner = attackers.Count > 0 ? attackingPlayer : defenders.Count > 0 ? defendingPlayer : null;
        var events = priorityEvents.ToList();
        events.Add(new("BATTLE_RESPONSE_PRIORITY_CLOSED", "双方让过战斗响应优先权，开始结算战斗",
            new Dictionary<string, object?> { ["battlefieldObjectId"] = battlefield, ["battleId"] = state.BattleState.BattleId,
                ["attackingPlayerId"] = attackingPlayer, ["declarationContextPreserved"] = true,
                ["passedPriorityPlayerIds"] = SeatPlayerIds(state), ["combatDamageSkipped"] = true }));
        ApplyBattleCleanup(zones, cards, battlefield, attackers, defenders, events);
        if (winner is null)
            events.Add(BuildBattleNoResultEvent(zones, cards, battlefield, attackers, defenders, attackingPlayer, defendingPlayer));
        CloseResolvedBattle(zones, cards, battlefield, originalAttackers, originalDefenders, events);
        var settled = state with {
            Tick = state.Tick + 1, ActivePlayerId = state.TurnPlayerId,
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            TimingState = TimingStates.NeutralOpen, FocusPlayerId = null, PriorityPlayerId = null,
            PassedFocusPlayerIds = [], PassedPriorityPlayerIds = [],
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(ClearBattleDamageAssignmentLedgerMarkers(
                ClearBattleResponseDeclarationContextMarkers(state.UntilEndOfTurnEffects)), BattlefieldTaskMarkers.BattleSkipped(battlefield)) };
        var control = ResolveNonBattlefieldControlAfterSpellDuelClosed(settled, battlefield, concludingBattle: true,
            battleWinnerPlayerId: winner);
        settled = control.State;
        events.AddRange(control.Events);
        zones = NormalizeZonesForSeats(settled);
        cards = settled.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        if (winner is not null)
            settled = ResolveBattleWinLegendDraw(settled, zones, cards, winner, originalAttackers, originalDefenders, events);
        settled = settled with {
            BattleResolutions = AppendBattleResolutionEvents(state.BattleResolutions, events, settled.Tick, battlefield,
                attackingPlayer, defendingPlayer, winner, originalAttackers, originalDefenders, zones, cards),
            UntilEndOfTurnEffects = MarkPlayersWhoGainedExperienceThisTurn(settled.UntilEndOfTurnEffects, events) };
        var advance = AdvancePendingBattlefieldTasksAfterStateChange(settled, attackingPlayer, state);
        events.AddRange(advance.Events);
        return new(true, null, advance.State, events, ResolutionResult.BuildSnapshots(advance.State), BuildCorePrompts(advance.State));
    }

    private static MatchState ResolveBattleWinLegendDraw(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, string winner, IReadOnlyList<string> attackers,
        IReadOnlyList<string> defenders, List<GameEvent> events)
    {
        if (!TryGetDravenLegendCardNo(zones, cards, winner, out var legend)) return state;
        events.Add(new("LEGEND_TRIGGER_RESOLVED", $"{winner} 的荣耀行刑官因赢得战斗触发",
            new Dictionary<string, object?> { ["playerId"] = winner, ["legendCardNo"] = legend,
                ["trigger"] = "BATTLE_WON_DRAW_ONE", ["sourceObjectId"] = attackers.FirstOrDefault(),
                ["attackerObjectId"] = attackers.FirstOrDefault(), ["defenderObjectIds"] = defenders.ToArray() }));
        var draw = ApplyDrawToPlayer(state, zones, state.PlayerScores, winner, 1, state.RngCursor, events);
        return state with { PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores, RngCursor = draw.RngCursor,
            Status = draw.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
            WinnerPlayerId = draw.WinnerPlayerId ?? state.WinnerPlayerId };
    }
}
