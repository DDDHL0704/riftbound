using System.Text.Json.Serialization;

namespace Riftbound.Engine;

// The currently adopted consumer has one resource-only Flow cost. Other
// printings, multiple costs and non-resource costs require their own review.
public sealed record FlowCostSpec(int Mana, int Power, IReadOnlyList<string> Traits);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FlowPlayReceipt(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string OwnerId);

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> FlowPlaySources(MatchState state, string player)
        => state.PendingEffectPlay is null && state.PlayerZones.TryGetValue(player, out var zones)
            ? zones.Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var card)
                && card.OwnerId == player && card.ControllerId == player && !card.IsFaceDown
                && card.Tags.Contains(CardObjectTags.SpellCard)
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior)
                && IsSpellPlayBehavior(behavior) && behavior.FlowCost is not null).ToArray() : [];

    internal static CardBehaviorDefinition FlowPlayBehavior(MatchState state, string player,
        string source, CardBehaviorDefinition behavior)
        => behavior.FlowCost is not null && FlowPlaySources(state, player).Contains(source, StringComparer.Ordinal)
            ? behavior with { AppliedFlowCost = behavior.FlowCost } : behavior;

    private static FlowPlayReceipt? FlowReceipt(MatchState state, string player,
        string source, CardBehaviorDefinition behavior)
        => behavior.AppliedFlowCost is not null
            ? new(new(source, checked(state.CardObjects[source].ObjectGeneration + 1)), behavior.CardNo, player) : null;

    // CN829.1.b.1: normal completion and countering both leave the chain.
    // The receipt follows this confirmed play, even if its controller changes.
    private static string FlowStackExitDestination(IReadOnlyDictionary<string, CardObjectState> cards,
        StackItemState item, string destination)
        => item.FlowPlay is { } flow && flow.Source.ObjectId == item.SourceObjectId
            && cards.TryGetValue(item.SourceObjectId, out var card) && card.ObjectGeneration == flow.Source.Generation
                ? "BANISHED" : destination;

    internal static bool ValidFlowPlay(MatchState state, StackItemState item)
    {
        if (item.FlowPlay is not { } flow) return true;
        return flow.Source is not null && !string.IsNullOrWhiteSpace(item.SourceObjectId)
            && flow.Source.ObjectId == item.SourceObjectId && flow.Source.Generation >= 1
            && !string.IsNullOrWhiteSpace(flow.CardNo) && flow.CardNo == item.CardNo
            && !string.IsNullOrWhiteSpace(flow.OwnerId) && state.Seats.ContainsKey(flow.OwnerId)
            && state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.CardNo == flow.CardNo && source.OwnerId == flow.OwnerId
            && source.ObjectGeneration == flow.Source.Generation
            && state.ObjectLocations.GetValueOrDefault(source.ObjectId)?.Zone == "STACK"
            && source.Tags.Contains(CardObjectTags.SpellCard) && !source.IsFaceDown
            && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && behavior.CardNo == flow.CardNo && IsSpellPlayBehavior(behavior) && behavior.FlowCost is not null
            && item.PlayCost is not null && item.PlayCost.CardMana == behavior.ManaCost
            && !item.PlayedFromHand && item.PlayedFromStandby is null
            && !item.SourceConfirmed && item.DeckPlayPendingCard is null && item.AfterPlayRecycle is null
            && !StackObjectRules.IsAbility(item);
    }
}
