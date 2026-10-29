using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateSpectatorReplayFrame(
        string roomId,
        long lastEventSequence,
        long? currentTick,
        MatchState? authoritativeState,
        MatchReplayFrame? spectatorReplayFrame,
        List<string> errors)
    {
        if (spectatorReplayFrame is null)
        {
            return;
        }

        if (authoritativeState is null)
        {
            errors.Add("spectator replay frame requires authoritative state");
            return;
        }

        if (!string.Equals(spectatorReplayFrame.RoomId, roomId, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame room {spectatorReplayFrame.RoomId} does not match recovery room {roomId}");
        }

        if (spectatorReplayFrame.EventSequence != lastEventSequence)
        {
            errors.Add(
                $"spectator replay frame event sequence {spectatorReplayFrame.EventSequence} does not match recovery sequence {lastEventSequence}");
        }

        if (spectatorReplayFrame.Tick != authoritativeState.Tick)
        {
            errors.Add(
                $"spectator replay frame tick {spectatorReplayFrame.Tick} does not match authoritative state tick {authoritativeState.Tick}");
        }

        if (currentTick is { } expectedTick && spectatorReplayFrame.Tick != expectedTick)
        {
            errors.Add($"spectator replay frame tick {spectatorReplayFrame.Tick} does not match recovery tick {expectedTick}");
        }

        var expectedHash = MatchStateHasher.Hash(authoritativeState);
        if (!string.Equals(spectatorReplayFrame.AuthoritativeStateHash, expectedHash, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame hash does not match authoritative state hash");
        }

        authoritativeState = CoreRuleEngine.VisibleRuleChoiceState(authoritativeState);

        if (spectatorReplayFrame.SpectatorSnapshot is null)
        {
            errors.Add("spectator replay frame snapshot is required");
            return;
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Timing is null)
        {
            errors.Add("spectator replay frame snapshot timing is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            spectatorReplayFrame.SpectatorSnapshot.Timing,
            "spectator replay frame timing",
            errors);
        ValidateTimingOrderTriggerPromptFieldAbsence(
            spectatorReplayFrame.SpectatorSnapshot.Timing,
            "spectator replay frame timing",
            errors);

        ValidateSpectatorSnapshotCoreNumericScalarValues(
            spectatorReplayFrame.SpectatorSnapshot,
            errors);
        var spectatorSnapshotTurnState = ValidateSpectatorSnapshotRequiredStringScalarValue(
            spectatorReplayFrame.SpectatorSnapshot.TurnState,
            "turn state",
            errors,
            IsKnownTimingState);
        var spectatorSnapshotActivePlayerId = ValidateSpectatorSnapshotRequiredStringScalarValue(
            spectatorReplayFrame.SpectatorSnapshot.ActivePlayerId,
            "active player",
            errors);

        if (spectatorReplayFrame.SpectatorSnapshot.Players is null)
        {
            errors.Add("spectator replay frame snapshot players are required");
        }
        else
        {
            ValidateSpectatorSnapshotPlayerPayloads(
                spectatorReplayFrame.SpectatorSnapshot,
                authoritativeState,
                errors);
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Lanes is null)
        {
            errors.Add("spectator replay frame snapshot lanes are required");
        }
        else
        {
            ValidateSpectatorSnapshotLanePayloads(
                spectatorReplayFrame.SpectatorSnapshot,
                authoritativeState,
                errors);
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Stack is null)
        {
            errors.Add("spectator replay frame snapshot stack is required");
        }
        else
        {
            ValidateDamageMarkerViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateBeginningDrawViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateDynamicPowerAuraViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateTurnUnitPlayBoonViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors);
            ValidateEmpoweredViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, null, errors, "spectator replay frame");
            ValidateBeginningRecycleViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateBeginningRuneViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateEphemeralStartViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateGrantedTagViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateBattlefieldStartViews(spectatorReplayFrame.SpectatorSnapshot, authoritativeState, errors, "spectator replay frame");
            ValidateSpectatorSnapshotStackItemPayloads(
                spectatorReplayFrame.SpectatorSnapshot,
                authoritativeState,
                errors);
            ValidateSpectatorSnapshotStackPlayerReferences(
                spectatorReplayFrame.SpectatorSnapshot.Stack,
                BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys),
                errors);
            ValidateSpectatorSnapshotStackObjectReferences(
                spectatorReplayFrame.SpectatorSnapshot.Stack,
                BuildAuthoritativeStateKnownObjectIds(authoritativeState),
                authoritativeState,
                errors);
            ValidateSpectatorSnapshotStackAuthoritativeKeySet(
                spectatorReplayFrame.SpectatorSnapshot.Stack,
                authoritativeState.StackItems,
                errors);
            ValidateSpectatorSnapshotStackKeyedAuthoritativeValues(
                spectatorReplayFrame.SpectatorSnapshot.Stack,
                authoritativeState,
                errors);

            if (spectatorReplayFrame.SpectatorSnapshot.Stack.Count != authoritativeState.StackItems.Count)
            {
                errors.Add(
                    $"spectator replay frame snapshot stack count {spectatorReplayFrame.SpectatorSnapshot.Stack.Count} does not match authoritative state stack count {authoritativeState.StackItems.Count}");
            }

            var spectatorStackItemIds = ExtractStackItemIds(spectatorReplayFrame.SpectatorSnapshot);
            var authoritativeStackItemIds = authoritativeState.StackItems
                .Select(item => item.StackItemId)
                .ToArray();
            if (!StringListsEqual(spectatorStackItemIds, authoritativeStackItemIds))
            {
                errors.Add(
                    $"spectator replay frame snapshot stack item ids disagree with authoritative state stack item ids; {FormatExpectedActualForRecovery(authoritativeStackItemIds, spectatorStackItemIds)}");
            }

            var spectatorStackControllerIds = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "controllerId");
            var authoritativeStackControllerIds = authoritativeState.StackItems
                .Select(item => item.ControllerId)
                .ToArray();
            if (!StringListsEqual(spectatorStackControllerIds, authoritativeStackControllerIds))
            {
                errors.Add("spectator replay frame snapshot stack controller ids disagree with authoritative state stack controller ids");
            }

            var spectatorStackSourceObjectIds = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "sourceObjectId");
            var authoritativeStackSourceObjectIds = authoritativeState.StackItems
                .Select(item => CapturedBattleSourceIsPrivate(authoritativeState, item) ? "HIDDEN" : item.SourceObjectId)
                .ToArray();
            if (!StringListsEqual(spectatorStackSourceObjectIds, authoritativeStackSourceObjectIds))
            {
                errors.Add("spectator replay frame snapshot stack source object ids disagree with authoritative state stack source object ids");
            }

            var spectatorStackEffectKinds = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "effectKind");
            var authoritativeStackEffectKinds = authoritativeState.StackItems
                .Select(item => item.EffectKind)
                .ToArray();
            if (!StringListsEqual(spectatorStackEffectKinds, authoritativeStackEffectKinds))
            {
                errors.Add("spectator replay frame snapshot stack effect kinds disagree with authoritative state stack effect kinds");
            }

            var spectatorStackCardNumbers = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "cardNo");
            var authoritativeStackCardNumbers = authoritativeState.StackItems
                .Select(item => item.CardNo)
                .ToArray();
            if (!StringListsEqual(spectatorStackCardNumbers, authoritativeStackCardNumbers))
            {
                errors.Add("spectator replay frame snapshot stack card numbers disagree with authoritative state stack card numbers");
            }

            var spectatorStackTargetObjectIds = ExtractStackItemStringListValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "targetObjectIds");
            var authoritativeStackTargetObjectIds = VisibleStackTargetObjectIdsForSpectator(authoritativeState);
            if (!StringListCollectionsEqual(spectatorStackTargetObjectIds, authoritativeStackTargetObjectIds))
            {
                errors.Add("spectator replay frame snapshot stack target object ids disagree with authoritative state stack target object ids");
            }

            var spectatorStackDamageAmounts = ExtractStackItemIntValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "damageAmount");
            var authoritativeStackDamageAmounts = authoritativeState.StackItems
                .Select(item => item.DamageAmount)
                .ToArray();
            if (!IntListsEqual(spectatorStackDamageAmounts, authoritativeStackDamageAmounts))
            {
                errors.Add("spectator replay frame snapshot stack damage amounts disagree with authoritative state stack damage amounts");
            }

            var spectatorStackDestinations = ExtractStackItemOptionalStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "destination");
            var authoritativeStackDestinations = authoritativeState.StackItems
                .Select(item => item.Destination)
                .ToArray();
            if (!StringListsEqual(spectatorStackDestinations, authoritativeStackDestinations))
            {
                errors.Add("spectator replay frame snapshot stack destinations disagree with authoritative state stack destinations");
            }
        }

        if (spectatorSnapshotTurnState is not null
            && !string.Equals(
                spectatorSnapshotTurnState,
                authoritativeState.TimingState,
                StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame snapshot turn state does not match authoritative state timing state");
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Tick != spectatorReplayFrame.Tick)
        {
            errors.Add(
                $"spectator replay frame snapshot tick {spectatorReplayFrame.SpectatorSnapshot.Tick} does not match frame tick {spectatorReplayFrame.Tick}");
        }

        if (spectatorReplayFrame.SpectatorSnapshot.TurnNumber != authoritativeState.TurnNumber)
        {
            errors.Add(
                $"spectator replay frame snapshot turn number {spectatorReplayFrame.SpectatorSnapshot.TurnNumber} does not match authoritative state turn number {authoritativeState.TurnNumber}");
        }

        if (spectatorSnapshotActivePlayerId is not null
            && !string.Equals(
                spectatorSnapshotActivePlayerId,
                authoritativeState.ActivePlayerId,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot active player {spectatorSnapshotActivePlayerId} does not match authoritative state active player {authoritativeState.ActivePlayerId}");
        }

        var spectatorSeats = ExtractSeats(spectatorReplayFrame.SpectatorSnapshot);
        if (!SeatsEqual(authoritativeState.Seats, spectatorSeats))
        {
            errors.Add("spectator replay frame snapshot seats disagree with authoritative state seats");
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Timing.ContainsKey("temporaryPaymentResources"))
            errors.Add("spectator replay frame contains obsolete temporary payment resources");

        ValidateSpectatorCoreTimingPayloadValues(
            spectatorReplayFrame.SpectatorSnapshot.Timing,
            errors);

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "phase",
                out var spectatorPhase)
            || !string.Equals(spectatorPhase, authoritativeState.Phase, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing phase does not match authoritative state phase");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "timingState",
                out var spectatorTimingState)
            || !string.Equals(spectatorTimingState, authoritativeState.TimingState, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing state does not match authoritative state timing state");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "turnPlayerId",
                out var spectatorTurnPlayerId)
            || !string.Equals(spectatorTurnPlayerId, authoritativeState.TurnPlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing turn player does not match authoritative state turn player");
        }

        ValidateSpectatorPriorityTimingPayloadValues(
            spectatorReplayFrame.SpectatorSnapshot.Timing,
            errors);
        ValidateSpectatorFocusTimingPayloadValues(
            spectatorReplayFrame.SpectatorSnapshot.Timing,
            errors);

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "priorityPlayerId",
                out var spectatorPriorityPlayerId)
            || !string.Equals(spectatorPriorityPlayerId, authoritativeState.PendingRuleChoice is null ? authoritativeState.PriorityPlayerId : null, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing priority player does not match authoritative state priority player");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "focusPlayerId",
                out var spectatorFocusPlayerId)
            || !string.Equals(spectatorFocusPlayerId, authoritativeState.PendingRuleChoice is null ? authoritativeState.FocusPlayerId : null, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing focus player does not match authoritative state focus player");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "winnerPlayerId",
                out var spectatorWinnerPlayerId)
            || !string.Equals(spectatorWinnerPlayerId, authoritativeState.WinnerPlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing winner player does not match authoritative state winner player");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "passedPriorityPlayerIds",
                out var spectatorPassedPriorityPlayerIds)
            || !StringListsEqual(spectatorPassedPriorityPlayerIds, authoritativeState.PassedPriorityPlayerIds))
        {
            errors.Add("spectator replay frame timing passed priority players do not match authoritative state passed priority players");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "passedFocusPlayerIds",
                out var spectatorPassedFocusPlayerIds)
            || !StringListsEqual(spectatorPassedFocusPlayerIds, authoritativeState.PassedFocusPlayerIds))
        {
            errors.Add("spectator replay frame timing passed focus players do not match authoritative state passed focus players");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "destroyedUnitOwnerIdsThisTurn",
                out var spectatorDestroyedUnitOwnerIdsThisTurn)
            || !StringListsEqual(spectatorDestroyedUnitOwnerIdsThisTurn, authoritativeState.DestroyedUnitOwnerIdsThisTurn))
        {
            errors.Add("spectator replay frame timing destroyed unit owners do not match authoritative state destroyed unit owners");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "roomStatus",
                out var spectatorRoomStatus)
            || !string.Equals(spectatorRoomStatus, authoritativeState.Status, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing room status does not match authoritative state room status");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "readyPlayerIds",
                out var spectatorReadyPlayerIds)
            || !StringListsEqual(spectatorReadyPlayerIds, authoritativeState.ReadyPlayerIds))
        {
            errors.Add("spectator replay frame timing ready players do not match authoritative state ready players");
        }

        if (!TryReadObjectInt(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "winningScore",
                out var spectatorWinningScore)
            || spectatorWinningScore != EffectiveWinningScoreForRecovery(authoritativeState))
        {
            errors.Add("spectator replay frame timing winning score does not match authoritative state winning score");
        }

        ValidateSpectatorPendingTaskQueuePayload(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorPendingPaymentPayload(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateObservedDisclosureSnapshot(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, "spectator replay frame", errors);
        ValidateSpectatorPendingHandChoicePayload(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorPendingCardChoicePayload(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorContinuousEffectPayloads(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorTriggerQueuePayloads(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorBattlefieldTaskPayloads(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorRuleQueueCoveragePayloads(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);

        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue("turnWindow", out var spectatorTurnWindow)
            || IsNullSnapshotPayloadValue(spectatorTurnWindow))
        {
            errors.Add("spectator replay frame timing turn window does not match authoritative state turn window");
        }
        else if (!IsSnapshotPlayerPayloadObject(spectatorTurnWindow))
        {
            errors.Add("spectator replay frame timing turn window payload is required");
        }
        else
        {
            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorTurnWindow,
                "spectator replay frame timing turn window",
                errors);
            ValidateSpectatorTurnWindowPayloadValues(
                spectatorTurnWindow,
                errors);

            if (!TurnWindowMatches(spectatorTurnWindow, authoritativeState.TurnWindow))
            {
                errors.Add("spectator replay frame timing turn window does not match authoritative state turn window");
            }
        }

        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue("spellDuel", out var spectatorSpellDuel)
            || IsNullSnapshotPayloadValue(spectatorSpellDuel))
        {
            errors.Add("spectator replay frame timing spell duel does not match authoritative state spell duel");
        }
        else if (!IsSnapshotPlayerPayloadObject(spectatorSpellDuel))
        {
            errors.Add("spectator replay frame timing spell duel payload is required");
        }
        else
        {
            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorSpellDuel,
                "spectator replay frame timing spell duel",
                errors);
            ValidateSpectatorSpellDuelPayloadValues(
                spectatorSpellDuel,
                errors);

            if (!SpellDuelMatches(spectatorSpellDuel, authoritativeState.SpellDuelState))
            {
                errors.Add("spectator replay frame timing spell duel does not match authoritative state spell duel");
            }
        }

        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue("battle", out var spectatorBattle)
            || IsNullSnapshotPayloadValue(spectatorBattle))
        {
            errors.Add("spectator replay frame timing battle does not match authoritative state battle");
        }
        else if (!IsSnapshotPlayerPayloadObject(spectatorBattle))
        {
            errors.Add("spectator replay frame timing battle payload is required");
        }
        else
        {
            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorBattle,
                "spectator replay frame timing battle",
                errors);
            ValidateSpectatorBattleParticipantControllerPayloadPropertyNames(
                spectatorBattle,
                errors);
            ValidateSpectatorBattlePayloadValues(
                spectatorBattle,
                authoritativeState,
                errors);
            ValidateSpectatorBattleDamageAssignmentPayloadValues(
                spectatorBattle,
                authoritativeState,
                errors);

            if (TryReadObjectValue(spectatorBattle, "damageAssignment", out var spectatorDamageAssignment)
                && IsSnapshotPlayerPayloadObject(spectatorDamageAssignment))
            {
                ValidateSnapshotPayloadObjectPropertyNames(
                    spectatorDamageAssignment,
                    "spectator replay frame timing battle damage assignment",
                    errors);
                ValidateSpectatorBattleDamageAssignmentMapPayloadPropertyNames(
                    spectatorDamageAssignment,
                    errors);
                ValidateSpectatorBattleDamageAssignmentRequiredAssignmentPayloadPropertyNames(
                    spectatorDamageAssignment,
                    errors);
            }

            if (!BattleMatches(spectatorBattle, authoritativeState, authoritativeState.BattleState))
            {
                errors.Add("spectator replay frame timing battle does not match authoritative state battle");
            }
        }

        var spectatorKnownPlayerIds = BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys);
        var spectatorKnownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState);
        var authoritativeBattlefieldResolutionCount = authoritativeState.BattlefieldResolutions.Count;
        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue(
                "battlefieldResolutions",
                out var battlefieldResolutionsPayload)
            || IsNullSnapshotPayloadValue(battlefieldResolutionsPayload))
        {
            errors.Add("spectator replay frame timing battlefield resolutions are required");
            if (authoritativeBattlefieldResolutionCount > 0)
            {
                errors.Add(
                    $"spectator replay frame timing battlefield resolution count 0 does not match authoritative state battlefield resolution count {authoritativeBattlefieldResolutionCount}");
            }
        }
        else if (!TryReadObjectListValue(battlefieldResolutionsPayload, out var spectatorBattlefieldResolutions))
        {
            errors.Add("spectator replay frame timing battlefield resolutions payload is required");
        }
        else
        {
            ValidateRetainedResolutionHistoryListCount(
                spectatorBattlefieldResolutions.Count,
                "spectator replay frame timing battlefield resolutions",
                errors);
            ValidateRetainedResolutionHistoryPayloadTickOrder(
                spectatorBattlefieldResolutions,
                "spectator replay frame timing battlefield resolutions",
                "spectator replay frame timing battlefield resolution item",
                errors);
            var validateAuthoritativeBattlefieldResolutionParity =
                spectatorBattlefieldResolutions.Count == authoritativeBattlefieldResolutionCount;
            if (!validateAuthoritativeBattlefieldResolutionParity)
            {
                errors.Add(
                    $"spectator replay frame timing battlefield resolution count {spectatorBattlefieldResolutions.Count} does not match authoritative state battlefield resolution count {authoritativeBattlefieldResolutionCount}");
            }

            var seenSpectatorBattlefieldResolutionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resolutionPayload in spectatorBattlefieldResolutions)
            {
                if (!IsSnapshotPlayerPayloadObject(resolutionPayload))
                {
                    errors.Add("spectator replay frame timing battlefield resolution payload is required");
                    continue;
                }

                ValidateSnapshotPayloadObjectPropertyNames(
                    resolutionPayload,
                    "spectator replay frame timing battlefield resolution item",
                    errors);
                var resolutionId = ValidateSpectatorBattlefieldResolutionPayloadValues(
                    resolutionPayload,
                    errors);
                if (resolutionId is not null && !seenSpectatorBattlefieldResolutionIds.Add(resolutionId))
                {
                    errors.Add($"spectator replay frame timing battlefield resolution item resolution id {resolutionId} is duplicated");
                }

                ValidateSpectatorBattlefieldResolutionPayloadListValues(
                    resolutionPayload,
                    errors);
                ValidateBattlefieldResolutionObjectReferences(
                    resolutionPayload,
                    "spectator replay frame timing battlefield resolution item",
                    spectatorKnownObjectIds.Concat(authoritativeState.BattlefieldResolutions
                        .FirstOrDefault(r => r.ResolutionId == resolutionId)?.PublicObjectBindings?.Select(b => b.ObjectId) ?? [])
                        .ToHashSet(StringComparer.Ordinal),
                    "object registry",
                    errors);
                ValidateBattlefieldResolutionPlayerReferences(
                    resolutionPayload,
                    "spectator replay frame timing battlefield resolution item",
                    spectatorKnownPlayerIds,
                    "seats",
                    errors);
            }

            var authoritativeBattlefieldResolutionsById =
                BuildAuthoritativeBattlefieldResolutionsById(authoritativeState.BattlefieldResolutions);
            ValidateSpectatorBattlefieldResolutionAuthoritativeKeySet(
                spectatorBattlefieldResolutions,
                authoritativeBattlefieldResolutionsById,
                errors);
            ValidateSpectatorBattlefieldResolutionKeyedAuthoritativeValues(
                spectatorBattlefieldResolutions,
                authoritativeBattlefieldResolutionsById,
                errors);

            if (validateAuthoritativeBattlefieldResolutionParity)
            {
                var spectatorBattlefieldResolutionIds = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "resolutionId");
                var authoritativeBattlefieldResolutionIds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.ResolutionId)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionIds, authoritativeBattlefieldResolutionIds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution ids disagree with authoritative state battlefield resolution ids");
                }

                var spectatorBattlefieldResolutionTicks = ExtractObjectLongValues(
                    spectatorBattlefieldResolutions,
                    "tick");
                var authoritativeBattlefieldResolutionTicks = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.Tick)
                    .ToArray();
                if (!LongListsEqual(spectatorBattlefieldResolutionTicks, authoritativeBattlefieldResolutionTicks))
                {
                    errors.Add("spectator replay frame timing battlefield resolution ticks disagree with authoritative state battlefield resolution ticks");
                }

                var spectatorBattlefieldResolutionKinds = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "kind");
                var authoritativeBattlefieldResolutionKinds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.Kind)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionKinds, authoritativeBattlefieldResolutionKinds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution kinds disagree with authoritative state battlefield resolution kinds");
                }

                var spectatorBattlefieldResolutionReasons = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "reason");
                var authoritativeBattlefieldResolutionReasons = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.Reason)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionReasons, authoritativeBattlefieldResolutionReasons))
                {
                    errors.Add("spectator replay frame timing battlefield resolution reasons disagree with authoritative state battlefield resolution reasons");
                }

                var spectatorBattlefieldResolutionBattlefieldObjectIds = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "battlefieldObjectId");
                var authoritativeBattlefieldResolutionBattlefieldObjectIds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.BattlefieldObjectId)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionBattlefieldObjectIds, authoritativeBattlefieldResolutionBattlefieldObjectIds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution battlefield object ids disagree with authoritative state battlefield resolution battlefield object ids");
                }

                var spectatorBattlefieldResolutionPlayerIds = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "playerId");
                var authoritativeBattlefieldResolutionPlayerIds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.PlayerId ?? string.Empty)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionPlayerIds, authoritativeBattlefieldResolutionPlayerIds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution player ids disagree with authoritative state battlefield resolution player ids");
                }

                var spectatorBattlefieldResolutionPreviousControllerIds = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "previousControllerId");
                var authoritativeBattlefieldResolutionPreviousControllerIds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.PreviousControllerId ?? string.Empty)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionPreviousControllerIds, authoritativeBattlefieldResolutionPreviousControllerIds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution previous controller ids disagree with authoritative state battlefield resolution previous controller ids");
                }

                var spectatorBattlefieldResolutionControllerIds = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "controllerId");
                var authoritativeBattlefieldResolutionControllerIds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.ControllerId ?? string.Empty)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionControllerIds, authoritativeBattlefieldResolutionControllerIds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution controller ids disagree with authoritative state battlefield resolution controller ids");
                }

                var spectatorBattlefieldResolutionSourceObjectIds = ExtractObjectStringValues(
                    spectatorBattlefieldResolutions,
                    "sourceObjectId");
                var authoritativeBattlefieldResolutionSourceObjectIds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.SourceObjectId ?? string.Empty)
                    .ToArray();
                if (!StringListsEqual(spectatorBattlefieldResolutionSourceObjectIds, authoritativeBattlefieldResolutionSourceObjectIds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution source object ids disagree with authoritative state battlefield resolution source object ids");
                }

                var spectatorBattlefieldResolutionParticipantObjectIds = ExtractObjectStringListValues(
                    spectatorBattlefieldResolutions,
                    "participantObjectIds");
                var authoritativeBattlefieldResolutionParticipantObjectIds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.ParticipantObjectIds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattlefieldResolutionParticipantObjectIds, authoritativeBattlefieldResolutionParticipantObjectIds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution participant object ids disagree with authoritative state battlefield resolution participant object ids");
                }

                var spectatorBattlefieldResolutionRelatedEventKinds = ExtractObjectStringListValues(
                    spectatorBattlefieldResolutions,
                    "relatedEventKinds");
                var authoritativeBattlefieldResolutionRelatedEventKinds = authoritativeState.BattlefieldResolutions
                    .Select(resolution => resolution.RelatedEventKinds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattlefieldResolutionRelatedEventKinds, authoritativeBattlefieldResolutionRelatedEventKinds))
                {
                    errors.Add("spectator replay frame timing battlefield resolution related event kinds disagree with authoritative state battlefield resolution related event kinds");
                }
            }
        }

        var authoritativeBattleResolutionCount = authoritativeState.BattleResolutions.Count;
        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue(
                "battleResolutions",
                out var battleResolutionsPayload)
            || IsNullSnapshotPayloadValue(battleResolutionsPayload))
        {
            errors.Add("spectator replay frame timing battle resolutions are required");
            if (authoritativeBattleResolutionCount > 0)
            {
                errors.Add(
                    $"spectator replay frame timing battle resolution count 0 does not match authoritative state battle resolution count {authoritativeBattleResolutionCount}");
            }
        }
        else if (!TryReadObjectListValue(battleResolutionsPayload, out var spectatorBattleResolutions))
        {
            errors.Add("spectator replay frame timing battle resolutions payload is required");
        }
        else
        {
            ValidateRetainedResolutionHistoryListCount(
                spectatorBattleResolutions.Count,
                "spectator replay frame timing battle resolutions",
                errors);
            ValidateRetainedResolutionHistoryPayloadTickOrder(
                spectatorBattleResolutions,
                "spectator replay frame timing battle resolutions",
                "spectator replay frame timing battle resolution item",
                errors);
            var validateAuthoritativeBattleResolutionParity =
                spectatorBattleResolutions.Count == authoritativeBattleResolutionCount;
            if (!validateAuthoritativeBattleResolutionParity)
            {
                errors.Add(
                    $"spectator replay frame timing battle resolution count {spectatorBattleResolutions.Count} does not match authoritative state battle resolution count {authoritativeBattleResolutionCount}");
            }

            var seenSpectatorBattleResolutionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resolutionPayload in spectatorBattleResolutions)
            {
                if (!IsSnapshotPlayerPayloadObject(resolutionPayload))
                {
                    errors.Add("spectator replay frame timing battle resolution payload is required");
                    continue;
                }

                ValidateSnapshotPayloadObjectPropertyNames(
                    resolutionPayload,
                    "spectator replay frame timing battle resolution item",
                    errors);
                var resolutionId = ValidateSpectatorBattleResolutionPayloadValues(
                    resolutionPayload,
                    errors);
                if (resolutionId is not null && !seenSpectatorBattleResolutionIds.Add(resolutionId))
                {
                    errors.Add($"spectator replay frame timing battle resolution item resolution id {resolutionId} is duplicated");
                }

                ValidateSpectatorBattleResolutionPayloadListValues(
                    resolutionPayload,
                    errors);
                ValidateBattleResolutionObjectReferences(
                    resolutionPayload,
                    "spectator replay frame timing battle resolution item",
                    spectatorKnownObjectIds.Concat(authoritativeState.BattleResolutions.FirstOrDefault(r => r.ResolutionId == resolutionId)
                        ?.PublicObjectBindings?.Select(b => b.ObjectId) ?? []).ToHashSet(StringComparer.Ordinal),
                    "object registry",
                    errors);
                if (authoritativeState.BattleResolutions.FirstOrDefault(r => r.ResolutionId == resolutionId) is { } recorded)
                    ValidateCapturedPublicBattleHistoryView(resolutionPayload, recorded, "spectator replay frame timing battle resolution item", errors);
                ValidateBattleResolutionPlayerReferences(
                    resolutionPayload,
                    "spectator replay frame timing battle resolution item",
                    spectatorKnownPlayerIds,
                    "seats",
                    errors);
            }

            var authoritativeBattleResolutionsById =
                BuildAuthoritativeBattleResolutionsById(authoritativeState.BattleResolutions);
            ValidateSpectatorBattleResolutionAuthoritativeKeySet(
                spectatorBattleResolutions,
                authoritativeBattleResolutionsById,
                errors);
            ValidateSpectatorBattleResolutionKeyedAuthoritativeValues(
                spectatorBattleResolutions,
                authoritativeBattleResolutionsById,
                errors);

            if (validateAuthoritativeBattleResolutionParity)
            {
                var spectatorBattleResolutionIds = ExtractObjectStringValues(
                    spectatorBattleResolutions,
                    "resolutionId");
                var authoritativeBattleResolutionIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.ResolutionId)
                    .ToArray();
                if (!StringListsEqual(spectatorBattleResolutionIds, authoritativeBattleResolutionIds))
                {
                    errors.Add("spectator replay frame timing battle resolution ids disagree with authoritative state battle resolution ids");
                }

                var spectatorBattleResolutionTicks = ExtractObjectLongValues(
                    spectatorBattleResolutions,
                    "tick");
                var authoritativeBattleResolutionTicks = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.Tick)
                    .ToArray();
                if (!LongListsEqual(spectatorBattleResolutionTicks, authoritativeBattleResolutionTicks))
                {
                    errors.Add("spectator replay frame timing battle resolution ticks disagree with authoritative state battle resolution ticks");
                }

                var spectatorBattleResolutionKinds = ExtractObjectStringValues(
                    spectatorBattleResolutions,
                    "kind");
                var authoritativeBattleResolutionKinds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.Kind)
                    .ToArray();
                if (!StringListsEqual(spectatorBattleResolutionKinds, authoritativeBattleResolutionKinds))
                {
                    errors.Add("spectator replay frame timing battle resolution kinds disagree with authoritative state battle resolution kinds");
                }

                var spectatorBattleResolutionReasons = ExtractObjectStringValues(
                    spectatorBattleResolutions,
                    "reason");
                var authoritativeBattleResolutionReasons = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.Reason)
                    .ToArray();
                if (!StringListsEqual(spectatorBattleResolutionReasons, authoritativeBattleResolutionReasons))
                {
                    errors.Add("spectator replay frame timing battle resolution reasons disagree with authoritative state battle resolution reasons");
                }

                var spectatorBattleResolutionBattlefieldIds = ExtractObjectStringValues(
                    spectatorBattleResolutions,
                    "battlefieldId");
                var authoritativeBattleResolutionBattlefieldIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.BattlefieldId)
                    .ToArray();
                if (!StringListsEqual(spectatorBattleResolutionBattlefieldIds, authoritativeBattleResolutionBattlefieldIds))
                {
                    errors.Add("spectator replay frame timing battle resolution battlefield ids disagree with authoritative state battle resolution battlefield ids");
                }

                var spectatorBattleResolutionAttackingPlayerIds = ExtractObjectStringValues(
                    spectatorBattleResolutions,
                    "attackingPlayerId");
                var authoritativeBattleResolutionAttackingPlayerIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.AttackingPlayerId ?? string.Empty)
                    .ToArray();
                if (!StringListsEqual(spectatorBattleResolutionAttackingPlayerIds, authoritativeBattleResolutionAttackingPlayerIds))
                {
                    errors.Add("spectator replay frame timing battle resolution attacking player ids disagree with authoritative state battle resolution attacking player ids");
                }

                var spectatorBattleResolutionDefendingPlayerIds = ExtractObjectStringValues(
                    spectatorBattleResolutions,
                    "defendingPlayerId");
                var authoritativeBattleResolutionDefendingPlayerIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.DefendingPlayerId ?? string.Empty)
                    .ToArray();
                if (!StringListsEqual(spectatorBattleResolutionDefendingPlayerIds, authoritativeBattleResolutionDefendingPlayerIds))
                {
                    errors.Add("spectator replay frame timing battle resolution defending player ids disagree with authoritative state battle resolution defending player ids");
                }

                var spectatorBattleResolutionWinnerPlayerIds = ExtractObjectStringValues(
                    spectatorBattleResolutions,
                    "winnerPlayerId");
                var authoritativeBattleResolutionWinnerPlayerIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.WinnerPlayerId ?? string.Empty)
                    .ToArray();
                if (!StringListsEqual(spectatorBattleResolutionWinnerPlayerIds, authoritativeBattleResolutionWinnerPlayerIds))
                {
                    errors.Add("spectator replay frame timing battle resolution winner player ids disagree with authoritative state battle resolution winner player ids");
                }

                var spectatorBattleResolutionAttackerObjectIds = ExtractObjectStringListValues(
                    spectatorBattleResolutions,
                    "attackerObjectIds");
                var authoritativeBattleResolutionAttackerObjectIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.AttackerObjectIds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattleResolutionAttackerObjectIds, authoritativeBattleResolutionAttackerObjectIds))
                {
                    errors.Add("spectator replay frame timing battle resolution attacker object ids disagree with authoritative state battle resolution attacker object ids");
                }

                var spectatorBattleResolutionDefenderObjectIds = ExtractObjectStringListValues(
                    spectatorBattleResolutions,
                    "defenderObjectIds");
                var authoritativeBattleResolutionDefenderObjectIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.DefenderObjectIds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattleResolutionDefenderObjectIds, authoritativeBattleResolutionDefenderObjectIds))
                {
                    errors.Add("spectator replay frame timing battle resolution defender object ids disagree with authoritative state battle resolution defender object ids");
                }

                var spectatorBattleResolutionSurvivingAttackerObjectIds = ExtractObjectStringListValues(
                    spectatorBattleResolutions,
                    "survivingAttackerObjectIds");
                var authoritativeBattleResolutionSurvivingAttackerObjectIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.SurvivingAttackerObjectIds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattleResolutionSurvivingAttackerObjectIds, authoritativeBattleResolutionSurvivingAttackerObjectIds))
                {
                    errors.Add("spectator replay frame timing battle resolution surviving attacker object ids disagree with authoritative state battle resolution surviving attacker object ids");
                }

                var spectatorBattleResolutionSurvivingDefenderObjectIds = ExtractObjectStringListValues(
                    spectatorBattleResolutions,
                    "survivingDefenderObjectIds");
                var authoritativeBattleResolutionSurvivingDefenderObjectIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.SurvivingDefenderObjectIds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattleResolutionSurvivingDefenderObjectIds, authoritativeBattleResolutionSurvivingDefenderObjectIds))
                {
                    errors.Add("spectator replay frame timing battle resolution surviving defender object ids disagree with authoritative state battle resolution surviving defender object ids");
                }

                var spectatorBattleResolutionDestroyedObjectIds = ExtractObjectStringListValues(
                    spectatorBattleResolutions,
                    "destroyedObjectIds");
                var authoritativeBattleResolutionDestroyedObjectIds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.DestroyedObjectIds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattleResolutionDestroyedObjectIds, authoritativeBattleResolutionDestroyedObjectIds))
                {
                    errors.Add("spectator replay frame timing battle resolution destroyed object ids disagree with authoritative state battle resolution destroyed object ids");
                }

                var spectatorBattleResolutionRelatedEventKinds = ExtractObjectStringListValues(
                    spectatorBattleResolutions,
                    "relatedEventKinds");
                var authoritativeBattleResolutionRelatedEventKinds = authoritativeState.BattleResolutions
                    .Select(resolution => resolution.RelatedEventKinds)
                    .ToArray();
                if (!StringListCollectionsEqual(spectatorBattleResolutionRelatedEventKinds, authoritativeBattleResolutionRelatedEventKinds))
                {
                    errors.Add("spectator replay frame timing battle resolution related event kinds disagree with authoritative state battle resolution related event kinds");
                }
            }
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Timing.ContainsKey("seed")
            || spectatorReplayFrame.SpectatorSnapshot.Timing.ContainsKey("rngCursor"))
        {
            errors.Add("spectator replay frame timing leaks random state");
        }
    }

    private static IReadOnlyDictionary<string, BattlefieldResolutionState> BuildAuthoritativeBattlefieldResolutionsById(
        IReadOnlyList<BattlefieldResolutionState> authoritativeResolutions)
    {
        var authoritativeResolutionsById = new Dictionary<string, BattlefieldResolutionState>(StringComparer.Ordinal);
        foreach (var authoritativeResolution in authoritativeResolutions)
        {
            if (string.IsNullOrWhiteSpace(authoritativeResolution.ResolutionId))
            {
                continue;
            }

            var normalizedResolutionId = authoritativeResolution.ResolutionId.Trim();
            if (!authoritativeResolutionsById.ContainsKey(normalizedResolutionId))
            {
                authoritativeResolutionsById.Add(normalizedResolutionId, authoritativeResolution);
            }
        }

        return authoritativeResolutionsById;
    }

    private static IReadOnlyDictionary<string, BattleResolutionState> BuildAuthoritativeBattleResolutionsById(
        IReadOnlyList<BattleResolutionState> authoritativeResolutions)
    {
        var authoritativeResolutionsById = new Dictionary<string, BattleResolutionState>(StringComparer.Ordinal);
        foreach (var authoritativeResolution in authoritativeResolutions)
        {
            if (string.IsNullOrWhiteSpace(authoritativeResolution.ResolutionId))
            {
                continue;
            }

            var normalizedResolutionId = authoritativeResolution.ResolutionId.Trim();
            if (!authoritativeResolutionsById.ContainsKey(normalizedResolutionId))
            {
                authoritativeResolutionsById.Add(normalizedResolutionId, authoritativeResolution);
            }
        }

        return authoritativeResolutionsById;
    }

    private static void ValidateSpectatorBattlefieldResolutionAuthoritativeKeySet(
        IReadOnlyList<object?> resolutionPayloads,
        IReadOnlyDictionary<string, BattlefieldResolutionState> authoritativeResolutionsById,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield resolution item";
        var actualResolutionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolutionPayload in resolutionPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(resolutionPayload)
                || !TryReadObjectString(resolutionPayload, "resolutionId", out var resolutionId)
                || string.IsNullOrWhiteSpace(resolutionId))
            {
                continue;
            }

            var normalizedResolutionId = resolutionId.Trim();
            actualResolutionIds.Add(normalizedResolutionId);
            if (!authoritativeResolutionsById.ContainsKey(normalizedResolutionId))
            {
                errors.Add(
                    $"{payloadLabel} resolution id {normalizedResolutionId} is not present in authoritative state battlefield resolutions");
            }
        }

        foreach (var authoritativeResolutionId in authoritativeResolutionsById.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!actualResolutionIds.Contains(authoritativeResolutionId))
            {
                errors.Add(
                    $"{payloadLabel} resolution id {authoritativeResolutionId} is required by authoritative state battlefield resolutions");
            }
        }
    }

    private static void ValidateSpectatorBattleResolutionAuthoritativeKeySet(
        IReadOnlyList<object?> resolutionPayloads,
        IReadOnlyDictionary<string, BattleResolutionState> authoritativeResolutionsById,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle resolution item";
        var actualResolutionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolutionPayload in resolutionPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(resolutionPayload)
                || !TryReadObjectString(resolutionPayload, "resolutionId", out var resolutionId)
                || string.IsNullOrWhiteSpace(resolutionId))
            {
                continue;
            }

            var normalizedResolutionId = resolutionId.Trim();
            actualResolutionIds.Add(normalizedResolutionId);
            if (!authoritativeResolutionsById.ContainsKey(normalizedResolutionId))
            {
                errors.Add(
                    $"{payloadLabel} resolution id {normalizedResolutionId} is not present in authoritative state battle resolutions");
            }
        }

        foreach (var authoritativeResolutionId in authoritativeResolutionsById.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!actualResolutionIds.Contains(authoritativeResolutionId))
            {
                errors.Add(
                    $"{payloadLabel} resolution id {authoritativeResolutionId} is required by authoritative state battle resolutions");
            }
        }
    }

    private static void ValidateSpectatorBattlefieldResolutionKeyedAuthoritativeValues(
        IReadOnlyList<object?> resolutionPayloads,
        IReadOnlyDictionary<string, BattlefieldResolutionState> authoritativeResolutionsById,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield resolution item";
        const string authoritativeLabel = "authoritative state battlefield resolution";
        foreach (var resolutionPayload in resolutionPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(resolutionPayload)
                || !TryReadObjectString(resolutionPayload, "resolutionId", out var resolutionId)
                || string.IsNullOrWhiteSpace(resolutionId))
            {
                continue;
            }

            var normalizedResolutionId = resolutionId.Trim();
            if (!authoritativeResolutionsById.TryGetValue(normalizedResolutionId, out var authoritativeResolution))
            {
                continue;
            }

            ValidateSpectatorResolutionKeyedLongValue(
                resolutionPayload,
                "tick",
                "tick",
                authoritativeResolution.Tick,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringValue(
                resolutionPayload,
                "kind",
                "kind",
                authoritativeResolution.Kind,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringValue(
                resolutionPayload,
                "reason",
                "reason",
                authoritativeResolution.Reason,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringValue(
                resolutionPayload,
                "battlefieldObjectId",
                "battlefield object id",
                authoritativeResolution.BattlefieldObjectId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedOptionalStringValue(
                resolutionPayload,
                "playerId",
                "player id",
                authoritativeResolution.PlayerId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedOptionalStringValue(
                resolutionPayload,
                "previousControllerId",
                "previous controller id",
                authoritativeResolution.PreviousControllerId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedOptionalStringValue(
                resolutionPayload,
                "controllerId",
                "controller id",
                authoritativeResolution.ControllerId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedOptionalStringValue(
                resolutionPayload,
                "sourceObjectId",
                "source object id",
                authoritativeResolution.SourceObjectId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "participantObjectIds",
                "participant object ids",
                authoritativeResolution.ParticipantObjectIds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "relatedEventKinds",
                "related event kinds",
                authoritativeResolution.RelatedEventKinds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
        }
    }

    private static void ValidateSpectatorBattleResolutionKeyedAuthoritativeValues(
        IReadOnlyList<object?> resolutionPayloads,
        IReadOnlyDictionary<string, BattleResolutionState> authoritativeResolutionsById,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle resolution item";
        const string authoritativeLabel = "authoritative state battle resolution";
        foreach (var resolutionPayload in resolutionPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(resolutionPayload)
                || !TryReadObjectString(resolutionPayload, "resolutionId", out var resolutionId)
                || string.IsNullOrWhiteSpace(resolutionId))
            {
                continue;
            }

            var normalizedResolutionId = resolutionId.Trim();
            if (!authoritativeResolutionsById.TryGetValue(normalizedResolutionId, out var authoritativeResolution))
            {
                continue;
            }

            ValidateSpectatorResolutionKeyedLongValue(
                resolutionPayload,
                "tick",
                "tick",
                authoritativeResolution.Tick,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringValue(
                resolutionPayload,
                "kind",
                "kind",
                authoritativeResolution.Kind,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringValue(
                resolutionPayload,
                "reason",
                "reason",
                authoritativeResolution.Reason,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringValue(
                resolutionPayload,
                "battlefieldId",
                "battlefield id",
                authoritativeResolution.BattlefieldId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedOptionalStringValue(
                resolutionPayload,
                "attackingPlayerId",
                "attacking player id",
                authoritativeResolution.AttackingPlayerId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedOptionalStringValue(
                resolutionPayload,
                "defendingPlayerId",
                "defending player id",
                authoritativeResolution.DefendingPlayerId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedOptionalStringValue(
                resolutionPayload,
                "winnerPlayerId",
                "winner player id",
                authoritativeResolution.WinnerPlayerId,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "attackerObjectIds",
                "attacker object ids",
                authoritativeResolution.AttackerObjectIds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "defenderObjectIds",
                "defender object ids",
                authoritativeResolution.DefenderObjectIds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "survivingAttackerObjectIds",
                "surviving attacker object ids",
                authoritativeResolution.SurvivingAttackerObjectIds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "survivingDefenderObjectIds",
                "surviving defender object ids",
                authoritativeResolution.SurvivingDefenderObjectIds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "destroyedObjectIds",
                "destroyed object ids",
                authoritativeResolution.DestroyedObjectIds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
            ValidateSpectatorResolutionKeyedStringListValue(
                resolutionPayload,
                "relatedEventKinds",
                "related event kinds",
                authoritativeResolution.RelatedEventKinds,
                payloadLabel,
                authoritativeLabel,
                normalizedResolutionId,
                errors);
        }
    }

    private static void ValidateSpectatorResolutionKeyedStringValue(
        object? resolutionPayload,
        string key,
        string fieldLabel,
        string expected,
        string payloadLabel,
        string authoritativeLabel,
        string resolutionId,
        List<string> errors)
    {
        if (!TryReadObjectString(resolutionPayload, key, out var actual))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match {authoritativeLabel} {fieldLabel} {expected} for resolution id {resolutionId}");
        }
        else if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual ?? string.Empty} does not match {authoritativeLabel} {fieldLabel} {expected} for resolution id {resolutionId}");
        }
    }

    private static void ValidateSpectatorResolutionKeyedOptionalStringValue(
        object? resolutionPayload,
        string key,
        string fieldLabel,
        string? expected,
        string payloadLabel,
        string authoritativeLabel,
        string resolutionId,
        List<string> errors)
    {
        if (TryReadObjectOptionalString(resolutionPayload, key, out var actual)
            && !string.Equals(actual, expected ?? string.Empty, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match {authoritativeLabel} {fieldLabel} {expected ?? string.Empty} for resolution id {resolutionId}");
        }
    }

    private static void ValidateSpectatorResolutionKeyedLongValue(
        object? resolutionPayload,
        string key,
        string fieldLabel,
        long expected,
        string payloadLabel,
        string authoritativeLabel,
        string resolutionId,
        List<string> errors)
    {
        if (!TryReadObjectLong(resolutionPayload, key, out var actual))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match {authoritativeLabel} {fieldLabel} {expected} for resolution id {resolutionId}");
        }
        else if (actual != expected)
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match {authoritativeLabel} {fieldLabel} {expected} for resolution id {resolutionId}");
        }
    }

    private static void ValidateSpectatorResolutionKeyedStringListValue(
        object? resolutionPayload,
        string key,
        string fieldLabel,
        IReadOnlyList<string> expected,
        string payloadLabel,
        string authoritativeLabel,
        string resolutionId,
        List<string> errors)
    {
        if (!TryReadObjectStringList(resolutionPayload, key, out var actual)
            || !StringListsEqual(actual, expected))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} do not match {authoritativeLabel} {fieldLabel} for resolution id {resolutionId}");
        }
    }

    private static void ValidateSpectatorSnapshotCoreNumericScalarValues(
        SnapshotDto snapshot,
        List<string> errors)
    {
        if (snapshot.Tick < 0)
        {
            errors.Add($"spectator replay frame snapshot tick {snapshot.Tick} cannot be negative");
        }

        if (snapshot.TurnNumber < 1)
        {
            errors.Add($"spectator replay frame snapshot turn number {snapshot.TurnNumber} must be positive");
        }
    }

    private static string? ValidateSpectatorSnapshotRequiredStringScalarValue(
        string? value,
        string itemLabel,
        List<string> errors,
        Func<string, bool>? isKnownValue = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"spectator replay frame snapshot {itemLabel} is required");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame snapshot {itemLabel} {normalizedValue} has surrounding whitespace");
        }

        if (isKnownValue is not null && !isKnownValue(normalizedValue))
        {
            errors.Add($"spectator replay frame snapshot {itemLabel} {normalizedValue} is invalid");
        }

        return normalizedValue;
    }

    private static void ValidateSpectatorBattlefieldResolutionPayloadListValues(
        object? resolutionPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield resolution item";
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "participantObjectIds",
            "participant object id",
            errors);
        var participantObjectIds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "participantObjectIds",
            payloadLabel,
            "participant object id",
            errors);
        ValidateBattlefieldResolutionCombatParticipantAvailability(
            ReadNormalizedPayloadString(resolutionPayload, "kind"),
            ReadNormalizedPayloadString(resolutionPayload, "sourceObjectId"),
            participantObjectIds,
            payloadLabel,
            "source object id",
            "participant object id",
            errors);
        ValidateBattlefieldResolutionControlParticipantAbsence(
            ReadNormalizedPayloadString(resolutionPayload, "kind"),
            ReadNormalizedPayloadString(resolutionPayload, "sourceObjectId"),
            participantObjectIds,
            payloadLabel,
            "source object id",
            "participant object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "relatedEventKinds",
            "related event kind",
            errors);
        var relatedEventKinds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "relatedEventKinds",
            payloadLabel,
            "related event kind",
            errors,
            IsKnownBattlefieldResolutionRelatedEventKind);
        ValidateBattlefieldResolutionRelatedEventKindCompatibility(
            ReadNormalizedPayloadString(resolutionPayload, "kind"),
            relatedEventKinds,
            payloadLabel,
            errors);
    }

    private static string? ValidateSpectatorBattlefieldResolutionPayloadValues(
        object? resolutionPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield resolution item";
        return ValidateBattlefieldResolutionPayloadValues(resolutionPayload, payloadLabel, errors);
    }

    private static string? ValidateBattlefieldResolutionPayloadValues(
        object? resolutionPayload,
        string payloadLabel,
        List<string> errors)
    {
        var resolutionId = ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "resolutionId",
            payloadLabel,
            "resolution id",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeLongValue(
            resolutionPayload,
            "tick",
            payloadLabel,
            "tick",
            errors);
        var kind = ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "kind",
            payloadLabel,
            "kind",
            errors,
            IsKnownBattlefieldResolutionKind);
        var reason = ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "reason",
            payloadLabel,
            "reason",
            errors,
            IsKnownBattlefieldResolutionReason);
        ValidateBattlefieldResolutionKindReasonCompatibility(kind, reason, payloadLabel, errors);
        ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield object id",
            errors);
        var playerId = ValidateSnapshotPayloadOptionalStringValue(
            resolutionPayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);
        ValidateBattlefieldResolutionCombatPlayerAvailability(
            kind,
            playerId,
            payloadLabel,
            "player id",
            errors);
        var previousControllerId = ValidateSnapshotPayloadOptionalStringValue(
            resolutionPayload,
            "previousControllerId",
            payloadLabel,
            "previous controller id",
            errors);
        var controllerId = ValidateSnapshotPayloadOptionalStringValue(
            resolutionPayload,
            "controllerId",
            payloadLabel,
            "controller id",
            errors);
        ValidateBattlefieldResolutionControlControllerCompatibility(
            kind,
            reason,
            playerId,
            controllerId,
            payloadLabel,
            "player id",
            "controller id",
            errors);
        ValidateBattlefieldResolutionControlPreviousControllerCompatibility(
            kind,
            reason,
            previousControllerId,
            controllerId,
            payloadLabel,
            "previous controller id",
            "controller id",
            errors);
        ValidateBattlefieldResolutionCombatControllerAbsence(
            kind,
            previousControllerId,
            controllerId,
            payloadLabel,
            "previous controller id",
            "controller id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            resolutionPayload,
            "sourceObjectId",
            payloadLabel,
            "source object id",
            errors);
        return resolutionId;
    }

    private static void ValidateSpectatorBattleResolutionPayloadListValues(
        object? resolutionPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle resolution item";
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "attackerObjectIds",
            "attacker object id",
            errors);
        var attackerObjectIds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "attackerObjectIds",
            payloadLabel,
            "attacker object id",
            errors);
        ValidateBattleResolutionParticipantObjectListCount(
            attackerObjectIds,
            payloadLabel,
            "attacker object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "defenderObjectIds",
            "defender object id",
            errors);
        var defenderObjectIds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "defenderObjectIds",
            payloadLabel,
            "defender object id",
            errors);
        ValidateBattleResolutionParticipantObjectListCount(
            defenderObjectIds,
            payloadLabel,
            "defender object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "survivingAttackerObjectIds",
            "surviving attacker object id",
            errors);
        var survivingAttackerObjectIds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "survivingAttackerObjectIds",
            payloadLabel,
            "surviving attacker object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "survivingDefenderObjectIds",
            "surviving defender object id",
            errors);
        var survivingDefenderObjectIds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "survivingDefenderObjectIds",
            payloadLabel,
            "surviving defender object id",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "destroyedObjectIds",
            "destroyed object id",
            errors);
        var destroyedObjectIds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "destroyedObjectIds",
            payloadLabel,
            "destroyed object id",
            errors);
        ValidateBattleResolutionResultListObjectMembership(
            attackerObjectIds,
            defenderObjectIds,
            survivingAttackerObjectIds,
            survivingDefenderObjectIds,
            destroyedObjectIds,
            payloadLabel,
            errors);
        ValidateBattleResolutionNoResultSurvivorReasonCompatibility(
            ReadNormalizedPayloadString(resolutionPayload, "kind"),
            ReadNormalizedPayloadString(resolutionPayload, "reason"),
            survivingAttackerObjectIds,
            survivingDefenderObjectIds,
            payloadLabel,
            errors);
        ValidateBattleResolutionClosedWinnerSurvivorCompatibility(
            ReadNormalizedPayloadString(resolutionPayload, "kind"),
            ReadNormalizedPayloadString(resolutionPayload, "attackingPlayerId"),
            ReadNormalizedPayloadString(resolutionPayload, "defendingPlayerId"),
            ReadNormalizedPayloadString(resolutionPayload, "winnerPlayerId"),
            survivingAttackerObjectIds,
            survivingDefenderObjectIds,
            payloadLabel,
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            resolutionPayload,
            payloadLabel,
            "relatedEventKinds",
            "related event kind",
            errors);
        var relatedEventKinds = ValidateSnapshotPayloadStringListValues(
            resolutionPayload,
            "relatedEventKinds",
            payloadLabel,
            "related event kind",
            errors,
            IsKnownBattleResolutionRelatedEventKind);
        ValidateBattleResolutionRelatedEventKindCompatibility(
            ReadNormalizedPayloadString(resolutionPayload, "kind"),
            ReadNormalizedPayloadString(resolutionPayload, "reason"),
            relatedEventKinds,
            payloadLabel,
            errors);
    }

    private static string? ValidateSpectatorBattleResolutionPayloadValues(
        object? resolutionPayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battle resolution item";
        return ValidateBattleResolutionPayloadValues(resolutionPayload, payloadLabel, errors);
    }

    private static string? ValidateBattleResolutionPayloadValues(
        object? resolutionPayload,
        string payloadLabel,
        List<string> errors)
    {
        var resolutionId = ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "resolutionId",
            payloadLabel,
            "resolution id",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeLongValue(
            resolutionPayload,
            "tick",
            payloadLabel,
            "tick",
            errors);
        var kind = ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "kind",
            payloadLabel,
            "kind",
            errors,
            IsKnownBattleResolutionKind);
        var reason = ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "reason",
            payloadLabel,
            "reason",
            errors,
            IsKnownBattleResolutionReason);
        ValidateBattleResolutionKindReasonCompatibility(kind, reason, payloadLabel, errors);
        ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "battlefieldId",
            payloadLabel,
            "battlefield id",
            errors);
        var attackingPlayerId = ValidateSnapshotPayloadRequiredStringValue(
            resolutionPayload,
            "attackingPlayerId",
            payloadLabel,
            "attacking player id",
            errors);
        var defendingPlayerId = ValidateSnapshotPayloadOptionalStringValue(
            resolutionPayload,
            "defendingPlayerId",
            payloadLabel,
            "defending player id",
            errors);
        var winnerPlayerId = ValidateSnapshotPayloadOptionalStringValue(
            resolutionPayload,
            "winnerPlayerId",
            payloadLabel,
            "winner player id",
            errors);
        ValidateBattleResolutionPlayerRoleCompatibility(
            kind,
            attackingPlayerId,
            defendingPlayerId,
            winnerPlayerId,
            payloadLabel,
            errors);
        return resolutionId;
    }

    private static void ValidateBattlefieldResolutionObjectReferences(
        object? resolutionPayload,
        string payloadLabel,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        ValidateTimingOptionalObjectReferenceWithExpectedDetails(
            resolutionPayload,
            "battlefieldObjectId",
            $"{payloadLabel} battlefield object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingOptionalObjectReferenceWithExpectedDetails(
            resolutionPayload,
            "sourceObjectId",
            $"{payloadLabel} source object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            resolutionPayload,
            "participantObjectIds",
            $"{payloadLabel} participant object id",
            knownObjectIds,
            knownObjectLabel,
            errors);
    }
}
