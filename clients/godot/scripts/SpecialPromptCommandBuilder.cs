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

            if (string.Equals(actionName, "DECLARE_BATTLE", StringComparison.Ordinal))
            {
                return TryBuildDeclareBattleCommand(document.RootElement, out payload, out payloadKey, out reason);
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

    private static bool TryBuildDeclareBattleCommand(
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
            reason = "declare battle metadata is missing";
            return false;
        }

        if (FirstSourceRequirement(metadata) is not { } requirement)
        {
            reason = "declare battle source requirement is missing";
            return false;
        }

        var sourceObjectId = FirstNonEmpty(
            ReadString(requirement, "sourceObjectId"),
            FirstChoiceId(candidate, "sources"));
        var battlefieldId = FirstNonEmpty(
            FirstChoiceId(requirement, "battlefieldChoices", "destinationChoices"),
            FirstChoiceId(candidate, "destinations"));
        var defenderObjectIds = FirstNonEmptyStrings(
            ChoiceIdsByIndex(requirement, "targetChoicesByIndex"),
            ChoiceIds(requirement, "targetChoices"),
            ChoiceIds(candidate, "targets"));
        var battlefieldTargetObjectIds = FirstNonEmptyStrings(
            ChoiceIdsByIndex(requirement, "battlefieldTargetChoicesByIndex"),
            ChoiceIds(requirement, "battlefieldTargetChoices"));
        var optionalCosts = DeclareBattleOptionalCosts(requirement, candidate);

        if (string.IsNullOrWhiteSpace(sourceObjectId))
        {
            reason = "attacker source is missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(battlefieldId))
        {
            reason = "battlefield choice is missing";
            return false;
        }

        if (defenderObjectIds.Length == 0)
        {
            reason = "defender choice is missing";
            return false;
        }

        payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cmdType"] = "DECLARE_BATTLE",
            ["battlefieldId"] = battlefieldId,
            ["attackerObjectIds"] = new[] { sourceObjectId },
            ["defenderObjectIds"] = defenderObjectIds,
            ["optionalCosts"] = optionalCosts
        };

        if (battlefieldTargetObjectIds.Length > 0)
        {
            payload["battlefieldTargetObjectIds"] = battlefieldTargetObjectIds;
        }

        payloadKey = string.Join(",",
            new[]
            {
                $"attacker={sourceObjectId}",
                $"battlefield={battlefieldId}",
                $"defenders={string.Join("+", defenderObjectIds)}",
                $"battlefieldTargets={string.Join("+", battlefieldTargetObjectIds)}",
                $"optionalCosts={string.Join("+", optionalCosts)}"
            });
        return true;
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
            .Select(ChoiceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static JsonElement? FirstSourceRequirement(JsonElement metadata)
    {
        if (!metadata.TryGetProperty("sourceRequirements", out var requirements))
        {
            return null;
        }

        if (requirements.ValueKind == JsonValueKind.Array)
        {
            foreach (var requirement in requirements.EnumerateArray())
            {
                if (requirement.ValueKind == JsonValueKind.Object)
                {
                    return requirement;
                }
            }
        }

        if (requirements.ValueKind == JsonValueKind.Object)
        {
            foreach (var requirement in requirements.EnumerateObject())
            {
                if (requirement.Value.ValueKind == JsonValueKind.Object)
                {
                    return requirement.Value;
                }
            }
        }

        return null;
    }

    private static string FirstChoiceId(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var id = ChoiceIds(element, propertyName).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return string.Empty;
    }

    private static string[] ChoiceIdsByIndex(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var indexedChoices)
                || indexedChoices.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var values = indexedChoices
                .EnumerateObject()
                .OrderBy(choiceGroup => NumericKey(choiceGroup.Name))
                .Select(choiceGroup => FirstChoiceIdFromArray(choiceGroup.Value))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (values.Length > 0)
            {
                return values;
            }
        }

        return [];
    }

    private static string[] FirstNonEmptyStrings(params string[][] values)
    {
        foreach (var value in values)
        {
            if (value.Length > 0)
            {
                return value;
            }
        }

        return [];
    }

    private static string[] DeclareBattleOptionalCosts(JsonElement requirement, JsonElement candidate)
    {
        var optionalCosts = new List<string>();
        AddStrings(optionalCosts, ReadStringArray(requirement, "requiredOptionalCosts"));
        AddCombatAssignmentCost(optionalCosts, ChoiceIds(requirement, "optionalCostChoices"));
        AddCombatAssignmentCost(optionalCosts, ChoiceIds(candidate, "optionalCosts"));
        return optionalCosts
            .Where(cost => !string.IsNullOrWhiteSpace(cost))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddCombatAssignmentCost(List<string> optionalCosts, IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.Equals(candidate, "COMBAT_ASSIGNMENT", StringComparison.Ordinal))
            {
                optionalCosts.Add(candidate);
            }
        }
    }

    private static void AddStrings(List<string> values, IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                values.Add(candidate);
            }
        }
    }

    private static string FirstChoiceIdFromArray(JsonElement choices)
    {
        if (choices.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            var id = ChoiceId(choice);
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return string.Empty;
    }

    private static string ChoiceId(JsonElement choice)
    {
        return choice.ValueKind switch
        {
            JsonValueKind.String => choice.GetString() ?? string.Empty,
            JsonValueKind.Object => FirstNonEmpty(
                ReadString(choice, "id"),
                ReadString(choice, "choiceId"),
                ReadString(choice, "objectId")),
            _ => string.Empty
        };
    }

    private static int NumericKey(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : int.MaxValue;
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
