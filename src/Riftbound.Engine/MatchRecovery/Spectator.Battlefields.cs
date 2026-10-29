using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateSpectatorSnapshotBattlefieldOptionalStringScalar(
        object? battlefieldPayload,
        string key,
        string? expected,
        string payloadLabel,
        string description,
        string mismatchDescription,
        List<string> errors)
    {
        var hasScalar = TryReadObjectValue(battlefieldPayload, key, out var rawValue)
            && !IsNullSnapshotPayloadValue(rawValue);
        var value = ValidateSnapshotPayloadOptionalStringValue(
            battlefieldPayload,
            key,
            payloadLabel,
            description,
            errors);
        if (!hasScalar)
        {
            value = string.Empty;
        }

        if (value is not null
            && !string.Equals(value, expected ?? string.Empty, StringComparison.Ordinal))
        {
            errors.Add($"{payloadLabel} {description} does not match {mismatchDescription}");
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldBoolScalar(
        object? battlefieldPayload,
        string key,
        bool expected,
        string payloadLabel,
        string description,
        string mismatchDescription,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredBoolValue(
            battlefieldPayload,
            key,
            payloadLabel,
            description,
            errors);
        if (!TryReadObjectBool(battlefieldPayload, key, out var value) || value != expected)
        {
            errors.Add($"{payloadLabel} {description} does not match {mismatchDescription}");
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldNonNegativeIntScalar(
        object? battlefieldPayload,
        string key,
        int expected,
        string payloadLabel,
        string description,
        string mismatchDescription,
        List<string> errors)
    {
        var value = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            battlefieldPayload,
            key,
            payloadLabel,
            description,
            errors);
        if (value is null || value != expected)
        {
            errors.Add($"{payloadLabel} {description} does not match {mismatchDescription}");
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldListPayloads(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            var authoritativeBattlefield = authoritativeBattlefields[index];
            var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;
            var payloadLabel = $"spectator replay frame snapshot lane battlefield {battlefieldObjectId}";

            if (!IsSnapshotPlayerPayloadObject(spectatorBattlefield))
            {
                continue;
            }

            ValidateSpectatorSnapshotBattlefieldStringListScalar(
                spectatorBattlefield,
                "occupantObjectIds",
                authoritativeBattlefield.OccupantObjectIds,
                payloadLabel,
                "occupant object id",
                "occupant object ids",
                "authoritative state occupant object ids",
                errors);
            ValidateSpectatorSnapshotBattlefieldStringListScalar(
                spectatorBattlefield,
                "occupantControllerIds",
                authoritativeBattlefield.OccupantControllerIds,
                payloadLabel,
                "occupant controller id",
                "occupant controller ids",
                "authoritative state occupant controller ids",
                errors);

            ValidateSpectatorSnapshotBattlefieldUnitsBySidePayloadPropertyNames(
                battlefieldObjectId,
                spectatorBattlefield,
                errors);

            var expectedUnitsBySide = ExpectedSpectatorUnitsBySide(authoritativeState, authoritativeBattlefield);
            ValidateSpectatorSnapshotBattlefieldStringListDictionaryScalar(
                spectatorBattlefield,
                "unitsBySide",
                expectedUnitsBySide,
                payloadLabel,
                "units by side",
                "unit object id",
                "authoritative state units by side",
                errors);

            var visibleStandbyObjectIds = authoritativeBattlefield.StandbyObjectIds
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId))
                .ToArray();
            ValidateSpectatorSnapshotBattlefieldStringListScalar(
                spectatorBattlefield,
                "standbyObjectIds",
                visibleStandbyObjectIds,
                payloadLabel,
                "standby object id",
                "standby object ids",
                "authoritative state visible standby object ids",
                errors);

            var expectedScoredPlayerIds = SpectatorBattlefieldScoredThisTurnByPlayers(
                authoritativeState,
                battlefieldObjectId);
            ValidateSpectatorSnapshotBattlefieldStringListScalar(
                spectatorBattlefield,
                "scoredThisTurnPlayerIds",
                expectedScoredPlayerIds,
                payloadLabel,
                "scored player id",
                "scored player ids",
                "authoritative state scored player ids",
                errors);

            var expectedPendingTaskKinds = authoritativeState.PendingCleanupTasks
                .Where(task => string.Equals(task.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
                .Select(task => task.Kind)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray();
            ValidateSpectatorSnapshotBattlefieldStringListScalar(
                spectatorBattlefield,
                "pendingTaskKinds",
                expectedPendingTaskKinds,
                payloadLabel,
                "pending task kind",
                "pending task kinds",
                "authoritative state pending task kinds",
                errors);
            ValidateSpectatorSnapshotBattlefieldPendingTaskKindReferences(
                spectatorBattlefield,
                payloadLabel,
                expectedPendingTaskKinds,
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldPendingTaskKindReferences(
        object? battlefieldPayload,
        string payloadLabel,
        IReadOnlyList<string> expectedPendingTaskKinds,
        List<string> errors)
    {
        if (!TryReadObjectStringList(battlefieldPayload, "pendingTaskKinds", out var pendingTaskKinds))
        {
            return;
        }

        var knownPendingTaskKinds = expectedPendingTaskKinds
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Select(kind => kind.Trim())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var pendingTaskKind in pendingTaskKinds)
        {
            if (string.IsNullOrWhiteSpace(pendingTaskKind))
            {
                continue;
            }

            var normalizedPendingTaskKind = pendingTaskKind.Trim();
            if (!knownPendingTaskKinds.Contains(normalizedPendingTaskKind))
            {
                errors.Add(
                    $"{payloadLabel} pending task kind {normalizedPendingTaskKind} is missing from authoritative state pending task kinds");
            }
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldStringListScalar(
        object? battlefieldPayload,
        string key,
        IReadOnlyList<string> expected,
        string payloadLabel,
        string itemDescription,
        string mismatchDescription,
        string authoritativeDescription,
        List<string> errors)
    {
        ValidateSpectatorRequiredStringListPayloadShape(
            battlefieldPayload,
            payloadLabel,
            key,
            itemDescription,
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            battlefieldPayload,
            key,
            payloadLabel,
            itemDescription,
            errors);
        if (!TryReadObjectStringList(battlefieldPayload, key, out var value)
            || !StringListsEqual(value, expected))
        {
            errors.Add($"{payloadLabel} {mismatchDescription} do not match {authoritativeDescription}");
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldStringListDictionaryScalar(
        object? battlefieldPayload,
        string key,
        IReadOnlyDictionary<string, IReadOnlyList<string>> expected,
        string payloadLabel,
        string itemDescription,
        string listItemDescription,
        string authoritativeDescription,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredStringListDictionaryValues(
            battlefieldPayload,
            key,
            payloadLabel,
            itemDescription,
            listItemDescription,
            errors);
        if (!TryReadObjectStringListDictionary(battlefieldPayload, key, out var value)
            || !StringListDictionariesEqual(value, expected))
        {
            errors.Add($"{payloadLabel} {itemDescription} do not match {authoritativeDescription}");
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldPlayerReferences(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var knownPlayerIds = BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys);
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            if (!IsSnapshotPlayerPayloadObject(spectatorBattlefield))
            {
                continue;
            }

            var battlefieldObjectId = authoritativeBattlefields[index].BattlefieldObjectId;
            var payloadLabel = $"spectator replay frame snapshot lane battlefield {battlefieldObjectId}";
            ValidateTimingOptionalPlayerReference(
                spectatorBattlefield,
                "zonePlayerId",
                $"{payloadLabel} zone player",
                knownPlayerIds,
                "seats",
                errors);
            ValidateTimingOptionalPlayerReference(
                spectatorBattlefield,
                "controllerId",
                $"{payloadLabel} controller",
                knownPlayerIds,
                "seats",
                errors);
            ValidateTimingPlayerReferenceList(
                spectatorBattlefield,
                "occupantControllerIds",
                $"{payloadLabel} occupant controller id",
                knownPlayerIds,
                "seats",
                errors);
            ValidateSpectatorSnapshotBattlefieldUnitsBySidePlayerReferences(
                spectatorBattlefield,
                payloadLabel,
                knownPlayerIds,
                errors);
            ValidateTimingPlayerReferenceList(
                spectatorBattlefield,
                "scoredThisTurnPlayerIds",
                $"{payloadLabel} scored player id",
                knownPlayerIds,
                "seats",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldUnitsBySidePlayerReferences(
        object? battlefieldPayload,
        string payloadLabel,
        IReadOnlySet<string> knownPlayerIds,
        List<string> errors)
    {
        if (!TryReadObjectStringListDictionary(battlefieldPayload, "unitsBySide", out var unitsBySide))
        {
            return;
        }

        foreach (var playerId in unitsBySide.Keys)
        {
            ValidateTimingPlayerReference(
                $"{payloadLabel} units by side player",
                playerId,
                knownPlayerIds,
                "seats",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldObjectReferences(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            if (!IsSnapshotPlayerPayloadObject(spectatorBattlefield))
            {
                continue;
            }

            var authoritativeBattlefield = authoritativeBattlefields[index];
            var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;
            var payloadLabel = $"spectator replay frame snapshot lane battlefield {battlefieldObjectId}";
            var occupantObjectIds = BuildNormalizedObjectIdSet(authoritativeBattlefield.OccupantObjectIds);
            ValidateTimingObjectReferenceListWithExpectedDetails(
                spectatorBattlefield,
                "occupantObjectIds",
                $"{payloadLabel} occupant object id",
                occupantObjectIds,
                "authoritative state occupant object ids",
                errors);
            ValidateSpectatorSnapshotBattlefieldUnitsBySideObjectReferences(
                spectatorBattlefield,
                payloadLabel,
                occupantObjectIds,
                errors);

            var visibleStandbyObjectIds = authoritativeBattlefield.StandbyObjectIds
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId));
            ValidateTimingObjectReferenceListWithExpectedDetails(
                spectatorBattlefield,
                "standbyObjectIds",
                $"{payloadLabel} standby object id",
                BuildNormalizedObjectIdSet(visibleStandbyObjectIds),
                "authoritative state visible standby object ids",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldUnitsBySideObjectReferences(
        object? battlefieldPayload,
        string payloadLabel,
        IReadOnlySet<string> knownOccupantObjectIds,
        List<string> errors)
    {
        if (!TryReadObjectStringListDictionary(battlefieldPayload, "unitsBySide", out var unitsBySide))
        {
            return;
        }

        foreach (var (playerId, unitObjectIds) in unitsBySide)
        {
            var normalizedPlayerId = playerId.Trim();
            foreach (var unitObjectId in unitObjectIds)
            {
                ValidateTimingObjectReferenceWithExpectedDetails(
                    $"{payloadLabel} units by side {normalizedPlayerId} unit object id",
                    unitObjectId,
                    knownOccupantObjectIds,
                    "authoritative state occupant object ids",
                    errors);
            }
        }
    }

    private static IReadOnlySet<string> BuildNormalizedObjectIdSet(IEnumerable<string> objectIds)
    {
        return objectIds
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Select(objectId => objectId.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidateSpectatorSnapshotBattlefieldUnitsBySidePayloadPropertyNames(
        string battlefieldObjectId,
        object? battlefieldPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(battlefieldPayload, "unitsBySide", out var unitsBySidePayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            unitsBySidePayload,
            $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} units by side",
            errors);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ExpectedSpectatorUnitsBySide(
        MatchState authoritativeState,
        BattlefieldState battlefield)
    {
        return battlefield.OccupantObjectIds
            .Where(objectId => authoritativeState.CardObjects.ContainsKey(objectId))
            .GroupBy(
                objectId => EffectiveFieldControllerIdForRecovery(
                    authoritativeState,
                    objectId,
                    authoritativeState.CardObjects[objectId]),
                StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> SpectatorBattlefieldScoredThisTurnByPlayers(
        MatchState authoritativeState,
        string battlefieldObjectId)
    {
        return authoritativeState.UntilEndOfTurnEffects
            .Select(effectId => TryParseBattlefieldScoreGainedMarker(effectId, out var markerBattlefieldObjectId, out var playerId)
                && string.Equals(markerBattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                    ? playerId
                    : null)
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryParseBattlefieldScoreGainedMarker(
        string effectId,
        out string battlefieldObjectId,
        out string playerId)
    {
        battlefieldObjectId = string.Empty;
        playerId = string.Empty;
        if (!effectId.StartsWith(BattlefieldTaskMarkers.ScoreGainedThisTurnPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var marker = effectId[BattlefieldTaskMarkers.ScoreGainedThisTurnPrefix.Length..];
        var separator = marker.LastIndexOf(':');
        if (separator <= 0 || separator == marker.Length - 1)
        {
            return false;
        }

        battlefieldObjectId = marker[..separator];
        playerId = marker[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(battlefieldObjectId)
            && !string.IsNullOrWhiteSpace(playerId);
    }

    private static void ValidateSpectatorSnapshotStandbySlotPayloads(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var knownPlayerIds = BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys);
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            var authoritativeBattlefield = authoritativeBattlefields[index];
            var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;
            if (!TryReadObjectList(spectatorBattlefield, "standbySlots", out var standbySlots))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slots are required");
                continue;
            }

            if (standbySlots.Count != authoritativeBattlefield.StandbyObjectIds.Count)
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slots count does not match authoritative state standby slots count");
            }

            ValidateSpectatorSnapshotExtraStandbySlotPayloadShapes(
                standbySlots,
                battlefieldObjectId,
                authoritativeBattlefield.StandbyObjectIds.Count,
                errors);

            var slotCount = Math.Min(standbySlots.Count, authoritativeBattlefield.StandbyObjectIds.Count);
            var visibleStandbyObjectIds = authoritativeBattlefield.StandbyObjectIds
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId))
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                ValidateSpectatorSnapshotStandbySlotPayload(
                    standbySlots[slotIndex],
                    authoritativeState,
                    authoritativeBattlefield,
                    authoritativeBattlefield.StandbyObjectIds[slotIndex],
                    knownPlayerIds,
                    visibleStandbyObjectIds,
                    slotIndex,
                    errors);
            }
        }
    }

    private static void ValidateSpectatorSnapshotExtraStandbySlotPayloadShapes(
        IReadOnlyList<object?> spectatorStandbySlots,
        string battlefieldObjectId,
        int authoritativeStandbySlotCount,
        List<string> errors)
    {
        for (var slotIndex = authoritativeStandbySlotCount; slotIndex < spectatorStandbySlots.Count; slotIndex++)
        {
            if (!IsSnapshotPlayerPayloadObject(spectatorStandbySlots[slotIndex]))
            {
                errors.Add($"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot item {slotIndex + 1} payload is required");
            }
        }
    }

    private static void ValidateSpectatorSnapshotStandbySlotPayload(
        object? spectatorStandbySlot,
        MatchState authoritativeState,
        BattlefieldState authoritativeBattlefield,
        string standbyObjectId,
        IReadOnlySet<string> knownPlayerIds,
        IReadOnlySet<string> visibleStandbyObjectIds,
        int slotIndex,
        List<string> errors)
    {
        var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;
        var expectedSlotId = $"{battlefieldObjectId}:standby:{slotIndex + 1}";
        var payloadLabel = $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId}";
        if (!IsSnapshotPlayerPayloadObject(spectatorStandbySlot))
        {
            errors.Add($"{payloadLabel} payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            spectatorStandbySlot,
            payloadLabel,
            errors);

        var slotId = ValidateSnapshotPayloadRequiredStringValue(
            spectatorStandbySlot,
            "slotId",
            payloadLabel,
            "slot id",
            errors);
        if (slotId is null || !string.Equals(slotId, expectedSlotId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} slot id does not match authoritative state slot id");
        }

        var payloadBattlefieldObjectId = ValidateSnapshotPayloadRequiredStringValue(
            spectatorStandbySlot,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield id",
            errors);
        if (payloadBattlefieldObjectId is null || !string.Equals(payloadBattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} battlefield id does not match authoritative state battlefield id");
        }

        var expectedControllerId = ExpectedStandbySlotControllerId(authoritativeState, standbyObjectId);
        var expectedSidePlayerId = ExpectedStandbySlotSidePlayerId(
            authoritativeState,
            standbyObjectId,
            expectedControllerId);
        var hasSidePlayerId = TryReadObjectValue(spectatorStandbySlot, "sidePlayerId", out var sidePlayerIdPayload)
            && !IsNullSnapshotPayloadValue(sidePlayerIdPayload);
        var sidePlayerId = ValidateSnapshotPayloadOptionalStringValue(
            spectatorStandbySlot,
            "sidePlayerId",
            payloadLabel,
            "side player",
            errors);
        if (!hasSidePlayerId)
        {
            sidePlayerId = string.Empty;
        }

        if (sidePlayerId is not null
            && !string.Equals(sidePlayerId, expectedSidePlayerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} side player does not match authoritative state side player");
        }
        ValidateTimingOptionalPlayerReference(
            spectatorStandbySlot,
            "sidePlayerId",
            $"{payloadLabel} side player",
            knownPlayerIds,
            "seats",
            errors);

        var hasControllerId = TryReadObjectValue(spectatorStandbySlot, "controllerId", out var controllerIdPayload)
            && !IsNullSnapshotPayloadValue(controllerIdPayload);
        var controllerId = ValidateSnapshotPayloadOptionalStringValue(
            spectatorStandbySlot,
            "controllerId",
            payloadLabel,
            "controller",
            errors);
        if (!hasControllerId)
        {
            controllerId = string.Empty;
        }

        if (controllerId is not null
            && !string.Equals(controllerId, expectedControllerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} controller does not match authoritative state controller");
        }
        ValidateTimingOptionalPlayerReference(
            spectatorStandbySlot,
            "controllerId",
            $"{payloadLabel} controller",
            knownPlayerIds,
            "seats",
            errors);

        var expectedVisible = !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, standbyObjectId);
        ValidateSnapshotPayloadRequiredBoolValue(
            spectatorStandbySlot,
            "visible",
            payloadLabel,
            "visibility",
            errors);
        var hasReadableVisible = TryReadObjectBool(spectatorStandbySlot, "visible", out var visible);
        if (!hasReadableVisible || visible != expectedVisible)
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} visibility does not match authoritative spectator visibility");
        }

        var expectedState = expectedVisible ? "VISIBLE" : "HIDDEN";
        var state = ValidateSnapshotPayloadRequiredStringValue(
            spectatorStandbySlot,
            "state",
            payloadLabel,
            "state",
            errors,
            IsKnownStandbySlotState);
        if (state is null || !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} state does not match authoritative spectator state");
        }

        if (state is not null && hasReadableVisible)
        {
            var expectedPayloadState = visible ? "VISIBLE" : "HIDDEN";
            if (!string.Equals(state, expectedPayloadState, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} state does not match visibility");
            }
        }

        var expectedIsFaceDown = authoritativeState.CardObjects.TryGetValue(standbyObjectId, out var cardObject)
            && cardObject.IsFaceDown;
        ValidateSnapshotPayloadRequiredBoolValue(
            spectatorStandbySlot,
            "isFaceDown",
            payloadLabel,
            "face-down flag",
            errors);
        if (!TryReadObjectBool(spectatorStandbySlot, "isFaceDown", out var isFaceDown)
            || isFaceDown != expectedIsFaceDown)
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} face-down flag does not match authoritative state face-down flag");
        }

        var hiddenObjectIdRedactionReported = false;
        if (hasReadableVisible
            && !visible
            && TryReadObjectValue(spectatorStandbySlot, "objectId", out _))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} hidden object id must be redacted");
            hiddenObjectIdRedactionReported = true;
        }

        if (expectedVisible)
        {
            var objectId = ValidateSnapshotPayloadRequiredStringValue(
                spectatorStandbySlot,
                "objectId",
                payloadLabel,
                "object id",
                errors);
            if (objectId is not null)
            {
                ValidateTimingObjectReferenceWithExpectedDetails(
                    $"{payloadLabel} object id",
                    objectId,
                    visibleStandbyObjectIds,
                    "authoritative state visible standby object ids",
                    errors);
            }

            if (objectId is null || !string.Equals(objectId, standbyObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} object id does not match authoritative visible standby object id");
            }
        }
        else if (!hiddenObjectIdRedactionReported
            && TryReadObjectValue(spectatorStandbySlot, "objectId", out _))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} hidden object id must be redacted");
        }
    }

    private static string ExpectedStandbySlotControllerId(MatchState authoritativeState, string standbyObjectId)
    {
        authoritativeState.CardObjects.TryGetValue(standbyObjectId, out var cardObject);
        authoritativeState.ObjectLocations.TryGetValue(standbyObjectId, out var location);
        if (cardObject is null)
        {
            return location?.PlayerId ?? string.Empty;
        }

        return EffectiveFieldControllerIdForRecovery(authoritativeState, standbyObjectId, cardObject);
    }

    private static string ExpectedStandbySlotSidePlayerId(
        MatchState authoritativeState,
        string standbyObjectId,
        string controllerId)
    {
        return authoritativeState.ObjectLocations.TryGetValue(standbyObjectId, out var location)
            ? location.PlayerId
            : controllerId;
    }

    private static IReadOnlyList<(string PlayerId, string ObjectId)> ExpectedSpectatorLaneBattlefieldObjectPairs(
        MatchState authoritativeState)
    {
        return authoritativeState.PlayerZones
            .OrderBy(entry => authoritativeState.Seats.TryGetValue(entry.Key, out var seat) ? seat : entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId))
                .Select(objectId => (entry.Key, objectId)))
            .ToArray();
    }

    private static IReadOnlyList<string> FormatBattlefieldObjectPairsForRecovery(
        IReadOnlyList<(string PlayerId, string ObjectId)> pairs)
    {
        return pairs
            .Select(pair => $"{pair.PlayerId}:{pair.ObjectId}")
            .ToArray();
    }

    private static bool IsHiddenBattlefieldStandbyForSpectator(MatchState authoritativeState, string objectId)
    {
        if (!authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject)
            || !cardObject.IsFaceDown
            || !authoritativeState.ObjectLocations.TryGetValue(objectId, out var location)
            || !string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            return false;
        }

        return !string.Equals(cardObject.OwnerId, "__spectator__", StringComparison.Ordinal)
            && !string.Equals(
                EffectiveFieldControllerIdForRecovery(authoritativeState, objectId, cardObject),
                "__spectator__",
                StringComparison.Ordinal);
    }

    private static string EffectiveFieldControllerIdForRecovery(
        MatchState authoritativeState,
        string objectId,
        CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return authoritativeState.ObjectLocations.TryGetValue(objectId, out var location)
            ? location.PlayerId
            : string.Empty;
    }

    private static int EffectiveWinningScoreForRecovery(MatchState authoritativeState)
    {
        // OGN276 applies globally while the battlefield is on the public field;
        // conquering it does not move its physical card to the new controller's zone.
        var modifier = authoritativeState.PlayerZones.Values.SelectMany(zones => zones.Battlefields)
            .Distinct(StringComparer.Ordinal)
            .Sum(objectId => authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject)
                && !cardObject.IsFaceDown && cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
                    ? BattlefieldWinningScoreIncreaseAmountForRecovery(cardObject.CardNo) : 0);
        return BaseWinningScore + modifier;
    }

    private static int BattlefieldWinningScoreIncreaseAmountForRecovery(string? cardNo)
    {
        return BattlefieldStaticAbilitySpecRules.TryGetAbility(
                cardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldWinningScoreIncreaseAbility,
                out var ability)
                ? ability.Amount
                : 0;
    }

    private static bool SourceObjectControlledByPlayerOrLegacyOwnedForRecovery(
        CardObjectState cardObject,
        string playerId)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(cardObject.OwnerId)
            || string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal);
    }

    private static void ValidateSpectatorPendingTaskQueuePayload(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!timing.TryGetValue("pendingTaskQueue", out var queuePayload)
            || IsNullSnapshotPayloadValue(queuePayload))
        {
            errors.Add("spectator replay frame timing pending task queue is required");
            if (authoritativeState.PendingTaskQueue.Tasks.Count > 0)
            {
                errors.Add("spectator replay frame timing pending task queue task count does not match authoritative state pending task queue task count");
            }

            return;
        }

        if (!IsSnapshotPlayerPayloadObject(queuePayload))
        {
            errors.Add("spectator replay frame timing pending task queue payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            queuePayload,
            "spectator replay frame timing pending task queue",
            errors);
        var queueResult = ValidateSpectatorPendingTaskQueuePayloadValues(queuePayload, errors);
        var validatedSpectatorActiveTaskId = queueResult.ActiveTaskId;
        int? taskPayloadCount = null;
        IReadOnlyList<string>? taskKinds = null;

        var authoritativeQueue = authoritativeState.PendingTaskQueue;
        if (!TryReadObjectBool(queuePayload, "hasTasks", out var hasTasks)
            || hasTasks != authoritativeQueue.HasTasks)
        {
            errors.Add("spectator replay frame timing pending task queue has tasks does not match authoritative state pending task queue has tasks");
        }

        if (!TryReadObjectBool(queuePayload, "isBlocking", out var isBlocking)
            || isBlocking != authoritativeQueue.IsBlocking)
        {
            errors.Add("spectator replay frame timing pending task queue blocking state does not match authoritative state pending task queue blocking state");
        }

        if (!TryReadObjectString(queuePayload, "phase", out var phase)
            || !string.Equals(phase, authoritativeQueue.Phase, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending task queue phase does not match authoritative state pending task queue phase");
        }

        var activeTaskId = ExpectedSpectatorPendingTaskQueueActiveTaskId(authoritativeState);
        if (!TryReadObjectOptionalString(queuePayload, "activeTaskId", out var spectatorActiveTaskId)
            || !string.Equals(spectatorActiveTaskId, activeTaskId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending task queue active task id does not match authoritative state pending task queue active task id");
        }

        if (!TryReadObjectList(queuePayload, "tasks", out var taskPayloads))
        {
            errors.Add("spectator replay frame timing pending task queue task count does not match authoritative state pending task queue task count");
        }
        else
        {
            taskPayloadCount = taskPayloads.Count;
            ValidateSnapshotPendingTaskQueueFlagsMatchTaskCount(
                queueResult,
                taskPayloads.Count,
                "spectator replay frame timing pending task queue",
                errors);

            taskKinds = ValidateSpectatorPendingTaskQueueTaskPayloads(
                taskPayloads,
                authoritativeState,
                authoritativeQueue.Tasks,
                validatedSpectatorActiveTaskId,
                errors,
                validateAuthoritativeParity: taskPayloads.Count == authoritativeQueue.Tasks.Count);

            if (taskPayloads.Count != authoritativeQueue.Tasks.Count)
            {
                errors.Add("spectator replay frame timing pending task queue task count does not match authoritative state pending task queue task count");
            }
        }

        if (!TryReadObjectValue(queuePayload, "metadata", out var metadataPayload)
            || IsNullSnapshotPayloadValue(metadataPayload))
        {
            errors.Add("spectator replay frame timing pending task queue metadata is required");
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(metadataPayload))
        {
            errors.Add("spectator replay frame timing pending task queue metadata payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            metadataPayload,
            "spectator replay frame timing pending task queue metadata",
            errors);
        var metadataTaskCount = ValidateSpectatorPendingTaskQueueMetadataPayloadValues(metadataPayload, errors);
        if (taskPayloadCount is int actualTaskCount
            && metadataTaskCount is int expectedTaskCount
            && expectedTaskCount >= 0
            && expectedTaskCount != actualTaskCount)
        {
            errors.Add(
                $"spectator replay frame timing pending task queue metadata task count {expectedTaskCount} does not match pending task queue task count {actualTaskCount}");
        }

        if (metadataTaskCount is not int taskCount
            || taskCount != authoritativeQueue.Tasks.Count)
        {
            errors.Add("spectator replay frame timing pending task queue metadata task count does not match authoritative state pending task queue task count");
        }

        if (taskKinds is not null
            && TryReadObjectStringList(metadataPayload, "stateBasedTaskKinds", out var taskPayloadStateBasedTaskKinds)
            && !StringListsEqual(taskPayloadStateBasedTaskKinds, ExpectedStateBasedTaskKinds(taskKinds)))
        {
            errors.Add("spectator replay frame timing pending task queue metadata state-based task kinds do not match pending task queue task kinds");
        }

        if (!TryReadObjectStringList(metadataPayload, "stateBasedTaskKinds", out var stateBasedTaskKinds)
            || !StringListsEqual(stateBasedTaskKinds, ExpectedStateBasedTaskKinds(authoritativeQueue.Tasks)))
        {
            errors.Add("spectator replay frame timing pending task queue metadata state-based task kinds do not match authoritative state pending task queue state-based task kinds");
        }
    }

    private static IReadOnlyList<string>? ValidateSpectatorPendingTaskQueueTaskPayloads(
        IReadOnlyList<object?> taskPayloads,
        MatchState authoritativeState,
        IReadOnlyList<CleanupTaskState> authoritativeTasks,
        string? activeTaskId,
        List<string> errors,
        bool validateAuthoritativeParity)
    {
        var seenTaskIds = new HashSet<string>(StringComparer.Ordinal);
        var taskKinds = new List<string>();
        var taskKindsAreValid = true;
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload))
            {
                errors.Add("spectator replay frame timing pending task queue task payload is required");
                taskKindsAreValid = false;
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                taskPayload,
                "spectator replay frame timing pending task queue task item",
                errors);
            var taskResult = ValidateSpectatorPendingTaskQueueTaskPayloadValues(taskPayload, errors);
            var taskId = taskResult.TaskId;
            if (taskId is not null && !seenTaskIds.Add(taskId))
            {
                errors.Add($"spectator replay frame timing pending task queue task item task id {taskId} is duplicated");
            }

            if (taskResult.Kind is null)
            {
                taskKindsAreValid = false;
            }
            else
            {
                taskKinds.Add(taskResult.Kind);
            }
        }

        if (!string.IsNullOrEmpty(activeTaskId) && !seenTaskIds.Contains(activeTaskId))
        {
            errors.Add($"spectator replay frame timing pending task queue active task id {activeTaskId} does not match a pending task queue task id");
        }

        if (taskPayloads.Count > 0 && string.IsNullOrEmpty(activeTaskId))
        {
            errors.Add("spectator replay frame timing pending task queue active task id is required when pending task queue has tasks");
        }

        var authoritativeTasksByVisibleTaskId = BuildAuthoritativePendingTaskQueueTasksByVisibleTaskId(
            authoritativeState,
            authoritativeTasks);
        ValidateSpectatorPendingTaskQueueAuthoritativeTaskKeySet(
            taskPayloads,
            authoritativeTasksByVisibleTaskId,
            errors);
        ValidateSpectatorPendingTaskQueueKeyedAuthoritativeValues(
            taskPayloads,
            authoritativeTasksByVisibleTaskId,
            authoritativeState,
            errors);

        if (!validateAuthoritativeParity)
        {
            return taskKindsAreValid ? taskKinds : null;
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(taskPayloads, "taskId"),
                authoritativeTasks.Select(task => VisibleCleanupTaskIdForRecovery(authoritativeState, task)).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task ids do not match authoritative state pending task queue task ids");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(taskPayloads, "kind"),
                authoritativeTasks.Select(task => task.Kind).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task kinds do not match authoritative state pending task queue task kinds");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(taskPayloads, "reason"),
                authoritativeTasks.Select(task => task.Reason).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task reasons do not match authoritative state pending task queue task reasons");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(taskPayloads, "playerId"),
                authoritativeTasks.Select(task => task.PlayerId ?? string.Empty).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task players do not match authoritative state pending task queue task players");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(taskPayloads, "battlefieldObjectId"),
                authoritativeTasks.Select(task => task.BattlefieldObjectId ?? string.Empty).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task battlefield object ids do not match authoritative state pending task queue task battlefield object ids");
        }

        var objectIdMismatch = false;
        var hiddenObjectMismatch = false;
        var hiddenObjectKindMismatch = false;
        for (var index = 0; index < authoritativeTasks.Count; index++)
        {
            var task = authoritativeTasks[index];
            var taskPayload = taskPayloads[index];
            if (ShouldHideCleanupTaskObjectIdForRecovery(authoritativeState, task))
            {
                if (TryReadObjectValue(taskPayload, "objectId", out _))
                {
                    objectIdMismatch = true;
                }

                if (!TryReadObjectBool(taskPayload, "hiddenObject", out var hiddenObject)
                    || !hiddenObject)
                {
                    hiddenObjectMismatch = true;
                }

                if (!TryReadObjectString(taskPayload, "hiddenObjectKind", out var hiddenObjectKind)
                    || !string.Equals(hiddenObjectKind, "BATTLEFIELD_STANDBY", StringComparison.Ordinal))
                {
                    hiddenObjectKindMismatch = true;
                }

                continue;
            }

            if (!TryReadObjectOptionalString(taskPayload, "objectId", out var objectId)
                || !string.Equals(objectId, task.ObjectId ?? string.Empty, StringComparison.Ordinal))
            {
                objectIdMismatch = true;
            }

            if (TryReadObjectValue(taskPayload, "hiddenObject", out _))
            {
                hiddenObjectMismatch = true;
            }

            if (TryReadObjectValue(taskPayload, "hiddenObjectKind", out _))
            {
                hiddenObjectKindMismatch = true;
            }
        }

        if (objectIdMismatch)
        {
            errors.Add("spectator replay frame timing pending task queue task object ids do not match authoritative state pending task queue task object ids");
        }

        if (hiddenObjectMismatch)
        {
            errors.Add("spectator replay frame timing pending task queue task hidden object flags do not match authoritative state pending task queue task hidden object flags");
        }

        if (hiddenObjectKindMismatch)
        {
            errors.Add("spectator replay frame timing pending task queue task hidden object kinds do not match authoritative state pending task queue task hidden object kinds");
        }

        return taskKindsAreValid ? taskKinds : null;
    }
}
