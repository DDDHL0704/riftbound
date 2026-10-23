namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // SFD 025 / 025a promo grant a location permission, independently of the
    // Reaction timing permission. Player battle roles last until battle end;
    // the last attacking unit leaving does not end a pending result skill.
    // Shared execution and prompt destination enumeration must both call this.
    private static bool RengarAttackingBattlefieldPlayAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, string destination)
    {
        if (state.Status != MatchStatuses.InProgress || !state.Seats.ContainsKey(player)
            || !behavior.PlaysSourceToBaseAsUnit
            || behavior.CardNo is not ("SFD·025/221" or "SFD·025a/221·P")
            || !destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)) return false;
        var field = destination["BATTLEFIELD:".Length..];
        return state.CardObjects.TryGetValue(field, out var battlefield)
            && IsBattlefieldCardObject(battlefield) && !battlefield.IsFaceDown
            && IsObjectOnField(state.PlayerZones, field)
            && IsInitialBattleDuel(state, out var battle) && battle.InitialRolesEstablished
            && battle.BattlefieldId == field && battle.InitialAttackingPlayerId == player;
    }
}
