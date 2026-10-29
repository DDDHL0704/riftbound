using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidatePlayerViews(
        long? currentTick,
        long lastEventSequence,
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        List<string> errors,
        MatchState? authoritativeState)
    {
        foreach (var (playerId, view) in playerViews)
        {
            if (!string.Equals(playerId, view.PlayerId, StringComparison.Ordinal))
            {
                errors.Add($"player view key {playerId} does not match payload player {view.PlayerId}");
            }

            if (view.Snapshot is null)
            {
                errors.Add($"snapshot for {view.PlayerId} is required");
                continue;
            }

            ValidateSnapshotShape(view, errors, authoritativeState);
            if (authoritativeState is not null)
                ValidateOrdinaryPrivatePlaySkillSnapshot(view, authoritativeState, errors);
            if (authoritativeState is not null && view.Snapshot.Timing is { } disclosureTiming)
                ValidateObservedDisclosureSnapshot(disclosureTiming, authoritativeState, $"snapshot for {view.PlayerId}", errors);

            if (view.Snapshot.Players is null)
            {
                errors.Add($"snapshot for {view.PlayerId} players are required");
            }
            else
            {
                ValidateSnapshotPlayerPayloads(view, errors);
                ValidateSnapshotPlayerCoverage(view, playerViews.Keys, errors);
                ValidateSnapshotActivePlayer(view, errors);
                ValidateSnapshotTimingPlayerMembership(view, "turnPlayerId", "turn player", errors);
                ValidateSnapshotTimingPlayerMembership(
                    view,
                    "priorityPlayerId",
                    "priority player",
                    errors,
                    optional: true);
                ValidateSnapshotTimingPlayerMembership(
                    view,
                    "focusPlayerId",
                    "focus player",
                    errors,
                    optional: true);
                ValidateSnapshotTimingPlayerMembership(
                    view,
                    "winnerPlayerId",
                    "winner player",
                    errors,
                    optional: true);
                ValidateSnapshotTimingPlayerListMembership(view, "readyPlayerIds", "ready player", errors);
                ValidateSnapshotTimingPlayerListMembership(
                    view,
                    "passedPriorityPlayerIds",
                    "passed priority player",
                    errors);
                ValidateSnapshotTimingPlayerListMembership(
                    view,
                    "passedFocusPlayerIds",
                    "passed focus player",
                    errors);
                ValidateSnapshotTimingPlayerListMembership(
                    view,
                    "destroyedUnitOwnerIdsThisTurn",
                    "destroyed unit owner",
                    errors);
            }

            if (view.Snapshot.Tick != view.SnapshotTick)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has payload tick {view.Snapshot.Tick} but row tick {view.SnapshotTick}");
            }

            if (view.Snapshot.TurnNumber < 1)
            {
                errors.Add($"snapshot for {view.PlayerId} has invalid turn number {view.Snapshot.TurnNumber}");
            }

            if (view.SnapshotTick < 0)
            {
                errors.Add($"snapshot for {view.PlayerId} has negative row tick {view.SnapshotTick}");
            }

            if (currentTick is { } recoveryTick && view.SnapshotTick > recoveryTick)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has row tick {view.SnapshotTick} after recovery tick {recoveryTick}");
            }

            if (currentTick is { } snapshotRecoveryTick && view.SnapshotTick != snapshotRecoveryTick)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has row tick {view.SnapshotTick} but recovery tick {snapshotRecoveryTick}");
            }

            if (view.SnapshotEventSequence < 0)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has negative event sequence {view.SnapshotEventSequence}");
            }

            if (view.SnapshotEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} points to future event sequence {view.SnapshotEventSequence}");
            }

            if (view.SnapshotEventSequence != lastEventSequence)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has event sequence {view.SnapshotEventSequence} but recovery event sequence {lastEventSequence}");
            }

            if (view.Prompt is null
                && (view.PromptTick is not null || view.PromptEventSequence is not null))
            {
                errors.Add(
                    $"prompt metadata for {view.PlayerId} has tick/event sequence without prompt payload");
            }

            if (view.Prompt is not null
                && (view.PromptTick is null || view.PromptEventSequence is null))
            {
                errors.Add(
                    $"prompt for {view.PlayerId} is missing row tick/event sequence metadata");
            }

            if (view.Prompt is not null && view.PromptEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} points to future event sequence {view.PromptEventSequence}");
            }

            if (view.PromptTick is { } negativePromptRowTick && negativePromptRowTick < 0)
            {
                errors.Add($"prompt for {view.PlayerId} has negative row tick {negativePromptRowTick}");
            }

            if (view.PromptEventSequence is { } negativePromptEventSequence && negativePromptEventSequence < 0)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has negative event sequence {negativePromptEventSequence}");
            }

            if (currentTick is { } currentRecoveryTick
                && view.PromptTick is { } futurePromptRowTick
                && futurePromptRowTick > currentRecoveryTick)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has row tick {futurePromptRowTick} after recovery tick {currentRecoveryTick}");
            }

            if (view.Prompt is not null
                && view.Prompt.SnapshotTick is { } promptSnapshotTick
                && view.PromptTick is { } promptRowTick
                && promptSnapshotTick != promptRowTick)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has payload snapshot tick {promptSnapshotTick} but row tick {promptRowTick}");
            }

            if (view.Prompt is not null
                && view.PromptTick is { } promptTick
                && promptTick != view.SnapshotTick)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has row tick {promptTick} but snapshot row tick {view.SnapshotTick}");
            }

            if (view.Prompt is not null
                && view.PromptEventSequence is { } promptEventSequence
                && promptEventSequence != view.SnapshotEventSequence)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has event sequence {promptEventSequence} but snapshot event sequence {view.SnapshotEventSequence}");
            }

            if (view.Prompt is not null
                && !string.Equals(view.PlayerId, view.Prompt.PlayerId, StringComparison.Ordinal))
            {
                errors.Add($"prompt for {view.PlayerId} has payload player {view.Prompt.PlayerId}");
            }
        }
    }

    private static void ValidateSnapshotShape(
        RecoveredPlayerView view,
        List<string> errors,
        MatchState? authoritativeState)
    {
        if (view.Snapshot.Lanes is null)
        {
            errors.Add($"snapshot for {view.PlayerId} lanes are required");
        }
        else
        {
            ValidateSnapshotPayloadObjectPropertyNames(
                view.Snapshot.Lanes,
                $"snapshot for {view.PlayerId} lanes",
                errors);
            ValidateSnapshotLaneListPayloadShapes(view, errors);
            ValidateSnapshotLaneListItemPayloadShapes(view, errors);
            ValidateSnapshotLaneListItemPayloadPropertyNames(view, errors);
            ValidateSnapshotLaneBattlefieldObjectIdItemPayloadValues(view, errors);
            ValidateSnapshotLaneBattlefieldItemScalarPayloadValues(view, errors);
            ValidateSnapshotLaneBattlefieldItemListPayloadValues(view, errors);
            ValidateSnapshotLaneBattlefieldStandbySlotListPayloadShapes(view, errors);
            ValidateSnapshotLaneBattlefieldStandbySlotItemPayloadShapes(view, errors);
            ValidateSnapshotLaneBattlefieldStandbySlotItemPayloadPropertyNames(view, errors);
            ValidateSnapshotLaneBattlefieldStandbySlotItemPayloadValues(view, errors);
        }

        if (view.Snapshot.Stack is null)
        {
            errors.Add($"snapshot for {view.PlayerId} stack is required");
        }
        else
        {
            ValidateSnapshotStackItemPayloadShapes(view, errors);
            ValidateSnapshotStackItemPayloadPropertyNames(view, errors);
            ValidateSnapshotStackItemPayloadValues(view, errors);
        }

        if (view.Snapshot.Timing is null)
        {
            errors.Add($"snapshot for {view.PlayerId} timing is required");
        }

        if (string.IsNullOrWhiteSpace(view.Snapshot.TurnState))
        {
            errors.Add($"snapshot for {view.PlayerId} turn state is required");
        }
        else if (!IsKnownTimingState(view.Snapshot.TurnState))
        {
            errors.Add($"snapshot for {view.PlayerId} turn state {view.Snapshot.TurnState} is invalid");
        }

        if (view.Snapshot.Timing is not null)
        {
            ValidateSnapshotPayloadObjectPropertyNames(
                view.Snapshot.Timing,
                $"snapshot for {view.PlayerId} timing",
                errors);
            ValidateTimingOrderTriggerPromptFieldAbsence(
                view.Snapshot.Timing,
                $"snapshot for {view.PlayerId} timing",
                errors);

            if (!TryReadString(view.Snapshot.Timing, "timingState", out var snapshotTimingState)
                || string.IsNullOrWhiteSpace(snapshotTimingState))
            {
                errors.Add($"snapshot for {view.PlayerId} timing state is required");
            }
            else
            {
                if (!IsKnownTimingState(snapshotTimingState))
                {
                    errors.Add($"snapshot for {view.PlayerId} timing state {snapshotTimingState} is invalid");
                }

                if (!string.IsNullOrWhiteSpace(view.Snapshot.TurnState)
                    && !string.Equals(view.Snapshot.TurnState, snapshotTimingState, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"snapshot for {view.PlayerId} turn state {view.Snapshot.TurnState} does not match timing state {snapshotTimingState}");
                }
            }

            if (view.Snapshot.Timing?.ContainsKey("temporaryPaymentResources") == true)
                errors.Add($"snapshot for {view.PlayerId} contains obsolete temporary payment resources");

            ValidateSnapshotTimingAllowedString(view, "phase", "phase", IsKnownMatchPhase, errors);
            ValidateSnapshotTimingRequiredString(view, "turnPlayerId", "turn player", errors);
            ValidateSnapshotTimingAllowedString(view, "roomStatus", "room status", IsKnownMatchStatus, errors);
            ValidateSnapshotTimingTopLevelPlayerListPayloadShapes(view, errors);
            ValidateSnapshotTimingObjectPayloadShape(
                view,
                "turnWindow",
                "turn window",
                errors);
            ValidateSnapshotTimingObjectPayloadPropertyNames(
                view,
                "turnWindow",
                "turn window",
                errors);
            ValidateSnapshotTimingTurnWindowPayloadValues(view, errors);
            ValidateSnapshotTimingObjectPayloadShape(
                view,
                "spellDuel",
                "spell duel",
                errors);
            ValidateSnapshotTimingObjectPayloadPropertyNames(
                view,
                "spellDuel",
                "spell duel",
                errors);
            ValidateSnapshotTimingSpellDuelPayloadValues(view, errors);
            ValidateSnapshotTimingObjectPayloadShape(
                view,
                "battle",
                "battle",
                errors);
            ValidateSnapshotTimingObjectPayloadPropertyNames(
                view,
                "battle",
                "battle",
                errors);
            ValidateSnapshotTimingBattlePayloadValues(view, errors, authoritativeState);
            ValidateSnapshotTimingBattleParticipantControllerPayloadPropertyNames(view, errors);
            ValidateSnapshotTimingBattleDamageAssignmentPayloadPropertyNames(view, errors);
            ValidateSnapshotTimingBattleDamageAssignmentMapPayloadPropertyNames(view, errors);
            ValidateSnapshotTimingBattleDamageAssignmentRequiredAssignmentPayloadPropertyNames(view, errors);
            ValidateSnapshotTimingBattleDamageAssignmentPayloadValues(view, errors);
            ValidateSnapshotTimingPendingPaymentPayloadShape(view, errors);
            ValidateSnapshotTimingPaymentPayloadPropertyNames(view, errors);
            ValidateSnapshotTimingPaymentPowerTraitPayloadPropertyNames(view, errors);
            ValidateSnapshotTimingPendingPaymentScalarPayloadValues(view, errors);
            ValidateSnapshotTimingPendingPaymentListPayloadValues(view, errors);
            ValidateSnapshotTimingPendingTaskQueuePayloadShape(view, errors);
            ValidateSnapshotTimingPendingTaskQueuePayloadPropertyNames(view, errors);
            ValidateSnapshotTimingPendingTaskQueueTaskPayloadShapes(view, errors);
            ValidateSnapshotTimingPendingTaskQueueMetadataPayloadShape(view, errors);
            ValidateSnapshotTimingPendingTaskQueuePayloadValues(view, errors);
            ValidateSnapshotTimingObjectPayloadShape(
                view,
                "pendingHandChoice",
                "pending hand choice",
                errors);
            ValidateSnapshotTimingObjectPayloadPropertyNames(
                view,
                "pendingHandChoice",
                "pending hand choice",
                errors);
            ValidateSnapshotTimingPendingHandChoicePayloadValues(view, errors);
            ValidateSnapshotTimingObjectPayloadShape(
                view,
                "pendingCardChoice",
                "pending card choice",
                errors);
            ValidateSnapshotTimingObjectPayloadPropertyNames(
                view,
                "pendingCardChoice",
                "pending card choice",
                errors);
            ValidateSnapshotTimingPendingCardChoicePayloadValues(view, errors, authoritativeState);
            ValidateSnapshotTimingTopLevelObjectListPayloadShapes(view, errors);
            ValidateSnapshotTimingListItemPayloadShapes(
                view,
                "ruleQueueCoverage",
                "rule queue coverage",
                errors);
            ValidateSnapshotTimingListItemPayloadPropertyNames(
                view,
                "ruleQueueCoverage",
                "rule queue coverage",
                errors);
            ValidateSnapshotTimingRuleQueueCoveragePayloadValues(view, errors);
            ValidateSnapshotTimingListItemPayloadShapes(
                view,
                "continuousEffects",
                "continuous effect",
                errors);
            ValidateSnapshotTimingListItemPayloadPropertyNames(
                view,
                "continuousEffects",
                "continuous effect",
                errors);
            ValidateSnapshotTimingContinuousEffectScalarPayloadValues(view, errors, authoritativeState);
            ValidateSnapshotTimingMinimumMightProvenance(view, authoritativeState, errors);
            ValidateSnapshotTimingContinuousEffectListPayloadValues(view, errors);
            ValidateSnapshotTimingListItemPayloadShapes(
                view,
                "triggerQueue",
                "trigger queue item",
                errors);
            ValidateSnapshotTimingListItemPayloadPropertyNames(
                view,
                "triggerQueue",
                "trigger queue",
                errors);
            ValidateSnapshotTimingTriggerQueuePayloadValues(view, errors, authoritativeState);
            ValidateSnapshotTimingListItemPayloadShapes(
                view,
                "battlefieldTasks",
                "battlefield task",
                errors);
            ValidateSnapshotTimingListItemPayloadPropertyNames(
                view,
                "battlefieldTasks",
                "battlefield task",
                errors);
            ValidateSnapshotTimingBattlefieldTaskScalarPayloadValues(view, errors);
            ValidateSnapshotTimingBattlefieldTaskListPayloadValues(view, errors);
            ValidateSnapshotTimingListItemPayloadShapes(
                view,
                "battlefieldResolutions",
                "battlefield resolution",
                errors);
            ValidateSnapshotTimingListItemPayloadPropertyNames(
                view,
                "battlefieldResolutions",
                "battlefield resolution",
                errors);
            ValidateSnapshotTimingListItemPayloadShapes(
                view,
                "battleResolutions",
                "battle resolution",
                errors);
            ValidateSnapshotTimingListItemPayloadPropertyNames(
                view,
                "battleResolutions",
                "battle resolution",
                errors);
            ValidateSnapshotTimingResolutionHistoryScalarPayloadValues(view, errors, authoritativeState);
            ValidateSnapshotTimingResolutionHistoryListPayloadValues(view, errors);
        }
    }

    private static void ValidateSnapshotTimingTopLevelPlayerListPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        var payloadLabel = $"snapshot for {view.PlayerId} timing";
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            view.Snapshot.Timing,
            "readyPlayerIds",
            payloadLabel,
            "ready player",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            view.Snapshot.Timing,
            "passedPriorityPlayerIds",
            payloadLabel,
            "passed priority player",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            view.Snapshot.Timing,
            "passedFocusPlayerIds",
            payloadLabel,
            "passed focus player",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            view.Snapshot.Timing,
            "destroyedUnitOwnerIdsThisTurn",
            payloadLabel,
            "destroyed unit owner",
            errors);
    }

    private static void ValidateSnapshotTimingTopLevelObjectListPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        var payloadLabel = $"snapshot for {view.PlayerId} timing";
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Timing,
            "continuousEffects",
            payloadLabel,
            "continuous effect",
            errors);
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Timing,
            "triggerQueue",
            payloadLabel,
            "trigger queue",
            errors);
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Timing,
            "battlefieldTasks",
            payloadLabel,
            "battlefield task",
            errors);
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Timing,
            "battlefieldResolutions",
            payloadLabel,
            "battlefield resolution",
            errors);
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Timing,
            "battleResolutions",
            payloadLabel,
            "battle resolution",
            errors);
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Timing,
            "ruleQueueCoverage",
            payloadLabel,
            "rule queue coverage",
            errors);
    }

    private static void ValidateSnapshotTimingRuleQueueCoveragePayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, "ruleQueueCoverage", out var coveragePayloads))
        {
            return;
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var coveragePayload in coveragePayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(coveragePayload))
            {
                continue;
            }

            var payloadLabel = $"snapshot for {view.PlayerId} timing rule queue coverage item";
            var key = ValidateSnapshotPayloadRequiredStringValue(
                coveragePayload,
                "key",
                payloadLabel,
                "key",
                errors,
                IsKnownRuleQueueCoverageKey,
                KnownRuleQueueCoverageKeys);
            if (key is not null && !seenKeys.Add(key))
            {
                errors.Add($"{payloadLabel} key {key} is duplicated");
            }

            var liveCount = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
                coveragePayload,
                "liveCount",
                payloadLabel,
                "live count",
                errors);
            var evidenceKeys = ValidateSnapshotPayloadRequiredStringListValues(
                coveragePayload,
                "evidenceKeys",
                payloadLabel,
                "evidence key",
                errors,
                IsKnownRuleQueueCoverageEvidenceKey);
            if (evidenceKeys is not null)
            {
                var duplicateEvidenceKeys = evidenceKeys
                    .GroupBy(value => value, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                foreach (var duplicate in duplicateEvidenceKeys)
                {
                    errors.Add($"{payloadLabel} evidence key {duplicate} is duplicated");
                }
            }

            if (liveCount is > 0 && evidenceKeys is { Count: 0 })
            {
                errors.Add($"{payloadLabel} evidence key list must not be empty when live count is positive");
            }

            if (liveCount == 0 && evidenceKeys is { Count: > 0 })
            {
                errors.Add($"{payloadLabel} evidence key list must be empty when live count is zero");
            }
        }
    }

    private static void ValidateSnapshotLaneListPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        var payloadLabel = $"snapshot for {view.PlayerId} lanes";
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Lanes,
            "battlefieldObjectIds",
            payloadLabel,
            "battlefield object id",
            errors);
        ValidateSnapshotPayloadRequiredObjectListPayloadShape(
            view.Snapshot.Lanes,
            "battlefields",
            payloadLabel,
            "battlefield",
            errors);
    }

    private static void ValidateSnapshotLaneListItemPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        ValidateSnapshotPayloadObjectListItemPayloadShapes(
            view.Snapshot.Lanes,
            "battlefieldObjectIds",
            $"snapshot for {view.PlayerId} lanes battlefield object id",
            errors);
        ValidateSnapshotPayloadObjectListItemPayloadShapes(
            view.Snapshot.Lanes,
            "battlefields",
            $"snapshot for {view.PlayerId} lanes battlefield",
            errors);
    }

    private static void ValidateSnapshotLaneListItemPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        ValidateSnapshotPayloadObjectListItemPayloadPropertyNames(
            view.Snapshot.Lanes,
            "battlefieldObjectIds",
            $"snapshot for {view.PlayerId} lanes battlefield object id",
            errors);
        ValidateSnapshotPayloadObjectListItemPayloadPropertyNames(
            view.Snapshot.Lanes,
            "battlefields",
            $"snapshot for {view.PlayerId} lanes battlefield",
            errors);
    }

    private static void ValidateSnapshotLaneBattlefieldObjectIdItemPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (!TryReadObjectList(view.Snapshot.Lanes, "battlefieldObjectIds", out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!IsSnapshotPlayerPayloadObject(item))
            {
                continue;
            }

            var itemLabel = $"snapshot for {view.PlayerId} lanes battlefield object id item {index + 1}";
            ValidateSnapshotPayloadRequiredStringValue(
                item,
                "playerId",
                itemLabel,
                "player id",
                errors);
            ValidateSnapshotPayloadRequiredStringValue(
                item,
                "objectId",
                itemLabel,
                "object id",
                errors);
        }
    }

    private static void ValidateSnapshotLaneBattlefieldItemScalarPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (!TryReadObjectList(view.Snapshot.Lanes, "battlefields", out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!IsSnapshotPlayerPayloadObject(item))
            {
                continue;
            }

            var itemLabel = $"snapshot for {view.PlayerId} lanes battlefield item {index + 1}";
            ValidateSnapshotPayloadRequiredStringValue(
                item,
                "battlefieldObjectId",
                itemLabel,
                "battlefield object id",
                errors);
            ValidateSnapshotPayloadRequiredStringValue(
                item,
                "zonePlayerId",
                itemLabel,
                "zone player",
                errors);
            ValidateSnapshotPayloadOptionalStringValue(
                item,
                "cardNo",
                itemLabel,
                "card number",
                errors);
            ValidateSnapshotPayloadOptionalStringValue(
                item,
                "controllerId",
                itemLabel,
                "controller",
                errors);
            ValidateSnapshotPayloadRequiredStringValue(
                item,
                "status",
                itemLabel,
                "status",
                errors,
                IsKnownBattlefieldStatus);
            ValidateSnapshotPayloadRequiredBoolValue(
                item,
                "contested",
                itemLabel,
                "contested",
                errors);
            ValidateSnapshotPayloadRequiredBoolValue(
                item,
                "scoredThisTurn",
                itemLabel,
                "scored this turn",
                errors);
            ValidateSnapshotPayloadRequiredNonNegativeIntValue(
                item,
                "standbySlotCount",
                itemLabel,
                "standby slot count",
                errors);
            ValidateSnapshotPayloadRequiredNonNegativeIntValue(
                item,
                "faceDownStandbyCount",
                itemLabel,
                "face-down standby count",
                errors);
            ValidateSnapshotPayloadRequiredNonNegativeIntValue(
                item,
                "hiddenStandbyCount",
                itemLabel,
                "hidden standby count",
                errors);
        }
    }

    private static void ValidateSnapshotLaneBattlefieldItemListPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (!TryReadObjectList(view.Snapshot.Lanes, "battlefields", out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!IsSnapshotPlayerPayloadObject(item))
            {
                continue;
            }

            var itemLabel = $"snapshot for {view.PlayerId} lanes battlefield item {index + 1}";
            ValidateSnapshotPayloadRequiredStringListValues(
                item,
                "occupantObjectIds",
                itemLabel,
                "occupant object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListValues(
                item,
                "occupantControllerIds",
                itemLabel,
                "occupant controller id",
                errors);
            ValidateSnapshotPayloadRequiredStringListDictionaryValues(
                item,
                "unitsBySide",
                itemLabel,
                "units by side",
                "unit object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListValues(
                item,
                "standbyObjectIds",
                itemLabel,
                "standby object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListValues(
                item,
                "scoredThisTurnPlayerIds",
                itemLabel,
                "scored player id",
                errors);
            ValidateSnapshotPayloadRequiredStringListValues(
                item,
                "pendingTaskKinds",
                itemLabel,
                "pending task kind",
                errors);
        }
    }

    private static void ValidateSnapshotLaneBattlefieldStandbySlotListPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (!TryReadObjectList(view.Snapshot.Lanes, "battlefields", out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!IsSnapshotPlayerPayloadObject(item))
            {
                continue;
            }

            ValidateSnapshotPayloadRequiredObjectListPayloadShape(
                item,
                "standbySlots",
                $"snapshot for {view.PlayerId} lanes battlefield item {index + 1}",
                "standby slot",
                errors);
        }
    }

    private static void ValidateSnapshotLaneBattlefieldStandbySlotItemPayloadShapes(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (!TryReadObjectList(view.Snapshot.Lanes, "battlefields", out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!IsSnapshotPlayerPayloadObject(item))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectListItemPayloadShapes(
                item,
                "standbySlots",
                $"snapshot for {view.PlayerId} lanes battlefield item {index + 1} standby slot",
                errors);
        }
    }

    private static void ValidateSnapshotLaneBattlefieldStandbySlotItemPayloadPropertyNames(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (!TryReadObjectList(view.Snapshot.Lanes, "battlefields", out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!IsSnapshotPlayerPayloadObject(item))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectListItemPayloadPropertyNames(
                item,
                "standbySlots",
                $"snapshot for {view.PlayerId} lanes battlefield item {index + 1} standby slot",
                errors);
        }
    }

    private static void ValidateSnapshotLaneBattlefieldStandbySlotItemPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (!TryReadObjectList(view.Snapshot.Lanes, "battlefields", out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!IsSnapshotPlayerPayloadObject(item)
                || !TryReadObjectList(item, "standbySlots", out var standbySlots))
            {
                continue;
            }

            for (var slotIndex = 0; slotIndex < standbySlots.Count; slotIndex++)
            {
                var slot = standbySlots[slotIndex];
                if (!IsSnapshotPlayerPayloadObject(slot))
                {
                    continue;
                }

                var slotLabel = $"snapshot for {view.PlayerId} lanes battlefield item {index + 1} standby slot item {slotIndex + 1}";
                ValidateSnapshotPayloadRequiredStringValue(
                    slot,
                    "slotId",
                    slotLabel,
                    "slot id",
                    errors);
                ValidateSnapshotPayloadRequiredStringValue(
                    slot,
                    "battlefieldObjectId",
                    slotLabel,
                    "battlefield id",
                    errors);
                ValidateSnapshotPayloadOptionalStringValue(
                    slot,
                    "sidePlayerId",
                    slotLabel,
                    "side player",
                    errors);
                ValidateSnapshotPayloadOptionalStringValue(
                    slot,
                    "controllerId",
                    slotLabel,
                    "controller",
                    errors);
                ValidateSnapshotPayloadRequiredBoolValue(
                    slot,
                    "visible",
                    slotLabel,
                    "visibility",
                    errors);
                var state = ValidateSnapshotPayloadRequiredStringValue(
                    slot,
                    "state",
                    slotLabel,
                    "state",
                    errors,
                    IsKnownStandbySlotState);
                ValidateSnapshotPayloadRequiredBoolValue(
                    slot,
                    "isFaceDown",
                    slotLabel,
                    "face-down flag",
                    errors);

                if (TryReadObjectBool(slot, "visible", out var visible))
                {
                    if (state is not null)
                    {
                        var expectedState = visible ? "VISIBLE" : "HIDDEN";
                        if (!string.Equals(state, expectedState, StringComparison.Ordinal))
                        {
                            errors.Add($"{slotLabel} state does not match visibility");
                        }
                    }

                    if (visible)
                    {
                        ValidateSnapshotPayloadRequiredStringValue(
                            slot,
                            "objectId",
                            slotLabel,
                            "object id",
                            errors);
                    }
                    else if (TryReadObjectValue(slot, "objectId", out _))
                    {
                        errors.Add($"{slotLabel} hidden object id must be redacted");
                    }
                }
                else
                {
                    ValidateSnapshotPayloadOptionalStringValue(
                        slot,
                        "objectId",
                        slotLabel,
                        "object id",
                        errors);
                }
            }
        }
    }

    private static void ValidateSnapshotTimingObjectPayloadShape(
        RecoveredPlayerView view,
        string key,
        string payloadLabel,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, key, out var payload)
            || IsNullSnapshotPayloadValue(payload))
        {
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(payload))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {payloadLabel} payload is required");
        }
    }

    private static void ValidateSnapshotTimingObjectPayloadPropertyNames(
        RecoveredPlayerView view,
        string key,
        string payloadLabel,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, key, out var payload)
            || !IsSnapshotPlayerPayloadObject(payload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            payload,
            $"snapshot for {view.PlayerId} timing {payloadLabel}",
            errors);
    }
}
