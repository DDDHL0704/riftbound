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
            .Select(gameEvent =>
            {
                var objectRefs = gameEvent.ObjectRefs is { Count: > 0 }
                    ? EnrichEventObjectRefs(gameEvent.ObjectRefs, state)
                    : BuildEventObjectRefs(gameEvent.Payload, state);
                return gameEvent with
                {
                    Payload = RedactHiddenPayload(gameEvent.Payload, state),
                    ObjectRefs = objectRefs
                };
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object?> RedactHiddenPayload(
        IReadOnlyDictionary<string, object?> payload,
        MatchState state)
    {
        var hiddenObjectIds = state.CardObjects
            .Where(entry => entry.Value.IsFaceDown)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (payload.Count == 0 || hiddenObjectIds.Count == 0)
        {
            return payload;
        }

        var redacted = RedactHiddenPayloadRecord(payload, hiddenObjectIds, out var changed);
        return changed ? redacted : payload;
    }

    private static IReadOnlyDictionary<string, object?> RedactHiddenPayloadRecord(
        IReadOnlyDictionary<string, object?> record,
        IReadOnlySet<string> hiddenObjectIds,
        out bool changed)
    {
        var redactedCardNoPrefixes = CollectRedactedCardNoPrefixes(record, hiddenObjectIds);
        var result = new Dictionary<string, object?>(record.Count, StringComparer.Ordinal);
        changed = false;
        foreach (var (key, value) in record)
        {
            var redactedValue = RedactHiddenPayloadValue(key, value, hiddenObjectIds, redactedCardNoPrefixes, out var valueChanged);
            result[key] = redactedValue;
            changed |= valueChanged;
        }

        return result;
    }

    private static HashSet<string> CollectRedactedCardNoPrefixes(
        IReadOnlyDictionary<string, object?> record,
        IReadOnlySet<string> hiddenObjectIds)
    {
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (rawKey, value) in record)
        {
            var key = NormalizePayloadKey(rawKey);
            if (TryGetObjectIdPrefix(key, out var prefix) && ContainsHiddenObjectId(value, hiddenObjectIds))
            {
                prefixes.Add(prefix);
                if (string.Equals(prefix, "source", StringComparison.Ordinal)
                    || string.Equals(prefix, "", StringComparison.Ordinal))
                {
                    prefixes.Add("");
                }
            }
        }

        return prefixes;
    }

    private static object? RedactHiddenPayloadValue(
        string rawKey,
        object? value,
        IReadOnlySet<string> hiddenObjectIds,
        HashSet<string> redactedCardNoPrefixes,
        out bool changed)
    {
        var key = NormalizePayloadKey(rawKey);
        if (TryGetObjectIdPrefix(key, out _))
        {
            return RedactHiddenObjectIdValue(value, hiddenObjectIds, out changed);
        }

        if (TryGetCardNoPrefix(key, out var cardNoPrefix) && redactedCardNoPrefixes.Contains(cardNoPrefix))
        {
            changed = true;
            return null;
        }

        return RedactNestedHiddenPayloadValue(value, hiddenObjectIds, out changed);
    }

    private static object? RedactHiddenObjectIdValue(
        object? value,
        IReadOnlySet<string> hiddenObjectIds,
        out bool changed)
    {
        if (TryReadString(value, out var text))
        {
            changed = hiddenObjectIds.Contains(text);
            return changed ? "HIDDEN" : value;
        }

        var objectIds = ReadStringList(value).ToArray();
        if (objectIds.Length == 0)
        {
            changed = false;
            return value;
        }

        changed = objectIds.Any(hiddenObjectIds.Contains);
        return changed
            ? objectIds.Select(objectId => hiddenObjectIds.Contains(objectId) ? "HIDDEN" : objectId).ToArray()
            : value;
    }

    private static object? RedactNestedHiddenPayloadValue(
        object? value,
        IReadOnlySet<string> hiddenObjectIds,
        out bool changed)
    {
        switch (value)
        {
            case null:
            case string:
                changed = false;
                return value;
            case IReadOnlyDictionary<string, object?> typed:
                return RedactHiddenPayloadRecord(typed, hiddenObjectIds, out changed);
            case IDictionary<string, object?> mutable:
                return RedactHiddenPayloadRecord(new Dictionary<string, object?>(mutable, StringComparer.Ordinal), hiddenObjectIds, out changed);
            case JsonElement { ValueKind: JsonValueKind.Object } jsonObject:
                return RedactHiddenPayloadRecord(JsonObjectRecord(jsonObject), hiddenObjectIds, out changed);
            case JsonElement { ValueKind: JsonValueKind.Array } jsonArray:
            {
                var items = new List<object?>();
                changed = false;
                foreach (var item in jsonArray.EnumerateArray())
                {
                    var redactedItem = RedactNestedHiddenPayloadValue(item.Clone(), hiddenObjectIds, out var itemChanged);
                    items.Add(redactedItem);
                    changed |= itemChanged;
                }

                return changed ? items.ToArray() : value;
            }
            case IEnumerable enumerable:
            {
                var items = new List<object?>();
                changed = false;
                foreach (var item in enumerable)
                {
                    var redactedItem = RedactNestedHiddenPayloadValue(item, hiddenObjectIds, out var itemChanged);
                    items.Add(redactedItem);
                    changed |= itemChanged;
                }

                return changed ? items.ToArray() : value;
            }
            default:
                var record = ObjectRecord(value);
                if (record.Count == 0)
                {
                    changed = false;
                    return value;
                }

                var redacted = RedactHiddenPayloadRecord(record, hiddenObjectIds, out changed);
                return changed ? redacted : value;
        }
    }

    private static bool ContainsHiddenObjectId(
        object? value,
        IReadOnlySet<string> hiddenObjectIds)
    {
        if (TryReadString(value, out var text))
        {
            return hiddenObjectIds.Contains(text);
        }
        return ReadStringList(value).Any(hiddenObjectIds.Contains);
    }

    private static bool TryGetCardNoPrefix(string key, out string prefix)
    {
        const string singularCardNoSuffix = "CardNo";
        if (string.Equals(key, "cardNo", StringComparison.Ordinal))
        {
            prefix = "";
            return true;
        }

        if (key.EndsWith(singularCardNoSuffix, StringComparison.Ordinal)
            && key.Length > singularCardNoSuffix.Length)
        {
            prefix = key[..^singularCardNoSuffix.Length];
            return true;
        }

        prefix = string.Empty;
        return false;
    }

    private static bool TryGetObjectIdPrefix(string key, out string prefix)
    {
        if (key.EndsWith(ArrayObjectIdsSuffix, StringComparison.Ordinal)
            && key.Length > ArrayObjectIdsSuffix.Length)
        {
            prefix = key[..^ArrayObjectIdsSuffix.Length];
            return true;
        }

        if (key.EndsWith(SingularObjectIdSuffix, StringComparison.Ordinal)
            && key.Length > SingularObjectIdSuffix.Length)
        {
            prefix = key[..^SingularObjectIdSuffix.Length];
            return true;
        }

        if (string.Equals(key, "objectId", StringComparison.Ordinal)
            || string.Equals(key, "objectIds", StringComparison.Ordinal))
        {
            prefix = "";
            return true;
        }

        if (string.Equals(key, "battlefieldId", StringComparison.Ordinal))
        {
            prefix = "battlefield";
            return true;
        }

        prefix = string.Empty;
        return false;
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
            return new GameEventObjectRef("HIDDEN", role, IsFaceDown: sourceRef.IsFaceDown, IsHidden: true);
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
        if (isHidden)
        {
            return new GameEventObjectRef("HIDDEN", role, IsFaceDown: isFaceDown, IsHidden: true);
        }

        return new GameEventObjectRef(
            objectId,
            role,
            cardObject.CardNo ?? NormalizeOptionalText(sourceRef.CardNo),
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
            return new GameEventObjectRef("HIDDEN", role, IsHidden: true);
        }

        if (!state.CardObjects.TryGetValue(objectId, out var cardObject))
        {
            return new GameEventObjectRef(objectId, role);
        }

        state.ObjectLocations.TryGetValue(objectId, out var location);
        if (cardObject.IsFaceDown)
        {
            return new GameEventObjectRef("HIDDEN", role, IsFaceDown: true, IsHidden: true);
        }

        return new GameEventObjectRef(
            objectId,
            role,
            cardObject.CardNo,
            cardObject.OwnerId,
            cardObject.ControllerId,
            location?.Zone,
            location?.BattlefieldObjectId,
            IsFaceDown: false,
            IsHidden: false);
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
