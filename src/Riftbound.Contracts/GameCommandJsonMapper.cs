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
            "READY" => new ReadyCommand(),
            "PASS_PRIORITY" => new PassPriorityCommand(),
            "PASS_FOCUS" => new PassFocusCommand(),
            "PASS" => new PassCommand(),
            "END_TURN" => new EndTurnCommand(),
            "PLAY_CARD" => new PlayCardCommand(
                Text(cmd, "sourceObjectId"),
                Text(cmd, "cardNo"),
                TextArray(cmd, "targetObjectIds"),
                Text(cmd, "mode"),
                TextArray(cmd, "optionalCosts"),
                Text(cmd, "destination")),
            "ACTIVATE_ABILITY" => new ActivateAbilityCommand(
                Text(cmd, "sourceObjectId"),
                Text(cmd, "abilityId"),
                TextArray(cmd, "targetObjectIds"),
                TextArray(cmd, "optionalCosts")),
            "HIDE_CARD" => new HideCardCommand(
                Text(cmd, "sourceObjectId"),
                Text(cmd, "cardNo"),
                Text(cmd, "destination"),
                TextArray(cmd, "optionalCosts")),
            "MOVE_UNIT" => new MoveUnitCommand(
                Text(cmd, "sourceObjectId"),
                Text(cmd, "origin"),
                Text(cmd, "destination"),
                TextArray(cmd, "optionalCosts")),
            "ASSEMBLE_EQUIPMENT" => new AssembleEquipmentCommand(
                Text(cmd, "sourceObjectId"),
                Text(cmd, "targetObjectId"),
                TextArray(cmd, "optionalCosts")),
            "DECLARE_BATTLE" => new DeclareBattleCommand(
                Text(cmd, "battlefieldId"),
                TextArray(cmd, "attackerObjectIds"),
                TextArray(cmd, "defenderObjectIds"),
                TextArray(cmd, "optionalCosts")),
            _ => new UnsupportedCommand(cmdType, cmd.Clone())
        };
    }

    private static string Text(JsonElement cmd, string propertyName)
    {
        return cmd.ValueKind == JsonValueKind.Object
            && cmd.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
    }

    private static IReadOnlyList<string> TextArray(JsonElement cmd, string propertyName)
    {
        if (cmd.ValueKind != JsonValueKind.Object
            || !cmd.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            .Select(item => item.GetString()!.Trim())
            .ToArray();
    }
}
