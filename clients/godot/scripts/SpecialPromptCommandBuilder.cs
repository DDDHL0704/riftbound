using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Riftbound.GodotClient;

public sealed record TriggerPromptItem(string TriggerId, string Label, int ControllerBlockIndex);

public sealed record DamageTargetPromptItem(string TargetObjectId, string Label, int LethalDamageThreshold);

public sealed record DamageAssignmentPromptItem(
    string SourceObjectId,
    string SourceLabel,
    int DamagePool,
    IReadOnlyList<DamageTargetPromptItem> Targets);

public sealed record DamageAssignmentSelection(string SourceObjectId, string TargetObjectId, int Damage);

public static class SpecialPromptCommandBuilder
{
    public static bool TryReadOrderTriggers(
        Godot.Collections.Dictionary action,
        out IReadOnlyList<TriggerPromptItem> triggers,
        out string reason)
    {
        triggers = [];
        reason = string.Empty;
        if (!TryReadCandidateMetadata(action, out var metadata, out reason))
        {
            return false;
        }

        if (!TryReadTriggerIds(metadata, out var orderedTriggerIds, out reason))
        {
            return false;
        }

        if (!metadata.TryGetProperty("triggerChoices", out var triggerChoices)
            || triggerChoices.ValueKind != JsonValueKind.Array)
        {
            reason = "triggerChoices is missing";
            return false;
        }

        if (!TryReadTriggerChoiceLabels(triggerChoices, orderedTriggerIds, out var labelsById, out reason))
        {
            return false;
        }

        if (!TryReadTriggerControllerBlocks(metadata, orderedTriggerIds, out var controllerBlockIndexes, out reason))
        {
            return false;
        }

        var parsed = new List<TriggerPromptItem>(orderedTriggerIds.Length);
        foreach (var triggerId in orderedTriggerIds)
        {
            if (!labelsById.TryGetValue(triggerId, out var label))
            {
                reason = "triggerChoices label is missing";
                return false;
            }

            parsed.Add(new TriggerPromptItem(triggerId, label, controllerBlockIndexes[triggerId]));
        }

        triggers = parsed;
        return true;
    }

    public static bool TryBuildOrderTriggersPayload(
        Godot.Collections.Dictionary action,
        IReadOnlyList<string> orderedTriggerIds,
        out Dictionary<string, object?> payload,
        out string payloadKey,
        out string reason)
    {
        payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        payloadKey = string.Empty;
        if (!TryReadOrderTriggers(action, out var serverTriggers, out reason))
        {
            return false;
        }

        if (!HasSameIdsInDifferentOrder(serverTriggers.Select(trigger => trigger.TriggerId), orderedTriggerIds)
            || !ValidateTriggerBlockOrder(serverTriggers, orderedTriggerIds, out reason))
        {
            reason = string.IsNullOrWhiteSpace(reason)
                ? "ordered trigger selection must preserve every server trigger"
                : reason;
            return false;
        }

        payload["cmdType"] = "ORDER_TRIGGERS";
        payload["orderedTriggerIds"] = orderedTriggerIds.ToArray();
        payload["triggerIds"] = orderedTriggerIds.ToArray();
        payloadKey = "server trigger order";
        return true;
    }

    public static bool TryReadDamageAssignmentPrompt(
        Godot.Collections.Dictionary action,
        out IReadOnlyList<DamageAssignmentPromptItem> assignments,
        out string reason)
    {
        assignments = [];
        reason = string.Empty;
        if (!TryReadCandidateMetadata(action, out var metadata, out reason))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ReadString(metadata, "battleId")))
        {
            reason = "battleId is missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FirstNonEmpty(ReadString(metadata, "battlefieldId"), ReadString(metadata, "battlefieldObjectId"))))
        {
            reason = "battlefieldId is missing";
            return false;
        }

        return TryReadDamageAssignmentPromptMetadata(metadata, out assignments, out reason);
    }

    public static bool TryBuildDamageAssignmentPayload(
        Godot.Collections.Dictionary action,
        IReadOnlyList<DamageAssignmentSelection> selections,
        out Dictionary<string, object?> payload,
        out string payloadKey,
        out string reason)
    {
        payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        payloadKey = string.Empty;
        if (!TryReadDamageAssignmentPrompt(action, out var serverAssignments, out reason)
            || !TryReadCandidateMetadata(action, out var metadata, out reason))
        {
            return false;
        }

        if (!ValidateDamageAssignments(serverAssignments, selections, out var orderedSelections, out reason))
        {
            return false;
        }

        payload["cmdType"] = "ASSIGN_COMBAT_DAMAGE";
        payload["battleId"] = ReadString(metadata, "battleId");
        payload["battlefieldId"] = FirstNonEmpty(ReadString(metadata, "battlefieldId"), ReadString(metadata, "battlefieldObjectId"));
        payload["assignments"] = orderedSelections
            .Select(selection => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sourceObjectId"] = selection.SourceObjectId,
                ["targetObjectId"] = selection.TargetObjectId,
                ["damage"] = selection.Damage
            })
            .ToList();
        payloadKey = "server damage assignments";
        return true;
    }

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

        reason = "damage assignment requires explicit overlay selections";
        return false;
    }

    private static JsonElement? MetadataElement(JsonElement candidate)
    {
        return candidate.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            ? metadata
            : null;
    }

    private static bool TryReadCandidateMetadata(
        Godot.Collections.Dictionary action,
        out JsonElement metadata,
        out string reason)
    {
        metadata = default;
        reason = string.Empty;
        var candidateJson = action.TryGetValue("candidateJson", out var candidateValue)
            ? candidateValue.AsString()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(candidateJson))
        {
            reason = "candidate JSON is missing";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(candidateJson);
            if (MetadataElement(document.RootElement) is not { } parsedMetadata)
            {
                reason = "metadata is missing";
                return false;
            }

            metadata = parsedMetadata.Clone();
            return true;
        }
        catch (JsonException ex)
        {
            reason = $"malformed candidate JSON: {ex.Message}";
            return false;
        }
    }

    private static bool TryReadTriggerIds(
        JsonElement metadata,
        out string[] orderedTriggerIds,
        out string reason)
    {
        orderedTriggerIds = [];
        reason = string.Empty;
        string[]? selectedIds = null;
        foreach (var propertyName in new[] { "orderedTriggerIds", "triggerIds" })
        {
            if (!metadata.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (!TryReadUniqueTriggerIds(property, propertyName, out var ids, out reason))
            {
                return false;
            }

            if (selectedIds is not null
                && !selectedIds.ToHashSet(StringComparer.Ordinal).SetEquals(ids))
            {
                reason = "orderedTriggerIds and triggerIds do not exactly cover the same triggers";
                return false;
            }

            selectedIds ??= ids;
        }

        if (selectedIds is null)
        {
            reason = "orderedTriggerIds is missing";
            return false;
        }

        orderedTriggerIds = selectedIds;
        return true;
    }

    private static bool TryReadUniqueTriggerIds(
        JsonElement value,
        string fieldName,
        out string[] triggerIds,
        out string reason)
    {
        triggerIds = [];
        reason = string.Empty;
        if (value.ValueKind != JsonValueKind.Array)
        {
            reason = $"{fieldName} is malformed";
            return false;
        }

        if (value.GetArrayLength() == 0)
        {
            reason = $"{fieldName} is empty";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var parsed = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            var id = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                reason = $"{fieldName} contains an empty trigger id";
                return false;
            }

            if (!seen.Add(id))
            {
                reason = $"{fieldName} duplicates a trigger id";
                return false;
            }

            parsed.Add(id);
        }

        triggerIds = parsed.ToArray();
        return true;
    }

    private static bool TryReadTriggerChoiceLabels(
        JsonElement triggerChoices,
        IReadOnlyList<string> orderedTriggerIds,
        out Dictionary<string, string> labelsById,
        out string reason)
    {
        labelsById = new Dictionary<string, string>(StringComparer.Ordinal);
        reason = string.Empty;
        if (triggerChoices.GetArrayLength() == 0)
        {
            reason = "triggerChoices is empty";
            return false;
        }

        foreach (var choice in triggerChoices.EnumerateArray())
        {
            if (choice.ValueKind != JsonValueKind.Object)
            {
                reason = "triggerChoices entry is malformed";
                return false;
            }

            var id = ChoiceId(choice);
            var label = ReadString(choice, "label");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))
            {
                reason = "triggerChoices id or label is missing";
                return false;
            }

            if (!labelsById.TryAdd(id, label))
            {
                reason = "triggerChoices duplicates a trigger id";
                return false;
            }
        }

        if (!labelsById.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(orderedTriggerIds))
        {
            reason = "triggerChoices does not exactly cover server triggers";
            return false;
        }

        return true;
    }

    private static bool HasSameIdsInDifferentOrder(
        IEnumerable<string> serverIds,
        IReadOnlyList<string> submittedIds)
    {
        var expected = serverIds.ToArray();
        if (expected.Length != submittedIds.Count)
        {
            return false;
        }

        return expected.GroupBy(id => id, StringComparer.Ordinal)
            .All(group => submittedIds.Count(id => string.Equals(id, group.Key, StringComparison.Ordinal)) == group.Count());
    }

    private static bool TryReadTriggerControllerBlocks(
        JsonElement metadata,
        IReadOnlyList<string> orderedTriggerIds,
        out IReadOnlyDictionary<string, int> ControllerBlockIndex,
        out string reason)
    {
        ControllerBlockIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        reason = string.Empty;
        if (!metadata.TryGetProperty("legalOrderingConstraints", out var constraints)
            || constraints.ValueKind != JsonValueKind.Object)
        {
            reason = "legalOrderingConstraints is missing";
            return false;
        }

        if (ReadBool(constraints, "crossControllerReorderingAllowed", true)
            || !ReadBool(constraints, "preserveControllerBlocks", false)
            || !ReadBool(constraints, "withinControllerReorderingAllowed", false))
        {
            reason = "legalOrderingConstraints block guards are missing";
            return false;
        }

        if (!constraints.TryGetProperty("legalResolutionControllerBlockOrder", out var blocks)
            || blocks.ValueKind != JsonValueKind.Array
            || blocks.GetArrayLength() == 0)
        {
            reason = "legalResolutionControllerBlockOrder is missing";
            return false;
        }

        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var blockIndex = 0;
        foreach (var block in blocks.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object)
            {
                reason = "legalResolutionControllerBlockOrder block is malformed";
                return false;
            }

            if (!block.TryGetProperty("triggerIds", out var blockTriggerIds)
                || !TryReadUniqueTriggerIds(
                    blockTriggerIds,
                    "legalResolutionControllerBlockOrder triggerIds",
                    out var triggerIds,
                    out reason))
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = "legalResolutionControllerBlockOrder triggerIds is missing";
                }

                return false;
            }

            foreach (var triggerId in triggerIds)
            {
                if (!indexes.TryAdd(triggerId, blockIndex))
                {
                    reason = "legalResolutionControllerBlockOrder duplicates a trigger";
                    return false;
                }
            }

            blockIndex++;
        }

        if (indexes.Count != orderedTriggerIds.Count
            || !indexes.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(orderedTriggerIds))
        {
            reason = "legalResolutionControllerBlockOrder does not preserve server triggers";
            return false;
        }

        ControllerBlockIndex = indexes;
        return true;
    }

    private static bool ValidateTriggerBlockOrder(
        IReadOnlyList<TriggerPromptItem> serverTriggers,
        IReadOnlyList<string> submittedIds,
        out string reason)
    {
        reason = string.Empty;
        var blockById = serverTriggers.ToDictionary(trigger => trigger.TriggerId, trigger => trigger.ControllerBlockIndex, StringComparer.Ordinal);
        var previousBlock = -1;
        foreach (var triggerId in submittedIds)
        {
            if (!blockById.TryGetValue(triggerId, out var blockIndex) || blockIndex < previousBlock)
            {
                reason = "ordered trigger selection must preserve server controller blocks";
                return false;
            }

            previousBlock = blockIndex;
        }

        return true;
    }

    private static bool TryReadDamageAssignmentPromptMetadata(
        JsonElement metadata,
        out IReadOnlyList<DamageAssignmentPromptItem> assignments,
        out string reason)
    {
        assignments = [];
        reason = string.Empty;
        var damagePool = FirstPositiveIntMap(metadata, "assignableDamagePool", "damagePool");
        if (damagePool.Count == 0)
        {
            reason = "assignableDamagePool or damagePool is missing";
            return false;
        }

        if (!metadata.TryGetProperty("requiredAssignments", out var requiredAssignments)
            || requiredAssignments.ValueKind != JsonValueKind.Array
            || requiredAssignments.GetArrayLength() == 0)
        {
            reason = "requiredAssignments is missing";
            return false;
        }

        if (!metadata.TryGetProperty("assignmentChoices", out var assignmentChoices)
            || assignmentChoices.ValueKind != JsonValueKind.Array)
        {
            reason = "assignmentChoices is missing";
            return false;
        }

        if (!metadata.TryGetProperty("battleParticipants", out var battleParticipants)
            || battleParticipants.ValueKind != JsonValueKind.Array)
        {
            reason = "battleParticipants is missing";
            return false;
        }

        var lethalDamageThreshold = ReadNonNegativeIntMap(metadata, "lethalDamageThreshold");
        if (lethalDamageThreshold.Count == 0)
        {
            reason = "lethalDamageThreshold is missing";
            return false;
        }

        if (!TryReadParticipantLabels(battleParticipants, out var participantLabels, out reason)
            || !TryReadAssignmentChoicePairs(assignmentChoices, out var allowedPairs, out reason))
        {
            return false;
        }

        if (!TryReadLegalTargetsMap(metadata, out var legalTargetsBySource, out var hasLegalTargets, out reason))
        {
            return false;
        }

        var parsed = new List<DamageAssignmentPromptItem>();
        var seenSources = new HashSet<string>(StringComparer.Ordinal);
        var expectedPairs = new HashSet<(string Source, string Target)>();
        foreach (var requirement in requiredAssignments.EnumerateArray())
        {
            if (requirement.ValueKind != JsonValueKind.Object)
            {
                reason = "requiredAssignments entry is malformed";
                return false;
            }

            var sourceObjectId = ReadString(requirement, "sourceObjectId");
            var requiredDamage = ReadInt(requirement, "damage");
            var legalTargetObjectIds = ReadStringArray(requirement, "legalTargetObjectIds");
            legalTargetsBySource.TryGetValue(sourceObjectId, out var metadataTargets);

            if (string.IsNullOrWhiteSpace(sourceObjectId)
                || requiredDamage <= 0
                || legalTargetObjectIds.Count == 0
                || !seenSources.Add(sourceObjectId))
            {
                reason = "requiredAssignments source, damage, or legalTargetObjectIds is missing";
                return false;
            }

            if (legalTargetObjectIds.Distinct(StringComparer.Ordinal).Count() != legalTargetObjectIds.Count)
            {
                reason = "legalTargetObjectIds contains a duplicate target";
                return false;
            }

            if (!damagePool.TryGetValue(sourceObjectId, out var poolDamage) || poolDamage != requiredDamage)
            {
                reason = "requiredAssignments damage does not match assignableDamagePool";
                return false;
            }

            if (!participantLabels.TryGetValue(sourceObjectId, out var sourceLabel))
            {
                reason = "battleParticipants source is missing";
                return false;
            }

            if (metadataTargets is not null && !legalTargetObjectIds.SequenceEqual(metadataTargets, StringComparer.Ordinal))
            {
                reason = "legalTargets does not match requiredAssignments order";
                return false;
            }

            var targets = new List<DamageTargetPromptItem>(legalTargetObjectIds.Count);
            foreach (var targetObjectId in legalTargetObjectIds)
            {
                expectedPairs.Add((sourceObjectId, targetObjectId));
                if (!allowedPairs.Contains((sourceObjectId, targetObjectId)))
                {
                    reason = "assignmentChoices target is missing";
                    return false;
                }

                if (!participantLabels.TryGetValue(targetObjectId, out var targetLabel))
                {
                    reason = "battleParticipants target is missing";
                    return false;
                }

                if (!lethalDamageThreshold.TryGetValue(targetObjectId, out var threshold))
                {
                    reason = "lethalDamageThreshold target is missing";
                    return false;
                }

                targets.Add(new DamageTargetPromptItem(targetObjectId, targetLabel, threshold));
            }

            parsed.Add(new DamageAssignmentPromptItem(sourceObjectId, sourceLabel, poolDamage, targets));
        }

        if (!allowedPairs.SetEquals(expectedPairs))
        {
            reason = "assignmentChoices does not exactly cover required legal targets";
            return false;
        }

        if (hasLegalTargets
            && (!legalTargetsBySource.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(seenSources)
                || parsed.Any(assignment => !legalTargetsBySource[assignment.SourceObjectId]
                    .SequenceEqual(assignment.Targets.Select(target => target.TargetObjectId), StringComparer.Ordinal))))
        {
            reason = "legalTargets does not exactly cover required legal targets";
            return false;
        }

        assignments = parsed;
        return true;
    }

    private static bool ValidateDamageAssignments(
        IReadOnlyList<DamageAssignmentPromptItem> serverAssignments,
        IReadOnlyList<DamageAssignmentSelection> selections,
        out IReadOnlyList<DamageAssignmentSelection> orderedSelections,
        out string reason)
    {
        orderedSelections = [];
        reason = string.Empty;
        var byPair = new Dictionary<(string Source, string Target), int>();
        foreach (var selection in selections)
        {
            if (selection.Damage <= 0)
            {
                reason = "damage assignment must be a positive integer";
                return false;
            }

            var source = serverAssignments.FirstOrDefault(item => string.Equals(item.SourceObjectId, selection.SourceObjectId, StringComparison.Ordinal));
            if (source is null || !source.Targets.Any(target => string.Equals(target.TargetObjectId, selection.TargetObjectId, StringComparison.Ordinal)))
            {
                reason = "damage assignment is not a server-provided choice";
                return false;
            }

            var key = (selection.SourceObjectId, selection.TargetObjectId);
            byPair[key] = byPair.GetValueOrDefault(key) + selection.Damage;
        }

        var ordered = new List<DamageAssignmentSelection>();
        foreach (var source in serverAssignments)
        {
            var sourceDamage = 0;
            for (var targetIndex = 0; targetIndex < source.Targets.Count; targetIndex++)
            {
                var target = source.Targets[targetIndex];
                var damage = byPair.GetValueOrDefault((source.SourceObjectId, target.TargetObjectId));
                sourceDamage += damage;
                var isLastTarget = targetIndex == source.Targets.Count - 1;
                if (!isLastTarget && damage > target.LethalDamageThreshold)
                {
                    reason = "damage assignment exceeds a server lethalDamageThreshold";
                    return false;
                }

                if (!isLastTarget && damage < target.LethalDamageThreshold
                    && source.Targets.Skip(targetIndex + 1).Any(later => byPair.GetValueOrDefault((source.SourceObjectId, later.TargetObjectId)) > 0))
                {
                    reason = "damage assignment must satisfy server target order and lethalDamageThreshold";
                    return false;
                }

                if (damage > 0)
                {
                    ordered.Add(new DamageAssignmentSelection(source.SourceObjectId, target.TargetObjectId, damage));
                }
            }

            if (sourceDamage != source.DamagePool)
            {
                reason = "damage assignment must allocate the full server damage pool";
                return false;
            }
        }

        orderedSelections = ordered;
        return true;
    }

    private static bool TryReadParticipantLabels(
        JsonElement battleParticipants,
        out Dictionary<string, string> labels,
        out string reason)
    {
        labels = new Dictionary<string, string>(StringComparer.Ordinal);
        reason = string.Empty;
        var roleCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var participant in battleParticipants.EnumerateArray())
        {
            if (participant.ValueKind != JsonValueKind.Object)
            {
                reason = "battleParticipants entry is malformed";
                return false;
            }

            var objectId = ReadString(participant, "objectId");
            if (string.IsNullOrWhiteSpace(objectId))
            {
                reason = "battleParticipants objectId is missing";
                return false;
            }

            var role = ReadString(participant, "role");
            if (string.IsNullOrWhiteSpace(role))
            {
                reason = "battleParticipants role is missing";
                return false;
            }

            if (!TryReadNonNegativeInt(participant, "power", out var power))
            {
                reason = "battleParticipants power is missing";
                return false;
            }

            if (!TryReadNonNegativeInt(participant, "damage", out var damage))
            {
                reason = "battleParticipants damage is missing";
                return false;
            }

            if (labels.ContainsKey(objectId))
            {
                reason = "battleParticipants duplicates an objectId";
                return false;
            }

            roleCounts[role] = roleCounts.GetValueOrDefault(role) + 1;
            labels[objectId] = $"{FriendlyParticipantRole(role)} {roleCounts[role]} · 战力 {power} · 已受伤 {damage}";
        }

        return labels.Count > 0;
    }

    private static bool TryReadAssignmentChoicePairs(
        JsonElement assignmentChoices,
        out HashSet<(string Source, string Target)> pairs,
        out string reason)
    {
        pairs = new HashSet<(string Source, string Target)>();
        reason = string.Empty;
        foreach (var choice in assignmentChoices.EnumerateArray())
        {
            if (choice.ValueKind != JsonValueKind.Object)
            {
                reason = "assignmentChoices pair is missing";
                return false;
            }

            if (!TryReadAssignmentChoicePair(choice, out var sourceObjectId, out var targetObjectId))
            {
                reason = "assignmentChoices pair is missing";
                return false;
            }

            if (!pairs.Add((sourceObjectId, targetObjectId)))
            {
                reason = "assignmentChoices duplicates a source-target pair";
                return false;
            }
        }

        return pairs.Count > 0;
    }

    private static bool TryReadAssignmentChoicePair(
        JsonElement choice,
        out string sourceObjectId,
        out string targetObjectId)
    {
        sourceObjectId = ReadString(choice, "sourceObjectId");
        targetObjectId = ReadString(choice, "targetObjectId");
        if (!string.IsNullOrWhiteSpace(sourceObjectId) && !string.IsNullOrWhiteSpace(targetObjectId))
        {
            return true;
        }

        var parts = ReadString(choice, "id").Split("->", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        sourceObjectId = parts[0];
        targetObjectId = parts[1];
        return true;
    }

    private static string FriendlyParticipantRole(string role)
    {
        return role.ToUpperInvariant() switch
        {
            "ATTACKER" => "攻击单位",
            "DEFENDER" or "BLOCKER" => "阻挡单位",
            _ => "战斗单位"
        };
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

    private static IReadOnlyDictionary<string, int> FirstPositiveIntMap(JsonElement metadata, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var values = ReadPositiveIntMap(metadata, propertyName);
            if (values.Count > 0)
            {
                return values;
            }
        }

        return new Dictionary<string, int>(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> ReadPositiveIntMap(JsonElement metadata, string propertyName)
    {
        return ReadIntMap(metadata, propertyName, value => value > 0);
    }

    private static IReadOnlyDictionary<string, int> ReadNonNegativeIntMap(JsonElement metadata, string propertyName)
    {
        return ReadIntMap(metadata, propertyName, value => value >= 0);
    }

    private static IReadOnlyDictionary<string, int> ReadIntMap(
        JsonElement metadata,
        string propertyName,
        Func<int, bool> includeValue)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!metadata.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var item in property.EnumerateObject())
        {
            if (!TryReadInt(item.Value, out var value))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.Name) && includeValue(value))
            {
                result[item.Name] = value;
            }
        }

        return result;
    }

    private static bool TryReadLegalTargetsMap(
        JsonElement metadata,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> legalTargetsBySource,
        out bool hasLegalTargets,
        out string reason)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        legalTargetsBySource = result;
        hasLegalTargets = metadata.TryGetProperty("legalTargets", out var property);
        reason = string.Empty;
        if (!hasLegalTargets)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            reason = "legalTargets is malformed";
            return false;
        }

        foreach (var item in property.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                reason = "legalTargets contains an empty source";
                return false;
            }

            if (result.ContainsKey(item.Name))
            {
                reason = "legalTargets duplicates a source";
                return false;
            }

            if (item.Value.ValueKind != JsonValueKind.Array)
            {
                reason = "legalTargets target list is malformed";
                return false;
            }

            if (item.Value.GetArrayLength() == 0)
            {
                reason = "legalTargets target list is empty";
                return false;
            }

            var seenTargets = new HashSet<string>(StringComparer.Ordinal);
            var targets = new List<string>();
            foreach (var target in item.Value.EnumerateArray())
            {
                var targetObjectId = target.ValueKind == JsonValueKind.String ? target.GetString() : null;
                if (string.IsNullOrWhiteSpace(targetObjectId))
                {
                    reason = "legalTargets contains an empty target";
                    return false;
                }

                if (!seenTargets.Add(targetObjectId))
                {
                    reason = "legalTargets contains a duplicate target";
                    return false;
                }

                targets.Add(targetObjectId);
            }

            result[item.Name] = targets;
        }

        if (result.Count == 0)
        {
            reason = "legalTargets is empty";
            return false;
        }

        legalTargetsBySource = result;
        return true;
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

    private static bool TryReadNonNegativeInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
            && TryReadInt(property, out value)
            && value >= 0;
    }

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            value = number;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out number))
        {
            value = number;
            return true;
        }

        return false;
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool fallback)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : fallback;
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
