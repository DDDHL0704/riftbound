using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The resolved play receipt already captures the source's entry generation.
    // Reuse it without exposing a link to a later private-zone incarnation.
    internal static ObjectBinding? PlayInstructionSource(MatchState state, StackItemState item)
        => state.CardPlays.Resolved.FirstOrDefault(play => play.PlayId == item.StackItemId
            && play.CardNo == item.CardNo && play.PlayerId == item.ControllerId
            && play.Source.ObjectId == item.SourceObjectId)?.Source;

    internal static ContinuousEffectState PublicPlayInstructionEffect(MatchState state, ContinuousEffectState effect, string viewer)
    {
        // Keep the full provenance in the authoritative ledger. A recycled
        // source's new deck object is not disclosed by its resolved modifier.
        if (effect.SourceCardNo is not { } no || effect.SourceObjectId is not { } id
            || !CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)
            || !behavior.RecyclesSourceIfPlayedFromStandby
            || ResolutionResult.VisibleObjectIdForViewer(state, id, viewer) != "HIDDEN") return effect;
        return effect with { SourceObjectId = "HIDDEN" };
    }

    private static void ResolvePermanentPlaySourceInstructions(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        // A printed instruction referring to "me" keeps the object that entered,
        // even when a response returns and replays the physical card (CN 124).
        if (!item.SourceConfirmed || PlayInstructionSource(state, item) is not { } source
            || !cards.TryGetValue(source.ObjectId, out var card)
            || card.ObjectGeneration != source.Generation || !IsFaceUpNonStandbyUnit(card)
            || !IsObjectOnField(zones, source.ObjectId)) return;
        if (behavior.GrantsSourceEphemeralIfOpponentTurn && state.TurnPlayerId != item.ControllerId)
        {
            cards[source.ObjectId] = card with {
                Tags = card.Tags.Append(CardObjectTags.Ephemeral).Distinct(StringComparer.Ordinal).ToArray(),
                GrantedTags = (card.GrantedTags ?? []).Append(CardObjectTags.Ephemeral).Distinct(StringComparer.Ordinal).ToArray() };
            events.Add(new("KEYWORD_GRANTED", "打出技能给予来源瞬息", new Dictionary<string, object?> {
                ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId,
                ["keyword"] = CardObjectTags.Ephemeral }));
        }
        if (behavior.RecyclesSourceIfPlayedFromStandby && item.PlayedFromStandby is not null
            && TryMoveTargetToOwnerMainDeck(zones, cards, source.ObjectId, "BOTTOM", out var owner, out _))
            events.Add(CaptureRecycleObservers(new("CARDS_RECYCLED", "打出技能回收来源单位", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["ownerPlayerId"] = owner,
                ["sourceObjectId"] = source.ObjectId, ["cardIds"] = new[] { source.ObjectId },
                ["count"] = 1, ["destinationZone"] = "MAIN_DECK" }), zones, cards));
    }
}
