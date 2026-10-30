using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestMillContext(
    [property: JsonRequired] string BattlefieldObjectId,
    [property: JsonRequired] long BattlefieldGeneration,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string ConquestSourceObjectId,
    [property: JsonRequired] long ConquestTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestMillPublicContext(
    [property: JsonRequired] string BattlefieldObjectId,
    [property: JsonRequired] long BattlefieldGeneration,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] long ConquestTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitConquestReturnContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string BattlefieldObjectId,
    [property: JsonRequired] long ConquestTick,
    [property: JsonRequired] int Occurrence,
    [property: JsonRequired] long FaceRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal static TriggerSpec? UnitConquestReturnSpec(string? cardNo)
        => UnitConquestTriggerSpecRules.TryGetTrigger(cardNo,
            UnitConquestTriggerSpecRules.IsUnitConquestPayReturnSelfToHandTrigger, out var spec) ? spec : null;

    internal static bool ValidUnitConquestReturnContext(MatchState state, UnitConquestReturnContext context,
        string effect, string source, string controller, string? cardNo)
        => context.Source is not null && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && context.Source.Generation >= 0 && context.Source.ObjectId == source
            && !string.IsNullOrWhiteSpace(context.ControllerId) && context.ControllerId == controller
            && state.Seats.ContainsKey(controller) && !string.IsNullOrWhiteSpace(context.BattlefieldObjectId)
            && state.BattlefieldStates.ContainsKey(context.BattlefieldObjectId)
            && context.ConquestTick >= 0 && context.ConquestTick <= state.Tick
            && context.Occurrence >= 0 && context.FaceRevision >= 0 && cardNo == context.CardNo
            && UnitConquestReturnSpec(context.CardNo) is { } spec && RuntimeTriggerEffectKind(spec) == effect
            && PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision,
                context.ConquestTick, context.TokenFactoryCardNo);

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
        recastTriggers = recastTriggers.Concat(CaptureBattlefieldConquestMill(playerZones, cardObjects,
            playerId, battlefieldId, sourceObjectId, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureBattlefieldConquestDiscardDraw(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, sourceObjectId, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureBattlefieldConquestReadyEquipment(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, sourceObjectId, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureBattlefieldConquestConsumeBoon(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, sourceObjectId, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureConquestTokens(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureConquestSources(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick, "BATTLEFIELD_CONQUERED")).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureConquestFlow(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick)).ToArray();
        recastTriggers = recastTriggers.Concat(CaptureUnitConquestReturns(state with { PlayerZones = playerZones,
            CardObjects = cardObjects, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones) },
            playerId, battlefieldId, conqueringUnitObjectIds, resolutionTick)).ToArray();
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

        var battlefieldRecycleRuneTrigger = ResolveBattlefieldConquerRecycleRuneTrigger(
            playerZones,
            cardObjects,
            playerId,
            battlefieldId,
            sourceObjectId,
            rngCursor);
        rngCursor = battlefieldRecycleRuneTrigger.RngCursor;
        events.AddRange(battlefieldRecycleRuneTrigger.Events);

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

    private static IReadOnlyList<TriggerQueueItemState> CaptureBattlefieldConquestMill(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string controller, string battlefieldId, string conquestSource, long tick)
    {
        if (!TryGetBattlefieldCardObject(zones, cards, battlefieldId, out var id, out var battlefield)
            || string.IsNullOrWhiteSpace(battlefield.CardNo)
            || !BattlefieldTriggerSpecRules.TryGetTrigger(battlefield.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerMillTrigger, out var spec)) return [];
        var context = new BattlefieldConquestMillContext(id, battlefield.ObjectGeneration, battlefield.CardNo,
            controller, conquestSource, tick);
        return [new($"CONQUEST-MILL:{tick}:{id}", controller, id, spec.Kind, "BATTLEFIELD_CONQUERED")
        {
            SourceCardNo = battlefield.CardNo, EnqueuedAtTick = tick, BattlefieldConquestMill = context
        }];
    }

    internal static string UnitConquestReturnTriggerId(UnitConquestReturnContext context)
        => $"UNIT-CONQUEST-RETURN:{context.ConquestTick}:{MatchStateHasher.HashValue(new {
            context.Source, context.ControllerId, context.BattlefieldObjectId, context.Occurrence })}";

    private static IEnumerable<TriggerQueueItemState> CaptureUnitConquestReturns(MatchState state, string controller,
        string battlefield, IReadOnlyList<string> units, long tick)
    {
        foreach (var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!TryGetUnitConquestPayReturnSelfToHandSource(state.CardObjects, state.PlayerZones, controller, id, out var source)
                || string.IsNullOrWhiteSpace(source.CardNo)
                || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != battlefield
                || UnitConquestReturnSpec(source.CardNo) is not { } spec) continue;
            var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects,
                state.ObjectLocations, controller, battlefield, "BATTLEFIELD_CONQUERED");
            for (var occurrence = 0; occurrence < repeats; occurrence++)
            {
                var context = new UnitConquestReturnContext(new(id, source.ObjectGeneration), source.CardNo,
                    controller, battlefield, tick, occurrence, source.CopyFaceTransitions?.Count ?? 0, source.TokenFactoryCardNo);
                yield return new TriggerQueueItemState(UnitConquestReturnTriggerId(context), controller, id,
                    RuntimeTriggerEffectKind(spec), "BATTLEFIELD_CONQUERED") {
                    SourceCardNo = source.CardNo, EnqueuedAtTick = tick, UnitConquestReturn = context };
            }
        }
    }

    private static StackResolutionResult ResolveUnitConquestReturn(MatchState state, StackItemState item)
    {
        var context = item.UnitConquestReturn!;
        if (!ValidUnitConquestReturnContext(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            || item.TriggerCost is null || !ValidTriggerCostReceipt(state, item))
            throw new InvalidOperationException("Invalid confirmed unit conquest return.");
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var ownerId = string.Empty;
        var returned = cards.TryGetValue(context.Source.ObjectId, out var current)
            && current.ObjectGeneration == context.Source.Generation && IsObjectOnField(zones, context.Source.ObjectId)
            && TryReturnTargetToHand(zones, cards, context.Source.ObjectId, out ownerId, out _);
        var events = new List<GameEvent>();
        if (returned)
        {
            var payment = TriggerCostPayment(item);
            events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", $"{item.ControllerId} 的征服回手来源单位返回手牌",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["battlefieldId"] = context.BattlefieldObjectId, ["battlefieldObjectId"] = context.BattlefieldObjectId,
                    ["trigger"] = item.EffectKind, ["sourceObjectId"] = item.SourceObjectId,
                    ["returnedObjectId"] = item.SourceObjectId, ["ownerPlayerId"] = ownerId,
                    ["paymentId"] = payment.PaymentId, ["paymentWindow"] = payment.PaymentWindow }));
            events.Add(new("UNIT_RETURNED_TO_HAND", $"{item.SourceObjectId} 返回手牌",
                new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId,
                    ["targetObjectId"] = item.SourceObjectId, ["ownerPlayerId"] = ownerId, ["reason"] = item.EffectKind }));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }

    internal static BattlefieldConquestMillPublicContext? PublicBattlefieldConquestMill(BattlefieldConquestMillContext? context)
        => context is null ? null : new(context.BattlefieldObjectId, context.BattlefieldGeneration,
            context.CardNo, context.ControllerId, context.ConquestTick);

    internal static bool ValidBattlefieldConquestMillPublicFrame(BattlefieldConquestMillPublicContext context)
        => !string.IsNullOrWhiteSpace(context.BattlefieldObjectId)
            && !string.IsNullOrWhiteSpace(context.ControllerId)
            && !string.IsNullOrWhiteSpace(context.CardNo)
            && context.BattlefieldGeneration >= 0 && context.ConquestTick >= 0
            && BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerMillTrigger, out _);

    internal static bool ValidBattlefieldConquestMillFrame(BattlefieldConquestMillContext context)
        => !string.IsNullOrWhiteSpace(context.ConquestSourceObjectId)
            && ValidBattlefieldConquestMillPublicFrame(PublicBattlefieldConquestMill(context)!);

    internal static bool ValidBattlefieldConquestMillContext(MatchState state, BattlefieldConquestMillContext context,
        string effect, string source, string controller, string? cardNo)
        => ValidBattlefieldConquestMillFrame(context)
            && effect == TriggerKinds.BattlefieldConquerMill && source == context.BattlefieldObjectId
            && controller == context.ControllerId && cardNo == context.CardNo
            && state.Seats.ContainsKey(controller) && context.ConquestTick <= state.Tick
            && state.CardObjects.TryGetValue(source, out var battlefield)
            && (battlefield.CardNo == context.CardNo || ReplacedBattlefieldCard(state, battlefield)?.CardNo == context.CardNo)
            && battlefield.ObjectGeneration >= context.BattlefieldGeneration
            && (state.BattlefieldResolutions.Any(r => r.Kind == "CONQUERED" && r.Tick == context.ConquestTick
                    && r.BattlefieldObjectId == source && r.PlayerId == controller
                    && r.SourceObjectId == context.ConquestSourceObjectId)
                // The public resolution list retains only 12 entries. A pending
                // older skill can outlive that projection; full recovery still
                // requires its accepted conquest and enqueue events in the journal.
                || state.BattlefieldResolutions.Count == 12
                    && state.BattlefieldResolutions.All(r => r.Tick > context.ConquestTick));

    private static StackResolutionResult ResolveBattlefieldConquestMill(MatchState state, StackItemState item)
    {
        var context = item.BattlefieldConquestMill!;
        if (!ValidBattlefieldConquestMillContext(state, context, item.EffectKind, item.SourceObjectId,
                item.ControllerId, item.CardNo)
            || !BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerMillTrigger, out var spec))
            throw new InvalidOperationException("Invalid captured battlefield conquest mill.");

        var zones = NormalizeZonesForSeats(state);
        var playerZones = zones[item.ControllerId];
        IReadOnlyDictionary<string, int> scores = state.PlayerScores;
        var rng = state.RngCursor;
        var burnouts = 0;
        string? winner = null;
        var moved = new List<string>();
        var batch = new List<string>();
        var events = new List<GameEvent>();
        void FlushMoved()
        {
            if (batch.Count == 0) return;
            events.Add(new GameEvent("CARDS_MILLED", $"{item.ControllerId} 将主牌堆顶 {batch.Count} 张牌放入废牌堆",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["sourceObjectId"] = context.BattlefieldObjectId, ["cardIds"] = batch.ToArray(),
                    ["count"] = batch.Count, ["sourceZone"] = spec.MillSourceZone,
                    ["destinationZone"] = spec.MillDestinationZone }));
            batch.Clear();
        }
        for (var i = 0; i < spec.MillCount.GetValueOrDefault() && winner is null; i++)
        {
            // Move each card before the next attempt so an intervening burnout
            // recycles the graveyard as it exists at that exact point.
            var draw = TakeTopCardWithBurnout(state with { PlayerScores = scores, RngCursor = rng }, item.ControllerId, playerZones, burnouts);
            if (draw.Burnouts.Count > 0) FlushMoved();
            foreach (var burnout in draw.Burnouts)
                events.Add(new GameEvent("BURNOUT_APPLIED", $"{item.ControllerId} 执行燃尽",
                    new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                        ["scoredPlayerId"] = burnout.ScoredPlayerId, ["scoredPlayerScore"] = burnout.ScoredPlayerScore,
                        ["burnoutIndex"] = ++burnouts }));
            scores = draw.PlayerScores;
            rng = draw.RngCursor;
            winner = draw.WinnerPlayerId;
            playerZones = playerZones with { MainDeck = draw.MainDeck,
                Graveyard = draw.Graveyard.Concat(draw.DrawnCards).ToArray() };
            moved.AddRange(draw.DrawnCards);
            batch.AddRange(draw.DrawnCards);
            if (draw.DrawnCards.Count == 0) break;
        }
        FlushMoved();
        zones[item.ControllerId] = playerZones;
        var hiddenConquestSource = state.PlayerZones.Values.Any(z => z.Hand.Contains(context.ConquestSourceObjectId)
            || z.MainDeck.Contains(context.ConquestSourceObjectId) || z.RuneDeck.Contains(context.ConquestSourceObjectId))
            || state.CardObjects.TryGetValue(context.ConquestSourceObjectId, out var conquestSource) && conquestSource.IsFaceDown;
        events.Insert(0, new GameEvent("BATTLEFIELD_TRIGGER_RESOLVED", $"{item.ControllerId} 征服战场并弃置主牌堆顶牌",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                ["battlefieldId"] = context.BattlefieldObjectId, ["battlefieldObjectId"] = context.BattlefieldObjectId,
                ["battlefieldCardNo"] = context.CardNo, ["trigger"] = item.EffectKind,
                ["sourceObjectId"] = hiddenConquestSource ? "HIDDEN" : context.ConquestSourceObjectId, ["count"] = moved.Count }));
        if (winner is not null) events.Add(BuildScoreVictoryEvent(winner, EffectiveWinningScore(state)));
        return new(zones, state.CardObjects, scores, NormalizeExperienceForSeats(state), state.RunePools,
            state.UntilEndOfTurnEffects, winner, events, [], null, [], null, [], rng);
    }
}
