using System;
using System.Linq;
using System.Text.Json;

namespace Riftbound.GodotClient.Ui;

internal static class BattleEventPresenter
{
    public static string Describe(JsonElement message, string viewerId, Func<string, string?> cardName)
    {
        static string Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : "";
        static bool Hidden(JsonElement value) => new[] { "isHidden", "isFaceDown" }
            .Any(key => value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.True);
        var description = Text(message, "description");
        var references = message.TryGetProperty("objectRefs", out var refs) && refs.ValueKind == JsonValueKind.Array
            ? refs.EnumerateArray().ToArray() : [];
        string Name(JsonElement reference) => Hidden(reference) ? "隐藏卡牌" : cardName(Text(reference, "cardNo")) ?? "卡牌";
        if (Text(message, "kind") == "STACK_ITEM_RESOLVED")
        {
            var source = references.FirstOrDefault(reference => Text(reference, "role") == "来源");
            return source.ValueKind == JsonValueKind.Object ? $"{Name(source)}已结算" : "链顶行动已结算";
        }
        foreach (var reference in references.OrderByDescending(reference => Text(reference, "objectId").Length))
        {
            var id = Text(reference, "objectId");
            if (id.Length > 0) description = description.Replace(id, Name(reference), StringComparison.Ordinal);
        }
        if (message.TryGetProperty("payload", out var payload))
            foreach (var key in new[] { "playerId", "controllerId" })
            {
                var id = Text(payload, key);
                if (id.Length > 0) description = description.Replace(id, id == viewerId ? "我方" : "对手", StringComparison.Ordinal);
            }
        return description;
    }
}
