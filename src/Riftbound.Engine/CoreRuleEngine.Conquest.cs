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
            var conquestIndex = events.FindLastIndex(gameEvent => gameEvent.Kind == "BATTLEFIELD_CONQUERED"
                && EventPayloadString(gameEvent, "playerId") == playerId
                && EventPayloadString(gameEvent, "battlefieldId") == battlefieldId);
            if (conquestIndex >= 0)
                events[conquestIndex] = events[conquestIndex] with
                {
                    Payload = new Dictionary<string, object?>(events[conquestIndex].Payload)
                    {
                        ["huntAmount"] = huntAmount,
                        ["huntSourceObjectIds"] = huntConquerSources.Select(source => source.ObjectId).ToArray(),
                        ["huntAmountsBySource"] = huntConquerSources.ToDictionary(
                            source => source.ObjectId, source => source.HuntAmount, StringComparer.Ordinal)
                    }
                };
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
            recastTriggers = recastTriggers.Concat(CaptureConquestUnitEvents(state with { PlayerZones = playerZones,
                CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
                conqueringUnitObjectIds, resolutionTick)).ToArray();
        if (conqueredRealBattlefield)
            recastTriggers = recastTriggers.Concat(CaptureLegendImageTriggers(playerZones, cardObjects,
                playerId, battlefieldId, resolutionTick)).Concat(CaptureIvernTriggers(playerZones, cardObjects,
                playerId, battlefieldId, resolutionTick)).Concat(CaptureLegendConquest(playerZones, cardObjects,
                playerId, battlefieldId, assignedOverkillDamageToEnemyUnits, resolutionTick)).ToArray();
        if (conqueredRealBattlefield)
            recastTriggers = recastTriggers.Concat(CaptureConquestDamage(state, state with {PlayerZones=playerZones,CardObjects=cardObjects},
                playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureBattlefieldConquestMight(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureGrantedConquestReturns(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureDelayedEndCreation(state with { PlayerZones=playerZones,CardObjects=cardObjects },
            playerId,battlefieldId,resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureBattlefieldConquestLook(playerZones,cardObjects,
            playerId,battlefieldId,resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureConquestTokens(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureConquestScores(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick, "BATTLEFIELD_CONQUERED")).ToArray();
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
                conqueringUnitObjectIds.Where(id => !cardObjects.TryGetValue(id, out var unit)
                    || ConquestTokenSpec(unit.CardNo) is null).ToArray(),
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
                battlefieldDiscardedObjectIds, playerZones);
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
            events, untilEndOfTurnEffects);

        var garenControlledUnitCount = CountControlledUnitsAtBattlefield(
            playerZones, cardObjects, ReconcileObjectLocations(state.ObjectLocations, playerZones),
            playerId, battlefieldId);
        if (winnerPlayerId is null
            && garenControlledUnitCount >= 4
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
                    ["controlledBattlefieldUnitCount"] = garenControlledUnitCount
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
        foreach (var conqueringUnitId in conqueringUnitObjectIds.Distinct(StringComparer.Ordinal))
        {
            if (pendingPayment is not null) break;
            if (TryOpenUnitConquestPayReturnSelfToHandPaymentWindow(
                playerZones,
                cardObjects,
                playerId,
                battlefieldId,
                conqueringUnitId,
                resolutionTick,
                events,
                out var unitConquestPayReturnSelfToHandPendingPayment))
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
