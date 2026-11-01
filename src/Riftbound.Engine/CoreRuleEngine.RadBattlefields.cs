using Riftbound.Contracts;

namespace Riftbound.Engine;

/// <summary>
/// Rules that are shared by the RAD battlefield cards.  These are kept in the
/// event collector so a battlefield effect is captured from the accepted
/// authoritative event, before later cleanup can erase its source location.
/// </summary>
public sealed partial class CoreRuleEngine
{
    internal const string Rad012CardNo = "RAD·012/167";
    internal const string Rad017CardNo = "RAD·017/167";
    internal const string Rad106CardNo = "RAD·106/167";
    internal const string Rad024CardNo = "RAD·024/167";
    internal const string Rad069CardNo = "RAD·069/167";
    internal const string Rad136CardNo = "RAD·136/167";
    internal const string Rad052CardNo = "RAD·052/167";
    internal const string Rad157CardNo = "RAD·157/167";
    internal const string Rad158CardNo = "RAD·158/167";
    internal const string Rad159CardNo = "RAD·159/167";
    internal const string Rad160CardNo = "RAD·160/167";
    internal const string Rad161CardNo = "RAD·161/167";
    internal const string Rad162CardNo = "RAD·162/167";
    internal const string Rad163CardNo = "RAD·163/167";
    internal const string Rad164CardNo = "RAD·164/167";
    internal const string Rad165CardNo = "RAD·165/167";
    internal const string Rad166CardNo = "RAD·166/167";
    internal const string Rad184CardNo = "RAD·184/167";

    internal const string Rad165InsightKind = "RAD165";

    private static string Rad157GoldMarker(int turn, string field, string hiddenObject)
        => $"RAD157_GOLD:{turn}:{field}:{hiddenObject}";

    internal static string Rad162NextHasteMarkerPrefix(string playerId)
        => $"RAD162_NEXT_HASTE:{playerId}:";

    internal static string Rad163UsedMarker(int turn, string playerId, string field)
        => $"RAD163_USED:{turn}:{playerId}:{field}";

    private static string Rad159UsedMarker(int turn, string playerId, string field)
        => $"RAD159_USED:{turn}:{playerId}:{field}";

    internal static bool IsRad165InsightKind(string? kind)
        => string.Equals(kind, Rad165InsightKind, StringComparison.Ordinal);

    private static bool IsRadBattlefieldCard(MatchState state, string battlefieldObjectId, string cardNo)
        => state.CardObjects.TryGetValue(battlefieldObjectId, out var field)
            && string.Equals(field.CardNo, cardNo, StringComparison.Ordinal)
            && field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
            && state.BattlefieldStates.ContainsKey(battlefieldObjectId);

    private static bool TryEventString(GameEvent ev, string key, out string value)
    {
        if (ev.Payload.TryGetValue(key, out var raw) && raw is string text && !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    // Unit entry events carry their destination as a precise location
    // ("BATTLEFIELD:<objectId>"), while equipment entry events expose the
    // parsed battlefieldObjectId directly.  Battlefield observers consume
    // both event shapes so that a battlefield effect sees every permanent
    // entering it.
    private static bool TryEventBattlefieldId(GameEvent ev, out string battlefieldObjectId)
    {
        if (TryEventString(ev, "battlefieldObjectId", out battlefieldObjectId)
            || TryEventString(ev, "battlefieldId", out battlefieldObjectId))
            return true;

        if (TryEventString(ev, "destination", out var destination)
            && destination.StartsWith("BATTLEFIELD:", StringComparison.OrdinalIgnoreCase))
        {
            battlefieldObjectId = destination[(destination.IndexOf(':') + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(battlefieldObjectId);
        }

        battlefieldObjectId = string.Empty;
        return false;
    }

    private static ResolutionResult ResolveRadBattlefieldObservers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress)
            return result;

        result = ResolveRad012Observers(before, result);

        var state = result.State;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var runePools = state.RunePools.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var scores = NormalizeScoresForSeats(state).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var rngCursor = state.RngCursor;
        var effects = state.UntilEndOfTurnEffects.ToList();
        var queue = state.TriggerQueue.ToList();
        var events = result.Events.ToList();
        var changed = false;

        foreach (var (ev, eventIndex) in result.Events.Select((ev, index) => (ev, index)))
        {
            if (ev.Kind == "BATTLE_RESULT_DETERMINED"
                && ev.Payload.GetValueOrDefault("continuation") is BattleResultContinuation battle
                && battle.WinnerPlayerId is { } battleWinner)
            {
                foreach (var equipment in cards.Values
                    .Where(card => card.CardNo == Rad024CardNo && !card.IsFaceDown
                        && card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                        && IsObjectOnField(zones, card.ObjectId)
                        && EffectiveFieldControllerId(zones, card.ObjectId, card) == battleWinner
                        && locations.GetValueOrDefault(card.ObjectId) is { Zone: "BATTLEFIELD", BattlefieldObjectId: var field }
                        && field == battle.BattlefieldId)
                    .OrderBy(card => card.ObjectId, StringComparer.Ordinal))
                {
                    var marker = $"RAD024_DRAW:{battle.ResultTick}:{battle.BattlefieldId}:{equipment.ObjectId}:{equipment.ObjectGeneration}";
                    if (effects.Contains(marker, StringComparer.Ordinal)) continue;
                    var drawState = state with {
                        PlayerZones = zones, CardObjects = cards, PlayerScores = scores,
                        RngCursor = rngCursor, ObjectLocations = locations };
                    var draw = ApplyDrawToPlayer(drawState, zones, cards, scores, battleWinner, 1,
                        drawState.RngCursor, events, deferNormalVictory: true);
                    scores = draw.PlayerScores.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                    rngCursor = draw.RngCursor;
                    effects.Add(marker);
                    events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "幻梦之根在此处赢得战斗后抽牌",
                        new Dictionary<string, object?> {
                            ["playerId"] = battleWinner, ["battlefieldObjectId"] = battle.BattlefieldId,
                            ["battlefieldCardNo"] = Rad024CardNo, ["sourceObjectId"] = equipment.ObjectId,
                            ["trigger"] = "RAD024_BATTLE_WIN_DRAW", ["count"] = 1 }));
                    changed = true;
                }
            }

            if (ev.Kind == "CARD_PLAYED"
                && TryEventString(ev, "playerId", out var rad069Player))
            {
                foreach (var source in cards.Values
                    .Where(card => card.CardNo == Rad069CardNo && !card.IsFaceDown
                        && card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                        && IsObjectOnField(zones, card.ObjectId)
                        && EffectiveFieldControllerId(zones, card.ObjectId, card) == rad069Player)
                    .OrderBy(card => card.ObjectId, StringComparer.Ordinal))
                {
                    var marker = $"RAD069_DRAW:{state.TurnNumber}:{state.Tick}:{eventIndex}:{rad069Player}:{source.ObjectId}";
                    if (effects.Contains(marker, StringComparer.Ordinal)) continue;
                    var priorHandCount = zones.GetValueOrDefault(rad069Player)?.Hand.Count ?? 0;
                    var drawState = state with {
                        PlayerZones = zones, CardObjects = cards, PlayerScores = scores,
                        RngCursor = rngCursor, ObjectLocations = locations };
                    var draw = ApplyDrawToPlayer(drawState, zones, cards, scores, rad069Player, 2,
                        drawState.RngCursor, events, deferNormalVictory: true);
                    scores = draw.PlayerScores.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                    rngCursor = draw.RngCursor;
                    var drawn = (zones.GetValueOrDefault(rad069Player)?.Hand.Count ?? 0) - priorHandCount;
                    effects.Add(marker);
                    changed = true;
                    if (drawn <= 0 || (zones.GetValueOrDefault(rad069Player)?.Hand.Count(id => id != source.ObjectId) ?? 0) < 9)
                        continue;

                    var opponents = state.Seats.Keys.Where(id => id != rad069Player)
                        .Order(StringComparer.Ordinal).ToArray();
                    if (opponents.Length == 0) continue;
                    var selectedOpponent = RuleChoices.Value!.Choose(rad069Player,
                        "临界零点：选择一名对手获得1分。",
                        opponents.Select(id => new RuleChoiceOption(id, id)).ToArray(),
                        kind: "RAD069_OPPONENT", privateReason: "选择一名对手获得1分。");

                    var discarded = zones[rad069Player].Hand.ToArray();
                    foreach (var id in discarded)
                        if (!TryDiscardCardFromHand(zones, cards, rad069Player, id))
                            throw new InvalidOperationException("RAD069 hand discard no longer legal.");
                    if (discarded.Length > 0)
                        ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, rad069Player,
                            "CARD_DISCARDED", source.ObjectId, discarded, events);
                    events.Add(new("CARD_DISCARDED", "临界零点弃置手牌",
                        new Dictionary<string, object?> { ["playerId"] = rad069Player, ["sourceObjectId"] = source.ObjectId,
                            ["count"] = discarded.Length, ["reason"] = "RAD069_CRITICAL_ZERO" }));

                    var destroyItem = new StackItemState($"rad069-destroy-{state.Tick}-{eventIndex}-{source.ObjectId}",
                        rad069Player, source.ObjectId, "RAD_069_CRITICAL_ZERO", Rad069CardNo, [], 0, 1, []);
                    var destroyState = state with {
                        PlayerZones = zones, CardObjects = cards, PlayerScores = scores,
                        RunePools = runePools, ObjectLocations = locations };
                    var removal = ResolveFieldDestructions(destroyState, zones, cards, destroyItem,
                        new HashSet<string>(StringComparer.Ordinal), state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal),
                        runePools, objectLocations: locations,
                        explicitDestroyObjectIds: new HashSet<string>(StringComparer.Ordinal) { source.ObjectId },
                        explicitDestroyReason: "SKILL_EFFECT");
                    runePools = removal.RunePools.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                    queue.AddRange(removal.TriggerQueue);
                    events.AddRange(removal.Events);
                    scores[selectedOpponent] = scores.GetValueOrDefault(selectedOpponent) + 1;
                    var winner = scores[selectedOpponent] >= EffectiveWinningScore(state) ? selectedOpponent : null;
                    events.Add(new("SCORE_GAINED", "临界零点让对手获得1分",
                        new Dictionary<string, object?> { ["playerId"] = selectedOpponent, ["sourceObjectId"] = source.ObjectId,
                            ["amount"] = 1, ["score"] = scores[selectedOpponent], ["reason"] = "RAD069_CRITICAL_ZERO" }));
                    if (winner is not null)
                        result = result with { State = result.State with { Status = MatchStatuses.Finished, WinnerPlayerId = winner } };
                }
            }

            if (ev.Kind == "CARD_HIDDEN"
                && TryEventString(ev, "playerId", out var standbyPlayer)
                && TryEventString(ev, "battlefieldObjectId", out var standbyField)
                && TryEventString(ev, "sourceObjectId", out var hiddenObject)
                && IsRadBattlefieldCard(state, standbyField, Rad157CardNo))
            {
                var marker = Rad157GoldMarker(state.TurnNumber, standbyField, hiddenObject);
                if (effects.Contains(marker, StringComparer.Ordinal))
                    continue;

                CreateLegendEquipmentToken(zones, cards, standbyPlayer, standbyField,
                    TriggerKinds.BattlefieldDeployStandbyCreateGold, "金币",
                    [CardObjectTags.EquipmentCard, "金币", "反应"], true, events, Rad157CardNo);
                effects.Add(marker);
                events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "班德尔斥候学院打出休眠金币",
                    new Dictionary<string, object?> {
                        ["playerId"] = standbyPlayer, ["battlefieldObjectId"] = standbyField,
                        ["battlefieldCardNo"] = Rad157CardNo, ["trigger"] = TriggerKinds.BattlefieldDeployStandbyCreateGold,
                        ["sourceObjectId"] = hiddenObject
                    }));
                changed = true;
            }

            if (ev.Kind == "BATTLEFIELD_CONQUERED"
                && TryEventString(ev, "playerId", out var conqueror)
                && TryEventString(ev, "battlefieldId", out var conqueredField)
                && IsRadBattlefieldCard(state, conqueredField, Rad162CardNo))
            {
                var marker = Rad162NextHasteMarkerPrefix(conqueror) + state.Tick + ":" + conqueredField;
                if (!effects.Contains(marker, StringComparer.Ordinal))
                {
                    effects.Add(marker);
                    events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "野火帮秘密基地授予下一名单位急速",
                        new Dictionary<string, object?> {
                            ["playerId"] = conqueror, ["battlefieldObjectId"] = conqueredField,
                            ["battlefieldCardNo"] = Rad162CardNo, ["trigger"] = TriggerKinds.BattlefieldConquerNextUnitHaste,
                            ["effectId"] = marker
                        }));
                    changed = true;
                }
            }

            if (ev.Kind is "UNIT_PLAYED_TO_BATTLEFIELD" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD"
                && TryEventString(ev, "playerId", out var playedBy)
                && TryEventBattlefieldId(ev, out var playedField)
                && IsRadBattlefieldCard(state, playedField, Rad163CardNo))
            {
                var marker = Rad163UsedMarker(state.TurnNumber, playedBy, playedField);
                if (!effects.Contains(marker, StringComparer.Ordinal))
                {
                    effects.Add(marker);
                    events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "猎风广场减少首个永久物费用",
                        new Dictionary<string, object?> {
                            ["playerId"] = playedBy, ["battlefieldObjectId"] = playedField,
                            ["battlefieldCardNo"] = Rad163CardNo, ["trigger"] = TriggerKinds.BattlefieldFirstPermanentCostReduction,
                            ["amount"] = 1
                        }));
                    changed = true;
                }

            }

            // RAD·165 is a separate battlefield identity with the same
            // per-player/per-turn first-permanent timing as RAD·163.  Keep it
            // outside the RAD·163 branch so the Insight battlefield can fire
            // on its own.
            if (ev.Kind is "UNIT_PLAYED_TO_BATTLEFIELD" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD"
                && TryEventString(ev, "playerId", out var insightPlayer)
                && TryEventBattlefieldId(ev, out var insightField)
                && IsRadBattlefieldCard(state, insightField, Rad165CardNo))
            {
                QueueRad165Insight(state, effects, queue, events, insightPlayer, insightField, ev);
                changed = true;
            }

            if (ev.Kind == "UNIT_DESTROYED"
                && ev.Payload.GetValueOrDefault("capturedUnitDeath") is CapturedUnitDeath death
                && !death.Unit.WasToken
                && death.Unit.Location is { Zone: "BATTLEFIELD", BattlefieldObjectId: { } deathField }
                && IsRadBattlefieldCard(state, deathField, Rad159CardNo))
            {
                var owner = death.Unit.ControllerId;
                var marker = Rad159UsedMarker(state.TurnNumber, owner, deathField);
                if (!effects.Contains(marker, StringComparer.Ordinal) && zones.ContainsKey(owner))
                {
                    CreateBattlefieldUnitTokensInBase(zones, cards, owner, deathField, "随从", 1, 1,
                        TriggerKinds.BattlefieldUnitDestroyedCreateMinion, events, effects);
                    effects.Add(marker);
                    events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "黑色玫瑰圣所打出随从",
                        new Dictionary<string, object?> {
                            ["playerId"] = owner, ["battlefieldObjectId"] = deathField,
                            ["battlefieldCardNo"] = Rad159CardNo, ["trigger"] = TriggerKinds.BattlefieldUnitDestroyedCreateMinion,
                            ["destroyedObjectId"] = death.Unit.ObjectId
                        }));
                    changed = true;
                }
            }
        }

        // The marker belongs to the accepted unit play, so consume exactly one
        // RAD·162 grant after the play event has been recorded.
        foreach (var ev in result.Events.Where(e => e.Kind == "UNIT_PLAYED_TO_BASE" || e.Kind == "UNIT_PLAYED_TO_BATTLEFIELD"))
        {
            if (!TryEventString(ev, "playerId", out var player)
                || !CardPlayEventIsUnit(state, ev)) continue;
            var marker = effects.Where(id => id.StartsWith(Rad162NextHasteMarkerPrefix(player), StringComparison.Ordinal))
                .Order(StringComparer.Ordinal).FirstOrDefault();
            if (marker is not null)
            {
                effects.Remove(marker);
                changed = true;
            }
        }

        if (!changed && queue.Count == state.TriggerQueue.Count)
            return result;

        var next = state with {
            PlayerZones = zones,
            CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones),
            RunePools = runePools,
            PlayerScores = scores,
            RngCursor = rngCursor,
            UntilEndOfTurnEffects = effects.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            TriggerQueue = queue
        };
        if (result.State.Status == MatchStatuses.Finished)
            next = next with { Status = MatchStatuses.Finished, WinnerPlayerId = result.State.WinnerPlayerId };
        return result with {
            State = next,
            Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(next),
            Prompts = BuildCorePrompts(next)
        };
    }

    private static ResolutionResult ResolveRad012Observers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress)
            return result;

        var state = result.State;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var grants = (state.TimedBanishPlayGrants ?? []).ToList();
        var events = result.Events.ToList();
        var changed = false;
        var leaveKinds = new HashSet<string>(StringComparer.Ordinal) {
            "EQUIPMENT_DESTROYED", "EQUIPMENT_RETURNED_TO_HAND", "EQUIPMENT_RECALLED_TO_BASE",
            "UNIT_BANISHED", "UNIT_RECALLED_TO_BASE" };

        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index)))
        {
            string? playedObject = null;
            string? leftObject = null;
            var isPlay = ev.Kind == "CARD_PLAYED"
                && TryEventString(ev, "sourceObjectId", out playedObject)
                && (ev.Payload.GetValueOrDefault("cardNo") as string) == Rad012CardNo;
            var isLeave = leaveKinds.Contains(ev.Kind)
                && TryEventString(ev, "targetObjectId", out leftObject)
                && IsRad012Object(before, state, leftObject);
            if (!isPlay && !isLeave) continue;
            var sourceId = isPlay ? playedObject! : leftObject!;
            var actor = ev.Payload.GetValueOrDefault("playerId") as string
                ?? ev.Payload.GetValueOrDefault("controllerId") as string
                ?? before.CardObjects.GetValueOrDefault(sourceId)?.ControllerId
                ?? state.CardObjects.GetValueOrDefault(sourceId)?.OwnerId;
            if (string.IsNullOrWhiteSpace(actor) || !zones.ContainsKey(actor)) continue;
            var marker = $"RAD012_BANISH:{state.TurnNumber}:{state.Tick}:{index}:{sourceId}";
            if (grants.Any(g => g.Kind == "RAD012" && g.Id == marker) || state.UntilEndOfTurnEffects.Contains(marker, StringComparer.Ordinal))
                continue;
            var own = zones[actor];
            if (own.MainDeck.FirstOrDefault() is not { } topId || !cards.TryGetValue(topId, out var top))
                continue;

            var restored = PrintedCardFactory.TryRestoreOutsidePlay(top, actor, out var printed) ? printed : top;
            var generation = checked(top.ObjectGeneration + 1);
            cards[topId] = restored with { ObjectGeneration = generation, IsFaceDown = false };
            zones[actor] = own with { MainDeck = own.MainDeck.Skip(1).ToArray(), Banished = own.Banished.Append(topId).Distinct(StringComparer.Ordinal).ToArray() };
            locations = ReconcileObjectLocations(locations, zones);
            var resolvingStackItemId = ev.Payload.GetValueOrDefault("stackItemId") as string
                ?? ev.Payload.GetValueOrDefault("parentStackItemId") as string
                ?? $"rad012-{state.Tick}-{index}-{sourceId}";
            var expiry = state.TurnPlayerId == actor && state.Phase != MatchPhases.TurnEnd ? state.TurnNumber : (int?)null;
            var grant = new TimedBanishPlayGrant("", actor, state.TurnNumber,
                RuleChoices.Value?.CompletionTick ?? state.Tick + 1, resolvingStackItemId, null,
                new(topId, generation), cards[topId].CardNo!, "RAD012", expiry);
            grant = grant with { Id = TimedBanishPlayRules.Id(grant) };
            grants.Add(grant);
            events.Add(new("CARD_BANISHED", "符刃铁砧放逐主牌堆顶牌并授予期间出牌权限",
                new Dictionary<string, object?> {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, sourceId, "__spectator__"),
                    ["targetObjectId"] = topId, ["cardNo"] = cards[topId].CardNo,
                    ["playerId"] = actor, ["destinationZone"] = "BANISHED", ["permissionId"] = grant.Id,
                    ["reason"] = "RAD012" }));
            // The marker is private state only; it prevents a replayed collector
            // pass from processing the same event twice.
            state = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Append(marker).ToArray() };
            changed = true;
        }

        if (!changed) return result;
        var next = result.State with {
            PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones),
            TimedBanishPlayGrants = grants.ToArray(),
            UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
        return result with { State = next, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static bool IsRad012Object(MatchState before, MatchState state, string objectId)
        => before.CardObjects.GetValueOrDefault(objectId)?.CardNo == Rad012CardNo
            || state.CardObjects.GetValueOrDefault(objectId)?.CardNo == Rad012CardNo;

    private static bool CardPlayEventIsUnit(MatchState state, GameEvent ev)
        => TryEventString(ev, "sourceObjectId", out var source)
            && state.CardObjects.TryGetValue(source, out var card)
            && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);

    private static void QueueRad165Insight(MatchState state, List<string> effects, List<TriggerQueueItemState> queue,
        List<GameEvent> events, string player, string field, GameEvent playEvent)
    {
        var marker = $"RAD165_USED:{state.TurnNumber}:{player}:{field}";
        if (effects.Contains(marker, StringComparer.Ordinal)
            || queue.Any(item => IsRad165InsightKind(item.InsightContext?.Kind)
                && item.SourceObjectId == field && item.ControllerId == player))
            return;

        var card = state.CardObjects[field];
        var trigger = new TriggerQueueItemState(
            $"rad165-insight-{state.TurnNumber}-{state.Tick}-{field}-{playEvent.Payload.GetValueOrDefault("sourceObjectId")}",
            player, field, InsightTriggerEffect, playEvent.Kind)
        {
            SourceCardNo = card.CardNo,
            EnqueuedAtTick = state.Tick,
            InsightContext = new InsightTriggerContext(card.CardNo!, player, 1, card.ObjectGeneration,
                Rad165InsightKind)
        };
        queue.Add(trigger);
        events.Add(BuildTriggerQueuedEvent(trigger));
        effects.Add(marker);
    }
}
