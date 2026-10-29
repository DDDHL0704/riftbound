using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record DeckPlaySequenceContext(string Kind, ObjectBinding Source, string CardNo, string ControllerId,
    IReadOnlyList<string> Players, IReadOnlyDictionary<string,IReadOnlyList<ObjectBinding>> Viewed,
    IReadOnlyDictionary<string,ObjectBinding> Selected, int SelectionIndex, int PlayIndex, bool ParentCompleted = false)
{
    public int ExecutionIndex { get; init; }
    public IReadOnlyList<DeckPlayQueuedCard> Queue { get; init; } = [];
    public IReadOnlyList<string> ObservedPlayers { get; init; } = [];
}
public sealed record DeckPlayQueuedCard(string OwnerId, ObjectBinding Card, string CardNo, long BeforeBanishGeneration, int ExecutionIndex);
public sealed record DeckPlayPendingCardContext(string ParentStackItemId, string Kind, string OwnerId, string PlayerId,
    ObjectBinding Card, string CardNo, int OrderIndex);

public sealed partial class CoreRuleEngine
{
    internal const string DeckPlayChoiceWindow = "DECK_PLAY_SELECTION";

    internal static StackItemState? DeckPlayChoiceParent(MatchState state, PendingCardChoiceState choice)
        => choice.DeckContext is { } context
            ? state.StackItems.FirstOrDefault(item => item.StackItemId == context.StackItemId) : null;

    private static string[] DeckPlayPlayers(MatchState state, string controller, string kind)
        => kind.StartsWith("SINGLE_", StringComparison.Ordinal) ? [controller]
            : kind == "OPPONENT_REVEAL_PLAY" ? PlayerIdsStartingAfter(state, controller).Where(p => p != controller).ToArray()
            : new[] { state.TurnPlayerId }.Concat(PlayerIdsStartingAfter(state, state.TurnPlayerId).Where(p => p != state.TurnPlayerId)).ToArray();

    private static int DeckPlayLookCount(CardBehaviorDefinition definition)
        => definition.DeckPlayKind == "OPPONENT_REVEAL_PLAY" ? 1
            : definition.DeckPlayKind == "EACH_PLAYER_LOOK_PLAY" ? 5 : definition.MainDeckLookCount;

    internal static bool DeckPlaySequenceReveals(StackItemState item)
        => TryGetEffectPlayDefinition(item, out var definition) && (definition.DeckPlayKind == "OPPONENT_REVEAL_PLAY" || definition.EffectPlayRevealsCards);

    private static string[] DeckPlayLegalSelection(MatchState state, StackItemState item, DeckPlaySequenceContext c, string owner)
    {
        TryGetEffectPlayDefinition(item, out var definition);
        return c.Viewed[owner].Select(card => card.ObjectId).Where(id => c.Kind == "SINGLE_PRIVATE_UNIT_OR_GEAR_PLAY"
            ? state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard) || state.CardObjects[id].Tags.Contains(CardObjectTags.EquipmentCard)
            : c.Kind is not ("SINGLE_PRIVATE_UNIT_PLAY" or "SINGLE_PRIVATE_LIMITED_UNIT_PLAY" or "SINGLE_REVEAL_UNTIL_UNIT")
            || state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)
                && (c.Kind != "SINGLE_PRIVATE_LIMITED_UNIT_PLAY" || definition.DeckPlayMaximumUnitMight is { } maximum
                    && state.CardObjects[id].Power <= maximum)).ToArray();
    }

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
            IgnoreAllCosts: c.Kind == "OPPONENT_REVEAL_PLAY" || definition.EffectPlayIgnoreAllCosts);
    }

    private static StackResolutionResult BeginDeckPlaySequence(MatchState state, StackItemState item, CardBehaviorDefinition definition)
    {
        // Declared deck identities are never valid targets for these effects.
        if (item.TargetObjectIds.Count != 0 && item.DestroyDeckPlay is not { Prepared: true }) return ResolveStackItemEffect(state, item with { EffectPlayCompleted = true });
        var events = new List<GameEvent>();
        var c = item.DeckPlaySequence;
        if (c is null)
        {
            var players = DeckPlayPlayers(state, item.ControllerId, definition.DeckPlayKind);
            var count = DeckPlayLookCount(definition);
            // Observation order is rule state; JSON object member order is not persistent data.
            var viewed = players.ToDictionary(p => p, p => (IReadOnlyList<ObjectBinding>)(definition.DeckPlayKind == "SINGLE_REVEAL_UNTIL_UNIT"
                ? state.PlayerZones[p].MainDeck.TakeWhile(id => !state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard))
                    .Concat(state.PlayerZones[p].MainDeck.SkipWhile(id => !state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)).Take(1))
                : state.PlayerZones[p].MainDeck.Take(count))
                .Select(id => new ObjectBinding(id, state.CardObjects[id].ObjectGeneration)).ToArray(), StringComparer.Ordinal);
            var generation = item.EndAbility?.Source.ObjectGeneration ?? item.DestroyDeckPlay?.Source.Generation ?? (item.FieldContext is { Kind: "ATTACK_DECK_PLAY" } field ? field.SourceGeneration : state.CardObjects[item.SourceObjectId].ObjectGeneration);
            if (DeckPlaySequenceReveals(item))
                foreach (var player in players)
                    if (viewed[player].Count > 0) events.Add(new("CARDS_REVEALED", "展示原效果看到的顶部卡牌", new Dictionary<string,object?> {
                        ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["count"] = viewed[player].Count,
                        ["cards"] = viewed[player].Select(card => new { objectId = card.ObjectId, cardNo = state.CardObjects[card.ObjectId].CardNo }).ToArray() }));
            var observationZones=NormalizeZonesForSeats(state);var observationCards=state.CardObjects.ToDictionary();
            foreach(var player in definition.DeckPlayKind=="EACH_PLAYER_LOOK_PLAY"?Array.Empty<string>():players)
            {
                var remaining=ObserveDeckCards(state,observationZones,observationCards,item,player,viewed[player].Select(card=>card.ObjectId).ToArray(),
                    DeckPlaySequenceReveals(item)?"REVEAL":"LOOK",0,events);
                viewed[player]=remaining.Select(id=>new ObjectBinding(id,observationCards[id].ObjectGeneration)).ToArray();
            }
            state=state with {PlayerZones=observationZones,CardObjects=observationCards,ObjectLocations=DeckPlayLocations(state,observationZones,state.StackItems)};
            c = new(definition.DeckPlayKind, new(item.SourceObjectId, generation),
                item.CardNo!, item.ControllerId, players, viewed, new Dictionary<string,ObjectBinding>(), 0, 0);
        }
        while(c.SelectionIndex<c.Players.Count)
        {
            var player=c.Players[c.SelectionIndex];
            if(c.Kind=="EACH_PLAYER_LOOK_PLAY" && !c.ObservedPlayers.Contains(player))
            {
                var zones=NormalizeZonesForSeats(state);var cards=state.CardObjects.ToDictionary();
                var remaining=ObserveDeckCards(state,zones,cards,item,player,c.Viewed[player].Select(card=>card.ObjectId).ToArray(),"LOOK",c.ExecutionIndex,events);
                state=state with {PlayerZones=zones,CardObjects=cards,ObjectLocations=DeckPlayLocations(state,zones,state.StackItems)};
                c=c with {Viewed=new Dictionary<string,IReadOnlyList<ObjectBinding>>(c.Viewed){[player]=remaining.Select(id=>new ObjectBinding(id,cards[id].ObjectGeneration)).ToArray()},
                    ObservedPlayers=c.ObservedPlayers.Append(player).ToArray()};
            }
            if(c.Viewed[player].Count>0)break;
            c=c with {SelectionIndex=c.SelectionIndex+1};
        }
        item = item with { DeckPlaySequence = c };
        if (c.SelectionIndex < c.Players.Count)
        {
            var owner = c.Players[c.SelectionIndex];
            var ids = c.Viewed[owner].Select(card => card.ObjectId).ToArray();
            var legal = DeckPlayLegalSelection(state, item, c, owner);
            if (c.Kind == "SINGLE_REVEAL_UNTIL_UNIT")
            {
                var (next, selected) = ApplyDeckPlaySelection(state, item, owner, legal.FirstOrDefault(), definition);
                var resumed = BeginDeckPlaySequence(next, next.StackItems.Single(s => s.StackItemId == item.StackItemId), definition);
                return resumed with { Events = events.Concat(selected).Concat(resumed.Events).ToArray() };
            }
            if (c.Kind == "SINGLE_PRIVATE_LIMITED_UNIT_PLAY" && legal.Length == 0)
            {
                var (next, recycled) = ApplyDeckPlaySelection(state, item, owner, null, definition);
                var resumed = BeginDeckPlaySequence(next, next.StackItems.Single(s => s.StackItemId == item.StackItemId), definition);
                return resumed with { Events = events.Concat(recycled).Concat(resumed.Events).ToArray() };
            }
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
                ObjectLocations = state.ObjectLocations, PendingCardChoice = choice, Events = events };
        }
        // This instruction follows every player's banish/recycle selection.
        // Other consumers instruct the selected card's play before recycling.
        if (c.Kind == "EACH_PLAYER_LOOK_PLAY")
        {
            var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
            var stack = state.StackItems.ToList(); var queued = c.Queue.ToList();
            foreach (var owner in DeckPlayOrder(c))
                if (c.Selected.TryGetValue(owner, out var selected))
                    queued.Add(AddDeckPlayPendingCard(item, c, owner, selected, queued.Count, zones, cards, stack, events));
            c = c with { Queue = queued };
            state = state with { PlayerZones = zones, CardObjects = cards, StackItems = stack,
                ObjectLocations = DeckPlayLocations(state, zones, stack) };
        }
        if (definition.DeckPlayOptionalReflexiveEmpower)
        {
            var created = CreateDeckPlayReflexive(state, item with { DeckPlaySequence = c }, c);
            state = created.State;
            events.AddRange(created.Events);
        }
        if (c.ExecutionIndex + 1 < item.EffectRepeatCount)
        {
            var count = DeckPlayLookCount(definition);
            c = c with { ExecutionIndex = c.ExecutionIndex + 1, SelectionIndex = 0,
                Selected = new Dictionary<string,ObjectBinding>(), ObservedPlayers=[], Viewed = c.Players.ToDictionary(p => p,
                    p => (IReadOnlyList<ObjectBinding>)state.PlayerZones[p].MainDeck.Take(count).Select(id => new ObjectBinding(id,state.CardObjects[id].ObjectGeneration)).ToArray()) };
            if (DeckPlaySequenceReveals(item))
                foreach (var player in c.Players)
                    if (c.Viewed[player].Count > 0) events.Add(new("CARDS_REVEALED", "下一次执行展示主牌堆顶部的卡牌", new Dictionary<string,object?> {
                        ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["count"] = c.Viewed[player].Count,
                        ["cards"] = c.Viewed[player].Select(card => new { objectId = card.ObjectId, cardNo = state.CardObjects[card.ObjectId].CardNo }).ToArray() }));
            var repeatedZones=NormalizeZonesForSeats(state);var repeatedCards=state.CardObjects.ToDictionary();
            var repeatedViewed=c.Viewed.ToDictionary();
            foreach(var player in c.Kind=="EACH_PLAYER_LOOK_PLAY"?Array.Empty<string>():c.Players)
            {
                var remaining=ObserveDeckCards(state,repeatedZones,repeatedCards,item,player,c.Viewed[player].Select(card=>card.ObjectId).ToArray(),
                    DeckPlaySequenceReveals(item)?"REVEAL":"LOOK",c.ExecutionIndex,events);
                repeatedViewed[player]=remaining.Select(id=>new ObjectBinding(id,repeatedCards[id].ObjectGeneration)).ToArray();
            }
            state=state with {PlayerZones=repeatedZones,CardObjects=repeatedCards,ObjectLocations=DeckPlayLocations(state,repeatedZones,state.StackItems)};
            c=c with {Viewed=repeatedViewed};
            var again = BeginDeckPlaySequence(state, item with { DeckPlaySequence = c }, definition);
            return again with { Events = events.Concat(again.Events).ToArray() };
        }
        if (c.Queue.Count == 0 && !state.StackItems.Any(s => s.DeckPlayReflexive?.ParentStackItemId == item.StackItemId))
            return ResolveStackItemEffect(state, item with { DeckPlaySequence = c, EffectPlayCompleted = true });
        StackResolutionResult? completed = null;
        if (!c.ParentCompleted)
        {
            completed = ResolveStackItemEffect(state, item with { DeckPlaySequence = c, EffectPlayCompleted = true });
            var zones = completed.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
            var cards = completed.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            var stack = (completed.StackItems ?? state.StackItems).Where(s => s.StackItemId != item.StackItemId).ToArray();
            c = c with { ParentCompleted = true };
            var locations = DeckPlayLocations(state, zones, stack);
            completed = completed with { PlayerZones = zones, CardObjects = cards, StackItems = stack,
                ObjectLocations = locations };
            events.AddRange(completed.Events);
            events.Add(new("STACK_ITEM_RESOLVED", "来源法术已完成结算，继续按顺序确认再次打出的卡牌", new Dictionary<string,object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId, ["controllerId"] = item.ControllerId,
                ["effectKind"] = item.EffectKind, ["cardNo"] = item.CardNo, ["targetObjectIds"] = item.TargetObjectIds }));
        }
        item = item with { DeckPlaySequence = c, EffectPlayCompleted = true };
        var pending = c.PlayIndex < c.Queue.Count ? DeckPlayPending(item, c) : null;
        return (completed ?? NoopStackResolutionResult(state)) with { PendingEffectPlay = pending, Events = events };
    }

    private static string[] DeckPlayOrder(DeckPlaySequenceContext c)
        => c.Kind == "OPPONENT_REVEAL_PLAY" ? c.Players.ToArray() : c.Players.Skip(1).Concat(c.Players.Take(1)).ToArray();

    private static DeckPlayQueuedCard AddDeckPlayPendingCard(StackItemState parent, DeckPlaySequenceContext c,
        string owner, ObjectBinding selected, int index, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, List<StackItemState> stack, List<GameEvent> events)
    {
        var card = cards[selected.ObjectId];
        var binding = selected with { Generation = checked(card.ObjectGeneration + 1) };
        var actor = c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : owner;
        cards[card.ObjectId] = card with { ObjectGeneration = binding.Generation };
        zones[owner] = zones[owner] with { Banished = RemoveFromZone(zones[owner].Banished, card.ObjectId) };
        var receipt = new DeckPlayPendingCardContext(parent.StackItemId, c.Kind, owner, actor, binding, card.CardNo!, index);
        var kind = CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var printed) ? printed.EffectKind : "UNSUPPORTED_PENDING_CARD";
        var slot = new StackItemState(stackItemId: $"PENDING-{parent.StackItemId}-{index}-{card.ObjectId}", controllerId: actor,
            sourceObjectId: card.ObjectId, effectKind: kind, targetObjectIds: [], cardNo: card.CardNo) { DeckPlayPendingCard = receipt };
        stack.Add(slot);
        events.Add(new("STACK_ITEM_PENDING", "效果要求打出的卡牌成为待确认项目", new Dictionary<string, object?> {
            ["stackItemId"] = slot.StackItemId, ["sourceObjectId"] = card.ObjectId, ["cardNo"] = card.CardNo,
            ["controllerId"] = actor, ["parentStackItemId"] = parent.StackItemId, ["orderIndex"] = index }));
        return new(owner, binding, card.CardNo ?? "", selected.Generation - 1, c.ExecutionIndex);
    }

    private static Dictionary<string, ObjectLocationState> DeckPlayLocations(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IEnumerable<StackItemState> stack)
    {
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        foreach (var slot in stack.Where(s => s.DeckPlayPendingCard is not null))
            locations[slot.SourceObjectId] = new(slot.ControllerId, "STACK");
        return locations;
    }

    private static ResolutionResult ResolveDeckPlaySelection(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidDeckPlayChoice(state, choice)) return RejectWithCorePrompts(state, "本次牌堆选择上下文已失效。", ErrorCodes.InvalidTarget);
        var item = DeckPlayChoiceParent(state, choice)!; var c = item.DeckPlaySequence!;
        TryGetEffectPlayDefinition(item, out var definition);
        var owner = c.Players[c.SelectionIndex];
        var (next, selectedEvents) = ApplyDeckPlaySelection(state, item, owner, selected.SingleOrDefault(), definition);
        var events=selectedEvents.Append(new GameEvent("CARD_CHOICE_RESOLVED","牌堆选择已确认",new Dictionary<string,object?> {
            ["playerId"]=choice.PlayerId,["sourceObjectId"]=item.SourceObjectId,["choiceId"]=choice.ChoiceId,["chosenCount"]=selected.Count})).ToArray();
        var resumed = ResolvePassPriority(next, new("deck-play-choice", choice.PlayerId, CommandTypes.PassPriority),
            forceResolve: true, resolvingStackItemId: item.StackItemId);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }

    private static (MatchState State, IReadOnlyList<GameEvent> Events) ApplyDeckPlaySelection(MatchState state,
        StackItemState item, string owner, string? id, CardBehaviorDefinition definition)
    {
        var c = item.DeckPlaySequence!;
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        var picked = c.Selected.ToDictionary(p => p.Key, p => p.Value);
        var queue = c.Queue.ToList(); var stack = state.StackItems.ToList();
        if (id is not null)
        {
            var old = cards[id];
            var card = PrintedCardFactory.TryRestoreOutsidePlay(old, old.OwnerId ?? owner, out var printed) ? printed : old;
            cards[id] = card with { ControllerId = c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : owner,
                ObjectGeneration = checked(old.ObjectGeneration + 1) };
            zones[owner] = zones[owner] with { MainDeck = RemoveFromZone(zones[owner].MainDeck, id), Banished = zones[owner].Banished.Append(id).ToArray() };
            picked[owner] = new(id, cards[id].ObjectGeneration);
            events.Add(CaptureOwnedBanishObservers(new("CARD_BANISHED", "所选牌被放逐，等待再次打出", new Dictionary<string,object?> {
                ["playerId"] = owner, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                ["cardNo"] = cards[id].CardNo, ["destinationZone"] = "BANISHED" }),
                zones, cards, c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : owner, [id]));
            if (c.Kind != "EACH_PLAYER_LOOK_PLAY")
                queue.Add(AddDeckPlayPendingCard(item, c, owner, picked[owner], queue.Count, zones, cards, stack, events));
        }
        var remaining = c.Viewed[owner].Select(card => card.ObjectId).Where(x => x != id).ToArray();
        if (definition.DeckPlayDrawsUnselected)
            ApplyDrawToPlayer(state, zones, state.PlayerScores, owner, remaining.Length, state.RngCursor, events, deferNormalVictory: true);
        else
        {
            var recycled = RandomizeForMainDeckBottom(remaining, state.Seed, state.RngCursor, item.SourceObjectId);
            zones[owner] = zones[owner] with { MainDeck = zones[owner].MainDeck.Where(x => !remaining.Contains(x)).Concat(recycled).ToArray() };
            if (remaining.Length > 0) events.Add(CaptureRecycleObservers(new("CARDS_RECYCLED", "回收未选择的牌", new Dictionary<string,object?> {
                ["playerId"] = c.Kind == "OPPONENT_REVEAL_PLAY" ? c.ControllerId : owner, ["ownerPlayerId"] = owner,
                ["sourceObjectId"] = item.SourceObjectId, ["count"] = remaining.Length, ["zone"] = "MAIN_DECK",
                ["visibility"] = DeckPlaySequenceReveals(item) ? "REVEALED" : "LOOKED_NOT_REVEALED" }), zones, cards, remaining));
        }
        item = item with { DeckPlaySequence = c with { Selected = picked, Queue = queue, SelectionIndex = c.SelectionIndex + 1 } };
        var next = state with { PendingCardChoice = null, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = DeckPlayLocations(state, zones, stack), RngCursor = state.RngCursor + (!definition.DeckPlayDrawsUnselected && remaining.Length > 1 ? 1 : 0),
            StackItems = stack.Select(s => s.StackItemId == item.StackItemId ? item : s).ToArray() };
        return (next, events);
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
            || (item.EndAbility is { } ended
                ? c.Source.Generation != ended.Source.ObjectGeneration || !ValidEndAbility(state, item)
                : item.DestroyDeckPlay is { } destroyed
                ? c.Source.Generation != destroyed.Source.Generation || !ValidDestroyDeckPlayContext(state, item)
                : item.FieldContext is { Kind: "ATTACK_DECK_PLAY" } captured
                ? c.Source.Generation != captured.SourceGeneration || !ValidFieldContextState(state, captured, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
                : !state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration < c.Source.Generation)
            || c.Players is null || c.Viewed is null || c.Selected is null || c.Queue is null || c.ObservedPlayers is null
            || c.SelectionIndex < 0 || c.SelectionIndex > c.Players.Count || c.PlayIndex < 0 || c.PlayIndex > c.Queue.Count
            || c.ExecutionIndex < 0 || c.ExecutionIndex >= item.EffectRepeatCount
            || !c.ParentCompleted && c.PlayIndex != 0
            || c.ParentCompleted != item.EffectPlayCompleted
            || c.ParentCompleted && (c.SelectionIndex != c.Players.Count || c.ExecutionIndex != item.EffectRepeatCount - 1)
            || item.LegendConquest is not null && (item.TriggerCost is null || !ValidTriggerCostReceipt(state, item))) return false;
        var players = DeckPlayPlayers(state, c.ControllerId, c.Kind);
        if (!players.SequenceEqual(c.Players) || !c.Viewed.Keys.Order().SequenceEqual(players.Order())
            || c.ObservedPlayers.Distinct().Count()!=c.ObservedPlayers.Count
            || c.Kind=="EACH_PLAYER_LOOK_PLAY" && !c.ObservedPlayers.SequenceEqual(players.Take(Math.Min(c.SelectionIndex+1,players.Length)))
            || c.Kind!="EACH_PLAYER_LOOK_PLAY" && c.ObservedPlayers.Count!=0
            || c.Selected.Keys.Any(p => !players.Contains(p)) || c.Queue.Any(q => q is null)
            || c.Queue.Select(q => q.Card?.ObjectId).Distinct().Count() != c.Queue.Count) return false;
        var pendingSlots = state.StackItems.Where(s => s.DeckPlayPendingCard?.ParentStackItemId == item.StackItemId).ToArray();
        if (!pendingSlots.Select(s => s.DeckPlayPendingCard!.Card).SequenceEqual(c.Queue.Skip(c.PlayIndex).Select(q => q.Card))
            || !c.ParentCompleted && (!state.StackItems.Any(s => s.StackItemId == item.StackItemId)
                || state.StackItems.TakeWhile(s => s.StackItemId != item.StackItemId)
                    .Any(s => s.DeckPlayPendingCard?.ParentStackItemId == item.StackItemId))) return false;
        for (var i=0;i<players.Length;i++)
        {
            var player=players[i];var viewed=c.Viewed[player];
            if (viewed is null || viewed.Any(p => p is null || string.IsNullOrWhiteSpace(p.ObjectId) || p.Generation < 0
                    || !state.CardObjects.TryGetValue(p.ObjectId,out var card) || card.ObjectGeneration < p.Generation)
                || viewed.Select(p => p.ObjectId).Distinct(StringComparer.Ordinal).Count() != viewed.Count
                || c.Kind != "SINGLE_REVEAL_UNTIL_UNIT" && viewed.Count > DeckPlayLookCount(b)) return false;
            var ids=viewed.Select(p=>p.ObjectId).ToArray();
            var expectedObserved=ObservedRemainingDeckCards(state,item,player,c.ExecutionIndex);
            if(expectedObserved is not null && !expectedObserved.SequenceEqual(ids))return false;
            if (c.Kind == "SINGLE_REVEAL_UNTIL_UNIT")
            {
                var units = ids.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)).ToArray();
                if (units.Length > 1 || units.Length == 1 && ids[^1] != units[0]
                    || i < c.SelectionIndex && units.Length == 1 && (!c.Selected.TryGetValue(player, out var firstUnit) || firstUnit.ObjectId != units[0])
                    || units.Length == 0 && c.Selected.ContainsKey(player)) return false;
                if (units.Length == 0 && i < c.SelectionIndex) continue;
            }
            if (i >= c.SelectionIndex && !state.PlayerZones[player].MainDeck.Take(viewed.Count).SequenceEqual(ids)) return false;
            if (i < c.SelectionIndex && viewed.Count > 0)
            {
                if (!c.Selected.ContainsKey(player) && b.EffectPlayOptional)
                {
                    if (c.ParentCompleted && c.Queue.Any(q => q.OwnerId == player && q.ExecutionIndex == c.ExecutionIndex)) return false;
                    continue;
                }
                if (!c.Selected.TryGetValue(player,out var picked) || picked is null || viewed.FirstOrDefault(p => p.ObjectId == picked.ObjectId) is not { } before
                    || picked.Generation != before.Generation+1 || !state.CardObjects.TryGetValue(picked.ObjectId,out var card) || card.ObjectGeneration < picked.Generation
                    || c.Kind == "SINGLE_PRIVATE_LIMITED_UNIT_PLAY" && (!card.Tags.Contains(CardObjectTags.UnitCard)
                        || b.DeckPlayMaximumUnitMight is not { } maximum || card.Power > maximum)
                    || !c.ParentCompleted && !state.PlayerZones[player].Banished.Contains(picked.ObjectId)
                        && !c.Queue.Any(q => q.OwnerId == player && q.ExecutionIndex == c.ExecutionIndex
                            && q.Card.ObjectId == picked.ObjectId && q.Card.Generation == picked.Generation + 1)) return false;
                if (c.ParentCompleted && !c.Queue.Any(q => q.OwnerId == player && q.ExecutionIndex == c.ExecutionIndex && q.Card.ObjectId == picked.ObjectId)) return false;
            }
            else if (c.Selected.ContainsKey(player)) return false;
        }
        var previousRound=-1;var previousOrder=-1;
        for (var i=0;i<c.Queue.Count;i++)
        {
            var q=c.Queue[i];var order=Array.IndexOf(DeckPlayOrder(c),q.OwnerId);
            if (q.Card is null || q.BeforeBanishGeneration < 0 || q.ExecutionIndex < 0 || q.ExecutionIndex > c.ExecutionIndex
                || q.Card.Generation != q.BeforeBanishGeneration + 2 || order < 0
                || q.ExecutionIndex < previousRound || q.ExecutionIndex == previousRound && order <= previousOrder
                || !state.CardObjects.TryGetValue(q.Card.ObjectId,out var card) || card.ObjectGeneration < q.Card.Generation) return false;
            if (!c.ParentCompleted && q.ExecutionIndex == c.ExecutionIndex
                && c.Kind == "EACH_PLAYER_LOOK_PLAY" && c.SelectionIndex != c.Players.Count) return false;
            if (q.ExecutionIndex == c.ExecutionIndex && (!c.Selected.TryGetValue(q.OwnerId, out var selected)
                || selected.ObjectId != q.Card.ObjectId || selected.Generation + 1 != q.Card.Generation)) return false;
            if (i >= c.PlayIndex && (card.ObjectGeneration != q.Card.Generation
                || q.CardNo != card.CardNo
                || !state.StackItems.Any(s => s.DeckPlayPendingCard?.Card == q.Card))) return false;
            previousRound=q.ExecutionIndex;previousOrder=order;
        }
        return true;
    }

    internal static bool ValidDeckPlayChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = DeckPlayChoiceParent(state, choice);
        if (item?.DeckPlaySequence is not { } c || !ValidDeckPlaySequence(state, item) || c.SelectionIndex >= c.Players.Count) return false;
        TryGetEffectPlayDefinition(item, out var definition);
        var owner = c.Players[c.SelectionIndex]; var ids = c.Viewed[owner].Select(card => card.ObjectId).ToArray();
        var legal = DeckPlayLegalSelection(state, item, c, owner);
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
        var pending=state.PendingEffectPlay?.Parent.ObservedPlayPermission?.Suspended ?? state.PendingEffectPlay;
        var parent = pending?.Parent.StackItemId == receipt.ParentStackItemId ? pending.Parent
            : state.StackItems.FirstOrDefault(s => s.StackItemId == receipt.ParentStackItemId && s.DeckPlaySequence is { ParentCompleted: false });
        if (parent?.DeckPlaySequence is not { } c || !ValidDeckPlaySequence(state, parent) || receipt.Card is null
            || receipt.ParentStackItemId != parent.StackItemId || receipt.Kind != c.Kind
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
