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

    public static ResolutionResult Rejected(
        MatchState state,
        string error,
        string errorCode = ErrorCodes.UnsupportedCommand)
    {
        return new ResolutionResult(false, error, state, [], BuildSnapshots(state), BuildPrompts(state), errorCode);
    }

    public static bool HasBlockingPendingTaskQueue(MatchState state)
    {
        if (state.Status != MatchStatuses.InProgress
            || string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal)
            || string.Equals(state.Phase, MatchPhases.TurnStart, StringComparison.Ordinal)
            || state.PendingHandChoice is not null
            || state.PendingCardChoice is not null
            || state.PendingEffectPlay is not null
            || HasOpenStackPriority(state)
            || HasOpenBattleResponsePriority(state)
            || HasOpenBattleDamageAssignmentWindow(state)
            || HasOpenSpellDuelFocus(state))
        {
            return false;
        }

        return state.PendingTaskQueue.IsBlocking;
    }

    public static CleanupTaskState? ActiveStartBattleTask(MatchState state)
    {
        return state.PendingTaskQueue.Tasks.FirstOrDefault(task =>
            string.Equals(task.TaskId, state.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal)
            && string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
    }

    internal static bool HasOpenOrderTriggersWindow(MatchState state)
    {
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && !string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal)
            && state.PendingPayment is null
            && state.PendingHandChoice is null
            && state.PendingCardChoice is null
            && state.PendingRuleChoice is null
            && state.PendingEffectPlay is null
            && !CoreRuleEngine.HasObservedConfirmationBeforeTriggers(state)
            && !HasOpenStackPriority(state)
            && !HasOpenSpellDuelFocus(state)
            && state.TriggerQueue.Count > 1
            && !string.IsNullOrWhiteSpace(OrderTriggersOrderingPlayerId(state));
    }

    internal static string? OrderTriggersOrderingPlayerId(MatchState state)
    {
        // Each controller chooses only their own simultaneous blocks, in APNAP order.
        if (CoreRuleEngine.NextTriggerChoiceControllerId(state) is { } choosingController)
            return choosingController;

        var controllerIds = state.TriggerQueue
            .Select(trigger => trigger.ControllerId)
            .Where(controllerId => !string.IsNullOrWhiteSpace(controllerId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var firstPlayer = CoreRuleEngine.BattleRoleOrderingPlayer(state) ?? state.TurnPlayerId;
        if (controllerIds.Contains(firstPlayer, StringComparer.Ordinal))
        {
            return firstPlayer;
        }

        return CoreRuleEngine.OrderTriggerApnapControllerIds(state, controllerIds).FirstOrDefault();
    }

    internal static IReadOnlyList<string> OrderTriggerIds(MatchState state)
    {
        return state.TriggerQueue
            .Select(trigger => trigger.TriggerId)
            .Where(triggerId => !string.IsNullOrWhiteSpace(triggerId))
            .ToArray();
    }

    internal static IReadOnlyList<string> OrderTriggerLegalResolutionIds(MatchState state)
        => CoreRuleEngine.TriggerResolutionOrderIds(state);

    internal static IReadOnlyList<IReadOnlyDictionary<string, object?>> OrderTriggerViews(
        MatchState state,
        string viewerPlayerId)
    {
        return state.TriggerQueue
            .Select(trigger => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["triggerId"] = trigger.TriggerId,
                ["sourceObjectId"] = OrderTriggerSourceObjectIdForViewer(state, trigger, viewerPlayerId),
                ["sourceVisibility"] = OrderTriggerSourceObjectIdForViewer(state, trigger, viewerPlayerId) == "HIDDEN" ? "HIDDEN" : "VISIBLE",
                ["controllerId"] = trigger.ControllerId,
                ["effectKind"] = OrderTriggerEffectKindForViewer(state, trigger, viewerPlayerId),
                ["triggeredByEventKind"] = trigger.TriggeredByEventKind,
                ["summary"] = OrderTriggerSummary(state, trigger, viewerPlayerId),
                ["visibleText"] = OrderTriggerVisibleText(state, trigger, viewerPlayerId)
            })
            .ToArray();
    }

    internal static IReadOnlyList<ActionPromptChoiceDto> OrderTriggerChoices(
        MatchState state,
        string viewerPlayerId)
    {
        return state.TriggerQueue
            .Select(trigger => new ActionPromptChoiceDto(
                trigger.TriggerId,
                OrderTriggerSummary(state, trigger, viewerPlayerId),
                OrderTriggerVisibleText(state, trigger, viewerPlayerId)))
            .ToArray();
    }

    internal static IReadOnlyDictionary<string, object?> OrderTriggerLegalOrderingConstraints(MatchState state)
    {
        var controllerBlockOrder = OrderTriggerControllerBlockOrder(state);
        var legalResolutionBlockOrder = controllerBlockOrder.Reverse().ToArray();
        return new Dictionary<string, object?>
        {
            ["orderingPolicy"] = "APNAP_CONTROLLER_BLOCKS_CONSERVATIVE",
            ["orderedTriggerIdsSemantics"] = "STACK_RESOLUTION_ORDER_TOP_FIRST",
            ["mustContainExactly"] = OrderTriggerIds(state),
            ["noDuplicates"] = true,
            ["unknownTriggerIdsRejected"] = true,
            ["crossControllerReorderingAllowed"] = false,
            ["withinControllerReorderingAllowed"] = true,
            ["preserveControllerBlocks"] = true,
            ["preserveTriggerChronology"] = true,
            ["controllerBlockOrder"] = controllerBlockOrder,
            ["legalResolutionControllerBlockOrder"] = legalResolutionBlockOrder,
            ["activePlayerId"] = state.ActivePlayerId,
            ["turnPlayerId"] = state.TurnPlayerId,
            ["orderingPlayerId"] = OrderTriggersOrderingPlayerId(state) ?? string.Empty
        };
    }

    internal static string? BattleDamageAssigningPlayerId(MatchState state)
    {
        var battle = state.BattleState;
        var attackingPlayerId = BattleAttackingPlayerId(state, battle);
        var defendingPlayerId = BattleDefendingPlayerId(state, battle);
        if (string.IsNullOrWhiteSpace(attackingPlayerId))
        {
            return null;
        }

        var acceptedAssignments = BattleDamageAssignmentLedgerFor(state, battle);
        var attackerSourceIds = BattleDamageRequiredSourceIdsForSide(state, battle, battle.AttackerObjectIds);
        if (attackerSourceIds.Count > 0 && !AssignmentsCoverSources(state, battle, acceptedAssignments, attackerSourceIds))
        {
            return attackingPlayerId;
        }

        if (!string.IsNullOrWhiteSpace(defendingPlayerId))
        {
            var defenderSourceIds = BattleDamageRequiredSourceIdsForSide(state, battle, battle.DefenderObjectIds);
            if (defenderSourceIds.Count > 0 && !AssignmentsCoverSources(state, battle, acceptedAssignments, defenderSourceIds))
            {
                return defendingPlayerId;
            }
        }

        return null;
    }

    internal static IReadOnlyList<string> BattleDamageRequiredSourceIdsForPlayer(
        MatchState state,
        BattleState battle,
        string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return [];
        }

        var acceptedAssignments = BattleDamageAssignmentLedgerFor(state, battle);
        var attackingPlayerId = BattleAttackingPlayerId(state, battle);
        var defendingPlayerId = BattleDefendingPlayerId(state, battle);
        var attackerSourceIds = BattleDamageRequiredSourceIdsForSide(state, battle, battle.AttackerObjectIds);
        if (string.Equals(playerId, attackingPlayerId, StringComparison.Ordinal))
        {
            if (attackerSourceIds.Count > 0 && !AssignmentsCoverSources(state, battle, acceptedAssignments, attackerSourceIds))
            {
                return attackerSourceIds;
            }
        }

        var defenderSourceIds = BattleDamageRequiredSourceIdsForSide(state, battle, battle.DefenderObjectIds);
        if (string.Equals(playerId, defendingPlayerId, StringComparison.Ordinal))
        {
            if ((attackerSourceIds.Count == 0 || AssignmentsCoverSources(state, battle, acceptedAssignments, attackerSourceIds))
                && defenderSourceIds.Count > 0
                && !AssignmentsCoverSources(state, battle, acceptedAssignments, defenderSourceIds))
            {
                return defenderSourceIds;
            }
        }

        return [];
    }

    private static IReadOnlyList<string> BattleDamageRequiredSourceIdsForSide(
        MatchState state,
        BattleState battle,
        IReadOnlyList<string> sideSourceObjectIds)
    {
        var damagePool = BattleDamagePoolFor(state, battle);
        var legalTargets = BattleDamageLegalTargetsFor(state, battle);
        return sideSourceObjectIds
            .Where(objectId => battle.ParticipantControllerIds.TryGetValue(objectId, out var controllerId)
                && !string.IsNullOrWhiteSpace(controllerId)
                && damagePool.TryGetValue(objectId, out var damage)
                && damage > 0
                && legalTargets.TryGetValue(objectId, out var targets)
                && targets.Count > 0)
            .ToArray();
    }

    internal static IReadOnlyList<CombatDamageAssignmentDto> BattleDamageAssignmentLedgerFor(
        MatchState state,
        BattleState battle)
    {
        return TryReadBattleDamageAssignmentLedger(state, battle, out var assignments)
            ? assignments
            : [];
    }

    private static bool AssignmentsCoverSources(
        MatchState state,
        BattleState battle,
        IReadOnlyList<CombatDamageAssignmentDto> assignments,
        IReadOnlyList<string> sourceObjectIds)
    {
        var damagePool = BattleDamagePoolFor(state, battle);
        foreach (var sourceObjectId in sourceObjectIds)
        {
            var expectedDamage = damagePool.TryGetValue(sourceObjectId, out var damage)
                ? damage
                : 0;
            var assignedDamage = assignments
                .Where(assignment => string.Equals(assignment.SourceObjectId, sourceObjectId, StringComparison.Ordinal))
                .Sum(assignment => assignment.Damage);
            if (assignedDamage != expectedDamage)
            {
                return false;
            }
        }

        return true;
    }

    private static string? BattleAttackingPlayerId(MatchState state, BattleState battle)
    {
        var attackerControllerIds = battle.AttackerObjectIds
            .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                ? EffectiveFieldControllerId(state, objectId, cardObject)
                : battle.ParticipantControllerIds.TryGetValue(objectId, out var controllerId)
                    ? controllerId
                    : string.Empty)
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return attackerControllerIds.Length == 1 ? attackerControllerIds[0] : null;
    }

    private static string? BattleDefendingPlayerId(MatchState state, BattleState battle)
    {
        var defenderControllerIds = battle.DefenderObjectIds
            .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                ? EffectiveFieldControllerId(state, objectId, cardObject)
                : battle.ParticipantControllerIds.TryGetValue(objectId, out var controllerId)
                    ? controllerId
                    : string.Empty)
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return defenderControllerIds.Length == 1 ? defenderControllerIds[0] : null;
    }

    private static bool TryReadBattleDamageAssignmentLedger(
        MatchState state,
        BattleState battle,
        out IReadOnlyList<CombatDamageAssignmentDto> assignments)
    {
        assignments = [];
        var marker = state.UntilEndOfTurnEffects.FirstOrDefault(effectId =>
            effectId.StartsWith(BattleDamageAssignmentLedgerPrefix, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(marker))
        {
            return false;
        }

        try
        {
            var encoded = marker[BattleDamageAssignmentLedgerPrefix.Length..];
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var parsed = JsonSerializer.Deserialize<BattleDamageAssignmentLedger>(json);
            if (parsed is null
                || !string.Equals(parsed.BattleId, battle.BattleId, StringComparison.Ordinal)
                || !string.Equals(parsed.BattlefieldId, battle.BattlefieldObjectId, StringComparison.Ordinal))
            {
                return false;
            }

            assignments = parsed.Assignments
                .Where(assignment => !string.IsNullOrWhiteSpace(assignment.SourceObjectId)
                    && !string.IsNullOrWhiteSpace(assignment.TargetObjectId)
                    && assignment.Damage > 0)
                .ToArray();
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static IReadOnlyDictionary<string, int> BattleDamagePoolFor(MatchState state, BattleState battle)
    {
        return BattleParticipantObjectIds(battle)
            .ToDictionary(
                objectId => objectId,
                objectId => Math.Max(0, CoreRuleEngine.ResolveAssignmentBattleCombatPower(state, battle, objectId, out _, out _)),
                StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> BattleDamageLegalTargetsFor(
        MatchState state,
        BattleState battle)
    {
        var orderedAttackers = OrderedBattleDamageTargetObjectIds(state, battle.AttackerObjectIds);
        var orderedDefenders = OrderedBattleDamageTargetObjectIds(state, battle.DefenderObjectIds);
        return BattleParticipantObjectIds(battle)
            .ToDictionary(
                objectId => objectId,
                objectId => BattleLegalTargetsForSource(battle, objectId, orderedAttackers, orderedDefenders),
                StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, int> BattleExistingDamageFor(MatchState state, BattleState battle)
    {
        return BattleParticipantObjectIds(battle)
            .ToDictionary(
                objectId => objectId,
                objectId => state.CardObjects.TryGetValue(objectId, out var cardObject) ? cardObject.Damage : 0,
                StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, int> BattleLethalDamageThresholdFor(MatchState state, BattleState battle)
    {
        return CoreRuleEngine.BuildCombatLethalDamageThreshold(state, battle);
    }

    internal static IReadOnlyList<IReadOnlyDictionary<string, object?>> BattleRequiredAssignmentsFor(MatchState state, BattleState battle)
    {
        var damagePool = BattleDamagePoolFor(state, battle);
        var legalTargets = BattleDamageLegalTargetsFor(state, battle);
        return damagePool
            .Where(entry => entry.Value > 0 && legalTargets.TryGetValue(entry.Key, out var targets) && targets.Count > 0)
            .Select(entry => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["sourceObjectId"] = entry.Key,
                ["damage"] = entry.Value,
                ["legalTargetObjectIds"] = legalTargets[entry.Key]
            })
            .ToArray();
    }

    internal static int BattleEffectivePowerFor(MatchState state, string objectId)
    {
        return BattleEffectivePowerFor(state, state.BattleState, objectId);
    }

    private static int BattleEffectivePowerFor(MatchState state, BattleState battle, string objectId)
        => CoreRuleEngine.ResolveAssignmentBattleCombatPower(state, battle, objectId, out _, out _, damageContribution: false);

    private static bool IsStunnedForBattle(CardObjectState cardObject)
    {
        return cardObject.UntilEndOfTurnEffects.Contains("STUNNED", StringComparer.Ordinal)
            || cardObject.Tags.Contains("眩晕", StringComparer.Ordinal);
    }

    internal static IReadOnlyList<string> BattleParticipantObjectIds(BattleState battle)
    {
        return battle.AttackerObjectIds
            .Concat(battle.DefenderObjectIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<string> BattleLegalTargetsForSource(
        BattleState battle,
        string sourceObjectId,
        IReadOnlyList<string>? orderedAttackers = null,
        IReadOnlyList<string>? orderedDefenders = null)
    {
        if (battle.AttackerObjectIds.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            return orderedDefenders ?? battle.DefenderObjectIds.ToArray();
        }

        if (battle.DefenderObjectIds.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            return orderedAttackers ?? battle.AttackerObjectIds.ToArray();
        }

        return [];
    }

    internal static string BattleParticipantRole(BattleState battle, string objectId)
    {
        if (battle.AttackerObjectIds.Contains(objectId, StringComparer.Ordinal))
        {
            return "ATTACKER";
        }

        return battle.DefenderObjectIds.Contains(objectId, StringComparer.Ordinal)
            ? "DEFENDER"
            : "UNKNOWN";
    }

    public static string BlockingPendingTaskQueueReason(MatchState state)
    {
        var queue = state.PendingTaskQueue;
        var activeTask = queue.Tasks.FirstOrDefault(task => string.Equals(task.TaskId, queue.ActiveTaskId, StringComparison.Ordinal))
            ?? queue.Tasks.FirstOrDefault();
        if (activeTask is null)
        {
            return "等待服务端处理任务队列";
        }

        return $"等待服务端处理任务队列：{PendingTaskKindLabel(activeTask.Kind)}";
    }

    private static string PendingTaskKindLabel(string kind)
    {
        return kind switch
        {
            "BATTLEFIELD_CONTESTED" => "战场控制检查",
            "DESTROY_LETHAL_UNIT" => "致命伤害清理",
            "DESTROY_ZERO_POWER_UNIT" => "0 战力清理",
            "REMOVE_ILLEGAL_STANDBY" => "待命清理",
            "RECALL_UNATTACHED_EQUIPMENT" => "装备清理",
            "START_BATTLE" => "开始战斗",
            "START_SPELL_DUEL" => "开始法术对决",
            _ => "服务端任务"
        };
    }

    public static IReadOnlyDictionary<string, SnapshotDto> BuildSnapshots(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => BuildSnapshotForViewer(state, playerId),
            StringComparer.Ordinal);
    }

    public static SnapshotDto BuildSpectatorSnapshot(MatchState state)
    {
        return BuildSnapshotForViewer(state, "__spectator__");
    }

    private static SnapshotDto BuildSnapshotForViewer(MatchState state, string viewerPlayerId)
    {
        state = CoreRuleEngine.VisibleRuleChoiceState(state);
        var readyPlayers = state.ReadyPlayerIds.ToHashSet(StringComparer.Ordinal);
        var players = state.Seats.ToDictionary(
            entry => entry.Key,
            entry => (object?)BuildPlayerSnapshotView(state, readyPlayers, viewerPlayerId, entry.Key),
            StringComparer.Ordinal);

        return new SnapshotDto(
            state.Tick,
            state.TurnNumber,
            state.ActivePlayerId,
            players,
            BuildLaneSnapshotView(state, viewerPlayerId),
            state.StackItems.Select(item => (object?)BuildStackItemSnapshotView(state, item, viewerPlayerId)).ToArray(),
            new Dictionary<string, object?>
            {
                ["phase"] = state.Phase,
                ["turnStartStep"] = state.TurnStartStep,
                ["timingState"] = state.TimingState,
                ["turnPlayerId"] = state.TurnPlayerId,
                ["priorityPlayerId"] = state.PendingRuleChoice is null ? state.PriorityPlayerId : null,
                ["passedPriorityPlayerIds"] = state.PassedPriorityPlayerIds,
                ["focusPlayerId"] = state.PendingRuleChoice is null ? state.FocusPlayerId : null,
                ["passedFocusPlayerIds"] = state.PassedFocusPlayerIds,
                ["winnerPlayerId"] = state.WinnerPlayerId,
                ["destroyedUnitOwnerIdsThisTurn"] = state.DestroyedUnitOwnerIdsThisTurn,
                ["turnWindow"] = BuildTurnWindowSnapshotView(state.TurnWindow),
                ["spellDuel"] = BuildSpellDuelSnapshotView(state.SpellDuelState),
                ["battle"] = BuildBattleSnapshotView(state, state.BattleState, viewerPlayerId),
                ["battleResolutions"] = state.BattleResolutions
                    .Select(resolution => BuildBattleResolutionSnapshotView(state, resolution, viewerPlayerId))
                    .ToArray(),
                ["battlefieldTasks"] = state.BattlefieldTasks
                    .Select(task => BuildBattlefieldTaskSnapshotView(state, task, viewerPlayerId))
                    .ToArray(),
                ["battlefieldResolutions"] = state.BattlefieldResolutions
                    .Select(resolution => BuildBattlefieldResolutionSnapshotView(state, resolution, viewerPlayerId))
                    .ToArray(),
                ["pendingTaskQueue"] = BuildPendingTaskQueueSnapshotView(state, state.PendingTaskQueue, viewerPlayerId),
                ["pendingPayment"] = BuildPendingPaymentSnapshotView(state, CoreRuleEngine.VisiblePendingPayment(state)),
                ["ruleQueueCoverage"] = BuildRuleQueueCoverageSnapshotView(state),
                ["pendingHandChoice"] = BuildPendingHandChoiceSnapshotView(state, state.PendingHandChoice, viewerPlayerId),
                ["pendingCardChoice"] = BuildPendingCardChoiceSnapshotView(state, viewerPlayerId),
                ["pendingEffectPlay"] = CoreRuleEngine.EffectPlayView(state, VisibleObjectIdForViewer(state, state.PendingEffectPlay?.Parent.SourceObjectId, viewerPlayerId)),
                ["continuousEffects"] = state.ContinuousEffects.Select(BuildContinuousEffectSnapshotView).ToArray(),
                ["triggerQueue"] = state.TriggerQueue
                    .Select(trigger => BuildTriggerQueueItemSnapshotView(state, trigger, viewerPlayerId))
                    .ToArray(),
                ["winningScore"] = EffectiveWinningScore(state),
                ["roomStatus"] = state.Status,
                ["readyPlayerIds"] = state.ReadyPlayerIds
            },
            state.TimingState,
            BuildTableSnapshotView(state, viewerPlayerId));
    }

    private static SnapshotTableDto BuildTableSnapshotView(MatchState state, string viewerPlayerId)
    {
        var players = state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => BuildTablePlayerSnapshotView(state, viewerPlayerId, entry.Key, entry.Value))
            .ToArray();
        var battlefields = state.BattlefieldStates.Values
            .Select((battlefield, index) => BuildTableBattlefieldSnapshotView(state, battlefield, index, viewerPlayerId))
            .ToArray();

        return new SnapshotTableDto(
            "server-snapshot",
            viewerPlayerId,
            OfficialDeckValidator.RuneDeckCount,
            players,
            battlefields);
    }

    private static SnapshotTablePlayerDto BuildTablePlayerSnapshotView(
        MatchState state,
        string viewerPlayerId,
        string subjectPlayerId,
        string seat)
    {
        var zones = state.PlayerZones.TryGetValue(subjectPlayerId, out var playerZones)
            ? playerZones
            : PlayerZones.Empty;
        var isViewer = string.Equals(viewerPlayerId, subjectPlayerId, StringComparison.Ordinal);
        var perspective = isViewer
            ? "self"
            : string.Equals(viewerPlayerId, "__spectator__", StringComparison.Ordinal)
                ? "spectator"
                : "opponent";
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

        var tableZones = new SnapshotTablePlayerZonesDto(
            zones.MainDeck.Count,
            zones.RuneDeck.Count,
            isViewer ? zones.Hand : zones.Hand.Where(id => CoreRuleEngine.IsPubliclyRevealedHandCard(state, id)).Order(StringComparer.Ordinal).ToArray(),
            isViewer ? 0 : zones.Hand.Count(id => !CoreRuleEngine.IsPubliclyRevealedHandCard(state, id)),
            zones.Base,
            baseCards,
            baseRunes,
            visibleBattlefields,
            hiddenBattlefieldStandbyCount,
            zones.Graveyard,
            zones.Banished,
            zones.LegendZone,
            zones.ChampionZone);

        return new SnapshotTablePlayerDto(
            subjectPlayerId,
            seat,
            perspective,
            isViewer,
            tableZones);
    }

    private static SnapshotTableBattlefieldDto BuildTableBattlefieldSnapshotView(
        MatchState state,
        BattlefieldState battlefield,
        int index,
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
            .Select((objectId, slotIndex) => BuildTableStandbySlotSnapshotView(state, battlefield, objectId, slotIndex, viewerPlayerId))
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

        return new SnapshotTableBattlefieldDto(
            index,
            battlefield.BattlefieldObjectId,
            battlefield.ZonePlayerId,
            battlefield.CardNo,
            battlefield.ControllerId,
            battlefield.Status,
            battlefield.Contested,
            VisibleObjectIdsForViewer(state, battlefield.OccupantObjectIds, viewerPlayerId),
            battlefield.OccupantControllerIds,
            unitsBySide,
            visibleStandbyObjectIds,
            standbySlots,
            battlefield.StandbyObjectIds.Count,
            battlefield.FaceDownStandbyCount,
            battlefield.StandbyObjectIds.Count - visibleStandbyObjectIds.Length,
            scoredByPlayerIds.Count > 0,
            scoredByPlayerIds,
            pendingTaskKinds);
    }

    private static SnapshotTableStandbySlotDto BuildTableStandbySlotSnapshotView(
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

        return new SnapshotTableStandbySlotDto(
            $"{battlefield.BattlefieldObjectId}:standby:{index + 1}",
            battlefield.BattlefieldObjectId,
            location?.PlayerId ?? controllerId,
            controllerId,
            visible,
            visible ? "VISIBLE" : "HIDDEN",
            cardObject?.IsFaceDown ?? false,
            visible ? objectId : null);
    }

    private static int EffectiveWinningScore(MatchState state) =>
        BaseWinningScore + BattlefieldLocalRules.WinningScoreIncrease(state.PlayerZones, state.CardObjects);
}
