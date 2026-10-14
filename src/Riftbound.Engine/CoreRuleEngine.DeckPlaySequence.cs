using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record DeckPlaySequenceContext(string Kind, ObjectBinding Source, string CardNo, string ControllerId,
    IReadOnlyList<string> Players, IReadOnlyDictionary<string,IReadOnlyDictionary<string,long>> Viewed,
    IReadOnlyDictionary<string,ObjectBinding> Selected, int SelectionIndex, int PlayIndex, bool ParentCompleted = false)
{
    public int ExecutionIndex { get; init; }
    public IReadOnlyList<DeckPlayQueuedCard> Queue { get; init; } = [];
}
public sealed record DeckPlayQueuedCard(string OwnerId, ObjectBinding Card, string CardNo, long BeforeBanishGeneration, int ExecutionIndex);
public sealed record DeckPlayPendingCardContext(string ParentStackItemId, string Kind, string OwnerId, string PlayerId,
    ObjectBinding Card, string CardNo, int OrderIndex);

public sealed partial class CoreRuleEngine
{
    internal const string DeckPlayChoiceWindow = "DECK_PLAY_SELECTION";

    private static string[] DeckPlayPlayers(MatchState state, string controller, string kind)
        => kind.StartsWith("SINGLE_", StringComparison.Ordinal) ? [controller]
            : kind == "OPPONENT_REVEAL_PLAY" ? PlayerIdsStartingAfter(state, controller).Where(p => p != controller).ToArray()
            : new[] { state.TurnPlayerId }.Concat(PlayerIdsStartingAfter(state, state.TurnPlayerId).Where(p => p != state.TurnPlayerId)).ToArray();

    private static int DeckPlayLookCount(CardBehaviorDefinition definition)
        => definition.DeckPlayKind == "OPPONENT_REVEAL_PLAY" ? 1
            : definition.DeckPlayKind == "EACH_PLAYER_LOOK_PLAY" ? 5 : definition.MainDeckLookCount;

    internal static bool DeckPlaySequenceReveals(StackItemState item)
        => TryGetEffectPlayDefinition(item, out var definition) && (definition.DeckPlayKind == "OPPONENT_REVEAL_PLAY" || definition.EffectPlayRevealsCards);

    private static string[] DeckPlayLegalSelection(MatchState state, DeckPlaySequenceContext c, string owner)
        => c.Viewed[owner].Keys.Where(id => c.Kind != "SINGLE_PRIVATE_UNIT_PLAY"
            || state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)).ToArray();

    private static PendingEffectPlayState DeckPlayPending(StackItemState item, DeckPlaySequenceContext c)
    {
        TryGetEffectPlayDefinition(item, out var definition);
        var chosen = c.Queue[c.PlayIndex]; var card = chosen.Card;
        var actor = c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : chosen.OwnerId;
        return new($"DECK-PLAY-EFFECT-{item.StackItemId}-{c.PlayIndex}", actor, item, "STACK",
            new Dictionary<string,long> { [card.ObjectId] = card.Generation },
            c.Kind is "OPPONENT_REVEAL_PLAY" or "EACH_PLAYER_LOOK_PLAY" || definition.EffectPlayIgnoreBaseMana,
            c.Kind == "OPPONENT_REVEAL_PLAY" || definition.EffectPlayIgnoreBasePower,
            definition.EffectPlayManaReduction, definition.EffectPlayDestination, false,
            IgnoreAllCosts: c.Kind == "OPPONENT_REVEAL_PLAY");
    }

    private static StackResolutionResult BeginDeckPlaySequence(MatchState state, StackItemState item, CardBehaviorDefinition definition)
    {
        // Declared deck identities are never valid targets for these effects.
        if (item.TargetObjectIds.Count != 0) return ResolveStackItemEffect(state, item with { EffectPlayCompleted = true });
        var events = new List<GameEvent>();
        var c = item.DeckPlaySequence;
        if (c is null)
        {
            var players = DeckPlayPlayers(state, item.ControllerId, definition.DeckPlayKind);
            var count = DeckPlayLookCount(definition);
            var viewed = players.ToDictionary(p => p, p => (IReadOnlyDictionary<string,long>)state.PlayerZones[p].MainDeck.Take(count)
                .ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration), StringComparer.Ordinal);
            var generation = item.FieldContext is { Kind: "ATTACK_DECK_PLAY" } field ? field.SourceGeneration : state.CardObjects[item.SourceObjectId].ObjectGeneration;
            c = new(definition.DeckPlayKind, new(item.SourceObjectId, generation),
                item.CardNo!, item.ControllerId, players, viewed, new Dictionary<string,ObjectBinding>(), 0, 0);
            if (DeckPlaySequenceReveals(item))
                foreach (var player in players)
                    if (viewed[player].Count > 0) events.Add(new("CARDS_REVEALED", "对手展示主牌堆顶部的卡牌", new Dictionary<string,object?> {
                        ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["count"] = viewed[player].Count,
                        ["cards"] = viewed[player].Keys.Select(id => new { objectId = id, cardNo = state.CardObjects[id].CardNo }).ToArray() }));
        }
        while (c.SelectionIndex < c.Players.Count && c.Viewed[c.Players[c.SelectionIndex]].Count == 0)
            c = c with { SelectionIndex = c.SelectionIndex + 1 };
        item = item with { DeckPlaySequence = c };
        if (c.SelectionIndex < c.Players.Count)
        {
            var owner = c.Players[c.SelectionIndex];
            var ids = c.Viewed[owner].Keys.ToArray();
            var legal = DeckPlayLegalSelection(state, c, owner);
            var required = definition.EffectPlayOptional ? 0 : 1;
            var actor = c.Kind == "OPPONENT_REVEAL_PLAY" ? item.ControllerId : owner;
            var choice = new PendingCardChoiceState(DeckPlaySelectionId(item, c), DeckPlayChoiceWindow,
                actor, required, legal.Length > 0 ? 1 : 0, legal, ids,
                c.Kind == "OPPONENT_REVEAL_PLAY" ? "选择展示的卡牌，放逐后无视费用打出"
                    : definition.DeckPlayDrawsUnselected ? "可以放逐一张展示的牌并打出；抽取未放逐的牌"
                    : "查看主牌堆顶部的牌，选择放逐并打出，回收其余牌",
                item.SourceObjectId, item.EffectKind) { DeckContext = new(item.StackItemId, c.SelectionIndex) };
            events.Add(new("CARD_CHOICE_REQUESTED", "等待结算中的牌堆选择", new Dictionary<string,object?> {
                ["playerId"] = actor, ["sourceObjectId"] = item.SourceObjectId, ["choiceId"] = choice.ChoiceId,
                ["choiceWindow"] = choice.ChoiceWindow, ["contextCount"] = ids.Length }));
            return NoopStackResolutionResult(state) with { StackItems = state.StackItems.Select(s => s.StackItemId == item.StackItemId ? item : s).ToArray(),
                PendingCardChoice = choice, Events = events };
        }
        var queued = c.Queue.ToList();
        foreach (var owner in DeckPlayOrder(c))
            if (c.Selected.TryGetValue(owner, out var selected))
                queued.Add(new(owner, selected, state.CardObjects[selected.ObjectId].CardNo ?? "", selected.Generation - 1, c.ExecutionIndex));
        c = c with { Queue = queued };
        if (c.ExecutionIndex + 1 < item.EffectRepeatCount)
        {
            var count = DeckPlayLookCount(definition);
            c = c with { ExecutionIndex = c.ExecutionIndex + 1, SelectionIndex = 0,
                Selected = new Dictionary<string,ObjectBinding>(), Viewed = c.Players.ToDictionary(p => p,
                    p => (IReadOnlyDictionary<string,long>)state.PlayerZones[p].MainDeck.Take(count).ToDictionary(id => id,id => state.CardObjects[id].ObjectGeneration)) };
            if (DeckPlaySequenceReveals(item))
                foreach (var player in c.Players)
                    if (c.Viewed[player].Count > 0) events.Add(new("CARDS_REVEALED", "下一次执行展示主牌堆顶部的卡牌", new Dictionary<string,object?> {
                        ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["count"] = c.Viewed[player].Count,
                        ["cards"] = c.Viewed[player].Keys.Select(id => new { objectId = id, cardNo = state.CardObjects[id].CardNo }).ToArray() }));
            var again = BeginDeckPlaySequence(state, item with { DeckPlaySequence = c }, definition);
            return again with { Events = events.Concat(again.Events).ToArray() };
        }
        if (c.Queue.Count == 0)
            return ResolveStackItemEffect(state, item with { DeckPlaySequence = c, EffectPlayCompleted = true });
        StackResolutionResult? completed = null;
        if (!c.ParentCompleted)
        {
            completed = ResolveStackItemEffect(state, item with { DeckPlaySequence = c, EffectPlayCompleted = true });
            var zones = completed.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
            var cards = completed.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            var pendingQueue = c.Queue.ToArray();
            var stack = state.StackItems.Where(s => s.StackItemId != item.StackItemId).ToList();
            for (var index = 0; index < pendingQueue.Length; index++)
            {
                var entry = pendingQueue[index]; var owner = entry.OwnerId; var binding = entry.Card;
                var card = cards[binding.ObjectId];
                binding = binding with { Generation = checked(card.ObjectGeneration + 1) }; pendingQueue[index] = entry with { Card = binding };
                cards[card.ObjectId] = card with { ObjectGeneration = binding.Generation };
                zones[owner] = zones[owner] with { Banished = RemoveFromZone(zones[owner].Banished, card.ObjectId) };
                var actor = c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : owner;
                var pendingCard = new DeckPlayPendingCardContext(item.StackItemId, c.Kind, owner, actor, binding, card.CardNo!, index);
                var kind = CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var printed) ? printed.EffectKind : "UNSUPPORTED_PENDING_CARD";
                var slot = new StackItemState(stackItemId: $"PENDING-{item.StackItemId}-{index}-{card.ObjectId}", controllerId: actor,
                    sourceObjectId: card.ObjectId, effectKind: kind, targetObjectIds: [], cardNo: card.CardNo) { DeckPlayPendingCard = pendingCard };
                stack.Add(slot); events.Add(new("STACK_ITEM_PENDING", "效果要求打出的卡牌成为待确认项目", new Dictionary<string,object?> {
                    ["stackItemId"] = slot.StackItemId, ["sourceObjectId"] = card.ObjectId, ["cardNo"] = card.CardNo,
                    ["controllerId"] = actor, ["parentStackItemId"] = item.StackItemId, ["orderIndex"] = index }));
            }
            c = c with { ParentCompleted = true, Queue = pendingQueue };
            var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
            foreach (var slot in stack.Where(s => s.DeckPlayPendingCard is not null))
                locations[slot.SourceObjectId] = new(slot.ControllerId, "STACK");
            completed = completed with { PlayerZones = zones, CardObjects = cards, StackItems = stack,
                ObjectLocations = locations };
            events.AddRange(completed.Events);
            events.Add(new("STACK_ITEM_RESOLVED", "来源法术已完成结算，继续按顺序确认再次打出的卡牌", new Dictionary<string,object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId, ["controllerId"] = item.ControllerId,
                ["effectKind"] = item.EffectKind, ["cardNo"] = item.CardNo, ["targetObjectIds"] = item.TargetObjectIds }));
        }
        item = item with { DeckPlaySequence = c, EffectPlayCompleted = true };
        var pending = DeckPlayPending(item, c);
        return (completed ?? NoopStackResolutionResult(state)) with { PendingEffectPlay = pending, Events = events };
    }

    private static string[] DeckPlayOrder(DeckPlaySequenceContext c)
        => c.Kind == "OPPONENT_REVEAL_PLAY" ? c.Players.ToArray() : c.Players.Skip(1).Concat(c.Players.Take(1)).ToArray();

    private static ResolutionResult ResolveDeckPlaySelection(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        var item = state.StackItems.Last(); var c = item.DeckPlaySequence!;
        if (!ValidDeckPlayChoice(state, choice)) return RejectWithCorePrompts(state, "本次牌堆选择上下文已失效。", ErrorCodes.InvalidTarget);
        TryGetEffectPlayDefinition(item, out var definition);
        var owner = c.Players[c.SelectionIndex]; var id = selected.SingleOrDefault();
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        var picked = c.Selected.ToDictionary(p => p.Key, p => p.Value);
        if (id is not null)
        {
            var old = cards[id];
            var card = PrintedCardFactory.TryRestoreOutsidePlay(old, old.OwnerId ?? owner, out var printed) ? printed : old;
            cards[id] = card with { ControllerId = c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : owner,
                ObjectGeneration = checked(old.ObjectGeneration + 1) };
            zones[owner] = zones[owner] with { MainDeck = RemoveFromZone(zones[owner].MainDeck, id), Banished = zones[owner].Banished.Append(id).ToArray() };
            picked[owner] = new(id, cards[id].ObjectGeneration);
            events.Add(new("CARD_BANISHED", "所选牌被放逐，等待再次打出", new Dictionary<string,object?> {
                ["playerId"] = owner, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                ["cardNo"] = cards[id].CardNo, ["destinationZone"] = "BANISHED" }));
        }
        var remaining = c.Viewed[owner].Keys.Where(x => x != id).ToArray();
        if (definition.DeckPlayDrawsUnselected)
            ApplyDrawToPlayer(state, zones, state.PlayerScores, owner, remaining.Length, state.RngCursor, events);
        else
        {
            var recycled = RandomizeForMainDeckBottom(remaining, state.Seed, state.RngCursor, item.SourceObjectId);
            zones[owner] = zones[owner] with { MainDeck = zones[owner].MainDeck.Where(x => !remaining.Contains(x)).Concat(recycled).ToArray() };
            if (remaining.Length > 0) events.Add(new("CARDS_RECYCLED", "回收未选择的牌", new Dictionary<string,object?> {
                ["playerId"] = owner, ["sourceObjectId"] = item.SourceObjectId, ["count"] = remaining.Length }));
        }
        item = item with { DeckPlaySequence = c with { Selected = picked, SelectionIndex = c.SelectionIndex + 1 } };
        var next = state with { PendingCardChoice = null, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RngCursor = state.RngCursor + (!definition.DeckPlayDrawsUnselected && remaining.Length > 1 ? 1 : 0),
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(item).ToArray() };
        var resumed = ResolvePassPriority(next, new("deck-play-choice", choice.PlayerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }

    private static string DeckPlaySelectionId(StackItemState item, DeckPlaySequenceContext c)
        => $"DECK-PLAY-{item.StackItemId}-" + (item.EffectRepeatCount > 1 ? c.ExecutionIndex + "-" : "") + c.SelectionIndex;

    internal static bool ValidDeckPlaySequence(MatchState state, StackItemState item)
    {
        if (item.DeckPlaySequence is not { } c) return !TryGetEffectPlayDefinition(item, out var plain)
            || plain.DeckPlayKind.Length == 0 || item.TargetObjectIds.Count == 0;
        if (!TryGetEffectPlayDefinition(item, out var b) || b.DeckPlayKind != c.Kind || b.CardNo != item.CardNo
            || c.CardNo != item.CardNo || c.ControllerId != item.ControllerId || c.Source is null
            || c.Source.ObjectId != item.SourceObjectId || c.Source.Generation < 0
            || (item.FieldContext is { Kind: "ATTACK_DECK_PLAY" } captured
                ? c.Source.Generation != captured.SourceGeneration || !ValidFieldContextState(state, captured, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
                : !state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration < c.Source.Generation)
            || c.Players is null || c.Viewed is null || c.Selected is null || c.Queue is null
            || c.SelectionIndex < 0 || c.SelectionIndex > c.Players.Count || c.PlayIndex < 0 || c.PlayIndex > c.Queue.Count
            || c.ExecutionIndex < 0 || c.ExecutionIndex >= item.EffectRepeatCount
            || !c.ParentCompleted && c.PlayIndex != 0
            || item.LegendConquest is not null && (item.TriggerCost is null || !ValidTriggerCostReceipt(state, item))) return false;
        var players = DeckPlayPlayers(state, c.ControllerId, c.Kind);
        if (!players.SequenceEqual(c.Players) || !c.Viewed.Keys.Order().SequenceEqual(players.Order())
            || c.Selected.Keys.Any(p => !players.Contains(p)) || c.Queue.Any(q => q is null)
            || c.Queue.Select(q => q.Card?.ObjectId).Distinct().Count() != c.Queue.Count) return false;
        for (var i=0;i<players.Length;i++)
        {
            var player=players[i];var viewed=c.Viewed[player];
            if (viewed is null || viewed.Count > DeckPlayLookCount(b)
                || viewed.Any(p => p.Value < 0 || !state.CardObjects.TryGetValue(p.Key,out var card) || card.ObjectGeneration < p.Value)) return false;
            if (i >= c.SelectionIndex && !state.PlayerZones[player].MainDeck.Take(viewed.Count).SequenceEqual(viewed.Keys)) return false;
            if (i < c.SelectionIndex && viewed.Count > 0)
            {
                if (!c.Selected.ContainsKey(player) && b.EffectPlayOptional)
                {
                    if (c.ParentCompleted && c.Queue.Any(q => q.OwnerId == player && q.ExecutionIndex == c.ExecutionIndex)) return false;
                    continue;
                }
                if (!c.Selected.TryGetValue(player,out var picked) || picked is null || !viewed.TryGetValue(picked.ObjectId,out var before)
                    || picked.Generation != before+1 || !state.CardObjects.TryGetValue(picked.ObjectId,out var card) || card.ObjectGeneration < picked.Generation
                    || !c.ParentCompleted && !state.PlayerZones[player].Banished.Contains(picked.ObjectId)) return false;
                if (c.ParentCompleted && !c.Queue.Any(q => q.OwnerId == player && q.ExecutionIndex == c.ExecutionIndex && q.Card.ObjectId == picked.ObjectId)) return false;
            }
            else if (c.Selected.ContainsKey(player)) return false;
        }
        var previousRound=-1;var previousOrder=-1;
        for (var i=0;i<c.Queue.Count;i++)
        {
            var q=c.Queue[i];var order=Array.IndexOf(DeckPlayOrder(c),q.OwnerId);
            if (q.Card is null || q.BeforeBanishGeneration < 0 || q.ExecutionIndex < 0 || q.ExecutionIndex > c.ExecutionIndex
                || q.Card.Generation != q.BeforeBanishGeneration+(c.ParentCompleted ? 2 : 1) || order < 0
                || q.ExecutionIndex < previousRound || q.ExecutionIndex == previousRound && order <= previousOrder
                || !state.CardObjects.TryGetValue(q.Card.ObjectId,out var card) || card.ObjectGeneration < q.Card.Generation) return false;
            if (!c.ParentCompleted && (!state.PlayerZones[q.OwnerId].Banished.Contains(q.Card.ObjectId) || q.ExecutionIndex >= c.ExecutionIndex)) return false;
            if (c.ParentCompleted && i >= c.PlayIndex && (card.ObjectGeneration != q.Card.Generation
                || !state.StackItems.Any(s => s.DeckPlayPendingCard?.Card == q.Card))) return false;
            previousRound=q.ExecutionIndex;previousOrder=order;
        }
        return true;
    }

    internal static bool ValidDeckPlayChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.LastOrDefault();
        if (item?.DeckPlaySequence is not { } c || !ValidDeckPlaySequence(state, item) || c.SelectionIndex >= c.Players.Count) return false;
        TryGetEffectPlayDefinition(item, out var definition);
        var owner = c.Players[c.SelectionIndex]; var ids = c.Viewed[owner].Keys.ToArray();
        var legal = DeckPlayLegalSelection(state, c, owner);
        return choice.ChoiceWindow == DeckPlayChoiceWindow && choice.ChoiceId == DeckPlaySelectionId(item, c)
            && choice.PlayerId == (c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : c.Players[c.SelectionIndex])
            && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == (definition.EffectPlayOptional ? 0 : 1) && choice.MaxCount == (legal.Length > 0 ? 1 : 0) && legal.SequenceEqual(choice.LegalObjectIds)
            && ids.SequenceEqual(choice.ContextObjectIds) && choice.DeckContext == new DeckChoiceContext(item.StackItemId, c.SelectionIndex);
    }

    internal static bool ValidDeckPlayPending(MatchState state, PendingEffectPlayState pending)
    {
        if (pending.Parent.DeckPlaySequence is not { } c || !ValidDeckPlaySequence(state, pending.Parent)
            || c.SelectionIndex != c.Players.Count || c.PlayIndex >= c.Queue.Count || !c.ParentCompleted
            || !pending.Parent.EffectPlayCompleted) return false;
        var expected = DeckPlayPending(pending.Parent, c);
        return pending.SourceZone == expected.SourceZone && pending.PlayerId == expected.PlayerId
            && pending.ChoiceId == expected.ChoiceId
            && pending.Sources.Count == 1 && expected.Sources.All(s => pending.Sources.GetValueOrDefault(s.Key, -1) == s.Value)
            && pending.IgnoreBaseMana == expected.IgnoreBaseMana && pending.IgnoreBasePower == expected.IgnoreBasePower
            && pending.IgnoreAllCosts == expected.IgnoreAllCosts && pending.ManaReduction == expected.ManaReduction
            && pending.DestinationPolicy == expected.DestinationPolicy && !pending.Optional && pending.ViewedCardIds is null && pending.RevealedHand is null;
    }

    private static ResolutionResult FinishDeckPlaySequence(MatchState state, PlayerIntent intent, PendingEffectPlayState pending, IReadOnlyList<GameEvent> events)
    {
        var c = pending.Parent.DeckPlaySequence!;
        var current = c.Queue[c.PlayIndex].Card;
        if (state.StackItems.Any(s => s.DeckPlayPendingCard?.Card.ObjectId == current.ObjectId))
        {
            var owner = state.CardObjects[current.ObjectId].OwnerId!;
            var zones = NormalizeZonesForSeats(state); zones[owner] = zones[owner] with { Banished = zones[owner].Banished.Append(current.ObjectId).ToArray() };
            var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            cards[current.ObjectId] = cards[current.ObjectId] with { ControllerId = owner };
            state = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                StackItems = state.StackItems.Where(s => s.DeckPlayPendingCard?.Card.ObjectId != current.ObjectId).ToArray() };
            events = events.Append(new GameEvent("EFFECT_PLAY_UNABLE", "无法完成打出的牌返回放逐区", new Dictionary<string,object?> {
                ["playerId"] = pending.PlayerId, ["sourceObjectId"] = pending.Parent.SourceObjectId, ["targetObjectId"] = current.ObjectId,
                ["destinationZone"] = "BANISHED" })).ToArray();
        }
        c = c with { PlayIndex = c.PlayIndex + 1 };
        PendingEffectPlayState? nextPending = null;
        if (c.PlayIndex < c.Queue.Count)
        {
            var player = c.Queue[c.PlayIndex].OwnerId; var card = c.Queue[c.PlayIndex].Card;
            nextPending = pending with { ChoiceId = $"DECK-PLAY-EFFECT-{pending.Parent.StackItemId}-{c.PlayIndex}",
                PlayerId = c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : player,
                Parent = pending.Parent with { DeckPlaySequence = c }, Sources = new Dictionary<string,long> { [card.ObjectId] = card.Generation } };
        }
        var next = state with { PendingEffectPlay = nextPending, PriorityPlayerId = nextPending is not null ? null : state.StackItems.LastOrDefault()?.ControllerId,
            PassedPriorityPlayerIds = [], ActivePlayerId = nextPending?.PlayerId ?? state.StackItems.LastOrDefault()?.ControllerId ?? state.TurnPlayerId,
            TimingState = nextPending is not null || state.StackItems.Count > 0 ? TimingStates.NeutralClosed
                : state.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : TimingStates.NeutralOpen };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    internal static bool ValidDeckPlayPendingCard(MatchState state, StackItemState item)
    {
        if (item.DeckPlayPendingCard is not { } receipt) return true;
        if (state.PendingEffectPlay?.Parent.DeckPlaySequence is not { } c || receipt.Card is null
            || receipt.ParentStackItemId != state.PendingEffectPlay.Parent.StackItemId || receipt.Kind != c.Kind
            || receipt.OrderIndex < c.PlayIndex || receipt.OrderIndex >= c.Queue.Count || item.SourceConfirmed
            || item.CardNo != receipt.CardNo || item.SourceObjectId != receipt.Card.ObjectId || item.ControllerId != receipt.PlayerId
            || c.Queue[receipt.OrderIndex].Card != receipt.Card
            || c.Queue[receipt.OrderIndex].OwnerId != receipt.OwnerId
            || receipt.PlayerId != (c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : receipt.OwnerId)
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var card) || card.ObjectGeneration != receipt.Card.Generation
            || card.CardNo != receipt.CardNo || card.OwnerId != receipt.OwnerId || card.ControllerId != receipt.PlayerId
            || item.StackItemId != $"PENDING-{receipt.ParentStackItemId}-{receipt.OrderIndex}-{receipt.Card.ObjectId}") return false;
        return item.TargetObjectIds.Count == 0 && !item.EffectPlayCompleted
            && (CardBehaviorRegistry.TryGetByCardNo(receipt.CardNo, out var definition)
                ? definition.EffectKind == item.EffectKind : item.EffectKind == "UNSUPPORTED_PENDING_CARD")
            && state.ObjectLocations.GetValueOrDefault(item.SourceObjectId)?.Zone == "STACK";
    }
}
