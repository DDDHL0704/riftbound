using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static IReadOnlyDictionary<string, CleanupTaskState> BuildAuthoritativePendingTaskQueueTasksByVisibleTaskId(
        MatchState authoritativeState,
        IReadOnlyList<CleanupTaskState> authoritativeTasks)
    {
        var authoritativeTasksByVisibleTaskId = new Dictionary<string, CleanupTaskState>(StringComparer.Ordinal);
        foreach (var authoritativeTask in authoritativeTasks)
        {
            var visibleTaskId = VisibleCleanupTaskIdForRecovery(authoritativeState, authoritativeTask);
            if (string.IsNullOrWhiteSpace(visibleTaskId))
            {
                continue;
            }

            var normalizedTaskId = visibleTaskId.Trim();
            if (!authoritativeTasksByVisibleTaskId.ContainsKey(normalizedTaskId))
            {
                authoritativeTasksByVisibleTaskId.Add(normalizedTaskId, authoritativeTask);
            }
        }

        return authoritativeTasksByVisibleTaskId;
    }

    private static void ValidateSpectatorPendingTaskQueueAuthoritativeTaskKeySet(
        IReadOnlyList<object?> taskPayloads,
        IReadOnlyDictionary<string, CleanupTaskState> authoritativeTasksByVisibleTaskId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending task queue task item";
        var actualTaskIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload)
                || !TryReadObjectString(taskPayload, "taskId", out var taskId)
                || string.IsNullOrWhiteSpace(taskId))
            {
                continue;
            }

            var normalizedTaskId = taskId.Trim();
            actualTaskIds.Add(normalizedTaskId);
            if (!authoritativeTasksByVisibleTaskId.ContainsKey(normalizedTaskId))
            {
                errors.Add(
                    $"{payloadLabel} task id {normalizedTaskId} is not present in authoritative state pending task queue tasks");
            }
        }

        foreach (var authoritativeTaskId in authoritativeTasksByVisibleTaskId.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!actualTaskIds.Contains(authoritativeTaskId))
            {
                errors.Add(
                    $"{payloadLabel} task id {authoritativeTaskId} is required by authoritative state pending task queue tasks");
            }
        }
    }

    private static void ValidateSpectatorPendingTaskQueueKeyedAuthoritativeValues(
        IReadOnlyList<object?> taskPayloads,
        IReadOnlyDictionary<string, CleanupTaskState> authoritativeTasksByVisibleTaskId,
        MatchState authoritativeState,
        List<string> errors)
    {
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload)
                || !TryReadObjectString(taskPayload, "taskId", out var taskId)
                || string.IsNullOrWhiteSpace(taskId))
            {
                continue;
            }

            var normalizedTaskId = taskId.Trim();
            if (!authoritativeTasksByVisibleTaskId.TryGetValue(normalizedTaskId, out var authoritativeTask))
            {
                continue;
            }

            ValidateSpectatorPendingTaskQueueKeyedRequiredStringValue(
                taskPayload,
                "kind",
                "kind",
                authoritativeTask.Kind,
                normalizedTaskId,
                errors);
            ValidateSpectatorPendingTaskQueueKeyedRequiredStringValue(
                taskPayload,
                "reason",
                "reason",
                authoritativeTask.Reason,
                normalizedTaskId,
                errors);
            ValidateSpectatorPendingTaskQueueKeyedOptionalStringValue(
                taskPayload,
                "playerId",
                "player id",
                authoritativeTask.PlayerId,
                normalizedTaskId,
                errors);
            ValidateSpectatorPendingTaskQueueKeyedOptionalStringValue(
                taskPayload,
                "battlefieldObjectId",
                "battlefield object id",
                authoritativeTask.BattlefieldObjectId,
                normalizedTaskId,
                errors);

            ValidateSpectatorPendingTaskQueueKeyedAuthoritativeObjectValues(
                taskPayload,
                authoritativeTask,
                authoritativeState,
                normalizedTaskId,
                errors);
        }
    }

    private static void ValidateSpectatorPendingTaskQueueKeyedRequiredStringValue(
        object? taskPayload,
        string key,
        string fieldLabel,
        string expected,
        string taskId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending task queue task item";
        if (!TryReadObjectString(taskPayload, key, out var actual))
        {
            var detail = FormatExpectedActualForRecovery(
                expected,
                FormatUnreadableObjectValue(taskPayload, key));
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match authoritative state pending task queue task {fieldLabel} {expected} for task id {taskId}; {detail}");
            return;
        }

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            var detail = FormatExpectedActualForRecovery(expected, actual);
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match authoritative state pending task queue task {fieldLabel} {expected} for task id {taskId}; {detail}");
        }
    }

    private static void ValidateSpectatorPendingTaskQueueKeyedOptionalStringValue(
        object? taskPayload,
        string key,
        string fieldLabel,
        string? expected,
        string taskId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending task queue task item";
        var expectedText = expected ?? string.Empty;
        if (!TryReadObjectValue(taskPayload, key, out var value))
        {
            if (!string.IsNullOrEmpty(expectedText))
            {
                errors.Add(
                    $"{payloadLabel} {fieldLabel} does not match authoritative state pending task queue task {fieldLabel} {expectedText} for task id {taskId}; {FormatExpectedActualForRecovery(expectedText, "<missing>")}");
            }

            return;
        }

        if (!TryReadOptionalStringValue(value, out var actual))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match authoritative state pending task queue task {fieldLabel} {expectedText} for task id {taskId}; {FormatExpectedActualForRecovery(expectedText, "<unreadable>")}");
            return;
        }

        if (!string.Equals(actual, expectedText, StringComparison.Ordinal))
        {
            var detail = FormatExpectedActualForRecovery(expectedText, actual);
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match authoritative state pending task queue task {fieldLabel} {expectedText} for task id {taskId}; {detail}");
        }
    }

    private static void ValidateSpectatorPendingTaskQueueKeyedAuthoritativeObjectValues(
        object? taskPayload,
        CleanupTaskState authoritativeTask,
        MatchState authoritativeState,
        string taskId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending task queue task item";
        if (ShouldHideCleanupTaskObjectIdForRecovery(authoritativeState, authoritativeTask))
        {
            if (TryReadObjectValue(taskPayload, "objectId", out _))
            {
                errors.Add(
                    $"{payloadLabel} object id does not match authoritative state pending task queue task object id redaction for task id {taskId}");
            }

            if (!TryReadObjectBool(taskPayload, "hiddenObject", out var hiddenObject)
                || !hiddenObject)
            {
                var detail = FormatExpectedActualForRecovery(
                    true,
                    TryReadObjectBool(taskPayload, "hiddenObject", out var readableHiddenObject)
                        ? readableHiddenObject
                        : FormatUnreadableObjectValue(taskPayload, "hiddenObject"));
                errors.Add(
                    $"{payloadLabel} hidden object flag {FormatBoolForRecoveryDiagnostic(hiddenObject)} does not match authoritative state pending task queue task hidden object flag true for task id {taskId}; {detail}");
            }

            if (!TryReadObjectString(taskPayload, "hiddenObjectKind", out var hiddenObjectKind)
                || !string.Equals(hiddenObjectKind, "BATTLEFIELD_STANDBY", StringComparison.Ordinal))
            {
                var detail = FormatExpectedActualForRecovery(
                    "BATTLEFIELD_STANDBY",
                    TryReadObjectString(taskPayload, "hiddenObjectKind", out var readableHiddenObjectKind)
                        ? readableHiddenObjectKind
                        : FormatUnreadableObjectValue(taskPayload, "hiddenObjectKind"));
                errors.Add(
                    $"{payloadLabel} hidden object kind {hiddenObjectKind ?? string.Empty} does not match authoritative state pending task queue task hidden object kind BATTLEFIELD_STANDBY for task id {taskId}; {detail}");
            }

            return;
        }

        ValidateSpectatorPendingTaskQueueKeyedOptionalStringValue(
            taskPayload,
            "objectId",
            "object id",
            authoritativeTask.ObjectId,
            taskId,
            errors);

        if (TryReadObjectValue(taskPayload, "hiddenObject", out _))
        {
            errors.Add(
                $"{payloadLabel} hidden object flag does not match authoritative state pending task queue task hidden object flag for task id {taskId}");
        }

        if (TryReadObjectValue(taskPayload, "hiddenObjectKind", out _))
        {
            errors.Add(
                $"{payloadLabel} hidden object kind does not match authoritative state pending task queue task hidden object kind for task id {taskId}");
        }
    }

    private static (string? ActiveTaskId, bool? HasTasks, bool? IsBlocking) ValidateSpectatorPendingTaskQueuePayloadValues(
        object? queuePayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending task queue";
        return ValidatePendingTaskQueuePayloadValues(queuePayload, payloadLabel, errors);
    }

    private static (string? ActiveTaskId, bool? HasTasks, bool? IsBlocking) ValidatePendingTaskQueuePayloadValues(
        object? queuePayload,
        string payloadLabel,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredBoolValue(
            queuePayload,
            "hasTasks",
            payloadLabel,
            "has tasks flag",
            errors);
        ValidateSnapshotPayloadRequiredBoolValue(
            queuePayload,
            "isBlocking",
            payloadLabel,
            "blocking flag",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            queuePayload,
            "phase",
            payloadLabel,
            "phase",
            errors);
        var activeTaskId = ValidateSnapshotPayloadOptionalStringValue(
            queuePayload,
            "activeTaskId",
            payloadLabel,
            "active task id",
            errors);
        var hasTasks = TryReadObjectBool(queuePayload, "hasTasks", out var hasTasksValue)
            ? hasTasksValue
            : (bool?)null;
        var isBlocking = TryReadObjectBool(queuePayload, "isBlocking", out var isBlockingValue)
            ? isBlockingValue
            : (bool?)null;

        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            queuePayload,
            "tasks",
            payloadLabel,
            "task",
            errors);

        if (!TryReadObjectValue(queuePayload, "tasks", out var tasksPayload)
            || IsNullSnapshotPayloadValue(tasksPayload))
        {
            errors.Add($"{payloadLabel} task list is required");
            return (activeTaskId, hasTasks, isBlocking);
        }

        if (!TryReadObjectListValue(tasksPayload, out _))
        {
            errors.Add($"{payloadLabel} task list is invalid");
        }

        return (activeTaskId, hasTasks, isBlocking);
    }

    private static void ValidateSnapshotPendingTaskQueueFlagsMatchTaskCount(
        (string? ActiveTaskId, bool? HasTasks, bool? IsBlocking) queueResult,
        int taskCount,
        string payloadLabel,
        List<string> errors)
    {
        var hasTasks = taskCount > 0;
        if (queueResult.HasTasks is bool actualHasTasks && actualHasTasks != hasTasks)
        {
            errors.Add(
                $"{payloadLabel} has tasks flag {FormatBoolForRecoveryDiagnostic(actualHasTasks)} does not match pending task queue task count {taskCount}; {FormatExpectedActualForRecovery(hasTasks, actualHasTasks)}");
        }

        if (queueResult.IsBlocking is bool actualIsBlocking && actualIsBlocking != hasTasks)
        {
            errors.Add(
                $"{payloadLabel} blocking flag {FormatBoolForRecoveryDiagnostic(actualIsBlocking)} does not match pending task queue task count {taskCount}; {FormatExpectedActualForRecovery(hasTasks, actualIsBlocking)}");
        }
    }

    private static string FormatBoolForRecoveryDiagnostic(bool value)
    {
        return value ? "true" : "false";
    }

    private static int? ValidateSpectatorPendingTaskQueueMetadataPayloadValues(
        object? metadataPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending task queue metadata";
        ValidateSpectatorRequiredStringListPayloadShape(
            metadataPayload,
            payloadLabel,
            "stateBasedTaskKinds",
            "state-based task kind",
            errors);
        return ValidatePendingTaskQueueMetadataPayloadValues(metadataPayload, payloadLabel, errors);
    }

    private static int? ValidatePendingTaskQueueMetadataPayloadValues(
        object? metadataPayload,
        string payloadLabel,
        List<string> errors)
    {
        var taskCount = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            metadataPayload,
            "taskCount",
            payloadLabel,
            "task count",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            metadataPayload,
            "stateBasedTaskKinds",
            payloadLabel,
            "state-based task kind",
            errors);
        return taskCount;
    }

    private static (string? TaskId, string? Kind) ValidateSpectatorPendingTaskQueueTaskPayloadValues(
        object? taskPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending task queue task item";
        return ValidatePendingTaskQueueTaskPayloadValues(taskPayload, payloadLabel, errors);
    }

    private static (string? TaskId, string? Kind) ValidatePendingTaskQueueTaskPayloadValues(
        object? taskPayload,
        string payloadLabel,
        List<string> errors)
    {
        var taskId = ValidateSnapshotPayloadRequiredStringValue(
            taskPayload,
            "taskId",
            payloadLabel,
            "task id",
            errors);
        var kind = ValidateSnapshotPayloadRequiredStringValue(
            taskPayload,
            "kind",
            payloadLabel,
            "kind",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            taskPayload,
            "reason",
            payloadLabel,
            "reason",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            taskPayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            taskPayload,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield object id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            taskPayload,
            "objectId",
            payloadLabel,
            "object id",
            errors);
        ValidateSnapshotPayloadOptionalBoolValue(
            taskPayload,
            "hiddenObject",
            payloadLabel,
            "hidden object flag",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            taskPayload,
            "hiddenObjectKind",
            payloadLabel,
            "hidden object kind",
            errors);
        return (taskId, kind);
    }

    private static string ExpectedSpectatorPendingTaskQueueActiveTaskId(MatchState authoritativeState)
    {
        var queue = authoritativeState.PendingTaskQueue;
        var activeTask = queue.Tasks.FirstOrDefault(task =>
            string.Equals(task.TaskId, queue.ActiveTaskId, StringComparison.Ordinal));
        return activeTask is null
            ? queue.ActiveTaskId ?? string.Empty
            : VisibleCleanupTaskIdForRecovery(authoritativeState, activeTask);
    }

    private static string VisibleCleanupTaskIdForRecovery(MatchState authoritativeState, CleanupTaskState task)
    {
        if (!ShouldHideCleanupTaskObjectIdForRecovery(authoritativeState, task))
        {
            return task.TaskId;
        }

        var ordinal = HiddenStandbyOrdinalForRecovery(
            authoritativeState,
            task.ObjectId!,
            task.BattlefieldObjectId);
        return $"cleanup:illegal-standby:{task.BattlefieldObjectId}:hidden-standby-{ordinal}";
    }

    private static bool ShouldHideCleanupTaskObjectIdForRecovery(MatchState authoritativeState, CleanupTaskState task)
    {
        return string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(task.ObjectId)
            && authoritativeState.CardObjects.TryGetValue(task.ObjectId, out var cardObject)
            && cardObject.IsFaceDown
            && cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal);
    }

    private static int HiddenStandbyOrdinalForRecovery(
        MatchState authoritativeState,
        string objectId,
        string? battlefieldObjectId)
    {
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !authoritativeState.BattlefieldStates.TryGetValue(battlefieldObjectId, out var battlefield))
        {
            return 1;
        }

        var hiddenIds = battlefield.StandbyObjectIds
            .Where(candidate => IsHiddenBattlefieldStandbyForSpectator(authoritativeState, candidate))
            .OrderBy(candidate => candidate, StringComparer.Ordinal)
            .ToArray();
        var index = Array.IndexOf(hiddenIds, objectId);
        return index < 0 ? 1 : index + 1;
    }

    private static IReadOnlyList<string> ExpectedStateBasedTaskKinds(IReadOnlyList<CleanupTaskState> tasks)
    {
        return ExpectedStateBasedTaskKinds(tasks.Select(task => task.Kind));
    }

    private static IReadOnlyList<string> ExpectedStateBasedTaskKinds(IEnumerable<string> taskKinds)
    {
        return taskKinds
            .Where(IsSnapshotStateBasedCleanupTaskForRecovery)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSnapshotStateBasedCleanupTaskForRecovery(string kind)
    {
        return string.Equals(kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "DESTROY_ZERO_POWER_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            || string.Equals(kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal);
    }

    private static void ValidateObservedDisclosureSnapshot(IReadOnlyDictionary<string, object?> timing,
        MatchState state, string label, List<string> errors)
    {
        var hasPayment = TryReadObjectValue(timing, "pendingPayment", out var payment) && IsSnapshotPlayerPayloadObject(payment);
        var hasKind = hasPayment && TryReadObjectValue(payment, "deckObservationKind", out _);
        if (state.PendingRuleChoice is not { Request.Kind: "DECK_OBSERVATION_BANISH" } pending)
        {
            if (hasKind) errors.Add($"{label} deck observation disclosure kind is not allowed outside its choice");
            return;
        }
        // Derive independently from the authoritative observation preview, not the snapshot producer.
        var observation = pending.Request.Preview?.DeckObservationView;
        var expected = observation?.Public == true ? "REVEAL" : "LOOK";
        if (observation is null || !hasKind || !TryReadObjectValue(payment, "deckObservationKind", out var value)
            || JsonSerializer.SerializeToElement(value) is not { ValueKind: JsonValueKind.String } text
            || text.GetString() != expected)
            errors.Add($"{label} deck observation disclosure kind does not match the authoritative preview");
    }

    private static void ValidateSpectatorPendingPaymentPayload(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativePayment = CoreRuleEngine.VisiblePendingPayment(authoritativeState);
        if (!timing.TryGetValue("pendingPayment", out var paymentPayload))
        {
            errors.Add("spectator replay frame timing pending payment is required");
            AddMissingSpectatorPendingPaymentDiagnostics(authoritativeState, authoritativePayment, errors);
            return;
        }

        if (authoritativePayment is null)
        {
            if (!IsNullSnapshotPayloadValue(paymentPayload))
            {
                errors.Add("spectator replay frame timing pending payment must be empty when authoritative state has no pending payment");
            }

            return;
        }

        if (IsNullSnapshotPayloadValue(paymentPayload))
        {
            errors.Add("spectator replay frame timing pending payment is required");
            AddMissingSpectatorPendingPaymentDiagnostics(authoritativeState, authoritativePayment, errors);
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(paymentPayload))
        {
            errors.Add("spectator replay frame timing pending payment payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            paymentPayload,
            "spectator replay frame timing pending payment",
            errors);
        ValidateSpectatorPendingPaymentPayloadValues(paymentPayload, errors);

        ValidateSpectatorPendingPaymentRequiredStringAuthoritativeValue(
            paymentPayload,
            "paymentId",
            "id",
            authoritativePayment.PaymentId,
            errors);
        ValidateSpectatorPendingPaymentRequiredStringAuthoritativeValue(
            paymentPayload,
            "paymentWindow",
            "window",
            authoritativePayment.PaymentWindow,
            errors);
        ValidateSpectatorPendingPaymentRequiredStringAuthoritativeValue(
            paymentPayload,
            "playerId",
            "player",
            authoritativePayment.PlayerId,
            errors);

        if (!TryReadObjectValue(paymentPayload, "cost", out var costPayload)
            || IsNullSnapshotPayloadValue(costPayload))
        {
            errors.Add("spectator replay frame timing pending payment cost is required");
        }
        else if (!IsSnapshotPlayerPayloadObject(costPayload))
        {
            errors.Add("spectator replay frame timing pending payment cost payload is required");
        }
        else
        {
            ValidateSnapshotPayloadObjectPropertyNames(
                costPayload,
                "spectator replay frame timing pending payment cost",
                errors);
            ValidateSpectatorPendingPaymentPowerTraitPayloadPropertyNames(costPayload, errors);

            ValidateSpectatorPendingPaymentRequiredIntAuthoritativeValue(
                costPayload,
                "mana",
                "mana cost",
                authoritativePayment.ManaCost,
                errors);
            ValidateSpectatorPendingPaymentRequiredIntAuthoritativeValue(
                costPayload,
                "power",
                "power cost",
                authoritativePayment.PowerCost,
                errors);
            ValidateSpectatorPendingPaymentRequiredIntDictionaryAuthoritativeValue(
                costPayload,
                "powerByTrait",
                "power cost traits",
                authoritativePayment.PowerCostByTrait,
                errors);
        }

        ValidateSpectatorPendingPaymentRequiredStringListAuthoritativeValue(
            paymentPayload,
            "paymentChoices",
            "choices",
            authoritativePayment.LegalPaymentChoiceIds,
            errors);

        ValidateSpectatorPendingPaymentRequiredStringListAuthoritativeValue(
            paymentPayload,
            "paymentResourceActions",
            "resource actions",
            ExpectedSpectatorPendingPaymentResourceActionIds(authoritativeState, authoritativePayment),
            errors);
    }

    private static void AddMissingSpectatorPendingPaymentDiagnostics(
        MatchState authoritativeState,
        PendingPaymentState? authoritativePayment,
        List<string> errors)
    {
        if (authoritativePayment is null)
        {
            return;
        }

        if (authoritativePayment.ManaCost > 0)
        {
            errors.Add(
                $"spectator replay frame timing pending payment mana cost 0 does not match authoritative state pending payment mana cost {authoritativePayment.ManaCost}");
        }

        if (authoritativePayment.PowerCost > 0)
        {
            errors.Add(
                $"spectator replay frame timing pending payment power cost 0 does not match authoritative state pending payment power cost {authoritativePayment.PowerCost}");
        }

        if (authoritativePayment.PowerCostByTrait.Count > 0)
        {
            errors.Add(
                $"spectator replay frame timing pending payment power cost trait count 0 does not match authoritative state pending payment power cost trait count {authoritativePayment.PowerCostByTrait.Count}");
        }

        if (authoritativePayment.LegalPaymentChoiceIds.Count > 0)
        {
            errors.Add(
                $"spectator replay frame timing pending payment choice count 0 does not match authoritative state pending payment choice count {authoritativePayment.LegalPaymentChoiceIds.Count}");
        }

        var expectedResourceActionCount =
            ExpectedSpectatorPendingPaymentResourceActionIds(authoritativeState, authoritativePayment).Count;
        if (expectedResourceActionCount > 0)
        {
            errors.Add(
                $"spectator replay frame timing pending payment resource action count 0 does not match authoritative state pending payment resource action count {expectedResourceActionCount}");
        }
    }

    private static void ValidateSpectatorPendingPaymentPayloadValues(
        object? paymentPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending payment";
        ValidateSnapshotPayloadRequiredStringValue(
            paymentPayload,
            "paymentId",
            payloadLabel,
            "payment id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            paymentPayload,
            "paymentWindow",
            payloadLabel,
            "payment window",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            paymentPayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            paymentPayload,
            payloadLabel,
            "paymentChoices",
            "payment choice",
            errors);
        ValidateSnapshotPayloadStringListValues(
            paymentPayload,
            "paymentChoices",
            payloadLabel,
            "payment choice",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            paymentPayload,
            payloadLabel,
            "paymentResourceActions",
            "payment resource action",
            errors);
        ValidateSnapshotPayloadStringListValues(
            paymentPayload,
            "paymentResourceActions",
            payloadLabel,
            "payment resource action",
            errors);

        if (!TryReadObjectValue(paymentPayload, "cost", out var costPayload)
            || !IsSnapshotPlayerPayloadObject(costPayload))
        {
            return;
        }

        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            costPayload,
            "mana",
            $"{payloadLabel} cost",
            "mana",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            costPayload,
            "power",
            $"{payloadLabel} cost",
            "power",
            errors);
        ValidateSpectatorRequiredIntMapPayloadShape(
            costPayload,
            $"{payloadLabel} cost",
            "powerByTrait",
            "power cost trait",
            errors);
        ValidateSnapshotPayloadRequiredPositiveIntMapValues(
            costPayload,
            "powerByTrait",
            $"{payloadLabel} cost",
            "power cost trait",
            errors);
    }

    private static void ValidateSpectatorPendingPaymentRequiredStringAuthoritativeValue(
        object? paymentPayload,
        string key,
        string fieldLabel,
        string expected,
        List<string> errors)
    {
        var readable = TryReadObjectString(paymentPayload, key, out var actual);
        if (readable
            && string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        AddSpectatorPendingPaymentAuthoritativeMismatch(
            fieldLabel,
            "does",
            expected,
            readable ? actual : FormatUnreadableObjectValue(paymentPayload, key),
            errors);
    }

    private static void ValidateSpectatorPendingPaymentRequiredIntAuthoritativeValue(
        object? costPayload,
        string key,
        string fieldLabel,
        int expected,
        List<string> errors)
    {
        var readable = TryReadObjectInt(costPayload, key, out var actual);
        if (readable && actual == expected)
        {
            return;
        }

        AddSpectatorPendingPaymentAuthoritativeMismatch(
            fieldLabel,
            "does",
            expected,
            readable ? actual : FormatUnreadableObjectValue(costPayload, key),
            errors);
    }

    private static void ValidateSpectatorPendingPaymentRequiredIntDictionaryAuthoritativeValue(
        object? costPayload,
        string key,
        string fieldLabel,
        IReadOnlyDictionary<string, int> expected,
        List<string> errors)
    {
        var readable = TryReadObjectIntDictionary(costPayload, key, out var actual);
        if (readable && IntDictionariesEqual(actual, expected))
        {
            return;
        }

        AddSpectatorPendingPaymentAuthoritativeMismatch(
            fieldLabel,
            "do",
            expected,
            readable ? actual : FormatUnreadableObjectValue(costPayload, key),
            errors);
    }

    private static void ValidateSpectatorPendingPaymentRequiredStringListAuthoritativeValue(
        object? paymentPayload,
        string key,
        string fieldLabel,
        IReadOnlyList<string> expected,
        List<string> errors)
    {
        var readable = TryReadObjectStringList(paymentPayload, key, out var actual);
        if (readable && StringListsEqual(actual, expected))
        {
            return;
        }

        AddSpectatorPendingPaymentAuthoritativeMismatch(
            fieldLabel,
            "do",
            expected,
            readable ? actual : FormatUnreadableObjectValue(paymentPayload, key),
            errors);
    }

    private static void AddSpectatorPendingPaymentAuthoritativeMismatch(
        string fieldLabel,
        string matchVerb,
        object? expected,
        object? actual,
        List<string> errors)
    {
        errors.Add(
            $"spectator replay frame timing pending payment {fieldLabel} {matchVerb} not match authoritative state pending payment {fieldLabel}; {FormatExpectedActualForRecovery(expected, actual)}");
    }

    private static IReadOnlyList<string> ExpectedSpectatorPendingPaymentResourceActionIds(
        MatchState authoritativeState,
        PendingPaymentState payment)
    {
        return payment.PaymentResourceActionIds
            .Concat(payment.LegalPaymentChoiceIds.Where(choiceId =>
                choiceId.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal)))
            .Concat(ExpectedBlueSentinelDelayedResourceActionIdsForRecovery(authoritativeState, payment))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ExpectedBlueSentinelDelayedResourceActionIdsForRecovery(
        MatchState authoritativeState,
        PendingPaymentState payment)
    {
        if (payment.PowerCost <= 0 && payment.PowerCostByTrait.Count == 0)
        {
            return [];
        }

        var runePool = authoritativeState.RunePools.TryGetValue(payment.PlayerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        if (PaymentCostRules.CanPayPowerCost(runePool, payment.PowerCost, payment.PowerCostByTrait))
        {
            return [];
        }

        return authoritativeState.TriggerQueue
            .Where(trigger => BlueSentinelDelayedTriggerCanPayForRecovery(authoritativeState, payment, trigger))
            .Select(trigger => $"{P4ActivatedAbilityCatalog.BlueSentinelDelayedResourceActionPrefix}{trigger.TriggerId}")
            .ToArray();
    }

    private static bool BlueSentinelDelayedTriggerCanPayForRecovery(
        MatchState authoritativeState,
        PendingPaymentState payment,
        TriggerQueueItemState trigger)
    {
        if (!TryReadBlueSentinelDelayedTriggerContextForRecovery(
                trigger.TriggerId,
                out var capturedTurnNumber,
                out var sourceObjectId,
                out var battlefieldObjectId)
            || !string.Equals(trigger.ControllerId, payment.PlayerId, StringComparison.Ordinal)
            || !string.Equals(trigger.SourceObjectId, sourceObjectId, StringComparison.Ordinal)
            || !string.Equals(trigger.EffectKind, P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityEffectKind, StringComparison.Ordinal)
            || !string.Equals(trigger.TriggeredByEventKind, "BATTLEFIELD_HELD", StringComparison.Ordinal)
            || authoritativeState.TurnNumber != capturedTurnNumber + 1
            || !string.Equals(authoritativeState.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(authoritativeState.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(authoritativeState.ActivePlayerId, payment.PlayerId, StringComparison.Ordinal)
            || !BlueSentinelDelayedSourceStillHoldsBattlefieldForRecovery(
                authoritativeState,
                payment.PlayerId,
                sourceObjectId,
                battlefieldObjectId))
        {
            return false;
        }

        var runePool = authoritativeState.RunePools.TryGetValue(payment.PlayerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        return !PaymentCostRules.CanPayPowerCost(runePool, payment.PowerCost, payment.PowerCostByTrait)
            && PaymentCostRules.CanPayPowerCost(runePool with { Power = runePool.Power + P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower },
                payment.PowerCost, payment.PowerCostByTrait);
    }

    private static bool TryReadBlueSentinelDelayedTriggerContextForRecovery(
        string triggerId,
        out int capturedTurnNumber,
        out string sourceObjectId,
        out string battlefieldObjectId)
    {
        capturedTurnNumber = 0;
        sourceObjectId = string.Empty;
        battlefieldObjectId = string.Empty;
        var parts = triggerId.Split("::", StringSplitOptions.None);
        if (parts.Length != 4
            || !string.Equals(parts[0], BlueSentinelDelayedTriggerIdPrefixForRecovery, StringComparison.Ordinal)
            || !int.TryParse(
                parts[1],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out capturedTurnNumber)
            || string.IsNullOrWhiteSpace(parts[2])
            || string.IsNullOrWhiteSpace(parts[3]))
        {
            return false;
        }

        sourceObjectId = parts[2];
        battlefieldObjectId = parts[3];
        return capturedTurnNumber > 0;
    }

    private static bool BlueSentinelDelayedSourceStillHoldsBattlefieldForRecovery(
        MatchState authoritativeState,
        string playerId,
        string sourceObjectId,
        string battlefieldObjectId)
    {
        return authoritativeState.CardObjects.TryGetValue(sourceObjectId, out var sourceState)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(
                P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId,
                sourceState.CardNo)
            && sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !sourceState.IsFaceDown
            && SourceObjectControlledByPlayerOrLegacyOwnedForRecovery(sourceState, playerId)
            && authoritativeState.ObjectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            && string.Equals(sourceLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(sourceLocation.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
            && authoritativeState.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal)
            && (!authoritativeState.CardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState)
                || SourceObjectControlledByPlayerOrLegacyOwnedForRecovery(battlefieldState, playerId));
    }

    private static void ValidateSpectatorContinuousEffectPayloads(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!timing.TryGetValue("continuousEffects", out var continuousEffectsPayload)
            || IsNullSnapshotPayloadValue(continuousEffectsPayload))
        {
            errors.Add("spectator replay frame timing continuous effects are required");
            var authoritativeEffectCount = authoritativeState.ContinuousEffects.Count;
            if (authoritativeEffectCount > 0)
            {
                AddSpectatorContinuousEffectCountMismatch(
                    spectatorCount: 0,
                    authoritativeCount: authoritativeEffectCount,
                    errors);
            }

            ReportMissingSpectatorContinuousEffectAuthoritativeKeySet(
                authoritativeState.ContinuousEffects,
                errors);
            return;
        }

        if (!TryReadObjectListValue(continuousEffectsPayload, out var spectatorEffects))
        {
            errors.Add("spectator replay frame timing continuous effects payload is required");
            var authoritativeEffectCount = authoritativeState.ContinuousEffects.Count;
            if (authoritativeEffectCount > 0)
            {
                AddSpectatorContinuousEffectCountMismatch(
                    spectatorCount: 0,
                    authoritativeCount: authoritativeEffectCount,
                    errors);
            }

            ReportMissingSpectatorContinuousEffectAuthoritativeKeySet(
                authoritativeState.ContinuousEffects,
                errors);
            return;
        }

        IReadOnlyList<ContinuousEffectState> authoritativeEffects = authoritativeState.ContinuousEffects.Select(effect => CoreRuleEngine.PublicPlayInstructionEffect(authoritativeState, effect, "__spectator__")).ToArray();
        var validateAuthoritativeParity = spectatorEffects.Count == authoritativeEffects.Count;
        if (!validateAuthoritativeParity)
        {
            AddSpectatorContinuousEffectCountMismatch(
                spectatorEffects.Count,
                authoritativeEffects.Count,
                errors);
        }

        var seenEffectIds = new HashSet<string>(StringComparer.Ordinal);
        var seenSequences = new HashSet<int>();
        var validSequences = new List<int>();
        var knownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState).ToHashSet(StringComparer.Ordinal);
        if (authoritativeEffects.Any(effect => effect.SourceObjectId == "HIDDEN"))
            knownObjectIds.Add("HIDDEN");
        foreach (var spectatorEffect in spectatorEffects)
        {
            if (!IsSnapshotPlayerPayloadObject(spectatorEffect))
            {
                errors.Add("spectator replay frame timing continuous effect payload is required");
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorEffect,
                "spectator replay frame timing continuous effect item",
                errors);
            var effectValues = ValidateSpectatorContinuousEffectPayloadValues(
                spectatorEffect,
                knownObjectIds,
                errors);
            if (effectValues.EffectId is not null && !seenEffectIds.Add(effectValues.EffectId))
            {
                errors.Add(
                    $"spectator replay frame timing continuous effect item effect id {effectValues.EffectId} is duplicated");
            }

            ValidateContinuousEffectSequenceValue(
                "spectator replay frame timing continuous effect item",
                effectValues.Sequence,
                seenSequences,
                validSequences,
                errors);
        }

        ValidateContinuousEffectSequenceContinuity(
            "spectator replay frame timing continuous effect item",
            validSequences,
            errors);

        var authoritativeEffectsById = BuildAuthoritativeContinuousEffectsById(authoritativeEffects);
        ValidateSpectatorContinuousEffectAuthoritativeKeySet(
            spectatorEffects,
            authoritativeEffectsById,
            errors);
        ValidateSpectatorContinuousEffectKeyedAuthoritativeValues(
            spectatorEffects,
            authoritativeEffectsById,
            errors);

        if (!validateAuthoritativeParity)
        {
            return;
        }

        var effectIdsMatch = true;
        var scopesMatch = true;
        var layersMatch = true;
        var durationsMatch = true;
        var targetObjectIdsMatch = true;
        var sourceObjectIdsMatch = true;
        var powerDeltasMatch = true;
        var basePowersMatch = true;
        var effectivePowersMatch = true;
        var sequencesMatch = true;
        var effectKindsMatch = true;
        var sourceCardNumbersMatch = true;
        var sourcePathsMatch = true;
        var layerEngineStatusesMatch = true;
        var requestedPowerDeltasMatch = true;
        var appliedPowerDeltasMatch = true;
        var minimumPowersMatch = true;
        var resultingPowersMatch = true;
        var snapshotMightsMatch = true;
        var appliedOrdersMatch = true;
        var sourceOrdersMatch = true;
        var conditionsMatch = true;
        var lifecyclesMatch = true;
        var participantObjectIdsMatch = true;
        var sourceDependencyObjectIdsMatch = true;
        var targetDependencyObjectIdsMatch = true;
        var participantDependencyObjectIdsMatch = true;
        var deferredLayerEngineResidualsMatch = true;

        for (var index = 0; index < authoritativeEffects.Count; index++)
        {
            var spectatorEffect = spectatorEffects[index];
            if (!IsSnapshotPlayerPayloadObject(spectatorEffect))
            {
                continue;
            }

            var authoritativeEffect = authoritativeEffects[index];
            if (!TryReadObjectString(spectatorEffect, "effectId", out var effectId)
                || !string.Equals(effectId, authoritativeEffect.EffectId, StringComparison.Ordinal))
            {
                effectIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorEffect, "scope", out var scope)
                || !string.Equals(scope, authoritativeEffect.Scope, StringComparison.Ordinal))
            {
                scopesMatch = false;
            }

            if (!TryReadObjectString(spectatorEffect, "layer", out var layer)
                || !string.Equals(layer, authoritativeEffect.Layer, StringComparison.Ordinal))
            {
                layersMatch = false;
            }

            if (!TryReadObjectString(spectatorEffect, "duration", out var duration)
                || !string.Equals(duration, authoritativeEffect.Duration, StringComparison.Ordinal))
            {
                durationsMatch = false;
            }

            if (!TryReadObjectValue(spectatorEffect, "targetObjectId", out var targetObjectIdPayload)
                || !TryReadOptionalStringValue(targetObjectIdPayload, out var targetObjectId)
                || !string.Equals(
                    targetObjectId,
                    authoritativeEffect.TargetObjectId ?? string.Empty,
                    StringComparison.Ordinal))
            {
                targetObjectIdsMatch = false;
            }

            if (!TryReadObjectValue(spectatorEffect, "sourceObjectId", out var sourceObjectIdPayload)
                || !TryReadOptionalStringValue(sourceObjectIdPayload, out var sourceObjectId)
                || !string.Equals(
                    sourceObjectId,
                    authoritativeEffect.SourceObjectId ?? string.Empty,
                    StringComparison.Ordinal))
            {
                sourceObjectIdsMatch = false;
            }

            if (!TryReadObjectInt(spectatorEffect, "powerDelta", out var powerDelta)
                || powerDelta != authoritativeEffect.PowerDelta)
            {
                powerDeltasMatch = false;
            }

            if (!TryReadObjectInt(spectatorEffect, "basePower", out var basePower)
                || basePower != authoritativeEffect.BasePower)
            {
                basePowersMatch = false;
            }

            if (!TryReadObjectInt(spectatorEffect, "effectivePower", out var effectivePower)
                || effectivePower != authoritativeEffect.EffectivePower)
            {
                effectivePowersMatch = false;
            }

            if (!TryReadObjectInt(spectatorEffect, "sequence", out var sequence)
                || sequence != authoritativeEffect.Sequence)
            {
                sequencesMatch = false;
            }

            if (!OptionalStringSnapshotValueMatches(spectatorEffect, "effectKind", authoritativeEffect.EffectKind))
            {
                effectKindsMatch = false;
            }

            if (!OptionalStringSnapshotValueMatches(spectatorEffect, "sourceCardNo", authoritativeEffect.SourceCardNo))
            {
                sourceCardNumbersMatch = false;
            }

            if (!OptionalStringSnapshotValueMatches(spectatorEffect, "sourcePath", authoritativeEffect.SourcePath))
            {
                sourcePathsMatch = false;
            }

            if (!OptionalStringSnapshotValueMatches(
                    spectatorEffect,
                    "layerEngineStatus",
                    authoritativeEffect.IsLayerEngineFoundationOnly ? "FOUNDATION_ONLY" : null))
            {
                layerEngineStatusesMatch = false;
            }

            if (!OptionalIntSnapshotValueMatches(
                    spectatorEffect,
                    "requestedPowerDelta",
                    authoritativeEffect.RequestedPowerDelta))
            {
                requestedPowerDeltasMatch = false;
            }

            if (!OptionalIntSnapshotValueMatches(
                    spectatorEffect,
                    "appliedPowerDelta",
                    authoritativeEffect.AppliedPowerDelta))
            {
                appliedPowerDeltasMatch = false;
            }

            if (!OptionalIntSnapshotValueMatches(spectatorEffect, "minimumPower", authoritativeEffect.MinimumPower))
            {
                minimumPowersMatch = false;
            }

            if (!OptionalIntSnapshotValueMatches(spectatorEffect, "resultingPower", authoritativeEffect.ResultingPower))
            {
                resultingPowersMatch = false;
            }

            if (!OptionalIntSnapshotValueMatches(spectatorEffect, "snapshotMight", authoritativeEffect.SnapshotMight))
            {
                snapshotMightsMatch = false;
            }

            if (!OptionalIntSnapshotValueMatches(spectatorEffect, "appliedOrder", authoritativeEffect.AppliedOrder))
            {
                appliedOrdersMatch = false;
            }

            if (!OptionalIntSnapshotValueMatches(spectatorEffect, "sourceOrder", authoritativeEffect.SourceOrder))
            {
                sourceOrdersMatch = false;
            }

            if (!OptionalStringSnapshotValueMatches(spectatorEffect, "condition", authoritativeEffect.Condition))
            {
                conditionsMatch = false;
            }

            if (!OptionalStringSnapshotValueMatches(spectatorEffect, "lifecycle", authoritativeEffect.Lifecycle))
            {
                lifecyclesMatch = false;
            }

            if (!OptionalStringListSnapshotValueMatches(
                    spectatorEffect,
                    "participantObjectIds",
                    authoritativeEffect.ParticipantObjectIds))
            {
                participantObjectIdsMatch = false;
            }

            if (!OptionalStringListSnapshotValueMatches(
                    spectatorEffect,
                    "sourceDependencyObjectIds",
                    authoritativeEffect.SourceDependencyObjectIds))
            {
                sourceDependencyObjectIdsMatch = false;
            }

            if (!OptionalStringListSnapshotValueMatches(
                    spectatorEffect,
                    "targetDependencyObjectIds",
                    authoritativeEffect.TargetDependencyObjectIds))
            {
                targetDependencyObjectIdsMatch = false;
            }

            if (!OptionalStringListSnapshotValueMatches(
                    spectatorEffect,
                    "participantDependencyObjectIds",
                    authoritativeEffect.ParticipantDependencyObjectIds))
            {
                participantDependencyObjectIdsMatch = false;
            }

            if (!OptionalStringListSnapshotValueMatches(
                    spectatorEffect,
                    "deferredLayerEngineResiduals",
                    authoritativeEffect.DeferredLayerEngineResiduals))
            {
                deferredLayerEngineResidualsMatch = false;
            }
        }

        if (!effectIdsMatch)
        {
            AddSpectatorContinuousEffectStringAggregateMismatch(
                "ids",
                authoritativeEffects.Select(effect => effect.EffectId),
                spectatorEffects,
                "effectId",
                errors);
        }

        if (!scopesMatch)
        {
            AddSpectatorContinuousEffectStringAggregateMismatch(
                "scopes",
                authoritativeEffects.Select(effect => effect.Scope),
                spectatorEffects,
                "scope",
                errors);
        }

        if (!layersMatch)
        {
            AddSpectatorContinuousEffectStringAggregateMismatch(
                "layers",
                authoritativeEffects.Select(effect => effect.Layer),
                spectatorEffects,
                "layer",
                errors);
        }

        if (!durationsMatch)
        {
            AddSpectatorContinuousEffectStringAggregateMismatch(
                "durations",
                authoritativeEffects.Select(effect => effect.Duration),
                spectatorEffects,
                "duration",
                errors);
        }

        if (!targetObjectIdsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "target objects",
                authoritativeEffects.Select(effect => effect.TargetObjectId),
                spectatorEffects,
                "targetObjectId",
                errors);
        }

        if (!sourceObjectIdsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "source objects",
                authoritativeEffects.Select(effect => effect.SourceObjectId),
                spectatorEffects,
                "sourceObjectId",
                errors);
        }

        if (!powerDeltasMatch)
        {
            AddSpectatorContinuousEffectIntAggregateMismatch(
                "power deltas",
                authoritativeEffects.Select(effect => effect.PowerDelta),
                spectatorEffects,
                "powerDelta",
                errors);
        }

        if (!basePowersMatch)
        {
            AddSpectatorContinuousEffectIntAggregateMismatch(
                "base powers",
                authoritativeEffects.Select(effect => effect.BasePower),
                spectatorEffects,
                "basePower",
                errors);
        }

        if (!effectivePowersMatch)
        {
            AddSpectatorContinuousEffectIntAggregateMismatch(
                "effective powers",
                authoritativeEffects.Select(effect => effect.EffectivePower),
                spectatorEffects,
                "effectivePower",
                errors);
        }

        if (!sequencesMatch)
        {
            AddSpectatorContinuousEffectIntAggregateMismatch(
                "sequences",
                authoritativeEffects.Select(effect => effect.Sequence),
                spectatorEffects,
                "sequence",
                errors);
        }

        if (!effectKindsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "kinds",
                authoritativeEffects.Select(effect => effect.EffectKind),
                spectatorEffects,
                "effectKind",
                errors);
        }

        if (!sourceCardNumbersMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "source card numbers",
                authoritativeEffects.Select(effect => effect.SourceCardNo),
                spectatorEffects,
                "sourceCardNo",
                errors);
        }

        if (!sourcePathsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "source paths",
                authoritativeEffects.Select(effect => effect.SourcePath),
                spectatorEffects,
                "sourcePath",
                errors);
        }

        if (!layerEngineStatusesMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "layer engine statuses",
                authoritativeEffects.Select(effect => effect.IsLayerEngineFoundationOnly ? "FOUNDATION_ONLY" : null),
                spectatorEffects,
                "layerEngineStatus",
                errors);
        }

        if (!requestedPowerDeltasMatch)
        {
            AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
                "requested power deltas",
                authoritativeEffects.Select(effect => effect.RequestedPowerDelta),
                spectatorEffects,
                "requestedPowerDelta",
                errors);
        }

        if (!appliedPowerDeltasMatch)
        {
            AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
                "applied power deltas",
                authoritativeEffects.Select(effect => effect.AppliedPowerDelta),
                spectatorEffects,
                "appliedPowerDelta",
                errors);
        }

        if (!minimumPowersMatch)
        {
            AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
                "minimum powers",
                authoritativeEffects.Select(effect => effect.MinimumPower),
                spectatorEffects,
                "minimumPower",
                errors);
        }

        if (!resultingPowersMatch)
        {
            AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
                "resulting powers",
                authoritativeEffects.Select(effect => effect.ResultingPower),
                spectatorEffects,
                "resultingPower",
                errors);
        }

        if (!snapshotMightsMatch)
        {
            AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
                "snapshot Mights",
                authoritativeEffects.Select(effect => effect.SnapshotMight),
                spectatorEffects,
                "snapshotMight",
                errors);
        }

        if (!appliedOrdersMatch)
        {
            AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
                "applied orders",
                authoritativeEffects.Select(effect => effect.AppliedOrder),
                spectatorEffects,
                "appliedOrder",
                errors);
        }

        if (!sourceOrdersMatch)
        {
            AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
                "source orders",
                authoritativeEffects.Select(effect => effect.SourceOrder),
                spectatorEffects,
                "sourceOrder",
                errors);
        }

        if (!conditionsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "conditions",
                authoritativeEffects.Select(effect => effect.Condition),
                spectatorEffects,
                "condition",
                errors);
        }

        if (!lifecyclesMatch)
        {
            AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
                "lifecycles",
                authoritativeEffects.Select(effect => effect.Lifecycle),
                spectatorEffects,
                "lifecycle",
                errors);
        }

        if (!participantObjectIdsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringListAggregateMismatch(
                "participant object ids",
                authoritativeEffects.Select(effect => effect.ParticipantObjectIds),
                spectatorEffects,
                "participantObjectIds",
                errors);
        }

        if (!sourceDependencyObjectIdsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringListAggregateMismatch(
                "source dependency object ids",
                authoritativeEffects.Select(effect => effect.SourceDependencyObjectIds),
                spectatorEffects,
                "sourceDependencyObjectIds",
                errors);
        }

        if (!targetDependencyObjectIdsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringListAggregateMismatch(
                "target dependency object ids",
                authoritativeEffects.Select(effect => effect.TargetDependencyObjectIds),
                spectatorEffects,
                "targetDependencyObjectIds",
                errors);
        }

        if (!participantDependencyObjectIdsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringListAggregateMismatch(
                "participant dependency object ids",
                authoritativeEffects.Select(effect => effect.ParticipantDependencyObjectIds),
                spectatorEffects,
                "participantDependencyObjectIds",
                errors);
        }

        if (!deferredLayerEngineResidualsMatch)
        {
            AddSpectatorContinuousEffectOptionalStringListAggregateMismatch(
                "deferred LayerEngine residuals",
                authoritativeEffects.Select(effect => effect.DeferredLayerEngineResiduals),
                spectatorEffects,
                "deferredLayerEngineResiduals",
                errors);
        }
    }

    private static void AddSpectatorContinuousEffectStringAggregateMismatch(
        string fieldLabel,
        IEnumerable<string?> expectedValues,
        IReadOnlyList<object?> spectatorEffects,
        string key,
        List<string> errors)
    {
        AddSpectatorContinuousEffectAggregateMismatch(
            fieldLabel,
            expectedValues.Select(value => FormatRecoveryDiagnosticValue(value ?? string.Empty)).ToArray(),
            ExtractObjectStringDiagnosticValues(spectatorEffects, key),
            errors);
    }

    private static void AddSpectatorContinuousEffectOptionalStringAggregateMismatch(
        string fieldLabel,
        IEnumerable<string?> expectedValues,
        IReadOnlyList<object?> spectatorEffects,
        string key,
        List<string> errors)
    {
        AddSpectatorContinuousEffectAggregateMismatch(
            fieldLabel,
            expectedValues.Select(value => FormatRecoveryDiagnosticValue(value ?? string.Empty)).ToArray(),
            ExtractObjectOptionalStringDiagnosticValues(spectatorEffects, key),
            errors);
    }

    private static void AddSpectatorContinuousEffectIntAggregateMismatch(
        string fieldLabel,
        IEnumerable<int> expectedValues,
        IReadOnlyList<object?> spectatorEffects,
        string key,
        List<string> errors)
    {
        AddSpectatorContinuousEffectAggregateMismatch(
            fieldLabel,
            expectedValues.Select(value => FormatRecoveryDiagnosticValue(value)).ToArray(),
            ExtractObjectIntDiagnosticValues(spectatorEffects, key),
            errors);
    }

    private static void AddSpectatorContinuousEffectOptionalIntAggregateMismatch(
        string fieldLabel,
        IEnumerable<int?> expectedValues,
        IReadOnlyList<object?> spectatorEffects,
        string key,
        List<string> errors)
    {
        AddSpectatorContinuousEffectAggregateMismatch(
            fieldLabel,
            expectedValues
                .Select(value => FormatRecoveryDiagnosticValue(value.HasValue ? (object)value.Value : string.Empty))
                .ToArray(),
            ExtractObjectOptionalIntDiagnosticValues(spectatorEffects, key),
            errors);
    }
}
