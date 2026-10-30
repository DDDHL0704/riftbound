using System.Text.Json.Serialization;

namespace Riftbound.Engine;

// Resource-only Flow costs share normal payment, timing and extra-cost rules.
public sealed record FlowCostSpec(int Mana, int Power, IReadOnlyList<string> Traits);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FlowPlayReceipt(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string OwnerId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Grant = null);

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> FlowPlaySources(MatchState state, string player)
        => state.PendingEffectPlay is null && state.PlayerZones.TryGetValue(player, out var zones)
            ? zones.Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var card)
                && card.OwnerId == player && card.ControllerId == player && !card.IsFaceDown
                && card.Tags.Contains(CardObjectTags.SpellCard)
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior)
                && IsSpellPlayBehavior(behavior)
                && FlowPlayBehaviors(state, player, id, behavior).Any(b => b.AppliedFlowCost is not null)).ToArray() : [];

    internal const string FlowCostChoicePrefix = "FLOW_COST:";
    internal static string FlowCostChoiceId(FlowCostSpec cost)
        => FlowCostChoicePrefix + cost.Mana + ":" + cost.Power + ":"
            + (cost.Power == 0 ? "" : string.Join(',', cost.Traits.Order(StringComparer.Ordinal)));

    internal static IReadOnlyList<CardBehaviorDefinition> FlowPlayBehaviors(MatchState state, string player,
        string source, CardBehaviorDefinition behavior)
    {
        if (state.PendingEffectPlay is not null || !state.PlayerZones.TryGetValue(player, out var zones)
            || !zones.Graveyard.Contains(source) || !state.CardObjects.TryGetValue(source, out var card)
            || card.OwnerId != player || card.ControllerId != player || card.IsFaceDown
            || !card.Tags.Contains(CardObjectTags.SpellCard) || card.CardNo != behavior.CardNo
            || !IsSpellPlayBehavior(behavior)) return [behavior];
        var choices = new List<CardBehaviorDefinition>();
        if (behavior.FlowCost is { } printed) choices.Add(behavior with { AppliedFlowCost = printed });
        foreach (var grant in CurrentFlowGrants(state, player, source))
            choices.Add(behavior with { AppliedFlowCost = PrintedFlowCost(behavior.CardNo), AppliedFlowGrant = FlowGrantMarker(grant) });
        return choices.Count == 0 ? [behavior] : choices.DistinctBy(b => FlowCostChoiceId(b.AppliedFlowCost!))
            .OrderBy(b => b.AppliedFlowCost!.Mana).ThenBy(b => b.AppliedFlowCost!.Power).ToArray();
    }

    private static bool TrySelectFlowCost(MatchState state, string player, string source,
        CardBehaviorDefinition behavior, IReadOnlyList<string>? selections, out CardBehaviorDefinition selected,
        out IReadOnlyList<string> remaining)
    {
        var normalGraveyardPlay = (selections ?? []).Any(c => string.Equals(c?.Trim(), NormalGraveyardPlayChoice, StringComparison.Ordinal));
        var choices = (selections ?? []).Where(c => c?.Trim().StartsWith(FlowCostChoicePrefix, StringComparison.Ordinal) == true).ToArray();
        remaining = (selections ?? []).Where(c => c?.Trim().StartsWith(FlowCostChoicePrefix, StringComparison.Ordinal) != true
            && !string.Equals(c?.Trim(), NormalGraveyardPlayChoice, StringComparison.Ordinal)).ToArray();
        if (normalGraveyardPlay && choices.Length == 0)
        {
            selected = behavior;
            return true;
        }
        var variants = FlowPlayBehaviors(state, player, source, behavior);
        selected = variants.FirstOrDefault(b => choices.Length == 0 || b.AppliedFlowCost is { } cost
            && FlowCostChoiceId(cost) == choices[0])!;
        return choices.Length <= 1 && selected is not null;
    }

    private static FlowPlayReceipt? FlowReceipt(MatchState state, string player,
        string source, CardBehaviorDefinition behavior)
        => behavior.AppliedFlowCost is not null
            ? new(new(source, checked(state.CardObjects[source].ObjectGeneration + 1)), behavior.CardNo, player, behavior.AppliedFlowGrant) : null;

    // CN829.1.b.1: normal completion and countering both leave the chain.
    // The receipt follows this confirmed play, even if its controller changes.
    private static string FlowStackExitDestination(IReadOnlyDictionary<string, CardObjectState> cards,
        StackItemState item, string destination)
        => item.PlayedFromNormalGraveyard && CardBehaviorRegistry.GetAll()
                .Where(b => b.CardNo == item.CardNo).Any(IsSpellPlayBehavior)
            ? "BANISHED"
            : item.FlowPlay is { } flow && flow.Source.ObjectId == item.SourceObjectId
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
            && behavior.CardNo == flow.CardNo && IsSpellPlayBehavior(behavior)
            && (flow.Grant is null ? behavior.FlowCost is not null
                : ReadFlowGrant(flow.Grant) is { } grant && ValidFlowGrant(state, grant)
                    && grant.ControllerId == flow.OwnerId && grant.Spell.ObjectId == source.ObjectId
                    && grant.CardNo == source.CardNo && grant.Spell.Generation + 1 == flow.Source.Generation
                    && state.UntilEndOfTurnEffects.Contains(flow.Grant, StringComparer.Ordinal))
            && item.PlayCost is not null && item.PlayCost.CardMana == behavior.ManaCost
            && !item.PlayedFromHand && item.PlayedFromStandby is null
            && !item.SourceConfirmed && item.DeckPlayPendingCard is null && item.AfterPlayRecycle is null
            && !StackObjectRules.IsAbility(item);
    }
}
