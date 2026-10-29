using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateContinuousEffectLayerEngineResidualConsistency(
        object? effectPayload,
        string effectLabel,
        string? layer,
        string? layerEngineStatus,
        List<string> errors)
    {
        var hasFoundationOnlyStatus = string.Equals(layerEngineStatus, "FOUNDATION_ONLY", StringComparison.Ordinal);
        var isStaticAuraLayer = string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal);
        if (!TryReadObjectValue(effectPayload, "deferredLayerEngineResiduals", out var residualPayload)
            || IsNullSnapshotPayloadValue(residualPayload))
        {
            if (hasFoundationOnlyStatus || isStaticAuraLayer)
            {
                errors.Add($"{effectLabel} deferred LayerEngine residual list is required");
            }

            if (isStaticAuraLayer && !hasFoundationOnlyStatus)
            {
                errors.Add($"{effectLabel} static aura requires foundation-only layer engine status");
            }

            return;
        }

        if (!TryReadStringListValue(residualPayload, out var residuals))
        {
            if (isStaticAuraLayer && !hasFoundationOnlyStatus)
            {
                errors.Add($"{effectLabel} static aura requires foundation-only layer engine status");
            }

            return;
        }

        if (residuals.Count == 0)
        {
            errors.Add($"{effectLabel} deferred LayerEngine residual list is required");
        }

        if (!hasFoundationOnlyStatus)
        {
            errors.Add($"{effectLabel} deferred LayerEngine residuals require foundation-only layer engine status");
            if (isStaticAuraLayer)
            {
                errors.Add($"{effectLabel} static aura requires foundation-only layer engine status");
            }
        }
    }

    private static void AddSpectatorTriggerQueueStringAggregateMismatch(
        string fieldLabel,
        IEnumerable<string?> expectedValues,
        IReadOnlyList<object?> triggerPayloads,
        string key,
        List<string> errors)
    {
        AddSpectatorTriggerQueueAggregateMismatch(
            fieldLabel,
            expectedValues.Select(value => FormatRecoveryDiagnosticValue(value ?? string.Empty)).ToArray(),
            ExtractObjectStringDiagnosticValues(triggerPayloads, key),
            errors);
    }

    private static void AddSpectatorTriggerQueueAggregateMismatch(
        string fieldLabel,
        IReadOnlyList<string> expectedValues,
        IReadOnlyList<string> actualValues,
        List<string> errors)
    {
        errors.Add(
            $"spectator replay frame timing trigger queue {fieldLabel} disagree with authoritative state trigger queue {fieldLabel}; {FormatExpectedActualForRecovery(expectedValues, actualValues)}");
    }

    private static IReadOnlyDictionary<string, TriggerQueueItemState> BuildAuthoritativeTriggerQueueById(
        IReadOnlyList<TriggerQueueItemState> authoritativeTriggers)
    {
        var authoritativeTriggersById = new Dictionary<string, TriggerQueueItemState>(StringComparer.Ordinal);
        foreach (var authoritativeTrigger in authoritativeTriggers)
        {
            if (string.IsNullOrWhiteSpace(authoritativeTrigger.TriggerId))
            {
                continue;
            }

            var normalizedTriggerId = authoritativeTrigger.TriggerId.Trim();
            if (!authoritativeTriggersById.ContainsKey(normalizedTriggerId))
            {
                authoritativeTriggersById.Add(normalizedTriggerId, authoritativeTrigger);
            }
        }

        return authoritativeTriggersById;
    }

    private static void ReportMissingSpectatorTriggerQueueAuthoritativeKeySet(
        IReadOnlyList<TriggerQueueItemState> authoritativeTriggers,
        List<string> errors)
    {
        ValidateSpectatorTriggerQueueAuthoritativeKeySet(
            Array.Empty<object?>(),
            BuildAuthoritativeTriggerQueueById(authoritativeTriggers),
            errors);
    }

    private static void ValidateSpectatorTriggerQueueAuthoritativeKeySet(
        IReadOnlyList<object?> triggerPayloads,
        IReadOnlyDictionary<string, TriggerQueueItemState> authoritativeTriggersById,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing trigger queue item";
        var actualTriggerIds = new HashSet<string>(StringComparer.Ordinal);
        var expectedTriggerIds = authoritativeTriggersById.Keys
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        foreach (var triggerPayload in triggerPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(triggerPayload)
                || !TryReadObjectString(triggerPayload, "triggerId", out var triggerId)
                || string.IsNullOrWhiteSpace(triggerId))
            {
                continue;
            }

            var normalizedTriggerId = triggerId.Trim();
            actualTriggerIds.Add(normalizedTriggerId);
            if (!authoritativeTriggersById.ContainsKey(normalizedTriggerId))
            {
                errors.Add(
                    $"{payloadLabel} trigger id {normalizedTriggerId} is not present in authoritative state trigger queue; {FormatExpectedActualForRecovery(expectedTriggerIds, normalizedTriggerId)}");
            }
        }

        var actualTriggerIdValues = actualTriggerIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        foreach (var authoritativeTriggerId in expectedTriggerIds)
        {
            if (!actualTriggerIds.Contains(authoritativeTriggerId))
            {
                errors.Add(
                    $"{payloadLabel} trigger id {authoritativeTriggerId} is required by authoritative state trigger queue; {FormatExpectedActualForRecovery(expectedTriggerIds, actualTriggerIdValues)}");
            }
        }
    }

    private static void ValidateSpectatorTriggerQueueKeyedRequiredStringValue(
        object? triggerPayload,
        string key,
        string fieldLabel,
        string expected,
        string triggerId,
        List<string> errors)
    {
        if (!TryReadObjectString(triggerPayload, key, out var actual))
        {
            var detail = FormatExpectedActualForRecovery(
                expected,
                FormatUnreadableObjectValue(triggerPayload, key));
            errors.Add(
                $"spectator replay frame timing trigger queue item {fieldLabel} does not match authoritative state trigger queue {fieldLabel} {expected} for trigger id {triggerId}; {detail}");
            return;
        }

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            var detail = FormatExpectedActualForRecovery(expected, actual);
            errors.Add(
                $"spectator replay frame timing trigger queue item {fieldLabel} {actual} does not match authoritative state trigger queue {fieldLabel} {expected} for trigger id {triggerId}; {detail}");
        }
    }

    private static void AddSpectatorTriggerQueueCountMismatch(
        int spectatorCount,
        int authoritativeCount,
        List<string> errors)
    {
        errors.Add(
            $"spectator replay frame timing trigger queue count {spectatorCount} does not match authoritative state trigger queue count {authoritativeCount}; {FormatExpectedActualForRecovery(authoritativeCount, spectatorCount)}");
    }

    private static void ValidateTriggerQueueSourceRedactionConsistency(
        string payloadLabel,
        string? sourceObjectId,
        string? sourceVisibility,
        string? effectKind,
        List<string> errors,
        bool publicCapturedEffect = false)
    {
        if (string.Equals(sourceVisibility, "HIDDEN", StringComparison.Ordinal))
        {
            if (sourceObjectId is not null
                && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} hidden source object id must be redacted; {FormatExpectedActualForRecovery("HIDDEN", sourceObjectId)}");
            }

            if (effectKind is not null
                && !string.Equals(effectKind, "HIDDEN", StringComparison.Ordinal)
                && !publicCapturedEffect)
            {
                errors.Add(
                    $"{payloadLabel} hidden effect kind must be redacted; {FormatExpectedActualForRecovery("HIDDEN", effectKind)}");
            }

            return;
        }

        if (string.Equals(sourceVisibility, "VISIBLE", StringComparison.Ordinal))
        {
            if (string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} visible source object id must not be redacted; {FormatExpectedActualForRecovery("<non-HIDDEN>", sourceObjectId)}");
            }

            if (string.Equals(effectKind, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} visible effect kind must not be redacted; {FormatExpectedActualForRecovery("<non-HIDDEN>", effectKind)}");
            }
        }
    }

    private static void ValidateTriggerQueueIdentityRedactionSentinelAbsence(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? triggeredEventKind,
        List<string> errors)
    {
        if (string.Equals(triggerId, "HIDDEN", StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} trigger id must not be redacted; {FormatExpectedActualForRecovery("<non-HIDDEN>", triggerId)}");
        }

        if (string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} controller id must not be redacted; {FormatExpectedActualForRecovery("<non-HIDDEN>", controllerId)}");
        }

        if (string.Equals(triggeredEventKind, "HIDDEN", StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} triggered event kind must not be redacted; {FormatExpectedActualForRecovery("<non-HIDDEN>", triggeredEventKind)}");
        }
    }

    private static void ValidateTriggerQueueTriggeredEventKindKnownValue(
        string payloadLabel,
        string? triggeredEventKind,
        List<string> errors)
    {
        if (triggeredEventKind is null
            || string.Equals(triggeredEventKind, "HIDDEN", StringComparison.Ordinal)
            || IsKnownTriggerQueueTriggeredEventKind(triggeredEventKind))
        {
            return;
        }

        errors.Add(
            $"{payloadLabel} triggered event kind {triggeredEventKind} is invalid; {FormatExpectedActualForRecovery(KnownTriggerQueueTriggeredEventKinds, triggeredEventKind)}; additional supported trigger event kinds [{string.Join(", ", AdditionalTriggerQueueTriggeredEventKinds)}]");
    }

    private static void ValidateTriggerQueueTriggeredEventKindMustBe(
        string payloadLabel,
        string diagnosticName,
        string? triggeredEventKind,
        string expectedTriggeredEventKind,
        List<string> errors)
    {
        if (triggeredEventKind is null
            || string.Equals(triggeredEventKind, "HIDDEN", StringComparison.Ordinal)
            || string.Equals(triggeredEventKind, expectedTriggeredEventKind, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(
            $"{payloadLabel} {diagnosticName} triggered event kind {triggeredEventKind} must be {expectedTriggeredEventKind}; {FormatExpectedActualForRecovery(expectedTriggeredEventKind, triggeredEventKind)}");
    }

    private static void ValidateTriggerQueueEffectKindMustBe(
        string payloadLabel,
        string diagnosticName,
        string? effectKind,
        string expectedEffectKind,
        List<string> errors)
    {
        if (effectKind is null
            || string.Equals(effectKind, "HIDDEN", StringComparison.Ordinal)
            || string.Equals(effectKind, expectedEffectKind, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(
            $"{payloadLabel} {diagnosticName} effect kind {effectKind} must be {expectedEffectKind}; {FormatExpectedActualForRecovery(expectedEffectKind, effectKind)}");
    }

    private static void ValidateTriggerQueueSourceObjectIdMustMatchTriggerIdSourceObjectId(
        string payloadLabel,
        string diagnosticName,
        string? sourceObjectId,
        string expectedSourceObjectId,
        List<string> errors)
    {
        if (sourceObjectId is null
            || string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            || string.Equals(sourceObjectId, expectedSourceObjectId, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(
            $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} must match trigger id source object id {expectedSourceObjectId}; {FormatExpectedActualForRecovery(expectedSourceObjectId, sourceObjectId)}");
    }

    private static void AddTriggerQueueTriggerControllerMismatch(
        string payloadLabel,
        string diagnosticName,
        string subjectLabel,
        string actualControllerId,
        string expectedControllerId,
        string contextLabel,
        List<string> errors)
    {
        errors.Add(
            $"{payloadLabel} {diagnosticName} {subjectLabel} {actualControllerId} must match trigger controller id {expectedControllerId} in {contextLabel}; {FormatExpectedActualForRecovery(expectedControllerId, actualControllerId)}");
    }

    private static void AddTriggerQueueSourceCardNoMismatch(
        string payloadLabel,
        string diagnosticName,
        string sourceObjectId,
        string actualCardNoLabel,
        string expectedCardNoLabel,
        string contextLabel,
        List<string> errors)
    {
        errors.Add(
            $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} card no {actualCardNoLabel} must be {expectedCardNoLabel} in {contextLabel}; {FormatExpectedActualForRecovery(expectedCardNoLabel, actualCardNoLabel)}");
    }

    private static void AddTriggerQueueSourceCardSpecMismatch(
        string payloadLabel,
        string diagnosticName,
        string sourceObjectId,
        string actualCardNoLabel,
        string expectedSpecLabel,
        string contextLabel,
        List<string> errors)
    {
        errors.Add(
            $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} card no {actualCardNoLabel} must reference {expectedSpecLabel} in {contextLabel}; {FormatExpectedActualForRecovery(expectedSpecLabel, actualCardNoLabel)}");
    }

    private static string ExpectedSourceCardNoLabelForAbilityId(string abilityId)
    {
        return P4ActivatedAbilityCatalog.TryGetByAbilityId(abilityId, out var definition)
            ? string.Join(" or ", P4ActivatedAbilityCatalog.SourceCardNosForAbility(definition))
            : abilityId;
    }

    private static void AddTriggerQueueSourceObjectIdBeforeEffectKindMismatch(
        string payloadLabel,
        string diagnosticName,
        string sourceObjectId,
        string triggerId,
        string expectedEffectKind,
        IEnumerable<string>? knownObjectIds,
        List<string> errors)
    {
        var expectedSourceObjectId = TryFindTriggerQueueSourceObjectIdBeforeEffectKindForRecovery(
            triggerId,
            expectedEffectKind,
            knownObjectIds,
            out var triggerSourceObjectId)
            ? triggerSourceObjectId
            : $"<trigger id source object before {expectedEffectKind}>";
        errors.Add(
            $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} must match trigger id source object id before {expectedEffectKind}; {FormatExpectedActualForRecovery(expectedSourceObjectId, sourceObjectId)}");
    }

    private static bool TryFindTriggerQueueSourceObjectIdBeforeEffectKindForRecovery(
        string triggerId,
        string expectedEffectKind,
        IEnumerable<string>? knownObjectIds,
        out string sourceObjectId)
    {
        sourceObjectId = string.Empty;
        var effectMarker = $"-{expectedEffectKind}";
        var effectMarkerIndex = triggerId.LastIndexOf(effectMarker, StringComparison.Ordinal);
        if (effectMarkerIndex <= StandardTriggerIdPrefixForRecovery.Length || knownObjectIds is null)
        {
            return false;
        }

        var triggerPrefix = triggerId[..effectMarkerIndex];
        foreach (var knownObjectId in knownObjectIds
                     .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                     .OrderByDescending(candidate => candidate.Length)
                     .ThenBy(candidate => candidate, StringComparer.Ordinal))
        {
            if (triggerPrefix.EndsWith($"-{knownObjectId}", StringComparison.Ordinal))
            {
                sourceObjectId = knownObjectId;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string>? GetTriggerQueueKnownObjectIdsForSourceContext(
        IReadOnlyDictionary<string, string>? objectControllers,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations)
    {
        return objectControllers?.Keys ?? objectCardNos?.Keys ?? objectLocations?.Keys;
    }

    private static void ValidateTriggerQueueBlueSentinelDelayedResourceContext(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? sourceObjectId,
        string? sourceVisibility,
        IReadOnlyDictionary<string, string>? objectControllers,
        string objectControllerLabel,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        string objectCardNoLabel,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        string objectFaceDownLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        string objectTagLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerBattlefieldObjectIdsByPlayer,
        string playerZoneLabel,
        IReadOnlySet<string>? battlefieldStateObjectIds,
        string battlefieldStateLabel,
        int? currentTurnNumber,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        if (triggerId is null || !IsBlueSentinelDelayedTriggerIdForRecovery(triggerId))
        {
            return;
        }

        if (!TryReadBlueSentinelDelayedTriggerContextForRecovery(
                triggerId,
                out var capturedTurnNumber,
                out var expectedSourceObjectId,
                out var battlefieldObjectId))
        {
            errors.Add($"{payloadLabel} blue sentinel delayed resource trigger id is invalid");
            return;
        }

        if (currentTurnNumber is { } turnNumber
            && turnNumber > 0
            && capturedTurnNumber > turnNumber)
        {
            errors.Add(
                $"{payloadLabel} blue sentinel delayed resource captured turn number {capturedTurnNumber} cannot be greater than current turn {turnNumber}");
        }
        else if (currentTurnNumber is { } staleTurnNumber
            && staleTurnNumber > 1
            && capturedTurnNumber < staleTurnNumber - 1)
        {
            errors.Add(
                $"{payloadLabel} blue sentinel delayed resource captured turn number {capturedTurnNumber} cannot be earlier than previous turn {staleTurnNumber - 1} for current turn {staleTurnNumber}");
        }

        if (controllerId is not null
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && objectControllers is not null
            && objectControllers.TryGetValue(expectedSourceObjectId, out var sourceControllerId)
            && !string.Equals(sourceControllerId, controllerId, StringComparison.Ordinal))
        {
            AddTriggerQueueTriggerControllerMismatch(
                payloadLabel,
                "blue sentinel delayed resource",
                $"source object id {expectedSourceObjectId} controller id",
                sourceControllerId,
                controllerId,
                objectControllerLabel,
                errors);
        }

        if (objectCardNos is not null
            && objectCardNos.TryGetValue(expectedSourceObjectId, out var sourceCardNo)
            && !P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(
                P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId,
                sourceCardNo))
        {
            var sourceCardNoLabel = sourceCardNo is null ? "<null>" : sourceCardNo;
            AddTriggerQueueSourceCardNoMismatch(
                payloadLabel,
                "blue sentinel delayed resource",
                expectedSourceObjectId,
                sourceCardNoLabel,
                ExpectedSourceCardNoLabelForAbilityId(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId),
                objectCardNoLabel,
                errors);
        }

        if (objectFaceDowns is not null
            && objectFaceDowns.TryGetValue(expectedSourceObjectId, out var isFaceDown)
            && isFaceDown)
        {
            errors.Add(
                $"{payloadLabel} blue sentinel delayed resource source object id {expectedSourceObjectId} must not be face down in {objectFaceDownLabel}; {FormatExpectedActualForRecovery(false, isFaceDown)}");
        }

        if (objectTags is not null
            && objectTags.TryGetValue(expectedSourceObjectId, out var sourceTags)
            && !sourceTags.Contains(CardObjectTags.UnitCard))
        {
            errors.Add(
                $"{payloadLabel} blue sentinel delayed resource source object id {expectedSourceObjectId} must be a unit card in {objectTagLabel}; {FormatExpectedActualForRecovery(CardObjectTags.UnitCard, FormatObjectTagsForRecovery(sourceTags))}");
        }

        if (battlefieldStateObjectIds is not null
            && !battlefieldStateObjectIds.Contains(battlefieldObjectId))
        {
            errors.Add(
                $"{payloadLabel} blue sentinel delayed resource battlefield object id {battlefieldObjectId} is missing from {battlefieldStateLabel}; {FormatExpectedActualForRecovery(FormatObjectIdsForRecovery(battlefieldStateObjectIds), battlefieldObjectId)}");
        }

        if (controllerId is not null
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && (battlefieldStateObjectIds is null || battlefieldStateObjectIds.Contains(battlefieldObjectId))
            && objectControllers is not null
            && objectControllers.TryGetValue(battlefieldObjectId, out var battlefieldControllerId)
            && !string.Equals(battlefieldControllerId, controllerId, StringComparison.Ordinal))
        {
            AddTriggerQueueTriggerControllerMismatch(
                payloadLabel,
                "blue sentinel delayed resource",
                $"battlefield object id {battlefieldObjectId} controller id",
                battlefieldControllerId,
                controllerId,
                objectControllerLabel,
                errors);
        }

        ValidateBlueSentinelDelayedResourceSourceLocationForRecovery(
            payloadLabel,
            expectedSourceObjectId,
            battlefieldObjectId,
            objectLocations,
            objectLocationLabel,
            battlefieldStateObjectIds,
            errors);
        if (controllerId is not null)
        {
            ValidateBlueSentinelDelayedResourceSourcePlayerZoneMembershipForRecovery(
                payloadLabel,
                expectedSourceObjectId,
                battlefieldObjectId,
                controllerId,
                objectControllers,
                objectLocations,
                playerBattlefieldObjectIdsByPlayer,
                playerZoneLabel,
                battlefieldStateObjectIds,
                errors);
        }

        ValidateTriggerQueueSourceVisibilityMustBeVisible(
            payloadLabel,
            "blue sentinel delayed resource",
            sourceVisibility,
            errors);

        ValidateTriggerQueueSourceObjectIdMustMatchTriggerIdSourceObjectId(
            payloadLabel,
            "blue sentinel delayed resource",
            sourceObjectId,
            expectedSourceObjectId,
            errors);

        ValidateTriggerQueueEffectKindMustBe(
            payloadLabel,
            "blue sentinel delayed resource",
            effectKind,
            P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityEffectKind,
            errors);

        ValidateTriggerQueueTriggeredEventKindMustBe(
            payloadLabel,
            "blue sentinel delayed resource",
            triggeredEventKind,
            "BATTLEFIELD_HELD",
            errors);
    }

    private static void ValidateBlueSentinelDelayedResourceSourceLocationForRecovery(
        string payloadLabel,
        string sourceObjectId,
        string battlefieldObjectId,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        IReadOnlySet<string>? battlefieldStateObjectIds,
        List<string> errors)
    {
        if (objectLocations is null
            || !objectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            || string.IsNullOrWhiteSpace(sourceLocation.Zone))
        {
            return;
        }

        var sourceZone = sourceLocation.Zone.Trim();
        if (!string.Equals(sourceZone, "BATTLEFIELD", StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} blue sentinel delayed resource source object id {sourceObjectId} location zone {sourceZone} must be BATTLEFIELD in {objectLocationLabel}; {FormatExpectedActualForRecovery("BATTLEFIELD", sourceZone)}");
            return;
        }

        if (battlefieldStateObjectIds is not null
            && !battlefieldStateObjectIds.Contains(battlefieldObjectId))
        {
            return;
        }

        if (!string.Equals(sourceLocation.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
        {
            var actualBattlefieldObjectId = string.IsNullOrWhiteSpace(sourceLocation.BattlefieldObjectId)
                ? "<null>"
                : sourceLocation.BattlefieldObjectId;
            errors.Add(
                $"{payloadLabel} blue sentinel delayed resource source object id {sourceObjectId} battlefield object id {actualBattlefieldObjectId} must match trigger id battlefield object id {battlefieldObjectId} in {objectLocationLabel}; {FormatExpectedActualForRecovery(battlefieldObjectId, actualBattlefieldObjectId)}");
        }
    }

    private static void ValidateBlueSentinelDelayedResourceSourcePlayerZoneMembershipForRecovery(
        string payloadLabel,
        string sourceObjectId,
        string battlefieldObjectId,
        string controllerId,
        IReadOnlyDictionary<string, string>? objectControllers,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerBattlefieldObjectIdsByPlayer,
        string playerZoneLabel,
        IReadOnlySet<string>? battlefieldStateObjectIds,
        List<string> errors)
    {
        if (string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            || playerBattlefieldObjectIdsByPlayer is null
            || !playerBattlefieldObjectIdsByPlayer.TryGetValue(controllerId, out var battlefieldObjectIds)
            || objectControllers is null
            || !objectControllers.TryGetValue(sourceObjectId, out var sourceControllerId)
            || !string.Equals(sourceControllerId, controllerId, StringComparison.Ordinal)
            || objectLocations is null
            || !objectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            || string.IsNullOrWhiteSpace(sourceLocation.Zone))
        {
            return;
        }

        if (battlefieldStateObjectIds is not null
            && !battlefieldStateObjectIds.Contains(battlefieldObjectId))
        {
            return;
        }

        var sourceZone = sourceLocation.Zone.Trim();
        if (!string.Equals(sourceZone, "BATTLEFIELD", StringComparison.Ordinal)
            || !string.Equals(sourceLocation.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
            || battlefieldObjectIds.Contains(sourceObjectId))
        {
            return;
        }

        errors.Add(
            $"{payloadLabel} blue sentinel delayed resource source object id {sourceObjectId} must be in trigger controller battlefield zone in {playerZoneLabel}; {FormatExpectedActualForRecovery($"contains {sourceObjectId}", FormatObjectIdsForRecovery(battlefieldObjectIds))}");
    }

    private static bool IsBlueSentinelDelayedTriggerIdForRecovery(string triggerId)
    {
        return string.Equals(triggerId, BlueSentinelDelayedTriggerIdPrefixForRecovery, StringComparison.Ordinal)
            || triggerId.StartsWith($"{BlueSentinelDelayedTriggerIdPrefixForRecovery}::", StringComparison.Ordinal);
    }

    private static void ValidateTriggerQueueJhinMovementResourceContext(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? sourceObjectId,
        IReadOnlyDictionary<string, string>? objectControllers,
        string objectControllerLabel,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        string objectCardNoLabel,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        string objectFaceDownLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        string objectTagLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        IReadOnlySet<string>? battlefieldStateObjectIds,
        string battlefieldStateLabel,
        string? effectKind,
        string? triggeredEventKind,
        long currentTick,
        List<string> errors)
    {
        if (triggerId is null || !IsJhinMovementResourceTriggerIdForRecovery(triggerId))
        {
            return;
        }

        if (!TryReadJhinMovementTriggerContextForRecovery(
                triggerId,
                out var triggerTick,
                out var expectedSourceObjectId,
                out var origin,
                out var destination))
        {
            errors.Add($"{payloadLabel} jhin movement resource trigger id is invalid");
            return;
        }

        if (triggerTick > currentTick)
        {
            errors.Add(
                $"{payloadLabel} jhin movement resource trigger tick {triggerTick} cannot be greater than current tick {currentTick}");
        }

        if (string.Equals(origin, destination, StringComparison.Ordinal))
        {
            errors.Add($"{payloadLabel} jhin movement resource origin {origin} and destination {destination} must differ");
        }

        if (!IsJhinMovementEndpointForRecovery(origin))
        {
            errors.Add(
                $"{payloadLabel} jhin movement resource origin {origin} must be BASE, BATTLEFIELD, or BATTLEFIELD:<battlefieldObjectId>");
        }

        if (!IsJhinMovementEndpointForRecovery(destination))
        {
            errors.Add(
                $"{payloadLabel} jhin movement resource destination {destination} must be BASE, BATTLEFIELD, or BATTLEFIELD:<battlefieldObjectId>");
        }

        ValidateJhinMovementPreciseBattlefieldEndpointForRecovery(
            payloadLabel,
            "origin",
            origin,
            battlefieldStateObjectIds,
            battlefieldStateLabel,
            errors);
        ValidateJhinMovementPreciseBattlefieldEndpointForRecovery(
            payloadLabel,
            "destination",
            destination,
            battlefieldStateObjectIds,
            battlefieldStateLabel,
            errors);
        ValidateJhinMovementDestinationLocationForRecovery(
            payloadLabel,
            expectedSourceObjectId,
            destination,
            objectLocations,
            objectLocationLabel,
            errors);

        ValidateTriggerQueueSourceObjectIdMustMatchTriggerIdSourceObjectId(
            payloadLabel,
            "jhin movement resource",
            sourceObjectId,
            expectedSourceObjectId,
            errors);

        if (controllerId is not null
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && objectControllers is not null
            && objectControllers.TryGetValue(expectedSourceObjectId, out var sourceControllerId)
            && !string.Equals(sourceControllerId, controllerId, StringComparison.Ordinal))
        {
            AddTriggerQueueTriggerControllerMismatch(
                payloadLabel,
                "jhin movement resource",
                $"source object id {expectedSourceObjectId} controller id",
                sourceControllerId,
                controllerId,
                objectControllerLabel,
                errors);
        }

        if (objectCardNos is not null
            && objectCardNos.TryGetValue(expectedSourceObjectId, out var sourceCardNo)
            && !P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(
                P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId,
                sourceCardNo))
        {
            var sourceCardNoLabel = sourceCardNo is null ? "<null>" : sourceCardNo;
            AddTriggerQueueSourceCardNoMismatch(
                payloadLabel,
                "jhin movement resource",
                expectedSourceObjectId,
                sourceCardNoLabel,
                ExpectedSourceCardNoLabelForAbilityId(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId),
                objectCardNoLabel,
                errors);
        }

        if (objectFaceDowns is not null
            && objectFaceDowns.TryGetValue(expectedSourceObjectId, out var isFaceDown)
            && isFaceDown)
        {
            errors.Add(
                $"{payloadLabel} jhin movement resource source object id {expectedSourceObjectId} must not be face down in {objectFaceDownLabel}; {FormatExpectedActualForRecovery(false, isFaceDown)}");
        }

        if (objectTags is not null
            && objectTags.TryGetValue(expectedSourceObjectId, out var sourceTags)
            && !sourceTags.Contains(CardObjectTags.UnitCard))
        {
            errors.Add(
                $"{payloadLabel} jhin movement resource source object id {expectedSourceObjectId} must be a unit card in {objectTagLabel}; {FormatExpectedActualForRecovery(CardObjectTags.UnitCard, FormatObjectTagsForRecovery(sourceTags))}");
        }


        ValidateTriggerQueueEffectKindMustBe(
            payloadLabel,
            "jhin movement resource",
            effectKind,
            P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind,
            errors);

        var expectedTriggeredEventKind = destination.StartsWith("BATTLEFIELD", StringComparison.Ordinal)
            ? "UNIT_MOVED_TO_BATTLEFIELD"
            : "UNIT_MOVED_TO_BASE";
        ValidateTriggerQueueTriggeredEventKindMustBe(
            payloadLabel,
            "jhin movement resource",
            triggeredEventKind,
            expectedTriggeredEventKind,
            errors);
    }

    private static bool IsJhinMovementResourceTriggerIdForRecovery(string triggerId)
    {
        return string.Equals(triggerId, JhinMovementResourceTriggerIdPrefixForRecovery, StringComparison.Ordinal)
            || triggerId.StartsWith($"{JhinMovementResourceTriggerIdPrefixForRecovery}::", StringComparison.Ordinal);
    }

    private static void ValidateJhinMovementPreciseBattlefieldEndpointForRecovery(
        string payloadLabel,
        string endpointLabel,
        string endpoint,
        IReadOnlySet<string>? battlefieldStateObjectIds,
        string battlefieldStateLabel,
        List<string> errors)
    {
        if (battlefieldStateObjectIds is null
            || !TryReadJhinPreciseBattlefieldEndpointForRecovery(endpoint, out var battlefieldObjectId))
        {
            return;
        }

        if (!battlefieldStateObjectIds.Contains(battlefieldObjectId))
        {
            errors.Add(
                $"{payloadLabel} jhin movement resource {endpointLabel} battlefield object id {battlefieldObjectId} is missing from {battlefieldStateLabel}; {FormatExpectedActualForRecovery(FormatObjectIdsForRecovery(battlefieldStateObjectIds), battlefieldObjectId)}");
        }
    }

    private static void ValidateJhinMovementDestinationLocationForRecovery(
        string payloadLabel,
        string sourceObjectId,
        string destination,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        List<string> errors)
    {
        if (objectLocations is null
            || !objectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            || string.IsNullOrWhiteSpace(sourceLocation.Zone))
        {
            return;
        }

        var sourceZone = sourceLocation.Zone.Trim();
        if (string.Equals(destination, "BASE", StringComparison.Ordinal))
        {
            if (!string.Equals(sourceZone, "BASE", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} jhin movement resource destination BASE does not match source object id {sourceObjectId} location {sourceZone} in {objectLocationLabel}");
            }

            return;
        }

        if (string.Equals(destination, "BATTLEFIELD", StringComparison.Ordinal))
        {
            if (!string.Equals(sourceZone, "BATTLEFIELD", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} jhin movement resource destination BATTLEFIELD does not match source object id {sourceObjectId} location {sourceZone} in {objectLocationLabel}");
            }

            return;
        }

        if (!TryReadJhinPreciseBattlefieldEndpointForRecovery(destination, out var destinationBattlefieldObjectId))
        {
            return;
        }

        if (!string.Equals(sourceZone, "BATTLEFIELD", StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} jhin movement resource destination {destination} does not match source object id {sourceObjectId} location {sourceZone} in {objectLocationLabel}");
            return;
        }

        if (!string.Equals(sourceLocation.BattlefieldObjectId, destinationBattlefieldObjectId, StringComparison.Ordinal))
        {
            var actualBattlefieldObjectId = string.IsNullOrWhiteSpace(sourceLocation.BattlefieldObjectId)
                ? "<null>"
                : sourceLocation.BattlefieldObjectId;
            errors.Add(
                $"{payloadLabel} jhin movement resource destination {destination} does not match source object id {sourceObjectId} battlefield object id {actualBattlefieldObjectId} in {objectLocationLabel}");
        }
    }

    private static bool IsJhinMovementEndpointForRecovery(string endpoint)
    {
        if (string.Equals(endpoint, "BASE", StringComparison.Ordinal)
            || string.Equals(endpoint, "BATTLEFIELD", StringComparison.Ordinal))
        {
            return true;
        }

        const string preciseBattlefieldPrefix = "BATTLEFIELD:";
        return endpoint.StartsWith(preciseBattlefieldPrefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(endpoint[preciseBattlefieldPrefix.Length..]);
    }

    private static bool TryReadJhinPreciseBattlefieldEndpointForRecovery(
        string endpoint,
        out string battlefieldObjectId)
    {
        const string preciseBattlefieldPrefix = "BATTLEFIELD:";
        battlefieldObjectId = string.Empty;
        if (!endpoint.StartsWith(preciseBattlefieldPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        battlefieldObjectId = endpoint[preciseBattlefieldPrefix.Length..];
        return !string.IsNullOrWhiteSpace(battlefieldObjectId);
    }

    private static bool TryReadJhinMovementTriggerContextForRecovery(
        string triggerId,
        out long tick,
        out string sourceObjectId,
        out string origin,
        out string destination)
    {
        tick = 0;
        sourceObjectId = string.Empty;
        origin = string.Empty;
        destination = string.Empty;
        var parts = triggerId.Split("::", StringSplitOptions.None);
        if (parts.Length != 5
            || !string.Equals(parts[0], JhinMovementResourceTriggerIdPrefixForRecovery, StringComparison.Ordinal)
            || !long.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out tick)
            || string.IsNullOrWhiteSpace(parts[2])
            || string.IsNullOrWhiteSpace(parts[3])
            || string.IsNullOrWhiteSpace(parts[4]))
        {
            return false;
        }

        sourceObjectId = parts[2];
        origin = parts[3];
        destination = parts[4];
        return tick > 0;
    }

    private static void ValidateTriggerQueueKogmawLastBreathContext(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? sourceObjectId,
        string? sourceVisibility,
        IReadOnlyDictionary<string, string>? objectControllers,
        string objectControllerLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        string objectCardNoLabel,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        string objectFaceDownLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        string objectTagLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        IReadOnlySet<string>? battlefieldStateObjectIds,
        string battlefieldStateLabel,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors, MatchState? capturedState = null, string? viewerPlayerId = null)
    {
        if (ValidateCapturedLastBreathBattlefieldDamageMetadata(capturedState, viewerPlayerId, payloadLabel, triggerId, controllerId,
            sourceObjectId, sourceVisibility, effectKind, triggeredEventKind, errors)) return;

        if (triggerId is null || !IsKogmawLastBreathTriggerIdForRecovery(triggerId, effectKind))
        {
            return;
        }

        if (!TryReadKogmawLastBreathTriggerBattlefieldObjectIdForRecovery(triggerId, out var battlefieldObjectId))
        {
            errors.Add($"{payloadLabel} kogmaw last breath trigger id is invalid");
            return;
        }

        var battlefieldObjectKnown = knownObjectIds is null || knownObjectIds.Contains(battlefieldObjectId);
        if (!battlefieldObjectKnown)
        {
            var expectedObjectIds = knownObjectIds!
                .OrderBy(knownObjectId => knownObjectId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"{payloadLabel} kogmaw last breath battlefield object id {battlefieldObjectId} is missing from {knownObjectLabel}; {FormatExpectedActualForRecovery(expectedObjectIds, battlefieldObjectId)}");
        }

        var battlefieldObjectIsBattlefieldCard = objectTags is null;
        if (battlefieldObjectKnown && objectTags is not null)
        {
            if (!objectTags.TryGetValue(battlefieldObjectId, out var battlefieldObjectTags))
            {
                battlefieldObjectIsBattlefieldCard = false;
                errors.Add(
                    $"{payloadLabel} kogmaw last breath battlefield object id {battlefieldObjectId} is missing from object tags; {FormatObjectTagPresenceForRecovery(objectTags, battlefieldObjectId)}");
            }
            else if (!battlefieldObjectTags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag))
            {
                battlefieldObjectIsBattlefieldCard = false;
                errors.Add(
                    $"{payloadLabel} kogmaw last breath battlefield object id {battlefieldObjectId} is not a battlefield card");
            }
            else
            {
                battlefieldObjectIsBattlefieldCard = true;
            }
        }

        if (battlefieldObjectKnown
            && battlefieldObjectIsBattlefieldCard
            && battlefieldStateObjectIds is not null
            && !battlefieldStateObjectIds.Contains(battlefieldObjectId))
        {
            errors.Add(
                $"{payloadLabel} kogmaw last breath battlefield object id {battlefieldObjectId} is missing from {battlefieldStateLabel}; {FormatExpectedActualForRecovery(FormatObjectIdsForRecovery(battlefieldStateObjectIds), battlefieldObjectId)}");
        }

        if (battlefieldObjectKnown
            && battlefieldObjectIsBattlefieldCard
            && (battlefieldStateObjectIds is null || battlefieldStateObjectIds.Contains(battlefieldObjectId))
            && objectLocations is not null
            && objectLocations.TryGetValue(battlefieldObjectId, out var battlefieldObjectLocation)
            && !string.IsNullOrWhiteSpace(battlefieldObjectLocation.Zone)
            && !string.Equals(battlefieldObjectLocation.Zone.Trim(), "BATTLEFIELD", StringComparison.Ordinal))
        {
            var battlefieldObjectZone = battlefieldObjectLocation.Zone.Trim();
            errors.Add(
                $"{payloadLabel} kogmaw last breath battlefield object id {battlefieldObjectId} location zone {battlefieldObjectZone} must be BATTLEFIELD in {objectLocationLabel}; {FormatExpectedActualForRecovery("BATTLEFIELD", battlefieldObjectZone)}");
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && !DoesKogmawLastBreathTriggerIdMatchSourceObjectIdForRecovery(triggerId, sourceObjectId))
        {
            AddTriggerQueueSourceObjectIdBeforeEffectKindMismatch(
                payloadLabel,
                "kogmaw last breath",
                sourceObjectId,
                triggerId,
                KogmawLastBreathAoeEffectKindForRecovery,
                knownObjectIds,
                errors);
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && triggerId.StartsWith(
                $"{StandardTriggerIdPrefixForRecovery}-{sourceObjectId}-",
                StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} kogmaw last breath trigger id stack item id is required before source object id {sourceObjectId}");
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectLocations is not null
            && objectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            && !string.IsNullOrWhiteSpace(sourceLocation.Zone)
            && !string.Equals(sourceLocation.Zone.Trim(), "GRAVEYARD", StringComparison.Ordinal))
        {
            var sourceZone = sourceLocation.Zone.Trim();
            errors.Add(
                $"{payloadLabel} kogmaw last breath source object id {sourceObjectId} location zone {sourceZone} must be GRAVEYARD in {objectLocationLabel}; {FormatExpectedActualForRecovery("GRAVEYARD", sourceZone)}");
        }

        if (controllerId is not null
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectControllers is not null
            && objectControllers.TryGetValue(sourceObjectId, out var sourceControllerId)
            && !string.Equals(sourceControllerId, controllerId, StringComparison.Ordinal))
        {
            AddTriggerQueueTriggerControllerMismatch(
                payloadLabel,
                "kogmaw last breath",
                $"source object id {sourceObjectId} controller id",
                sourceControllerId,
                controllerId,
                objectControllerLabel,
                errors);
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectCardNos is not null
            && objectCardNos.TryGetValue(sourceObjectId, out var sourceCardNo)
            && !UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathSourceBattlefieldAoeDamageTrigger,
                out _))
        {
            var sourceCardNoLabel = sourceCardNo is null ? "<null>" : sourceCardNo;
            AddTriggerQueueSourceCardSpecMismatch(
                payloadLabel,
                "kogmaw last breath",
                sourceObjectId,
                sourceCardNoLabel,
                KogmawLastBreathAoeSourceTriggerSpecLabelForRecovery,
                objectCardNoLabel,
                errors);
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectFaceDowns is not null
            && objectFaceDowns.TryGetValue(sourceObjectId, out var isFaceDown)
            && isFaceDown)
        {
            errors.Add(
                $"{payloadLabel} kogmaw last breath source object id {sourceObjectId} must not be face down in {objectFaceDownLabel}; {FormatExpectedActualForRecovery(false, isFaceDown)}");
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectTags is not null
            && objectTags.TryGetValue(sourceObjectId, out var sourceTags)
            && !sourceTags.Contains(CardObjectTags.UnitCard))
        {
            errors.Add(
                $"{payloadLabel} kogmaw last breath source object id {sourceObjectId} must be a unit card in {objectTagLabel}; {FormatExpectedActualForRecovery(CardObjectTags.UnitCard, FormatObjectTagsForRecovery(sourceTags))}");
        }

        ValidateTriggerQueueSourceVisibilityMustBeVisible(
            payloadLabel,
            "kogmaw last breath",
            sourceVisibility,
            errors);

        ValidateTriggerQueueEffectKindMustBe(
            payloadLabel,
            "kogmaw last breath",
            effectKind,
            KogmawLastBreathAoeEffectKindForRecovery,
            errors);

        ValidateTriggerQueueTriggeredEventKindMustBe(
            payloadLabel,
            "kogmaw last breath",
            triggeredEventKind,
            "UNIT_DESTROYED",
            errors);
    }

    private static bool IsKogmawLastBreathTriggerIdForRecovery(
        string triggerId,
        string? effectKind)
    {
        var marker = $"{KogmawLastBreathAoeEffectKindForRecovery}{KogmawTriggerBattlefieldMarkerForRecovery}";
        var markerIndex = triggerId.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return string.Equals(effectKind, KogmawLastBreathAoeEffectKindForRecovery, StringComparison.Ordinal);
        }

        return !IsNestedTriggerIdWithKogmawStackPrefixForRecovery(triggerId, effectKind, markerIndex);
    }

    private static bool DoesKogmawLastBreathTriggerIdMatchSourceObjectIdForRecovery(
        string triggerId,
        string sourceObjectId)
    {
        var marker = $"{KogmawLastBreathAoeEffectKindForRecovery}{KogmawTriggerBattlefieldMarkerForRecovery}";
        var markerIndex = triggerId.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return false;
        }

        return triggerId[..markerIndex].EndsWith($"-{sourceObjectId}-", StringComparison.Ordinal);
    }

    private static bool IsNestedTriggerIdWithKogmawStackPrefixForRecovery(
        string triggerId,
        string? effectKind,
        int markerIndex)
    {
        if (string.IsNullOrWhiteSpace(effectKind)
            || string.Equals(effectKind, "HIDDEN", StringComparison.Ordinal)
            || string.Equals(effectKind, KogmawLastBreathAoeEffectKindForRecovery, StringComparison.Ordinal))
        {
            return false;
        }

        var effectTail = $"-{effectKind}";
        var effectTailIndex = triggerId.LastIndexOf(effectTail, StringComparison.Ordinal);
        return effectTailIndex > markerIndex
            && effectTailIndex + effectTail.Length == triggerId.Length;
    }

    private static bool TryReadKogmawLastBreathTriggerBattlefieldObjectIdForRecovery(
        string triggerId,
        out string battlefieldObjectId)
    {
        battlefieldObjectId = string.Empty;
        var marker = $"{KogmawLastBreathAoeEffectKindForRecovery}{KogmawTriggerBattlefieldMarkerForRecovery}";
        var markerIndex = triggerId.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        battlefieldObjectId = triggerId[(markerIndex + marker.Length)..];
        return !string.IsNullOrWhiteSpace(battlefieldObjectId);
    }

    private static void ValidateTriggerQueueOgsLuxHighCostSpellContext(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? sourceObjectId,
        string? sourceVisibility,
        IReadOnlyDictionary<string, string>? objectControllers,
        string objectControllerLabel,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        string objectCardNoLabel,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        string objectFaceDownLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        string objectTagLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerFieldObjectIdsByPlayer,
        string playerZoneLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        if (triggerId is null || !IsOgsLuxHighCostSpellTriggerForRecovery(triggerId, effectKind))
        {
            return;
        }

        if (!IsOgsLuxHighCostSpellTriggerIdForRecovery(triggerId))
        {
            errors.Add($"{payloadLabel} ogs lux high cost spell trigger id is invalid");
            return;
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
        {
            var sourceObjectSuffix = $"-{sourceObjectId}-{OgsLuxHighCostSpellPowerEffectKindForRecovery}";
            if (!triggerId.EndsWith(sourceObjectSuffix, StringComparison.Ordinal))
            {
                AddTriggerQueueSourceObjectIdBeforeEffectKindMismatch(
                    payloadLabel,
                    "ogs lux high cost spell",
                    sourceObjectId,
                    triggerId,
                    OgsLuxHighCostSpellPowerEffectKindForRecovery,
                    GetTriggerQueueKnownObjectIdsForSourceContext(objectControllers, objectCardNos, objectLocations),
                    errors);
            }
            else if (triggerId.Length - sourceObjectSuffix.Length <= StandardTriggerIdPrefixForRecovery.Length)
            {
                errors.Add(
                    $"{payloadLabel} ogs lux high cost spell trigger id stack item id is required before source object id {sourceObjectId}");
            }

            if (controllerId is not null
                && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
                && objectControllers is not null
                && objectControllers.TryGetValue(sourceObjectId, out var sourceControllerId)
                && !string.Equals(sourceControllerId, controllerId, StringComparison.Ordinal))
            {
                AddTriggerQueueTriggerControllerMismatch(
                    payloadLabel,
                    "ogs lux high cost spell",
                    $"source object id {sourceObjectId} controller id",
                    sourceControllerId,
                    controllerId,
                    objectControllerLabel,
                    errors);
            }

            if (controllerId is not null
                && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
                && objectControllers is not null
                && objectControllers.TryGetValue(sourceObjectId, out var zoneSourceControllerId)
                && string.Equals(zoneSourceControllerId, controllerId, StringComparison.Ordinal)
                && playerFieldObjectIdsByPlayer is not null
                && playerFieldObjectIdsByPlayer.TryGetValue(controllerId, out var fieldObjectIds)
                && !fieldObjectIds.Contains(sourceObjectId))
            {
                errors.Add(
                    $"{payloadLabel} ogs lux high cost spell source object id {sourceObjectId} must be in trigger controller field zone in {playerZoneLabel}; {FormatExpectedActualForRecovery($"contains {sourceObjectId}", FormatObjectIdsForRecovery(fieldObjectIds))}");
            }

            if (objectCardNos is not null
                && objectCardNos.TryGetValue(sourceObjectId, out var sourceCardNo)
                && !SpellPlayedTriggerSpecRules.TryGetTrigger(
                    sourceCardNo,
                    SpellPlayedTriggerSpecRules.IsUnitHighCostSpellPowerModifierTrigger,
                    out _))
            {
                var sourceCardNoLabel = sourceCardNo is null ? "<null>" : sourceCardNo;
                AddTriggerQueueSourceCardSpecMismatch(
                    payloadLabel,
                    "ogs lux high cost spell",
                    sourceObjectId,
                    sourceCardNoLabel,
                    OgsLuxHighCostSpellSourceTriggerSpecLabelForRecovery,
                    objectCardNoLabel,
                    errors);
            }

            if (objectFaceDowns is not null
                && objectFaceDowns.TryGetValue(sourceObjectId, out var isFaceDown)
                && isFaceDown)
            {
                errors.Add(
                    $"{payloadLabel} ogs lux high cost spell source object id {sourceObjectId} must not be face down in {objectFaceDownLabel}; {FormatExpectedActualForRecovery(false, isFaceDown)}");
            }

            if (objectTags is not null
                && objectTags.TryGetValue(sourceObjectId, out var sourceTags)
                && !sourceTags.Contains(CardObjectTags.UnitCard))
            {
                errors.Add(
                    $"{payloadLabel} ogs lux high cost spell source object id {sourceObjectId} must be a unit card in {objectTagLabel}; {FormatExpectedActualForRecovery(CardObjectTags.UnitCard, FormatObjectTagsForRecovery(sourceTags))}");
            }


            if (objectTags is not null
                && objectTags.TryGetValue(sourceObjectId, out var equipmentSourceTags)
                && equipmentSourceTags.Contains(CardObjectTags.EquipmentCard))
            {
                errors.Add(
                    $"{payloadLabel} ogs lux high cost spell source object id {sourceObjectId} must not be an equipment card in {objectTagLabel}; {FormatExpectedActualForRecovery("<non-equipment card>", CardObjectTags.EquipmentCard)}");
            }

            if (objectLocations is not null
                && objectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
                && !string.IsNullOrWhiteSpace(sourceLocation.Zone))
            {
                var sourceZone = sourceLocation.Zone.Trim();
                if (!string.Equals(sourceZone, "BASE", StringComparison.Ordinal)
                    && !string.Equals(sourceZone, "BATTLEFIELD", StringComparison.Ordinal))
                {
                    errors.Add(
                        $"{payloadLabel} ogs lux high cost spell source object id {sourceObjectId} location zone {sourceZone} must be BASE or BATTLEFIELD in {objectLocationLabel}; {FormatExpectedActualForRecovery("BASE or BATTLEFIELD", sourceZone)}");
                }

                if (controllerId is not null
                    && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(sourceLocation.PlayerId))
                {
                    var sourcePlayerId = sourceLocation.PlayerId.Trim();
                    if (!string.Equals(sourcePlayerId, controllerId, StringComparison.Ordinal))
                    {
                        AddTriggerQueueTriggerControllerMismatch(
                            payloadLabel,
                            "ogs lux high cost spell",
                            $"source object id {sourceObjectId} location player id",
                            sourcePlayerId,
                            controllerId,
                            objectLocationLabel,
                            errors);
                    }
                }
            }
        }

        ValidateTriggerQueueSourceVisibilityMustBeVisible(
            payloadLabel,
            "ogs lux high cost spell",
            sourceVisibility,
            errors);

        ValidateTriggerQueueEffectKindMustBe(
            payloadLabel,
            "ogs lux high cost spell",
            effectKind,
            OgsLuxHighCostSpellPowerEffectKindForRecovery,
            errors);

        ValidateTriggerQueueTriggeredEventKindMustBe(
            payloadLabel,
            "ogs lux high cost spell",
            triggeredEventKind,
            "CARD_PLAYED",
            errors);
    }

    private static bool IsOgsLuxHighCostSpellTriggerForRecovery(
        string triggerId,
        string? effectKind)
    {
        return IsOgsLuxHighCostSpellTriggerIdForRecovery(triggerId)
            || string.Equals(effectKind, OgsLuxHighCostSpellPowerEffectKindForRecovery, StringComparison.Ordinal);
    }

    private static bool IsOgsLuxHighCostSpellTriggerIdForRecovery(string triggerId)
    {
        return triggerId.StartsWith(StandardTriggerIdPrefixForRecovery, StringComparison.Ordinal)
            && triggerId.EndsWith(
                $"-{OgsLuxHighCostSpellPowerEffectKindForRecovery}",
                StringComparison.Ordinal);
    }

    private static void ValidateTriggerQueueTeemoOnPlaySelfPowerContext(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? sourceObjectId,
        string? sourceVisibility,
        IReadOnlyDictionary<string, string>? objectControllers,
        string objectControllerLabel,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        string objectCardNoLabel,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        string objectFaceDownLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        string objectTagLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerBaseObjectIdsByPlayer,
        string playerZoneLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        if (triggerId is null || !IsTeemoOnPlaySelfPowerTriggerForRecovery(triggerId, effectKind))
        {
            return;
        }

        if (!TryReadTeemoOnPlaySelfPowerTriggerEffectKindForRecovery(triggerId, out var expectedEffectKind))
        {
            errors.Add($"{payloadLabel} teemo on-play self-power trigger id is invalid");
            return;
        }

        if (!DoesTeemoOnPlaySelfPowerTriggerIdHaveStackContextForRecovery(triggerId, expectedEffectKind))
        {
            errors.Add(
                $"{payloadLabel} teemo on-play self-power trigger id stack item id is required before {expectedEffectKind}");
        }

        if (controllerId is not null
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectControllers is not null
            && objectControllers.TryGetValue(sourceObjectId, out var sourceControllerId)
            && !string.Equals(sourceControllerId, controllerId, StringComparison.Ordinal))
        {
            AddTriggerQueueTriggerControllerMismatch(
                payloadLabel,
                "teemo on-play self-power",
                $"source object id {sourceObjectId} controller id",
                sourceControllerId,
                controllerId,
                objectControllerLabel,
                errors);
        }

        if (controllerId is not null
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectControllers is not null
            && objectControllers.TryGetValue(sourceObjectId, out var zoneSourceControllerId)
            && string.Equals(zoneSourceControllerId, controllerId, StringComparison.Ordinal)
            && playerBaseObjectIdsByPlayer is not null
            && playerBaseObjectIdsByPlayer.TryGetValue(controllerId, out var baseObjectIds)
            && !baseObjectIds.Contains(sourceObjectId))
        {
            errors.Add(
                $"{payloadLabel} teemo on-play self-power source object id {sourceObjectId} must be in trigger controller base zone in {playerZoneLabel}; {FormatExpectedActualForRecovery($"contains {sourceObjectId}", FormatObjectIdsForRecovery(baseObjectIds))}");
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectCardNos is not null
            && objectCardNos.TryGetValue(sourceObjectId, out var sourceCardNo)
            && !CardBehaviorRegistry.IsOnPlaySourcePowerTriggerSource(sourceCardNo, expectedEffectKind))
        {
            var sourceCardNoLabel = sourceCardNo is null ? "<null>" : sourceCardNo;
            AddTriggerQueueSourceCardSpecMismatch(
                payloadLabel,
                "teemo on-play self-power",
                sourceObjectId,
                sourceCardNoLabel,
                OnPlaySourcePowerSourceTriggerSpecLabelForRecovery,
                objectCardNoLabel,
                errors);
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectFaceDowns is not null
            && objectFaceDowns.TryGetValue(sourceObjectId, out var isFaceDown)
            && isFaceDown)
        {
            errors.Add(
                $"{payloadLabel} teemo on-play self-power source object id {sourceObjectId} must not be face down in {objectFaceDownLabel}; {FormatExpectedActualForRecovery(false, isFaceDown)}");
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectTags is not null
            && objectTags.TryGetValue(sourceObjectId, out var sourceTags)
            && !sourceTags.Contains(CardObjectTags.UnitCard))
        {
            errors.Add(
                $"{payloadLabel} teemo on-play self-power source object id {sourceObjectId} must be a unit card in {objectTagLabel}; {FormatExpectedActualForRecovery(CardObjectTags.UnitCard, FormatObjectTagsForRecovery(sourceTags))}");
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
            && objectLocations is not null
            && objectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            && !string.IsNullOrWhiteSpace(sourceLocation.Zone))
        {
            var sourceZone = sourceLocation.Zone.Trim();
            if (!string.Equals(sourceZone, "BASE", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} teemo on-play self-power source object id {sourceObjectId} location zone {sourceZone} must be BASE in {objectLocationLabel}; {FormatExpectedActualForRecovery("BASE", sourceZone)}");
            }

            if (controllerId is not null
                && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(sourceLocation.PlayerId))
            {
                var sourcePlayerId = sourceLocation.PlayerId.Trim();
                if (!string.Equals(sourcePlayerId, controllerId, StringComparison.Ordinal))
                {
                    AddTriggerQueueTriggerControllerMismatch(
                        payloadLabel,
                        "teemo on-play self-power",
                        $"source object id {sourceObjectId} location player id",
                        sourcePlayerId,
                        controllerId,
                        objectLocationLabel,
                        errors);
                }
            }
        }

        ValidateTriggerQueueSourceVisibilityMustBeVisible(
            payloadLabel,
            "teemo on-play self-power",
            sourceVisibility,
            errors);

        ValidateTriggerQueueEffectKindMustBe(
            payloadLabel,
            "teemo on-play self-power",
            effectKind,
            expectedEffectKind,
            errors);

        ValidateTriggerQueueTriggeredEventKindMustBe(
            payloadLabel,
            "teemo on-play self-power",
            triggeredEventKind,
            "UNIT_PLAYED_TO_BASE",
            errors);
    }

    private static bool IsTeemoOnPlaySelfPowerTriggerForRecovery(
        string triggerId,
        string? effectKind)
    {
        return TryReadTeemoOnPlaySelfPowerTriggerEffectKindForRecovery(triggerId, out _)
            || IsTeemoOnPlaySelfPowerEffectKindForRecovery(effectKind);
    }

    private static bool TryReadTeemoOnPlaySelfPowerTriggerEffectKindForRecovery(
        string triggerId,
        out string effectKind)
    {
        effectKind = string.Empty;
        if (!triggerId.StartsWith(StandardTriggerIdPrefixForRecovery, StringComparison.Ordinal))
        {
            return false;
        }

        return TryReadKnownTeemoOnPlaySelfPowerEffectKindSuffixForRecovery(
            triggerId,
            TeemoOnPlaySelfPowerEffectKindForRecovery,
            out effectKind)
            || TryReadKnownTeemoOnPlaySelfPowerEffectKindSuffixForRecovery(
                triggerId,
                TeemoAltAOnPlaySelfPowerEffectKindForRecovery,
                out effectKind)
            || TryReadKnownTeemoOnPlaySelfPowerEffectKindSuffixForRecovery(
                triggerId,
                TeemoAltBOnPlaySelfPowerEffectKindForRecovery,
                out effectKind)
            || TryReadKnownTeemoOnPlaySelfPowerEffectKindSuffixForRecovery(
                triggerId,
                FndTeemoOnPlaySelfPowerEffectKindForRecovery,
                out effectKind);
    }

    private static bool TryReadKnownTeemoOnPlaySelfPowerEffectKindSuffixForRecovery(
        string triggerId,
        string candidateEffectKind,
        out string effectKind)
    {
        effectKind = string.Empty;
        if (!triggerId.EndsWith($"-{candidateEffectKind}", StringComparison.Ordinal))
        {
            return false;
        }

        effectKind = candidateEffectKind;
        return true;
    }

    private static bool DoesTeemoOnPlaySelfPowerTriggerIdHaveStackContextForRecovery(
        string triggerId,
        string expectedEffectKind)
    {
        var suffix = $"-{expectedEffectKind}";
        return triggerId.Length > StandardTriggerIdPrefixForRecovery.Length + suffix.Length;
    }

    private static bool IsTeemoOnPlaySelfPowerEffectKindForRecovery(string? effectKind)
    {
        return string.Equals(effectKind, TeemoOnPlaySelfPowerEffectKindForRecovery, StringComparison.Ordinal)
            || string.Equals(effectKind, TeemoAltAOnPlaySelfPowerEffectKindForRecovery, StringComparison.Ordinal)
            || string.Equals(effectKind, TeemoAltBOnPlaySelfPowerEffectKindForRecovery, StringComparison.Ordinal)
            || string.Equals(effectKind, FndTeemoOnPlaySelfPowerEffectKindForRecovery, StringComparison.Ordinal);
    }

    private static void ValidateTriggerQueueWatchfulSentinelLastBreathDrawContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            WatchfulSentinelLastBreathDrawEffectKindForRecovery,
            "watchful sentinel last-breath draw",
            errors);
    }
}
