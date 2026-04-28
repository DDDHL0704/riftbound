using System.Text.Json;

namespace Riftbound.Contracts;

public static class GameCommandJsonMapper
{
    public static GameCommand Map(JsonElement cmd)
    {
        var cmdType = "UNKNOWN";
        if (cmd.ValueKind == JsonValueKind.Object
            && cmd.TryGetProperty("cmdType", out var cmdTypeProperty)
            && cmdTypeProperty.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(cmdTypeProperty.GetString()))
        {
            cmdType = cmdTypeProperty.GetString()!;
        }

        return cmdType switch
        {
            "PASS_PRIORITY" => new PassPriorityCommand(),
            "PASS_FOCUS" => new PassFocusCommand(),
            "PASS" => new PassCommand(),
            "END_TURN" => new EndTurnCommand(),
            _ => new UnsupportedCommand(cmdType, cmd.Clone())
        };
    }
}
