namespace Riftbound.Engine;

internal static class ProjectionViewers
{
    // Empty player IDs cannot occupy a room seat or own inspection permissions.
    // Keep the legacy table label separate from the identity used for visibility.
    internal const string Spectator = "";
    internal const string SpectatorTableId = "__spectator__";
}
