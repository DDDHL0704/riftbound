using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendBattleWinDrawContext(ObjectBinding Source, string CardNo, string ControllerId,
    string BattlefieldId, long ResultTick, string Role);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleResultContinuation(string BattlefieldId, string AttackingPlayerId,
    string? DefendingPlayerId, string? WinnerPlayerId, long ResultTick,
    IReadOnlyList<string> AttackerObjectIds, IReadOnlyList<string> DefenderObjectIds,
    IReadOnlyList<string> SurvivingAttackerObjectIds, IReadOnlyList<string> SurvivingDefenderObjectIds,
    IReadOnlyList<string> DestroyedObjectIds, IReadOnlyList<string> RelatedEventKinds,
    IReadOnlyList<ObjectBinding> PublicObjectBindings, int AssignedOverkillDamage, string Stage)
{
    [JsonRequired]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool AttackerRecalledByBattleCleanup { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string CleanupRecallNoResultReason = "BATTLE_CLEANUP_ATTACKER_RECALL";

    private static ResolutionResult CollectResolutionEffects(MatchState state, ResolutionResult result, int roleEventOffset = 0)
    {
            result = RecordHandDiscardObservers(result);
            result = QueueEmpoweredBanish(result);
            result = CaptureDuelChainFocus(state, result);
            result = CollectObservedDeckPlays(result);
            result = EndDetachedUnitCopies(state, result);
            result = RefreshRuleDerivedKeywordLayers(result);
            result = ExpireClosedBattleLimitedEffects(result);
            result = QueueGrantedPredict(state, result);
            result = QueuePrintedPredict(state, result);
            result = QueueSourcePlayDraw(state, result);
            result = QueueUnitPlayMight(state, result);
            result = QueueSivirRuneRecycles(result);
            result = QueueRecycledCardBoons(result);
            if (result.State.RuleTextModeUses is { } uses && uses.TurnNumber != result.State.TurnNumber)
                result = result with { State = result.State with { RuleTextModeUses = null } };
            result = QueueReflexiveDuels(RecordDeathObservers(state, RecordDelayedDestruction(state, RecordDamageDestruction(state, RecordAttachmentObservers(state, RecordMovementObservers(ApplyNestedDestructionContinuations(result)))))));
            var collected = QueueTempered(state, RecordDrawTriggers(state, ResolveImmediateResourceTriggers(ApplyFriendlyEquipmentStaticPowerRecompute(
                QueueUnitEntryTriggers(state, QueueCreatedTokenReady(QueueReflexiveCopies(QueueBattlefieldReturnTriggers(QueueDeathRevealTriggers(QueueInsightEventTriggers(FinalizeTokenDepartures(state, result)))))))))));
            // New field objects need their current incarnation before role and
            // Mighty observers capture bindings. The final pass is idempotent
            // and also binds stack items created by those observers.
            collected = ApplyObjectContinuity(state, collected);
            collected = RecordDeckPlayReflexiveCard(state, collected);
            collected = AdvanceDeckPlayReflexives(collected);
            collected = DeferCompanionPermanentPlayOrdering(state, collected);
            collected = QueueTurnUnitPlayBoons(state, collected);
            collected = QueueStandbyPlayedObservers(collected);
            collected = RecordBattleDuelRoles(state, collected, roleEventOffset);
            collected = RecordMightyTransitions(state, collected);
            collected = ResumeDuelChainFocus(collected);
            collected = StampNewTriggerQueueTimes(state, collected);
            // A physical play has completed before its earned skills confirm,
            // including a checked target intent supplied in the same command.
            collected = RecordCardPlayHistory(state, collected);
            var completed = RecordCardPlayHistory(state, RefreshRuleDerivedKeywordLayers(ApplyObjectContinuity(state,
                PrepareTriggerConfirmation(AdvanceTurnStartSequence(AdvanceEndStep(AdvanceObservedDeckPlays(PrepareTriggerConfirmation(PublishPendingTriggers(CaptureDeathTriggerSources(state,
                    RecordCombatMightSourceLocations(state, RecordDistributionSourceLocations(state, collected))))))))))));
            return StampNewTriggerQueueTimes(state, completed);
    }

    internal const string LegendBattleWinDrawEffect = "CAPTURED_LEGEND_BATTLE_WIN_DRAW";

    // CN468.1-468.2: finish battle cleanup and its earned FEPR before deciding
    // an outcome. Empty queues advance in Complete without a response window.
    private static bool TryBeginBattleResultContinuation(MatchState resultState,
        string battlefield, string attacker, string? defender,
        IReadOnlyList<string> attackers, IReadOnlyList<string> defenders,
        int overkill, List<GameEvent> events, out MatchState pending)
    {
        resultState = resultState with { Tick = RuleChoices.Value?.CompletionTick ?? resultState.Tick };
        pending = resultState;
        if (defender is null || attackers.Count == 0 || defenders.Count == 0) return false;
        var zones = NormalizeZonesForSeats(resultState);
        var cards = resultState.CardObjects.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var cleanupStart = events.Count;
        ApplyBattleCleanup(zones, cards, battlefield, attackers, defenders, events);
        // A response can remove an earlier role unit before the actual damage participants are declared.
        var bindings = TryReadBattleResponseDeclarationContext(resultState, battlefield, out var declaration)
            ? declaration.PublicObjectBindings?.Where(b => attackers.Contains(b.ObjectId) || defenders.Contains(b.ObjectId)).ToArray()
            : CaptureBattlePublicBindings(resultState, attackers.Concat(defenders));
        var continuation = new BattleResultContinuation(battlefield, attacker, defender, null, resultState.Tick,
            attackers.ToArray(), defenders.ToArray(), SurvivingBattleUnitObjectIds(zones, cards, attackers),
            SurvivingBattleUnitObjectIds(zones, cards, defenders),
            events.Where(e => e.Kind == "UNIT_DESTROYED").Select(e => EventPayloadString(e, "targetObjectId")!)
                .Where(id => attackers.Concat(defenders).Contains(id)).Distinct(StringComparer.Ordinal).ToArray(),
            events.Where(e => e.Kind is "DAMAGE_APPLIED" or "UNIT_DESTROYED" or "DAMAGE_REMOVED" or "UNIT_RECALLED_TO_BASE")
                .Select(e => e.Kind).Distinct(StringComparer.Ordinal).ToArray(), bindings ?? [], overkill, "CLEANUP_TRIGGERS")
        {
            AttackerRecalledByBattleCleanup = events.Skip(cleanupStart).Any(e => e.Kind == "UNIT_RECALLED_TO_BASE"
                && EventPayloadString(e, "reason") == "BATTLE_CLEANUP_ATTACKER_RECALL")
        };
        events.Add(new("BATTLE_CLEANUP_COMPLETED", "先处理战斗伤害与清理触发，再判定结果",
            new Dictionary<string, object?> { ["continuation"] = continuation }));
        pending = resultState with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(resultState.ObjectLocations, zones), PendingBattleResult = continuation,
            TimingState = TimingStates.NeutralClosed, FocusPlayerId = null, PriorityPlayerId = null,
            PassedFocusPlayerIds = [], PassedPriorityPlayerIds = [] };
        return true;
    }

    private static MatchState DetermineBattleResultAfterCleanup(MatchState resultState,
        BattleResultContinuation cleanup, List<GameEvent> events)
    {
        resultState = resultState with { Tick = RuleChoices.Value?.CompletionTick ?? resultState.Tick };
        var battlefield = cleanup.BattlefieldId;
        var attacker = cleanup.AttackingPlayerId;
        var defender = cleanup.DefendingPlayerId!;
        var liveAttackers = LiveParty(attacker);
        var liveDefenders = LiveParty(defender);
        var attackers = cleanup.AttackerObjectIds.Where(id => !liveDefenders.Contains(id))
            .Concat(liveAttackers).Distinct(StringComparer.Ordinal).ToArray();
        var defenders = cleanup.DefenderObjectIds.Where(id => !liveAttackers.Contains(id))
            .Concat(liveDefenders).Distinct(StringComparer.Ordinal).ToArray();
        var winner = !cleanup.AttackerRecalledByBattleCleanup && TryResolveBattleWinnerPlayerId(
            resultState.PlayerZones, resultState.CardObjects, liveAttackers, liveDefenders, defender, attacker, out var determined)
                ? determined : null;
        var role = winner == attacker ? "ATTACKER" : "DEFENDER";
        var queued = new List<TriggerQueueItemState>();
        if (winner is not null) queued.AddRange(CaptureUnitBattleWinDrawObservations(resultState, battlefield, winner,
            attackers, defenders, resultState.Tick).Select(x => x.Trigger with { UnitBattleWinDraw = x.Context }));
        if (winner is not null) queued.AddRange(CaptureKhazixBattleWinObservations(resultState, battlefield, winner,
            role, resultState.Tick).Select(x => x.Trigger with { KhazixBattleWin = x.Context }));
        if (winner is not null && resultState.PlayerZones.TryGetValue(winner, out var winnerZones))
            foreach (var id in winnerZones.LegendZone)
                if (resultState.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown
                    && card.ControllerId == winner && LegendCardHasIdentity(card.CardNo, DravenLegendIdentityId))
                {
                    var context = new LegendBattleWinDrawContext(new(id, card.ObjectGeneration), card.CardNo!,
                        winner, battlefield, resultState.Tick, role);
                    queued.Add(new($"legend-battle-win-{resultState.Tick}-{battlefield}-{id}", winner, id,
                        LegendBattleWinDrawEffect, "BATTLE_RESULT_DETERMINED")
                    { LegendBattleWinDraw = context, SourceCardNo = card.CardNo, EnqueuedAtTick = resultState.Tick });
                }
        var scoreObservations = winner is null ? Array.Empty<CapturedDravenBattleWinScore>()
            : CaptureDravenBattleWinScoreObservations(resultState, battlefield, winner,
                attackers, defenders, resultState.Tick);
        queued.AddRange(scoreObservations.Select(x => x.Trigger with { DravenBattleWinScore = x.Context }));
        var zones = NormalizeZonesForSeats(resultState);
        var cards = resultState.CardObjects.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var bindings = cleanup.PublicObjectBindings.Concat(CaptureBattlePublicBindings(resultState,
            liveAttackers.Concat(liveDefenders))).DistinctBy(binding => (binding.ObjectId, binding.Generation)).ToArray();
        var continuation = new BattleResultContinuation(battlefield, attacker, defender, winner, resultState.Tick,
            attackers, defenders, liveAttackers, liveDefenders,
            cleanup.DestroyedObjectIds, cleanup.RelatedEventKinds.Concat(events.Where(e => e.Kind is "DAMAGE_APPLIED"
                or "UNIT_DESTROYED" or "DAMAGE_REMOVED" or "UNIT_RECALLED_TO_BASE" or "BATTLE_NO_RESULT")
                .Select(e => e.Kind)).Concat(winner is null ? ["BATTLE_NO_RESULT"] : [])
                .Distinct(StringComparer.Ordinal).ToArray(), bindings, cleanup.AssignedOverkillDamage, "RESULT_TRIGGERS")
        { AttackerRecalledByBattleCleanup = cleanup.AttackerRecalledByBattleCleanup };
        if (winner is null) events.Add(BuildBattleNoResultEvent(continuation));
        events.Add(new("BATTLE_RESULT_DETERMINED", "判定战斗结果，先处理结果触发技能",
            new Dictionary<string, object?> { ["continuation"] = continuation }));
        events.AddRange(queued.Select(trigger =>
        {
            var ev = BuildTriggerQueuedEvent(trigger);
            return ev with { Payload = new Dictionary<string, object?>(ev.Payload)
            { ["battleWinContext"] = (object?)trigger.UnitBattleWinDraw ?? (object?)trigger.LegendBattleWinDraw ?? (object?)trigger.KhazixBattleWin ?? trigger.DravenBattleWinScore } };
        }));
        return resultState with
        {
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(resultState.ObjectLocations, zones),
            PendingBattleResult = continuation, TriggerQueue = resultState.TriggerQueue.Concat(queued).ToArray(),
            UntilEndOfTurnEffects = scoreObservations.Aggregate(resultState.UntilEndOfTurnEffects,
                (effects, captured) => AddUntilEndOfTurnEffect(effects, captured.UsedMarker)),
            TimingState = TimingStates.NeutralClosed, FocusPlayerId = null, PriorityPlayerId = null,
            PassedFocusPlayerIds = [], PassedPriorityPlayerIds = []
        };

        string[] LiveParty(string player) => resultState.ObjectLocations.Where(entry => entry.Value.Zone == "BATTLEFIELD"
            && entry.Value.BattlefieldObjectId == battlefield
            && resultState.CardObjects.TryGetValue(entry.Key, out var card) && IsFaceUpNonStandbyUnit(card)
            && resultState.PlayerZones.TryGetValue(entry.Value.PlayerId, out var ownerZones)
            && ownerZones.Battlefields.Contains(entry.Key)
            && EffectiveFieldControllerId(resultState.PlayerZones, entry.Key, card) == player)
            .Select(entry => entry.Key).Order(StringComparer.Ordinal).ToArray();
    }

    private static string BattleResultReason(BattleResultContinuation c) => c.WinnerPlayerId is not null ? "BATTLE_CLOSED"
        : c.AttackerRecalledByBattleCleanup ? CleanupRecallNoResultReason
        : c.SurvivingAttackerObjectIds.Count == 0 && c.SurvivingDefenderObjectIds.Count == 0
            ? "ALL_PARTICIPANTS_DESTROYED" : "BOTH_SIDES_RETAIN_UNITS";

    private static GameEvent BuildBattleNoResultEvent(BattleResultContinuation c)
        => new("BATTLE_NO_RESULT", "战斗没有胜者", new Dictionary<string, object?> {
            ["battlefieldId"] = c.BattlefieldId, ["attackingPlayerId"] = c.AttackingPlayerId,
            ["defendingPlayerId"] = c.DefendingPlayerId, ["attackerObjectIds"] = c.AttackerObjectIds.ToArray(),
            ["defenderObjectIds"] = c.DefenderObjectIds.ToArray(),
            ["survivingAttackerObjectIds"] = c.SurvivingAttackerObjectIds.ToArray(),
            ["survivingDefenderObjectIds"] = c.SurvivingDefenderObjectIds.ToArray(), ["reason"] = BattleResultReason(c) });

    private static bool BattleSettlementHasPendingItems(MatchState state) => state.StackItems.Count > 0
        || state.TriggerQueue.Any(IsImmediateTrigger) || state.PendingPayment is not null
        || state.PendingCardChoice is not null || state.PendingHandChoice is not null || state.PendingEffectPlay is not null;

    private static ResolutionResult AdvanceBattleResultContinuation(ResolutionResult result)
    {
        var state = result.State;
        if (!result.Accepted || state.PendingBattleResult is not { } continuation) return result;
        if (state.Status != MatchStatuses.InProgress) return result;
        if (BattleSettlementHasPendingItems(state)) return result;
        var events = result.Events.ToList();
        if (continuation.Stage == "CLEANUP_TRIGGERS")
            return Rebuild(DetermineBattleResultAfterCleanup(state, continuation, events), events);
        if (continuation.Stage == "RESULT_TRIGGERS")
        {
            state = state with { PendingBattleResult = continuation with { Stage = "CONTROL_TRIGGERS" },
                TimingState = TimingStates.NeutralOpen, PriorityPlayerId = null, PassedPriorityPlayerIds = [] };
            var control = ResolveNonBattlefieldControlAfterSpellDuelClosed(state, continuation.BattlefieldId,
                concludingBattle: true, battleWinnerPlayerId: continuation.WinnerPlayerId,
                assignedOverkillDamage: continuation.AssignedOverkillDamage);
            state = control.State;
            events.AddRange(control.Events);
            // Collect control-generated observers before deciding whether the
            // battle can close, even if the direct resolver queued no skills.
            return Rebuild(state, events);
        }

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        CloseResolvedBattle(zones, cards, state.ObjectLocations, continuation.BattlefieldId, continuation.AttackerObjectIds,
            continuation.DefenderObjectIds, events);
        var resultKind = continuation.WinnerPlayerId is null ? "NO_RESULT" : "CLOSED";
        var resultReason = BattleResultReason(continuation);
        var history = new BattleResolutionState(
            $"battle-result:{state.Tick}:{resultKind}:{continuation.BattlefieldId}:{continuation.AttackerObjectIds.FirstOrDefault() ?? continuation.AttackingPlayerId}",
            state.Tick, resultKind, resultReason, continuation.BattlefieldId, continuation.AttackingPlayerId,
            continuation.DefendingPlayerId, continuation.WinnerPlayerId, continuation.AttackerObjectIds,
            continuation.DefenderObjectIds, continuation.SurvivingAttackerObjectIds, continuation.SurvivingDefenderObjectIds,
            continuation.DestroyedObjectIds, continuation.RelatedEventKinds.Append("BATTLE_CLOSED").Distinct().ToArray())
        { PublicObjectBindings = continuation.PublicObjectBindings };
        state = state with
        {
            PendingBattleResult = null, CardObjects = cards, PlayerZones = zones,
            BattleResolutions = new[] { history }.Concat(state.BattleResolutions).Take(12).ToArray(),
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(ClearBattleDamageAssignmentLedgerMarkers(
                ClearBattleResponseDeclarationContextMarkers(state.UntilEndOfTurnEffects)),
                BattlefieldTaskMarkers.BattleSkipped(continuation.BattlefieldId)),
            ActivePlayerId = state.TurnPlayerId, TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null, FocusPlayerId = null, PassedFocusPlayerIds = [], PassedPriorityPlayerIds = []
        };
        // CN 463: opposing occupants after battle end create a new pending
        // battle. The completed battle's markers must not suppress its new duel.
        if (state.BattlefieldStates.TryGetValue(continuation.BattlefieldId, out var battlefield)
            && battlefield.OccupantControllerIds.Distinct(StringComparer.Ordinal).Count() > 1)
            state = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects
                .Where(effect => effect != BattlefieldTaskMarkers.BattleSkipped(continuation.BattlefieldId)
                    && effect != BattlefieldTaskMarkers.SpellDuelCompleted(continuation.BattlefieldId)).ToArray() };
        var advance = AdvancePendingBattlefieldTasksAfterStateChange(state, continuation.AttackingPlayerId, result.State);
        events.AddRange(advance.Events);
        return Rebuild(advance.State, events);

        ResolutionResult Rebuild(MatchState next, IReadOnlyList<GameEvent> allEvents)
            => result with { State = next, Events = allEvents, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static StackResolutionResult ResolveLegendBattleWinDraw(MatchState state, StackItemState item,
        LegendBattleWinDrawContext context)
    {
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent> { new("LEGEND_TRIGGER_RESOLVED", "荣耀行刑官赢得战斗，抽一张牌",
            new Dictionary<string, object?> { ["playerId"] = context.ControllerId, ["legendCardNo"] = context.CardNo,
                ["sourceObjectId"] = context.Source.ObjectId, ["battlefieldId"] = context.BattlefieldId,
                ["trigger"] = "BATTLE_WON_DRAW_ONE" }) };
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, context.ControllerId, 1,
            state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores,
            WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
