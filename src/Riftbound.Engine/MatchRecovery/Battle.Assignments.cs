using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static bool HasBattleDamageAssignmentPendingFields(object? damageAssignmentPayload)
    {
        return TryReadObjectValue(damageAssignmentPayload, "phase", out _)
            || TryReadObjectValue(damageAssignmentPayload, "battleId", out _)
            || TryReadObjectValue(damageAssignmentPayload, "battlefieldId", out _)
            || TryReadObjectValue(damageAssignmentPayload, "assigningPlayerId", out _)
            || TryReadObjectValue(damageAssignmentPayload, "damagePool", out _)
            || TryReadObjectValue(damageAssignmentPayload, "legalTargets", out _)
            || TryReadObjectValue(damageAssignmentPayload, "existingDamage", out _)
            || TryReadObjectValue(damageAssignmentPayload, "lethalDamageThreshold", out _)
            || TryReadObjectValue(damageAssignmentPayload, "requiredAssignments", out _);
    }

    private static void ValidateBattleDamageAssignmentPendingPayloadValues(
        object? damageAssignmentPayload,
        string payloadLabel,
        bool shouldValidatePendingFields,
        List<string> errors)
    {
        if (!shouldValidatePendingFields)
        {
            return;
        }

        ValidateSnapshotPayloadRequiredStringValue(
            damageAssignmentPayload,
            "phase",
            payloadLabel,
            "phase",
            errors,
            value => string.Equals(value, "DAMAGE_ASSIGNMENT", StringComparison.Ordinal));
        ValidateSnapshotPayloadRequiredStringValue(
            damageAssignmentPayload,
            "battleId",
            payloadLabel,
            "battle id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            damageAssignmentPayload,
            "battlefieldId",
            payloadLabel,
            "battlefield id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            damageAssignmentPayload,
            "assigningPlayerId",
            payloadLabel,
            "assigning player id",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntMapValues(
            damageAssignmentPayload,
            "damagePool",
            payloadLabel,
            "damage pool",
            errors);
        ValidateSnapshotPayloadRequiredStringListDictionaryValues(
            damageAssignmentPayload,
            "legalTargets",
            payloadLabel,
            "legal targets",
            "legal target object id",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntMapValues(
            damageAssignmentPayload,
            "existingDamage",
            payloadLabel,
            "existing damage",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntMapValues(
            damageAssignmentPayload,
            "lethalDamageThreshold",
            payloadLabel,
            "lethal damage threshold",
            errors);
        ValidateBattleDamageAssignmentRequiredAssignmentValues(
            damageAssignmentPayload,
            $"{payloadLabel} required assignment",
            errors);
    }

    private static void ValidateBattleDamageAssignmentPlayerReferences(
        object? damageAssignmentPayload,
        string payloadLabel,
        IReadOnlySet<string>? knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        ValidateTimingOptionalPlayerReference(
            damageAssignmentPayload,
            "assigningPlayerId",
            $"{payloadLabel} assigning player id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
    }

    private static void ValidateBattleDamageAssignmentIdentityConsistency(
        object? battlePayload,
        object? damageAssignmentPayload,
        string payloadLabel,
        bool shouldValidatePendingFields,
        List<string> errors)
    {
        if (!shouldValidatePendingFields)
        {
            return;
        }

        ValidateBattleDamageAssignmentIdentityConsistency(
            battlePayload,
            "battleId",
            "battle id",
            damageAssignmentPayload,
            "battleId",
            "battle id",
            payloadLabel,
            errors);
        ValidateBattleDamageAssignmentIdentityConsistency(
            battlePayload,
            "battlefieldObjectId",
            "battlefield object id",
            damageAssignmentPayload,
            "battlefieldId",
            "battlefield object id",
            payloadLabel,
            errors);
    }

    private static void ValidateBattleDamageAssignmentIdentityConsistency(
        object? battlePayload,
        string battleKey,
        string battleLabel,
        object? damageAssignmentPayload,
        string damageAssignmentKey,
        string damageAssignmentLabel,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectString(battlePayload, battleKey, out var battleValue)
            || string.IsNullOrWhiteSpace(battleValue)
            || !TryReadObjectString(damageAssignmentPayload, damageAssignmentKey, out var damageAssignmentValue)
            || string.IsNullOrWhiteSpace(damageAssignmentValue))
        {
            return;
        }

        var normalizedBattleValue = battleValue.Trim();
        var normalizedDamageAssignmentValue = damageAssignmentValue.Trim();
        if (!string.Equals(normalizedDamageAssignmentValue, normalizedBattleValue, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {damageAssignmentLabel} {normalizedDamageAssignmentValue} does not match enclosing battle {battleLabel} {normalizedBattleValue}; {FormatExpectedActualForRecovery(normalizedBattleValue, normalizedDamageAssignmentValue)}");
        }
    }

    private static void ValidateBattleDamageAssignmentParticipantMembership(
        object? battlePayload,
        object? damageAssignmentPayload,
        string payloadLabel,
        bool shouldValidatePendingFields,
        List<string> errors)
    {
        if (!shouldValidatePendingFields
            || !TryBuildBattleParticipantObjectIdSet(battlePayload, out var participantObjectIds))
        {
            return;
        }

        ValidateBattleDamageAssignmentParticipantIntMapKeys(
            damageAssignmentPayload,
            "damagePool",
            $"{payloadLabel} damage pool source object id",
            participantObjectIds,
            errors);
        ValidateBattleDamageAssignmentLegalTargetParticipants(
            damageAssignmentPayload,
            participantObjectIds,
            payloadLabel,
            errors);
        ValidateBattleDamageAssignmentParticipantIntMapKeys(
            damageAssignmentPayload,
            "existingDamage",
            $"{payloadLabel} existing damage object id",
            participantObjectIds,
            errors);
        ValidateBattleDamageAssignmentParticipantIntMapKeys(
            damageAssignmentPayload,
            "lethalDamageThreshold",
            $"{payloadLabel} lethal damage threshold object id",
            participantObjectIds,
            errors);
        ValidateBattleDamageAssignmentRequiredAssignmentParticipantMembership(
            damageAssignmentPayload,
            participantObjectIds,
            payloadLabel,
            errors);
    }

    private static bool TryBuildBattleParticipantObjectIdSet(
        object? battlePayload,
        out IReadOnlySet<string> participantObjectIds)
    {
        if (!TryReadObjectStringList(battlePayload, "attackerObjectIds", out var attackerObjectIds)
            || !TryReadObjectStringList(battlePayload, "defenderObjectIds", out var defenderObjectIds))
        {
            participantObjectIds = new HashSet<string>(StringComparer.Ordinal);
            return false;
        }

        participantObjectIds = attackerObjectIds
            .Concat(defenderObjectIds)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Select(objectId => objectId.Trim())
            .ToHashSet(StringComparer.Ordinal);
        return true;
    }

    private static void ValidateBattleDamageAssignmentParticipantIntMapKeys(
        object? damageAssignmentPayload,
        string payloadKey,
        string itemLabel,
        IReadOnlySet<string> participantObjectIds,
        List<string> errors)
    {
        if (!TryReadObjectIntDictionary(damageAssignmentPayload, payloadKey, out var entries))
        {
            return;
        }

        foreach (var objectId in entries.Keys)
        {
            ValidateBattleDamageAssignmentParticipantObjectId(
                objectId,
                itemLabel,
                participantObjectIds,
                errors);
        }
    }

    private static void ValidateBattleDamageAssignmentLegalTargetParticipants(
        object? damageAssignmentPayload,
        IReadOnlySet<string> participantObjectIds,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectStringListDictionary(damageAssignmentPayload, "legalTargets", out var legalTargets))
        {
            return;
        }

        foreach (var (sourceObjectId, legalTargetObjectIds) in legalTargets)
        {
            ValidateBattleDamageAssignmentParticipantObjectId(
                sourceObjectId,
                $"{payloadLabel} legal targets source object id",
                participantObjectIds,
                errors);
            foreach (var legalTargetObjectId in legalTargetObjectIds)
            {
                ValidateBattleDamageAssignmentParticipantObjectId(
                    legalTargetObjectId,
                    $"{payloadLabel} legal targets legal target object id",
                    participantObjectIds,
                    errors);
            }
        }
    }

    private static void ValidateBattleDamageAssignmentRequiredAssignmentParticipantMembership(
        object? damageAssignmentPayload,
        IReadOnlySet<string> participantObjectIds,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectList(damageAssignmentPayload, "requiredAssignments", out var requiredAssignments))
        {
            return;
        }

        foreach (var requiredAssignment in requiredAssignments)
        {
            if (!IsSnapshotPlayerPayloadObject(requiredAssignment))
            {
                continue;
            }

            if (TryReadObjectString(requiredAssignment, "sourceObjectId", out var sourceObjectId))
            {
                ValidateBattleDamageAssignmentParticipantObjectId(
                    sourceObjectId,
                    $"{payloadLabel} required assignment item source object id",
                    participantObjectIds,
                    errors);
            }

            if (!TryReadObjectStringList(requiredAssignment, "legalTargetObjectIds", out var legalTargetObjectIds))
            {
                continue;
            }

            foreach (var legalTargetObjectId in legalTargetObjectIds)
            {
                ValidateBattleDamageAssignmentParticipantObjectId(
                    legalTargetObjectId,
                    $"{payloadLabel} required assignment item legal target object id",
                    participantObjectIds,
                    errors);
            }
        }
    }

    private static void ValidateBattleDamageAssignmentParticipantObjectId(
        string? objectId,
        string itemLabel,
        IReadOnlySet<string> participantObjectIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var normalizedObjectId = objectId.Trim();
        if (!participantObjectIds.Contains(normalizedObjectId))
        {
            var expectedParticipantObjectIds = participantObjectIds
                .OrderBy(participantObjectId => participantObjectId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"{itemLabel} {normalizedObjectId} is not an enclosing battle participant; {FormatExpectedActualForRecovery(expectedParticipantObjectIds, normalizedObjectId)}");
        }
    }

    private static void ValidateBattleDamageAssignmentRequiredAssignmentValues(
        object? damageAssignmentPayload,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(damageAssignmentPayload, "requiredAssignments", out var requiredAssignmentsPayload)
            || IsNullSnapshotPayloadValue(requiredAssignmentsPayload))
        {
            errors.Add($"{payloadLabel} list is required");
            return;
        }

        if (!TryReadObjectListValue(requiredAssignmentsPayload, out var requiredAssignments))
        {
            errors.Add($"{payloadLabel} list payload is required");
            return;
        }

        foreach (var requiredAssignment in requiredAssignments)
        {
            if (!IsSnapshotPlayerPayloadObject(requiredAssignment))
            {
                errors.Add($"{payloadLabel} item payload is required");
                continue;
            }

            ValidateBattleDamageAssignmentRequiredAssignmentItemValues(
                requiredAssignment,
                $"{payloadLabel} item",
                errors);
        }
    }

    private static void ValidateBattleDamageAssignmentRequiredAssignmentItemValues(
        object? requiredAssignmentPayload,
        string payloadLabel,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredStringValue(
            requiredAssignmentPayload,
            "sourceObjectId",
            payloadLabel,
            "source object id",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            requiredAssignmentPayload,
            "damage",
            payloadLabel,
            "damage",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            requiredAssignmentPayload,
            "legalTargetObjectIds",
            payloadLabel,
            "legal target object id",
            errors);
    }

    private static void ValidateBattleDamageAssignmentObjectReferences(
        object? damageAssignmentPayload,
        string payloadLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (knownObjectIds is null)
        {
            return;
        }

        ValidateTimingOptionalObjectReferenceWithExpectedDetails(
            damageAssignmentPayload,
            "battlefieldId",
            $"{payloadLabel} battlefield object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceMapKeysWithExpectedDetails(
            damageAssignmentPayload,
            "damagePool",
            $"{payloadLabel} damage pool source object id",
            IsSnapshotIntMapPayloadObject,
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceStringListDictionaryWithExpectedDetails(
            damageAssignmentPayload,
            "legalTargets",
            $"{payloadLabel} legal targets source object id",
            $"{payloadLabel} legal targets legal target object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceMapKeysWithExpectedDetails(
            damageAssignmentPayload,
            "existingDamage",
            $"{payloadLabel} existing damage object id",
            IsSnapshotIntMapPayloadObject,
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceMapKeysWithExpectedDetails(
            damageAssignmentPayload,
            "lethalDamageThreshold",
            $"{payloadLabel} lethal damage threshold object id",
            IsSnapshotIntMapPayloadObject,
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateBattleDamageAssignmentRequiredAssignmentObjectReferences(
            damageAssignmentPayload,
            payloadLabel,
            knownObjectIds,
            knownObjectLabel,
            errors);
    }

    private static void ValidateBattleDamageAssignmentRequiredAssignmentObjectReferences(
        object? damageAssignmentPayload,
        string payloadLabel,
        IReadOnlySet<string> knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (!TryReadObjectList(damageAssignmentPayload, "requiredAssignments", out var requiredAssignments))
        {
            return;
        }

        foreach (var requiredAssignment in requiredAssignments)
        {
            if (!IsSnapshotPlayerPayloadObject(requiredAssignment))
            {
                continue;
            }

            ValidateTimingOptionalObjectReferenceWithExpectedDetails(
                requiredAssignment,
                "sourceObjectId",
                $"{payloadLabel} required assignment item source object id",
                knownObjectIds,
                knownObjectLabel,
                errors);
            ValidateTimingObjectReferenceListWithExpectedDetails(
                requiredAssignment,
                "legalTargetObjectIds",
                $"{payloadLabel} required assignment item legal target object id",
                knownObjectIds,
                knownObjectLabel,
                errors);
        }
    }

    private static void ValidateTimingOptionalObjectReferenceWithExpectedDetails(
        object? payload,
        string payloadKey,
        string objectLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (knownObjectIds is null
            || !TryReadObjectValue(payload, payloadKey, out var objectIdPayload)
            || IsNullSnapshotPayloadValue(objectIdPayload)
            || !TryReadOptionalStringValue(objectIdPayload, out var objectId)
            || string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        ValidateTimingObjectReferenceWithExpectedDetails(
            objectLabel,
            objectId,
            knownObjectIds,
            knownObjectLabel,
            errors);
    }

    private static void ValidateTimingObjectReferenceMapKeysWithExpectedDetails(
        object? payload,
        string payloadKey,
        string objectLabel,
        Func<object?, bool> isValidMapPayload,
        IReadOnlySet<string> knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, payloadKey, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload)
            || !isValidMapPayload(mapPayload))
        {
            return;
        }

        foreach (var (objectId, _) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            ValidateTimingObjectReferenceWithExpectedDetails(
                objectLabel,
                objectId,
                knownObjectIds,
                knownObjectLabel,
                errors);
        }
    }

    private static void ValidateTimingObjectReferenceStringListDictionaryWithExpectedDetails(
        object? payload,
        string payloadKey,
        string sourceObjectLabel,
        string targetObjectLabel,
        IReadOnlySet<string> knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, payloadKey, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload)
            || !IsSnapshotStringListMapPayloadObject(mapPayload))
        {
            return;
        }

        foreach (var (sourceObjectId, targetObjectIdsPayload) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            ValidateTimingObjectReferenceWithExpectedDetails(
                sourceObjectLabel,
                sourceObjectId,
                knownObjectIds,
                knownObjectLabel,
                errors);

            if (!TryReadStringListValue(targetObjectIdsPayload, out var targetObjectIds))
            {
                continue;
            }

            foreach (var targetObjectId in targetObjectIds)
            {
                ValidateTimingObjectReferenceWithExpectedDetails(
                    targetObjectLabel,
                    targetObjectId,
                    knownObjectIds,
                    knownObjectLabel,
                    errors);
            }
        }
    }

    private static void ValidateTimingObjectReferenceListWithExpectedDetails(
        object? payload,
        string payloadKey,
        string objectLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (knownObjectIds is null
            || !TryReadObjectStringList(payload, payloadKey, out var objectIds))
        {
            return;
        }

        foreach (var objectId in objectIds)
        {
            ValidateTimingObjectReferenceWithExpectedDetails(
                objectLabel,
                objectId,
                knownObjectIds,
                knownObjectLabel,
                errors);
        }
    }

    private static void ValidateTimingObjectReferenceWithExpectedDetails(
        string objectLabel,
        string objectId,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (knownObjectIds is null || string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var normalizedObjectId = objectId.Trim();
        if (!knownObjectIds.Contains(normalizedObjectId))
        {
            var expectedObjectIds = knownObjectIds
                .OrderBy(knownObjectId => knownObjectId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"{objectLabel} {normalizedObjectId} is missing from {knownObjectLabel}; {FormatExpectedActualForRecovery(expectedObjectIds, normalizedObjectId)}");
        }
    }

    private static void ValidateSpectatorBattleDamageAssignmentRequiredAssignmentPayloadPropertyNames(
        object? damageAssignmentPayload,
        List<string> errors)
    {
        if (!TryReadObjectList(damageAssignmentPayload, "requiredAssignments", out var requiredAssignments))
        {
            return;
        }

        foreach (var requiredAssignment in requiredAssignments)
        {
            if (!IsSnapshotPlayerPayloadObject(requiredAssignment))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                requiredAssignment,
                "spectator replay frame timing battle damage assignment required assignment item",
                errors);
        }
    }

    private static void ValidateSpectatorBattleParticipantControllerPayloadPropertyNames(
        object? battlePayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(battlePayload, "participantControllerIds", out var participantControllerIds))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            participantControllerIds,
            "spectator replay frame timing battle participant controllers",
            errors);
    }

    private static void ValidateSpectatorBattleDamageAssignmentMapPayloadPropertyNames(
        object? damageAssignmentPayload,
        List<string> errors)
    {
        ValidateSpectatorBattleDamageAssignmentMapPayloadPropertyNames(
            damageAssignmentPayload,
            "damagePool",
            "damage pool",
            errors);
        ValidateSpectatorBattleDamageAssignmentMapPayloadPropertyNames(
            damageAssignmentPayload,
            "legalTargets",
            "legal targets",
            errors);
        ValidateSpectatorBattleDamageAssignmentMapPayloadPropertyNames(
            damageAssignmentPayload,
            "existingDamage",
            "existing damage",
            errors);
        ValidateSpectatorBattleDamageAssignmentMapPayloadPropertyNames(
            damageAssignmentPayload,
            "lethalDamageThreshold",
            "lethal damage threshold",
            errors);
    }

    private static void ValidateSpectatorBattleDamageAssignmentMapPayloadPropertyNames(
        object? damageAssignmentPayload,
        string payloadKey,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(damageAssignmentPayload, payloadKey, out var mapPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            mapPayload,
            $"spectator replay frame timing battle damage assignment {payloadLabel}",
            errors);
    }

    public static IReadOnlyDictionary<string, string> ExtractSeats(SnapshotDto snapshot)
    {
        var seats = new Dictionary<string, string>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return seats;
        }

        foreach (var (playerId, player) in snapshot.Players)
        {
            if (TryReadSeat(player, out var seat))
            {
                seats[playerId] = seat;
            }
        }

        return seats;
    }

    private static void ValidateSpectatorSnapshotStackItemPayloads(
        SnapshotDto snapshot,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (snapshot.Stack is null)
        {
            return;
        }

        var seenStackItemIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < snapshot.Stack.Count; index++)
        {
            var stackItem = snapshot.Stack[index];
            var stackItemLabel = index < authoritativeState.StackItems.Count
                ? authoritativeState.StackItems[index].StackItemId
                : TryReadObjectString(stackItem, "stackItemId", out var stackItemId)
                    && !string.IsNullOrWhiteSpace(stackItemId)
                    ? stackItemId
                    : $"#{index + 1}";
            if (!IsSnapshotPlayerPayloadObject(stackItem))
            {
                errors.Add($"spectator replay frame snapshot stack item {stackItemLabel} payload is required");
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                stackItem,
                $"spectator replay frame snapshot stack item {stackItemLabel}",
                errors);
            var normalizedStackItemId = ValidateSpectatorSnapshotStackItemPayloadValues(stackItem, stackItemLabel, errors);
            if (normalizedStackItemId is not null && !seenStackItemIds.Add(normalizedStackItemId))
            {
                errors.Add($"spectator replay frame snapshot stack item {normalizedStackItemId} is duplicated");
            }
        }
    }

    private static void ValidateSpectatorSnapshotStackKeyedRequiredStringValue(
        object? stackItem,
        string key,
        string fieldLabel,
        string expected,
        string stackItemId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame snapshot stack item";
        if (!TryReadObjectString(stackItem, key, out var actual))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match authoritative state stack item {fieldLabel} {expected} for stack item id {stackItemId}");
            return;
        }

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match authoritative state stack item {fieldLabel} {expected} for stack item id {stackItemId}");
        }
    }
}
