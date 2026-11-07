using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BeforeDeckRevealLookContext(StackItemState Parent, string PlayerId, string DeckOwnerId,
    string DeckZone, ObjectBinding Source, string SourceCardNo, long SourceFaceRevision, long CapturedTick,
    ObjectBinding Card, string CardNo, int ExecutionIndex, int InstanceIndex,
    RuleTextInstanceBinding? Origin = null, string? SourceTokenFactoryCardNo = null, bool SelectionReveal = false);

public sealed partial class CoreRuleEngine
{
    internal const string BeforeDeckRevealChoiceKind = "BEFORE_DECK_REVEAL_RECYCLE";
    internal const string BeforeDeckRevealOrderKind = "BEFORE_DECK_REVEAL_ORDER";
    internal const string BeforeDeckRevealLookEvent = "BEFORE_DECK_REVEAL_LOOK_APPLIED";
    private static readonly Lazy<HashSet<string>> BeforeRevealSources = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(card => StaticAbilityParser.Parse(card.CardEffect ?? "", [])
                .Any(spec => spec.Kind == StaticAbilityKinds.BeforeDeckRevealLookRecycle && spec.Amount == 1))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    private static (CardObjectState Unit, RuleTextInstance Instance)[] BeforeRevealInstances(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, string player)
        => cards.Values.Where(unit => IsFaceUpNonStandbyUnit(unit)
            && IsObjectOnField(zones, unit.ObjectId) && EffectiveFieldControllerId(zones, unit.ObjectId, unit) == player)
            .OrderBy(unit => unit.ObjectId, StringComparer.Ordinal)
            .SelectMany(unit => AttachedRuleTextRules.Instances(zones, cards, unit)
                .Where(instance => BeforeRevealSources.Value.Contains(instance.CardNo))
                .Select(instance => (Unit: unit, Instance: instance))).ToArray();

    // This modifies a reveal instruction, before its cards or conditional result
    // are read. A LOOK is not a REVEAL and therefore cannot recurse here.
    private static void BeforeDeckReveal(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, StackItemState parent, string deckOwner,
        List<GameEvent> events, int execution = 0, string zone = "MAIN_DECK", bool selectionReveal = false, string? revealingPlayer = null)
    {
        if (RuleChoices.Value is not { } frame) return;
        var player = revealingPlayer ?? deckOwner;
        var sources = BeforeRevealInstances(zones, cards, player);
        var remaining = Enumerable.Range(0, sources.Length).ToList();
        while (remaining.Count > 0)
        {
            var deck = zone == "RUNE_DECK" ? zones[deckOwner].RuneDeck : zones[deckOwner].MainDeck;
            if (deck.FirstOrDefault() is not { } top || !cards.TryGetValue(top, out var card)) break;
            var index = remaining[0];
            if (remaining.Count > 1)
            {
                var options = remaining.Select(i => new RuleChoiceOption("SOURCE:" + i,
                    DeckCardNames.Value.GetValueOrDefault(sources[i].Instance.CardNo, sources[i].Instance.CardNo),
                    [sources[i].Unit.ObjectId])).ToArray();
                var ordered = frame.Choose(player, "选择先应用的展示前效果。", options,
                    kind: BeforeDeckRevealOrderKind, preview: state with { PlayerZones = zones.ToDictionary(),
                        CardObjects = cards.ToDictionary(), ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                        DeckObservationView = null });
                index = remaining.Single(i => "SOURCE:" + i == ordered);
            }
            remaining.Remove(index);
            var entry = sources[index];
            var context = new BeforeDeckRevealLookContext(parent, player, deckOwner, zone,
                new(entry.Unit.ObjectId, entry.Unit.ObjectGeneration), entry.Instance.CardNo,
                entry.Unit.CopyFaceTransitions?.Count ?? 0, state.Tick, new(top, card.ObjectGeneration), card.CardNo!,
                execution, index, entry.Instance.Origin, entry.Unit.TokenFactoryCardNo, selectionReveal);
            events.Add(new(BeforeDeckRevealLookEvent, "展示前私下查看牌堆顶部一张牌", new Dictionary<string, object?> {
                ["playerId"] = player, ["deckOwnerPlayerId"] = deckOwner, ["parentStackItemId"] = parent.StackItemId, ["zone"] = zone }));
            if (zone == "MAIN_DECK")
                ObserveDeckCards(state, zones, cards, parent, player, [top], "LOOK", execution, events, beforeReveal: context);
            deck = zone == "RUNE_DECK" ? zones[deckOwner].RuneDeck : zones[deckOwner].MainDeck;
            if (!deck.Contains(top)) continue; // A looked Nocturne may already be banished.
            var preview = state with { PlayerZones = zones.ToDictionary(), CardObjects = cards.ToDictionary(),
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                DeckObservationView = new(player, false, new Dictionary<string, long> { [top] = card.ObjectGeneration }) {
                    DeckZone = zone == "RUNE_DECK" ? zone : null, DeckOwnerId = player == deckOwner ? null : deckOwner, BeforeReveal = context } };
            var selected = frame.Choose(player, "展示前查看牌堆顶部，并可将其回收。",
                [new("KEEP", "保留牌顶并继续展示"), new("RECYCLE", "回收所查看的牌并继续展示")],
                kind: BeforeDeckRevealChoiceKind, preview: preview,
                privateReason: $"查看了《{DeckCardNames.Value.GetValueOrDefault(card.CardNo!, card.CardNo!)}》；可以将其回收，然后继续原展示。" );
            if (selected != "RECYCLE") continue;
            if (zone == "RUNE_DECK") zones[deckOwner] = zones[deckOwner] with { RuneDeck = [..deck.Where(id => id != top), top] };
            else zones[deckOwner] = zones[deckOwner] with { MainDeck = [..deck.Where(id => id != top), top] };
            // One card is put at the bottom: no shuffle, new incarnation or Power.
            var recycled = CaptureRecycleObservers(new("CARDS_RECYCLED", "回收展示前私下查看的牌", new Dictionary<string, object?> {
                ["playerId"] = player, ["ownerPlayerId"] = deckOwner, ["sourceObjectId"] = entry.Unit.ObjectId, ["cardIds"] = new[] { top },
                ["count"] = 1, ["sourceZone"] = zone, ["destinationZone"] = zone }), zones, cards);
            events.Add(recycled with { Payload = recycled.Payload.Where(field => field.Key != "cardIds")
                .ToDictionary(field => field.Key, field => field.Value) });
        }
    }

    internal static bool ValidBeforeDeckRevealLook(MatchState state, BeforeDeckRevealLookContext context)
    {
        try { return context is not null && context.Parent is not null && state.Seats.ContainsKey(context.PlayerId)
            && state.Seats.ContainsKey(context.DeckOwnerId) && context.DeckZone is "MAIN_DECK" or "RUNE_DECK"
            && context.Source is { Generation: >= 0 } && context.Card is { Generation: >= 0 }
            && !string.IsNullOrWhiteSpace(context.Source.ObjectId) && !string.IsNullOrWhiteSpace(context.Card.ObjectId)
            && BeforeRevealSources.Value.Contains(context.SourceCardNo)
            && context.SourceFaceRevision >= 0 && context.CapturedTick >= 0 && context.CapturedTick <= state.Tick
            && context.InstanceIndex >= 0 && context.ExecutionIndex >= 0 && context.ExecutionIndex < context.Parent.EffectRepeatCount
            && ValidBeforeRevealInstruction(state, context)
            && (context.Origin is { } origin
                ? AttachedRuleTextRules.ValidCaptured(state, origin, context.Source.ObjectId, context.Source.Generation, context.SourceCardNo)
                : PredictBindingMatches(state, context.Source, context.SourceCardNo, context.SourceFaceRevision,
                    context.CapturedTick, context.SourceTokenFactoryCardNo)); }
        catch (Exception e) when (e is NullReferenceException or ArgumentException or KeyNotFoundException
            or InvalidOperationException or OverflowException) { return false; }
    }

    private static bool ValidBeforeRevealInstruction(MatchState state, BeforeDeckRevealLookContext context)
    {
        var parent = context.Parent;
        if (context.DeckZone == "RUNE_DECK")
            return !context.SelectionReveal && context.PlayerId == parent.ControllerId && context.DeckOwnerId == context.PlayerId
                && parent.UnitEvent is { } unit && UnitEventSpec(unit.CardNo)?.Effect == RuneDomainRevealEffect
                && ValidUnitEvent(state, unit, parent.EffectKind, parent.SourceObjectId, parent.CardNo, parent.ControllerId);
        if (context.SelectionReveal)
            return context.PlayerId == parent.ControllerId && context.DeckOwnerId == context.PlayerId
                && TryGetDeckChoiceBehavior(state, parent, out _);
        if (ExpectedObservationKind(parent) != "REVEAL" || !ValidObservationParent(state, parent)) return false;
        if (parent.BeginningDraw?.Kind == "LOWEST_DECK_TOP_MANA") return context.PlayerId == context.DeckOwnerId;
        if (TryGetEffectPlayDefinition(parent, out var play) && play.DeckPlayKind.Length > 0)
            return DeckPlayPlayers(state, parent.ControllerId, play.DeckPlayKind).Contains(context.DeckOwnerId)
                && context.PlayerId == DeckRevealPlayer(parent, context.DeckOwnerId);
        return context.PlayerId == parent.ControllerId && context.DeckOwnerId == context.PlayerId;
    }

    internal static bool ValidBeforeDeckRevealDisclosure(MatchState state)
    {
        try { return ValidBeforeDeckRevealDisclosureCore(state); }
        catch (Exception e) when (e is NullReferenceException or ArgumentException or KeyNotFoundException
            or InvalidOperationException or OverflowException) { return false; }
    }

    private static bool ValidBeforeDeckRevealDisclosureCore(MatchState state)
    {
        if (state.DeckObservationView is not { } view)
            return state.PendingRuleChoice?.Request.Kind != BeforeDeckRevealChoiceKind;
        if (view.BeforeReveal is not { } context)
            return view.DeckZone is null && view.DeckOwnerId is null
                && state.PendingRuleChoice?.Request.Kind != BeforeDeckRevealChoiceKind;
        if (state.PendingRuleChoice is not { } pending || pending.Request.Kind is not (BeforeDeckRevealChoiceKind or "DECK_OBSERVATION_BANISH")
            || pending.Request.PlayerId != context.PlayerId || view.PlayerId != context.PlayerId || view.Public
            || view.DeckOwnerId != (context.PlayerId == context.DeckOwnerId ? null : context.DeckOwnerId)
            || (view.DeckZone ?? "MAIN_DECK") != context.DeckZone
            || view.Cards.Count != 1 || !view.Cards.TryGetValue(context.Card.ObjectId, out var generation)
            || generation != context.Card.Generation || !ValidBeforeDeckRevealLook(state, context)) return false;
        var deck = context.DeckZone == "RUNE_DECK" ? state.PlayerZones[context.DeckOwnerId].RuneDeck
            : state.PlayerZones[context.DeckOwnerId].MainDeck;
        var sources = BeforeRevealInstances(state.PlayerZones, state.CardObjects, context.PlayerId);
        return deck.FirstOrDefault() == context.Card.ObjectId && context.InstanceIndex < sources.Length
            && sources[context.InstanceIndex].Unit.ObjectId == context.Source.ObjectId
            && sources[context.InstanceIndex].Unit.ObjectGeneration == context.Source.Generation
            && sources[context.InstanceIndex].Instance.CardNo == context.SourceCardNo
            && sources[context.InstanceIndex].Instance.Origin == context.Origin
            && state.CardObjects.TryGetValue(context.Card.ObjectId, out var card)
            && card.ObjectGeneration == context.Card.Generation && card.CardNo == context.CardNo;
    }
}
