using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateBattleResolutionObjectReferences(
        object? resolutionPayload,
        string payloadLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        ValidateTimingOptionalObjectReferenceWithExpectedDetails(
            resolutionPayload,
            "battlefieldId",
            $"{payloadLabel} battlefield object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            resolutionPayload,
            "attackerObjectIds",
            $"{payloadLabel} attacker object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            resolutionPayload,
            "defenderObjectIds",
            $"{payloadLabel} defender object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            resolutionPayload,
            "survivingAttackerObjectIds",
            $"{payloadLabel} surviving attacker object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            resolutionPayload,
            "survivingDefenderObjectIds",
            $"{payloadLabel} surviving defender object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            resolutionPayload,
            "destroyedObjectIds",
            $"{payloadLabel} destroyed object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
    }

    private static void ValidateBattlefieldResolutionPlayerReferences(
        object? resolutionPayload,
        string payloadLabel,
        IReadOnlySet<string>? knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        ValidateTimingOptionalPlayerReference(
            resolutionPayload,
            "playerId",
            $"{payloadLabel} player id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
        ValidateTimingOptionalPlayerReference(
            resolutionPayload,
            "previousControllerId",
            $"{payloadLabel} previous controller id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
        ValidateTimingOptionalPlayerReference(
            resolutionPayload,
            "controllerId",
            $"{payloadLabel} controller id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
    }

    private static void ValidateBattleResolutionPlayerReferences(
        object? resolutionPayload,
        string payloadLabel,
        IReadOnlySet<string>? knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        ValidateTimingOptionalPlayerReference(
            resolutionPayload,
            "attackingPlayerId",
            $"{payloadLabel} attacking player id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
        ValidateTimingOptionalPlayerReference(
            resolutionPayload,
            "defendingPlayerId",
            $"{payloadLabel} defending player id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
        ValidateTimingOptionalPlayerReference(
            resolutionPayload,
            "winnerPlayerId",
            $"{payloadLabel} winner player id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
    }

    private static void ValidateSpectatorCoreTimingPayloadValues(
        IReadOnlyDictionary<string, object?> timing,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing";
        ValidateSnapshotPayloadRequiredStringValue(
            timing,
            "phase",
            payloadLabel,
            "phase",
            errors,
            IsKnownMatchPhase);
        ValidateSnapshotPayloadRequiredStringValue(
            timing,
            "timingState",
            payloadLabel,
            "timing state",
            errors,
            IsKnownTimingState);
        ValidateSnapshotPayloadRequiredStringValue(
            timing,
            "turnPlayerId",
            payloadLabel,
            "turn player id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            timing,
            "roomStatus",
            payloadLabel,
            "room status",
            errors,
            IsKnownMatchStatus);
        ValidateSpectatorRequiredStringListPayloadShape(
            timing,
            payloadLabel,
            "readyPlayerIds",
            "ready player id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            timing,
            "readyPlayerIds",
            payloadLabel,
            "ready player id",
            errors);
        ValidateSnapshotPayloadRequiredPositiveIntValue(
            timing,
            "winningScore",
            payloadLabel,
            "winning score",
            errors);
    }

    private static void ValidateSpectatorPriorityTimingPayloadValues(
        IReadOnlyDictionary<string, object?> timing,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing";
        ValidateSnapshotPayloadOptionalStringValue(
            timing,
            "priorityPlayerId",
            payloadLabel,
            "priority player id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            timing,
            payloadLabel,
            "passedPriorityPlayerIds",
            "passed priority player id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            timing,
            "passedPriorityPlayerIds",
            payloadLabel,
            "passed priority player id",
            errors);
    }

    private static void ValidateSpectatorFocusTimingPayloadValues(
        IReadOnlyDictionary<string, object?> timing,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing";
        ValidateSnapshotPayloadOptionalStringValue(
            timing,
            "focusPlayerId",
            payloadLabel,
            "focus player id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            timing,
            "winnerPlayerId",
            payloadLabel,
            "winner player id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            timing,
            payloadLabel,
            "passedFocusPlayerIds",
            "passed focus player id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            timing,
            "passedFocusPlayerIds",
            payloadLabel,
            "passed focus player id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            timing,
            payloadLabel,
            "destroyedUnitOwnerIdsThisTurn",
            "destroyed unit owner id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            timing,
            "destroyedUnitOwnerIdsThisTurn",
            payloadLabel,
            "destroyed unit owner id",
            errors);
    }

    private static void ValidateSpectatorTurnWindowPayloadValues(
        object? turnWindowPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing turn window";
        ValidateTurnWindowPayloadValues(turnWindowPayload, payloadLabel, errors);
    }

    private static void ValidateTurnWindowPayloadValues(
        object? turnWindowPayload,
        string payloadLabel,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredStringValue(
            turnWindowPayload,
            "state",
            payloadLabel,
            "state",
            errors);
        ValidateSnapshotPayloadRequiredBoolValue(
            turnWindowPayload,
            "isSpellDuel",
            payloadLabel,
            "is spell duel flag",
            errors);
        ValidateSnapshotPayloadRequiredBoolValue(
            turnWindowPayload,
            "isClosed",
            payloadLabel,
            "is closed flag",
            errors);
        ValidateSnapshotPayloadRequiredBoolValue(
            turnWindowPayload,
            "hasStack",
            payloadLabel,
            "has stack flag",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            turnWindowPayload,
            "actingPlayerId",
            payloadLabel,
            "acting player id",
            errors);
    }

    private static void ValidateSpectatorSpellDuelPayloadValues(
        object? spellDuelPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing spell duel";
        ValidateSpectatorSpellDuelListPayloadShapes(
            spellDuelPayload,
            errors);
        ValidateSpellDuelPayloadValues(spellDuelPayload, payloadLabel, errors);
    }

    private static void ValidateSpectatorSpellDuelListPayloadShapes(
        object? spellDuelPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing spell duel";
        ValidateSpectatorRequiredStringListPayloadShape(
            spellDuelPayload,
            payloadLabel,
            "passedFocusPlayerIds",
            "passed focus player id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            spellDuelPayload,
            payloadLabel,
            "stackItemIds",
            "stack item id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            spellDuelPayload,
            payloadLabel,
            "stackControllerIds",
            "stack controller id",
            errors);
    }

    private static void ValidateSpellDuelPayloadValues(
        object? spellDuelPayload,
        string payloadLabel,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredBoolValue(
            spellDuelPayload,
            "isActive",
            payloadLabel,
            "active flag",
            errors);
        ValidateSnapshotPayloadRequiredBoolValue(
            spellDuelPayload,
            "isClosed",
            payloadLabel,
            "closed flag",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            spellDuelPayload,
            "spellDuelId",
            payloadLabel,
            "spell duel id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            spellDuelPayload,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield object id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            spellDuelPayload,
            "focusPlayerId",
            payloadLabel,
            "focus player id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            spellDuelPayload,
            "passedFocusPlayerIds",
            payloadLabel,
            "passed focus player id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            spellDuelPayload,
            "stackItemIds",
            payloadLabel,
            "stack item id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            spellDuelPayload,
            "stackControllerIds",
            payloadLabel,
            "stack controller id",
            errors);
    }

    private static void ValidateSpectatorBattlePayloadValues(
        object? battlePayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle";
        var knownPlayerIds = BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys);
        var knownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState);
        ValidateSnapshotPayloadRequiredBoolValue(
            battlePayload,
            "isActive",
            payloadLabel,
            "active flag",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            battlePayload,
            "battleId",
            payloadLabel,
            "battle id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            battlePayload,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield object id",
            errors);
        ValidateSpectatorBattleParticipantListPayloadShapes(
            battlePayload,
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            battlePayload,
            "attackerObjectIds",
            payloadLabel,
            "attacker object id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            battlePayload,
            "defenderObjectIds",
            payloadLabel,
            "defender object id",
            errors);
        ValidateSpectatorBattleParticipantControllerPayloadValues(battlePayload, errors);
        ValidateBattleActiveParticipantConsistency(
            battlePayload,
            payloadLabel,
            errors,
            authoritativeState);
        ValidateBattlePlayerReferences(
            battlePayload,
            payloadLabel,
            knownPlayerIds,
            "seats",
            errors);
        ValidateBattleObjectReferences(
            battlePayload,
            payloadLabel,
            knownObjectIds,
            "object registry",
            errors);
        ValidateBattleParticipantControllerMembership(
            battlePayload,
            payloadLabel,
            errors);
    }

    private static void ValidateSpectatorBattleParticipantListPayloadShapes(
        object? battlePayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle";
        ValidateSpectatorRequiredStringListPayloadShape(
            battlePayload,
            payloadLabel,
            "attackerObjectIds",
            "attacker object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            battlePayload,
            payloadLabel,
            "defenderObjectIds",
            "defender object id",
            errors);
    }

    private static void ValidateSpectatorRequiredStringListPayloadShape(
        object? payload,
        string payloadLabel,
        string payloadKey,
        string itemLabel,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredStringListPayloadShape(payload, payloadKey, payloadLabel, itemLabel, errors);
    }

    private static void ValidateSpectatorRequiredIntMapPayloadShape(
        object? payload,
        string payloadLabel,
        string payloadKey,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, payloadKey, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload))
        {
            return;
        }

        if (!IsSnapshotIntMapPayloadObject(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map payload is required");
        }
    }

    private static void ValidateSpectatorBattleParticipantControllerPayloadValues(
        object? battlePayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle";
        if (!TryReadObjectValue(battlePayload, "participantControllerIds", out var participantControllerIdsPayload)
            || IsNullSnapshotPayloadValue(participantControllerIdsPayload))
        {
            errors.Add($"{payloadLabel} participant controller map is required");
            return;
        }

        if (!IsSnapshotStringMapPayloadObject(participantControllerIdsPayload))
        {
            errors.Add($"{payloadLabel} participant controller map payload is required");
            return;
        }

        ValidateSnapshotPayloadRequiredStringDictionaryValues(
            battlePayload,
            "participantControllerIds",
            payloadLabel,
            "participant controller",
            errors);
    }

    private static void ValidateBattlePayloadValues(
        object? battlePayload,
        string payloadLabel,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredBoolValue(
            battlePayload,
            "isActive",
            payloadLabel,
            "active flag",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            battlePayload,
            "battleId",
            payloadLabel,
            "battle id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            battlePayload,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield object id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            battlePayload,
            "attackerObjectIds",
            payloadLabel,
            "attacker object id",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            battlePayload,
            "defenderObjectIds",
            payloadLabel,
            "defender object id",
            errors);
        ValidateSnapshotPayloadRequiredStringDictionaryValues(
            battlePayload,
            "participantControllerIds",
            payloadLabel,
            "participant controller",
            errors);
    }

    private static void ValidateBattlePlayerReferences(
        object? battlePayload,
        string payloadLabel,
        IReadOnlySet<string>? knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        ValidateTimingPlayerReferenceDictionaryValues(
            battlePayload,
            "participantControllerIds",
            payloadLabel,
            "participant controller",
            "player id",
            knownPlayerIds,
            knownPlayerLabel,
            errors);
    }

    private static void ValidateBattleObjectReferences(
        object? battlePayload,
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
            battlePayload,
            "battlefieldObjectId",
            $"{payloadLabel} battlefield object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            battlePayload,
            "attackerObjectIds",
            $"{payloadLabel} attacker object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            battlePayload,
            "defenderObjectIds",
            $"{payloadLabel} defender object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceMapKeysWithExpectedDetails(
            battlePayload,
            "participantControllerIds",
            $"{payloadLabel} participant object id",
            IsSnapshotStringMapPayloadObject,
            knownObjectIds,
            knownObjectLabel,
            errors);
    }

    private static void ValidateBattleActiveParticipantConsistency(
        object? battlePayload,
        string payloadLabel,
        List<string> errors,
        MatchState? authoritativeState)
    {
        if (!TryReadObjectBool(battlePayload, "isActive", out var isActive)
            || !TryBuildBattleParticipantObjectIdSet(battlePayload, out var participantObjectIds))
        {
            return;
        }

        var hasParticipants = participantObjectIds.Count > 0;
        var actualParticipantObjectIds = participantObjectIds
            .OrderBy(participantObjectId => participantObjectId, StringComparer.Ordinal)
            .ToArray();
        // Empty current rosters are legitimate only while an authoritative
        // captured declaration keeps this precise battle pending settlement.
        var capturedEmptyBattle = authoritativeState is not null
            && CoreRuleEngine.TryReadBattleResponseDeclarationContext(authoritativeState, string.Empty, out _)
            && BattleMatches(battlePayload, authoritativeState, authoritativeState.BattleState);
        if (isActive && !hasParticipants && !capturedEmptyBattle)
        {
            errors.Add(
                $"{payloadLabel} active flag is true but no battle participants are present; {FormatExpectedActualForRecovery("<non-empty>", actualParticipantObjectIds)}");
        }
        else if (!isActive && hasParticipants)
        {
            errors.Add(
                $"{payloadLabel} active flag is false but battle participants are present; {FormatExpectedActualForRecovery(Array.Empty<string>(), actualParticipantObjectIds)}");
        }
    }

    private static void ValidateBattleParticipantControllerMembership(
        object? battlePayload,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryBuildBattleParticipantObjectIdSet(battlePayload, out var participantObjectIds)
            || !TryReadObjectStringDictionary(battlePayload, "participantControllerIds", out var participantControllerIds))
        {
            return;
        }

        var controllerObjectIds = participantControllerIds.Keys
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Select(objectId => objectId.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var expectedParticipantObjectIds = participantObjectIds
            .OrderBy(participantObjectId => participantObjectId, StringComparer.Ordinal)
            .ToArray();
        var actualControllerObjectIds = controllerObjectIds
            .OrderBy(controllerObjectId => controllerObjectId, StringComparer.Ordinal)
            .ToArray();

        foreach (var controllerObjectId in controllerObjectIds)
        {
            if (!participantObjectIds.Contains(controllerObjectId))
            {
                errors.Add(
                    $"{payloadLabel} participant controller object id {controllerObjectId} is not an enclosing battle participant; {FormatExpectedActualForRecovery(expectedParticipantObjectIds, controllerObjectId)}");
            }
        }

        foreach (var participantObjectId in participantObjectIds)
        {
            if (!controllerObjectIds.Contains(participantObjectId))
            {
                errors.Add(
                    $"{payloadLabel} participant controller for participant object id {participantObjectId} is required; {FormatExpectedActualForRecovery(expectedParticipantObjectIds, actualControllerObjectIds)}");
            }
        }
    }

    private static void ValidateSpectatorBattleDamageAssignmentPayloadValues(
        object? battlePayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle damage assignment";
        if (!TryReadObjectValue(battlePayload, "damageAssignment", out var damageAssignmentPayload)
            || IsNullSnapshotPayloadValue(damageAssignmentPayload))
        {
            errors.Add($"{payloadLabel} payload is required");
            AddMissingSpectatorBattleDamageAssignmentDiagnostics(authoritativeState, errors);
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(damageAssignmentPayload))
        {
            errors.Add($"{payloadLabel} payload is required");
            AddMissingSpectatorBattleDamageAssignmentDiagnostics(authoritativeState, errors);
            return;
        }

        ValidateSnapshotPayloadRequiredBoolValue(
            damageAssignmentPayload,
            "isPending",
            payloadLabel,
            "pending flag",
            errors);
        ValidateSpectatorBattleDamageAssignmentMapPayloadShapes(
            damageAssignmentPayload,
            errors);
        ValidateSpectatorBattleDamageAssignmentRequiredAssignmentItemPayloadShapes(
            damageAssignmentPayload,
            errors);

        var shouldValidatePendingFields = ResolutionResult.HasOpenBattleDamageAssignmentWindow(authoritativeState)
            || (TryReadObjectBool(damageAssignmentPayload, "isPending", out var isPending) && isPending);
        ValidateBattleDamageAssignmentPendingPayloadValues(
            damageAssignmentPayload,
            payloadLabel,
            shouldValidatePendingFields,
            errors);
        AddMissingSpectatorBattleDamageAssignmentFieldDiagnostics(
            damageAssignmentPayload,
            authoritativeState,
            errors);
        ValidateSpectatorBattleDamageAssignmentCollectionCounts(
            damageAssignmentPayload,
            authoritativeState,
            errors);
        ValidateSpectatorBattleDamageAssignmentAuthoritativeValues(
            damageAssignmentPayload,
            authoritativeState,
            errors);
        ValidateBattleDamageAssignmentIdentityConsistency(
            battlePayload,
            damageAssignmentPayload,
            payloadLabel,
            shouldValidatePendingFields,
            errors);
        ValidateBattleDamageAssignmentParticipantMembership(
            battlePayload,
            damageAssignmentPayload,
            payloadLabel,
            shouldValidatePendingFields,
            errors);
        ValidateBattleDamageAssignmentPlayerReferences(
            damageAssignmentPayload,
            payloadLabel,
            BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys),
            "seats",
            errors);
        ValidateBattleDamageAssignmentObjectReferences(
            damageAssignmentPayload,
            payloadLabel,
            BuildAuthoritativeStateKnownObjectIds(authoritativeState),
            "object registry",
            errors);
    }

    private static void AddMissingSpectatorBattleDamageAssignmentDiagnostics(
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!ResolutionResult.HasOpenBattleDamageAssignmentWindow(authoritativeState))
        {
            return;
        }

        var authoritativeBattle = authoritativeState.BattleState;
        var authoritativeDamagePoolCount =
            ResolutionResult.BattleDamagePoolFor(authoritativeState, authoritativeBattle).Count;
        AddSpectatorBattleDamageAssignmentCountDiagnostic(
            "damage pool",
            0,
            authoritativeDamagePoolCount,
            errors);

        var authoritativeLegalTargetCount =
            ResolutionResult.BattleDamageLegalTargetsFor(authoritativeState, authoritativeBattle).Count;
        AddSpectatorBattleDamageAssignmentCountDiagnostic(
            "legal target",
            0,
            authoritativeLegalTargetCount,
            errors);

        var authoritativeExistingDamageCount =
            ResolutionResult.BattleExistingDamageFor(authoritativeState, authoritativeBattle).Count;
        AddSpectatorBattleDamageAssignmentCountDiagnostic(
            "existing damage",
            0,
            authoritativeExistingDamageCount,
            errors);

        var authoritativeLethalDamageThresholdCount =
            ResolutionResult.BattleLethalDamageThresholdFor(authoritativeState, authoritativeBattle).Count;
        AddSpectatorBattleDamageAssignmentCountDiagnostic(
            "lethal damage threshold",
            0,
            authoritativeLethalDamageThresholdCount,
            errors);

        var authoritativeRequiredAssignmentCount =
            ResolutionResult.BattleRequiredAssignmentsFor(authoritativeState, authoritativeBattle).Count;
        AddSpectatorBattleDamageAssignmentCountDiagnostic(
            "required assignment",
            0,
            authoritativeRequiredAssignmentCount,
            errors);
    }

    private static void AddMissingSpectatorBattleDamageAssignmentFieldDiagnostics(
        object? damageAssignmentPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!ResolutionResult.HasOpenBattleDamageAssignmentWindow(authoritativeState))
        {
            return;
        }

        var authoritativeBattle = authoritativeState.BattleState;
        AddMissingSpectatorBattleDamageAssignmentFieldDiagnostic(
            damageAssignmentPayload,
            "damagePool",
            "damage pool",
            IsSnapshotIntMapPayloadObject,
            ResolutionResult.BattleDamagePoolFor(authoritativeState, authoritativeBattle).Count,
            errors);
        AddMissingSpectatorBattleDamageAssignmentFieldDiagnostic(
            damageAssignmentPayload,
            "legalTargets",
            "legal target",
            IsSnapshotStringListMapPayloadObject,
            ResolutionResult.BattleDamageLegalTargetsFor(authoritativeState, authoritativeBattle).Count,
            errors);
        AddMissingSpectatorBattleDamageAssignmentFieldDiagnostic(
            damageAssignmentPayload,
            "existingDamage",
            "existing damage",
            IsSnapshotIntMapPayloadObject,
            ResolutionResult.BattleExistingDamageFor(authoritativeState, authoritativeBattle).Count,
            errors);
        AddMissingSpectatorBattleDamageAssignmentFieldDiagnostic(
            damageAssignmentPayload,
            "lethalDamageThreshold",
            "lethal damage threshold",
            IsSnapshotIntMapPayloadObject,
            ResolutionResult.BattleLethalDamageThresholdFor(authoritativeState, authoritativeBattle).Count,
            errors);
        AddMissingSpectatorBattleDamageAssignmentFieldDiagnostic(
            damageAssignmentPayload,
            "requiredAssignments",
            "required assignment",
            payload => TryReadObjectListValue(payload, out _),
            ResolutionResult.BattleRequiredAssignmentsFor(authoritativeState, authoritativeBattle).Count,
            errors);
    }

    private static void AddMissingSpectatorBattleDamageAssignmentFieldDiagnostic(
        object? damageAssignmentPayload,
        string payloadKey,
        string countLabel,
        Func<object?, bool> isReadablePayload,
        int authoritativeCount,
        List<string> errors)
    {
        if (TryReadObjectValue(damageAssignmentPayload, payloadKey, out var fieldPayload)
            && !IsNullSnapshotPayloadValue(fieldPayload)
            && isReadablePayload(fieldPayload))
        {
            return;
        }

        AddSpectatorBattleDamageAssignmentCountDiagnostic(
            countLabel,
            0,
            authoritativeCount,
            errors);
    }

    private static void ValidateSpectatorBattleDamageAssignmentCollectionCounts(
        object? damageAssignmentPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!ResolutionResult.HasOpenBattleDamageAssignmentWindow(authoritativeState))
        {
            return;
        }

        var authoritativeBattle = authoritativeState.BattleState;
        if (TryReadObjectIntDictionary(damageAssignmentPayload, "damagePool", out var damagePool))
        {
            AddSpectatorBattleDamageAssignmentCountDiagnostic(
                "damage pool",
                damagePool.Count,
                ResolutionResult.BattleDamagePoolFor(authoritativeState, authoritativeBattle).Count,
                errors);
        }

        if (TryReadObjectStringListDictionary(damageAssignmentPayload, "legalTargets", out var legalTargets))
        {
            AddSpectatorBattleDamageAssignmentCountDiagnostic(
                "legal target",
                legalTargets.Count,
                ResolutionResult.BattleDamageLegalTargetsFor(authoritativeState, authoritativeBattle).Count,
                errors);
        }

        if (TryReadObjectIntDictionary(damageAssignmentPayload, "existingDamage", out var existingDamage))
        {
            AddSpectatorBattleDamageAssignmentCountDiagnostic(
                "existing damage",
                existingDamage.Count,
                ResolutionResult.BattleExistingDamageFor(authoritativeState, authoritativeBattle).Count,
                errors);
        }

        if (TryReadObjectIntDictionary(damageAssignmentPayload, "lethalDamageThreshold", out var lethalDamageThreshold))
        {
            AddSpectatorBattleDamageAssignmentCountDiagnostic(
                "lethal damage threshold",
                lethalDamageThreshold.Count,
                ResolutionResult.BattleLethalDamageThresholdFor(authoritativeState, authoritativeBattle).Count,
                errors);
        }

        if (TryReadObjectList(damageAssignmentPayload, "requiredAssignments", out var requiredAssignments))
        {
            AddSpectatorBattleDamageAssignmentCountDiagnostic(
                "required assignment",
                requiredAssignments.Count,
                ResolutionResult.BattleRequiredAssignmentsFor(authoritativeState, authoritativeBattle).Count,
                errors);
        }
    }

    private static void AddSpectatorBattleDamageAssignmentCountDiagnostic(
        string countLabel,
        int spectatorCount,
        int authoritativeCount,
        List<string> errors)
    {
        if (spectatorCount == authoritativeCount)
        {
            return;
        }

        errors.Add(
            $"spectator replay frame timing battle damage assignment {countLabel} count {spectatorCount} does not match authoritative state battle damage assignment {countLabel} count {authoritativeCount}; {FormatExpectedActualForRecovery(authoritativeCount, spectatorCount)}");
    }

    private static void ValidateSpectatorBattleDamageAssignmentAuthoritativeValues(
        object? damageAssignmentPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        var expectedPending = ResolutionResult.HasOpenBattleDamageAssignmentWindow(authoritativeState);
        ValidateSpectatorBattleDamageAssignmentBoolAuthoritativeValue(
            damageAssignmentPayload,
            "isPending",
            "pending flag",
            expectedPending,
            reportUnreadableWhenExpected: expectedPending,
            errors);

        if (!expectedPending)
        {
            return;
        }

        var authoritativeBattle = authoritativeState.BattleState;
        ValidateSpectatorBattleDamageAssignmentStringAuthoritativeValue(
            damageAssignmentPayload,
            "phase",
            "phase",
            "DAMAGE_ASSIGNMENT",
            errors);
        ValidateSpectatorBattleDamageAssignmentStringAuthoritativeValue(
            damageAssignmentPayload,
            "battleId",
            "battle id",
            authoritativeBattle.BattleId,
            errors);
        ValidateSpectatorBattleDamageAssignmentStringAuthoritativeValue(
            damageAssignmentPayload,
            "battlefieldId",
            "battlefield object id",
            authoritativeBattle.BattlefieldObjectId,
            errors);
        ValidateSpectatorBattleDamageAssignmentStringAuthoritativeValue(
            damageAssignmentPayload,
            "assigningPlayerId",
            "assigning player id",
            ResolutionResult.BattleDamageAssigningPlayerId(authoritativeState),
            errors);

        var authoritativeDamagePool =
            ResolutionResult.BattleDamagePoolFor(authoritativeState, authoritativeBattle);
        ValidateSpectatorBattleDamageAssignmentIntDictionaryAuthoritativeValue(
            damageAssignmentPayload,
            "damagePool",
            "damage pool",
            authoritativeDamagePool,
            errors);

        var authoritativeLegalTargets =
            ResolutionResult.BattleDamageLegalTargetsFor(authoritativeState, authoritativeBattle);
        ValidateSpectatorBattleDamageAssignmentStringListDictionaryAuthoritativeValue(
            damageAssignmentPayload,
            "legalTargets",
            "legal targets",
            authoritativeLegalTargets,
            errors);

        var authoritativeExistingDamage =
            ResolutionResult.BattleExistingDamageFor(authoritativeState, authoritativeBattle);
        ValidateSpectatorBattleDamageAssignmentIntDictionaryAuthoritativeValue(
            damageAssignmentPayload,
            "existingDamage",
            "existing damage",
            authoritativeExistingDamage,
            errors);

        var authoritativeLethalDamageThreshold =
            ResolutionResult.BattleLethalDamageThresholdFor(authoritativeState, authoritativeBattle);
        ValidateSpectatorBattleDamageAssignmentIntDictionaryAuthoritativeValue(
            damageAssignmentPayload,
            "lethalDamageThreshold",
            "lethal damage threshold",
            authoritativeLethalDamageThreshold,
            errors);

        var authoritativeRequiredAssignments =
            ResolutionResult.BattleRequiredAssignmentsFor(authoritativeState, authoritativeBattle);
        ValidateSpectatorBattleDamageAssignmentRequiredAssignmentsAuthoritativeValue(
            damageAssignmentPayload,
            "requiredAssignments",
            "required assignments",
            authoritativeRequiredAssignments,
            errors);
    }

    private static void ValidateSpectatorBattleDamageAssignmentBoolAuthoritativeValue(
        object? damageAssignmentPayload,
        string key,
        string fieldLabel,
        bool expected,
        bool reportUnreadableWhenExpected,
        List<string> errors)
    {
        var readable = TryReadObjectBool(damageAssignmentPayload, key, out var actual);
        if (readable)
        {
            if (actual != expected)
            {
                AddSpectatorBattleDamageAssignmentValueMismatch(fieldLabel, expected, actual, errors);
            }

            return;
        }

        if (reportUnreadableWhenExpected)
        {
            AddSpectatorBattleDamageAssignmentValueMismatch(
                fieldLabel,
                expected,
                FormatUnreadableObjectValue(damageAssignmentPayload, key),
                errors);
        }
    }

    private static void ValidateSpectatorBattleDamageAssignmentStringAuthoritativeValue(
        object? damageAssignmentPayload,
        string key,
        string fieldLabel,
        string? expected,
        List<string> errors)
    {
        var readable = TryReadObjectString(damageAssignmentPayload, key, out var actual);
        if (readable
            && string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        AddSpectatorBattleDamageAssignmentValueMismatch(
            fieldLabel,
            expected,
            readable ? actual : FormatUnreadableObjectValue(damageAssignmentPayload, key),
            errors);
    }

    private static void ValidateSpectatorBattleDamageAssignmentIntDictionaryAuthoritativeValue(
        object? damageAssignmentPayload,
        string key,
        string fieldLabel,
        IReadOnlyDictionary<string, int> expected,
        List<string> errors)
    {
        var readable = TryReadObjectIntDictionary(damageAssignmentPayload, key, out var actual);
        if (readable && IntDictionariesEqual(actual, expected))
        {
            return;
        }

        if (expected.Count == 0 && !readable)
        {
            return;
        }

        AddSpectatorBattleDamageAssignmentValueMismatch(
            fieldLabel,
            expected,
            readable ? actual : FormatUnreadableObjectValue(damageAssignmentPayload, key),
            errors);
    }

    private static void ValidateSpectatorBattleDamageAssignmentStringListDictionaryAuthoritativeValue(
        object? damageAssignmentPayload,
        string key,
        string fieldLabel,
        IReadOnlyDictionary<string, IReadOnlyList<string>> expected,
        List<string> errors)
    {
        var readable = TryReadObjectStringListDictionary(damageAssignmentPayload, key, out var actual);
        if (readable && StringListDictionariesEqual(actual, expected))
        {
            return;
        }

        if (expected.Count == 0 && !readable)
        {
            return;
        }

        AddSpectatorBattleDamageAssignmentValueMismatch(
            fieldLabel,
            expected,
            readable ? actual : FormatUnreadableObjectValue(damageAssignmentPayload, key),
            errors);
    }

    private static void ValidateSpectatorBattleDamageAssignmentRequiredAssignmentsAuthoritativeValue(
        object? damageAssignmentPayload,
        string key,
        string fieldLabel,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> expectedPayload,
        List<string> errors)
    {
        var expected = TryConvertRequiredAssignmentObjects(expectedPayload, out var parsedExpected)
            ? parsedExpected
            : [];
        var readable = TryReadObjectRequiredAssignments(damageAssignmentPayload, key, out var actual);
        if (readable && RequiredAssignmentsEqual(actual, expected))
        {
            return;
        }

        if (expected.Count == 0 && !readable)
        {
            return;
        }

        AddSpectatorBattleDamageAssignmentValueMismatch(
            fieldLabel,
            expected,
            readable ? actual : FormatUnreadableObjectValue(damageAssignmentPayload, key),
            errors);
    }

    private static void AddSpectatorBattleDamageAssignmentValueMismatch(
        string fieldLabel,
        object? expected,
        object? actual,
        List<string> errors)
    {
        errors.Add(
            $"spectator replay frame timing battle damage assignment {fieldLabel} does not match authoritative state battle damage assignment {fieldLabel}; {FormatExpectedActualForRecovery(expected, actual)}");
    }

    private static void ValidateSpectatorBattleDamageAssignmentMapPayloadShapes(
        object? damageAssignmentPayload,
        List<string> errors)
    {
        ValidateBattleDamageAssignmentMapPayloadShapes(
            damageAssignmentPayload,
            "spectator replay frame timing battle damage assignment",
            errors);
    }

    private static void ValidateBattleDamageAssignmentMapPayloadShapes(
        object? damageAssignmentPayload,
        string payloadLabel,
        List<string> errors)
    {
        ValidateBattleDamageAssignmentMapPayloadShape(
            damageAssignmentPayload,
            "damagePool",
            payloadLabel,
            "damage pool",
            IsSnapshotIntMapPayloadObject,
            errors);
        ValidateBattleDamageAssignmentMapPayloadShape(
            damageAssignmentPayload,
            "legalTargets",
            payloadLabel,
            "legal targets",
            IsSnapshotStringListMapPayloadObject,
            errors);
        ValidateBattleDamageAssignmentMapPayloadShape(
            damageAssignmentPayload,
            "existingDamage",
            payloadLabel,
            "existing damage",
            IsSnapshotIntMapPayloadObject,
            errors);
        ValidateBattleDamageAssignmentMapPayloadShape(
            damageAssignmentPayload,
            "lethalDamageThreshold",
            payloadLabel,
            "lethal damage threshold",
            IsSnapshotIntMapPayloadObject,
            errors);
    }

    private static void ValidateBattleDamageAssignmentMapPayloadShape(
        object? damageAssignmentPayload,
        string payloadKey,
        string payloadLabel,
        string mapLabel,
        Func<object?, bool> isValidMapPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(damageAssignmentPayload, payloadKey, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload))
        {
            return;
        }

        if (!isValidMapPayload(mapPayload))
        {
            errors.Add($"{payloadLabel} {mapLabel} map payload is required");
        }
    }

    private static void ValidateSpectatorBattleDamageAssignmentRequiredAssignmentItemPayloadShapes(
        object? damageAssignmentPayload,
        List<string> errors)
    {
        ValidateBattleDamageAssignmentRequiredAssignmentItemPayloadShapes(
            damageAssignmentPayload,
            "spectator replay frame timing battle damage assignment",
            errors);
    }

    private static void ValidateBattleDamageAssignmentRequiredAssignmentItemPayloadShapes(
        object? damageAssignmentPayload,
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

            ValidateBattleDamageAssignmentRequiredAssignmentItemPayloadShape(
                requiredAssignment,
                payloadLabel,
                errors);
        }
    }

    private static void ValidateBattleDamageAssignmentRequiredAssignmentItemPayloadShape(
        object? requiredAssignmentPayload,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(requiredAssignmentPayload, "legalTargetObjectIds", out var legalTargetObjectIdsPayload)
            || IsNullSnapshotPayloadValue(legalTargetObjectIdsPayload))
        {
            return;
        }

        if (!TryReadStringListValue(legalTargetObjectIdsPayload, out _))
        {
            errors.Add($"{payloadLabel} required assignment item legal target object id list payload is required");
        }
    }
}
