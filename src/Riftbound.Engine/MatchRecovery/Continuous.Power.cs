using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateContinuousEffectNonStaticAuraParticipantListAbsence(
        object? effectPayload,
        string effectLabel,
        string? layer,
        List<string> errors)
    {
        if (layer is null
            || !IsKnownContinuousEffectLayer(layer)
            || string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal))
        {
            return;
        }

        ValidateContinuousEffectReadableStringListAbsence(
            effectPayload,
            "participantObjectIds",
            $"{effectLabel} {layer} participant object id list must be absent",
            errors);
        ValidateContinuousEffectReadableStringListAbsence(
            effectPayload,
            "participantDependencyObjectIds",
            $"{effectLabel} {layer} participant dependency object id list must be absent",
            errors);
    }

    private static void ValidateContinuousEffectNonStaticAuraDependencyListAbsence(
        object? effectPayload,
        string effectLabel,
        string? layer,
        List<string> errors)
    {
        if (layer is null
            || !IsKnownContinuousEffectLayer(layer)
            || string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal))
        {
            return;
        }

        ValidateContinuousEffectReadableStringListAbsence(
            effectPayload,
            "sourceDependencyObjectIds",
            $"{effectLabel} {layer} source dependency object id list must be absent",
            errors);
        ValidateContinuousEffectReadableStringListAbsence(
            effectPayload,
            "targetDependencyObjectIds",
            $"{effectLabel} {layer} target dependency object id list must be absent",
            errors);
    }

    private static void ValidateContinuousEffectNonStaticAuraStaticMetadataAbsence(
        object? effectPayload,
        string effectLabel,
        string? layer,
        List<string> errors)
    {
        if (layer is null
            || !IsKnownContinuousEffectLayer(layer)
            || string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal))
        {
            return;
        }

        ValidateContinuousEffectReadableStringAbsence(
            effectPayload,
            "condition",
            $"{effectLabel} {layer} condition must be absent",
            errors);
        ValidateContinuousEffectReadableStringAbsence(
            effectPayload,
            "lifecycle",
            $"{effectLabel} {layer} lifecycle must be absent",
            errors);
    }

    private static void ValidateContinuousEffectReadableStringListAbsence(
        object? effectPayload,
        string key,
        string diagnostic,
        List<string> errors)
    {
        if (!TryReadObjectStringList(effectPayload, key, out var values)
            || values.Count == 0)
        {
            return;
        }

        errors.Add(diagnostic);
    }

    private static void ValidateContinuousEffectReadableStringAbsence(
        object? effectPayload,
        string key,
        string diagnostic,
        List<string> errors)
    {
        if (!TryReadObjectString(effectPayload, key, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        errors.Add(diagnostic);
    }

    private static void ValidateContinuousEffectStaticAuraDependencyListConsistency(
        object? effectPayload,
        string effectLabel,
        List<string> errors)
    {
        if (!TryReadObjectString(effectPayload, "layer", out var layer)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal))
        {
            return;
        }

        ValidateContinuousEffectStaticAuraRequiredListPresence(
            effectPayload,
            "sourceDependencyObjectIds",
            $"{effectLabel} static aura source dependency object id list is required",
            errors);
        ValidateContinuousEffectStaticAuraRequiredListPresence(
            effectPayload,
            "targetDependencyObjectIds",
            $"{effectLabel} static aura target dependency object id list is required",
            errors);
        ValidateContinuousEffectStaticAuraParticipantObjectListPresence(
            effectPayload,
            effectLabel,
            errors);
        ValidateContinuousEffectStaticAuraParticipantDependencyListPresence(
            effectPayload,
            effectLabel,
            errors);
        ValidateContinuousEffectStaticAuraRequiredSourceOrderPresence(
            effectPayload,
            $"{effectLabel} static aura source order is required",
            errors);
        ValidateContinuousEffectStaticAuraReadableIntAbsence(
            effectPayload,
            "requestedPowerDelta",
            $"{effectLabel} static aura requested power delta must be absent",
            errors);
        ValidateContinuousEffectStaticAuraReadableIntAbsence(
            effectPayload,
            "appliedPowerDelta",
            $"{effectLabel} static aura applied power delta must be absent",
            errors);
        ValidateContinuousEffectStaticAuraReadableIntAbsence(
            effectPayload,
            "minimumPower",
            $"{effectLabel} static aura minimum power must be absent",
            errors);
        ValidateContinuousEffectStaticAuraReadableIntAbsence(
            effectPayload,
            "resultingPower",
            $"{effectLabel} static aura resulting power must be absent",
            errors);
        ValidateContinuousEffectStaticAuraReadableIntAbsence(
            effectPayload,
            "appliedOrder",
            $"{effectLabel} static aura applied order must be absent",
            errors);
        ValidateContinuousEffectStaticAuraDependencyListMembership(
            effectPayload,
            "sourceObjectId",
            "sourceDependencyObjectIds",
            $"{effectLabel} static aura source dependency object id list must include source object id",
            errors);
        ValidateContinuousEffectStaticAuraDependencyListMembership(
            effectPayload,
            "targetObjectId",
            "targetDependencyObjectIds",
            $"{effectLabel} static aura target dependency object id list must include target object id",
            errors);
        ValidateContinuousEffectStaticAuraParticipantDependencyListMembership(
            effectPayload,
            effectLabel,
            errors);
        ValidateContinuousEffectStaticAuraBattlefieldParticipantObjectListConsistency(
            effectPayload,
            effectLabel,
            errors);
    }

    private static void ValidateContinuousEffectStaticAuraRequiredListPresence(
        object? effectPayload,
        string key,
        string diagnostic,
        List<string> errors)
    {
        if (!TryReadObjectValue(effectPayload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload))
        {
            errors.Add(diagnostic);
        }
    }

    private static void ValidateContinuousEffectStaticAuraRequiredSourceOrderPresence(
        object? effectPayload,
        string diagnostic,
        List<string> errors)
    {
        if (!TryReadObjectValue(effectPayload, "sourceOrder", out var sourceOrderPayload)
            || IsNullSnapshotPayloadValue(sourceOrderPayload))
        {
            errors.Add(diagnostic);
        }
    }

    private static void ValidateContinuousEffectStaticAuraReadableIntAbsence(
        object? effectPayload,
        string key,
        string diagnostic,
        List<string> errors)
    {
        if (!TryReadObjectValue(effectPayload, key, out var valuePayload)
            || IsNullSnapshotPayloadValue(valuePayload)
            || !TryReadIntValue(valuePayload, out _))
        {
            return;
        }

        errors.Add(diagnostic);
    }

    private static void ValidateContinuousEffectStaticAuraDependencyListMembership(
        object? effectPayload,
        string objectIdKey,
        string dependencyListKey,
        string diagnosticPrefix,
        List<string> errors)
    {
        if (!TryReadObjectString(effectPayload, objectIdKey, out var objectId)
            || !TryReadObjectStringList(effectPayload, dependencyListKey, out var dependencyObjectIds)
            || dependencyObjectIds.Count == 0
            || dependencyObjectIds.Contains(objectId, StringComparer.Ordinal))
        {
            return;
        }

        errors.Add($"{diagnosticPrefix} {objectId}");
    }

    private static void ValidateContinuousEffectStaticAuraParticipantObjectListPresence(
        object? effectPayload,
        string effectLabel,
        List<string> errors)
    {
        if (!TryReadObjectStringList(effectPayload, "participantDependencyObjectIds", out var participantDependencyObjectIds)
            || participantDependencyObjectIds.Count == 0)
        {
            return;
        }

        if (!TryReadObjectValue(effectPayload, "participantObjectIds", out var participantObjectIdsPayload)
            || IsNullSnapshotPayloadValue(participantObjectIdsPayload))
        {
            errors.Add(
                $"{effectLabel} static aura participant object id list is required when participant dependency object id list is present");
        }
    }

    private static void ValidateContinuousEffectStaticAuraParticipantDependencyListPresence(
        object? effectPayload,
        string effectLabel,
        List<string> errors)
    {
        if (!TryReadObjectStringList(effectPayload, "participantObjectIds", out var participantObjectIds)
            || participantObjectIds.Count == 0)
        {
            return;
        }

        if (!TryReadObjectValue(effectPayload, "participantDependencyObjectIds", out var participantDependencyObjectIdsPayload)
            || IsNullSnapshotPayloadValue(participantDependencyObjectIdsPayload))
        {
            errors.Add(
                $"{effectLabel} static aura participant dependency object id list is required when participant object id list is present");
        }
    }

    private static void ValidateContinuousEffectStaticAuraParticipantDependencyListMembership(
        object? effectPayload,
        string effectLabel,
        List<string> errors)
    {
        if (!TryReadObjectStringList(effectPayload, "participantObjectIds", out var participantObjectIds)
            || participantObjectIds.Count == 0
            || !TryReadObjectStringList(effectPayload, "participantDependencyObjectIds", out var participantDependencyObjectIds)
            || participantDependencyObjectIds.Count == 0)
        {
            return;
        }

        foreach (var participantObjectId in participantObjectIds.Distinct(StringComparer.Ordinal))
        {
            if (participantDependencyObjectIds.Contains(participantObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            errors.Add(
                $"{effectLabel} static aura participant dependency object id list must include participant object id {participantObjectId}");
        }
    }

    private static void ValidateContinuousEffectStaticAuraBattlefieldParticipantObjectListConsistency(
        object? effectPayload,
        string effectLabel,
        List<string> errors)
    {
        if (!TryReadObjectString(effectPayload, "scope", out var scope)
            || !string.Equals(scope, "BATTLEFIELD", StringComparison.Ordinal)
            || !TryReadObjectString(effectPayload, "duration", out var duration)
            || (!string.Equals(
                    duration,
                    "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                    StringComparison.Ordinal)
                && !string.Equals(
                    duration,
                    "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD",
                    StringComparison.Ordinal)))
        {
            return;
        }

        if (!TryReadObjectValue(effectPayload, "participantObjectIds", out var participantObjectIdsPayload)
            || IsNullSnapshotPayloadValue(participantObjectIdsPayload))
        {
            errors.Add($"{effectLabel} battlefield static aura participant object id list is required");
            return;
        }

        if (!TryReadObjectString(effectPayload, "targetObjectId", out var targetObjectId)
            || !TryReadObjectStringList(effectPayload, "participantObjectIds", out var participantObjectIds)
            || participantObjectIds.Count == 0
            || participantObjectIds.Contains(targetObjectId, StringComparer.Ordinal))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} battlefield static aura participant object id list must include target object id {targetObjectId}");
    }

    private static void ValidateContinuousEffectLayerDurationConsistency(
        string effectLabel,
        string? layer,
        string? duration,
        List<string> errors, string? effectKind = null)
    {
        if (duration == "UNTIL_END_OF_BATTLE") {
            if (!(layer == ContinuousEffectLayers.PowerModifier && effectKind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND"
                || layer == ContinuousEffectLayers.RuleText && effectKind is "BATTLE_STEADFAST_DEFEND" or "BATTLE_BULWARK_DEFEND"))
                errors.Add($"{effectLabel} battle duration is invalid for its layer or definition");
            return;
        }
        if (effectKind == CoreRuleEngine.BoonAmplifierEffect && layer == ContinuousEffectLayers.StaticAura
            && duration == "UNTIL_END_OF_TURN") return;
        if (layer is null
            || duration is null
            || !IsKnownContinuousEffectLayer(layer)
            || !IsKnownContinuousEffectDuration(duration))
        {
            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal))
        {
            if (!string.Equals(duration, "WHILE_ARMAMENT_ATTACHED", StringComparison.Ordinal)
                && !string.Equals(duration, "WHILE_SOURCE_ON_PUBLIC_FIELD", StringComparison.Ordinal)
                && !string.Equals(
                    duration,
                    "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD",
                    StringComparison.Ordinal)
                && !string.Equals(
                    duration,
                    "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                    StringComparison.Ordinal)
                && !string.Equals(
                    duration,
                    "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD",
                    StringComparison.Ordinal)
                && !IsObjectStaticAuraPowerDeltaValidatedDuration(duration))
            {
                errors.Add($"{effectLabel} static aura duration {duration} is invalid");
            }

            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && (string.Equals(duration, "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", StringComparison.Ordinal)
                || string.Equals(duration, "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", StringComparison.Ordinal)
                || string.Equals(duration, "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", StringComparison.Ordinal)))
        {
            return;
        }

        if (!string.Equals(duration, "UNTIL_END_OF_TURN", StringComparison.Ordinal))
        {
            errors.Add($"{effectLabel} {layer} duration {duration} is invalid");
        }
    }

    private static void ValidateContinuousEffectStaticAuraScopeDurationConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? duration,
        List<string> errors, string? effectKind = null)
    {
        if (effectKind == CoreRuleEngine.BoonAmplifierEffect && layer == ContinuousEffectLayers.StaticAura
            && duration == "UNTIL_END_OF_TURN") return;
        if (scope is null
            || layer is null
            || duration is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsKnownContinuousEffectDuration(duration)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (string.Equals(scope, "OBJECT", StringComparison.Ordinal)
            && !string.Equals(duration, "WHILE_ARMAMENT_ATTACHED", StringComparison.Ordinal)
            && !string.Equals(duration, "WHILE_SOURCE_ON_PUBLIC_FIELD", StringComparison.Ordinal)
            && !string.Equals(duration, "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", StringComparison.Ordinal)
            && !IsObjectStaticAuraPowerDeltaValidatedDuration(duration))
        {
            errors.Add($"{effectLabel} object static aura duration {duration} is invalid");
            return;
        }

        if (string.Equals(scope, "BATTLEFIELD", StringComparison.Ordinal)
            && !string.Equals(duration, "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", StringComparison.Ordinal)
            && !string.Equals(duration, "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", StringComparison.Ordinal))
        {
            errors.Add($"{effectLabel} battlefield static aura duration {duration} is invalid");
        }
    }

    private static void ValidateContinuousEffectRuleTextPowerScalarConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        int? powerDelta,
        int? basePower,
        int? effectivePower,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (powerDelta.HasValue && powerDelta.Value != 0)
        {
            errors.Add($"{effectLabel} RULE_TEXT power delta must be 0");
        }

        if (!string.Equals(scope, "GLOBAL", StringComparison.Ordinal))
        {
            return;
        }

        if (basePower.HasValue && basePower.Value != 0)
        {
            errors.Add($"{effectLabel} global rule text base power must be 0");
        }

        if (effectivePower.HasValue && effectivePower.Value != 0)
        {
            errors.Add($"{effectLabel} global rule text effective power must be 0");
        }
    }

    private static void ValidateContinuousEffectPowerModifierPowerDeltaConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        int? powerDelta,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer)
            || !powerDelta.HasValue
            || powerDelta.Value != 0)
        {
            return;
        }

        errors.Add($"{effectLabel} POWER_MODIFIER power delta must be nonzero");
    }

    private static void ValidateContinuousEffectRuleTextModifierScalarAbsence(
        string effectLabel,
        string? scope,
        string? layer,
        int? requestedPowerDelta,
        int? appliedPowerDelta,
        int? minimumPower,
        int? resultingPower,
        int? appliedOrder,
        int? sourceOrder,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (requestedPowerDelta.HasValue)
        {
            errors.Add($"{effectLabel} RULE_TEXT requested power delta must be absent");
        }

        if (appliedPowerDelta.HasValue)
        {
            errors.Add($"{effectLabel} RULE_TEXT applied power delta must be absent");
        }

        if (minimumPower.HasValue)
        {
            errors.Add($"{effectLabel} RULE_TEXT minimum power must be absent");
        }

        if (resultingPower.HasValue)
        {
            errors.Add($"{effectLabel} RULE_TEXT resulting power must be absent");
        }

        if (appliedOrder.HasValue)
        {
            errors.Add($"{effectLabel} RULE_TEXT applied order must be absent");
        }

        if (sourceOrder.HasValue)
        {
            errors.Add($"{effectLabel} RULE_TEXT source order must be absent");
        }
    }

    private static void ValidateContinuousEffectPowerModifierSourceOrderConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? sourceObjectId,
        int? sourceOrder,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer)
            || !sourceOrder.HasValue
            || sourceObjectId is not null)
        {
            return;
        }

        errors.Add($"{effectLabel} POWER_MODIFIER source order must be absent without source object id");
    }

    private static void ValidateContinuousEffectPowerModifierFoundationMetadataConsistency(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        if (!hasFoundationOnlyStatus && !hasReadableResiduals)
        {
            return;
        }

        ValidateContinuousEffectRequiredMetadataPresence(
            effectPayload,
            "effectKind",
            $"{effectLabel} POWER_MODIFIER foundation effect kind is required",
            errors);
        ValidateContinuousEffectRequiredMetadataPresence(
            effectPayload,
            "sourcePath",
            $"{effectLabel} POWER_MODIFIER foundation source path is required",
            errors);
    }

    private static void ValidateContinuousEffectRequiredMetadataPresence(
        object? effectPayload,
        string key,
        string diagnostic,
        List<string> errors)
    {
        if (!TryReadObjectValue(effectPayload, key, out var valuePayload)
            || IsNullSnapshotPayloadValue(valuePayload))
        {
            errors.Add(diagnostic);
        }
    }

    private static void ValidateContinuousEffectPowerModifierFoundationSourceCardConsistency(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? sourceCardNo,
        string? sourceObjectId,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        if (!hasFoundationOnlyStatus && !hasReadableResiduals)
        {
            return;
        }

        if (sourceObjectId is null)
        {
            if (sourceCardNo is not null)
            {
                errors.Add($"{effectLabel} POWER_MODIFIER foundation source card no must be absent without source object id");
            }

            return;
        }

        if (!TryReadObjectValue(effectPayload, "sourceCardNo", out var sourceCardNoPayload)
            || IsNullSnapshotPayloadValue(sourceCardNoPayload))
        {
            errors.Add($"{effectLabel} POWER_MODIFIER foundation source card no is required when source object id is present");
        }
    }

    private static void ValidateContinuousEffectPowerModifierNonFoundationMetadataAbsence(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? effectKind,
        string? sourceCardNo,
        string? sourcePath,
        string? sourceObjectId,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        if (hasFoundationOnlyStatus || hasReadableResiduals)
        {
            return;
        }

        if (effectKind is not null)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER effect kind must be absent without foundation metadata");
        }

        if (sourceCardNo is not null)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER source card no must be absent without foundation metadata");
        }

        if (sourcePath is not null)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER source path must be absent without foundation metadata");
        }

        if (sourceObjectId is not null)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER source object id must be absent without foundation metadata");
        }
    }

    private static void ValidateContinuousEffectPowerModifierFoundationModifierScalarCompleteness(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? sourceObjectId,
        int? requestedPowerDelta,
        int? appliedPowerDelta,
        int? minimumPower,
        int? resultingPower,
        int? appliedOrder,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        if (!hasFoundationOnlyStatus && !hasReadableResiduals)
        {
            return;
        }

        var hasTrackedModifierScalar = requestedPowerDelta.HasValue
            || appliedPowerDelta.HasValue
            || minimumPower.HasValue
            || resultingPower.HasValue
            || appliedOrder.HasValue;
        if (!hasTrackedModifierScalar && sourceObjectId is null)
        {
            return;
        }

        var diagnosticReason = hasTrackedModifierScalar
            ? "when tracked modifier scalars are present"
            : "when foundation source object id is present";

        if (sourceObjectId is null)
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation source object id is required when tracked modifier scalars are present");
        }

        if (!requestedPowerDelta.HasValue)
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation requested power delta is required {diagnosticReason}");
        }

        if (!appliedPowerDelta.HasValue)
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation applied power delta is required {diagnosticReason}");
        }

        if (!minimumPower.HasValue)
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation minimum power is required {diagnosticReason}");
        }

        if (!resultingPower.HasValue)
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation resulting power is required {diagnosticReason}");
        }

        if (!appliedOrder.HasValue)
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation applied order is required {diagnosticReason}");
        }
    }

    private static void ValidateContinuousEffectPowerModifierFoundationLegacyRemainderMetadataConsistency(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? effectKind,
        string? sourcePath,
        string? sourceObjectId,
        int? requestedPowerDelta,
        int? appliedPowerDelta,
        int? minimumPower,
        int? resultingPower,
        int? appliedOrder,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        var hasTrackedModifierScalar = requestedPowerDelta.HasValue
            || appliedPowerDelta.HasValue
            || minimumPower.HasValue
            || resultingPower.HasValue
            || appliedOrder.HasValue;
        if ((!hasFoundationOnlyStatus && !hasReadableResiduals)
            || sourceObjectId is not null
            || hasTrackedModifierScalar)
        {
            return;
        }

        const string legacyEffectKind = "LEGACY_UNTRACKED_POWER_MODIFIER";
        const string legacySourcePath = "MatchState.ContinuousEffects.LegacyRemainder";
        if (effectKind is not null
            && !string.Equals(effectKind, legacyEffectKind, StringComparison.Ordinal))
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation legacy remainder effect kind must be {legacyEffectKind} without source object id");
        }

        if (sourcePath is not null
            && !string.Equals(sourcePath, legacySourcePath, StringComparison.Ordinal))
        {
            errors.Add(
                $"{effectLabel} POWER_MODIFIER foundation legacy remainder source path must be {legacySourcePath} without source object id");
        }
    }

    private static void ValidateContinuousEffectPowerModifierFoundationResultingPowerConsistency(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        int? effectivePower,
        int? resultingPower,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        if ((!hasFoundationOnlyStatus && !hasReadableResiduals)
            || !effectivePower.HasValue
            || !resultingPower.HasValue
            || resultingPower.Value == effectivePower.Value)
        {
            return;
        }

        errors.Add(
            $"{effectLabel} POWER_MODIFIER foundation resulting power {resultingPower.Value} must match effective power {effectivePower.Value}");
    }

    private static void ValidateContinuousEffectPowerModifierNonFoundationPowerScalarConsistency(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        int? powerDelta,
        int? basePower,
        int? effectivePower,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        if (hasFoundationOnlyStatus
            || hasReadableResiduals
            || !powerDelta.HasValue
            || !basePower.HasValue
            || !effectivePower.HasValue)
        {
            return;
        }

        var requiredEffectivePower = basePower.Value + powerDelta.Value;
        if (effectivePower.Value == requiredEffectivePower)
        {
            return;
        }

        errors.Add(
            $"{effectLabel} POWER_MODIFIER non-foundation effective power {effectivePower.Value} must equal base power {basePower.Value} plus power delta {powerDelta.Value}");
    }

    private static void ValidateContinuousEffectPowerModifierNonFoundationModifierScalarAbsence(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        int? requestedPowerDelta,
        int? appliedPowerDelta,
        int? minimumPower,
        int? resultingPower,
        int? appliedOrder,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var hasReadableResiduals = TryReadObjectStringList(
                effectPayload,
                "deferredLayerEngineResiduals",
                out var residuals)
            && residuals.Count > 0;
        if (hasFoundationOnlyStatus || hasReadableResiduals)
        {
            return;
        }

        if (requestedPowerDelta.HasValue)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER requested power delta must be absent without foundation metadata");
        }

        if (appliedPowerDelta.HasValue)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER applied power delta must be absent without foundation metadata");
        }

        if (minimumPower.HasValue)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER minimum power must be absent without foundation metadata");
        }

        if (resultingPower.HasValue)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER resulting power must be absent without foundation metadata");
        }

        if (appliedOrder.HasValue)
        {
            errors.Add($"{effectLabel} POWER_MODIFIER applied order must be absent without foundation metadata");
        }
    }

    private static void ValidateContinuousEffectRuleTextRuntimeMetadataAbsence(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? effectKind,
        string? sourceCardNo,
        string? sourcePath,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var equipmentKeywordAura = scope == "OBJECT"
            && effectKind == StaticAuraKinds.FriendlyEquipmentKeyword
            && sourcePath == "CoreRuleEngine.EquipmentKeywordAuraContributions"
            && !string.IsNullOrWhiteSpace(sourceCardNo)
            && (sourceCardNo.Contains('·') || sourceCardNo.Contains('/'))
            && TryReadObjectString(effectPayload, "effectId", out var equipmentEffectId)
            && equipmentEffectId is not null
            && equipmentEffectId.StartsWith("EQUIPMENT_KEYWORD_AURA:", StringComparison.Ordinal);
        var battleKeyword = scope == "OBJECT" && effectKind is "BATTLE_STEADFAST_DEFEND" or "BATTLE_BULWARK_DEFEND"
            && sourceCardNo is not null
            && (BattleLimitedMarkers.Definition(sourceCardNo, BattleLimitedMarkers.SteadfastDefend)
                || BattleLimitedMarkers.Definition(sourceCardNo, BattleLimitedMarkers.BulwarkDefend))
            && sourcePath == "CoreRuleEngine.ResolveBattleLimitedRoleSkill"
            && TryReadObjectString(effectPayload, "duration", out var battleDuration) && battleDuration == "UNTIL_END_OF_BATTLE"
            && TryReadObjectString(effectPayload, "effectId", out var battleEffectId) && battleEffectId is not null
            && battleEffectId.StartsWith("OBJECT:", StringComparison.Ordinal)
            && (battleEffectId.Contains(":COMBAT_KEYWORD:坚守2:", StringComparison.Ordinal)
                || battleEffectId.Contains(":COMBAT_KEYWORD:壁垒:", StringComparison.Ordinal));
        if (!battleKeyword && !equipmentKeywordAura && !string.IsNullOrWhiteSpace(effectKind))
        {
            errors.Add($"{effectLabel} RULE_TEXT effect kind must be absent");
        }

        if (!battleKeyword && !equipmentKeywordAura && !string.IsNullOrWhiteSpace(sourceCardNo))
        {
            errors.Add($"{effectLabel} RULE_TEXT source card no must be absent");
        }

        if (!battleKeyword && !equipmentKeywordAura && !string.IsNullOrWhiteSpace(sourcePath))
        {
            errors.Add($"{effectLabel} RULE_TEXT source path must be absent");
        }

        if (!string.IsNullOrWhiteSpace(layerEngineStatus))
        {
            errors.Add($"{effectLabel} RULE_TEXT layer engine status must be absent");
        }

        ValidateContinuousEffectReadableStringListAbsence(
            effectPayload,
            "deferredLayerEngineResiduals",
            $"{effectLabel} RULE_TEXT deferred LayerEngine residual list must be absent",
            errors);
    }

    private static void ValidateContinuousEffectAppliedPowerDeltaConsistency(
        string effectLabel,
        int? powerDelta,
        int? appliedPowerDelta,
        List<string> errors)
    {
        if (!powerDelta.HasValue
            || !appliedPowerDelta.HasValue
            || appliedPowerDelta.Value == powerDelta.Value)
        {
            return;
        }

        errors.Add(
            $"{effectLabel} applied power delta {appliedPowerDelta.Value} must match power delta {powerDelta.Value}");
    }

    private static void ValidateContinuousEffectPowerFloorConsistency(
        string effectLabel,
        int? minimumPower,
        int? resultingPower,
        int? requestedPowerDelta,
        int? appliedPowerDelta,
        int? snapshotMight,
        List<string> errors, string? effectKind = null, string? duration = null, string? sourceCardNo = null,
        string? sourcePath = null, string? effectId = null)
    {
        if (duration == "UNTIL_END_OF_BATTLE" && effectKind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND"
            && sourceCardNo is "SFD·110/221" or "SFD·110a/221" && sourcePath == "CoreRuleEngine.ResolveBattleLimitedRoleSkill"
            && effectId?.StartsWith("BATTLE_LIMITED_POWER:", StringComparison.Ordinal) == true) {
            if (minimumPower != 0 || snapshotMight is not > 0 || requestedPowerDelta != snapshotMight || appliedPowerDelta != snapshotMight)
                errors.Add($"{effectLabel} battle double snapshot and additive deltas disagree");
            return;
        }
        if (minimumPower is not > 0 || requestedPowerDelta is >= 0)
        {
            if (snapshotMight.HasValue)
                errors.Add($"{effectLabel} snapshot Might must be absent without a minimum Might reduction");
            if (minimumPower is > 0 && resultingPower.HasValue && resultingPower.Value < minimumPower.Value)
                errors.Add($"{effectLabel} resulting power {resultingPower.Value} is less than minimum power {minimumPower.Value}");
            return;
        }

        if (!snapshotMight.HasValue)
        {
            // Missing provenance is invalid; do not recover old development
            // matches by guessing total Might from the unit's current raw stat.
            if (requestedPowerDelta is < 0)
                errors.Add($"{effectLabel} snapshot Might is required for a minimum Might reduction");
            if (resultingPower.HasValue && resultingPower.Value < minimumPower.Value)
                errors.Add($"{effectLabel} resulting power {resultingPower.Value} is less than minimum power {minimumPower.Value}");
            return;
        }

        if (snapshotMight.Value < 0)
        {
            errors.Add($"{effectLabel} snapshot Might cannot be negative");
            return;
        }
        if (!requestedPowerDelta.HasValue || !appliedPowerDelta.HasValue)
        {
            errors.Add($"{effectLabel} snapshot Might requires requested and applied power deltas");
            return;
        }

        // Independent arithmetic check of the decision captured at application.
        // Later aura, equipment or battle-role changes do not reapply the floor.
        var allowedReduction = Math.Max(0L, (long)snapshotMight.Value - minimumPower.Value);
        var expectedDelta = Math.Max((long)requestedPowerDelta.Value, -allowedReduction);
        if (appliedPowerDelta.Value != expectedDelta)
            errors.Add($"{effectLabel} applied power delta {appliedPowerDelta.Value} disagrees with snapshot Might {snapshotMight.Value} and minimum power {minimumPower.Value}");
    }

    private static void ValidateContinuousEffectPowerModifierFoundationResidualCanonicality(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer)
            || !string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal)
            || !TryReadObjectStringList(effectPayload, "deferredLayerEngineResiduals", out var residuals)
            || residuals.Count == 0)
        {
            return;
        }

        if (StringListsEqual(residuals, ContinuousEffectFoundationResiduals()))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} POWER_MODIFIER foundation deferred LayerEngine residual list must match current foundation residual set");
    }

    private static void ValidateContinuousEffectStaticAuraFoundationResidualCanonicality(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? layerEngineStatus,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer)
            || !string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal)
            || !TryReadObjectStringList(effectPayload, "deferredLayerEngineResiduals", out var residuals)
            || residuals.Count == 0)
        {
            return;
        }

        if (StringListsEqual(residuals, ContinuousEffectFoundationResiduals()))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} static aura deferred LayerEngine residual list must match current foundation residual set");
    }

    private static IReadOnlyList<string> ContinuousEffectFoundationResiduals()
    {
        return
        [
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        ];
    }
}
