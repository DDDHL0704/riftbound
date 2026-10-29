using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial class MatchSession
{

    private void RequirePlayer(string playerId)
    {
        lock (seatGate)
        {
            if (!seats.ContainsKey(playerId))
            {
                throw new MatchSessionException(ErrorCodes.PlayerNotInRoom, "玩家不在房间中。");
            }
        }
    }

    private static string NewReconnectToken()
    {
        return $"rt_{Guid.NewGuid():N}";
    }

    private static string NormalizeClientIntentId(string clientIntentId)
    {
        if (string.IsNullOrWhiteSpace(clientIntentId))
        {
            throw new MatchSessionException(
                ErrorCodes.ClientIntentIdRequired,
                "客户端行动编号不能为空。");
        }

        var normalized = clientIntentId.Trim();
        if (normalized.StartsWith("room-seat:", StringComparison.Ordinal))
            throw new MatchSessionException(ErrorCodes.ClientIntentConflict, ClientIntentConflictMessage);
        return normalized;
    }

    private static string NormalizeScenarioId(string scenarioId)
    {
        return string.IsNullOrWhiteSpace(scenarioId)
            ? throw new MatchSessionException(ErrorCodes.UnsupportedCommand, "开发测试场景编号不能为空。")
            : scenarioId.Trim();
    }
}
