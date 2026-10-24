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
    IReadOnlyList<ObjectBinding> PublicObjectBindings, int AssignedOverkillDamage, string Stage);

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult CollectResolutionEffects(MatchState state, ResolutionResult result)
    {
            result = CaptureDuelChainFocus(state, result);
            result = CollectObservedDeckPlays(result);
            result = EndDetachedUnitCopies(state, result);
            result = RefreshRuleDerivedKeywordLayers(result);
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
            collected = RecordBattleDuelRoles(state, collected);
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

    // CN 468.3-468.7: result skills finish before control, and control skills
    // finish before the battle closes. Both damage and skipped-damage paths
    // enter this same continuation for every determined winner.
    private static bool TryBeginBattleResultContinuation(MatchState resultState,
        string battlefield, string attacker, string? defender, string? winner,
        IReadOnlyList<string> attackers, IReadOnlyList<string> defenders,
        int overkill, List<GameEvent> events, out MatchState pending)
    {
        // A synchronous replacement can suspend the parent across commands.
        // Its result is born at the accepted completion, not the original attempt.
        resultState = resultState with { Tick = RuleChoices.Value?.CompletionTick ?? resultState.Tick };
        pending = resultState;
        // Death instructions have already captured their ordinary skills, but
        // their observer collector runs after this battle instruction completes.
        // Do not establish control or close a no-winner battle ahead of them.
        var hasCapturedDeaths = events.Any(e => e.Payload.GetValueOrDefault("capturedUnitDeath")
            is CapturedUnitDeath { DravenBattleDeathScore: not null });
        if (winner is null && !hasCapturedDeaths) return false;
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
        // A determined winner still requires the same cleanup/control/victory phases when no skill was earned.

        var zones = NormalizeZonesForSeats(resultState);
        var cards = resultState.CardObjects.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        ApplyBattleCleanup(zones, cards, battlefield, attackers, defenders, events);
        if (winner is null)
            events.Add(BuildBattleNoResultEvent(zones, cards, battlefield, attackers, defenders, attacker, defender));
        var bindings = TryReadBattleResponseDeclarationContext(resultState, battlefield, out var declaration)
            ? declaration.PublicObjectBindings : CaptureBattlePublicBindings(resultState, attackers.Concat(defenders));
        var continuation = new BattleResultContinuation(battlefield, attacker, defender, winner, resultState.Tick,
            attackers.ToArray(), defenders.ToArray(), SurvivingBattleUnitObjectIds(zones, cards, attackers),
            SurvivingBattleUnitObjectIds(zones, cards, defenders),
            events.Where(e => e.Kind == "UNIT_DESTROYED").Select(e => EventPayloadString(e, "targetObjectId")!)
                .Where(id => attackers.Concat(defenders).Contains(id)).Distinct(StringComparer.Ordinal).ToArray(),
            events.Where(e => e.Kind is "DAMAGE_APPLIED" or "UNIT_DESTROYED" or "DAMAGE_REMOVED" or "UNIT_RECALLED_TO_BASE" or "BATTLE_NO_RESULT")
                .Select(e => e.Kind).Distinct(StringComparer.Ordinal).ToArray(), bindings ?? [], overkill, "RESULT_TRIGGERS");
        events.Add(new("BATTLE_RESULT_DETERMINED", "判定战斗结果，先处理结果触发技能",
            new Dictionary<string, object?> { ["continuation"] = continuation }));
        events.AddRange(queued.Select(trigger =>
        {
            var ev = BuildTriggerQueuedEvent(trigger);
            return ev with { Payload = new Dictionary<string, object?>(ev.Payload)
            { ["battleWinContext"] = (object?)trigger.UnitBattleWinDraw ?? (object?)trigger.LegendBattleWinDraw ?? (object?)trigger.KhazixBattleWin ?? trigger.DravenBattleWinScore } };
        }));
        pending = resultState with
        {
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(resultState.ObjectLocations, zones),
            PendingBattleResult = continuation, TriggerQueue = resultState.TriggerQueue.Concat(queued).ToArray(),
            UntilEndOfTurnEffects = scoreObservations.Aggregate(resultState.UntilEndOfTurnEffects,
                (effects, captured) => AddUntilEndOfTurnEffect(effects, captured.UsedMarker)),
            TimingState = TimingStates.NeutralClosed, FocusPlayerId = null, PriorityPlayerId = null,
            PassedFocusPlayerIds = [], PassedPriorityPlayerIds = []
        };
        return true;
    }

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
        var resultReason = continuation.WinnerPlayerId is not null ? "BATTLE_CLOSED"
            : continuation.SurvivingAttackerObjectIds.Count == 0 && continuation.SurvivingDefenderObjectIds.Count == 0
                ? "ALL_PARTICIPANTS_DESTROYED" : "BOTH_SIDES_RETAIN_UNITS";
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
        // battle. The completed battle's skip marker must not suppress it.
        if (state.BattlefieldStates.TryGetValue(continuation.BattlefieldId, out var battlefield)
            && battlefield.OccupantControllerIds.Distinct(StringComparer.Ordinal).Count() > 1)
            state = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects
                .Where(effect => effect != BattlefieldTaskMarkers.BattleSkipped(continuation.BattlefieldId)).ToArray() };
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
        var draw = ApplyDrawToPlayer(state, zones, state.PlayerScores, context.ControllerId, 1,
            state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, PlayerScores = draw.PlayerScores,
            WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
