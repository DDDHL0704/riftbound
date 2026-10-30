using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveLastBreathDeckTopPlay(MatchState state, StackItemState item)
    {
        if (!ValidLastBreathBattlefieldDamageStack(state, item))
            throw new InvalidOperationException("Invalid captured last-breath deck play.");
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var player = item.ControllerId;
        if (zones[player].MainDeck.FirstOrDefault() is not { } id) return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(); var before = cards[id];
        zones[player] = zones[player] with { MainDeck = zones[player].MainDeck.Skip(1).ToArray(), Banished = [..zones[player].Banished, id] };
        ResetCardOutsidePlay(zones, cards, id, before, player);
        var parent = item with { LastBreathBattlefieldDamage = item.LastBreathBattlefieldDamage! with { BanishedDeckTop = new(id, cards[id].ObjectGeneration) } };
        var ev = CaptureOwnedBanishObservers(new("CARD_BANISHED", "绝念放逐主牌堆顶部的一张牌", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
            ["targetObjectId"] = id, ["cardNo"] = cards[id].CardNo, ["playerId"] = player, ["destinationZone"] = "BANISHED" }),
            zones, cards, player, [id]);
        TryGetEffectPlayDefinition(parent, out var definition);
        var pending = EffectPlaySourceMatches(cards[id], definition)
            ? new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", player, parent, "BANISHED",
                new Dictionary<string, long> { [id] = cards[id].ObjectGeneration }, true, false, 0, "ANY", true) : null;
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = [ev],
            PendingEffectPlay = pending, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

    internal static bool ValidLastBreathDeckTopPlay(MatchState state, PendingEffectPlayState p)
        => p.Parent.LastBreathBattlefieldDamage is { Kind: TriggerKinds.UnitLastBreathBanishDeckTopPlay, BanishedDeckTop: { } top }
            && ValidLastBreathBattlefieldDamageStack(state, p.Parent) && !p.Parent.EffectPlayCompleted
            && p.SourceZone == "BANISHED" && p.PlayerId == p.Parent.ControllerId && p.SourcePlayerId is null
            && p.IgnoreBaseMana && !p.IgnoreBasePower && !p.IgnoreAllCosts && p.ManaReduction == 0
            && p.Optional && p.DestinationPolicy == "ANY" && p.OriginalLocation is null && p.OriginalBattlefield is null
            && p.ViewedCardIds is null && p.RevealedHand is null && p.SourceReplay is null
            && p.Sources.Count == 1 && p.Sources.GetValueOrDefault(top.ObjectId, -1) == top.Generation
            && state.PlayerZones[p.PlayerId].Banished.Contains(top.ObjectId)
            && state.CardObjects.TryGetValue(top.ObjectId, out var card) && card.ObjectGeneration == top.Generation
            && card.OwnerId == p.PlayerId && card.ControllerId == p.PlayerId
            && state.ObjectLocations.GetValueOrDefault(top.ObjectId) is { Zone: "BANISHED" } place && place.PlayerId == p.PlayerId
            && TryGetEffectPlayDefinition(p.Parent, out var definition) && EffectPlaySourceMatches(card, definition);
}
