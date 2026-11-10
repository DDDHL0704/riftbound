using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? GraveyardReturnAnyTags { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventGraveyardReturnEffect = "RETURN_GRAVEYARD_UNIT_TO_HAND";

    private static bool IsUnitEventGraveyardReturnTarget(MatchState state, StackItemState item, string id)
        => state.PlayerZones[item.ControllerId].Graveyard.Contains(id)
            && state.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown && !TokenObjectRules.IsToken(card)
            && card.Tags.Contains(CardObjectTags.UnitCard)
            && SelectedUnitEventSpec(item.UnitEvent!)?.GraveyardReturnAnyTags is { Count: > 0 } tags
            && tags.Any(tag => HasAttribute(card, tag));

    private static StackResolutionResult ResolveUnitEventGraveyardReturn(MatchState state, StackItemState item)
    {
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || !StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !IsUnitEventGraveyardReturnTarget(state, item, target)) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        if (!TryReturnGraveyardCardToHand(zones, state.CardObjects, item.ControllerId, target))
            return NoopStackResolutionResult(state);
        return NoopStackResolutionResult(state) with { PlayerZones = zones,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            Events = [new("CARD_RETURNED_TO_HAND", "打出技能让符合属性的单位从废牌堆返回手牌",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target,
                    ["sourceZone"] = "GRAVEYARD", ["destinationZone"] = "HAND" })] };
    }
}
