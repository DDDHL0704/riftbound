using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial record ResolutionResult
{

    private static Dictionary<string, object?> BuildTurnWindowSnapshotView(TurnWindowState window)
    {
        return new Dictionary<string, object?>
        {
            ["state"] = window.State,
            ["isSpellDuel"] = window.IsSpellDuel,
            ["isClosed"] = window.IsClosed,
            ["hasStack"] = window.HasStack,
            ["actingPlayerId"] = window.ActingPlayerId
        };
    }

    private static Dictionary<string, object?> BuildSpellDuelSnapshotView(SpellDuelState spellDuel)
    {
        return new Dictionary<string, object?>
        {
            ["isActive"] = spellDuel.IsActive,
            ["isClosed"] = spellDuel.IsClosed,
            ["spellDuelId"] = spellDuel.SpellDuelId,
            ["battlefieldObjectId"] = spellDuel.BattlefieldObjectId,
            ["focusPlayerId"] = spellDuel.FocusPlayerId,
            ["passedFocusPlayerIds"] = spellDuel.PassedFocusPlayerIds,
            ["stackItemIds"] = spellDuel.StackItemIds,
            ["stackControllerIds"] = spellDuel.StackControllerIds
        };
    }

    private static Dictionary<string, object?> BuildBattleSnapshotView(
        MatchState state,
        BattleState battle,
        string viewerPlayerId)
    {
        return new Dictionary<string, object?>
        {
            ["isActive"] = battle.IsActive,
            ["battleId"] = battle.BattleId,
            ["battlefieldObjectId"] = battle.BattlefieldObjectId,
            ["attackerObjectIds"] = VisibleObjectIdsForViewer(state, battle.AttackerObjectIds, viewerPlayerId),
            ["defenderObjectIds"] = VisibleObjectIdsForViewer(state, battle.DefenderObjectIds, viewerPlayerId),
            ["participantControllerIds"] = battle.ParticipantControllerIds,
            ["damageAssignment"] = BuildBattleDamageAssignmentSnapshotView(state, battle, viewerPlayerId)
        };
    }

    private static Dictionary<string, object?> BuildBattleDamageAssignmentSnapshotView(
        MatchState state,
        BattleState battle,
        string viewerPlayerId)
    {
        if (!HasOpenBattleDamageAssignmentWindow(state))
        {
            return new Dictionary<string, object?>
            {
                ["isPending"] = false
            };
        }

        return new Dictionary<string, object?>
        {
            ["isPending"] = true,
            ["phase"] = "DAMAGE_ASSIGNMENT",
            ["battleId"] = battle.BattleId,
            ["battlefieldId"] = battle.BattlefieldObjectId,
            ["assigningPlayerId"] = BattleDamageAssigningPlayerId(state),
            ["damagePool"] = BuildVisibleObjectIntMapSnapshotView(state, BattleDamagePoolFor(state, battle), viewerPlayerId),
            ["legalTargets"] = BuildVisibleBattleDamageLegalTargetsSnapshotView(state, battle, viewerPlayerId),
            ["existingDamage"] = BuildVisibleObjectIntMapSnapshotView(state, BattleExistingDamageFor(state, battle), viewerPlayerId),
            ["lethalDamageThreshold"] = BuildVisibleObjectIntMapSnapshotView(state, BattleLethalDamageThresholdFor(state, battle), viewerPlayerId),
            ["requiredAssignments"] = BuildVisibleBattleRequiredAssignmentsSnapshotView(state, battle, viewerPlayerId)
        };
    }

    private static IReadOnlyDictionary<string, int> BuildVisibleObjectIntMapSnapshotView(
        MatchState state,
        IReadOnlyDictionary<string, int> valuesByObjectId,
        string viewerPlayerId)
    {
        var view = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (objectId, value) in valuesByObjectId)
        {
            view[VisibleObjectIdForViewer(state, objectId, viewerPlayerId)] = value;
        }

        return view;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildVisibleBattleDamageLegalTargetsSnapshotView(
        MatchState state,
        BattleState battle,
        string viewerPlayerId)
    {
        var view = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (sourceObjectId, targetObjectIds) in BattleDamageLegalTargetsFor(state, battle))
        {
            var visibleSourceObjectId = VisibleObjectIdForViewer(state, sourceObjectId, viewerPlayerId);
            if (!view.TryGetValue(visibleSourceObjectId, out var visibleTargetObjectIds))
            {
                visibleTargetObjectIds = [];
                view[visibleSourceObjectId] = visibleTargetObjectIds;
            }

            visibleTargetObjectIds.AddRange(VisibleObjectIdsForViewer(state, targetObjectIds, viewerPlayerId));
        }

        return view.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> BuildVisibleBattleRequiredAssignmentsSnapshotView(
        MatchState state,
        BattleState battle,
        string viewerPlayerId)
    {
        return BattleRequiredAssignmentsFor(state, battle)
            .Select(assignment =>
            {
                var view = new Dictionary<string, object?>(assignment, StringComparer.Ordinal);
                if (view.TryGetValue("sourceObjectId", out var sourceObjectId)
                    && sourceObjectId is string sourceObjectIdValue)
                {
                    view["sourceObjectId"] = VisibleObjectIdForViewer(state, sourceObjectIdValue, viewerPlayerId);
                }

                if (view.TryGetValue("legalTargetObjectIds", out var legalTargetObjectIds)
                    && legalTargetObjectIds is IReadOnlyList<string> targetObjectIds)
                {
                    view["legalTargetObjectIds"] = VisibleObjectIdsForViewer(state, targetObjectIds, viewerPlayerId);
                }

                return (IReadOnlyDictionary<string, object?>)view;
            })
            .ToArray();
    }

    private static Dictionary<string, object?> BuildBattlefieldTaskSnapshotView(
        MatchState state,
        BattlefieldTaskState task,
        string viewerPlayerId)
    {
        var view = new Dictionary<string, object?>
        {
            ["taskId"] = VisibleMetadataIdForViewer(state, task.TaskId, viewerPlayerId),
            ["kind"] = task.Kind,
            ["status"] = task.Status,
            ["reason"] = task.Reason,
            ["battlefieldObjectId"] = task.BattlefieldObjectId,
            ["participantControllerIds"] = task.ParticipantControllerIds,
            ["participantObjectIds"] = VisibleObjectIdsForViewer(state, task.ParticipantObjectIds, viewerPlayerId),
            ["actingPlayerId"] = task.ActingPlayerId,
            ["stackItemIds"] = task.StackItemIds
        };
        if (string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal))
        {
            view["spellDuelId"] = BattleLifecycleIds.SpellDuelIdForBattlefield(task.BattlefieldObjectId);
        }
        else if (string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal))
        {
            view["battleId"] = BattleLifecycleIds.BattleIdForBattlefield(task.BattlefieldObjectId);
        }

        return view;
    }

    private static Dictionary<string, object?> BuildBattlefieldResolutionSnapshotView(
        MatchState state,
        BattlefieldResolutionState resolution,
        string viewerPlayerId)
    {
        return new Dictionary<string, object?>
        {
            ["resolutionId"] = VisibleMetadataIdForViewer(state, resolution.ResolutionId, viewerPlayerId),
            ["tick"] = resolution.Tick,
            ["kind"] = resolution.Kind,
            ["reason"] = resolution.Reason,
            ["battlefieldObjectId"] = resolution.BattlefieldObjectId,
            ["playerId"] = resolution.PlayerId,
            ["previousControllerId"] = resolution.PreviousControllerId,
            ["controllerId"] = resolution.ControllerId,
            ["sourceObjectId"] = resolution.PublicObjectBindings?.Any(b => b.ObjectId == resolution.SourceObjectId) == true
                ? resolution.SourceObjectId : VisibleObjectIdForViewer(state, resolution.SourceObjectId, viewerPlayerId),
            ["participantObjectIds"] = resolution.ParticipantObjectIds.Select(id => resolution.PublicObjectBindings?.Any(b => b.ObjectId == id) == true
                ? id : VisibleObjectIdForViewer(state, id, viewerPlayerId)).ToArray(),
            ["relatedEventKinds"] = resolution.RelatedEventKinds
        };
    }

    private static Dictionary<string, object?> BuildBattleResolutionSnapshotView(
        MatchState state,
        BattleResolutionState resolution,
        string viewerPlayerId)
    {
        string[] References(IReadOnlyList<string> ids) => ids.Select(id => resolution.PublicObjectBindings?.Any(b => b.ObjectId == id) == true
            ? id : VisibleObjectIdForViewer(state, id, viewerPlayerId)!).ToArray();
        return new Dictionary<string, object?>
        {
            ["resolutionId"] = resolution.PublicObjectBindings?.Any(b => b.ObjectId == resolution.AttackerObjectIds.FirstOrDefault()) == true
                ? resolution.ResolutionId : VisibleMetadataIdForViewer(state, resolution.ResolutionId, viewerPlayerId),
            ["tick"] = resolution.Tick,
            ["kind"] = resolution.Kind,
            ["reason"] = resolution.Reason,
            ["battlefieldId"] = resolution.BattlefieldId,
            ["attackingPlayerId"] = resolution.AttackingPlayerId,
            ["defendingPlayerId"] = resolution.DefendingPlayerId,
            ["winnerPlayerId"] = resolution.WinnerPlayerId,
            ["attackerObjectIds"] = References(resolution.AttackerObjectIds),
            ["defenderObjectIds"] = References(resolution.DefenderObjectIds),
            ["survivingAttackerObjectIds"] = References(resolution.SurvivingAttackerObjectIds),
            ["survivingDefenderObjectIds"] = References(resolution.SurvivingDefenderObjectIds),
            ["destroyedObjectIds"] = References(resolution.DestroyedObjectIds),
            ["relatedEventKinds"] = resolution.RelatedEventKinds
        };
    }

    private static Dictionary<string, object?> BuildPendingTaskQueueSnapshotView(
        MatchState state,
        PendingTaskQueueState queue,
        string viewerPlayerId)
    {
        var activeTask = queue.Tasks.FirstOrDefault(task => string.Equals(task.TaskId, queue.ActiveTaskId, StringComparison.Ordinal));
        return new Dictionary<string, object?>
        {
            ["hasTasks"] = queue.HasTasks,
            ["isBlocking"] = queue.IsBlocking,
            ["phase"] = queue.Phase,
            ["activeTaskId"] = activeTask is null
                ? VisibleMetadataIdForViewer(state, queue.ActiveTaskId, viewerPlayerId)
                : VisibleCleanupTaskId(state, activeTask, viewerPlayerId),
            ["tasks"] = queue.Tasks.Select(task => BuildCleanupTaskSnapshotView(state, task, viewerPlayerId)).ToArray(),
            ["metadata"] = new Dictionary<string, object?>
            {
                ["taskCount"] = queue.Tasks.Count,
                ["stateBasedTaskKinds"] = queue.Tasks
                    .Where(task => IsSnapshotStateBasedCleanupTask(task.Kind))
                    .Select(task => task.Kind)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(kind => kind, StringComparer.Ordinal)
                    .ToArray()
            }
        };
    }

    private static bool IsSnapshotStateBasedCleanupTask(string kind)
    {
        return string.Equals(kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "DESTROY_ZERO_POWER_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            || string.Equals(kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal);
    }

    private static bool ShouldHideCleanupTaskObjectId(
        MatchState state,
        CleanupTaskState task,
        string viewerPlayerId)
    {
        return string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(task.ObjectId)
            && IsHiddenBattlefieldStandbyForViewer(state, task.ObjectId, viewerPlayerId);
    }

    private static string? VisibleCleanupTaskId(
        MatchState state,
        CleanupTaskState task,
        string viewerPlayerId)
    {
        if (!ShouldHideCleanupTaskObjectId(state, task, viewerPlayerId))
        {
            return VisibleMetadataIdForViewer(state, task.TaskId, viewerPlayerId);
        }

        var ordinal = HiddenStandbyOrdinal(state, task.ObjectId!, task.BattlefieldObjectId, viewerPlayerId);
        return $"cleanup:illegal-standby:{task.BattlefieldObjectId}:hidden-standby-{ordinal}";
    }

    private static int HiddenStandbyOrdinal(
        MatchState state,
        string objectId,
        string? battlefieldObjectId,
        string viewerPlayerId)
    {
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !state.BattlefieldStates.TryGetValue(battlefieldObjectId, out var battlefield))
        {
            return 1;
        }

        var hiddenIds = battlefield.StandbyObjectIds
            .Where(candidate => IsHiddenBattlefieldStandbyForViewer(state, candidate, viewerPlayerId))
            .OrderBy(candidate => candidate, StringComparer.Ordinal)
            .ToArray();
        var index = Array.IndexOf(hiddenIds, objectId);
        return index < 0 ? 1 : index + 1;
    }

    private static Dictionary<string, object?> BuildCleanupTaskSnapshotView(
        MatchState state,
        CleanupTaskState task,
        string viewerPlayerId)
    {
        var hidesObjectId = ShouldHideCleanupTaskObjectId(state, task, viewerPlayerId);
        var view = new Dictionary<string, object?>
        {
            ["taskId"] = VisibleCleanupTaskId(state, task, viewerPlayerId),
            ["kind"] = task.Kind,
            ["reason"] = task.Reason,
            ["playerId"] = task.PlayerId,
            ["battlefieldObjectId"] = task.BattlefieldObjectId
        };
        if (hidesObjectId)
        {
            view["hiddenObject"] = true;
            view["hiddenObjectKind"] = "BATTLEFIELD_STANDBY";
        }
        else
        {
            view["objectId"] = task.ObjectId;
        }

        return view;
    }

    private static Dictionary<string, object?> BuildPlayerSnapshotView(
        MatchState state,
        HashSet<string> readyPlayers,
        string viewerPlayerId,
        string subjectPlayerId)
    {
        var zones = state.PlayerZones.TryGetValue(subjectPlayerId, out var playerZones)
            ? playerZones
            : PlayerZones.Empty;
        var ownView = string.Equals(viewerPlayerId, subjectPlayerId, StringComparison.Ordinal);

        return new Dictionary<string, object?>
        {
            ["id"] = subjectPlayerId,
            ["name"] = subjectPlayerId,
            ["seat"] = state.Seats[subjectPlayerId],
            ["ready"] = readyPlayers.Contains(subjectPlayerId),
            ["handSize"] = zones.Hand.Count,
            ["score"] = state.PlayerScores.TryGetValue(subjectPlayerId, out var score) ? score : 0,
            ["experience"] = state.PlayerExperience.TryGetValue(subjectPlayerId, out var experience) ? experience : 0,
            ["cardsPlayedThisTurn"] = state.PlayerCardsPlayedThisTurn.TryGetValue(subjectPlayerId, out var cardsPlayedThisTurn)
                ? cardsPlayedThisTurn
                : 0,
            ["deckSubmitted"] = state.PlayerDecklists.ContainsKey(subjectPlayerId),
            ["mulliganCompleted"] = state.MulliganCompletedPlayerIds.Contains(subjectPlayerId, StringComparer.Ordinal),
            ["runePool"] = RunePoolView(state.RunePools.GetValueOrDefault(subjectPlayerId, RunePool.Empty)),
            ["zones"] = BuildZoneSnapshotView(state, zones, ownView, viewerPlayerId),
            ["objects"] = BuildObjectSnapshotView(state, VisibleObjectIds(state, zones, ownView, viewerPlayerId), viewerPlayerId)
        };
    }

    private static Dictionary<string, object?> RunePoolView(RunePool pool)
    {
        var view = new Dictionary<string, object?> { ["mana"] = pool.Mana, ["power"] = pool.TotalPower,
            ["untypedPower"] = pool.Power, ["powerByTrait"] = pool.PowerByTrait };
        if (pool.SpellOnlyMana != 0) view["spellOnlyMana"] = pool.SpellOnlyMana;
        if (pool.DuelOnlyMana != 0) view["duelOnlyMana"] = pool.DuelOnlyMana;
        if (pool.UnitPlayOrUnitAbilityOnlyMana != 0) view["unitPlayOrUnitAbilityOnlyMana"] = pool.UnitPlayOrUnitAbilityOnlyMana;
        if (pool.SpellOnlyPower != 0) view["spellOnlyPower"] = pool.SpellOnlyPower;
        if (pool.EquipmentOnlyPower != 0) view["equipmentOnlyPower"] = pool.EquipmentOnlyPower;
        if (pool.UnitOrEquipmentPlayOnlyPower != 0) view["unitOrEquipmentPlayOnlyPower"] = pool.UnitOrEquipmentPlayOnlyPower;
        return view;
    }

    private static Dictionary<string, object?> BuildZoneSnapshotView(
        MatchState state,
        PlayerZones zones,
        bool ownView,
        string viewerPlayerId)
    {
        var visibleBattlefields = zones.Battlefields
                .Where(objectId => !IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId))
                .ToArray();
        var hiddenBattlefieldStandbyCount = zones.Battlefields.Count(objectId =>
            IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId));
        var baseRunes = BaseRuneObjectIds(state, zones);
        var baseRuneSet = baseRunes.ToHashSet(StringComparer.Ordinal);
        var baseCards = zones.Base
            .Where(objectId => !baseRuneSet.Contains(objectId))
            .ToArray();

        return new Dictionary<string, object?>
        {
            ["mainDeckCount"] = zones.MainDeck.Count,
            ["runeDeckCount"] = zones.RuneDeck.Count,
            ["hand"] = ownView ? zones.Hand : zones.Hand.Where(id => CoreRuleEngine.IsPubliclyRevealedHandCard(state, id)).Order(StringComparer.Ordinal).ToArray(),
            ["handHidden"] = ownView ? 0 : zones.Hand.Count(id => !CoreRuleEngine.IsPubliclyRevealedHandCard(state, id)),
            ["base"] = zones.Base,
            ["baseCards"] = baseCards,
            ["baseRunes"] = baseRunes,
            ["battlefields"] = visibleBattlefields,
            ["battlefieldHiddenStandbyCount"] = hiddenBattlefieldStandbyCount,
            ["graveyard"] = zones.Graveyard,
            ["banished"] = zones.Banished,
            ["legendZone"] = zones.LegendZone,
            ["championZone"] = zones.ChampionZone
        };
    }

    private static IReadOnlyList<string> BaseRuneObjectIds(MatchState state, PlayerZones zones)
    {
        return zones.Base
            .Where(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal))
            .ToArray();
    }

    private static Dictionary<string, object?> BuildLaneSnapshotView(MatchState state, string viewerPlayerId)
    {
        var battlefieldObjectIds = state.PlayerZones
            .OrderBy(entry => state.Seats.TryGetValue(entry.Key, out var seat) ? seat : entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields
                .Where(objectId => !IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId))
                .Select(objectId => new Dictionary<string, object?>
                {
                    ["playerId"] = entry.Key,
                    ["objectId"] = objectId
                }))
            .ToArray();

        return new Dictionary<string, object?>
        {
            ["battlefieldObjectIds"] = battlefieldObjectIds,
            ["battlefieldCount"] = battlefieldObjectIds.Length,
            ["battlefields"] = BuildBattlefieldStateSnapshotView(state, viewerPlayerId)
        };
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildBattlefieldStateSnapshotView(
        MatchState state,
        string viewerPlayerId)
    {
        return state.BattlefieldStates.Values
            .Select(entry => BuildSingleBattlefieldStateSnapshotView(state, entry, viewerPlayerId))
            .ToArray();
    }

    private static Dictionary<string, object?> BuildSingleBattlefieldStateSnapshotView(
        MatchState state,
        BattlefieldState battlefield,
        string viewerPlayerId)
    {
        var pendingTaskKinds = state.PendingCleanupTasks
            .Where(task => string.Equals(task.BattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal))
            .Select(task => task.Kind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToArray();
        var visibleStandbyObjectIds = battlefield.StandbyObjectIds
            .Where(objectId => !IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId))
            .ToArray();
        var standbySlots = battlefield.StandbyObjectIds
            .Select((objectId, index) => BuildStandbySlotSnapshotView(state, battlefield, objectId, index, viewerPlayerId))
            .ToArray();
        var unitsBySide = battlefield.OccupantObjectIds
            .Where(objectId => state.CardObjects.ContainsKey(objectId))
            .GroupBy(
                objectId => EffectiveFieldControllerId(state, objectId, state.CardObjects[objectId]),
                StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)VisibleObjectIdsForViewer(
                    state,
                    group.OrderBy(objectId => objectId, StringComparer.Ordinal),
                    viewerPlayerId),
                StringComparer.Ordinal);
        var scoredByPlayerIds = BattlefieldScoredThisTurnByPlayers(state, battlefield.BattlefieldObjectId);

        return new Dictionary<string, object?>
        {
            ["battlefieldObjectId"] = battlefield.BattlefieldObjectId,
            ["zonePlayerId"] = battlefield.ZonePlayerId,
            ["cardNo"] = battlefield.CardNo,
            ["controllerId"] = battlefield.ControllerId,
            ["status"] = battlefield.Status,
            ["contested"] = battlefield.Contested,
            ["occupantObjectIds"] = VisibleObjectIdsForViewer(state, battlefield.OccupantObjectIds, viewerPlayerId),
            ["occupantControllerIds"] = battlefield.OccupantControllerIds,
            ["unitsBySide"] = unitsBySide,
            ["standbyObjectIds"] = visibleStandbyObjectIds,
            ["standbySlots"] = standbySlots,
            ["standbySlotCount"] = battlefield.StandbyObjectIds.Count,
            ["faceDownStandbyCount"] = battlefield.FaceDownStandbyCount,
            ["hiddenStandbyCount"] = battlefield.StandbyObjectIds.Count - visibleStandbyObjectIds.Length,
            ["scoredThisTurn"] = scoredByPlayerIds.Count > 0,
            ["scoredThisTurnPlayerIds"] = scoredByPlayerIds,
            ["pendingTaskKinds"] = pendingTaskKinds
        };
    }

    private static bool IsBattlefieldCardSnapshotObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> BuildStandbySlotSnapshotView(
        MatchState state,
        BattlefieldState battlefield,
        string objectId,
        int index,
        string viewerPlayerId)
    {
        state.CardObjects.TryGetValue(objectId, out var cardObject);
        state.ObjectLocations.TryGetValue(objectId, out var location);
        var visible = !IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId);
        var controllerId = cardObject is null
            ? location?.PlayerId
            : EffectiveFieldControllerId(state, objectId, cardObject);
        var view = new Dictionary<string, object?>
        {
            ["slotId"] = $"{battlefield.BattlefieldObjectId}:standby:{index + 1}",
            ["battlefieldObjectId"] = battlefield.BattlefieldObjectId,
            ["sidePlayerId"] = location?.PlayerId ?? controllerId,
            ["controllerId"] = controllerId,
            ["visible"] = visible,
            ["state"] = visible ? "VISIBLE" : "HIDDEN",
            ["isFaceDown"] = cardObject?.IsFaceDown ?? false
        };
        if (visible)
        {
            view["objectId"] = objectId;
        }

        return view;
    }

    private static IReadOnlyList<string> BattlefieldScoredThisTurnByPlayers(MatchState state, string battlefieldObjectId)
    {
        return state.UntilEndOfTurnEffects
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

    private static bool IsHiddenBattlefieldStandbyForViewer(
        MatchState state,
        string objectId,
        string viewerPlayerId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || !cardObject.IsFaceDown
            || !state.ObjectLocations.TryGetValue(objectId, out var location)
            || !string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            return false;
        }

        if (CoreRuleEngine.CanInspectFaceDown(state, objectId, viewerPlayerId))
        {
            return false;
        }

        return true;
    }

    internal static string VisibleObjectIdForViewer(
        MatchState state,
        string? objectId,
        string viewerPlayerId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return string.Empty;
        }

        return IsHiddenObjectIdForViewer(state, objectId, viewerPlayerId)
            ? "HIDDEN"
            : objectId;
    }

    private static IReadOnlyList<string> VisibleObjectIdsForViewer(
        MatchState state,
        IEnumerable<string> objectIds,
        string viewerPlayerId)
    {
        return objectIds
            .Select(objectId => VisibleObjectIdForViewer(state, objectId, viewerPlayerId))
            .ToArray();
    }

    private static string? VisibleMetadataIdForViewer(
        MatchState state,
        string? metadataId,
        string viewerPlayerId)
    {
        if (string.IsNullOrWhiteSpace(metadataId))
        {
            return metadataId;
        }

        var visibleId = metadataId;
        foreach (var hiddenObjectId in HiddenObjectIdsForViewer(state, viewerPlayerId)
            .OrderByDescending(objectId => objectId.Length)
            .ThenBy(objectId => objectId, StringComparer.Ordinal))
        {
            visibleId = visibleId.Replace(hiddenObjectId, "HIDDEN", StringComparison.Ordinal);
        }

        return visibleId;
    }

    private static IReadOnlyList<string> HiddenObjectIdsForViewer(
        MatchState state,
        string viewerPlayerId)
    {
        var hiddenObjectIds = new List<string>();
        foreach (var (playerId, zones) in state.PlayerZones)
        {
            if (!string.Equals(playerId, viewerPlayerId, StringComparison.Ordinal))
            {
                hiddenObjectIds.AddRange(zones.Hand);
                hiddenObjectIds.AddRange(zones.MainDeck);
                hiddenObjectIds.AddRange(zones.RuneDeck);
            }

            hiddenObjectIds.AddRange(zones.Battlefields.Where(objectId =>
                IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId)));
        }

        return hiddenObjectIds
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsHiddenObjectIdForViewer(
        MatchState state,
        string objectId,
        string viewerPlayerId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        if (CoreRuleEngine.CanViewObservedDeckCard(state,objectId,viewerPlayerId) || CoreRuleEngine.IsPubliclyRevealedHandCard(state, objectId) || CoreRuleEngine.IsPubliclyRevealedDeckCard(state, objectId)) return false;
        if (IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId))
        {
            return true;
        }

        foreach (var (playerId, zones) in state.PlayerZones)
        {
            if (string.Equals(playerId, viewerPlayerId, StringComparison.Ordinal))
            {
                continue;
            }

            if (zones.Hand.Contains(objectId, StringComparer.Ordinal)
                || zones.MainDeck.Contains(objectId, StringComparer.Ordinal)
                || zones.RuneDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
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

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return state.ObjectLocations.TryGetValue(objectId, out var location)
            ? location.PlayerId
            : string.Empty;
    }

    private static IReadOnlyList<string> VisibleObjectIds(
        MatchState state,
        PlayerZones zones,
        bool ownView,
        string viewerPlayerId)
    {
        var ids = new List<string>();
        if (ownView)
        {
            ids.AddRange(zones.Hand);
        }

        if (!ownView) ids.AddRange(zones.Hand.Where(id => CoreRuleEngine.IsPubliclyRevealedHandCard(state, id)).Order(StringComparer.Ordinal));
        ids.AddRange(zones.MainDeck.Where(id => CoreRuleEngine.CanViewObservedDeckCard(state,id,viewerPlayerId) || CoreRuleEngine.IsPubliclyRevealedDeckCard(state, id)));
        ids.AddRange(zones.Base);
        ids.AddRange(zones.Battlefields.Where(objectId =>
            !IsHiddenBattlefieldStandbyForViewer(state, objectId, viewerPlayerId)));
        ids.AddRange(zones.Graveyard);
        ids.AddRange(zones.Banished);
        ids.AddRange(zones.LegendZone);
        ids.AddRange(zones.ChampionZone);
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, object?> BuildObjectSnapshotView(
        MatchState state,
        IReadOnlyList<string> visibleObjectIds,
        string viewerPlayerId)
    {
        return visibleObjectIds
            .Where(objectId => state.CardObjects.ContainsKey(objectId))
            .ToDictionary(
                objectId => objectId,
                objectId => (object?)BuildCardObjectSnapshotView(state, objectId, viewerPlayerId),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> BuildCardObjectSnapshotView(
        MatchState state,
        string objectId,
        string viewerPlayerId)
    {
        var cardObject = state.CardObjects[objectId];
        var location = ResolveObjectLocation(state, objectId);
        if (cardObject.IsFaceDown && !CoreRuleEngine.CanInspectFaceDown(state, objectId, viewerPlayerId))
        {
            var redacted = new Dictionary<string, object?>
            {
                ["objectId"] = cardObject.ObjectId,
                ["isFaceDown"] = true
            };
            if (location is not null)
            {
                redacted["location"] = BuildObjectLocationSnapshotView(location, cardObject, includeObjectClassHints: false);
            }

            return redacted;
        }

        var view = new Dictionary<string, object?>
        {
            ["objectId"] = cardObject.ObjectId,
            ["damage"] = cardObject.Damage,
            ["power"] = cardObject.Power,
            ["basePower"] = ResolveBasePower(cardObject),
            ["effectivePower"] = SnapshotEffectivePower(state, cardObject, location),
            ["untilEndOfTurnPowerModifier"] = cardObject.UntilEndOfTurnPowerModifier,
            ["isExhausted"] = cardObject.IsExhausted,
            ["isFaceDown"] = cardObject.IsFaceDown,
            ["isAttacking"] = cardObject.IsAttacking,
            ["isDefending"] = cardObject.IsDefending,
            ["tags"] = cardObject.Tags,
            ["untilEndOfTurnEffects"] = cardObject.UntilEndOfTurnEffects.Where(effect => !BattleLimitedMarkers.PrivateMarker(effect)).ToArray(),
            ["manaCost"] = cardObject.ManaCost,
            ["attachedToObjectId"] = cardObject.AttachedToObjectId,
            ["cardNo"] = cardObject.CardNo,
            ["ownerId"] = cardObject.OwnerId,
            ["controllerId"] = cardObject.ControllerId
        };
        if (cardObject.GrantedTags is { Count: > 0 } grantedTags) view["grantedTags"] = grantedTags.ToArray();
        if (cardObject.IsEmpowered) view["isEmpowered"] = true;
        if (cardObject.DamageMarkedByPlayerIds is { Count: > 0 } damageMarkers)
            view["damageMarkedByPlayerIds"] = damageMarkers.ToArray();
        if (cardObject.MovementCountThisTurn > 0)
        {
            view["movementCountThisTurn"] = cardObject.MovementCountThisTurn;
            view["cannotTakeDamage"] = UnitMovementRules.CannotTakeDamage(cardObject);
        }
        if (cardObject.IsFaceDown && CoreRuleEngine.CanInspectFaceDown(state, objectId, viewerPlayerId))
        {
            view["canInspectFaceDown"] = true;
            view["inspectionViewerId"] = viewerPlayerId;
        }
        if (location is not null)
        {
            view["location"] = BuildObjectLocationSnapshotView(location, cardObject, includeObjectClassHints: true);
        }

        return view;
    }

    private static int ResolveBasePower(CardObjectState cardObject)
    {
        return TemporaryUnitEffectRules.Number(cardObject, TemporaryUnitEffectRules.BaseMightPrefix)
            ?? cardObject.Power - cardObject.UntilEndOfTurnPowerModifier;
    }

    internal static int SnapshotEffectivePower(MatchState state, CardObjectState card, ObjectLocationState? location)
    {
        if (card.IsFaceDown || !card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || location?.Zone is not ("BASE" or "BATTLEFIELD")) return card.Power;
        return CoreRuleEngine.ResolveCurrentFieldUnitPower(state, card);
    }

    private static Dictionary<string, object?> BuildObjectLocationSnapshotView(
        ObjectLocationState location,
        CardObjectState cardObject,
        bool includeObjectClassHints)
    {
        var (zoneKind, zoneLabel) = ObjectLocationSnapshotZones.Describe(location, cardObject, includeObjectClassHints);
        var view = new Dictionary<string, object?>
        {
            ["playerId"] = location.PlayerId,
            ["zone"] = location.Zone,
            ["zoneKind"] = zoneKind,
            ["zoneLabel"] = zoneLabel
        };
        if (!string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            view["battlefieldObjectId"] = location.BattlefieldObjectId;
        }

        return view;
    }

    private static ObjectLocationState? ResolveObjectLocation(MatchState state, string objectId)
    {
        if (state.ObjectLocations.TryGetValue(objectId, out var location))
        {
            return location;
        }

        foreach (var (playerId, zones) in state.PlayerZones)
        {
            if (zones.MainDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "MAIN_DECK");
            }
            if (zones.RuneDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "RUNE_DECK");
            }
            if (zones.Hand.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "HAND");
            }
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BASE");
            }
            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BATTLEFIELD");
            }
            if (zones.Graveyard.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "GRAVEYARD");
            }
            if (zones.Banished.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BANISHED");
            }
            if (zones.LegendZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "LEGEND");
            }
            if (zones.ChampionZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "CHAMPION");
            }
        }

        return null;
    }

    public static IReadOnlyDictionary<string, ActionPromptDto> BuildPrompts(MatchState state)
    {
        if (state.PendingRuleChoice is not null) return CoreRuleEngine.BuildRuleChoicePrompts(state);
        if (state.Status != MatchStatuses.InProgress)
        {
            var readyPlayers = state.ReadyPlayerIds.ToHashSet(StringComparer.Ordinal);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var ready = readyPlayers.Contains(playerId);
                var deckSubmitted = state.PlayerDecklists.ContainsKey(playerId);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    !ready && state.Status != MatchStatuses.Finished,
                    ready
                        ? "已准备，等待对手"
                        : deckSubmitted
                            ? "等待玩家准备"
                            : "等待提交合法卡组",
                    ready ? ["WAIT"] : deckSubmitted ? ["READY"] : ["SUBMIT_DECK"]);
            });
        }

        if (string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal))
        {
            var mulliganPlayerId = OpeningMulliganPlayerId(state);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, mulliganPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, mulliganPlayerId, StringComparison.Ordinal)
                    ? "请选择 0 到 2 张起手牌进行调度"
                    : "等待对手完成起手调度",
                string.Equals(playerId, mulliganPlayerId, StringComparison.Ordinal)
                    ? WithSurrender("MULLIGAN")
                    : WithSurrender("WAIT")));
        }

        if (state.PendingEffectPlay is not null) return CoreRuleEngine.BuildEffectPlayPrompts(state);

        if (state.PendingPayment is not null)
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.PendingPayment.PlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PendingPayment.PlayerId, StringComparison.Ordinal)
                    ? "请选择服务端允许的支付项"
                    : "等待对手支付费用",
                string.Equals(playerId, state.PendingPayment.PlayerId, StringComparison.Ordinal)
                    ? WithSurrender(CommandTypes.PayCost, CommandTypes.LegendAct, CommandTypes.ActivateAbility, CommandTypes.TapRune, CommandTypes.RecycleRune)
                    : WithSurrender("WAIT")));
        }

        if (state.PendingHandChoice is not null)
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.PendingHandChoice.PlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PendingHandChoice.PlayerId, StringComparison.Ordinal)
                    ? "请选择要弃置的手牌"
                    : "等待对手选择手牌",
                string.Equals(playerId, state.PendingHandChoice.PlayerId, StringComparison.Ordinal)
                    ? WithSurrender(CommandTypes.ChooseHandCards)
                    : WithSurrender("WAIT")));
        }

        if (state.PendingCardChoice is not null)
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.PendingCardChoice.PlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PendingCardChoice.PlayerId, StringComparison.Ordinal)
                    ? "请选择要处理的卡牌"
                    : "等待对手选择卡牌",
                string.Equals(playerId, state.PendingCardChoice.PlayerId, StringComparison.Ordinal)
                    ? state.PendingCardChoice.ChoiceWindow == CoreRuleEngine.EffectDamageWindow
                        ? WithSurrender(CommandTypes.AssignEffectDamage)
                        : state.PendingCardChoice.ChoiceWindow == "TRIGGER_CONFIRMATION"
                        ? WithSurrender(CommandTypes.ChooseCards, CommandTypes.LegendAct, CommandTypes.ActivateAbility, CommandTypes.TapRune, CommandTypes.RecycleRune)
                        : WithSurrender(CommandTypes.ChooseCards)
                    : WithSurrender("WAIT")));
        }

        if (HasOpenStackPriority(state) || HasOpenBattleResponsePriority(state))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过优先行动权"
                    : "等待对手优先行动",
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? WithSurrender(ActionPromptBuilder.StackPriorityActions(state, playerId))
                    : WithSurrender("WAIT")));
        }

        if (HasOpenSpellDuelFocus(state))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过焦点"
                    : "等待对手焦点行动",
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? WithSurrender(ActionPromptBuilder.SpellDuelFocusActions(state, playerId))
                    : WithSurrender("WAIT")));
        }

        if (HasOpenOrderTriggersWindow(state))
        {
            var orderingPlayerId = OrderTriggersOrderingPlayerId(state);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var canOrderTriggers = string.Equals(playerId, orderingPlayerId, StringComparison.Ordinal);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    canOrderTriggers,
                    canOrderTriggers
                        ? "请提交触发能力的结算顺序"
                        : "等待对手排序触发能力",
                    canOrderTriggers
                        ? WithSurrender(CommandTypes.OrderTriggers)
                        : WithSurrender("WAIT"));
            });
        }

        if (ResolutionResult.HasOpenBattleDamageAssignmentWindow(state))
        {
            var assigningPlayerId = BattleDamageAssigningPlayerId(state);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var canAssignDamage = string.Equals(playerId, assigningPlayerId, StringComparison.Ordinal);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    canAssignDamage,
                    canAssignDamage
                        ? "请提交战斗伤害分配"
                        : "等待对手提交战斗伤害分配",
                    canAssignDamage
                        ? WithSurrender(CommandTypes.AssignCombatDamage)
                        : WithSurrender("WAIT"));
            });
        }

        if (ActiveStartBattleTask(state) is not null)
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var canDeclareBattle = string.Equals(playerId, state.ActivePlayerId, StringComparison.Ordinal)
                    && ActionPromptBuilder.CanDeclareBattleForActiveTask(state, playerId);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    canDeclareBattle,
                    canDeclareBattle
                        ? "请为争夺战场声明战斗"
                        : "等待对手处理争夺战场战斗任务",
                    canDeclareBattle ? WithSurrender("DECLARE_BATTLE") : WithSurrender("WAIT"));
            });
        }

        if (HasBlockingPendingTaskQueue(state))
        {
            var reason = BlockingPendingTaskQueueReason(state);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                false,
                reason,
                WithSurrender("WAIT")));
        }

        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
        {
            var active = playerId == state.ActivePlayerId;
            var prompt = ActionPromptBuilder.Build(state, playerId, active,
                active ? "当前玩家普通开环行动" : "等待对手行动",
                active ? WithSurrender("PLAY_CARD", "ACTIVATE_ABILITY", "ASSEMBLE_EQUIPMENT", "MOVE_UNIT",
                    "DECLARE_BATTLE", "HIDE_CARD", "REVEAL_CARD", "TAP_RUNE", "RECYCLE_RUNE", "LEGEND_ACT", "END_TURN")
                    : WithSurrender("WAIT"));
            // The wire contract exposes candidates with Enabled and a reason. Preserve
            // that same shape for initial, accepted, rejected and recovered positions.
            return prompt;
        });
    }

    private static IReadOnlyList<string> WithSurrender(params string[] actions)
    {
        return WithSurrender((IReadOnlyList<string>)actions);
    }

    private static IReadOnlyList<string> WithSurrender(IReadOnlyList<string> actions)
    {
        return actions.Contains("SURRENDER", StringComparer.Ordinal)
            ? actions
            : actions.Concat(["SURRENDER"]).ToArray();
    }

    private static bool HasOpenStackPriority(MatchState state)
    {
        return state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId);
    }

    private static bool HasOpenBattleResponsePriority(MatchState state)
    {
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && state.StackItems.Count == 0
            && state.PendingPayment is null
            && state.PendingHandChoice is null
            && state.PendingCardChoice is null
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            && state.BattleState.IsActive
            && !string.IsNullOrWhiteSpace(state.BattleState.BattlefieldObjectId);
    }

    private static bool HasOpenSpellDuelFocus(MatchState state)
    {
        return string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId);
    }

    public static string? OpeningMulliganPlayerId(MatchState state)
    {
        var completed = state.MulliganCompletedPlayerIds.ToHashSet(StringComparer.Ordinal);
        return OpeningMulliganOrder(state)
            .FirstOrDefault(playerId => !completed.Contains(playerId));
    }

    private static IReadOnlyList<string> OpeningMulliganOrder(MatchState state)
    {
        var ordered = new List<string>();
        if (!string.IsNullOrWhiteSpace(state.ActivePlayerId)
            && state.Seats.ContainsKey(state.ActivePlayerId))
        {
            ordered.Add(state.ActivePlayerId);
        }

        if (!string.IsNullOrWhiteSpace(state.OpeningSecondActionPlayerId)
            && state.Seats.ContainsKey(state.OpeningSecondActionPlayerId)
            && !ordered.Contains(state.OpeningSecondActionPlayerId, StringComparer.Ordinal))
        {
            ordered.Add(state.OpeningSecondActionPlayerId);
        }

        ordered.AddRange(state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .Where(playerId => !ordered.Contains(playerId, StringComparer.Ordinal)));
        return ordered;
    }
}
