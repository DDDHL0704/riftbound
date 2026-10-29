using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateSnapshotTimingTurnWindowPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "turnWindow", out var turnWindowPayload)
            || !IsSnapshotPlayerPayloadObject(turnWindowPayload))
        {
            return;
        }

        ValidateTurnWindowPayloadValues(
            turnWindowPayload,
            $"snapshot for {view.PlayerId} timing turn window",
            errors);
    }

    private static void ValidateSnapshotTimingSpellDuelPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "spellDuel", out var spellDuelPayload)
            || !IsSnapshotPlayerPayloadObject(spellDuelPayload))
        {
            return;
        }

        ValidateSnapshotTimingSpellDuelListPayloadShapes(
            view,
            spellDuelPayload,
            errors);
        ValidateSpellDuelPayloadValues(
            spellDuelPayload,
            $"snapshot for {view.PlayerId} timing spell duel",
            errors);
    }

    private static void ValidateSnapshotTimingSpellDuelListPayloadShapes(
        RecoveredPlayerView view,
        object? spellDuelPayload,
        List<string> errors)
    {
        var payloadLabel = $"snapshot for {view.PlayerId} timing spell duel";
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            spellDuelPayload,
            "passedFocusPlayerIds",
            payloadLabel,
            "passed focus player id",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            spellDuelPayload,
            "stackItemIds",
            payloadLabel,
            "stack item id",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            spellDuelPayload,
            "stackControllerIds",
            payloadLabel,
            "stack controller id",
            errors);
    }

    private static void ValidateSnapshotTimingBattlePayloadValues(
        RecoveredPlayerView view,
        List<string> errors,
        MatchState? authoritativeState)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            || !IsSnapshotPlayerPayloadObject(battlePayload))
        {
            return;
        }

        ValidateSnapshotTimingBattleParticipantPayloadShapes(
            view,
            battlePayload,
            errors);
        var knownPlayerIds = view.Snapshot.Players is null
            ? null
            : BuildNormalizedPlayerIdSet(view.Snapshot.Players.Keys);
        var knownObjectIds = view.Snapshot.Players is null
            ? null
            : BuildSnapshotKnownObjectIds(view.Snapshot);
        ValidateBattlePayloadValues(
            battlePayload,
            $"snapshot for {view.PlayerId} timing battle",
            errors);
        ValidateBattleActiveParticipantConsistency(
            battlePayload,
            $"snapshot for {view.PlayerId} timing battle",
            errors,
            authoritativeState);
        ValidateBattlePlayerReferences(
            battlePayload,
            $"snapshot for {view.PlayerId} timing battle",
            knownPlayerIds,
            "players",
            errors);
        ValidateBattleObjectReferences(
            battlePayload,
            $"snapshot for {view.PlayerId} timing battle",
            knownObjectIds,
            "objects",
            errors);
        ValidateBattleParticipantControllerMembership(
            battlePayload,
            $"snapshot for {view.PlayerId} timing battle",
            errors);
    }

    private static void ValidateSnapshotTimingBattleParticipantPayloadShapes(
        RecoveredPlayerView view,
        object? battlePayload,
        List<string> errors)
    {
        var payloadLabel = $"snapshot for {view.PlayerId} timing battle";
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            battlePayload,
            "attackerObjectIds",
            payloadLabel,
            "attacker object id",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            battlePayload,
            "defenderObjectIds",
            payloadLabel,
            "defender object id",
            errors);

        if (!TryReadObjectValue(battlePayload, "participantControllerIds", out var participantControllerIdsPayload)
            || IsNullSnapshotPayloadValue(participantControllerIdsPayload))
        {
            return;
        }

        if (!IsSnapshotStringMapPayloadObject(participantControllerIdsPayload))
        {
            errors.Add($"{payloadLabel} participant controller map payload is required");
        }
    }

    private static void ValidateSnapshotTimingBattleParticipantControllerPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            || !IsSnapshotPlayerPayloadObject(battlePayload)
            || !TryReadObjectValue(battlePayload, "participantControllerIds", out var participantControllerIdsPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            participantControllerIdsPayload,
            $"snapshot for {view.PlayerId} timing battle participant controllers",
            errors);
    }

    private static void ValidateSnapshotTimingBattleDamageAssignmentPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            || !IsSnapshotPlayerPayloadObject(battlePayload)
            || !TryReadObjectValue(battlePayload, "damageAssignment", out var damageAssignmentPayload)
            || !IsSnapshotPlayerPayloadObject(damageAssignmentPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            damageAssignmentPayload,
            $"snapshot for {view.PlayerId} timing battle damage assignment",
            errors);
    }

    private static void ValidateSnapshotTimingBattleDamageAssignmentPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            || !IsSnapshotPlayerPayloadObject(battlePayload)
            || !TryReadObjectValue(battlePayload, "damageAssignment", out var damageAssignmentPayload)
            || IsNullSnapshotPayloadValue(damageAssignmentPayload))
        {
            return;
        }

        const string payloadSuffix = "timing battle damage assignment";
        var payloadLabel = $"snapshot for {view.PlayerId} {payloadSuffix}";
        if (!IsSnapshotPlayerPayloadObject(damageAssignmentPayload))
        {
            errors.Add($"{payloadLabel} payload is invalid");
            return;
        }

        ValidateSnapshotPayloadRequiredBoolValue(
            damageAssignmentPayload,
            "isPending",
            payloadLabel,
            "pending flag",
            errors);
        ValidateBattleDamageAssignmentMapPayloadShapes(
            damageAssignmentPayload,
            payloadLabel,
            errors);
        ValidateBattleDamageAssignmentRequiredAssignmentItemPayloadShapes(
            damageAssignmentPayload,
            payloadLabel,
            errors);

        var knownPlayerIds = view.Snapshot.Players is null
            ? null
            : BuildNormalizedPlayerIdSet(view.Snapshot.Players.Keys);
        var knownObjectIds = view.Snapshot.Players is null
            ? null
            : BuildSnapshotKnownObjectIds(view.Snapshot);
        var shouldValidatePendingFields =
            (TryReadObjectBool(damageAssignmentPayload, "isPending", out var isPending) && isPending)
            || HasBattleDamageAssignmentPendingFields(damageAssignmentPayload);
        ValidateBattleDamageAssignmentPendingPayloadValues(
            damageAssignmentPayload,
            payloadLabel,
            shouldValidatePendingFields,
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
            knownPlayerIds,
            "players",
            errors);
        ValidateBattleDamageAssignmentObjectReferences(
            damageAssignmentPayload,
            payloadLabel,
            knownObjectIds,
            "objects",
            errors);
    }

    private static void ValidateSnapshotTimingBattleDamageAssignmentMapPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            || !IsSnapshotPlayerPayloadObject(battlePayload)
            || !TryReadObjectValue(battlePayload, "damageAssignment", out var damageAssignmentPayload)
            || !IsSnapshotPlayerPayloadObject(damageAssignmentPayload))
        {
            return;
        }

        ValidateSnapshotTimingBattleDamageAssignmentMapPayloadPropertyNames(
            view,
            damageAssignmentPayload,
            "damagePool",
            "damage pool",
            errors);
        ValidateSnapshotTimingBattleDamageAssignmentMapPayloadPropertyNames(
            view,
            damageAssignmentPayload,
            "legalTargets",
            "legal targets",
            errors);
        ValidateSnapshotTimingBattleDamageAssignmentMapPayloadPropertyNames(
            view,
            damageAssignmentPayload,
            "existingDamage",
            "existing damage",
            errors);
        ValidateSnapshotTimingBattleDamageAssignmentMapPayloadPropertyNames(
            view,
            damageAssignmentPayload,
            "lethalDamageThreshold",
            "lethal damage threshold",
            errors);
    }

    private static void ValidateSnapshotTimingBattleDamageAssignmentMapPayloadPropertyNames(
        RecoveredPlayerView view,
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
            $"snapshot for {view.PlayerId} timing battle damage assignment {payloadLabel}",
            errors);
    }

    private static void ValidateSnapshotTimingBattleDamageAssignmentRequiredAssignmentPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            || !IsSnapshotPlayerPayloadObject(battlePayload)
            || !TryReadObjectValue(battlePayload, "damageAssignment", out var damageAssignmentPayload)
            || !IsSnapshotPlayerPayloadObject(damageAssignmentPayload)
            || !TryReadObjectList(damageAssignmentPayload, "requiredAssignments", out var requiredAssignments))
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
                $"snapshot for {view.PlayerId} timing battle damage assignment required assignment item",
                errors);
        }
    }

    private static void ValidateSnapshotTimingPaymentPowerTraitPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null)
        {
            return;
        }

        if (TryReadObjectValue(view.Snapshot.Timing, "pendingPayment", out var pendingPaymentPayload)
            && IsSnapshotPlayerPayloadObject(pendingPaymentPayload)
            && TryReadObjectValue(pendingPaymentPayload, "cost", out var costPayload)
            && IsSnapshotPlayerPayloadObject(costPayload)
            && TryReadObjectValue(costPayload, "powerByTrait", out var powerByTraitPayload))
        {
            ValidateSnapshotPayloadObjectPropertyNames(
                powerByTraitPayload,
                $"snapshot for {view.PlayerId} timing pending payment power cost traits",
                errors);
        }


    }

    private static void ValidateSnapshotTimingPaymentPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingPayment", out var pendingPaymentPayload)
            || !IsSnapshotPlayerPayloadObject(pendingPaymentPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            pendingPaymentPayload,
            $"snapshot for {view.PlayerId} timing pending payment",
            errors);

        if (!TryReadObjectValue(pendingPaymentPayload, "cost", out var costPayload)
            || !IsSnapshotPlayerPayloadObject(costPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            costPayload,
            $"snapshot for {view.PlayerId} timing pending payment cost",
            errors);
    }

    private static void ValidateSnapshotTimingPendingPaymentPayloadShape(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingPayment", out var pendingPaymentPayload)
            || IsNullSnapshotPayloadValue(pendingPaymentPayload))
        {
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(pendingPaymentPayload))
        {
            errors.Add($"snapshot for {view.PlayerId} timing pending payment payload is required");
        }
    }

    private static void ValidateSnapshotTimingPendingPaymentListPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingPayment", out var pendingPaymentPayload)
            || !IsSnapshotPlayerPayloadObject(pendingPaymentPayload))
        {
            return;
        }

        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            pendingPaymentPayload,
            "paymentChoices",
            $"snapshot for {view.PlayerId} timing pending payment",
            "payment choice",
            errors);
        ValidateSnapshotPayloadStringListValues(
            pendingPaymentPayload,
            "paymentChoices",
            $"snapshot for {view.PlayerId} timing pending payment",
            "payment choice",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            pendingPaymentPayload,
            "paymentResourceActions",
            $"snapshot for {view.PlayerId} timing pending payment",
            "payment resource action",
            errors);
        ValidateSnapshotPayloadStringListValues(
            pendingPaymentPayload,
            "paymentResourceActions",
            $"snapshot for {view.PlayerId} timing pending payment",
            "payment resource action",
            errors);
    }

    private static void ValidateSnapshotTimingPendingPaymentScalarPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingPayment", out var pendingPaymentPayload)
            || !IsSnapshotPlayerPayloadObject(pendingPaymentPayload))
        {
            return;
        }

        var payloadLabel = $"snapshot for {view.PlayerId} timing pending payment";
        ValidateSnapshotPayloadRequiredStringValue(
            pendingPaymentPayload,
            "paymentId",
            payloadLabel,
            "payment id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            pendingPaymentPayload,
            "paymentWindow",
            payloadLabel,
            "payment window",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            pendingPaymentPayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);

        if (!TryReadObjectValue(pendingPaymentPayload, "cost", out var costPayload)
            || IsNullSnapshotPayloadValue(costPayload))
        {
            errors.Add($"{payloadLabel} cost is required");
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(costPayload))
        {
            errors.Add($"{payloadLabel} cost payload is required");
            return;
        }

        var costLabel = $"{payloadLabel} cost";
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            costPayload,
            "mana",
            costLabel,
            "mana",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            costPayload,
            "power",
            costLabel,
            "power",
            errors);
        ValidateSnapshotPayloadRequiredIntMapPayloadShape(
            costPayload,
            "powerByTrait",
            costLabel,
            "power cost trait",
            errors);
        ValidateSnapshotPayloadRequiredPositiveIntMapValues(
            costPayload,
            "powerByTrait",
            costLabel,
            "power cost trait",
            errors);
    }

    private static void ValidateSnapshotTimingPendingTaskQueuePayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingTaskQueue", out var queuePayload)
            || !IsSnapshotPlayerPayloadObject(queuePayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            queuePayload,
            $"snapshot for {view.PlayerId} timing pending task queue",
            errors);

        if (TryReadObjectList(queuePayload, "tasks", out var taskPayloads))
        {
            foreach (var taskPayload in taskPayloads)
            {
                if (!IsSnapshotPlayerPayloadObject(taskPayload))
                {
                    continue;
                }

                ValidateSnapshotPayloadObjectPropertyNames(
                    taskPayload,
                    $"snapshot for {view.PlayerId} timing pending task queue task item",
                    errors);
            }
        }

        if (!TryReadObjectValue(queuePayload, "metadata", out var metadataPayload)
            || !IsSnapshotPlayerPayloadObject(metadataPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            metadataPayload,
            $"snapshot for {view.PlayerId} timing pending task queue metadata",
            errors);
    }

    private static void ValidateSnapshotTimingPendingTaskQueuePayloadShape(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingTaskQueue", out var queuePayload)
            || IsNullSnapshotPayloadValue(queuePayload))
        {
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(queuePayload))
        {
            errors.Add($"snapshot for {view.PlayerId} timing pending task queue payload is required");
        }
    }

    private static void ValidateSnapshotTimingPendingTaskQueueTaskPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingTaskQueue", out var queuePayload)
            || !IsSnapshotPlayerPayloadObject(queuePayload)
            || !TryReadObjectList(queuePayload, "tasks", out var taskPayloads))
        {
            return;
        }

        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload))
            {
                errors.Add($"snapshot for {view.PlayerId} timing pending task queue task payload is required");
            }
        }
    }

    private static void ValidateSnapshotTimingPendingTaskQueueMetadataPayloadShape(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingTaskQueue", out var queuePayload)
            || !IsSnapshotPlayerPayloadObject(queuePayload))
        {
            return;
        }

        if (!TryReadObjectValue(queuePayload, "metadata", out var metadataPayload)
            || IsNullSnapshotPayloadValue(metadataPayload))
        {
            errors.Add($"snapshot for {view.PlayerId} timing pending task queue metadata is required");
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(metadataPayload))
        {
            errors.Add($"snapshot for {view.PlayerId} timing pending task queue metadata payload is required");
        }
    }

    private static void ValidateSnapshotTimingPendingTaskQueuePayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingTaskQueue", out var queuePayload)
            || !IsSnapshotPlayerPayloadObject(queuePayload))
        {
            return;
        }

        var payloadLabel = $"snapshot for {view.PlayerId} timing pending task queue";
        var queueResult = ValidatePendingTaskQueuePayloadValues(queuePayload, payloadLabel, errors);
        var activeTaskId = queueResult.ActiveTaskId;
        int? taskPayloadCount = null;
        var taskKinds = new List<string>();
        var taskKindsAreValid = true;

        if (TryReadObjectList(queuePayload, "tasks", out var taskPayloads))
        {
            taskPayloadCount = taskPayloads.Count;
            var seenTaskIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var taskPayload in taskPayloads)
            {
                if (!IsSnapshotPlayerPayloadObject(taskPayload))
                {
                    continue;
                }

                var taskResult = ValidatePendingTaskQueueTaskPayloadValues(
                    taskPayload,
                    $"{payloadLabel} task item",
                    errors);
                var taskId = taskResult.TaskId;
                if (taskId is not null && !seenTaskIds.Add(taskId))
                {
                    errors.Add($"{payloadLabel} task item task id {taskId} is duplicated");
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
                errors.Add(
                    $"{payloadLabel} active task id {activeTaskId} does not match a pending task queue task id; {FormatExpectedActualForRecovery(FormatObjectIdsForRecovery(seenTaskIds), activeTaskId)}");
            }

            if (taskPayloads.Count > 0 && string.IsNullOrEmpty(activeTaskId))
            {
                errors.Add(
                    $"{payloadLabel} active task id is required when pending task queue has tasks; {FormatExpectedActualForRecovery(FormatObjectIdsForRecovery(seenTaskIds), activeTaskId ?? string.Empty)}");
            }

            ValidateSnapshotPendingTaskQueueFlagsMatchTaskCount(
                queueResult,
                taskPayloads.Count,
                payloadLabel,
                errors);
        }

        if (!TryReadObjectValue(queuePayload, "metadata", out var metadataPayload)
            || !IsSnapshotPlayerPayloadObject(metadataPayload))
        {
            return;
        }

        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            metadataPayload,
            "stateBasedTaskKinds",
            $"{payloadLabel} metadata",
            "state-based task kind",
            errors);
        var metadataTaskCount = ValidatePendingTaskQueueMetadataPayloadValues(
            metadataPayload,
            $"{payloadLabel} metadata",
            errors);
        if (taskPayloadCount is int actualTaskCount
            && metadataTaskCount is int expectedTaskCount
            && expectedTaskCount >= 0
            && expectedTaskCount != actualTaskCount)
        {
            errors.Add(
                $"{payloadLabel} metadata task count {expectedTaskCount} does not match pending task queue task count {actualTaskCount}; {FormatExpectedActualForRecovery(actualTaskCount, expectedTaskCount)}");
        }

        if (taskPayloadCount is not null
            && taskKindsAreValid
            && TryReadObjectStringList(metadataPayload, "stateBasedTaskKinds", out var stateBasedTaskKinds))
        {
            var expectedStateBasedTaskKinds = ExpectedStateBasedTaskKinds(taskKinds);
            if (!StringListsEqual(stateBasedTaskKinds, expectedStateBasedTaskKinds))
            {
                errors.Add(
                    $"{payloadLabel} metadata state-based task kinds do not match pending task queue task kinds; {FormatExpectedActualForRecovery(expectedStateBasedTaskKinds, stateBasedTaskKinds)}");
            }
        }
    }

    private static void ValidateSnapshotTimingPendingHandChoicePayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingHandChoice", out var choicePayload)
            || !IsSnapshotPlayerPayloadObject(choicePayload))
        {
            return;
        }

        var payloadLabel = $"snapshot for {view.PlayerId} timing pending hand choice";
        ValidatePendingHandChoicePayloadValues(
            choicePayload,
            payloadLabel,
            IsKnownSnapshotPendingHandChoiceState,
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            choicePayload,
            "legalObjectIds",
            payloadLabel,
            "legal object id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            choicePayload,
            "legalObjectIds",
            payloadLabel,
            "legal object id",
            errors);
    }

    private static void ValidateSnapshotTimingPendingCardChoicePayloadValues(
        RecoveredPlayerView view,
        List<string> errors,
        MatchState? authoritativeState)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "pendingCardChoice", out var choicePayload)
            || !IsSnapshotPlayerPayloadObject(choicePayload))
        {
            return;
        }

        var payloadLabel = $"snapshot for {view.PlayerId} timing pending card choice";
        var privateChoice = authoritativeState?.PendingCardChoice is { DeckContext: not null } choice && choice.PlayerId != view.PlayerId;
        ValidatePendingCardChoicePayloadValues(
            choicePayload,
            payloadLabel,
            IsKnownSnapshotPendingHandChoiceState,
            errors, privateChoice);
        if (privateChoice) ValidatePrivateDeckChoiceEnvelope(choicePayload, authoritativeState!.PendingCardChoice!, payloadLabel, errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            choicePayload,
            "legalObjectIds",
            payloadLabel,
            "legal object id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            choicePayload,
            "legalObjectIds",
            payloadLabel,
            "legal object id",
            errors);
    }

    private static bool IsKnownSnapshotPendingHandChoiceState(string value)
    {
        return string.Equals(value, "PENDING_CHOICE", StringComparison.Ordinal)
            || string.Equals(value, "WAITING_FOR_CHOICE", StringComparison.Ordinal);
    }

    private static void ValidateSnapshotTimingRequiredString(
        RecoveredPlayerView view,
        string key,
        string label,
        List<string> errors)
    {
        if (!TryReadString(view.Snapshot.Timing, key, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} is required");
        }
    }

    private static void ValidateSnapshotTimingAllowedString(
        RecoveredPlayerView view,
        string key,
        string label,
        Func<string, bool> isKnownValue,
        List<string> errors)
    {
        if (!TryReadString(view.Snapshot.Timing, key, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} is required");
            return;
        }

        if (!isKnownValue(value))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} {value} is invalid");
        }
    }

    private static void ValidateSnapshotStackItemPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        for (var index = 0; index < view.Snapshot.Stack.Count; index++)
        {
            var stackItem = view.Snapshot.Stack[index];
            if (!IsSnapshotPlayerPayloadObject(stackItem))
            {
                continue;
            }

            var stackItemLabel = SnapshotStackItemLabel(stackItem, index);
            ValidateSnapshotPayloadObjectPropertyNames(
                stackItem,
                $"snapshot for {view.PlayerId} stack item {stackItemLabel}",
                errors);
        }
    }

    private static void ValidateSnapshotStackItemPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        for (var index = 0; index < view.Snapshot.Stack.Count; index++)
        {
            var stackItem = view.Snapshot.Stack[index];
            if (IsSnapshotPlayerPayloadObject(stackItem))
            {
                continue;
            }

            var stackItemLabel = SnapshotStackItemLabel(stackItem, index);
            errors.Add($"snapshot for {view.PlayerId} stack item {stackItemLabel} payload is required");
        }
    }

    private static void ValidateSnapshotStackItemPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        var seenStackItemIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < view.Snapshot.Stack.Count; index++)
        {
            var stackItem = view.Snapshot.Stack[index];
            if (!IsSnapshotPlayerPayloadObject(stackItem))
            {
                continue;
            }

            var stackItemLabel = SnapshotStackItemLabel(stackItem, index);
            var stackItemId = ValidateStackItemPayloadValues(
                stackItem,
                $"snapshot for {view.PlayerId} stack item {stackItemLabel}",
                errors);
            if (stackItemId is not null && !seenStackItemIds.Add(stackItemId))
            {
                errors.Add($"snapshot for {view.PlayerId} stack item {stackItemId} is duplicated");
            }
        }
    }

    private static string SnapshotStackItemLabel(object? stackItem, int index)
    {
        return TryReadObjectString(stackItem, "stackItemId", out var stackItemId)
            && !string.IsNullOrWhiteSpace(stackItemId)
                ? stackItemId
                : $"#{index + 1}";
    }

    private static void ValidateSnapshotTimingListItemPayloadPropertyNames(
        RecoveredPlayerView view,
        string key,
        string payloadLabel,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, key, out var payloads))
        {
            return;
        }

        foreach (var payload in payloads)
        {
            if (!IsSnapshotPlayerPayloadObject(payload))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                payload,
                $"snapshot for {view.PlayerId} timing {payloadLabel} item",
                errors);
        }
    }

    private static void ValidateSnapshotTimingListItemPayloadShapes(
        RecoveredPlayerView view,
        string key,
        string payloadLabel,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, key, out var payloads))
        {
            return;
        }

        foreach (var payload in payloads)
        {
            if (!IsSnapshotPlayerPayloadObject(payload))
            {
                errors.Add($"snapshot for {view.PlayerId} timing {payloadLabel} payload is required");
            }
        }
    }

    private static void ValidateSnapshotTimingContinuousEffectScalarPayloadValues(
        RecoveredPlayerView view,
        List<string> errors,
        MatchState? authoritativeState)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, "continuousEffects", out var effectPayloads))
        {
            return;
        }

        var seenEffectIds = new HashSet<string>(StringComparer.Ordinal);
        var seenSequences = new HashSet<int>();
        var validSequences = new List<int>();
        var knownObjectIds = view.Snapshot.Players is null
            ? null
            : BuildSnapshotKnownObjectIds(view.Snapshot);
        var expectedEffects = authoritativeState?.Tick == view.SnapshotTick
            ? BuildAuthoritativeContinuousEffectsById(authoritativeState.ContinuousEffects) : null;
        foreach (var effectPayload in effectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(effectPayload))
            {
                continue;
            }

            var effectValues = ValidateContinuousEffectScalarPayloadValues(
                effectPayload,
                $"snapshot for {view.PlayerId} timing continuous effect item",
                errors);
            string? historicalSource = null;
            if (effectValues.EffectId is { } id && expectedEffects?.TryGetValue(id, out var expected) == true
                && expected.Scope == "OBJECT" && expected.Duration == "UNTIL_END_OF_TURN"
                && (expected.Layer == ContinuousEffectLayers.PowerModifier
                    || expected.Layer == ContinuousEffectLayers.StaticAura && expected.EffectKind == CoreRuleEngine.BoonAmplifierEffect
                        && authoritativeState is not null && CoreRuleEngine.ValidCurrentBoonAmplifiers(authoritativeState)))
            {
                var sourceMatches = OptionalStringSnapshotValueMatches(effectPayload, "sourceObjectId", expected.SourceObjectId)
                    && OptionalStringSnapshotValueMatches(effectPayload, "sourceCardNo", expected.SourceCardNo)
                    && OptionalStringSnapshotValueMatches(effectPayload, "sourcePath", expected.SourcePath)
                    && OptionalStringSnapshotValueMatches(effectPayload, "targetObjectId", expected.TargetObjectId);
                if (!sourceMatches)
                    errors.Add($"snapshot for {view.PlayerId} timing continuous effect {id} source provenance disagrees with authoritative state");
                // A resolved modifier keeps its public source reference even when
                // that source enters a private zone. Only the exact authoritative
                // reference may be absent from the current visible objects.
                if (sourceMatches && knownObjectIds?.Contains(expected.TargetObjectId ?? string.Empty) == true)
                    historicalSource = expected.SourceObjectId;
            }
            if (effectValues.EffectId is not null && !seenEffectIds.Add(effectValues.EffectId))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} timing continuous effect item effect id {effectValues.EffectId} is duplicated");
            }

            ValidateContinuousEffectSequenceValue(
                $"snapshot for {view.PlayerId} timing continuous effect item",
                effectValues.Sequence,
                seenSequences,
                validSequences,
                errors);
            ValidateContinuousEffectObjectReferences(
                effectPayload,
                $"snapshot for {view.PlayerId} timing continuous effect item",
                knownObjectIds,
                "objects",
                errors,
                historicalSource);
        }

        ValidateContinuousEffectSequenceContinuity(
            $"snapshot for {view.PlayerId} timing continuous effect item",
            validSequences,
            errors);
    }

    private static void ValidateSnapshotTimingMinimumMightProvenance(
        RecoveredPlayerView view,
        MatchState? authoritativeState,
        List<string> errors)
    {
        if (authoritativeState is null || view.SnapshotTick != authoritativeState.Tick
            || view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, "continuousEffects", out var payloads)) return;

        var expected = BuildAuthoritativeContinuousEffectsById(authoritativeState.ContinuousEffects);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var payload in payloads)
        {
            if (!TryReadObjectString(payload, "effectId", out var id) || id is null) continue;
            seen.Add(id);
            var might = expected.TryGetValue(id, out var effect) ? effect.SnapshotMight : null;
            if (!OptionalIntSnapshotValueMatches(payload, "snapshotMight", might))
                errors.Add($"snapshot for {view.PlayerId} timing continuous effect {id} snapshot Might disagrees with authoritative state");
        }
        var knownObjects = view.Snapshot.Players is null ? null : BuildSnapshotKnownObjectIds(view.Snapshot);
        foreach (var effect in expected.Values.Where(e => e.SnapshotMight.HasValue))
            if (effect.TargetObjectId is { } target && knownObjects?.Contains(target) == true && !seen.Contains(effect.EffectId))
                errors.Add($"snapshot for {view.PlayerId} timing continuous effect {effect.EffectId} snapshot Might provenance is missing");
    }

    private static void ValidateContinuousEffectSequenceValue(
        string effectLabel,
        int? sequence,
        HashSet<int> seenSequences,
        List<int> validSequences,
        List<string> errors)
    {
        if (!sequence.HasValue || sequence.Value <= 0)
        {
            return;
        }

        validSequences.Add(sequence.Value);
        if (!seenSequences.Add(sequence.Value))
        {
            errors.Add($"{effectLabel} sequence {sequence.Value} is duplicated");
        }
    }

    private static void ValidateContinuousEffectSequenceContinuity(
        string effectLabel,
        IReadOnlyList<int> validSequences,
        List<string> errors)
    {
        if (validSequences.Count == 0)
        {
            return;
        }

        var expectedSequence = 1;
        foreach (var sequence in validSequences.Distinct().OrderBy(sequence => sequence))
        {
            if (sequence != expectedSequence)
            {
                errors.Add($"{effectLabel} sequence values must be contiguous from 1");
                return;
            }

            expectedSequence++;
        }
    }

    private static void ValidateSnapshotTimingContinuousEffectListPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, "continuousEffects", out var effectPayloads))
        {
            return;
        }

        foreach (var effectPayload in effectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(effectPayload))
            {
                continue;
            }

            const string effectLabel = "continuous effect item";
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                effectPayload,
                "participantObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "participant object id",
                errors);
            ValidateSnapshotPayloadStringListValues(
                effectPayload,
                "participantObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "participant object id",
                errors);
            ValidateSnapshotPayloadPresentStringListNonEmpty(
                effectPayload,
                "participantObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "participant object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                effectPayload,
                "sourceDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "source dependency object id",
                errors);
            ValidateSnapshotPayloadStringListValues(
                effectPayload,
                "sourceDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "source dependency object id",
                errors);
            ValidateSnapshotPayloadPresentStringListNonEmpty(
                effectPayload,
                "sourceDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "source dependency object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                effectPayload,
                "targetDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "target dependency object id",
                errors);
            ValidateSnapshotPayloadStringListValues(
                effectPayload,
                "targetDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "target dependency object id",
                errors);
            ValidateSnapshotPayloadPresentStringListNonEmpty(
                effectPayload,
                "targetDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "target dependency object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                effectPayload,
                "participantDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "participant dependency object id",
                errors);
            ValidateSnapshotPayloadStringListValues(
                effectPayload,
                "participantDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "participant dependency object id",
                errors);
            ValidateSnapshotPayloadPresentStringListNonEmpty(
                effectPayload,
                "participantDependencyObjectIds",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "participant dependency object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                effectPayload,
                "deferredLayerEngineResiduals",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "deferred LayerEngine residual",
                errors);
            ValidateSnapshotPayloadStringListValues(
                effectPayload,
                "deferredLayerEngineResiduals",
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                "deferred LayerEngine residual",
                errors);
            ValidateContinuousEffectStaticAuraDependencyListConsistency(
                effectPayload,
                $"snapshot for {view.PlayerId} timing {effectLabel}",
                errors);
        }
    }

    private static void ValidateSnapshotTimingBattlefieldTaskScalarPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, "battlefieldTasks", out var taskPayloads))
        {
            return;
        }

        var seenTaskIds = new HashSet<string>(StringComparer.Ordinal);
        var knownPlayerIds = view.Snapshot.Players is null
            ? null
            : BuildNormalizedPlayerIdSet(view.Snapshot.Players.Keys);
        var knownObjectIds = view.Snapshot.Players is null
            ? null
            : BuildSnapshotKnownObjectIds(view.Snapshot);
        var objectTags = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectTagIndex(view.Snapshot);
        var knownBattlefieldStateObjectIds = BuildSnapshotBattlefieldStateObjectIds(view.Snapshot);
        var requiredTaskKindsByBattlefield = BuildSnapshotRequiredBattlefieldTaskKindsByBattlefield(view.Snapshot);
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload))
            {
                continue;
            }

            const string payloadLabel = "battlefield task item";
            var taskLabel = $"snapshot for {view.PlayerId} timing {payloadLabel}";
            var taskId = ValidateSnapshotPayloadRequiredStringValue(
                taskPayload,
                "taskId",
                taskLabel,
                "task id",
                errors);
            if (taskId is not null && !seenTaskIds.Add(taskId))
            {
                errors.Add($"{taskLabel} task id {taskId} is duplicated");
            }

            var kind = ValidateSnapshotPayloadRequiredStringValue(
                taskPayload,
                "kind",
                taskLabel,
                "kind",
                errors,
                IsKnownBattlefieldTaskKind);
            var status = ValidateSnapshotPayloadRequiredStringValue(
                taskPayload,
                "status",
                taskLabel,
                "status",
                errors,
                IsKnownBattlefieldTaskStatus);
            var reason = ValidateSnapshotPayloadRequiredStringValue(
                taskPayload,
                "reason",
                taskLabel,
                "reason",
                errors,
                IsKnownBattlefieldTaskReason);
            var battlefieldObjectId = ValidateSnapshotPayloadRequiredStringValue(
                taskPayload,
                "battlefieldObjectId",
                taskLabel,
                "battlefield object id",
                errors);
            ValidateTimingOptionalObjectReferenceWithExpectedDetails(
                taskPayload,
                "battlefieldObjectId",
                $"{taskLabel} battlefield object id",
                knownObjectIds,
                "objects",
                errors);
            ValidateBattlefieldTaskBattlefieldObjectCardMembership(
                taskPayload,
                $"{taskLabel} battlefield object id",
                objectTags,
                errors);
            ValidateBattlefieldTaskBattlefieldStateMembership(
                taskPayload,
                $"{taskLabel} battlefield object id",
                knownBattlefieldStateObjectIds,
                "battlefield states",
                errors);
            ValidateSnapshotPayloadOptionalStringValue(
                taskPayload,
                "actingPlayerId",
                taskLabel,
                "acting player id",
                errors);
            ValidateTimingOptionalPlayerReference(
                taskPayload,
                "actingPlayerId",
                $"{taskLabel} acting player id",
                knownPlayerIds,
                "players",
                errors);
            var hasSpellDuelIdPayload = TryReadObjectValue(taskPayload, "spellDuelId", out var spellDuelIdPayload)
                && !IsNullSnapshotPayloadValue(spellDuelIdPayload);
            var spellDuelId = ValidateSnapshotPayloadOptionalStringValue(
                taskPayload,
                "spellDuelId",
                taskLabel,
                "spell duel id",
                errors);
            var hasBattleIdPayload = TryReadObjectValue(taskPayload, "battleId", out var battleIdPayload)
                && !IsNullSnapshotPayloadValue(battleIdPayload);
            var battleId = ValidateSnapshotPayloadOptionalStringValue(
                taskPayload,
                "battleId",
                taskLabel,
                "battle id",
                errors);
            ValidateBattlefieldTaskDerivedTaskId(
                taskLabel,
                taskId,
                kind,
                battlefieldObjectId,
                errors);
            ValidateBattlefieldTaskDerivedIdentity(
                taskLabel,
                kind,
                battlefieldObjectId,
                spellDuelId,
                hasSpellDuelIdPayload,
                battleId,
                hasBattleIdPayload,
                errors);
            ValidateBattlefieldTaskKindSpecificIdentityExclusivity(taskLabel, kind, spellDuelId, battleId, errors);
            ValidateBattlefieldTaskKindConsistency(taskLabel, kind, status, reason, errors);
        }

        ValidateBattlefieldTaskKindSet(
            taskPayloads,
            requiredTaskKindsByBattlefield,
            $"snapshot for {view.PlayerId} timing battlefield task item",
            "battlefield state contest",
            errors);
        ValidateSnapshotBattlefieldTaskActiveSpellDuelLifecycleConsistency(
            view,
            taskPayloads,
            errors);
        ValidateSnapshotBattlefieldTaskWaitingBattleSpellDuelLifecycleConsistency(
            view,
            taskPayloads,
            errors);
        ValidateSnapshotBattlefieldTaskActiveBattleLifecycleConsistency(
            view,
            taskPayloads,
            errors);
    }

    private static void ValidateBattlefieldTaskDerivedTaskId(
        string taskLabel,
        string? taskId,
        string? kind,
        string? battlefieldObjectId,
        List<string> errors)
    {
        if (taskId is null || kind is null || battlefieldObjectId is null)
        {
            return;
        }

        var expectedTaskId = kind switch
        {
            "START_SPELL_DUEL" => $"task:start-spell-duel:{battlefieldObjectId}",
            "START_BATTLE" => $"task:start-battle:{battlefieldObjectId}",
            _ => null
        };
        if (expectedTaskId is null)
        {
            return;
        }

        if (!string.Equals(taskId, expectedTaskId, StringComparison.Ordinal))
        {
            errors.Add(
                $"{taskLabel} task id {taskId} does not match battlefield task id {expectedTaskId}; {FormatExpectedActualForRecovery(expectedTaskId, taskId)}");
        }
    }
}
