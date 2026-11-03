namespace Riftbound.Engine;

internal static class PlayerRelations
{
    // Supported rooms are duels. Ownership, controller ids and seat order do
    // not establish an alliance; adding teams requires an explicit relation.
    internal static bool AreAllies(MatchState state, string? first, string? second) => false;
}
