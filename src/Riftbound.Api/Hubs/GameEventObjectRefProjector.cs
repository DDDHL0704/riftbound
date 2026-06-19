using System.Collections;
using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.Api.Hubs;

public static class GameEventObjectRefProjector
{
    private static readonly IReadOnlyDictionary<string, string> SingularEventObjectRefRoles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["attachedToObjectId"] = "贴附",
        ["attackerObjectId"] = "攻击",
        ["battlefieldId"] = "战场",
        ["battlefieldObjectId"] = "战场",
        ["cardObjectId"] = "卡牌",
        ["defenderObjectId"] = "防守",
        ["destroyedObjectId"] = "被摧毁",
        ["equipmentObjectId"] = "装备",
        ["hostObjectId"] = "贴附",
        ["objectId"] = "对象",
        ["runeObjectId"] = "符文",
        ["sourceObjectId"] = "来源",
        ["targetObjectId"] = "目标",
        ["unitObjectId"] = "单位"
    };

    private static readonly IReadOnlyDictionary<string, string> ArrayEventObjectRefRoles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["attackerObjectIds"] = "攻击",
        ["banishedObjectIds"] = "放逐",
        ["cardObjectIds"] = "卡牌",
        ["chosenObjectIds"] = "已选",
        ["defenderObjectIds"] = "防守",
        ["destroyedObjectIds"] = "被摧毁",
        ["discardedObjectIds"] = "弃置",
        ["exhaustedObjectIds"] = "横置",
        ["objectIds"] = "对象",
        ["participantObjectIds"] = "参与",
        ["paymentObjectIds"] = "费用",
        ["readyObjectIds"] = "重置",
        ["revealedObjectIds"] = "展示",
        ["runeObjectIds"] = "符文",
        ["sourceObjectIds"] = "来源",
        ["targetObjectIds"] = "目标",
        ["unitObjectIds"] = "单位"
    };

    public static IReadOnlyList<GameEvent> ProjectEvents(IReadOnlyList<GameEvent> events, MatchState state)
    {
        return events
            .Select(gameEvent => gameEvent.ObjectRefs is { Count: > 0 }
                ? gameEvent
                : gameEvent with { ObjectRefs = BuildEventObjectRefs(gameEvent.Payload, state) })
            .ToArray();
    }

    public static IReadOnlyList<GameEventObjectRef>? BuildEventObjectRefs(
        IReadOnlyDictionary<string, object?> payload,
        MatchState state)
    {
        var rawRefs = new List<(string Role, string ObjectId)>();
        CollectEventObjectRefs(payload, rawRefs, 0);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var refs = new List<GameEventObjectRef>();
        foreach (var (role, objectId) in rawRefs)
        {
            var normalizedObjectId = NormalizeOptionalText(objectId);
            if (normalizedObjectId is null || !seen.Add(normalizedObjectId))
            {
                continue;
            }

            refs.Add(BuildEventObjectRef(normalizedObjectId, role, state));
        }

        return refs.Count > 0 ? refs : null;
    }

    private static void CollectEventObjectRefs(
        IReadOnlyDictionary<string, object?> record,
        List<(string Role, string ObjectId)> refs,
        int depth)
    {
        if (depth > 2)
        {
            return;
        }

        foreach (var (rawKey, value) in record)
        {
            var key = NormalizePayloadKey(rawKey);
            if (SingularEventObjectRefRoles.TryGetValue(key, out var singularRole)
                && TryReadString(value, out var objectId))
            {
                refs.Add((singularRole, objectId));
                continue;
            }

            if (ArrayEventObjectRefRoles.TryGetValue(key, out var arrayRole))
            {
                refs.AddRange(ReadStringList(value).Select(objectId => (arrayRole, objectId)));
                continue;
            }

            foreach (var nested in ReadNestedRecords(value))
            {
                CollectEventObjectRefs(nested, refs, depth + 1);
            }
        }
    }

    private static GameEventObjectRef BuildEventObjectRef(string objectId, string role, MatchState state)
    {
        if (string.Equals(objectId, "HIDDEN", StringComparison.Ordinal))
        {
            return new GameEventObjectRef(objectId, role, IsHidden: true);
        }

        if (!state.CardObjects.TryGetValue(objectId, out var cardObject))
        {
            return new GameEventObjectRef(objectId, role);
        }

        state.ObjectLocations.TryGetValue(objectId, out var location);
        return new GameEventObjectRef(
            objectId,
            role,
            cardObject.IsFaceDown ? null : cardObject.CardNo,
            cardObject.OwnerId,
            cardObject.ControllerId,
            location?.Zone,
            location?.BattlefieldObjectId,
            cardObject.IsFaceDown);
    }

    private static bool TryReadString(object? value, out string text)
    {
        switch (value)
        {
            case string stringValue when !string.IsNullOrWhiteSpace(stringValue):
                text = stringValue.Trim();
                return true;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                var elementText = element.GetString();
                if (!string.IsNullOrWhiteSpace(elementText))
                {
                    text = elementText.Trim();
                    return true;
                }

                break;
        }

        text = string.Empty;
        return false;
    }

    private static IEnumerable<string> ReadStringList(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (TryReadString(item, out var text))
                {
                    yield return text;
                }
            }

            yield break;
        }

        if (value is string)
        {
            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryReadString(item, out var text))
                {
                    yield return text;
                }
            }
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> ReadNestedRecords(object? value)
    {
        switch (value)
        {
            case null:
            case string:
                yield break;
            case IReadOnlyDictionary<string, object?> typed:
                yield return typed;
                yield break;
            case JsonElement { ValueKind: JsonValueKind.Object } jsonObject:
                yield return JsonObjectRecord(jsonObject);
                yield break;
            case JsonElement { ValueKind: JsonValueKind.Array } jsonArray:
                foreach (var item in jsonArray.EnumerateArray())
                {
                    foreach (var nested in ReadNestedRecords(item.Clone()))
                    {
                        yield return nested;
                    }
                }

                yield break;
            case IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    foreach (var nested in ReadNestedRecords(item))
                    {
                        yield return nested;
                    }
                }

                yield break;
            default:
                var record = ObjectRecord(value);
                if (record.Count > 0)
                {
                    yield return record;
                }

                break;
        }
    }

    private static IReadOnlyDictionary<string, object?> JsonObjectRecord(JsonElement jsonObject)
    {
        return jsonObject
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, object?> ObjectRecord(object value)
    {
        var type = value.GetType();
        if (IsScalarType(type))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToDictionary(property => NormalizePayloadKey(property.Name), property => property.GetValue(value), StringComparer.Ordinal);
    }

    private static bool IsScalarType(Type type)
    {
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(Guid)
            || type == typeof(TimeSpan);
    }

    private static string NormalizePayloadKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) || !char.IsUpper(key[0])
            ? key
            : $"{char.ToLowerInvariant(key[0])}{key[1..]}";
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
