using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PaidUnitReference(ObjectBinding Unit, string? TokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal static bool ValidExhaustedCostUnit(MatchState state, PaidUnitReference? reference, bool required,
        string sourceId, IReadOnlyList<string> targets)
    {
        if (!required) return reference is null;
        if (reference?.Unit is not { Generation: >= 0 } binding || string.IsNullOrWhiteSpace(binding.ObjectId)
            || binding.ObjectId == sourceId || targets.Contains(binding.ObjectId)) return false;
        if (state.CardObjects.TryGetValue(binding.ObjectId, out var card))
            return binding.Generation <= card.ObjectGeneration && reference.TokenFactoryCardNo == card.TokenFactoryCardNo;
        return reference.TokenFactoryCardNo is { } factoryNo
            && P6TokenFactoryCatalog.TryGetByCardNo(factoryNo, out var factory)
            && factory.Tags.Contains(CardObjectTags.UnitCard);
    }

    // The paid unit is a reference, not a second target. Read its current
    // position only while that incarnation remains a unit on the field.
    private static StackResolutionResult ResolveMovementToExhaustedCostUnit(MatchState state, StackItemState item,
        PaidUnitReference cost, Func<MatchState, string, bool> legalTarget)
    {
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "将友方单位移动到支付休眠费用的单位所在位置",
            new Dictionary<string, object?> {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
                ["stackItemId"] = item.StackItemId }) };
        var targetId = item.TargetObjectIds[0];
        if (!state.CardObjects.TryGetValue(cost.Unit.ObjectId, out var anchor) || anchor.ObjectGeneration != cost.Unit.Generation
            || !IsFaceUpNonStandbyUnit(anchor) || !IsObjectOnField(state.PlayerZones, anchor.ObjectId)
            || !TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, anchor.ObjectId, out var destination)
            || !StackObjectRules.ReferencesCurrentObject(state, item, targetId) || !legalTarget(state, targetId))
            return NoopStackResolutionResult(state) with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        var pools = state.RunePools.ToDictionary();
        TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, targetId, destination,
            item.ControllerId, item, events, frame => StackObjectRules.ReferencesCurrentObject(frame, item, targetId)
                && legalTarget(frame, targetId));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, RunePools = pools, Events = events };
    }
}
