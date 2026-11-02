using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record OwnedUnitReplayFact(ObjectBinding Unit, string OwnerId, string CardNo,
    ObjectLocationState Location, ObjectBinding? Battlefield, int ExecutionIndex, bool Token);
public sealed record OwnedUnitReplayContext(IReadOnlyList<LastKnownMightFrame> Frames,
    IReadOnlyList<OwnedUnitReplayFact> Units);

public sealed partial class CoreRuleEngine
{
    internal const string OwnedUnitReplayKind = "OWNED_FIELD_UNIT_REPLAY";

    private static string[] OwnedReplayCandidates(MatchState state, string actor)
        => state.CardObjects.Values.Where(card => IsFaceUpNonStandbyUnit(card)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) == actor)
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static StackResolutionResult BeginOwnedUnitReplay(MatchState state, StackItemState item, CardBehaviorDefinition definition)
    {
        if (!VariableAdditionalPowerCostRules.TryExtract(item.OptionalCosts, definition.MayPayAnyAdditionalPower,
                out _, out var amount, out _)) throw new InvalidOperationException("Invalid variable replay fee.");
        var players = DeckPlayPlayers(state, item.ControllerId, OwnedUnitReplayKind);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>(); var stack = state.StackItems.ToList();
        var facts = new List<OwnedUnitReplayFact>(); var frames = new List<LastKnownMightFrame>();
        var queue = new List<DeckPlayQueuedCard>();
        var c = new DeckPlaySequenceContext(OwnedUnitReplayKind,
            new(item.SourceObjectId, cards[item.SourceObjectId].ObjectGeneration), item.CardNo!, item.ControllerId,
            players, players.ToDictionary(p => p, _ => (IReadOnlyList<ObjectBinding>)Array.Empty<ObjectBinding>()),
            new Dictionary<string, ObjectBinding>(), players.Length, 0);
        for (var round = 0; round < item.EffectRepeatCount; round++)
        {
            var context = state with { PlayerZones = zones, CardObjects = cards, StackItems = stack,
                ObjectLocations = DeckPlayLocations(state, zones, stack) };
            frames.Add(CapturePublicMightFrame(context, zones, cards, context.ObjectLocations));
            var available = OwnedReplayCandidates(context, item.ControllerId).ToList();
            var selected = new List<string>(); var count = Math.Min(amount, available.Count);
            while (selected.Count < count)
            {
                var id = available.Count == count - selected.Count ? available[0]
                    : RuleChoices.Value!.Choose(item.ControllerId, "选择此次放逐的受控单位",
                        available.Select(id => new RuleChoiceOption(id, DeckChoiceLabel(context, id), [id])).ToArray(),
                        kind: "OWNED_UNIT_REPLAY_SELECTION", preview: context);
                selected.Add(id); available.Remove(id);
            }
            var roundFacts = new List<OwnedUnitReplayFact>();
            foreach (var id in selected)
            {
                var before = cards[id]; var at = context.ObjectLocations[id];
                var field = at.BattlefieldObjectId is { } fieldId ? new ObjectBinding(fieldId, cards[fieldId].ObjectGeneration) : null;
                var fact = new OwnedUnitReplayFact(new(id, before.ObjectGeneration), before.OwnerId!, before.CardNo!, at, field,
                    round, TokenObjectRules.IsToken(before));
                facts.Add(fact); roundFacts.Add(fact);
                if (!TryRemoveFieldTargetCore(zones, cards, id, true, out var removed))
                    throw new InvalidOperationException("A selected replay unit left its captured location.");
                events.Add(CaptureOwnedBanishObservers(new("UNIT_BANISHED", "放逐单位，等待其拥有者再次打出", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["ownerPlayerId"] = fact.OwnerId,
                    ["destinationZone"] = "BANISHED", ["parentStackItemId"] = item.StackItemId,
                    ["targetGeneration"] = fact.Unit.Generation, ["originalLocation"] = at, ["originalBattlefield"] = field,
                    ["cardNo"] = fact.CardNo, ["executionIndex"] = round,
                    ["detachedEquipmentObjectIds"] = removed.DetachedEquipmentObjectIds.ToArray() }), zones, cards, item.ControllerId, [id]));
                if (fact.Token)
                {
                    cards.Remove(id); zones[fact.OwnerId] = zones[fact.OwnerId] with { Banished = RemoveFromZone(zones[fact.OwnerId].Banished, id) };
                }
            }
            c = c with { ExecutionIndex = round };
            foreach (var owner in players)
            {
                var remaining = roundFacts.Where(f => f.OwnerId == owner && !f.Token).ToList();
                while (remaining.Count > 0)
                {
                    var preview = context with { PlayerZones = zones, CardObjects = cards, StackItems = stack,
                        ObjectLocations = DeckPlayLocations(context, zones, stack) };
                    var id = remaining.Count == 1 ? remaining[0].Unit.ObjectId
                        : RuleChoices.Value!.Choose(owner, "选择自己下一张加入结算链的单位牌",
                            remaining.Select(f => new RuleChoiceOption(f.Unit.ObjectId, DeckChoiceLabel(preview, f.Unit.ObjectId), [f.Unit.ObjectId])).ToArray(),
                            kind: "OWNED_UNIT_REPLAY_ORDER", preview: preview);
                    var fact = remaining.Single(f => f.Unit.ObjectId == id); remaining.Remove(fact);
                    var queued = AddDeckPlayPendingCard(item, c, owner, new(id, cards[id].ObjectGeneration), queue.Count,
                        zones, cards, stack, events);
                    queue.Add(queued with { OriginalLocation = fact.Location, OriginalBattlefield = fact.Battlefield });
                }
            }
        }
        c = c with { Queue = queue, OwnedReplay = new(frames, facts) };
        item = item with { DeckPlaySequence = c };
        var next = state with { PlayerZones = zones, CardObjects = cards, StackItems = stack.Select(s => s.StackItemId == item.StackItemId ? item : s).ToArray(),
            ObjectLocations = DeckPlayLocations(state, zones, stack) };
        var result = BeginDeckPlaySequence(next, item, definition);
        return result with { Events = events.Concat(result.Events).ToArray() };
    }

    private static bool ValidOwnedUnitReplay(MatchState state, StackItemState item, DeckPlaySequenceContext c, CardBehaviorDefinition definition)
    {
        try
        {
            if (c.OwnedReplay is not { } replay || !definition.MayPayAnyAdditionalPower || item.TargetObjectIds.Count != 0
                || !VariableAdditionalPowerCostRules.TryExtract(item.OptionalCosts, true, out _, out var amount, out _)
                || replay.Frames.Count != item.EffectRepeatCount || c.ExecutionIndex != item.EffectRepeatCount - 1
                || c.SelectionIndex != c.Players.Count || c.Selected.Count != 0 || c.Viewed.Values.Any(v => v.Count != 0)
                || c.ObservedPlayers.Count != 0 || replay.Units.Select(f => f.Unit.ObjectId).Distinct().Count() != replay.Units.Count
                || replay.Units.Any(f => f.ExecutionIndex < 0 || f.ExecutionIndex >= replay.Frames.Count)
                || c.Queue.Count != replay.Units.Count(f => !f.Token)) return false;
            for (var round = 0; round < replay.Frames.Count; round++)
            {
                var frame = replay.Frames[round];
                if (!ValidDestroyDeckMightFrame(state, frame)) return false;
                var before = MightFrameState(frame);
                var selected = replay.Units.Where(f => f.ExecutionIndex == round).ToArray();
                if (selected.Length != Math.Min(amount, OwnedReplayCandidates(before, item.ControllerId).Length)) return false;
                foreach (var fact in selected)
                    if (!frame.CardObjects.TryGetValue(fact.Unit.ObjectId, out var unit) || unit.ObjectGeneration != fact.Unit.Generation
                        || unit.CardNo != fact.CardNo || unit.OwnerId != fact.OwnerId || !state.Seats.ContainsKey(fact.OwnerId)
                        || !OwnedReplayCandidates(before, item.ControllerId).Contains(fact.Unit.ObjectId)
                        || frame.ObjectLocations.GetValueOrDefault(fact.Unit.ObjectId) != fact.Location
                        || fact.Token != TokenObjectRules.IsToken(unit)
                        || (fact.Location.BattlefieldObjectId is { } field
                            ? !frame.CardObjects.TryGetValue(field, out var fieldCard) || fact.Battlefield != new ObjectBinding(field, fieldCard.ObjectGeneration)
                            : fact.Battlefield is not null)) return false;
            }
            var previousRound = -1; var previousOwner = -1;
            for (var index = 0; index < c.Queue.Count; index++)
            {
                var q = c.Queue[index]; var owner = Array.IndexOf(c.Players.ToArray(), q.OwnerId);
                var fact = replay.Units.SingleOrDefault(f => f.Unit.ObjectId == q.Card.ObjectId && !f.Token);
                if (fact is null || q.BeforeBanishGeneration != fact.Unit.Generation || q.Card.Generation != fact.Unit.Generation + 2
                    || q.CardNo != (replay.Frames[fact.ExecutionIndex].CardObjects[fact.Unit.ObjectId].AttachmentUnitCopies?.Basis.CardNo ?? fact.CardNo)
                    || q.OwnerId != fact.OwnerId || q.ExecutionIndex != fact.ExecutionIndex
                    || q.OriginalLocation != fact.Location || q.OriginalBattlefield != fact.Battlefield || owner < 0
                    || q.ExecutionIndex < previousRound || q.ExecutionIndex == previousRound && owner < previousOwner
                    || !state.CardObjects.TryGetValue(q.Card.ObjectId, out var card) || card.ObjectGeneration < q.Card.Generation
                    || index >= c.PlayIndex && (card.ObjectGeneration != q.Card.Generation || card.CardNo != q.CardNo
                        || !state.StackItems.Any(s => s.DeckPlayPendingCard?.Card == q.Card))) return false;
                previousRound = q.ExecutionIndex; previousOwner = owner;
            }
            return true;
        }
        catch (Exception error) when (error is NullReferenceException or ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
        { return false; }
    }
}
