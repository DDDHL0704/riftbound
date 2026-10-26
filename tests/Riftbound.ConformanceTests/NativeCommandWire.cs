using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;

namespace Riftbound.ConformanceTests;

// Construct new native test requests from the actual intent and the original
// envelope. Existing fields, including intentional invalid values, take priority.
internal static class NativeCommandWire
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static int sequence;

    public static JsonElement Bind(GameCommand command, JsonElement original,
        [CallerMemberName] string caller = "")
    {
        if (original.ValueKind != JsonValueKind.Object
            || !original.TryGetProperty("cmdType", out var type) || type.ValueKind != JsonValueKind.String
            || type.GetString() != command.CmdType
            || command is ReadyCommand or PassCommand or PassPriorityCommand or PassFocusCommand
                or EndTurnCommand or SurrenderCommand or UnsupportedCommand) return original;
        var names = original.EnumerateObject().Select(property => property.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) return original;

        var raw = JsonNode.Parse(original.GetRawText())!.AsObject();
        var payload = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        var added = false;
        // The battle executor treats omitted attacker/defender lists as empty.
        if (command is DeclareBattleCommand)
            foreach (var field in new[] { "attackerObjectIds", "defenderObjectIds" })
                if (!raw.ContainsKey(field))
                {
                    raw[field] = payload[field]?.DeepClone() ?? new JsonArray();
                    added = true;
                }
        foreach (var property in payload)
            if (property.Key != "cmdType" && property.Value is not null && !raw.ContainsKey(property.Key))
            {
                if (property.Key is "mode" or "origin" or "destination"
                    && property.Value is JsonValue text && text.TryGetValue<string>(out var value) && value == "") continue;
                if (property.Key == "optionalCosts" && property.Value is JsonArray { Count: 0 }) continue;
                raw[property.Key] = property.Value.DeepClone();
                added = true;
            }
        if (!added) return original;
        var complete = JsonSerializer.SerializeToElement(raw);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_COMMAND_WIRE_EVIDENCE") is { Length: > 0 } root)
        {
            Directory.CreateDirectory(root);
            var number = Interlocked.Increment(ref sequence);
            File.WriteAllText(Path.Combine(root, number.ToString("D6") + ".json"),
                JsonSerializer.Serialize(new { caller, original, complete,
                    command = JsonSerializer.SerializeToElement(command, command.GetType(), Web) }, Web));
        }
        return complete;
    }

    public static JsonElement? Bind(GameCommand command, JsonElement? original,
        [CallerMemberName] string caller = "")
        => original is { } raw ? Bind(command, raw, caller) : null;

    public static JsonElement Bind(GameCommand command, ref JsonElement original,
        [CallerMemberName] string caller = "")
        => original = Bind(command, original, caller);

    public static JsonElement? Bind(GameCommand command, ref JsonElement? original,
        [CallerMemberName] string caller = "")
        => original = Bind(command, original, caller);
}
