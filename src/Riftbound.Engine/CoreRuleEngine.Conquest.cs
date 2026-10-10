using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Both combat and unopposed control changes must dispatch the same conquest skills.
    private static MatchState ResolveConquestTriggerEffects(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        IReadOnlyList<string> conqueringUnitObjectIds,
        int assignedOverkillDamageToEnemyUnits,
        long resolutionTick,
        List<GameEvent> events)
    {
        var originalObjectIds = cardObjects.Keys.ToHashSet(StringComparer.Ordinal);
        var playerScores = state.PlayerScores;
        var playerExperience = state.PlayerExperience;
        var runePools = state.RunePools.ToDictionary(e => e.Key, e => e.Value);
        var untilEndOfTurnEffects = state.UntilEndOfTurnEffects;
        var rngCursor = state.RngCursor;
        var winnerPlayerId = state.WinnerPlayerId;
        var pendingPayment = state.PendingPayment;
        var pendingHandChoice = state.PendingHandChoice;
        var conqueredRealBattlefield = TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out _, out _);
        var huntConquerSources = conqueringUnitObjectIds
            .Where(cardObjects.ContainsKey)
            .Select(objectId => new { ObjectId = objectId, CardObject = cardObjects[objectId],
                HuntAmount = CardResourceKeywordRules.HuntAmountFromTags(cardObjects[objectId].Tags) })
            .Where(source => source.HuntAmount > 0).ToArray();
        var huntAmount = huntConquerSources.Sum(source => source.HuntAmount);
        var conquestStackItem = new StackItemState($"conquest-{resolutionTick}-{battlefieldId}", playerId,
            sourceObjectId, "CONQUEST");
        if (huntAmount > 0)
        {
            var huntSource = huntConquerSources[0];
            playerExperience = GainExperience(
                NormalizeExperienceForSeats(state),
                playerId,
                huntAmount,
                conquestStackItem,
                events,
                huntSource.ObjectId,
                huntSource.CardObject.CardNo);
        }

        var recastTriggers = CaptureConquestRecasts(state with { PlayerZones = playerZones, CardObjects = cardObjects },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick);
        if (conqueredRealBattlefield)
            recastTriggers = recastTriggers.Concat(CaptureLegendImageTriggers(playerZones, cardObjects,
                playerId, battlefieldId, resolutionTick)).Concat(CaptureIvernTriggers(playerZones, cardObjects,
                playerId, battlefieldId, resolutionTick)).Concat(CaptureLegendConquest(playerZones, cardObjects,
                playerId, battlefieldId, assignedOverkillDamageToEnemyUnits, resolutionTick)).ToArray();
        events.AddRange(recastTriggers.Select(BuildTriggerQueuedEvent));
        var naturalUnitConquestEvents = new List<GameEvent>();
        if (conqueredRealBattlefield
            && TryResolveNaturalUnitConquestTriggerSpecs(
                state,
                playerZones,
                cardObjects,
                runePools,
                playerScores,
                untilEndOfTurnEffects,
                playerId,
                battlefieldId,
                conqueringUnitObjectIds,
                assignedOverkillDamageToEnemyUnits,
                rngCursor,
                naturalUnitConquestEvents,
                out var naturalUnitConquestDrawApplication,
                out var naturalUnitConquestUntilEndOfTurnEffects,
                out var naturalUnitConquestPendingPayment))
        {
            events.AddRange(naturalUnitConquestEvents);
            playerScores = naturalUnitConquestDrawApplication.PlayerScores;
            winnerPlayerId = naturalUnitConquestDrawApplication.WinnerPlayerId ?? winnerPlayerId;
            rngCursor = naturalUnitConquestDrawApplication.RngCursor;
            untilEndOfTurnEffects = naturalUnitConquestUntilEndOfTurnEffects;
            pendingPayment ??= naturalUnitConquestPendingPayment;
        }

        TryResolveBattlefieldConquerMillTwoTrigger(
            playerZones,
            cardObjects,
            playerId,
            battlefieldId,
            sourceObjectId,
            events);
        var battlefieldRecycleRuneTrigger = ResolveBattlefieldConquerRecycleRuneTrigger(
            playerZones,
            cardObjects,
            playerId,
            battlefieldId,
            sourceObjectId,
            rngCursor);
        rngCursor = battlefieldRecycleRuneTrigger.RngCursor;
        events.AddRange(battlefieldRecycleRuneTrigger.Events);
        var battlefieldRevealRecycleTrigger = ResolveBattlefieldConquerRevealRecycleTrigger(
            state,
            playerZones,
            cardObjects,
            playerId,
            battlefieldId,
            sourceObjectId,
            rngCursor);
        rngCursor = battlefieldRevealRecycleTrigger.RngCursor;
        events.AddRange(battlefieldRevealRecycleTrigger.Events);
        if (TryResolveBattlefieldConquerDiscardDrawTrigger(
                state,
                playerZones,
                cardObjects,
                playerScores,
                playerId,
                battlefieldId,
                sourceObjectId,
                rngCursor,
                events,
                out var battlefieldDiscardDrawApplication,
                out var battlefieldDiscardedObjectIds,
                    out pendingHandChoice))
        {
            playerScores = battlefieldDiscardDrawApplication.PlayerScores;
            winnerPlayerId = battlefieldDiscardDrawApplication.WinnerPlayerId ?? winnerPlayerId;
            rngCursor = battlefieldDiscardDrawApplication.RngCursor;
            untilEndOfTurnEffects = MarkPlayerDiscardedHandCardsThisTurn(
                untilEndOfTurnEffects,
                playerId,
                battlefieldDiscardedObjectIds);
        }
        if (TryResolveBattlefieldConquerConsumeBoonDrawTrigger(
                state,
                playerZones,
                cardObjects,
                playerScores,
                playerId,
                battlefieldId,
                sourceObjectId,
                rngCursor,
                events,
                out var battlefieldBoonDrawApplication))
        {
            playerScores = battlefieldBoonDrawApplication.PlayerScores;
            winnerPlayerId = battlefieldBoonDrawApplication.WinnerPlayerId ?? winnerPlayerId;
            rngCursor = battlefieldBoonDrawApplication.RngCursor;
        }
        if (TryOpenBattlefieldConquerPayReadyLegendPaymentWindow(
                playerZones,
                cardObjects,
                playerId,
                battlefieldId,
                sourceObjectId,
                resolutionTick,
                events,
                out var battlefieldReadyLegendPendingPayment))
        {
            pendingPayment = battlefieldReadyLegendPendingPayment;
        }
        if (TryOpenBattlefieldConquerPowerfulPayDrawPaymentWindow(
                playerZones,
                cardObjects,
                playerId,
                battlefieldId,
                sourceObjectId,
                conqueringUnitObjectIds,
                resolutionTick,
                events,
                out var battlefieldPowerfulDrawPendingPayment))
        {
            pendingPayment = battlefieldPowerfulDrawPendingPayment;
        }
        if (TryOpenBattlefieldConquerPayOneCreateGoldPaymentWindow(
                playerZones,
                cardObjects,
                playerId,
                battlefieldId,
                sourceObjectId,
                resolutionTick,
                events,
                out var battlefieldGoldPendingPayment))
        {
            pendingPayment = battlefieldGoldPendingPayment;
        }
        if (TryOpenBattlefieldConquerPayOneReturnUnitCreateSandSoldierPaymentWindow(
                playerZones,
                cardObjects,
                playerId,
                battlefieldId,
                sourceObjectId,
                resolutionTick,
                events,
                out var battlefieldSandSoldierPendingPayment))
        {
            pendingPayment = battlefieldSandSoldierPendingPayment;
        }
        if (TryResolveBattlefieldConquerReadyRunesAtEndTrigger(
                playerZones,
                cardObjects,
                untilEndOfTurnEffects,
                playerId,
                battlefieldId,
                sourceObjectId,
                events,
                out var battlefieldReadyRuneUntilEndOfTurnEffects))
        {
            untilEndOfTurnEffects = battlefieldReadyRuneUntilEndOfTurnEffects;
        }
        TryResolveBattlefieldConquerReadyEquipmentTrigger(
            playerZones,
            cardObjects,
            playerId,
            battlefieldId,
            sourceObjectId,
            events);
        if (TryResolveBattlefieldConquerDrawForOtherBattlefieldsTrigger(
                state,
                playerZones,
                cardObjects,
                playerScores,
                playerId,
                battlefieldId,
                sourceObjectId,
                rngCursor,
                events,
                out var battlefieldOtherDrawApplication))
        {
            playerScores = battlefieldOtherDrawApplication.PlayerScores;
            winnerPlayerId = battlefieldOtherDrawApplication.WinnerPlayerId ?? winnerPlayerId;
            rngCursor = battlefieldOtherDrawApplication.RngCursor;
        }
        TryResolveBattlefieldConquerOverkillCreateWarhawkTrigger(
            playerZones,
            cardObjects,
            playerId,
            battlefieldId,
            sourceObjectId,
            assignedOverkillDamageToEnemyUnits,
            events);

        if (winnerPlayerId is null
            && CountControlledBattlefieldUnits(playerZones, cardObjects, playerId) >= 4
            && TryGetGarenIntroLegendCardNo(playerZones, cardObjects, playerId, out var garenLegendCardNo))
        {
            events.Add(new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的德玛西亚之力因征服战场触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendCardNo"] = garenLegendCardNo,
                    ["trigger"] = "BATTLEFIELD_CONQUERED_DRAW_TWO",
                    ["sourceObjectId"] = sourceObjectId,
                    ["battlefieldId"] = battlefieldId,
                    ["controlledBattlefieldUnitCount"] = CountControlledBattlefieldUnits(playerZones, cardObjects, playerId)
                }));
            var drawApplication = ApplyDrawToPlayer(
                state,
                playerZones,
                playerScores,
                playerId,
                2,
                rngCursor,
                events);
            playerScores = drawApplication.PlayerScores;
            winnerPlayerId = drawApplication.WinnerPlayerId ?? winnerPlayerId;
            rngCursor = drawApplication.RngCursor;
        }
        if (pendingPayment is null
            && TryOpenUnitConquestPayReturnSelfToHandPaymentWindow(
                playerZones,
                cardObjects,
                playerId,
                battlefieldId,
                sourceObjectId,
                resolutionTick,
                events,
                out var unitConquestPayReturnSelfToHandPendingPayment))
        {
            pendingPayment = unitConquestPayReturnSelfToHandPendingPayment;
        }

        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        foreach (var objectId in cardObjects.Keys.Where(id => !originalObjectIds.Contains(id)))
        {
            // A token created here belongs to this precise battlefield, even when the
            // controller owns a different battlefield card in the same zone list.
            if (playerZones.TryGetValue(playerId, out var zones) && zones.Battlefields.Contains(objectId)
                && cardObjects[objectId].Tags.Contains(CardObjectTags.UnitCard))
                objectLocations[objectId] = new ObjectLocationState(playerId, MoveUnitBattlefieldZone, battlefieldId);
        }
        return state with
        {
            TriggerQueue = state.TriggerQueue.Concat(recastTriggers).ToArray(),
            ObjectLocations = objectLocations,
            PlayerZones = playerZones, CardObjects = cardObjects, PlayerScores = playerScores,
            PlayerExperience = playerExperience, RunePools = runePools, RngCursor = rngCursor,
            UntilEndOfTurnEffects = MarkPlayersWhoGainedExperienceThisTurn(untilEndOfTurnEffects, events),
            PendingPayment = pendingPayment, PendingHandChoice = pendingHandChoice,
            WinnerPlayerId = winnerPlayerId,
            Status = winnerPlayerId is null ? state.Status : MatchStatuses.Finished
        };
    }
}
