namespace Riftbound.Engine;

/// <summary>Connection presence is transport state, never a source of gameplay authority.</summary>
public sealed class PlayerConnections
{
    private readonly object gate = new();
    private readonly Dictionary<string, (string PlayerId, string? RoomId)> connections = new(StringComparer.Ordinal);

    public void Register(string connectionId, string playerId)
    {
        lock (gate)
        {
            var normalized = PlayerIdentityService.NormalizeHandle(playerId);
            var room = connections.TryGetValue(connectionId, out var existing) && existing.PlayerId == normalized
                ? existing.RoomId : null;
            connections[connectionId] = (normalized, room);
        }
    }

    public void EnterRoom(string connectionId, string playerId, string roomId)
    {
        lock (gate)
        {
            connections[connectionId] = (PlayerIdentityService.NormalizeHandle(playerId), roomId.Trim().ToUpperInvariant());
        }
    }

    public string? Disconnect(string connectionId)
    {
        lock (gate)
        {
            return connections.Remove(connectionId, out var existing) ? existing.PlayerId : null;
        }
    }

    public bool IsConnected(string playerId)
    {
        lock (gate)
        {
            return connections.Values.Any(connection => connection.PlayerId == playerId);
        }
    }

    public bool IsInRoom(string playerId, string roomId)
    {
        lock (gate)
        {
            return connections.Values.Any(connection => connection.PlayerId == playerId && connection.RoomId == roomId);
        }
    }
}
