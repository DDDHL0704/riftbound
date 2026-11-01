using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record BeginningDrawContext(string CardNo, string Kind, long SourceGeneration);

public sealed partial class CoreRuleEngine
{
    internal const string BeginningDrawEffect = "CAPTURED_BEGINNING_DRAW";
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> BeginningDrawFamilies = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["OGN·101/298"]));

    private static string? BeginningDrawKind(string? cardNo)
    {
        var normalized = OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo);
        if (normalized == "RAD·111/167") return "RAD111_SLOT_MACHINE";
        if (LegendCardHasIdentity(cardNo, LegendIdentityCatalog.JinxLegendIdentityId)) return "HAND_BELOW_TWO";
        if (BeginningDrawFamilies.Value["OGN·101/298"].Contains(normalized, StringComparer.Ordinal)) return "FACEDOWN_AT_START";
        return null;
    }

    private static bool ControlsFacedownBattlefieldCard(MatchState state, string player)
        => state.PlayerZones[player].Battlefields.Any(id => state.CardObjects.TryGetValue(id, out var card)
            && card.IsFaceDown && card.ControllerId == player
            && state.ObjectLocations.TryGetValue(id, out var at) && at.Zone == "BATTLEFIELD"
            && at.BattlefieldObjectId is { } field && state.CardObjects.TryGetValue(field, out var battlefield)
            && IsBattlefieldCardObject(battlefield));

    private static MatchState QueueBeginningDrawAbilities(MatchState state, List<GameEvent> events)
    {
        var player = state.TurnPlayerId;
        var queue = state.TriggerQueue.ToList();
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            var kind = BeginningDrawKind(source.CardNo);
            if (kind is null || source.ControllerId != player || source.IsFaceDown) continue;
            if (kind == "HAND_BELOW_TWO")
            {
                if (!state.PlayerZones[player].LegendZone.Contains(source.ObjectId) || !source.Tags.Contains("CARD_TYPE:LEGEND")) continue;
                // Official Jinx FAQ: the hand condition is an instruction, not a trigger condition.
            }
            else if (!IsObjectOnField(state.PlayerZones, source.ObjectId)
                || !IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, source.ObjectId)
                || !source.Tags.Contains(CardObjectTags.EquipmentCard) || !string.IsNullOrWhiteSpace(source.AttachedToObjectId)
                || !ControlsFacedownBattlefieldCard(state, player)) continue;
            var trigger = new TriggerQueueItemState($"beginning-draw-{state.TurnNumber}-{state.Tick}-{source.ObjectId}",
                player, source.ObjectId, BeginningDrawEffect, "TURN_START_BEGAN")
                { BeginningDraw = new(source.CardNo!, kind, source.ObjectGeneration), SourceCardNo = source.CardNo };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (source.CardNo != "RAD·111/167" || source.IsFaceDown
                || !source.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                || source.ControllerId != player || !IsObjectOnField(state.PlayerZones, source.ObjectId)
                || !IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, source.ObjectId)) continue;
            var trigger = new TriggerQueueItemState($"rad111-start-{state.TurnNumber}-{state.Tick}-{source.ObjectId}",
                player, source.ObjectId, BeginningDrawEffect, "TURN_START_BEGAN")
            {
                BeginningDraw = new(source.CardNo, "RAD111_SLOT_MACHINE", source.ObjectGeneration),
                SourceCardNo = source.CardNo
            };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        return state with { TriggerQueue = queue };
    }

    internal static bool ValidBeginningDraw(MatchState state, BeginningDrawContext? context, string effect,
        string controller, string source, string? cardNo = null)
        => context is null ? effect != BeginningDrawEffect
            : effect == BeginningDrawEffect && context.SourceGeneration >= 0 && !string.IsNullOrWhiteSpace(source)
                && context.Kind is "HAND_BELOW_TWO" or "FACEDOWN_AT_START" or "RAD111_SLOT_MACHINE"
                && BeginningDrawKind(context.CardNo) == context.Kind && (cardNo is null || context.CardNo == cardNo)
                && controller == state.TurnPlayerId && state.Phase == MatchPhases.TurnStart && state.TurnStartStep == "SCORE";

    private static StackResolutionResult ResolveBeginningDraw(MatchState state, StackItemState item)
    {
        var context = item.BeginningDraw!;
        if (context.Kind == "RAD111_SLOT_MACHINE") return ResolveRad111SlotMachine(state, item, context);
        if (context.Kind == "HAND_BELOW_TWO" && state.PlayerZones[item.ControllerId].Hand.Count >= 2)
            return NoopStackResolutionResult(state);
        // A condition in the trigger clause was checked at capture. It is not
        // checked a second time during resolution (CN 383.2.a.1, Sona example).
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent> { new(context.Kind == "HAND_BELOW_TWO" ? "LEGEND_TRIGGER_RESOLVED" : "TRIGGER_RESOLVED",
            "开始阶段技能抽一张牌", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["sourceCardNo"] = context.CardNo,
                ["trigger"] = context.Kind == "HAND_BELOW_TWO" ? "JINX_TURN_START_DRAW_IF_HAND_BELOW_TWO" : "MUSHROOM_BEGINNING_DRAW",
                ["drawCount"] = 1, ["beforeScoring"] = true }) };
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var draw = ApplyDrawToPlayer(state, zones, cards, NormalizeScoresForSeats(state), item.ControllerId, 1, state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores,
            WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

    private static StackResolutionResult ResolveRad111SlotMachine(MatchState state, StackItemState item,
        BeginningDrawContext context)
    {
        if (context.CardNo != "RAD·111/167" || item.CardNo != context.CardNo
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != context.SourceGeneration || source.ControllerId != item.ControllerId
            || !IsObjectOnField(state.PlayerZones, source.ObjectId))
            throw new InvalidOperationException("Invalid RAD·111 beginning ability.");

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> {
            new("TRIGGER_RESOLVED", "自走角子机展示每名玩家的牌库顶牌", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["sourceCardNo"] = context.CardNo,
                ["trigger"] = "RAD111_SLOT_MACHINE" }) };
        var tops = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var player in SeatPlayerIds(state))
        {
            var top = zones[player].MainDeck.FirstOrDefault();
            if (top is null || !cards.TryGetValue(top, out var card)) continue;
            tops[player] = top;
            events.Add(new("CARD_REVEALED", "自走角子机展示牌库顶牌", new Dictionary<string, object?> {
                ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = top, ["cardNo"] = card.CardNo, ["zone"] = TriggerZones.MainDeck,
                ["reason"] = "RAD111_SLOT_MACHINE" }));
        }

        var ownTop = tops.GetValueOrDefault(item.ControllerId);
        var ownCost = ownTop is not null && cards.TryGetValue(ownTop, out var ownCard) ? ownCard.ManaCost : int.MaxValue;
        var lowest = tops.Values.Select(id => cards[id].ManaCost).DefaultIfEmpty(int.MaxValue).Min();
        var draw = ownTop is not null && ownCost <= lowest;
        var result = NoopStackResolutionResult(state);
        if (draw)
        {
            var drawState = state with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
            var application = ApplyDrawToPlayer(drawState, zones, cards, state.PlayerScores,
                item.ControllerId, 1, state.RngCursor, events, deferNormalVictory: true);
            result = result with { PlayerScores = application.PlayerScores, WinnerPlayerId = application.WinnerPlayerId,
                RngCursor = application.RngCursor };
        }
        else if (ownTop is not null)
        {
            var recycled = RecycleLookedMainDeckCards(state, zones, cards, item.ControllerId,
                item.SourceObjectId, [ownTop], result.RngCursor);
            events.AddRange(recycled.Events); result = result with { RngCursor = recycled.RngCursor };
        }

        foreach (var (player, top) in tops)
        {
            if (player == item.ControllerId) continue;
            var recycled = RecycleLookedMainDeckCards(state, zones, cards, player,
                item.SourceObjectId, [top], result.RngCursor);
            events.AddRange(recycled.Events); result = result with { RngCursor = recycled.RngCursor };
        }
        return result with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
