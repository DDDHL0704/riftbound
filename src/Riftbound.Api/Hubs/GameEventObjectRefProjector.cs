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

    private const string SingularObjectIdSuffix = "ObjectId";
    private const string ArrayObjectIdsSuffix = "ObjectIds";

    public static IReadOnlyList<GameEvent> ProjectEvents(IReadOnlyList<GameEvent> events, MatchState state)
    {
        return events
            .Select(gameEvent => gameEvent with
            {
                ObjectRefs = gameEvent.ObjectRefs is { Count: > 0 }
                    ? EnrichEventObjectRefs(gameEvent.ObjectRefs, state)
                    : BuildEventObjectRefs(gameEvent.Payload, state)
            })
            .ToArray();
    }

    private static IReadOnlyList<GameEventObjectRef>? EnrichEventObjectRefs(
        IReadOnlyList<GameEventObjectRef> sourceRefs,
        MatchState state)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var refs = new List<GameEventObjectRef>();
        foreach (var sourceRef in sourceRefs)
        {
            var objectId = NormalizeOptionalText(sourceRef.ObjectId);
            if (objectId is null)
            {
                continue;
            }

            var role = NormalizeOptionalText(sourceRef.Role) ?? "对象";
            if (!seen.Add($"{role}:{objectId}"))
            {
                continue;
            }

            refs.Add(EnrichEventObjectRef(sourceRef, objectId, role, state));
        }

        return refs.Count > 0 ? refs : null;
    }

    private static GameEventObjectRef EnrichEventObjectRef(
        GameEventObjectRef sourceRef,
        string objectId,
        string role,
        MatchState state)
    {
        if (sourceRef.IsHidden || string.Equals(objectId, "HIDDEN", StringComparison.Ordinal))
        {
            return new GameEventObjectRef(objectId, role, IsFaceDown: sourceRef.IsFaceDown, IsHidden: true);
        }

        if (!state.CardObjects.TryGetValue(objectId, out var cardObject))
        {
            return sourceRef with
            {
                ObjectId = objectId,
                Role = role
            };
        }

        state.ObjectLocations.TryGetValue(objectId, out var location);
        var isFaceDown = sourceRef.IsFaceDown || cardObject.IsFaceDown;
        var isHidden = sourceRef.IsHidden || isFaceDown;
        return new GameEventObjectRef(
            objectId,
            role,
            isHidden ? null : cardObject.CardNo ?? NormalizeOptionalText(sourceRef.CardNo),
            cardObject.OwnerId ?? NormalizeOptionalText(sourceRef.OwnerId),
            cardObject.ControllerId ?? NormalizeOptionalText(sourceRef.ControllerId),
            location?.Zone ?? NormalizeOptionalText(sourceRef.Zone),
            location?.BattlefieldObjectId ?? NormalizeOptionalText(sourceRef.BattlefieldObjectId),
            isFaceDown,
            isHidden);
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
            if (normalizedObjectId is null || !seen.Add($"{role}:{normalizedObjectId}"))
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

            if (TryInferSingularObjectRefRole(key, out var inferredSingularRole)
                && TryReadString(value, out var inferredObjectId))
            {
                refs.Add((inferredSingularRole, inferredObjectId));
                continue;
            }

            if (TryInferArrayObjectRefRole(key, out var inferredArrayRole))
            {
                refs.AddRange(ReadStringList(value).Select(objectId => (inferredArrayRole, objectId)));
                continue;
            }

            foreach (var nested in ReadNestedRecords(value))
            {
                CollectEventObjectRefs(nested, refs, depth + 1);
            }
        }
    }

    private static bool TryInferSingularObjectRefRole(string key, out string role)
    {
        if (!key.EndsWith(SingularObjectIdSuffix, StringComparison.Ordinal)
            || key.Length <= SingularObjectIdSuffix.Length)
        {
            role = string.Empty;
            return false;
        }

        role = InferObjectRefRole(key[..^SingularObjectIdSuffix.Length]);
        return true;
    }

    private static bool TryInferArrayObjectRefRole(string key, out string role)
    {
        if (!key.EndsWith(ArrayObjectIdsSuffix, StringComparison.Ordinal)
            || key.Length <= ArrayObjectIdsSuffix.Length)
        {
            role = string.Empty;
            return false;
        }

        role = InferObjectRefRole(key[..^ArrayObjectIdsSuffix.Length]);
        return true;
    }

    private static string InferObjectRefRole(string rawPrefix)
    {
        var prefix = rawPrefix.ToLowerInvariant();
        if (prefix.Contains("battlefield", StringComparison.Ordinal))
        {
            return "战场";
        }

        if (prefix.Contains("source", StringComparison.Ordinal) || prefix.Contains("triggeredby", StringComparison.Ordinal))
        {
            return "来源";
        }

        if (prefix.Contains("target", StringComparison.Ordinal) || prefix.Contains("chosen", StringComparison.Ordinal) || prefix.Contains("selected", StringComparison.Ordinal))
        {
            return "目标";
        }

        if (prefix.Contains("attacker", StringComparison.Ordinal))
        {
            return "攻击";
        }

        if (prefix.Contains("defender", StringComparison.Ordinal))
        {
            return "防守";
        }

        if (prefix.Contains("destroy", StringComparison.Ordinal)
            || prefix.Contains("defeated", StringComparison.Ordinal)
            || prefix.Contains("removed", StringComparison.Ordinal)
            || prefix.Contains("cleared", StringComparison.Ordinal))
        {
            return "被移除";
        }

        if (prefix.Contains("discard", StringComparison.Ordinal))
        {
            return "弃置";
        }

        if (prefix.Contains("returned", StringComparison.Ordinal) || prefix.Contains("recalled", StringComparison.Ordinal))
        {
            return "返回";
        }

        if (prefix.Contains("recycled", StringComparison.Ordinal))
        {
            return "回收";
        }

        if (prefix.Contains("revealed", StringComparison.Ordinal))
        {
            return "展示";
        }

        if (prefix.Contains("rune", StringComparison.Ordinal))
        {
            return "符文";
        }

        if (prefix.Contains("equipment", StringComparison.Ordinal) || prefix.Contains("armament", StringComparison.Ordinal))
        {
            return "装备";
        }

        if (prefix.Contains("token", StringComparison.Ordinal))
        {
            return "衍生";
        }

        if (prefix.Contains("played", StringComparison.Ordinal))
        {
            return "打出";
        }

        if (prefix.Contains("activated", StringComparison.Ordinal))
        {
            return "激活";
        }

        if (prefix.Contains("ready", StringComparison.Ordinal) || prefix.Contains("readied", StringComparison.Ordinal))
        {
            return "重置";
        }

        if (prefix.Contains("moved", StringComparison.Ordinal) || prefix.Contains("destination", StringComparison.Ordinal))
        {
            return "移动";
        }

        if (prefix.Contains("unit", StringComparison.Ordinal) || prefix.Contains("participant", StringComparison.Ordinal))
        {
            return "单位";
        }

        if (prefix.Contains("hidden", StringComparison.Ordinal))
        {
            return "隐藏";
        }

        return "对象";
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
            cardObject.IsFaceDown,
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
