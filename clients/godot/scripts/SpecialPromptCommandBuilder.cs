using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Riftbound.GodotClient;

public static class SpecialPromptCommandBuilder
{
    public static bool TryBuild(
        Godot.Collections.Dictionary action,
        out Dictionary<string, object?> payload,
        out string payloadKey,
        out string reason)
    {
        payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        payloadKey = string.Empty;
        reason = string.Empty;

        var actionName = action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : string.Empty;
        var candidateJson = action.TryGetValue("candidateJson", out var candidateValue) ? candidateValue.AsString() : string.Empty;
        if (string.IsNullOrWhiteSpace(candidateJson))
        {
            reason = "candidate JSON is missing";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(candidateJson);
            if (string.Equals(actionName, "ORDER_TRIGGERS", StringComparison.Ordinal))
            {
                return TryBuildOrderTriggersCommand(document.RootElement, out payload, out payloadKey, out reason);
            }

            if (string.Equals(actionName, "ASSIGN_COMBAT_DAMAGE", StringComparison.Ordinal))
            {
                return TryBuildAssignCombatDamageCommand(document.RootElement, out payload, out payloadKey, out reason);
            }
        }
        catch (JsonException ex)
        {
            reason = $"malformed candidate JSON: {ex.Message}";
            return false;
        }

        reason = $"unsupported special action {actionName}";
        return false;
    }

    private static bool TryBuildOrderTriggersCommand(
        JsonElement candidate,
        out Dictionary<string, object?> payload,
        out string payloadKey,
        out string reason)
    {
        payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        payloadKey = string.Empty;
        reason = string.Empty;

        var metadata = MetadataElement(candidate);
        var orderedTriggerIds = metadata is null
            ? Array.Empty<string>()
            : FirstMetadataStringArray(metadata.Value, "orderedTriggerIds", "triggerIds");
        if (orderedTriggerIds.Length == 0 && metadata is { } metadataElement)
        {
            orderedTriggerIds = ChoiceIds(metadataElement, "triggerChoices");
        }

        if (orderedTriggerIds.Length == 0)
        {
            reason = "no server-provided trigger order";
            return false;
        }

        payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cmdType"] = "ORDER_TRIGGERS",
            ["orderedTriggerIds"] = orderedTriggerIds,
            ["triggerIds"] = orderedTriggerIds
        };
        payloadKey = string.Join(",", orderedTriggerIds);
        return true;
    }

    private static bool TryBuildAssignCombatDamageCommand(
        JsonElement candidate,
        out Dictionary<string, object?> payload,
        out string payloadKey,
        out string reason)
    {
        payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        payloadKey = string.Empty;
        reason = string.Empty;

        if (MetadataElement(candidate) is not { } metadata)
        {
            reason = "damage metadata is missing";
            return false;
        }

        var battleId = ReadString(metadata, "battleId");
        var battlefieldId = FirstNonEmpty(ReadString(metadata, "battlefieldId"), ReadString(metadata, "battlefieldObjectId"));
        var damageBySource = FirstMetadataIntMap(metadata, "assignableDamagePool", "damagePool", "damagePoolBySource");
        var assignments = DefaultDamageAssignments(metadata, damageBySource);
        if (string.IsNullOrWhiteSpace(battleId))
        {
            reason = "battleId is missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(battlefieldId))
        {
            reason = "battlefieldId is missing";
            return false;
        }

        if (assignments.Count == 0)
        {
            reason = "no server-provided damage assignment choices";
            return false;
        }

        payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cmdType"] = "ASSIGN_COMBAT_DAMAGE",
            ["battleId"] = battleId,
            ["battlefieldId"] = battlefieldId,
            ["assignments"] = assignments
        };
        payloadKey = string.Join(",", assignments.Select(assignment =>
            $"{assignment["sourceObjectId"]}->{assignment["targetObjectId"]}:{assignment["damage"]}"));
        return true;
    }

    private static List<Dictionary<string, object?>> DefaultDamageAssignments(
        JsonElement metadata,
        IReadOnlyDictionary<string, int> damageBySource)
    {
        var assignments = RequiredDamageAssignments(metadata);
        if (assignments.Count > 0)
        {
            return assignments;
        }

        var chosenTargets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (sourceObjectId, targetObjectId) in AssignmentChoicePairs(metadata))
        {
            chosenTargets.TryAdd(sourceObjectId, targetObjectId);
        }

        if (chosenTargets.Count == 0)
        {
            foreach (var (sourceObjectId, targetObjectIds) in StringListMap(metadata, "legalTargets", "legalTargetsBySource"))
            {
                var targetObjectId = targetObjectIds.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(targetObjectId))
                {
                    chosenTargets.TryAdd(sourceObjectId, targetObjectId);
                }
            }
        }

        return chosenTargets
            .Select(entry => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sourceObjectId"] = entry.Key,
                ["targetObjectId"] = entry.Value,
                ["damage"] = Math.Max(1, damageBySource.GetValueOrDefault(entry.Key, 1))
            })
            .ToList();
    }

    private static List<Dictionary<string, object?>> RequiredDamageAssignments(JsonElement metadata)
    {
        var result = new List<Dictionary<string, object?>>();
        if (!metadata.TryGetProperty("requiredAssignments", out var assignments)
            || assignments.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var assignment in assignments.EnumerateArray())
        {
            if (assignment.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var sourceObjectId = ReadString(assignment, "sourceObjectId");
            var targetObjectId = ReadString(assignment, "targetObjectId");
            var damage = ReadInt(assignment, "damage");
            if (damage <= 0)
            {
                damage = ReadInt(assignment, "requiredDamage");
            }
            if (string.IsNullOrWhiteSpace(sourceObjectId)
                || string.IsNullOrWhiteSpace(targetObjectId)
                || damage <= 0)
            {
                continue;
            }

            result.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sourceObjectId"] = sourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["damage"] = damage
            });
        }

        return result;
    }

    private static IEnumerable<(string SourceObjectId, string TargetObjectId)> AssignmentChoicePairs(JsonElement metadata)
    {
        if (!metadata.TryGetProperty("assignmentChoices", out var choices)
            || choices.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            var id = ReadString(choice, "id");
            var parts = id.Split("->", 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && !string.IsNullOrWhiteSpace(parts[0])
                && !string.IsNullOrWhiteSpace(parts[1]))
            {
                yield return (parts[0], parts[1]);
                continue;
            }

            var sourceObjectId = ReadString(choice, "sourceObjectId");
            var targetObjectId = ReadString(choice, "targetObjectId");
            if (!string.IsNullOrWhiteSpace(sourceObjectId)
                && !string.IsNullOrWhiteSpace(targetObjectId))
            {
                yield return (sourceObjectId, targetObjectId);
            }
        }
    }

    private static JsonElement? MetadataElement(JsonElement candidate)
    {
        return candidate.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            ? metadata
            : null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string[] FirstMetadataStringArray(JsonElement metadata, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var values = ReadStringArray(metadata, propertyName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (values.Length > 0)
            {
                return values;
            }
        }

        return [];
    }

    private static string[] ChoiceIds(JsonElement metadata, string propertyName)
    {
        if (!metadata.TryGetProperty(propertyName, out var choices)
            || choices.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return choices
            .EnumerateArray()
            .Select(choice => ReadString(choice, "id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> FirstMetadataIntMap(JsonElement metadata, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var values = IntMap(metadata, propertyName);
            if (values.Count > 0)
            {
                return values;
            }
        }

        return new Dictionary<string, int>(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> IntMap(JsonElement metadata, string propertyName)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!metadata.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var item in property.EnumerateObject())
        {
            var value = item.Value.ValueKind switch
            {
                JsonValueKind.Number when item.Value.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(item.Value.GetString(), out var number) => number,
                _ => 0
            };
            if (!string.IsNullOrWhiteSpace(item.Name) && value > 0)
            {
                result[item.Name] = value;
            }
        }

        return result;
    }

    private static IEnumerable<(string SourceObjectId, IReadOnlyList<string> TargetObjectIds)> StringListMap(
        JsonElement metadata,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!metadata.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var item in property.EnumerateObject())
            {
                var values = ReadStringArray(item.Value);
                if (!string.IsNullOrWhiteSpace(item.Name) && values.Count > 0)
                {
                    yield return (item.Name, values);
                }
            }
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), out var number) => number,
            _ => 0
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return [];
        }

        return ReadStringArray(property);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToArray();
    }
}
