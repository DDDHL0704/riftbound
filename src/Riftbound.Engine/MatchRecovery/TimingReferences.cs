using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateTimingPlayerReferenceList(
        object? payload,
        string payloadKey,
        string playerLabel,
        IReadOnlySet<string>? knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        if (knownPlayerIds is null)
        {
            return;
        }

        if (!TryReadObjectStringList(payload, payloadKey, out var playerIds))
        {
            return;
        }

        foreach (var playerId in playerIds)
        {
            ValidateTimingPlayerReference(
                playerLabel,
                playerId,
                knownPlayerIds,
                knownPlayerLabel,
                errors);
        }
    }

    private static void ValidateTimingPlayerReference(
        string playerLabel,
        string playerId,
        IReadOnlySet<string> knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        var normalizedPlayerId = playerId.Trim();
        if (!knownPlayerIds.Contains(normalizedPlayerId))
        {
            var expectedPlayerIds = knownPlayerIds
                .OrderBy(knownPlayerId => knownPlayerId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"{playerLabel} {normalizedPlayerId} is missing from {knownPlayerLabel}; {FormatExpectedActualForRecovery(expectedPlayerIds, normalizedPlayerId)}");
        }
    }

    private static void ValidateTimingObjectReferenceList(
        object? effectPayload,
        string payloadKey,
        string objectLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (knownObjectIds is null)
        {
            return;
        }

        if (!TryReadObjectStringList(effectPayload, payloadKey, out var objectIds))
        {
            return;
        }

        foreach (var objectId in objectIds)
        {
            ValidateTimingObjectReference(
                objectLabel,
                objectId,
                knownObjectIds,
                knownObjectLabel,
                errors);
        }
    }

    private static void ValidateBattlefieldTaskParticipantObjectBattlefieldMembership(
        object? taskPayload,
        string participantObjectLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        List<string> errors)
    {
        if (objectLocations is null
            || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
            || string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !TryReadObjectStringList(taskPayload, "participantObjectIds", out var participantObjectIds))
        {
            return;
        }

        var normalizedBattlefieldObjectId = battlefieldObjectId.Trim();
        var seenParticipantObjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participantObjectId in participantObjectIds)
        {
            if (string.IsNullOrWhiteSpace(participantObjectId))
            {
                continue;
            }

            var normalizedParticipantObjectId = participantObjectId.Trim();
            if (!seenParticipantObjectIds.Add(normalizedParticipantObjectId))
            {
                continue;
            }

            if (!objectLocations.TryGetValue(normalizedParticipantObjectId, out var location))
            {
                var detail = FormatExpectedActualForRecovery(
                    $"contains {normalizedParticipantObjectId}",
                    FormatObjectIdsForRecovery(objectLocations.Keys));
                errors.Add(
                    $"{participantObjectLabel} {normalizedParticipantObjectId} is missing from object locations; {detail}");
                continue;
            }

            if (!string.Equals(location.Zone?.Trim(), "BATTLEFIELD", StringComparison.Ordinal)
                || !string.Equals(
                    location.BattlefieldObjectId?.Trim() ?? string.Empty,
                    normalizedBattlefieldObjectId,
                    StringComparison.Ordinal))
            {
                var expectedLocation = FormatBattlefieldTaskLocationForRecovery("BATTLEFIELD", normalizedBattlefieldObjectId);
                var actualLocation = FormatBattlefieldTaskLocationForRecovery(location.Zone, location.BattlefieldObjectId);
                var detail = FormatExpectedActualForRecovery(expectedLocation, actualLocation);
                errors.Add(
                    $"{participantObjectLabel} {normalizedParticipantObjectId} is not located at battlefield object id {normalizedBattlefieldObjectId}; {detail}");
            }
        }
    }

    private static string FormatBattlefieldTaskLocationForRecovery(string? zone, string? battlefieldObjectId)
    {
        var normalizedZone = string.IsNullOrWhiteSpace(zone) ? string.Empty : zone.Trim();
        var normalizedBattlefieldObjectId = string.IsNullOrWhiteSpace(battlefieldObjectId) ? string.Empty : battlefieldObjectId.Trim();
        return $"{FormatRecoveryDiagnosticValue(normalizedZone)} @ {FormatRecoveryDiagnosticValue(normalizedBattlefieldObjectId)}";
    }

    private static void ValidateBattlefieldTaskBattlefieldObjectCardMembership(
        object? taskPayload,
        string battlefieldObjectLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        List<string> errors)
    {
        if (objectTags is null
            || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
            || string.IsNullOrWhiteSpace(battlefieldObjectId))
        {
            return;
        }

        var normalizedBattlefieldObjectId = battlefieldObjectId.Trim();
        if (!objectTags.TryGetValue(normalizedBattlefieldObjectId, out var tags))
        {
            errors.Add(
                $"{battlefieldObjectLabel} {normalizedBattlefieldObjectId} is missing from object tags; {FormatObjectTagPresenceForRecovery(objectTags, normalizedBattlefieldObjectId)}");
            return;
        }

        if (!tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag))
        {
            errors.Add($"{battlefieldObjectLabel} {normalizedBattlefieldObjectId} is not a battlefield card");
        }
    }

    private static void ValidateBattlefieldTaskBattlefieldStateMembership(
        object? taskPayload,
        string battlefieldObjectLabel,
        IReadOnlySet<string>? battlefieldStateObjectIds,
        string battlefieldStateLabel,
        List<string> errors)
    {
        if (battlefieldStateObjectIds is null
            || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
            || string.IsNullOrWhiteSpace(battlefieldObjectId))
        {
            return;
        }

        var normalizedBattlefieldObjectId = battlefieldObjectId.Trim();
        if (!battlefieldStateObjectIds.Contains(normalizedBattlefieldObjectId))
        {
            errors.Add(
                $"{battlefieldObjectLabel} {normalizedBattlefieldObjectId} is missing from {battlefieldStateLabel}; {FormatExpectedActualForRecovery(FormatObjectIdsForRecovery(battlefieldStateObjectIds), normalizedBattlefieldObjectId)}");
        }
    }

    private static void ValidateBattlefieldTaskParticipantObjectUnitMembership(
        object? taskPayload,
        string participantObjectLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        List<string> errors)
    {
        if (objectTags is null
            || !TryReadObjectStringList(taskPayload, "participantObjectIds", out var participantObjectIds))
        {
            return;
        }

        var seenParticipantObjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participantObjectId in participantObjectIds)
        {
            if (string.IsNullOrWhiteSpace(participantObjectId))
            {
                continue;
            }

            var normalizedParticipantObjectId = participantObjectId.Trim();
            if (!seenParticipantObjectIds.Add(normalizedParticipantObjectId))
            {
                continue;
            }

            if (!objectTags.TryGetValue(normalizedParticipantObjectId, out var tags))
            {
                errors.Add(
                    $"{participantObjectLabel} {normalizedParticipantObjectId} is missing from object tags; {FormatObjectTagPresenceForRecovery(objectTags, normalizedParticipantObjectId)}");
                continue;
            }

            if (!tags.Contains(CardObjectTags.UnitCard))
            {
                errors.Add($"{participantObjectLabel} {normalizedParticipantObjectId} is not a unit card");
            }
        }
    }

    private static void ValidateBattlefieldTaskParticipantObjectBattlefieldStateOccupantMembership(
        object? taskPayload,
        string participantObjectLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? occupantObjectIdsByBattlefield,
        string occupantObjectLabel,
        List<string> errors)
    {
        if (occupantObjectIdsByBattlefield is null
            || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
            || string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !TryReadObjectStringList(taskPayload, "participantObjectIds", out var participantObjectIds))
        {
            return;
        }

        var normalizedBattlefieldObjectId = battlefieldObjectId.Trim();
        if (!occupantObjectIdsByBattlefield.TryGetValue(normalizedBattlefieldObjectId, out var expectedParticipantObjectIds))
        {
            return;
        }

        var actualParticipantObjectIds = participantObjectIds
            .Where(participantObjectId => !string.IsNullOrWhiteSpace(participantObjectId))
            .Select(participantObjectId => participantObjectId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var actualParticipantObjectId in actualParticipantObjectIds)
        {
            if (!expectedParticipantObjectIds.Contains(actualParticipantObjectId))
            {
                errors.Add(
                    $"{participantObjectLabel} {actualParticipantObjectId} is not in {occupantObjectLabel} for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(FormatObjectIdsForRecovery(expectedParticipantObjectIds), actualParticipantObjectId)}");
            }
        }

        foreach (var expectedParticipantObjectId in expectedParticipantObjectIds)
        {
            if (!actualParticipantObjectIds.Contains(expectedParticipantObjectId))
            {
                errors.Add(
                    $"{participantObjectLabel} {expectedParticipantObjectId} is required by {occupantObjectLabel} for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery($"contains {expectedParticipantObjectId}", FormatObjectIdsForRecovery(actualParticipantObjectIds))}");
            }
        }
    }

    private static void ValidateBattlefieldTaskParticipantControllerObjectConsistency(
        object? taskPayload,
        string participantControllerLabel,
        IReadOnlyDictionary<string, string>? objectControllers,
        List<string> errors)
    {
        if (objectControllers is null
            || !TryReadObjectStringList(taskPayload, "participantControllerIds", out var participantControllerIds)
            || !TryReadObjectStringList(taskPayload, "participantObjectIds", out var participantObjectIds))
        {
            return;
        }

        var expectedControllerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participantObjectId in participantObjectIds)
        {
            if (string.IsNullOrWhiteSpace(participantObjectId))
            {
                continue;
            }

            var normalizedParticipantObjectId = participantObjectId.Trim();
            if (!objectControllers.TryGetValue(normalizedParticipantObjectId, out var controllerId)
                || string.IsNullOrWhiteSpace(controllerId))
            {
                var detail = FormatExpectedActualForRecovery(
                    $"contains {normalizedParticipantObjectId}",
                    FormatObjectIdsForRecovery(objectControllers.Keys));
                errors.Add(
                    $"{participantControllerLabel} for participant object id {normalizedParticipantObjectId} is missing from object controllers; {detail}");
                continue;
            }

            expectedControllerIds.Add(controllerId.Trim());
        }

        var actualControllerIds = participantControllerIds
            .Where(controllerId => !string.IsNullOrWhiteSpace(controllerId))
            .Select(controllerId => controllerId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var controllerId in actualControllerIds.OrderBy(controllerId => controllerId, StringComparer.Ordinal))
        {
            if (!expectedControllerIds.Contains(controllerId))
            {
                var detail = FormatExpectedActualForRecovery(
                    FormatObjectIdsForRecovery(expectedControllerIds),
                    controllerId);
                errors.Add(
                    $"{participantControllerLabel} {controllerId} is not a controller for participant object ids; {detail}");
            }
        }

        foreach (var controllerId in expectedControllerIds.OrderBy(controllerId => controllerId, StringComparer.Ordinal))
        {
            if (!actualControllerIds.Contains(controllerId))
            {
                var detail = FormatExpectedActualForRecovery(
                    $"contains {controllerId}",
                    FormatObjectIdsForRecovery(actualControllerIds));
                errors.Add(
                    $"{participantControllerLabel} {controllerId} is required by participant object ids; {detail}");
            }
        }
    }

    private static void ValidateTimingStackItemReferenceList(
        object? payload,
        string payloadKey,
        string stackItemLabel,
        IReadOnlySet<string>? knownStackItemIds,
        string knownStackLabel,
        List<string> errors)
    {
        if (knownStackItemIds is null)
        {
            return;
        }

        if (!TryReadObjectStringList(payload, payloadKey, out var stackItemIds))
        {
            return;
        }

        foreach (var stackItemId in stackItemIds)
        {
            if (string.IsNullOrWhiteSpace(stackItemId))
            {
                continue;
            }

            var normalizedStackItemId = stackItemId.Trim();
            if (!knownStackItemIds.Contains(normalizedStackItemId))
            {
                errors.Add($"{stackItemLabel} {normalizedStackItemId} is missing from {knownStackLabel}");
            }
        }
    }

    private static void ValidateTimingObjectReferenceDictionaryKeys(
        object? effectPayload,
        string payloadKey,
        string objectLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        ValidateTimingObjectReferenceMapKeys(
            effectPayload,
            payloadKey,
            objectLabel,
            IsSnapshotStringMapPayloadObject,
            knownObjectIds,
            knownObjectLabel,
            errors);
    }

    private static void ValidateTimingObjectReferenceMapKeys(
        object? effectPayload,
        string payloadKey,
        string objectLabel,
        Func<object?, bool> isValidMapPayload,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (knownObjectIds is null)
        {
            return;
        }

        if (!TryReadObjectValue(effectPayload, payloadKey, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload)
            || !isValidMapPayload(mapPayload))
        {
            return;
        }

        foreach (var (objectId, _) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            ValidateTimingObjectReference(
                objectLabel,
                objectId,
                knownObjectIds,
                knownObjectLabel,
                errors);
        }
    }

    private static void ValidateTimingObjectReferenceStringListDictionary(
        object? effectPayload,
        string payloadKey,
        string sourceObjectLabel,
        string targetObjectLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (knownObjectIds is null)
        {
            return;
        }

        if (!TryReadObjectValue(effectPayload, payloadKey, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload)
            || !IsSnapshotStringListMapPayloadObject(mapPayload))
        {
            return;
        }

        foreach (var (sourceObjectId, targetObjectIdsPayload) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            ValidateTimingObjectReference(
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
                ValidateTimingObjectReference(
                    targetObjectLabel,
                    targetObjectId,
                    knownObjectIds,
                    knownObjectLabel,
                    errors);
            }
        }
    }

    private static void ValidateTimingObjectReference(
        string objectLabel,
        string objectId,
        IReadOnlySet<string> knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
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

    private static (string? EffectId, int? Sequence) ValidateContinuousEffectScalarPayloadValues(
        object? effectPayload,
        string effectLabel,
        List<string> errors)
    {
        var effectId = ValidateSnapshotPayloadRequiredStringValue(
            effectPayload,
            "effectId",
            effectLabel,
            "effect id",
            errors);
        var scope = ValidateSnapshotPayloadRequiredStringValue(
            effectPayload,
            "scope",
            effectLabel,
            "scope",
            errors,
            IsKnownContinuousEffectScope);
        var layer = ValidateSnapshotPayloadRequiredStringValue(
            effectPayload,
            "layer",
            effectLabel,
            "layer",
            errors,
            IsKnownContinuousEffectLayer);
        ValidateContinuousEffectLayerScopeConsistency(effectLabel, scope, layer, errors);
        var duration = ValidateSnapshotPayloadRequiredStringValue(
            effectPayload,
            "duration",
            effectLabel,
            "duration",
            errors,
            IsKnownContinuousEffectDuration);
        TryReadObjectString(effectPayload, "effectKind", out var durationEffectKind);
        ValidateContinuousEffectLayerDurationConsistency(effectLabel, layer, duration, errors, durationEffectKind);
        ValidateContinuousEffectStaticAuraScopeDurationConsistency(effectLabel, scope, layer, duration, errors, durationEffectKind);
        var targetObjectId = ValidateSnapshotPayloadRequiredNullableStringValue(
            effectPayload,
            "targetObjectId",
            effectLabel,
            "target object id",
            errors);
        ValidateContinuousEffectTargetObjectConsistency(effectLabel, scope, layer, targetObjectId, errors);
        var sourceObjectId = ValidateSnapshotPayloadRequiredNullableStringValue(
            effectPayload,
            "sourceObjectId",
            effectLabel,
            "source object id",
            errors);
        ValidateContinuousEffectSourceObjectConsistency(effectLabel, scope, layer, sourceObjectId, errors);
        ValidateContinuousEffectStaticAuraObjectSourceTargetConsistency(
            effectLabel,
            scope,
            layer,
            duration,
            targetObjectId,
            sourceObjectId,
            errors);
        var powerDelta = ValidateSnapshotPayloadRequiredIntValue(
            effectPayload,
            "powerDelta",
            effectLabel,
            "power delta",
            errors);
        var basePower = ValidateSnapshotPayloadRequiredIntValue(
            effectPayload,
            "basePower",
            effectLabel,
            "base power",
            errors);
        var effectivePower = ValidateSnapshotPayloadRequiredIntValue(
            effectPayload,
            "effectivePower",
            effectLabel,
            "effective power",
            errors);
        var effectKind = ValidateSnapshotPayloadOptionalStringValue(
            effectPayload,
            "effectKind",
            effectLabel,
            "effect kind",
            errors,
            rejectEmptyString: true);
        var sourceCardNo = ValidateSnapshotPayloadOptionalStringValue(
            effectPayload,
            "sourceCardNo",
            effectLabel,
            "source card no",
            errors,
            rejectEmptyString: true);
        ValidateContinuousEffectRuleTextPowerScalarConsistency(
            effectLabel,
            scope,
            layer,
            powerDelta,
            basePower,
            effectivePower,
            errors);
        ValidateContinuousEffectPowerModifierPowerDeltaConsistency(
            effectLabel,
            scope,
            layer,
            powerDelta,
            errors);
        ValidateContinuousEffectStaticAuraPowerDeltaConsistency(
            effectPayload,
            effectLabel,
            scope,
            layer,
            duration,
            effectKind,
            sourceCardNo,
            powerDelta,
            errors);
        ValidateContinuousEffectStaticAuraBattlefieldPowerScalarConsistency(
            effectLabel,
            scope,
            layer,
            duration,
            powerDelta,
            basePower,
            effectivePower,
            errors);
        var sequence = ValidateSnapshotPayloadRequiredPositiveIntValue(
            effectPayload,
            "sequence",
            effectLabel,
            "sequence",
            errors);
        ValidateContinuousEffectStaticAuraEffectIdConsistency(
            effectLabel,
            effectId,
            scope,
            layer,
            duration,
            targetObjectId,
            sourceObjectId,
            effectKind,
            errors);
        var sourcePath = ValidateSnapshotPayloadOptionalStringValue(
            effectPayload,
            "sourcePath",
            effectLabel,
            "source path",
            errors,
            rejectEmptyString: true);
        var layerEngineStatus = ValidateSnapshotPayloadOptionalStringValue(
            effectPayload,
            "layerEngineStatus",
            effectLabel,
            "layer engine status",
            errors,
            IsKnownContinuousEffectLayerEngineStatus,
            rejectEmptyString: true);
        ValidateContinuousEffectLayerEngineResidualConsistency(
            effectPayload,
            effectLabel,
            layer,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierFoundationResidualCanonicality(
            effectPayload,
            effectLabel,
            scope,
            layer,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectStaticAuraFoundationResidualCanonicality(
            effectPayload,
            effectLabel,
            scope,
            layer,
            layerEngineStatus,
            errors);
        var requestedPowerDelta = ValidateSnapshotPayloadOptionalIntValue(
            effectPayload,
            "requestedPowerDelta",
            effectLabel,
            "requested power delta",
            errors);
        var appliedPowerDelta = ValidateSnapshotPayloadOptionalIntValue(
            effectPayload,
            "appliedPowerDelta",
            effectLabel,
            "applied power delta",
            errors);
        ValidateContinuousEffectAppliedPowerDeltaConsistency(effectLabel, powerDelta, appliedPowerDelta, errors);
        var minimumPower = ValidateSnapshotPayloadOptionalNonNegativeIntValue(
            effectPayload,
            "minimumPower",
            effectLabel,
            "minimum power",
            errors);
        var resultingPower = ValidateSnapshotPayloadOptionalIntValue(
            effectPayload,
            "resultingPower",
            effectLabel,
            "resulting power",
            errors);
        var snapshotMight = ValidateSnapshotPayloadOptionalNonNegativeIntValue(
            effectPayload,
            "snapshotMight",
            effectLabel,
            "snapshot Might",
            errors);
        if (snapshotMight.HasValue
            && (!string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                || !string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal)))
        {
            errors.Add($"{effectLabel} snapshot Might requires POWER_MODIFIER foundation metadata");
        }
        ValidateContinuousEffectPowerFloorConsistency(
            effectLabel, minimumPower, resultingPower, requestedPowerDelta, appliedPowerDelta, snapshotMight, errors,
            effectKind, duration, sourceCardNo, sourcePath, effectId);
        var appliedOrder = ValidateSnapshotPayloadOptionalPositiveIntValue(
            effectPayload,
            "appliedOrder",
            effectLabel,
            "applied order",
            errors);
        var sourceOrder = ValidateSnapshotPayloadOptionalPositiveIntValue(
            effectPayload,
            "sourceOrder",
            effectLabel,
            "source order",
            errors);
        ValidateContinuousEffectRuleTextModifierScalarAbsence(
            effectLabel,
            scope,
            layer,
            requestedPowerDelta,
            appliedPowerDelta,
            minimumPower,
            resultingPower,
            appliedOrder,
            sourceOrder,
            errors);
        ValidateContinuousEffectRuleTextRuntimeMetadataAbsence(
            effectPayload,
            effectLabel,
            scope,
            layer,
            effectKind,
            sourceCardNo,
            sourcePath,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierFoundationMetadataConsistency(
            effectPayload,
            effectLabel,
            scope,
            layer,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierFoundationSourceCardConsistency(
            effectPayload,
            effectLabel,
            scope,
            layer,
            sourceCardNo,
            sourceObjectId,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierFoundationModifierScalarCompleteness(
            effectPayload,
            effectLabel,
            scope,
            layer,
            sourceObjectId,
            requestedPowerDelta,
            appliedPowerDelta,
            minimumPower,
            resultingPower,
            appliedOrder,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierFoundationLegacyRemainderMetadataConsistency(
            effectPayload,
            effectLabel,
            scope,
            layer,
            effectKind,
            sourcePath,
            sourceObjectId,
            requestedPowerDelta,
            appliedPowerDelta,
            minimumPower,
            resultingPower,
            appliedOrder,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierFoundationResultingPowerConsistency(
            effectPayload,
            effectLabel,
            scope,
            layer,
            effectivePower,
            resultingPower,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierNonFoundationMetadataAbsence(
            effectPayload,
            effectLabel,
            scope,
            layer,
            effectKind,
            sourceCardNo,
            sourcePath,
            sourceObjectId,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierNonFoundationModifierScalarAbsence(
            effectPayload,
            effectLabel,
            scope,
            layer,
            requestedPowerDelta,
            appliedPowerDelta,
            minimumPower,
            resultingPower,
            appliedOrder,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierNonFoundationPowerScalarConsistency(
            effectPayload,
            effectLabel,
            scope,
            layer,
            powerDelta,
            basePower,
            effectivePower,
            layerEngineStatus,
            errors);
        ValidateContinuousEffectPowerModifierSourceOrderConsistency(
            effectLabel,
            scope,
            layer,
            sourceObjectId,
            sourceOrder,
            errors);
        var condition = ValidateSnapshotPayloadOptionalStringValue(
            effectPayload,
            "condition",
            effectLabel,
            "condition",
            errors,
            rejectEmptyString: true);
        var lifecycle = ValidateSnapshotPayloadOptionalStringValue(
            effectPayload,
            "lifecycle",
            effectLabel,
            "lifecycle",
            errors,
            rejectEmptyString: true);
        ValidateContinuousEffectStaticAuraMetadataConsistency(
            effectLabel,
            scope,
            layer,
            duration,
            effectKind,
            sourceCardNo,
            sourcePath,
            condition,
            lifecycle,
            errors);
        ValidateContinuousEffectNonStaticAuraParticipantListAbsence(effectPayload, effectLabel, layer, errors);
        ValidateContinuousEffectNonStaticAuraDependencyListAbsence(effectPayload, effectLabel, layer, errors);
        ValidateContinuousEffectNonStaticAuraStaticMetadataAbsence(effectPayload, effectLabel, layer, errors);
        return (effectId, sequence);
    }

    private static bool IsContinuousEffectLayerScopeValid(string scope, string layer)
    {
        if (string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal))
        {
            return string.Equals(scope, "OBJECT", StringComparison.Ordinal);
        }

        if (string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal))
        {
            return string.Equals(scope, "GLOBAL", StringComparison.Ordinal)
                || string.Equals(scope, "OBJECT", StringComparison.Ordinal);
        }

        return string.Equals(scope, "OBJECT", StringComparison.Ordinal)
            || string.Equals(scope, "BATTLEFIELD", StringComparison.Ordinal);
    }

    private static void ValidateContinuousEffectLayerScopeConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer))
        {
            return;
        }

        if (IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal))
        {
            errors.Add($"{effectLabel} POWER_MODIFIER scope {scope} is invalid");
            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal))
        {
            errors.Add($"{effectLabel} RULE_TEXT scope {scope} is invalid");
            return;
        }

        errors.Add($"{effectLabel} static aura scope {scope} is invalid");
    }

    private static void ValidateContinuousEffectTargetObjectConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? targetObjectId,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(scope, "GLOBAL", StringComparison.Ordinal))
        {
            if (targetObjectId is not null)
            {
                errors.Add($"{effectLabel} global rule text target object id must be absent");
            }

            return;
        }

        if (targetObjectId is not null)
        {
            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal))
        {
            errors.Add($"{effectLabel} static aura target object id is required");
            return;
        }

        errors.Add($"{effectLabel} {layer} target object id is required");
    }
}
