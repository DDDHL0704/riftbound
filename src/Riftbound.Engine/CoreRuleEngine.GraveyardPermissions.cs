using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string NormalGraveyardPlayChoice = "NORMAL_GRAVEYARD_PLAY";

    private static IEnumerable<CardBehaviorDefinition> ActiveFieldCardBehaviors(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, string player)
        => cards.Values.Where(c => !c.IsFaceDown && IsObjectOnField(zones, c.ObjectId)
                && EffectiveFieldControllerId(zones, c.ObjectId, c) == player)
            .SelectMany(c => AttachedRuleTextRules.Instances(zones, cards, c))
            .Select(i => CardBehaviorRegistry.TryGetByCardNo(i.CardNo, out var behavior) ? behavior : null)
            .OfType<CardBehaviorDefinition>();

    internal static IReadOnlyList<string> NormalGraveyardPlaySources(MatchState state, string player)
        => state.PendingEffectPlay is null && state.PlayerZones.TryGetValue(player, out var zones)
            && ActiveFieldCardBehaviors(state.PlayerZones, state.CardObjects, player).Any(b => b.GrantsNormalGraveyardPlay)
                ? zones.Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown
                    && card.OwnerId == player && card.ControllerId == player
                    && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var b)
                    && (IsSpellPlayBehavior(b) || b.PlaysSourceToBaseAsEquipment || b.PlaysSourceToBaseAsUnit)).ToArray() : [];

    internal static IReadOnlyList<CardBehaviorDefinition> NormalGraveyardPlayBehaviors(MatchState state,
        string player, string source, CardBehaviorDefinition behavior)
        => behavior.AppliedFlowCost is not null && NormalGraveyardPlaySources(state, player).Contains(source)
            ? [behavior, behavior with { AppliedFlowCost = null }] : [behavior];

    private static bool RedirectsGraveyardEntry(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string owner, string id)
        => ActiveFieldCardBehaviors(zones, cards, owner).Any(b => b.BanishesNonDeckGraveyardEntries);

    private static PlayerZones AddNonDeckGraveyardEntries(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string owner, PlayerZones destination, IReadOnlyList<string> ids)
    {
        var banished = ids.Where(id => RedirectsGraveyardEntry(zones, cards, owner, id)).ToHashSet(StringComparer.Ordinal);
        return destination with { Graveyard = destination.Graveyard.Concat(ids.Where(id => !banished.Contains(id))).Distinct().ToArray(),
            Banished = destination.Banished.Concat(ids.Where(banished.Contains)).Distinct().ToArray() };
    }

    // CN 370.4/373: a replacement source applies to its simultaneous group even
    // when that group also removes the source. Other replacements may run first.
    private static IReadOnlyList<CapturedDestructionOption> CaptureGraveyardDestructionBanishes(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        IEnumerable<string> targets)
    {
        var result = new List<CapturedDestructionOption>();
        foreach (var source in cards.Values.Where(c => !c.IsFaceDown && IsObjectOnField(zones, c.ObjectId)))
            foreach (var rule in AttachedRuleTextRules.Instances(zones, cards, source).Where(i =>
                CardBehaviorRegistry.TryGetByCardNo(i.CardNo, out var b) && b.BanishesNonDeckGraveyardEntries)) {
                var owner = EffectiveFieldControllerId(zones, source.ObjectId, source);
                foreach (var id in targets.Where(id => cards.TryGetValue(id, out var target)
                    && NonFieldDestinationOwner(zones, target, target.ControllerId ?? "") == owner))
                    result.Add(new(new(new($"GRAVEYARD_BANISH:{id}:{source.ObjectId}",
                        $"{source.ObjectId}：将 {id} 进入废牌堆替换为放逐", [id, source.ObjectId]), id, source.ObjectId,
                        "BANISH", $"GRAVEYARD_BANISH:{source.ObjectId}:{source.ObjectGeneration}:{rule.Id}", rule.CardNo), cards[id].ObjectGeneration));
            }
        return result;
    }

    private static StackResolutionResult ResolveBanishHandGraveyardBurn(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var own = zones[item.ControllerId]; var ids = own.Hand.Concat(own.Graveyard).Distinct().ToArray();
        zones[item.ControllerId] = own with { Hand = [], Graveyard = [], Banished = own.Banished.Concat(ids).Distinct().ToArray() };
        foreach (var id in ids) if (cards.TryGetValue(id, out var card)) ResetCardOutsidePlay(zones, cards, id, card, item.ControllerId);
        var events = new List<GameEvent> { new("CARDS_BANISHED", "放逐手牌和废牌堆里的卡牌", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["cardIds"] = ids,
            ["count"] = ids.Length, ["fromZones"] = new[] { "HAND", "GRAVEYARD" }, ["destinationZone"] = "BANISHED" }) };
        var burn = ApplyBurnToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId,
            UnitEventSpec(item.CardNo)!.Amount, state.RngCursor, item, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), PlayerScores = burn.PlayerScores,
            WinnerPlayerId = burn.WinnerPlayerId, RngCursor = burn.RngCursor, Events = events };
    }

    private static IReadOnlyList<string> RecordActualHandDiscards(IReadOnlyDictionary<string, PlayerZones> zones,
        string player, string eventKind, IReadOnlyList<string> attempted, List<GameEvent> events)
    {
        var banished = attempted.Where(id => zones[player].Banished.Contains(id)).ToHashSet(StringComparer.Ordinal);
        if (banished.Count > 0) for (var index = 0; index < events.Count; index++) {
            var ev = events[index];
            if (ev.Kind != eventKind) continue;
            var ids = ev.Payload.GetValueOrDefault("targetObjectId") is string id ? new[] { id }
                : ev.Payload.GetValueOrDefault("cardIds") as IReadOnlyList<string>
                    ?? ev.Payload.GetValueOrDefault("discardedObjectIds") as IReadOnlyList<string> ?? [];
            if (ids.Count == 0 || !ids.All(banished.Contains)) continue;
            events[index] = ev with { Kind = ids.Count == 1 ? "CARD_BANISHED" : "CARDS_BANISHED",
                Description = "弃置被替换为放逐", Payload = new Dictionary<string, object?>(ev.Payload) {
                    ["destinationZone"] = "BANISHED", ["replacedAction"] = "DISCARD" } };
        }
        return attempted.Where(id => zones[player].Graveyard.Contains(id)).ToArray();
    }
}
