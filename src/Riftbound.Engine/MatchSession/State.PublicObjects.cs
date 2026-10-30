using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial record MatchState
{

    public static MatchState Create(string roomId)
    {
        return new MatchState(
            roomId,
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal),
            MatchStatuses.Empty,
            [],
            "P1",
            MatchPhases.Room,
            TimingStates.Room,
            new Dictionary<string, RunePool>(StringComparer.Ordinal),
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            null,
            [],
            [],
            null,
            [],
            null);
    }

    private static string InferStatus(IReadOnlyDictionary<string, string> seats)
    {
        return seats.Count == 0 ? MatchStatuses.Empty : MatchStatuses.InProgress;
    }

    private static string InferPhase(string status)
    {
        return string.Equals(status, MatchStatuses.InProgress, StringComparison.Ordinal)
            ? MatchPhases.Main
            : MatchPhases.Room;
    }

    private static string InferTimingState(string status)
    {
        return string.Equals(status, MatchStatuses.InProgress, StringComparison.Ordinal)
            ? TimingStates.NeutralOpen
            : TimingStates.Room;
    }

    private static IReadOnlyList<string> NormalizeReadyPlayers(IReadOnlyList<string>? readyPlayerIds)
    {
        return (readyPlayerIds ?? [])
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Select(playerId => playerId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, RunePool> NormalizeRunePools(
        IReadOnlyDictionary<string, RunePool>? runePools)
    {
        return (runePools ?? new Dictionary<string, RunePool>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PlayerZones> NormalizePlayerZones(
        IReadOnlyDictionary<string, PlayerZones>? playerZones)
    {
        return (playerZones ?? new Dictionary<string, PlayerZones>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => NormalizeZones(entry.Value),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> NormalizeObjectLocations(
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations)
    {
        return (objectLocations ?? new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value is not null)
            .Select(entry => new KeyValuePair<string, ObjectLocationState>(
                entry.Key.Trim(),
                ObjectLocationState.Normalize(entry.Value)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.PlayerId)
                && !string.IsNullOrWhiteSpace(entry.Value.Zone))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static TurnWindowState BuildTurnWindowState(MatchState state)
    {
        var hasStack = state.StackItems.Count > 0;
        var isSpellDuel = IsSpellDuelTimingState(state.TimingState)
            || !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            || state.StackItems.Any(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal));
        var isClosed = hasStack || string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
            || string.Equals(state.TimingState, TimingStates.SpellDuelClosed, StringComparison.Ordinal);
        var windowState = isSpellDuel
            ? isClosed ? TimingStates.SpellDuelClosed : TimingStates.SpellDuelOpen
            : isClosed ? TimingStates.NeutralClosed : TimingStates.NeutralOpen;
        var actingPlayerId = isSpellDuel
            ? state.FocusPlayerId
            : hasStack
                ? state.PriorityPlayerId
                : state.ActivePlayerId;

        return new TurnWindowState(windowState, isSpellDuel, isClosed, hasStack, actingPlayerId);
    }

    private static SpellDuelState BuildSpellDuelState(MatchState state)
    {
        var isActive = IsSpellDuelTimingState(state.TimingState)
            || !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            || state.StackItems.Any(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal));
        if (!isActive)
        {
            return new SpellDuelState(false, false, null, null, null, [], [], []);
        }

        var battlefieldObjectId = SpellDuelBattlefieldObjectId(state);
        var stackItems = state.StackItems
            .Where(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || IsSpellDuelTimingState(state.TimingState))
            .ToArray();
        return new SpellDuelState(
            true,
            state.TurnWindow.IsClosed,
            SpellDuelIdForBattlefieldOrState(battlefieldObjectId, state, stackItems),
            battlefieldObjectId,
            state.FocusPlayerId,
            state.PassedFocusPlayerIds,
            stackItems.Select(item => item.StackItemId).ToArray(),
            stackItems
                .Select(item => item.ControllerId)
                .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    private static BattleState BuildBattleState(MatchState state)
    {
        var attackerObjectIds = state.CardObjects
            .Where(entry => entry.Value.IsAttacking
                && entry.Value.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        var defenderObjectIds = state.CardObjects
            .Where(entry => entry.Value.IsDefending
                && entry.Value.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        var participantObjectIds = attackerObjectIds
            .Concat(defenderObjectIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var participantControllerIds = participantObjectIds
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && !string.IsNullOrWhiteSpace(EffectiveFieldControllerId(state, objectId, cardObject)))
            .ToDictionary(
                objectId => objectId,
                objectId => EffectiveFieldControllerId(state, objectId, state.CardObjects[objectId]),
                StringComparer.Ordinal);
        var battlefieldObjectIds = participantObjectIds
            .Select(objectId => state.ObjectLocations.TryGetValue(objectId, out var location)
                ? location.BattlefieldObjectId
                : null)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var battlefieldObjectId = battlefieldObjectIds.Length == 1 ? battlefieldObjectIds[0] : null;
        // A captured battle continues even when its last unit leaves during a
        // response. Its declaration is cleared by battle settlement, not by
        // the lifetime of the participating objects.
        var hasDeclaration = CoreRuleEngine.TryReadBattleResponseDeclarationContext(state,
            battlefieldObjectId ?? string.Empty, out var declaration);
        battlefieldObjectId ??= hasDeclaration ? declaration.BattlefieldId : null;

        return new BattleState(
            participantObjectIds.Length > 0 || hasDeclaration,
            BattleLifecycleIds.BattleIdForBattlefield(battlefieldObjectId),
            battlefieldObjectId,
            attackerObjectIds,
            defenderObjectIds,
            participantControllerIds);
    }

    private static string? SpellDuelBattlefieldObjectId(MatchState state)
    {
        var activeSpellDuelBattlefieldObjectId = ActiveSpellDuelBattlefieldObjectId(state);
        if (!string.IsNullOrWhiteSpace(activeSpellDuelBattlefieldObjectId))
        {
            return activeSpellDuelBattlefieldObjectId;
        }

        var contestedBattlefieldObjectIds = state.BattlefieldStates.Values
            .Where(battlefield => battlefield.Contested)
            .Select(battlefield => battlefield.BattlefieldObjectId)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        return contestedBattlefieldObjectIds.Length == 1 ? contestedBattlefieldObjectIds[0] : null;
    }

    private static string? ActiveSpellDuelBattlefieldObjectId(MatchState state)
    {
        var isActive = IsSpellDuelTimingState(state.TimingState)
            || !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            || state.StackItems.Any(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal));
        if (!isActive)
        {
            return null;
        }
        if (CoreRuleEngine.IsInitialBattleDuel(state, out var captured)
            && !HasBattlefieldSpellDuelCompleted(state, captured.BattlefieldId))
            return captured.BattlefieldId;

        return state.BattlefieldStates.Values
            .Where(battlefield => battlefield.Contested
                && !HasBattlefieldSpellDuelCompleted(state, battlefield.BattlefieldObjectId))
            .Select(battlefield => battlefield.BattlefieldObjectId)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string? SpellDuelIdForBattlefieldOrState(
        string? battlefieldObjectId,
        MatchState state,
        IReadOnlyList<StackItemState> stackItems)
    {
        var battlefieldSpellDuelId = BattleLifecycleIds.SpellDuelIdForBattlefield(battlefieldObjectId);
        if (!string.IsNullOrWhiteSpace(battlefieldSpellDuelId))
        {
            return battlefieldSpellDuelId;
        }

        var stackItemId = stackItems
            .Select(item => item.StackItemId)
            .FirstOrDefault(stackId => !string.IsNullOrWhiteSpace(stackId));
        if (!string.IsNullOrWhiteSpace(stackItemId))
        {
            return $"spell-duel:stack:{stackItemId}";
        }

        return $"spell-duel:turn:{state.TurnNumber}";
    }

    private static bool IsSpellDuelTimingState(string timingState)
    {
        return string.Equals(timingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            || string.Equals(timingState, TimingStates.SpellDuelClosed, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, BattlefieldState> BuildBattlefieldStates(MatchState state)
    {
        var result = new Dictionary<string, BattlefieldState>(StringComparer.Ordinal);
        foreach (var (zonePlayerId, zones) in state.PlayerZones
            .OrderBy(entry => state.Seats.TryGetValue(entry.Key, out var seat) ? seat : entry.Key, StringComparer.Ordinal))
        {
            foreach (var battlefieldObjectId in zones.Battlefields
                .Where(objectId => IsBattlefieldCardStateObject(state.CardObjects, objectId)))
            {
                var battlefieldObject = state.CardObjects.TryGetValue(battlefieldObjectId, out var knownBattlefieldObject)
                    ? knownBattlefieldObject
                    : new CardObjectState(battlefieldObjectId);
                var battlefieldRelatedObjectIds = state.ObjectLocations
                    .Where(entry => string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                        && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                        && !string.Equals(entry.Key, battlefieldObjectId, StringComparison.Ordinal)
                        && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _)
                        && state.CardObjects.ContainsKey(entry.Key))
                    .Select(entry => entry.Key)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(objectId => objectId, StringComparer.Ordinal)
                    .ToArray();
                var standbyObjectIds = battlefieldRelatedObjectIds
                    .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                        && (cardObject.IsFaceDown))
                    .ToArray();
                var occupantObjectIds = battlefieldRelatedObjectIds
                    .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                        && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                        && !cardObject.IsFaceDown)
                    .ToArray();
                var occupantControllerIds = occupantObjectIds
                    .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                        ? EffectiveFieldControllerId(state, objectId, cardObject)
                        : string.Empty)
                    .Where(controllerId => !string.IsNullOrWhiteSpace(controllerId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                    .ToArray();
                var effectiveBattlefieldControllerId = battlefieldObject.ControllerId;
                var controllerId = string.IsNullOrWhiteSpace(effectiveBattlefieldControllerId)
                    ? null
                    : effectiveBattlefieldControllerId;
                var contested = occupantControllerIds.Length > 1
                    || (occupantControllerIds.Length == 1
                        && (controllerId is null
                            || !string.Equals(controllerId, occupantControllerIds[0], StringComparison.Ordinal)));

                result[battlefieldObjectId] = new BattlefieldState(
                    battlefieldObjectId,
                    zonePlayerId,
                    battlefieldObject.CardNo,
                    controllerId,
                    contested ? "CONTESTED" : controllerId is null ? "UNCONTROLLED" : "CONTROLLED",
                    contested,
                    occupantObjectIds,
                    occupantControllerIds,
                    standbyObjectIds,
                    standbyObjectIds.Count(objectId =>
                        state.CardObjects.TryGetValue(objectId, out var cardObject) && cardObject.IsFaceDown));
            }
        }

        return result;
    }

    private static IReadOnlyList<CleanupTaskState> BuildPendingCleanupTasks(MatchState state)
    {
        var tasks = new List<CleanupTaskState>();
        foreach (var (objectId, cardObject) in state.CardObjects
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (FieldObjectTypeRules.RequiresUnattachedEquipmentRecall(cardObject, state.ObjectLocations.GetValueOrDefault(objectId))
                && state.ObjectLocations.TryGetValue(objectId, out var equipmentLocation)
                && string.Equals(equipmentLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(equipmentLocation.BattlefieldObjectId)
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out _))
            {
                tasks.Add(new CleanupTaskState(
                    $"cleanup:unattached-equipment:{equipmentLocation.BattlefieldObjectId}:{objectId}",
                    "RECALL_UNATTACHED_EQUIPMENT",
                    "UNATTACHED_EQUIPMENT_CLEANUP",
                    EffectiveFieldControllerId(state, objectId, cardObject),
                    objectId,
                    equipmentLocation.BattlefieldObjectId));
            }

            if (!cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !TryFindFieldObjectLocation(state.PlayerZones, objectId, out var location))
            {
                continue;
            }

            if (!CoreRuleEngine.IsLethalFieldUnit(state, cardObject)) continue;

            tasks.Add(new CleanupTaskState(
                $"cleanup:lethal:{objectId}",
                "DESTROY_LETHAL_UNIT",
                "LETHAL_DAMAGE",
                location.PlayerId,
                objectId,
                state.ObjectLocations.TryGetValue(objectId, out var objectLocation)
                    ? objectLocation.BattlefieldObjectId
                    : null));
        }

        foreach (var battlefield in state.BattlefieldStates.Values
            .OrderBy(battlefield => battlefield.BattlefieldObjectId, StringComparer.Ordinal))
        {
            foreach (var standbyObjectId in battlefield.StandbyObjectIds
                .OrderBy(objectId => objectId, StringComparer.Ordinal))
            {
                if (!IsIllegalBattlefieldStandby(state, battlefield, standbyObjectId, out var location))
                {
                    continue;
                }

                tasks.Add(new CleanupTaskState(
                    $"cleanup:illegal-standby:{battlefield.BattlefieldObjectId}:{standbyObjectId}",
                    "REMOVE_ILLEGAL_STANDBY",
                    "BATTLEFIELD_CONTROL_CLEANUP",
                    location.PlayerId,
                    standbyObjectId,
                    battlefield.BattlefieldObjectId));
            }
        }

        foreach (var battlefield in state.BattlefieldStates.Values.Where(battlefield => battlefield.Contested))
        {
            if (HasBattlefieldBattleSkipped(state, battlefield.BattlefieldObjectId))
            {
                continue;
            }

            if (BattlefieldContestRules.IsInitialStaticContest(
                    state,
                    battlefield,
                    tasks.Any(task => IsStateBasedCleanupTask(task.Kind))))
            {
                continue;
            }

            var initiatingPlayerId = BattlefieldContestRules.Initiator(state, battlefield);
            tasks.Add(new CleanupTaskState(
                $"cleanup:battlefield-contested:{battlefield.BattlefieldObjectId}",
                "BATTLEFIELD_CONTESTED",
                "BATTLEFIELD_CONTROL_CHECK",
                initiatingPlayerId,
                null,
                battlefield.BattlefieldObjectId));
            tasks.Add(new CleanupTaskState(
                $"task:start-spell-duel:{battlefield.BattlefieldObjectId}",
                "START_SPELL_DUEL",
                "BATTLEFIELD_CONTESTED",
                initiatingPlayerId,
                null,
                battlefield.BattlefieldObjectId));
            if (BattlefieldHasOpposingOccupants(battlefield))
            {
                tasks.Add(new CleanupTaskState(
                    $"task:start-battle:{battlefield.BattlefieldObjectId}",
                    "START_BATTLE",
                    "SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST",
                    initiatingPlayerId,
                    null,
                    battlefield.BattlefieldObjectId));
            }
        }

        return tasks
            .OrderBy(task => task.TaskId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<BattlefieldTaskState> BuildBattlefieldTaskStates(MatchState state)
    {
        var tasks = new List<BattlefieldTaskState>();
        var hasStateBasedCleanup = state.PendingCleanupTasks.Any(task => IsStateBasedCleanupTask(task.Kind));
        var activeSpellDuelBattlefieldObjectId = ActiveSpellDuelBattlefieldObjectId(state);
        foreach (var battlefield in state.BattlefieldStates.Values
            .Where(battlefield => battlefield.Contested)
            .OrderBy(battlefield => battlefield.BattlefieldObjectId, StringComparer.Ordinal))
        {
            if (HasBattlefieldBattleSkipped(state, battlefield.BattlefieldObjectId))
            {
                continue;
            }

            if (BattlefieldContestRules.IsInitialStaticContest(state, battlefield, hasStateBasedCleanup))
            {
                continue;
            }

            var participantObjectIds = battlefield.OccupantObjectIds
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .ToArray();
            var participantControllerIds = participantObjectIds
                .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    ? EffectiveFieldControllerId(state, objectId, cardObject)
                    : string.Empty)
                .Where(controllerId => !string.IsNullOrWhiteSpace(controllerId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray();
            var stackItemIds = state.StackItems
                .Where(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                    || IsSpellDuelTimingState(state.TimingState))
                .Select(item => item.StackItemId)
                .ToArray();
            var spellDuelCompleted = HasBattlefieldSpellDuelCompleted(state, battlefield.BattlefieldObjectId);
            var spellDuelStatus = string.Equals(activeSpellDuelBattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
                ? "ACTIVE"
                : spellDuelCompleted ? "COMPLETED" : "PENDING";
            var hasOpposingOccupants = BattlefieldHasOpposingOccupants(battlefield);
            var battleStatus = state.BattleState.IsActive
                && string.Equals(state.BattleState.BattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
                    ? "ACTIVE"
                    : state.SpellDuelState.IsActive ? "WAITING_FOR_SPELL_DUEL" : "PENDING";

            tasks.Add(new BattlefieldTaskState(
                $"task:start-spell-duel:{battlefield.BattlefieldObjectId}",
                "START_SPELL_DUEL",
                spellDuelStatus,
                "BATTLEFIELD_CONTESTED",
                battlefield.BattlefieldObjectId,
                participantControllerIds,
                participantObjectIds,
                string.Equals(activeSpellDuelBattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
                    ? state.SpellDuelState.FocusPlayerId
                    : null,
                string.Equals(activeSpellDuelBattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
                    ? stackItemIds
                    : []));
            if (hasOpposingOccupants)
            {
                tasks.Add(new BattlefieldTaskState(
                    $"task:start-battle:{battlefield.BattlefieldObjectId}",
                    "START_BATTLE",
                    battleStatus,
                    "SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST",
                    battlefield.BattlefieldObjectId,
                    participantControllerIds,
                    participantObjectIds,
                    state.BattleState.IsActive ? state.ActivePlayerId : null,
                    []));
            }
        }

        return tasks;
    }

    private static bool BattlefieldHasOpposingOccupants(BattlefieldState battlefield)
    {
        return battlefield.OccupantControllerIds
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .Count() > 1;
    }

    internal static MatchState ClearCompletedMarkersForNewBattlefieldContests(MatchState state, MatchState? previousState)
    {
        if (previousState is null || previousState.SpellDuelState.IsActive || previousState.BattleState.IsActive)
        {
            return state;
        }

        // CN 190.3.a.1: a new arrival contests an uncontrolled/opposing battlefield anew.
        // A completed duel marker belongs to that contest, not to the whole turn.
        var newlyContested = state.BattlefieldStates.Values
            .Where(field => field.Contested
                && (!previousState.BattlefieldStates.TryGetValue(field.BattlefieldObjectId, out var previousField)
                    || !previousField.Contested))
            .SelectMany(field => new[]
            {
                BattlefieldTaskMarkers.SpellDuelCompleted(field.BattlefieldObjectId),
                BattlefieldTaskMarkers.BattleSkipped(field.BattlefieldObjectId)
            }).ToHashSet(StringComparer.Ordinal);
        return newlyContested.Count == 0 ? state : state with
        {
            UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Where(effect => !newlyContested.Contains(effect)).ToArray()
        };
    }

    internal static MatchState ClearStaleBattlefieldBattleSkippedMarkers(
        MatchState state,
        MatchState? previousState)
    {
        if (previousState is null)
        {
            return state;
        }

        var staleBattlefieldObjectIds = state.BattlefieldStates.Values
            .Where(battlefield => HasBattlefieldBattleSkipped(state, battlefield.BattlefieldObjectId)
                && HasBattlefieldBattleSkipped(previousState, battlefield.BattlefieldObjectId)
                && (!previousState.BattlefieldStates.TryGetValue(battlefield.BattlefieldObjectId, out var previousBattlefield)
                    || !BattlefieldHasLegalCombatantsForBattleTask(previousState, previousBattlefield))
                && BattlefieldHasLegalCombatantsForBattleTask(state, battlefield))
            .Select(battlefield => battlefield.BattlefieldObjectId)
            .ToHashSet(StringComparer.Ordinal);
        if (staleBattlefieldObjectIds.Count == 0)
        {
            return state;
        }

        return state with
        {
            UntilEndOfTurnEffects = state.UntilEndOfTurnEffects
                .Where(effectId => !effectId.StartsWith(BattlefieldTaskMarkers.BattleSkippedPrefix, StringComparison.Ordinal)
                    || !staleBattlefieldObjectIds.Contains(effectId[BattlefieldTaskMarkers.BattleSkippedPrefix.Length..]))
                .ToArray()
        };
    }

    private static bool BattlefieldHasLegalCombatantsForBattleTask(MatchState state, BattlefieldState battlefield)
    {
        var battleTaskPlayerId = BattlefieldContestRules.Initiator(state, battlefield);
        if (string.IsNullOrWhiteSpace(battleTaskPlayerId))
        {
            return false;
        }

        var hasReadyAttacker = battlefield.OccupantObjectIds.Any(objectId =>
            IsFaceUpBattlefieldUnitControlledBy(
                state,
                objectId,
                battlefield.BattlefieldObjectId,
                battleTaskPlayerId));
        if (!hasReadyAttacker)
        {
            return false;
        }

        return battlefield.OccupantObjectIds.Any(objectId =>
            IsFaceUpBattlefieldUnitNotControlledBy(
                state,
                objectId,
                battlefield.BattlefieldObjectId,
                battleTaskPlayerId));
    }

    private static bool IsFaceUpBattlefieldUnitControlledBy(
        MatchState state,
        string objectId,
        string battlefieldObjectId,
        string controllerId)
    {
        return IsFaceUpBattlefieldUnitAtBattlefield(state, objectId, battlefieldObjectId, out var cardObject)
            && string.Equals(EffectiveFieldControllerId(state, objectId, cardObject), controllerId, StringComparison.Ordinal);
    }

    private static bool IsFaceUpBattlefieldUnitNotControlledBy(
        MatchState state,
        string objectId,
        string battlefieldObjectId,
        string controllerId)
    {
        return IsFaceUpBattlefieldUnitAtBattlefield(state, objectId, battlefieldObjectId, out var cardObject)
            && !string.Equals(EffectiveFieldControllerId(state, objectId, cardObject), controllerId, StringComparison.Ordinal);
    }

    private static bool IsFaceUpBattlefieldUnitAtBattlefield(
        MatchState state,
        string objectId,
        string battlefieldObjectId,
        out CardObjectState cardObject)
    {
        cardObject = default!;
        if (!state.CardObjects.TryGetValue(objectId, out var knownCardObject)
            || string.IsNullOrWhiteSpace(knownCardObject.CardNo)
            || knownCardObject.IsFaceDown
            || knownCardObject.IsAttacking
            || knownCardObject.IsDefending
            || knownCardObject.IsFaceDown
            || !knownCardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !TryFindFieldObjectLocation(state.PlayerZones, objectId, out _)
            || !state.ObjectLocations.TryGetValue(objectId, out var location)
            || !string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || !string.Equals(location.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
        {
            return false;
        }

        cardObject = knownCardObject;
        return true;
    }

    private static string EffectiveFieldControllerId(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        // A battlefield can be owned while uncontrolled (CN core 190.2).
        if (cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return TryFindFieldObjectLocation(state.PlayerZones, objectId, out var location)
            ? location.PlayerId
            : string.Empty;
    }

    private static PendingTaskQueueState BuildPendingTaskQueue(MatchState state)
    {
        var tasks = state.PendingCleanupTasks
            .OrderBy(task => PendingTaskSortKey(task.Kind))
            .ThenBy(task => task.TaskId, StringComparer.Ordinal)
            .ToArray();
        var activeTask = SelectActivePendingTask(state, tasks);
        var phase = PendingTaskQueuePhase(state, activeTask);

        return new PendingTaskQueueState(
            tasks.Length > 0,
            tasks.Length > 0,
            phase,
            activeTask?.TaskId,
            tasks);
    }

    private static CleanupTaskState? SelectActivePendingTask(MatchState state, IReadOnlyList<CleanupTaskState> tasks)
    {
        if (tasks.Count == 0)
        {
            return null;
        }

        var stateBasedTask = tasks.FirstOrDefault(task => IsStateBasedCleanupTask(task.Kind));
        if (stateBasedTask is not null)
        {
            return stateBasedTask;
        }

        if (state.BattleState.IsActive && (!CoreRuleEngine.IsInitialBattleDuel(state, out var initialDuel)
            || HasBattlefieldSpellDuelCompleted(state, initialDuel.BattlefieldId)))
        {
            var activeBattleTask = tasks.FirstOrDefault(task =>
                string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(state.BattleState.BattlefieldObjectId)
                    || string.Equals(task.BattlefieldObjectId, state.BattleState.BattlefieldObjectId, StringComparison.Ordinal)));
            if (activeBattleTask is not null)
            {
                return activeBattleTask;
            }
        }

        var completedBattlefieldSpellDuelTask = tasks.FirstOrDefault(task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(task.BattlefieldObjectId)
            && HasBattlefieldSpellDuelCompleted(state, task.BattlefieldObjectId));
        if (completedBattlefieldSpellDuelTask is not null)
        {
            return completedBattlefieldSpellDuelTask;
        }

        if (state.SpellDuelState.IsActive)
        {
            var activeSpellDuelTask = tasks.FirstOrDefault(task =>
                string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal));
            if (activeSpellDuelTask is not null)
            {
                return activeSpellDuelTask;
            }
        }

        return tasks[0];
    }

    private static bool HasOwnerOrControllerIdentity(CardObjectState cardObject)
    {
        return !string.IsNullOrWhiteSpace(cardObject.OwnerId)
            || !string.IsNullOrWhiteSpace(cardObject.ControllerId);
    }

    private static string PendingTaskQueuePhase(MatchState state, CleanupTaskState? activeTask)
    {
        if (activeTask is null)
        {
            return "IDLE";
        }

        if (IsStateBasedCleanupTask(activeTask.Kind))
        {
            return "STATE_BASED_CLEANUP";
        }

        if (string.Equals(activeTask.Kind, "START_BATTLE", StringComparison.Ordinal))
        {
            return "BATTLE_TASKS";
        }

        if (string.Equals(activeTask.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
            && state.SpellDuelState.IsActive)
        {
            return "SPELL_DUEL_TASKS";
        }

        return "BATTLEFIELD_TASKS";
    }

    private static int PendingTaskSortKey(string kind)
    {
        return kind switch
        {
            "DESTROY_LETHAL_UNIT" => 0,
            "DESTROY_ZERO_POWER_UNIT" => 1,
            "REMOVE_ILLEGAL_STANDBY" => 2,
            "RECALL_UNATTACHED_EQUIPMENT" => 3,
            "BATTLEFIELD_CONTESTED" => 10,
            "START_SPELL_DUEL" => 20,
            "START_BATTLE" => 30,
            _ => 100
        };
    }

    private static bool IsStateBasedCleanupTask(string kind)
    {
        return string.Equals(kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "DESTROY_ZERO_POWER_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            || string.Equals(kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal);
    }

    private static bool IsIllegalBattlefieldStandby(
        MatchState state,
        BattlefieldState battlefield,
        string objectId,
        out ObjectLocationState location)
    {
        if (!state.ObjectLocations.TryGetValue(objectId, out var objectLocation))
        {
            location = new ObjectLocationState(string.Empty, string.Empty);
            return false;
        }

        location = objectLocation;
        return string.Equals(objectLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
            && !string.Equals(objectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
            && TryFindFieldObjectLocation(state.PlayerZones, objectId, out _)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && (cardObject.IsFaceDown)
            && !string.Equals(
                EffectiveFieldControllerId(state, objectId, cardObject),
                battlefield.ControllerId,
                StringComparison.Ordinal);
    }

    private static bool HasBattlefieldSpellDuelCompleted(MatchState state, string battlefieldObjectId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BattlefieldTaskMarkers.SpellDuelCompleted(battlefieldObjectId),
            StringComparer.Ordinal);
    }

    private static bool HasBattlefieldBattleSkipped(MatchState state, string battlefieldObjectId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BattlefieldTaskMarkers.BattleSkipped(battlefieldObjectId),
            StringComparer.Ordinal);
    }

    private static ContinuousEffectState ApplySourceOrder(
        ContinuousEffectState effect,
        IReadOnlyDictionary<string, int> publicFieldSourceOrders)
    {
        if (string.IsNullOrWhiteSpace(effect.SourceObjectId))
        {
            return effect;
        }

        if (string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal))
        {
            return effect;
        }

        return publicFieldSourceOrders.TryGetValue(effect.SourceObjectId, out var sourceOrder)
            ? effect with { SourceOrder = sourceOrder }
            : effect;
    }

    private static int SourceParticipantCountPowerStaticAuraProjectedPowerAdjustment(
        StaticAuraSpec aura,
        int powerDelta)
    {
        return string.Equals(
            aura.ParticipantScope,
            StaticAuraParticipantScopes.FriendlyPublicFieldEquipment,
            StringComparison.Ordinal)
            ? 0
            : powerDelta;
    }

    private static (
        string EffectIdPrefix,
        string SourcePath,
        string Condition,
        string Lifecycle) SourceParticipantCountPowerStaticAuraProjectionMetadata(StaticAuraSpec aura)
    {
        if (string.Equals(aura.Kind, StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:FRIENDLY_EQUIPMENT_POWER",
                "CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute",
                "SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT",
                "RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER",
                "CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus",
                "SOURCE_AND_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                "RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER",
                "CoreRuleEngine.ResolveSourceSameLocationOtherFriendlyUnitPowerBonus",
                "SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_LOCATION",
                "RECOMPUTED_FROM_CURRENT_SAME_LOCATION_FRIENDLY_UNIT_LOCATIONS");
        }

        return (
            $"STATIC_AURA:{aura.Kind}",
            "CoreRuleEngine.ResolveSourceParticipantCountPowerStaticAuraBonus",
            "SOURCE_PARTICIPANT_COUNT_POWER_SCOPE_MATCHES",
            "RECOMPUTED_FROM_CURRENT_SOURCE_PARTICIPANT_COUNT");
    }

    private static IReadOnlyList<ContinuousEffectState> BuildSourceObjectPowerStaticAuraEffects(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        var effects = new List<ContinuousEffectState>();
        if (string.IsNullOrWhiteSpace(cardObject.CardNo)
            || cardObject.IsFaceDown
            || !cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
            || !IsPublicFieldObjectLocationCompatible(state, objectId, fieldLocation.Zone))
        {
            return effects;
        }

        var dependencyObjectIds = PublicFieldDependencyObjectIds(state, [objectId]);
        foreach (var aura in StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)
            .Where(StaticAuraSpecRules.IsSourceObjectPowerStaticAura))
        {
            if (!SourceObjectPowerStaticAuraApplies(state, objectId, cardObject, aura))
            {
                continue;
            }

            var metadata = SourceObjectPowerStaticAuraProjectionMetadata(aura);
            var delta = StaticAuraSpecRules.SourceObjectPowerDelta(aura, state, cardObject);
            effects.Add(new ContinuousEffectState(
                $"{metadata.EffectIdPrefix}:{objectId}",
                "OBJECT",
                ContinuousEffectLayers.StaticAura,
                aura.Duration,
                objectId,
                objectId,
                delta,
                cardObject.Power,
                cardObject.Power + delta,
                aura.Kind,
                cardObject.CardNo,
                metadata.SourcePath,
                true,
                LayerEngineFoundationResiduals(),
                Condition: metadata.Condition,
                Lifecycle: metadata.Lifecycle,
                ParticipantObjectIds: [objectId],
                SourceDependencyObjectIds: dependencyObjectIds,
                TargetDependencyObjectIds: dependencyObjectIds,
                ParticipantDependencyObjectIds: dependencyObjectIds));
        }

        return effects;
    }
}
