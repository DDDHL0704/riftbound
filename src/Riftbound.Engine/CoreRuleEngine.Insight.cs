using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 436: Insight privately recycles any subset, then orders retained cards on top.
    // It is an instruction of the same resolving spell, not a new response window.
    private static StackResolutionResult ResolveInsightSpell(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        if (item.InsightCompleted) return FinishInsight(state, item);
        var effect = ResolveStackItemEffect(state, item, deferCompletion: true, skipInsight: true);
        return BeginInsight(state, item, effect, 1);
    }

    private static StackResolutionResult BeginInsight(MatchState state, StackItemState item, StackResolutionResult effect, int count)
    {
        var stack = RemoveCounteredStackItems(effect.StackItems ?? state.StackItems, effect.CounteredStackItemIds);
        var current = state with { PlayerZones = effect.PlayerZones, CardObjects = effect.CardObjects,
            RunePools = effect.RunePools, PlayerScores = effect.PlayerScores, PlayerExperience = effect.PlayerExperience,
            UntilEndOfTurnEffects = effect.UntilEndOfTurnEffects, StackItems = stack, RngCursor = effect.RngCursor,
            ObjectLocations = effect.ObjectLocations ?? ReconcileObjectLocations(state.ObjectLocations, effect.PlayerZones) };
        var viewed = current.PlayerZones[item.ControllerId].MainDeck.Take(count).ToArray();
        if (viewed.Length == 0 || effect.WinnerPlayerId is not null)
        {
            var finished = FinishInsight(current, item);
            return finished with { Events = effect.Events.Concat(finished.Events).ToArray(),
                CounteredStackItemIds = effect.CounteredStackItemIds,
                WinnerPlayerId = effect.WinnerPlayerId ?? finished.WinnerPlayerId };
        }
        var choice = new PendingCardChoiceState("INSIGHT:" + item.StackItemId, "INSIGHT", item.ControllerId,
            0, viewed.Length, viewed, viewed, $"洞察 {count}：选择任意数量的牌回收；不选则全部保留。若保留多张，下一步决定顶部顺序。", item.SourceObjectId, item.EffectKind)
            { DeckContext = new(item.StackItemId, 0) };
        return effect with { StackItems = stack, PendingCardChoice = choice,
            Events = effect.Events.Append(new GameEvent("INSIGHT_REQUESTED", "等待玩家私下完成洞察",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["sourceObjectId"] = item.SourceObjectId, ["choiceId"] = choice.ChoiceId, ["count"] = viewed.Length })).ToArray() };
    }

    private static StackResolutionResult FinishInsight(MatchState state, StackItemState item)
    {
        if (item.InsightContext is { } context)
        {
            var triggerZones = NormalizeZonesForSeats(state);
            var triggerEvents = new List<GameEvent>();
            var scores = state.PlayerScores;
            var experience = state.PlayerExperience;
            var rng = state.RngCursor;
            string? winner = null;
            var shouldDraw = context.Kind == "ACTIVATED";
            if (context is { Kind: "DUEL", PaymentAccepted: true }
                && triggerZones[item.ControllerId].MainDeck.FirstOrDefault() is { } top)
            {
                var card = state.CardObjects[top];
                triggerEvents.Add(new("CARD_REVEALED", "黛安娜展示洞察后的牌库顶", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["cardObjectId"] = top, ["cardNo"] = card.CardNo }));
                shouldDraw = CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var topBehavior) && IsSpellPlayBehavior(topBehavior);
            }
            if (shouldDraw)
            {
                var draw = ApplyDrawToPlayer(state, triggerZones, scores, item.ControllerId, 1, rng, triggerEvents);
                scores = draw.PlayerScores; rng = draw.RngCursor; winner = draw.WinnerPlayerId;
            }
            if (context.Kind == "ACTIVATED" && winner is null)
                experience = GainExperience(experience, item.ControllerId, 1, item, triggerEvents, item.SourceObjectId, context.CardNo);
            triggerEvents.Add(new("TRIGGER_RESOLVED", "洞察技能结算完成", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["controllerId"] = item.ControllerId, ["effectKind"] = item.EffectKind }));
            return NoopStackResolutionResult(state) with { PlayerZones = triggerZones, PlayerScores = scores,
                PlayerExperience = experience, RngCursor = rng, WinnerPlayerId = winner,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, triggerZones),
                StackItems = state.StackItems.Where(s => s.StackItemId != item.StackItemId).ToArray(), Events = triggerEvents };
        }
        CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        CompleteSpellSource(zones, cards, item, behavior);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            StackItems = state.StackItems.Where(s => s.StackItemId != item.StackItemId).ToArray(),
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

    private static ResolutionResult ResolveInsightChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        var item = state.StackItems.LastOrDefault();
        if (!ValidInsightChoice(state, choice, item))
            return RejectWithCorePrompts(state, "洞察上下文已经失效。", ErrorCodes.InvalidTarget);
        var zones = NormalizeZonesForSeats(state);
        var player = choice.PlayerId;
        var deck = zones[player].MainDeck;
        var events = new List<GameEvent>();
        var rng = state.RngCursor;
        if (choice.ChoiceWindow == "INSIGHT")
        {
            var retained = choice.ContextObjectIds.Where(id => !selected.Contains(id)).ToArray();
            var recycled = RandomizeForMainDeckBottom(selected, state.Seed, rng, item!.SourceObjectId);
            if (selected.Count > 1) rng++;
            zones[player] = zones[player] with { MainDeck = retained.Concat(deck.Skip(choice.ContextObjectIds.Count)).Concat(recycled).ToArray() };
            if (selected.Count > 0) events.Add(new("CARDS_RECYCLED", $"洞察回收 {selected.Count} 张牌", new Dictionary<string, object?>
                { ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["count"] = selected.Count }));
            if (retained.Length > 1)
            {
                var order = new PendingCardChoiceState("INSIGHT-ORDER:" + item.StackItemId, "INSIGHT_ORDER", player,
                    retained.Length, retained.Length, retained, retained, "请按从顶到底的顺序选择保留的牌。", item.SourceObjectId, item.EffectKind)
                    { DeckContext = new(item.StackItemId, 0) };
                var ordering = state with { Tick = state.Tick + 1, PlayerZones = zones, RngCursor = rng, PendingCardChoice = order };
                events.Add(new("INSIGHT_ORDER_REQUESTED", "等待玩家私下决定牌库顶部顺序", new Dictionary<string, object?>
                    { ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["count"] = retained.Length }));
                return new(true, null, ordering, events, ResolutionResult.BuildSnapshots(ordering), BuildCorePrompts(ordering));
            }
        }
        else zones[player] = zones[player] with { MainDeck = selected.Concat(deck.Skip(choice.ContextObjectIds.Count)).ToArray() };
        var next = state with { PendingCardChoice = null, PlayerZones = zones, RngCursor = rng,
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(item! with { InsightCompleted = true }).ToArray() };
        var resumed = ResolvePassPriority(next, new("insight-continuation", player, CommandTypes.PassPriority), forceResolve: true);
        events.Add(new("INSIGHT_COMPLETED", "已完成洞察", new Dictionary<string, object?>
            { ["playerId"] = player, ["sourceObjectId"] = item!.SourceObjectId,
                ["recycledCount"] = choice.ChoiceWindow == "INSIGHT" ? selected.Count : 0 }));
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }

    internal static bool ValidInsightChoice(MatchState state, PendingCardChoiceState choice, StackItemState? item)
    {
        if (item is null || item.InsightCompleted || item.EffectRepeatCount != 1
            || item.InsightContext is { Kind: "DUEL", PaymentAccepted: false }
            || choice.DeckContext is not { ExecutionIndex: 0 } context || context.StackItemId != item.StackItemId
            || choice.PlayerId != item.ControllerId || choice.SourceObjectId != item.SourceObjectId || choice.EffectKind != item.EffectKind
            || !state.PlayerZones.TryGetValue(item.ControllerId, out var zones)) return false;
        var count = item.InsightContext is { } captured && ValidInsightTrigger(captured, item.EffectKind, item.ControllerId, item.CardNo)
            ? captured.Count : CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior) && behavior.PerformsInsight ? 1 : 0;
        var viewedCount = choice.ContextObjectIds.Count;
        if (count == 0 || viewedCount == 0 || !zones.MainDeck.Take(viewedCount).SequenceEqual(choice.ContextObjectIds)
            || !choice.ContextObjectIds.SequenceEqual(choice.LegalObjectIds)) return false;
        return choice.ChoiceWindow switch {
            "INSIGHT" => choice.ChoiceId == "INSIGHT:" + item.StackItemId && choice.RequiredCount == 0
                && choice.MaxCount == Math.Min(count, zones.MainDeck.Count) && viewedCount == choice.MaxCount,
            "INSIGHT_ORDER" => choice.ChoiceId == "INSIGHT-ORDER:" + item.StackItemId && viewedCount > 1 && viewedCount <= count
                && choice.RequiredCount == viewedCount && choice.MaxCount == viewedCount,
            _ => false };
    }

    private static void CompleteSpellSource(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, StackItemState item, CardBehaviorDefinition behavior)
    {
        if (!behavior.PlaysSourceToBaseAsEquipment && !behavior.PlaysSourceToBaseAsUnit)
            MoveStackSpellToOwnerZone(zones, cards, item, behavior.BanishesSourceOnResolution ? "BANISHED" : "GRAVEYARD");
    }
}
