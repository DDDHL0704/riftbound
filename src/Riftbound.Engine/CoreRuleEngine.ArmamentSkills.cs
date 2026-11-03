using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveBorrowedArmamentSkill(MatchState state, StackItemState item,
        BorrowedExhaustSpec spec)
    {
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "获得的贴附技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId }) };
        if (item.TargetObjectIds.Where((id, index) => !StackObjectRules.ReferencesCurrentObject(state, item, id)
            || !IsBorrowedExhaustTarget(state, item.ControllerId, id, spec, item.SourceObjectId, index)).Any())
            return NoopStackResolutionResult(state) with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        TryAttachEquipment(state, zones, cards, state.ObjectLocations, item.ControllerId, item.SourceObjectId,
            item.TargetObjectIds[1], item.TargetObjectIds[0], item.EffectKind, events, out var locations,
            new Dictionary<string, object?> { ["stackItemId"] = item.StackItemId });
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, Events = events };
    }
}
