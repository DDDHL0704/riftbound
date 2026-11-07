using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 383.4.d and 403.2: the held location earns the skill, while the
    // destination is a separate, mandatory choice made before responses.
    private static StackResolutionResult ResolveUnitHeldEquipment(MatchState state, StackItemState item)
    {
        var context = item.HeldContext!;
        var target = HeldResolvedTarget(state, item);
        if (target is null) return NoopStackResolutionResult(state);
        var spec = UnitHeldSpec(context.CardNo)!;
        TryGetEquipmentTokenDefinition(spec.CreatedTokenName!, [], context.CardNo, out var factory);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var locations = CreateBattlefieldEquipmentTokenBatch(zones, cards, item.ControllerId, item.SourceObjectId,
            context.CardNo, "据守技能", factory.TokenFamilyName, factory.Tags, context.Amount,
            spec.CreatedTokenExhausted == true, target, events, state.ObjectLocations,
            publicSourceObjectId: visibleSource);
        events.Add(new("TRIGGER_RESOLVED", "据守技能打出装备指示物", new Dictionary<string, object?> {
            ["triggerId"] = item.StackItemId, ["controllerId"] = item.ControllerId,
            ["sourceObjectId"] = visibleSource, ["effectKind"] = item.EffectKind,
            ["battlefieldObjectId"] = target }));
        return NoopStackResolutionResult(state) with {
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events
        };
    }
}
