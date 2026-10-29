using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void AddSpectatorContinuousEffectOptionalStringListAggregateMismatch(
        string fieldLabel,
        IEnumerable<IReadOnlyList<string>?> expectedValues,
        IReadOnlyList<object?> spectatorEffects,
        string key,
        List<string> errors)
    {
        AddSpectatorContinuousEffectAggregateMismatch(
            fieldLabel,
            expectedValues.Select(value => FormatRecoveryDiagnosticValue(value ?? [])).ToArray(),
            ExtractObjectStringListDiagnosticValues(spectatorEffects, key),
            errors);
    }

    private static void AddSpectatorContinuousEffectAggregateMismatch(
        string fieldLabel,
        IReadOnlyList<string> expectedValues,
        IReadOnlyList<string> actualValues,
        List<string> errors)
    {
        errors.Add(
            $"spectator replay frame timing continuous effect {fieldLabel} disagree with authoritative state continuous effect {fieldLabel}; {FormatExpectedActualForRecovery(expectedValues, actualValues)}");
    }

    private static void AddSpectatorContinuousEffectCountMismatch(
        int spectatorCount,
        int authoritativeCount,
        List<string> errors)
    {
        errors.Add(
            $"spectator replay frame timing continuous effect count {spectatorCount} does not match authoritative state continuous effect count {authoritativeCount}; {FormatExpectedActualForRecovery(authoritativeCount, spectatorCount)}");
    }

    private static IReadOnlyDictionary<string, ContinuousEffectState> BuildAuthoritativeContinuousEffectsById(
        IReadOnlyList<ContinuousEffectState> authoritativeEffects)
    {
        var authoritativeEffectsById = new Dictionary<string, ContinuousEffectState>(StringComparer.Ordinal);
        foreach (var authoritativeEffect in authoritativeEffects)
        {
            if (string.IsNullOrWhiteSpace(authoritativeEffect.EffectId))
            {
                continue;
            }

            var normalizedEffectId = authoritativeEffect.EffectId.Trim();
            if (!authoritativeEffectsById.ContainsKey(normalizedEffectId))
            {
                authoritativeEffectsById.Add(normalizedEffectId, authoritativeEffect);
            }
        }

        return authoritativeEffectsById;
    }

    private static void ReportMissingSpectatorContinuousEffectAuthoritativeKeySet(
        IReadOnlyList<ContinuousEffectState> authoritativeEffects,
        List<string> errors)
    {
        ValidateSpectatorContinuousEffectAuthoritativeKeySet(
            Array.Empty<object?>(),
            BuildAuthoritativeContinuousEffectsById(authoritativeEffects),
            errors);
    }

    private static void ValidateSpectatorContinuousEffectAuthoritativeKeySet(
        IReadOnlyList<object?> effectPayloads,
        IReadOnlyDictionary<string, ContinuousEffectState> authoritativeEffectsById,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing continuous effect item";
        var actualEffectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effectPayload in effectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(effectPayload)
                || !TryReadObjectString(effectPayload, "effectId", out var effectId)
                || string.IsNullOrWhiteSpace(effectId))
            {
                continue;
            }

            var normalizedEffectId = effectId.Trim();
            actualEffectIds.Add(normalizedEffectId);
            if (!authoritativeEffectsById.ContainsKey(normalizedEffectId))
            {
                errors.Add(
                    $"{payloadLabel} effect id {normalizedEffectId} is not present in authoritative state continuous effects");
            }
        }

        foreach (var authoritativeEffectId in authoritativeEffectsById.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!actualEffectIds.Contains(authoritativeEffectId))
            {
                errors.Add(
                    $"{payloadLabel} effect id {authoritativeEffectId} is required by authoritative state continuous effects");
            }
        }
    }

    private static void ValidateSpectatorContinuousEffectKeyedAuthoritativeValues(
        IReadOnlyList<object?> effectPayloads,
        IReadOnlyDictionary<string, ContinuousEffectState> authoritativeEffectsById,
        List<string> errors)
    {
        foreach (var effectPayload in effectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(effectPayload)
                || !TryReadObjectString(effectPayload, "effectId", out var effectId)
                || string.IsNullOrWhiteSpace(effectId))
            {
                continue;
            }

            var normalizedEffectId = effectId.Trim();
            if (!authoritativeEffectsById.TryGetValue(normalizedEffectId, out var authoritativeEffect))
            {
                continue;
            }

            ValidateSpectatorContinuousEffectKeyedStringValue(
                effectPayload,
                "scope",
                "scope",
                authoritativeEffect.Scope,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedStringValue(
                effectPayload,
                "layer",
                "layer",
                authoritativeEffect.Layer,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedStringValue(
                effectPayload,
                "duration",
                "duration",
                authoritativeEffect.Duration,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "targetObjectId",
                "target object id",
                authoritativeEffect.TargetObjectId,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "sourceObjectId",
                "source object id",
                authoritativeEffect.SourceObjectId,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedIntValue(
                effectPayload,
                "powerDelta",
                "power delta",
                authoritativeEffect.PowerDelta,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedIntValue(
                effectPayload,
                "basePower",
                "base power",
                authoritativeEffect.BasePower,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedIntValue(
                effectPayload,
                "effectivePower",
                "effective power",
                authoritativeEffect.EffectivePower,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedIntValue(
                effectPayload,
                "sequence",
                "sequence",
                authoritativeEffect.Sequence,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "effectKind",
                "effect kind",
                authoritativeEffect.EffectKind,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "sourceCardNo",
                "source card number",
                authoritativeEffect.SourceCardNo,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "sourcePath",
                "source path",
                authoritativeEffect.SourcePath,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "layerEngineStatus",
                "layer engine status",
                authoritativeEffect.IsLayerEngineFoundationOnly ? "FOUNDATION_ONLY" : null,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
                effectPayload,
                "requestedPowerDelta",
                "requested power delta",
                authoritativeEffect.RequestedPowerDelta,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
                effectPayload,
                "appliedPowerDelta",
                "applied power delta",
                authoritativeEffect.AppliedPowerDelta,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
                effectPayload,
                "minimumPower",
                "minimum power",
                authoritativeEffect.MinimumPower,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
                effectPayload,
                "resultingPower",
                "resulting power",
                authoritativeEffect.ResultingPower,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
                effectPayload,
                "snapshotMight",
                "snapshot Might",
                authoritativeEffect.SnapshotMight,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
                effectPayload,
                "appliedOrder",
                "applied order",
                authoritativeEffect.AppliedOrder,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
                effectPayload,
                "sourceOrder",
                "source order",
                authoritativeEffect.SourceOrder,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "condition",
                "condition",
                authoritativeEffect.Condition,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
                effectPayload,
                "lifecycle",
                "lifecycle",
                authoritativeEffect.Lifecycle,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringListValue(
                effectPayload,
                "participantObjectIds",
                "participant object ids",
                authoritativeEffect.ParticipantObjectIds,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringListValue(
                effectPayload,
                "sourceDependencyObjectIds",
                "source dependency object ids",
                authoritativeEffect.SourceDependencyObjectIds,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringListValue(
                effectPayload,
                "targetDependencyObjectIds",
                "target dependency object ids",
                authoritativeEffect.TargetDependencyObjectIds,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringListValue(
                effectPayload,
                "participantDependencyObjectIds",
                "participant dependency object ids",
                authoritativeEffect.ParticipantDependencyObjectIds,
                normalizedEffectId,
                errors);
            ValidateSpectatorContinuousEffectKeyedOptionalStringListValue(
                effectPayload,
                "deferredLayerEngineResiduals",
                "deferred LayerEngine residuals",
                authoritativeEffect.DeferredLayerEngineResiduals,
                normalizedEffectId,
                errors);
        }
    }

    private static void ValidateSpectatorContinuousEffectKeyedStringValue(
        object? effectPayload,
        string key,
        string fieldLabel,
        string expected,
        string effectId,
        List<string> errors)
    {
        if (!TryReadObjectString(effectPayload, key, out var actual))
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(expected, FormatUnreadableObjectValue(effectPayload, key)),
                errors);
            return;
        }

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(expected, actual),
                errors);
        }
    }

    private static void ValidateSpectatorContinuousEffectKeyedOptionalStringValue(
        object? effectPayload,
        string key,
        string fieldLabel,
        string? expected,
        string effectId,
        List<string> errors)
    {
        var expectedText = expected ?? string.Empty;
        if (!TryReadObjectValue(effectPayload, key, out var value))
        {
            if (string.IsNullOrEmpty(expectedText))
            {
                return;
            }

            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(expectedText, "<missing>"),
                errors);
            return;
        }

        if (!TryReadOptionalStringValue(value, out var actual)
            || !string.Equals(actual, expectedText, StringComparison.Ordinal))
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(
                    expectedText,
                    TryReadOptionalStringValue(value, out var readableActual) ? readableActual : "<unreadable>"),
                errors);
        }
    }

    private static void ValidateSpectatorContinuousEffectKeyedIntValue(
        object? effectPayload,
        string key,
        string fieldLabel,
        int expected,
        string effectId,
        List<string> errors)
    {
        if (!TryReadObjectInt(effectPayload, key, out var actual))
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(expected, FormatUnreadableObjectValue(effectPayload, key)),
                errors);
            return;
        }

        if (actual != expected)
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(expected, actual),
                errors);
        }
    }

    private static void ValidateSpectatorContinuousEffectKeyedOptionalIntValue(
        object? effectPayload,
        string key,
        string fieldLabel,
        int? expected,
        string effectId,
        List<string> errors)
    {
        if (!TryReadObjectValue(effectPayload, key, out var value))
        {
            if (!expected.HasValue)
            {
                return;
            }

            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(expected.Value, "<missing>"),
                errors);
            return;
        }

        if (!expected.HasValue)
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery("<empty>", FormatReadableIntValue(value)),
                errors);
            return;
        }

        if (!TryReadIntValue(value, out var actual)
            || actual != expected.Value)
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(
                    expected.Value,
                    TryReadIntValue(value, out var readableActual) ? readableActual : "<unreadable>"),
                errors);
        }
    }

    private static void ValidateSpectatorContinuousEffectKeyedOptionalStringListValue(
        object? effectPayload,
        string key,
        string fieldLabel,
        IReadOnlyList<string>? expected,
        string effectId,
        List<string> errors)
    {
        IReadOnlyList<string> expectedValues = expected ?? [];
        if (!TryReadObjectValue(effectPayload, key, out var value))
        {
            if (expectedValues.Count == 0)
            {
                return;
            }

            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(expectedValues, "<missing>"),
                errors);
            return;
        }

        if (!TryReadStringListValue(value, out var actual)
            || !StringListsEqual(actual, expectedValues))
        {
            AddSpectatorContinuousEffectKeyedValueMismatch(
                fieldLabel,
                effectId,
                FormatExpectedActualForRecovery(
                    expectedValues,
                    TryReadStringListValue(value, out var readableActual) ? readableActual : "<unreadable>"),
                errors);
        }
    }

    private static void AddSpectatorContinuousEffectKeyedValueMismatch(
        string fieldLabel,
        string effectId,
        string detail,
        List<string> errors)
    {
        errors.Add(
            $"spectator replay frame timing continuous effect item {fieldLabel} does not match authoritative state continuous effect {fieldLabel} for effect id {effectId}; {detail}");
    }

    private static string FormatExpectedActualForRecovery(object? expected, object? actual)
    {
        return $"expected {FormatRecoveryDiagnosticValue(expected)} but got {FormatRecoveryDiagnosticValue(actual)}";
    }

    private static IReadOnlyList<string> FormatObjectTagsForRecovery(IEnumerable<string> tags)
    {
        return tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> FormatObjectIdsForRecovery(IEnumerable<string> objectIds)
    {
        return objectIds.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray();
    }

    private static string FormatObjectTagPresenceForRecovery(
        IReadOnlyDictionary<string, IReadOnlySet<string>> objectTags,
        string objectId)
    {
        return FormatExpectedActualForRecovery(
            $"contains {objectId}",
            FormatObjectIdsForRecovery(objectTags.Keys));
    }

    private static string FormatRecoveryDiagnosticValue(object? value)
    {
        return value switch
        {
            null => "<empty>",
            string text => text.Length == 0 ? "<empty>" : text,
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            IReadOnlyDictionary<string, int> values => FormatRecoveryDiagnosticIntDictionary(values),
            IReadOnlyDictionary<string, IReadOnlyList<string>> values => FormatRecoveryDiagnosticStringListDictionary(values),
            IReadOnlyList<BattleRequiredAssignmentView> values => FormatRecoveryDiagnosticRequiredAssignments(values),
            IReadOnlyList<IReadOnlyList<string>> values => FormatRecoveryDiagnosticStringListCollection(values),
            IReadOnlyList<string> values => values.Count == 0 ? "[]" : $"[{string.Join(", ", values)}]",
            IEnumerable<string> values => FormatRecoveryDiagnosticValue(values.ToArray()),
            _ => value.ToString() ?? "<unreadable>"
        };
    }

    private static string FormatObjectLocationValue(ObjectLocationState location)
    {
        return "{"
            + $"playerId: {FormatRecoveryDiagnosticValue(location.PlayerId)}, "
            + $"zone: {FormatRecoveryDiagnosticValue(location.Zone)}, "
            + $"battlefieldObjectId: {FormatRecoveryDiagnosticValue(location.BattlefieldObjectId ?? string.Empty)}"
            + "}";
    }

    private static string FormatRecoveryDiagnosticIntDictionary(IReadOnlyDictionary<string, int> values)
    {
        var formattedEntries = values
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}: {entry.Value.ToString(CultureInfo.InvariantCulture)}");

        return values.Count == 0
            ? "{}"
            : $"{{{string.Join(", ", formattedEntries)}}}";
    }

    private static string FormatRecoveryDiagnosticStringListDictionary(
        IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        var formattedEntries = values
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}: {FormatRecoveryDiagnosticValue(entry.Value)}");

        return values.Count == 0
            ? "{}"
            : $"{{{string.Join(", ", formattedEntries)}}}";
    }

    private static string FormatRecoveryDiagnosticStringListCollection(
        IReadOnlyList<IReadOnlyList<string>> values)
    {
        if (values.Count == 0)
        {
            return "[]";
        }

        var formattedItems = values.Select(FormatRecoveryDiagnosticValue);
        return $"[{string.Join(", ", formattedItems)}]";
    }

    private static string FormatRecoveryDiagnosticRequiredAssignments(
        IReadOnlyList<BattleRequiredAssignmentView> values)
    {
        if (values.Count == 0)
        {
            return "[]";
        }

        var formattedAssignments = values.Select(assignment =>
            "{"
            + $"sourceObjectId: {FormatRecoveryDiagnosticValue(assignment.SourceObjectId)}, "
            + $"damage: {FormatRecoveryDiagnosticValue(assignment.Damage)}, "
            + $"legalTargetObjectIds: {FormatRecoveryDiagnosticValue(assignment.LegalTargetObjectIds)}"
            + "}");

        return $"[{string.Join(", ", formattedAssignments)}]";
    }

    private static string FormatUnreadableObjectValue(object? payload, string key)
    {
        return TryReadObjectValue(payload, key, out _) ? "<unreadable>" : "<missing>";
    }

    private static object FormatReadableBoolValue(bool hasValue, bool value, object? payload, string key)
    {
        return hasValue ? value : FormatUnreadableObjectValue(payload, key);
    }

    private static object FormatReadableStringValue(bool hasValue, string? value, object? payload, string key)
    {
        return hasValue && value is not null ? value : FormatUnreadableObjectValue(payload, key);
    }

    private static object FormatReadableIntValue(int? value, object? payload, string key)
    {
        return value is int actual ? actual : FormatUnreadableObjectValue(payload, key);
    }

    private static object FormatReadableIntDictionaryValue(
        bool hasValue,
        IReadOnlyDictionary<string, int> value,
        object? payload,
        string key)
    {
        return hasValue ? value : FormatUnreadableObjectValue(payload, key);
    }

    private static object FormatReadableStringListValue(
        bool hasValue,
        IReadOnlyList<string> value,
        object? payload,
        string key)
    {
        return hasValue ? value : FormatUnreadableObjectValue(payload, key);
    }

    private static object FormatReadableIntValue(object? value)
    {
        return TryReadIntValue(value, out var actual) ? actual : "<unreadable>";
    }

    private static IReadOnlyList<string> ExtractObjectStringDiagnosticValues(
        IReadOnlyList<object?> items,
        string key)
    {
        return items
            .Select(item => TryReadObjectString(item, key, out var value)
                ? FormatRecoveryDiagnosticValue(value ?? string.Empty)
                : FormatRecoveryDiagnosticValue(FormatUnreadableObjectValue(item, key)))
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractObjectOptionalStringDiagnosticValues(
        IReadOnlyList<object?> items,
        string key)
    {
        return items
            .Select(item =>
            {
                if (!TryReadObjectValue(item, key, out var value))
                {
                    return FormatRecoveryDiagnosticValue("<missing>");
                }

                return TryReadOptionalStringValue(value, out var actual)
                    ? FormatRecoveryDiagnosticValue(actual)
                    : FormatRecoveryDiagnosticValue("<unreadable>");
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractObjectIntDiagnosticValues(
        IReadOnlyList<object?> items,
        string key)
    {
        return items
            .Select(item => TryReadObjectInt(item, key, out var value)
                ? FormatRecoveryDiagnosticValue(value)
                : FormatRecoveryDiagnosticValue(FormatUnreadableObjectValue(item, key)))
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractObjectOptionalIntDiagnosticValues(
        IReadOnlyList<object?> items,
        string key)
    {
        return items
            .Select(item =>
            {
                if (!TryReadObjectValue(item, key, out var value))
                {
                    return FormatRecoveryDiagnosticValue("<missing>");
                }

                return TryReadIntValue(value, out var actual)
                    ? FormatRecoveryDiagnosticValue(actual)
                    : FormatRecoveryDiagnosticValue("<unreadable>");
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractObjectStringListDiagnosticValues(
        IReadOnlyList<object?> items,
        string key)
    {
        return items
            .Select(item =>
            {
                if (!TryReadObjectValue(item, key, out var value))
                {
                    return FormatRecoveryDiagnosticValue("<missing>");
                }

                return TryReadStringListValue(value, out var actual)
                    ? FormatRecoveryDiagnosticValue(actual)
                    : FormatRecoveryDiagnosticValue("<unreadable>");
            })
            .ToArray();
    }

    private static (string? EffectId, int? Sequence) ValidateSpectatorContinuousEffectPayloadValues(
        object? effectPayload,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        const string effectLabel = "spectator replay frame timing continuous effect item";
        var effectValues = ValidateContinuousEffectScalarPayloadValues(effectPayload, effectLabel, errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            effectPayload,
            effectLabel,
            "participantObjectIds",
            "participant object id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            effectPayload,
            "participantObjectIds",
            effectLabel,
            "participant object id",
            errors);
        ValidateSnapshotPayloadPresentStringListNonEmpty(
            effectPayload,
            "participantObjectIds",
            effectLabel,
            "participant object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            effectPayload,
            effectLabel,
            "sourceDependencyObjectIds",
            "source dependency object id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            effectPayload,
            "sourceDependencyObjectIds",
            effectLabel,
            "source dependency object id",
            errors);
        ValidateSnapshotPayloadPresentStringListNonEmpty(
            effectPayload,
            "sourceDependencyObjectIds",
            effectLabel,
            "source dependency object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            effectPayload,
            effectLabel,
            "targetDependencyObjectIds",
            "target dependency object id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            effectPayload,
            "targetDependencyObjectIds",
            effectLabel,
            "target dependency object id",
            errors);
        ValidateSnapshotPayloadPresentStringListNonEmpty(
            effectPayload,
            "targetDependencyObjectIds",
            effectLabel,
            "target dependency object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            effectPayload,
            effectLabel,
            "participantDependencyObjectIds",
            "participant dependency object id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            effectPayload,
            "participantDependencyObjectIds",
            effectLabel,
            "participant dependency object id",
            errors);
        ValidateSnapshotPayloadPresentStringListNonEmpty(
            effectPayload,
            "participantDependencyObjectIds",
            effectLabel,
            "participant dependency object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            effectPayload,
            effectLabel,
            "deferredLayerEngineResiduals",
            "deferred LayerEngine residual",
            errors);
        ValidateSnapshotPayloadStringListValues(
            effectPayload,
            "deferredLayerEngineResiduals",
            effectLabel,
            "deferred LayerEngine residual",
            errors);
        ValidateContinuousEffectStaticAuraDependencyListConsistency(effectPayload, effectLabel, errors);
        ValidateContinuousEffectObjectReferences(
            effectPayload,
            effectLabel,
            knownObjectIds,
            "object registry",
            errors);
        return effectValues;
    }

    private static void ValidateContinuousEffectObjectReferences(
        object? effectPayload,
        string effectLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors,
        string? historicalSourceObjectId = null)
    {
        if (knownObjectIds is null)
        {
            return;
        }

        ValidateTimingOptionalObjectReferenceWithExpectedDetails(
            effectPayload,
            "targetObjectId",
            $"{effectLabel} target object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        if (historicalSourceObjectId is null)
            ValidateTimingOptionalObjectReferenceWithExpectedDetails(
                effectPayload,
                "sourceObjectId",
                $"{effectLabel} source object id",
                knownObjectIds,
                knownObjectLabel,
                errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            effectPayload,
            "participantObjectIds",
            $"{effectLabel} participant object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            effectPayload,
            "sourceDependencyObjectIds",
            $"{effectLabel} source dependency object id",
            historicalSourceObjectId is null ? knownObjectIds : knownObjectIds.Append(historicalSourceObjectId).ToHashSet(StringComparer.Ordinal),
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            effectPayload,
            "targetDependencyObjectIds",
            $"{effectLabel} target dependency object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            effectPayload,
            "participantDependencyObjectIds",
            $"{effectLabel} participant dependency object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
    }

    private static void ValidateTimingOptionalObjectReference(
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

        if (!TryReadObjectValue(effectPayload, payloadKey, out var objectIdPayload)
            || IsNullSnapshotPayloadValue(objectIdPayload)
            || !TryReadOptionalStringValue(objectIdPayload, out var objectId)
            || string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        ValidateTimingObjectReference(
            objectLabel,
            objectId,
            knownObjectIds,
            knownObjectLabel,
            errors);
    }

    private static void ValidateTimingOptionalPlayerReference(
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

        if (!TryReadObjectValue(payload, payloadKey, out var playerIdPayload)
            || IsNullSnapshotPayloadValue(playerIdPayload)
            || !TryReadOptionalStringValue(playerIdPayload, out var playerId)
            || string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        ValidateTimingPlayerReference(
            playerLabel,
            playerId,
            knownPlayerIds,
            knownPlayerLabel,
            errors);
    }

    private static void ValidateTimingPlayerReferenceDictionaryValues(
        object? payload,
        string payloadKey,
        string payloadLabel,
        string itemLabel,
        string valueLabel,
        IReadOnlySet<string>? knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        if (knownPlayerIds is null)
        {
            return;
        }

        if (!TryReadObjectValue(payload, payloadKey, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload)
            || !IsSnapshotStringMapPayloadObject(mapPayload))
        {
            return;
        }

        foreach (var (mapKey, rawValue) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            if (string.IsNullOrWhiteSpace(mapKey)
                || !TryReadStringValue(rawValue, out var playerId)
                || string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var normalizedKey = mapKey.Trim();
            ValidateTimingPlayerReference(
                $"{payloadLabel} {itemLabel} {normalizedKey} {valueLabel}",
                playerId,
                knownPlayerIds,
                knownPlayerLabel,
                errors);
        }
    }
}
