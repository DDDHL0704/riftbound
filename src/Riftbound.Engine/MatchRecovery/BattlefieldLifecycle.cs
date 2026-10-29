using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateBattlefieldTaskDerivedIdentity(
        string taskLabel,
        string? kind,
        string? battlefieldObjectId,
        string? spellDuelId,
        bool hasSpellDuelIdPayload,
        string? battleId,
        bool hasBattleIdPayload,
        List<string> errors)
    {
        if (kind is null || battlefieldObjectId is null)
        {
            return;
        }

        if (string.Equals(kind, "START_SPELL_DUEL", StringComparison.Ordinal))
        {
            var expectedSpellDuelId = BattleLifecycleIds.SpellDuelIdForBattlefield(battlefieldObjectId);
            if (expectedSpellDuelId is null)
            {
                return;
            }

            if (spellDuelId is null)
            {
                if (!hasSpellDuelIdPayload)
                {
                    errors.Add($"{taskLabel} spell duel id is required for START_SPELL_DUEL");
                }

                return;
            }

            if (spellDuelId.Length == 0)
            {
                errors.Add($"{taskLabel} spell duel id is required for START_SPELL_DUEL");
                return;
            }

            if (!string.Equals(spellDuelId, expectedSpellDuelId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} spell duel id {spellDuelId} does not match battlefield spell duel id {expectedSpellDuelId}; {FormatExpectedActualForRecovery(expectedSpellDuelId, spellDuelId)}");
            }

            return;
        }

        if (string.Equals(kind, "START_BATTLE", StringComparison.Ordinal))
        {
            var expectedBattleId = BattleLifecycleIds.BattleIdForBattlefield(battlefieldObjectId);
            if (expectedBattleId is null)
            {
                return;
            }

            if (battleId is null)
            {
                if (!hasBattleIdPayload)
                {
                    errors.Add($"{taskLabel} battle id is required for START_BATTLE");
                }

                return;
            }

            if (battleId.Length == 0)
            {
                errors.Add($"{taskLabel} battle id is required for START_BATTLE");
                return;
            }

            if (!string.Equals(battleId, expectedBattleId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} battle id {battleId} does not match battlefield battle id {expectedBattleId}; {FormatExpectedActualForRecovery(expectedBattleId, battleId)}");
            }
        }
    }

    private static void ValidateBattlefieldTaskKindSpecificIdentityExclusivity(
        string taskLabel,
        string? kind,
        string? spellDuelId,
        string? battleId,
        List<string> errors)
    {
        if (kind is null)
        {
            return;
        }

        if (string.Equals(kind, "START_SPELL_DUEL", StringComparison.Ordinal))
        {
            if (battleId is { Length: > 0 })
            {
                errors.Add($"{taskLabel} battle id {battleId} is invalid for START_SPELL_DUEL");
            }

            return;
        }

        if (string.Equals(kind, "START_BATTLE", StringComparison.Ordinal)
            && spellDuelId is { Length: > 0 })
        {
            errors.Add($"{taskLabel} spell duel id {spellDuelId} is invalid for START_BATTLE");
        }
    }

    private static void ValidateBattlefieldTaskKindConsistency(
        string taskLabel,
        string? kind,
        string? status,
        string? reason,
        List<string> errors)
    {
        if (kind is null)
        {
            return;
        }

        if (string.Equals(kind, "START_SPELL_DUEL", StringComparison.Ordinal))
        {
            if (reason is { Length: > 0 }
                && !string.Equals(reason, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} reason {reason} does not match START_SPELL_DUEL reason BATTLEFIELD_CONTESTED; {FormatExpectedActualForRecovery("BATTLEFIELD_CONTESTED", reason)}");
            }

            if (string.Equals(status, "WAITING_FOR_SPELL_DUEL", StringComparison.Ordinal))
            {
                errors.Add($"{taskLabel} status WAITING_FOR_SPELL_DUEL is invalid for START_SPELL_DUEL");
            }

            return;
        }

        if (string.Equals(kind, "START_BATTLE", StringComparison.Ordinal))
        {
            if (reason is { Length: > 0 }
                && !string.Equals(reason, "SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} reason {reason} does not match START_BATTLE reason SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST; {FormatExpectedActualForRecovery("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", reason)}");
            }

            if (string.Equals(status, "COMPLETED", StringComparison.Ordinal))
            {
                errors.Add($"{taskLabel} status COMPLETED is invalid for START_BATTLE");
            }
        }
    }

    private static void ValidateBattlefieldTaskKindSet(
        IReadOnlyList<object?> taskPayloads,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? requiredTaskKindsByBattlefield,
        string taskLabel,
        string requiredTaskKindsLabel,
        List<string> errors)
    {
        if (requiredTaskKindsByBattlefield is null)
        {
            return;
        }

        var actualTaskKindsByBattlefield = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload)
                || !TryReadObjectString(taskPayload, "kind", out var kind)
                || string.IsNullOrWhiteSpace(kind)
                || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId))
            {
                continue;
            }

            var normalizedKind = kind.Trim();
            if (!IsKnownBattlefieldTaskKind(normalizedKind))
            {
                continue;
            }

            var normalizedBattlefieldObjectId = battlefieldObjectId.Trim();
            if (!actualTaskKindsByBattlefield.TryGetValue(normalizedBattlefieldObjectId, out var actualTaskKinds))
            {
                actualTaskKinds = new HashSet<string>(StringComparer.Ordinal);
                actualTaskKindsByBattlefield[normalizedBattlefieldObjectId] = actualTaskKinds;
            }

            if (!actualTaskKinds.Add(normalizedKind))
            {
                errors.Add(
                    $"{taskLabel} kind {normalizedKind} is duplicated for battlefield object id {normalizedBattlefieldObjectId}");
            }
        }

        foreach (var battlefieldObjectId in actualTaskKindsByBattlefield.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            var actualTaskKinds = actualTaskKindsByBattlefield[battlefieldObjectId];
            requiredTaskKindsByBattlefield.TryGetValue(battlefieldObjectId, out var requiredTaskKinds);
            foreach (var actualTaskKind in actualTaskKinds.OrderBy(kind => kind, StringComparer.Ordinal))
            {
                if (requiredTaskKinds is null || !requiredTaskKinds.Contains(actualTaskKind))
                {
                    errors.Add(
                        $"{taskLabel} kind {actualTaskKind} is not required by {requiredTaskKindsLabel} for battlefield object id {battlefieldObjectId}");
                }
            }
        }

        foreach (var battlefieldObjectId in requiredTaskKindsByBattlefield.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            var requiredTaskKinds = requiredTaskKindsByBattlefield[battlefieldObjectId];
            actualTaskKindsByBattlefield.TryGetValue(battlefieldObjectId, out var actualTaskKinds);
            foreach (var requiredTaskKind in requiredTaskKinds.OrderBy(kind => kind, StringComparer.Ordinal))
            {
                if (actualTaskKinds is null || !actualTaskKinds.Contains(requiredTaskKind))
                {
                    errors.Add(
                        $"{taskLabel} kind {requiredTaskKind} is required by {requiredTaskKindsLabel} for battlefield object id {battlefieldObjectId}");
                }
            }
        }
    }

    private static void ValidateSpectatorBattlefieldTaskKeyedAuthoritativeValues(
        IReadOnlyList<object?> taskPayloads,
        IReadOnlyDictionary<(string BattlefieldObjectId, string Kind), BattlefieldTaskState> authoritativeTasksByKey,
        List<string> errors)
    {
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload)
                || !TryReadObjectString(taskPayload, "kind", out var kind)
                || string.IsNullOrWhiteSpace(kind)
                || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId))
            {
                continue;
            }

            var normalizedKind = kind.Trim();
            if (!IsKnownBattlefieldTaskKind(normalizedKind))
            {
                continue;
            }

            var normalizedBattlefieldObjectId = battlefieldObjectId.Trim();
            if (!authoritativeTasksByKey.TryGetValue(
                    (normalizedBattlefieldObjectId, normalizedKind),
                    out var authoritativeTask))
            {
                continue;
            }

            ValidateSpectatorBattlefieldTaskKeyedRequiredStringValue(
                taskPayload,
                "status",
                "status",
                authoritativeTask.Status,
                normalizedBattlefieldObjectId,
                normalizedKind,
                errors);
            ValidateSpectatorBattlefieldTaskKeyedRequiredStringValue(
                taskPayload,
                "reason",
                "reason",
                authoritativeTask.Reason,
                normalizedBattlefieldObjectId,
                normalizedKind,
                errors);
            ValidateSpectatorBattlefieldTaskKeyedRequiredStringListValue(
                taskPayload,
                "participantControllerIds",
                "participant controller ids",
                authoritativeTask.ParticipantControllerIds,
                normalizedBattlefieldObjectId,
                normalizedKind,
                errors);
            ValidateSpectatorBattlefieldTaskKeyedRequiredStringListValue(
                taskPayload,
                "participantObjectIds",
                "participant object ids",
                authoritativeTask.ParticipantObjectIds,
                normalizedBattlefieldObjectId,
                normalizedKind,
                errors);
            ValidateSpectatorBattlefieldTaskKeyedOptionalStringValue(
                taskPayload,
                "actingPlayerId",
                "acting player id",
                authoritativeTask.ActingPlayerId,
                normalizedBattlefieldObjectId,
                normalizedKind,
                errors);
            ValidateSpectatorBattlefieldTaskKeyedRequiredStringListValue(
                taskPayload,
                "stackItemIds",
                "stack item ids",
                authoritativeTask.StackItemIds,
                normalizedBattlefieldObjectId,
                normalizedKind,
                errors);
        }
    }

    private static void ValidateSpectatorBattlefieldTaskKeyedRequiredStringValue(
        object? taskPayload,
        string key,
        string fieldLabel,
        string expected,
        string battlefieldObjectId,
        string kind,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield task item";
        if (!TryReadObjectString(taskPayload, key, out var actual))
        {
            var detail = FormatExpectedActualForRecovery(
                expected,
                FormatUnreadableObjectValue(taskPayload, key));
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match authoritative state battlefield task {fieldLabel} {expected} for battlefield object id {battlefieldObjectId} kind {kind}; {detail}");
            return;
        }

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            var detail = FormatExpectedActualForRecovery(expected, actual);
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match authoritative state battlefield task {fieldLabel} {expected} for battlefield object id {battlefieldObjectId} kind {kind}; {detail}");
        }
    }

    private static void ValidateSpectatorBattlefieldTaskKeyedOptionalStringValue(
        object? taskPayload,
        string key,
        string fieldLabel,
        string? expected,
        string battlefieldObjectId,
        string kind,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield task item";
        var expectedText = expected ?? string.Empty;
        if (!TryReadObjectValue(taskPayload, key, out var value))
        {
            if (!string.IsNullOrEmpty(expectedText))
            {
                errors.Add(
                    $"{payloadLabel} {fieldLabel} does not match authoritative state battlefield task {fieldLabel} {expectedText} for battlefield object id {battlefieldObjectId} kind {kind}; {FormatExpectedActualForRecovery(expectedText, "<missing>")}");
            }

            return;
        }

        if (!TryReadOptionalStringValue(value, out var actual))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match authoritative state battlefield task {fieldLabel} {expectedText} for battlefield object id {battlefieldObjectId} kind {kind}; {FormatExpectedActualForRecovery(expectedText, "<unreadable>")}");
            return;
        }

        if (!string.Equals(actual, expectedText, StringComparison.Ordinal))
        {
            var detail = FormatExpectedActualForRecovery(expectedText, actual);
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match authoritative state battlefield task {fieldLabel} {expectedText} for battlefield object id {battlefieldObjectId} kind {kind}; {detail}");
        }
    }

    private static void ValidateSpectatorBattlefieldTaskKeyedRequiredStringListValue(
        object? taskPayload,
        string key,
        string fieldLabel,
        IReadOnlyList<string> expected,
        string battlefieldObjectId,
        string kind,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield task item";
        if (!TryReadObjectValue(taskPayload, key, out var value))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} disagree with authoritative state battlefield task {fieldLabel} for battlefield object id {battlefieldObjectId} kind {kind}; {FormatExpectedActualForRecovery(expected, "<missing>")}");
            return;
        }

        if (!TryReadStringListValue(value, out var actual)
            || !StringListsEqual(actual, expected))
        {
            var detail = FormatExpectedActualForRecovery(
                expected,
                TryReadStringListValue(value, out var readableActual) ? readableActual : "<unreadable>");
            errors.Add(
                $"{payloadLabel} {fieldLabel} disagree with authoritative state battlefield task {fieldLabel} for battlefield object id {battlefieldObjectId} kind {kind}; {detail}");
        }
    }

    private static void ValidateSnapshotBattlefieldTaskActiveSpellDuelLifecycleConsistency(
        RecoveredPlayerView view,
        IReadOnlyList<object?> taskPayloads,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "spellDuel", out var spellDuelPayload)
            || !IsSnapshotPlayerPayloadObject(spellDuelPayload)
            || !TryReadObjectBool(spellDuelPayload, "isActive", out var isActive)
            || !isActive
            || !TryReadObjectOptionalString(spellDuelPayload, "battlefieldObjectId", out var spellDuelBattlefieldObjectId)
            || string.IsNullOrWhiteSpace(spellDuelBattlefieldObjectId))
        {
            return;
        }

        var normalizedBattlefieldObjectId = spellDuelBattlefieldObjectId.Trim();
        var hasFocusPlayerId = TryReadObjectOptionalString(spellDuelPayload, "focusPlayerId", out var focusPlayerId)
            && !string.IsNullOrWhiteSpace(focusPlayerId);
        if (hasFocusPlayerId)
        {
            focusPlayerId = focusPlayerId.Trim();
        }

        var hasStackItemIds = TryReadObjectStringList(spellDuelPayload, "stackItemIds", out var spellDuelStackItemIds);
        var taskLabel = $"snapshot for {view.PlayerId} timing battlefield task item";
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload)
                || !TryReadObjectString(taskPayload, "kind", out var kind)
                || string.IsNullOrWhiteSpace(kind)
                || !string.Equals(kind.Trim(), "START_SPELL_DUEL", StringComparison.Ordinal)
                || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId)
                || !string.Equals(battlefieldObjectId.Trim(), normalizedBattlefieldObjectId, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryReadObjectString(taskPayload, "status", out var status)
                && !string.Equals(status, "ACTIVE", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} status {status} does not match active spell duel status ACTIVE for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery("ACTIVE", status)}");
            }

            if (hasFocusPlayerId
                && TryReadObjectOptionalString(taskPayload, "actingPlayerId", out var actingPlayerId)
                && !string.Equals(actingPlayerId, focusPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} acting player id {actingPlayerId} does not match active spell duel focus player id {focusPlayerId} for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(focusPlayerId, actingPlayerId)}");
            }

            if (hasStackItemIds
                && TryReadObjectStringList(taskPayload, "stackItemIds", out var stackItemIds)
                && !StringListsEqual(stackItemIds, spellDuelStackItemIds))
            {
                errors.Add(
                    $"{taskLabel} stack item ids disagree with active spell duel stack item ids for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(spellDuelStackItemIds, stackItemIds)}");
            }
        }
    }

    private static void ValidateSnapshotBattlefieldTaskActiveBattleLifecycleConsistency(
        RecoveredPlayerView view,
        IReadOnlyList<object?> taskPayloads,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            || !IsSnapshotPlayerPayloadObject(battlePayload)
            || !TryReadObjectBool(battlePayload, "isActive", out var isActive)
            || !isActive
            || !TryReadObjectOptionalString(battlePayload, "battlefieldObjectId", out var battleBattlefieldObjectId)
            || string.IsNullOrWhiteSpace(battleBattlefieldObjectId))
        {
            return;
        }

        var normalizedBattlefieldObjectId = battleBattlefieldObjectId.Trim();
        var hasBattleId = TryReadObjectOptionalString(battlePayload, "battleId", out var battleId)
            && !string.IsNullOrWhiteSpace(battleId);
        if (hasBattleId)
        {
            battleId = battleId.Trim();
        }

        var activePlayerId = string.IsNullOrWhiteSpace(view.Snapshot.ActivePlayerId)
            ? null
            : view.Snapshot.ActivePlayerId.Trim();
        var taskLabel = $"snapshot for {view.PlayerId} timing battlefield task item";
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload)
                || !TryReadObjectString(taskPayload, "kind", out var kind)
                || string.IsNullOrWhiteSpace(kind)
                || !string.Equals(kind.Trim(), "START_BATTLE", StringComparison.Ordinal)
                || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId)
                || !string.Equals(battlefieldObjectId.Trim(), normalizedBattlefieldObjectId, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryReadObjectString(taskPayload, "status", out var status)
                && !string.Equals(status, "ACTIVE", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} status {status} does not match active battle status ACTIVE for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery("ACTIVE", status)}");
            }

            if (activePlayerId is not null
                && TryReadObjectOptionalString(taskPayload, "actingPlayerId", out var actingPlayerId)
                && !string.Equals(actingPlayerId, activePlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} acting player id {actingPlayerId} does not match active battle active player id {activePlayerId} for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(activePlayerId, actingPlayerId)}");
            }

            if (TryReadObjectStringList(taskPayload, "stackItemIds", out var stackItemIds)
                && stackItemIds.Count > 0)
            {
                errors.Add(
                    $"{taskLabel} stack item ids must be empty for active battle for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(Array.Empty<string>(), stackItemIds)}");
            }

            if (hasBattleId
                && TryReadObjectOptionalString(taskPayload, "battleId", out var taskBattleId)
                && !string.Equals(taskBattleId, battleId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} battle id {taskBattleId} does not match active battle id {battleId} for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(battleId, taskBattleId)}");
            }
        }
    }

    private static void ValidateSnapshotBattlefieldTaskWaitingBattleSpellDuelLifecycleConsistency(
        RecoveredPlayerView view,
        IReadOnlyList<object?> taskPayloads,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectValue(view.Snapshot.Timing, "spellDuel", out var spellDuelPayload)
            || !IsSnapshotPlayerPayloadObject(spellDuelPayload)
            || !TryReadObjectBool(spellDuelPayload, "isActive", out var isSpellDuelActive)
            || !isSpellDuelActive
            || !TryReadObjectOptionalString(spellDuelPayload, "battlefieldObjectId", out var spellDuelBattlefieldObjectId)
            || string.IsNullOrWhiteSpace(spellDuelBattlefieldObjectId))
        {
            return;
        }

        if (TryReadObjectValue(view.Snapshot.Timing, "battle", out var battlePayload)
            && IsSnapshotPlayerPayloadObject(battlePayload)
            && TryReadObjectBool(battlePayload, "isActive", out var isBattleActive)
            && isBattleActive)
        {
            return;
        }

        var normalizedBattlefieldObjectId = spellDuelBattlefieldObjectId.Trim();
        var taskLabel = $"snapshot for {view.PlayerId} timing battlefield task item";
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload)
                || !TryReadObjectString(taskPayload, "kind", out var kind)
                || string.IsNullOrWhiteSpace(kind)
                || !string.Equals(kind.Trim(), "START_BATTLE", StringComparison.Ordinal)
                || !TryReadObjectString(taskPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId)
                || !string.Equals(battlefieldObjectId.Trim(), normalizedBattlefieldObjectId, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryReadObjectString(taskPayload, "status", out var status)
                && !string.Equals(status, "WAITING_FOR_SPELL_DUEL", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{taskLabel} status {status} does not match active spell duel waiting battle status WAITING_FOR_SPELL_DUEL for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery("WAITING_FOR_SPELL_DUEL", status)}");
            }

            if (TryReadObjectOptionalString(taskPayload, "actingPlayerId", out var actingPlayerId)
                && !string.IsNullOrEmpty(actingPlayerId))
            {
                errors.Add(
                    $"{taskLabel} acting player id {actingPlayerId} must be empty while waiting for active spell duel for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(string.Empty, actingPlayerId)}");
            }

            if (TryReadObjectStringList(taskPayload, "stackItemIds", out var stackItemIds)
                && stackItemIds.Count > 0)
            {
                errors.Add(
                    $"{taskLabel} stack item ids must be empty while waiting for active spell duel for battlefield object id {normalizedBattlefieldObjectId}; {FormatExpectedActualForRecovery(Array.Empty<string>(), stackItemIds)}");
            }
        }
    }

    private static void ValidateSnapshotTimingBattlefieldTaskListPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, "battlefieldTasks", out var taskPayloads))
        {
            return;
        }

        var knownObjectIds = view.Snapshot.Players is null
            ? null
            : BuildSnapshotKnownObjectIds(view.Snapshot);
        var objectLocations = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectLocationIndex(view.Snapshot);
        var objectControllers = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectControllerIndex(view.Snapshot);
        var objectTags = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectTagIndex(view.Snapshot);
        var battlefieldStateOccupantObjectIds = BuildSnapshotBattlefieldStateOccupantObjectIds(view.Snapshot);
        var knownPlayerIds = view.Snapshot.Players is null
            ? null
            : BuildNormalizedPlayerIdSet(view.Snapshot.Players.Keys);
        var knownStackItemIds = view.Snapshot.Stack is null
            ? null
            : BuildKnownStackItemIds(ExtractStackItemIds(view.Snapshot));
        foreach (var taskPayload in taskPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(taskPayload))
            {
                continue;
            }

            const string payloadLabel = "battlefield task item";
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                taskPayload,
                "participantControllerIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "participant controller id",
                errors);
            ValidateSnapshotPayloadStringListValues(
                taskPayload,
                "participantControllerIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "participant controller id",
                errors);
            ValidateTimingPlayerReferenceList(
                taskPayload,
                "participantControllerIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel} participant controller id",
                knownPlayerIds,
                "players",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                taskPayload,
                "participantObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "participant object id",
                errors);
            ValidateSnapshotPayloadStringListValues(
                taskPayload,
                "participantObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "participant object id",
                errors);
            ValidateTimingObjectReferenceListWithExpectedDetails(
                taskPayload,
                "participantObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel} participant object id",
                knownObjectIds,
                "objects",
                errors);
            ValidateBattlefieldTaskParticipantObjectBattlefieldMembership(
                taskPayload,
                $"snapshot for {view.PlayerId} timing {payloadLabel} participant object id",
                objectLocations,
                errors);
            ValidateBattlefieldTaskParticipantObjectUnitMembership(
                taskPayload,
                $"snapshot for {view.PlayerId} timing {payloadLabel} participant object id",
                objectTags,
                errors);
            ValidateBattlefieldTaskParticipantObjectBattlefieldStateOccupantMembership(
                taskPayload,
                $"snapshot for {view.PlayerId} timing {payloadLabel} participant object id",
                battlefieldStateOccupantObjectIds,
                "battlefield state occupants",
                errors);
            ValidateBattlefieldTaskParticipantControllerObjectConsistency(
                taskPayload,
                $"snapshot for {view.PlayerId} timing {payloadLabel} participant controller id",
                objectControllers,
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                taskPayload,
                "stackItemIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "stack item id",
                errors);
            ValidateSnapshotPayloadStringListValues(
                taskPayload,
                "stackItemIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "stack item id",
                errors);
            ValidateTimingStackItemReferenceList(
                taskPayload,
                "stackItemIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel} stack item id",
                knownStackItemIds,
                "stack",
                errors);
        }
    }

    private static bool IsKnownRuneTrait(string trait)
    {
        return trait is
            RuneTrait.Red
            or RuneTrait.Green
            or RuneTrait.Blue
            or RuneTrait.Yellow
            or RuneTrait.Orange
            or RuneTrait.Purple;
    }

    private static void ValidateSnapshotTimingResolutionHistoryScalarPayloadValues(
        RecoveredPlayerView view,
        List<string> errors, MatchState? authoritativeState)
    {
        if (view.Snapshot.Timing is null)
        {
            return;
        }

        var knownPlayerIds = view.Snapshot.Players is null
            ? null
            : BuildNormalizedPlayerIdSet(view.Snapshot.Players.Keys);
        var knownObjectIds = view.Snapshot.Players is null
            ? null
            : BuildSnapshotKnownObjectIds(view.Snapshot);
        if (TryReadObjectList(view.Snapshot.Timing, "battlefieldResolutions", out var battlefieldResolutionPayloads))
        {
            ValidateRetainedResolutionHistoryListCount(
                battlefieldResolutionPayloads.Count,
                $"snapshot for {view.PlayerId} timing battlefield resolutions",
                errors);
            ValidateRetainedResolutionHistoryPayloadTickOrder(
                battlefieldResolutionPayloads,
                $"snapshot for {view.PlayerId} timing battlefield resolutions",
                $"snapshot for {view.PlayerId} timing battlefield resolution item",
                errors);
            var seenBattlefieldResolutionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resolutionPayload in battlefieldResolutionPayloads)
            {
                if (!IsSnapshotPlayerPayloadObject(resolutionPayload))
                {
                    continue;
                }

                var resolutionLabel = $"snapshot for {view.PlayerId} timing battlefield resolution item";
                var resolutionId = ValidateBattlefieldResolutionPayloadValues(
                    resolutionPayload,
                    resolutionLabel,
                    errors);
                var recorded = authoritativeState?.BattlefieldResolutions.FirstOrDefault(r => r.ResolutionId == resolutionId);
                var historicalIds = recorded?.PublicObjectBindings?.Select(b => b.ObjectId).ToArray() ?? [];
                var historicalKnown = knownObjectIds is null ? null : knownObjectIds.Concat(historicalIds).ToHashSet(StringComparer.Ordinal);
                ValidateBattlefieldResolutionObjectReferences(
                    resolutionPayload,
                    resolutionLabel,
                    historicalKnown,
                    "objects",
                    errors);
                if (recorded is not null && recorded.PublicObjectBindings is not null)
                {
                    if (!TryReadObjectString(resolutionPayload, "sourceObjectId", out var sourceId)) sourceId = "";
                    var expectedSource = recorded.SourceObjectId ?? "";
                    if (recorded.PublicObjectBindings.Any(b => b.ObjectId == expectedSource) && sourceId != expectedSource)
                        errors.Add($"{resolutionLabel} public historical source differs from authoritative captured reference");
                    if (!TryReadObjectStringList(resolutionPayload, "participantObjectIds", out var participants)
                        || participants.Count != recorded.ParticipantObjectIds.Count
                        || participants.Where((id, index) => recorded.PublicObjectBindings.Any(b => b.ObjectId == recorded.ParticipantObjectIds[index])
                            && id != recorded.ParticipantObjectIds[index]).Any())
                        errors.Add($"{resolutionLabel} public historical participants differ from authoritative captured references");
                }
                ValidateBattlefieldResolutionPlayerReferences(
                    resolutionPayload,
                    resolutionLabel,
                    knownPlayerIds,
                    "players",
                    errors);
                if (resolutionId is not null && !seenBattlefieldResolutionIds.Add(resolutionId))
                {
                    errors.Add($"{resolutionLabel} resolution id {resolutionId} is duplicated");
                }
            }
        }

        if (!TryReadObjectList(view.Snapshot.Timing, "battleResolutions", out var battleResolutionPayloads))
        {
            return;
        }

        ValidateRetainedResolutionHistoryListCount(
            battleResolutionPayloads.Count,
            $"snapshot for {view.PlayerId} timing battle resolutions",
            errors);
        ValidateRetainedResolutionHistoryPayloadTickOrder(
            battleResolutionPayloads,
            $"snapshot for {view.PlayerId} timing battle resolutions",
            $"snapshot for {view.PlayerId} timing battle resolution item",
            errors);
        var seenBattleResolutionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolutionPayload in battleResolutionPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(resolutionPayload))
            {
                continue;
            }

            var resolutionLabel = $"snapshot for {view.PlayerId} timing battle resolution item";
            var resolutionId = ValidateBattleResolutionPayloadValues(
                resolutionPayload,
                resolutionLabel,
                errors);
            var recorded = authoritativeState?.BattleResolutions.FirstOrDefault(r => r.ResolutionId == resolutionId);
            var historicalKnown = knownObjectIds is null ? null : knownObjectIds.Concat(recorded?.PublicObjectBindings?.Select(b => b.ObjectId) ?? [])
                .ToHashSet(StringComparer.Ordinal);
            ValidateBattleResolutionObjectReferences(
                resolutionPayload,
                resolutionLabel,
                historicalKnown,
                "objects",
                errors);
            if (recorded is not null) ValidateCapturedPublicBattleHistoryView(resolutionPayload, recorded, resolutionLabel, errors);
            ValidateBattleResolutionPlayerReferences(
                resolutionPayload,
                resolutionLabel,
                knownPlayerIds,
                "players",
                errors);
            if (resolutionId is not null && !seenBattleResolutionIds.Add(resolutionId))
            {
                errors.Add($"{resolutionLabel} resolution id {resolutionId} is duplicated");
            }
        }
    }

    private static void ValidateCapturedPublicBattleHistoryView(object? payload, BattleResolutionState recorded, string label, List<string> errors)
    {
        if (recorded.PublicObjectBindings is not { } bindings) return;
        var fields = new Dictionary<string, IReadOnlyList<string>> {
            ["attackerObjectIds"] = recorded.AttackerObjectIds, ["defenderObjectIds"] = recorded.DefenderObjectIds,
            ["survivingAttackerObjectIds"] = recorded.SurvivingAttackerObjectIds, ["survivingDefenderObjectIds"] = recorded.SurvivingDefenderObjectIds,
            ["destroyedObjectIds"] = recorded.DestroyedObjectIds };
        foreach (var (field, expected) in fields)
            if (!TryReadObjectStringList(payload, field, out var actual) || actual.Count != expected.Count
                || actual.Where((id, index) => bindings.Any(b => b.ObjectId == expected[index]) && id != expected[index]).Any())
                errors.Add($"{label} {field} differs from captured public battle history");
    }

    private static void ValidateSnapshotTimingResolutionHistoryListPayloadValues(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null)
        {
            return;
        }

        if (TryReadObjectList(view.Snapshot.Timing, "battlefieldResolutions", out var battlefieldResolutionPayloads))
        {
            foreach (var resolutionPayload in battlefieldResolutionPayloads)
            {
                if (!IsSnapshotPlayerPayloadObject(resolutionPayload))
                {
                    continue;
                }

                const string payloadLabel = "battlefield resolution item";
                ValidateSnapshotPayloadRequiredStringListPayloadShape(
                    resolutionPayload,
                    "participantObjectIds",
                    $"snapshot for {view.PlayerId} timing {payloadLabel}",
                    "participant object id",
                    errors);
                var participantObjectIds = ValidateSnapshotPayloadStringListValues(
                    resolutionPayload,
                    "participantObjectIds",
                    $"snapshot for {view.PlayerId} timing {payloadLabel}",
                    "participant object id",
                    errors);
                ValidateBattlefieldResolutionCombatParticipantAvailability(
                    ReadNormalizedPayloadString(resolutionPayload, "kind"),
                    ReadNormalizedPayloadString(resolutionPayload, "sourceObjectId"),
                    participantObjectIds,
                    $"snapshot for {view.PlayerId} timing {payloadLabel}",
                    "source object id",
                    "participant object id",
                    errors);
                ValidateBattlefieldResolutionControlParticipantAbsence(
                    ReadNormalizedPayloadString(resolutionPayload, "kind"),
                    ReadNormalizedPayloadString(resolutionPayload, "sourceObjectId"),
                    participantObjectIds,
                    $"snapshot for {view.PlayerId} timing {payloadLabel}",
                    "source object id",
                    "participant object id",
                    errors);
                ValidateSnapshotPayloadRequiredStringListPayloadShape(
                    resolutionPayload,
                    "relatedEventKinds",
                    $"snapshot for {view.PlayerId} timing {payloadLabel}",
                    "related event kind",
                    errors);
                var relatedEventKinds = ValidateSnapshotPayloadStringListValues(
                    resolutionPayload,
                    "relatedEventKinds",
                    $"snapshot for {view.PlayerId} timing {payloadLabel}",
                    "related event kind",
                    errors,
                    IsKnownBattlefieldResolutionRelatedEventKind);
                ValidateBattlefieldResolutionRelatedEventKindCompatibility(
                    ReadNormalizedPayloadString(resolutionPayload, "kind"),
                    relatedEventKinds,
                    $"snapshot for {view.PlayerId} timing {payloadLabel}",
                    errors);
            }
        }

        if (!TryReadObjectList(view.Snapshot.Timing, "battleResolutions", out var battleResolutionPayloads))
        {
            return;
        }

        foreach (var resolutionPayload in battleResolutionPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(resolutionPayload))
            {
                continue;
            }

            const string payloadLabel = "battle resolution item";
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                resolutionPayload,
                "attackerObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "attacker object id",
                errors);
            var attackerObjectIds = ValidateSnapshotPayloadStringListValues(
                resolutionPayload,
                "attackerObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "attacker object id",
                errors);
            ValidateBattleResolutionParticipantObjectListCount(
                attackerObjectIds,
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "attacker object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                resolutionPayload,
                "defenderObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "defender object id",
                errors);
            var defenderObjectIds = ValidateSnapshotPayloadStringListValues(
                resolutionPayload,
                "defenderObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "defender object id",
                errors);
            ValidateBattleResolutionParticipantObjectListCount(
                defenderObjectIds,
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "defender object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                resolutionPayload,
                "survivingAttackerObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "surviving attacker object id",
                errors);
            var survivingAttackerObjectIds = ValidateSnapshotPayloadStringListValues(
                resolutionPayload,
                "survivingAttackerObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "surviving attacker object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                resolutionPayload,
                "survivingDefenderObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "surviving defender object id",
                errors);
            var survivingDefenderObjectIds = ValidateSnapshotPayloadStringListValues(
                resolutionPayload,
                "survivingDefenderObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "surviving defender object id",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                resolutionPayload,
                "destroyedObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "destroyed object id",
                errors);
            var destroyedObjectIds = ValidateSnapshotPayloadStringListValues(
                resolutionPayload,
                "destroyedObjectIds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "destroyed object id",
                errors);
            ValidateBattleResolutionResultListObjectMembership(
                attackerObjectIds,
                defenderObjectIds,
                survivingAttackerObjectIds,
                survivingDefenderObjectIds,
                destroyedObjectIds,
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                errors);
            ValidateBattleResolutionNoResultSurvivorReasonCompatibility(
                ReadNormalizedPayloadString(resolutionPayload, "kind"),
                ReadNormalizedPayloadString(resolutionPayload, "reason"),
                survivingAttackerObjectIds,
                survivingDefenderObjectIds,
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                errors);
            ValidateBattleResolutionClosedWinnerSurvivorCompatibility(
                ReadNormalizedPayloadString(resolutionPayload, "kind"),
                ReadNormalizedPayloadString(resolutionPayload, "attackingPlayerId"),
                ReadNormalizedPayloadString(resolutionPayload, "defendingPlayerId"),
                ReadNormalizedPayloadString(resolutionPayload, "winnerPlayerId"),
                survivingAttackerObjectIds,
                survivingDefenderObjectIds,
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                resolutionPayload,
                "relatedEventKinds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "related event kind",
                errors);
            var relatedEventKinds = ValidateSnapshotPayloadStringListValues(
                resolutionPayload,
                "relatedEventKinds",
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                "related event kind",
                errors,
                IsKnownBattleResolutionRelatedEventKind);
            ValidateBattleResolutionRelatedEventKindCompatibility(
                ReadNormalizedPayloadString(resolutionPayload, "kind"),
                ReadNormalizedPayloadString(resolutionPayload, "reason"),
                relatedEventKinds,
                $"snapshot for {view.PlayerId} timing {payloadLabel}",
                errors);
        }
    }

    private static bool IsKnownBattlefieldResolutionRelatedEventKind(string value)
    {
        return string.Equals(value, "BATTLEFIELD_HELD", StringComparison.Ordinal)
            || string.Equals(value, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            || string.Equals(value, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal);
    }

    private static void ValidateRetainedResolutionHistoryListCount(
        int count,
        string listLabel,
        List<string> errors)
    {
        if (count <= MaxRetainedResolutionHistoryItems)
        {
            return;
        }

        errors.Add(
            $"{listLabel} list has {count} items, maximum is {MaxRetainedResolutionHistoryItems}");
    }

    private static void ValidateRetainedResolutionHistoryPayloadTickOrder(
        IReadOnlyList<object?> resolutionPayloads,
        string listLabel,
        string itemLabel,
        List<string> errors)
    {
        long? previousTick = null;
        foreach (var resolutionPayload in resolutionPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(resolutionPayload)
                || !TryReadObjectLong(resolutionPayload, "tick", out var tick)
                || tick < 0)
            {
                continue;
            }

            if (previousTick is long previous && tick > previous)
            {
                var resolutionId = ReadNormalizedPayloadString(resolutionPayload, "resolutionId");
                var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
                errors.Add(
                    $"{listLabel} ticks must be retained newest-first: {itemLabel} resolution id {diagnosticResolutionId} tick {tick} appears after earlier tick {previous}");
            }

            previousTick = tick;
        }
    }

    private static bool IsKnownBattlefieldResolutionKind(string value)
    {
        return string.Equals(value, "HELD", StringComparison.Ordinal)
            || string.Equals(value, "CONQUERED", StringComparison.Ordinal)
            || string.Equals(value, "CONTROL_RESOLVED", StringComparison.Ordinal);
    }

    private static bool IsKnownBattlefieldResolutionReason(string value)
    {
        return IsKnownBattlefieldResolutionRelatedEventKind(value)
            || string.Equals(value, "UNCONTROLLED", StringComparison.Ordinal)
            || string.Equals(value, "CONTROL_CHANGED", StringComparison.Ordinal)
            || string.Equals(value, "CONTROL_CONFIRMED", StringComparison.Ordinal);
    }

    private static void ValidateBattlefieldResolutionRelatedEventKindCompatibility(
        string? kind,
        IReadOnlyList<string>? relatedEventKinds,
        string payloadLabel,
        List<string> errors)
    {
        if (kind is null
            || relatedEventKinds is null
            || !IsKnownBattlefieldResolutionKind(kind)
            || relatedEventKinds.Any(relatedEventKind => !IsKnownBattlefieldResolutionRelatedEventKind(relatedEventKind)))
        {
            return;
        }

        var expectedRelatedEventKind = kind switch
        {
            "HELD" => "BATTLEFIELD_HELD",
            "CONQUERED" => "BATTLEFIELD_CONQUERED",
            "CONTROL_RESOLVED" => "BATTLEFIELD_CONTROL_RESOLVED",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(expectedRelatedEventKind))
        {
            return;
        }

        foreach (var relatedEventKind in relatedEventKinds)
        {
            if (!string.Equals(relatedEventKind, expectedRelatedEventKind, StringComparison.Ordinal))
            {
                errors.Add($"{payloadLabel} related event kind {relatedEventKind} is invalid for kind {kind}");
            }
        }

        if (!relatedEventKinds.Contains(expectedRelatedEventKind, StringComparer.Ordinal))
        {
            errors.Add($"{payloadLabel} related event kinds must include {expectedRelatedEventKind} for kind {kind}");
        }
    }

    private static void ValidateBattlefieldResolutionKindReasonCompatibility(
        string? kind,
        string? reason,
        string payloadLabel,
        List<string> errors)
    {
        if (kind is null
            || reason is null
            || !IsKnownBattlefieldResolutionKind(kind)
            || !IsKnownBattlefieldResolutionReason(reason)
            || IsBattlefieldResolutionReasonValidForKind(kind, reason))
        {
            return;
        }

        errors.Add($"{payloadLabel} reason {reason} is invalid for kind {kind}");
    }

    private static void ValidateBattlefieldResolutionCombatParticipantAvailability(
        string? kind,
        string? sourceObjectId,
        IReadOnlyList<string>? participantObjectIds,
        string payloadLabel,
        string sourceObjectLabel,
        string participantObjectLabel,
        List<string> errors)
    {
        if (!IsCombatDerivedBattlefieldResolutionKind(kind))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sourceObjectId))
        {
            errors.Add($"{payloadLabel} {sourceObjectLabel} is required for kind {kind}");
        }

        if (participantObjectIds is null)
        {
            return;
        }

        if (participantObjectIds.Count == 0)
        {
            errors.Add($"{payloadLabel} {participantObjectLabel} list must not be empty for kind {kind}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId)
            && !participantObjectIds.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {sourceObjectLabel} {sourceObjectId} must be present in {participantObjectLabel}s for kind {kind}");
        }
    }

    private static bool IsCombatDerivedBattlefieldResolutionKind(string? kind)
    {
        return string.Equals(kind, "HELD", StringComparison.Ordinal)
            || string.Equals(kind, "CONQUERED", StringComparison.Ordinal);
    }

    private static void ValidateBattlefieldResolutionControlParticipantAbsence(
        string? kind,
        string? sourceObjectId,
        IReadOnlyList<string>? participantObjectIds,
        string payloadLabel,
        string sourceObjectLabel,
        string participantObjectLabel,
        List<string> errors)
    {
        if (!string.Equals(kind, "CONTROL_RESOLVED", StringComparison.Ordinal))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sourceObjectId))
        {
            errors.Add($"{payloadLabel} {sourceObjectLabel} must be absent for kind {kind}");
        }

        if (participantObjectIds is not null && participantObjectIds.Count > 0)
        {
            errors.Add($"{payloadLabel} {participantObjectLabel} list must be empty for kind {kind}");
        }
    }

    private static void ValidateBattlefieldResolutionCombatPlayerAvailability(
        string? kind,
        string? playerId,
        string payloadLabel,
        string playerLabel,
        List<string> errors)
    {
        if (!IsCombatDerivedBattlefieldResolutionKind(kind))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"{payloadLabel} {playerLabel} is required for kind {kind}");
        }
    }

    private static void ValidateBattlefieldResolutionCombatControllerAbsence(
        string? kind,
        string? previousControllerId,
        string? controllerId,
        string payloadLabel,
        string previousControllerLabel,
        string controllerLabel,
        List<string> errors)
    {
        if (!IsCombatDerivedBattlefieldResolutionKind(kind))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(previousControllerId))
        {
            errors.Add($"{payloadLabel} {previousControllerLabel} must be absent for kind {kind}");
        }

        if (!string.IsNullOrWhiteSpace(controllerId))
        {
            errors.Add($"{payloadLabel} {controllerLabel} must be absent for kind {kind}");
        }
    }

    private static void ValidateBattlefieldResolutionControlControllerCompatibility(
        string? kind,
        string? reason,
        string? playerId,
        string? controllerId,
        string payloadLabel,
        string playerLabel,
        string controllerLabel,
        List<string> errors)
    {
        if (!string.Equals(kind, "CONTROL_RESOLVED", StringComparison.Ordinal)
            || reason is null)
        {
            return;
        }

        var normalizedPlayerId = string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
        var normalizedControllerId = string.IsNullOrWhiteSpace(controllerId) ? null : controllerId.Trim();
        if (string.Equals(reason, "UNCONTROLLED", StringComparison.Ordinal))
        {
            if (normalizedPlayerId is not null)
            {
                errors.Add($"{payloadLabel} {playerLabel} must be absent for reason {reason}");
            }

            if (normalizedControllerId is not null)
            {
                errors.Add($"{payloadLabel} {controllerLabel} must be absent for reason {reason}");
            }

            return;
        }

        if (!string.Equals(reason, "CONTROL_CHANGED", StringComparison.Ordinal)
            && !string.Equals(reason, "CONTROL_CONFIRMED", StringComparison.Ordinal))
        {
            return;
        }

        if (normalizedPlayerId is null)
        {
            errors.Add($"{payloadLabel} {playerLabel} is required for reason {reason}");
        }

        if (normalizedControllerId is null)
        {
            errors.Add($"{payloadLabel} {controllerLabel} is required for reason {reason}");
        }

        if (normalizedPlayerId is not null
            && normalizedControllerId is not null
            && !string.Equals(normalizedPlayerId, normalizedControllerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {playerLabel} {normalizedPlayerId} must match {controllerLabel} {normalizedControllerId} for reason {reason}");
        }
    }

    private static void ValidateBattlefieldResolutionControlPreviousControllerCompatibility(
        string? kind,
        string? reason,
        string? previousControllerId,
        string? controllerId,
        string payloadLabel,
        string previousControllerLabel,
        string controllerLabel,
        List<string> errors)
    {
        if (!string.Equals(kind, "CONTROL_RESOLVED", StringComparison.Ordinal)
            || reason is null)
        {
            return;
        }

        var normalizedPreviousControllerId = string.IsNullOrWhiteSpace(previousControllerId)
            ? null
            : previousControllerId.Trim();
        var normalizedControllerId = string.IsNullOrWhiteSpace(controllerId) ? null : controllerId.Trim();
        if (string.Equals(reason, "CONTROL_CONFIRMED", StringComparison.Ordinal))
        {
            if (normalizedPreviousControllerId is null)
            {
                errors.Add($"{payloadLabel} {previousControllerLabel} is required for reason {reason}");
            }

            if (normalizedPreviousControllerId is not null
                && normalizedControllerId is not null
                && !string.Equals(normalizedPreviousControllerId, normalizedControllerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} {previousControllerLabel} {normalizedPreviousControllerId} must match {controllerLabel} {normalizedControllerId} for reason {reason}");
            }

            return;
        }

        if (string.Equals(reason, "CONTROL_CHANGED", StringComparison.Ordinal)
            && normalizedPreviousControllerId is not null
            && normalizedControllerId is not null
            && string.Equals(normalizedPreviousControllerId, normalizedControllerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {previousControllerLabel} {normalizedPreviousControllerId} must differ from {controllerLabel} {normalizedControllerId} for reason {reason}");
        }
    }

    private static bool IsBattlefieldResolutionReasonValidForKind(string kind, string reason)
    {
        return kind switch
        {
            "HELD" => string.Equals(reason, "BATTLEFIELD_HELD", StringComparison.Ordinal),
            "CONQUERED" => string.Equals(reason, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal),
            "CONTROL_RESOLVED" => string.Equals(reason, "UNCONTROLLED", StringComparison.Ordinal)
                || string.Equals(reason, "CONTROL_CHANGED", StringComparison.Ordinal)
                || string.Equals(reason, "CONTROL_CONFIRMED", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsKnownBattleResolutionRelatedEventKind(string value)
    {
        return string.Equals(value, "DAMAGE_APPLIED", StringComparison.Ordinal)
            || string.Equals(value, "UNIT_DESTROYED", StringComparison.Ordinal)
            || string.Equals(value, "DAMAGE_REMOVED", StringComparison.Ordinal)
            || string.Equals(value, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal)
            || string.Equals(value, "BATTLE_CLOSED", StringComparison.Ordinal)
            || string.Equals(value, "BATTLE_NO_RESULT", StringComparison.Ordinal);
    }

    private static bool IsKnownBattleResolutionKind(string value)
    {
        return string.Equals(value, "CLOSED", StringComparison.Ordinal)
            || string.Equals(value, "NO_RESULT", StringComparison.Ordinal);
    }

    private static bool IsKnownBattleResolutionReason(string value)
    {
        return string.Equals(value, "BATTLE_CLOSED", StringComparison.Ordinal)
            || string.Equals(value, "BATTLE_NO_RESULT", StringComparison.Ordinal)
            || string.Equals(value, "ALL_PARTICIPANTS_DESTROYED", StringComparison.Ordinal)
            || string.Equals(value, CoreRuleEngine.CleanupRecallNoResultReason, StringComparison.Ordinal)
            || string.Equals(value, "BOTH_SIDES_RETAIN_UNITS", StringComparison.Ordinal);
    }

    private static void ValidateBattleResolutionRelatedEventKindCompatibility(
        string? kind,
        string? reason,
        IReadOnlyList<string>? relatedEventKinds,
        string payloadLabel,
        List<string> errors)
    {
        if (kind is null
            || reason is null
            || relatedEventKinds is null
            || !IsKnownBattleResolutionKind(kind)
            || !IsKnownBattleResolutionReason(reason)
            || !IsBattleResolutionReasonValidForKind(kind, reason)
            || relatedEventKinds.Any(relatedEventKind => !IsKnownBattleResolutionRelatedEventKind(relatedEventKind)))
        {
            return;
        }

        var expectedResultEventKind = string.Equals(kind, "NO_RESULT", StringComparison.Ordinal)
            ? "BATTLE_NO_RESULT"
            : "BATTLE_CLOSED";
        if (!relatedEventKinds.Contains(expectedResultEventKind, StringComparer.Ordinal))
        {
            errors.Add($"{payloadLabel} related event kinds must include {expectedResultEventKind} for kind {kind} reason {reason}");
        }

        if (string.Equals(kind, "NO_RESULT", StringComparison.Ordinal)
            && !relatedEventKinds.Contains("BATTLE_CLOSED", StringComparer.Ordinal))
        {
            errors.Add($"{payloadLabel} related event kinds must include BATTLE_CLOSED for kind {kind} reason {reason}");
        }

        if (string.Equals(kind, "CLOSED", StringComparison.Ordinal)
            && relatedEventKinds.Contains("BATTLE_NO_RESULT", StringComparer.Ordinal))
        {
            errors.Add($"{payloadLabel} related event kind BATTLE_NO_RESULT is invalid for kind {kind} reason {reason}");
        }
    }

    private static void ValidateBattleResolutionKindReasonCompatibility(
        string? kind,
        string? reason,
        string payloadLabel,
        List<string> errors)
    {
        if (kind is null
            || reason is null
            || !IsKnownBattleResolutionKind(kind)
            || !IsKnownBattleResolutionReason(reason)
            || IsBattleResolutionReasonValidForKind(kind, reason))
        {
            return;
        }

        errors.Add($"{payloadLabel} reason {reason} is invalid for kind {kind}");
    }

    private static bool IsBattleResolutionReasonValidForKind(string kind, string reason)
    {
        return kind switch
        {
            "CLOSED" => string.Equals(reason, "BATTLE_CLOSED", StringComparison.Ordinal),
            "NO_RESULT" => string.Equals(reason, "ALL_PARTICIPANTS_DESTROYED", StringComparison.Ordinal)
                || string.Equals(reason, CoreRuleEngine.CleanupRecallNoResultReason, StringComparison.Ordinal)
                || string.Equals(reason, "BOTH_SIDES_RETAIN_UNITS", StringComparison.Ordinal),
            _ => false
        };
    }

    private static void ValidateBattleResolutionPlayerRoleCompatibility(
        string? kind,
        string? attackingPlayerId,
        string? defendingPlayerId,
        string? winnerPlayerId,
        string payloadLabel,
        List<string> errors)
    {
        if (kind is null || !IsKnownBattleResolutionKind(kind))
        {
            return;
        }

        var normalizedAttackingPlayerId = NormalizeOptionalCompatibilityText(attackingPlayerId);
        var normalizedDefendingPlayerId = NormalizeOptionalCompatibilityText(defendingPlayerId);
        var normalizedWinnerPlayerId = NormalizeOptionalCompatibilityText(winnerPlayerId);

        if (normalizedDefendingPlayerId is null)
        {
            errors.Add($"{payloadLabel} defending player id is required");
        }

        if (normalizedAttackingPlayerId is not null
            && normalizedDefendingPlayerId is not null
            && string.Equals(normalizedAttackingPlayerId, normalizedDefendingPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"{payloadLabel} attacking player id {normalizedAttackingPlayerId} also appears as defending player id");
        }

        if (string.Equals(kind, "NO_RESULT", StringComparison.Ordinal))
        {
            if (normalizedWinnerPlayerId is not null)
            {
                errors.Add($"{payloadLabel} winner player id {normalizedWinnerPlayerId} is invalid for kind {kind}");
            }

            return;
        }

        if (!string.Equals(kind, "CLOSED", StringComparison.Ordinal))
        {
            return;
        }

        if (normalizedWinnerPlayerId is null)
        {
            errors.Add($"{payloadLabel} winner player id is required for kind {kind}");
            return;
        }

        if (!string.Equals(normalizedWinnerPlayerId, normalizedAttackingPlayerId, StringComparison.Ordinal)
            && !string.Equals(normalizedWinnerPlayerId, normalizedDefendingPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"{payloadLabel} winner player id {normalizedWinnerPlayerId} is missing from attacking or defending player ids");
        }
    }

    private static string? NormalizeOptionalCompatibilityText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateBattleResolutionParticipantObjectListCount(
        IReadOnlyList<string>? objectIds,
        string payloadLabel,
        string objectLabel,
        List<string> errors)
    {
        if (objectIds is null)
        {
            return;
        }

        if (objectIds.Count == 0)
        {
            errors.Add($"{payloadLabel} {objectLabel} list must not be empty");
        }

    }

    private static void ValidateBattleResolutionResultListObjectMembership(
        IReadOnlyList<string>? attackerObjectIds,
        IReadOnlyList<string>? defenderObjectIds,
        IReadOnlyList<string>? survivingAttackerObjectIds,
        IReadOnlyList<string>? survivingDefenderObjectIds,
        IReadOnlyList<string>? destroyedObjectIds,
        string payloadLabel,
        List<string> errors)
    {
        if (attackerObjectIds is null
            || defenderObjectIds is null
            || survivingAttackerObjectIds is null
            || survivingDefenderObjectIds is null
            || destroyedObjectIds is null)
        {
            return;
        }

        var attackerObjectIdSet = attackerObjectIds.ToHashSet(StringComparer.Ordinal);
        var defenderObjectIdSet = defenderObjectIds.ToHashSet(StringComparer.Ordinal);
        foreach (var attackerObjectId in attackerObjectIds)
        {
            if (defenderObjectIdSet.Contains(attackerObjectId))
            {
                errors.Add($"{payloadLabel} attacker object id {attackerObjectId} also appears in defender object ids");
            }
        }

        foreach (var survivingAttackerObjectId in survivingAttackerObjectIds)
        {
            if (!attackerObjectIdSet.Contains(survivingAttackerObjectId))
            {
                errors.Add($"{payloadLabel} surviving attacker object id {survivingAttackerObjectId} is missing from attacker object ids");
            }
        }

        foreach (var survivingDefenderObjectId in survivingDefenderObjectIds)
        {
            if (!defenderObjectIdSet.Contains(survivingDefenderObjectId))
            {
                errors.Add($"{payloadLabel} surviving defender object id {survivingDefenderObjectId} is missing from defender object ids");
            }
        }

        var survivingAttackerObjectIdSet = survivingAttackerObjectIds.ToHashSet(StringComparer.Ordinal);
        var survivingDefenderObjectIdSet = survivingDefenderObjectIds.ToHashSet(StringComparer.Ordinal);
        foreach (var destroyedObjectId in destroyedObjectIds)
        {
            if (!attackerObjectIdSet.Contains(destroyedObjectId) && !defenderObjectIdSet.Contains(destroyedObjectId))
            {
                errors.Add($"{payloadLabel} destroyed object id {destroyedObjectId} is missing from attacker or defender object ids");
            }

            if (survivingAttackerObjectIdSet.Contains(destroyedObjectId))
            {
                errors.Add($"{payloadLabel} destroyed object id {destroyedObjectId} also appears in surviving attacker object ids");
            }

            if (survivingDefenderObjectIdSet.Contains(destroyedObjectId))
            {
                errors.Add($"{payloadLabel} destroyed object id {destroyedObjectId} also appears in surviving defender object ids");
            }
        }
    }

    private static void ValidateBattleResolutionNoResultSurvivorReasonCompatibility(
        string? kind,
        string? reason,
        IReadOnlyList<string>? survivingAttackerObjectIds,
        IReadOnlyList<string>? survivingDefenderObjectIds,
        string payloadLabel,
        List<string> errors)
    {
        if (!string.Equals(kind, "NO_RESULT", StringComparison.Ordinal)
            || reason is null
            || !IsKnownBattleResolutionReason(reason)
            || !IsBattleResolutionReasonValidForKind("NO_RESULT", reason)
            || survivingAttackerObjectIds is null
            || survivingDefenderObjectIds is null)
        {
            return;
        }

        // This cause is independently bound to accepted cleanup/result/close events
        // by normal recovery; component diagnostics cannot authorize that history.
        if (string.Equals(reason, CoreRuleEngine.CleanupRecallNoResultReason, StringComparison.Ordinal)) return;

        if (string.Equals(reason, "ALL_PARTICIPANTS_DESTROYED", StringComparison.Ordinal))
        {
            if (survivingAttackerObjectIds.Count > 0)
            {
                errors.Add($"{payloadLabel} surviving attacker object ids must be empty for kind {kind} reason {reason}");
            }

            if (survivingDefenderObjectIds.Count > 0)
            {
                errors.Add($"{payloadLabel} surviving defender object ids must be empty for kind {kind} reason {reason}");
            }

            return;
        }

        if (!string.Equals(reason, "BOTH_SIDES_RETAIN_UNITS", StringComparison.Ordinal))
        {
            return;
        }

        if (survivingAttackerObjectIds.Count == 0)
        {
            errors.Add($"{payloadLabel} surviving attacker object ids are required for kind {kind} reason {reason}");
        }

        if (survivingDefenderObjectIds.Count == 0)
        {
            errors.Add($"{payloadLabel} surviving defender object ids are required for kind {kind} reason {reason}");
        }
    }

    private static void ValidateBattleResolutionClosedWinnerSurvivorCompatibility(
        string? kind,
        string? attackingPlayerId,
        string? defendingPlayerId,
        string? winnerPlayerId,
        IReadOnlyList<string>? survivingAttackerObjectIds,
        IReadOnlyList<string>? survivingDefenderObjectIds,
        string payloadLabel,
        List<string> errors)
    {
        if (!string.Equals(kind, "CLOSED", StringComparison.Ordinal)
            || survivingAttackerObjectIds is null
            || survivingDefenderObjectIds is null)
        {
            return;
        }

        var normalizedAttackingPlayerId = NormalizeOptionalCompatibilityText(attackingPlayerId);
        var normalizedDefendingPlayerId = NormalizeOptionalCompatibilityText(defendingPlayerId);
        var normalizedWinnerPlayerId = NormalizeOptionalCompatibilityText(winnerPlayerId);
        if (normalizedWinnerPlayerId is null)
        {
            return;
        }

        if (string.Equals(normalizedWinnerPlayerId, normalizedAttackingPlayerId, StringComparison.Ordinal))
        {
            if (survivingAttackerObjectIds.Count == 0)
            {
                errors.Add($"{payloadLabel} surviving attacker object ids are required for winner player id {normalizedWinnerPlayerId} and kind {kind}");
            }

            if (survivingDefenderObjectIds.Count > 0)
            {
                errors.Add($"{payloadLabel} surviving defender object ids must be empty for winner player id {normalizedWinnerPlayerId} and kind {kind}");
            }

            return;
        }

        if (!string.Equals(normalizedWinnerPlayerId, normalizedDefendingPlayerId, StringComparison.Ordinal))
        {
            return;
        }

        if (survivingDefenderObjectIds.Count == 0)
        {
            errors.Add($"{payloadLabel} surviving defender object ids are required for winner player id {normalizedWinnerPlayerId} and kind {kind}");
        }

        if (survivingAttackerObjectIds.Count > 0)
        {
            errors.Add($"{payloadLabel} surviving attacker object ids must be empty for winner player id {normalizedWinnerPlayerId} and kind {kind}");
        }
    }

    private static bool IsKnownTriggerSourceVisibility(string value)
    {
        return KnownTriggerSourceVisibilities.Contains(value, StringComparer.Ordinal);
    }

    private static string? ValidateTriggerQueueSourceVisibilityValue(
        object? payload,
        string payloadLabel,
        List<string> errors)
    {
        return ValidateSnapshotPayloadRequiredStringValue(
            payload,
            "sourceVisibility",
            payloadLabel,
            "source visibility",
            errors,
            IsKnownTriggerSourceVisibility,
            KnownTriggerSourceVisibilities);
    }

    private static void ValidateTriggerQueueSourceVisibilityMustBeVisible(
        string payloadLabel,
        string diagnosticName,
        string? sourceVisibility,
        List<string> errors)
    {
        if (sourceVisibility is null
            || string.Equals(sourceVisibility, "VISIBLE", StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(
            $"{payloadLabel} {diagnosticName} source visibility must be VISIBLE; {FormatExpectedActualForRecovery("VISIBLE", sourceVisibility)}");
    }

    private static bool IsKnownTriggerQueueTriggeredEventKind(string value)
    {
        return KnownTriggerQueueTriggeredEventKinds.Contains(value, StringComparer.Ordinal)
            || AdditionalTriggerQueueTriggeredEventKinds.Contains(value, StringComparer.Ordinal);
    }

    private static bool IsKnownRuleQueueCoverageKey(string value)
    {
        return KnownRuleQueueCoverageKeys.Contains(value, StringComparer.Ordinal);
    }

    private static bool IsKnownRuleQueueCoverageEvidenceKey(string value)
    {
        return string.Equals(value, "stack", StringComparison.Ordinal)
            || string.Equals(value, "triggerQueue", StringComparison.Ordinal)
            || string.Equals(value, "battle", StringComparison.Ordinal)
            || string.Equals(value, "battlefieldTasks", StringComparison.Ordinal)
            || string.Equals(value, "pendingTaskQueue", StringComparison.Ordinal)
            || string.Equals(value, "battlefieldResolutions", StringComparison.Ordinal)
            || string.Equals(value, "battleResolutions", StringComparison.Ordinal)
            || string.Equals(value, "turnWindow", StringComparison.Ordinal)
            || string.Equals(value, "pendingPayment", StringComparison.Ordinal);
    }

    private static bool IsKnownContinuousEffectScope(string value)
    {
        return string.Equals(value, "GLOBAL", StringComparison.Ordinal)
            || string.Equals(value, "OBJECT", StringComparison.Ordinal)
            || string.Equals(value, "BATTLEFIELD", StringComparison.Ordinal);
    }
}
