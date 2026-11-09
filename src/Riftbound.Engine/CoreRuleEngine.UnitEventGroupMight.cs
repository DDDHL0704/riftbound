using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // A targetless instruction reads its current beneficiaries at resolution.
    // The original source object is excluded even when its controller changes;
    // a later incarnation with the same identifier is a different unit.
    private static StackResolutionResult ResolveOtherFriendlyUnitMight(MatchState state,
        StackItemState item, UnitEventAbilitySpec spec)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        var context = item.UnitEvent!;
        if (spec.FriendlyGroupAtSourceHere
            && CombatMightSourceLocation(state, item.SourceObjectId, context.SourceGeneration) is null)
            return NoopStackResolutionResult(state);
        var behavior = new CardBehaviorDefinition(context.CardNo, "其他友方单位本回合获得战力",
            0, item.EffectKind, 0, 0, PowerModifierAmount: spec.Amount);
        var publicSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        using (new MightInstructionScope(state, zones, cards, events))
            foreach (var id in GetControlledFieldUnitObjectIds(zones, cards, item.ControllerId))
            {
                var unit = cards[id];
                if (!IsFaceUpNonStandbyUnit(unit)
                    || id == item.SourceObjectId && unit.ObjectGeneration == context.SourceGeneration
                    || spec.FriendlyGroupAtSourceHere && !IsSameFieldLocation(zones, state.ObjectLocations, item.SourceObjectId, id)) continue;
                if (spec.Amount != 0)
                {
                    cards[id] = ApplyPowerModifier(state with { PlayerZones = zones, CardObjects = cards },
                        unit, behavior, item, id, spec.Amount, out var power);
                    events.Add(power with { Payload = new Dictionary<string, object?>(power.Payload) { ["sourceObjectId"] = publicSource } });
                }
                if (spec.GrantedCombatKeyword is { } keyword)
                    GrantUnitEventCombatKeyword(state, item, cards, events, id, keyword);
            }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }

    private static void GrantUnitEventCombatKeyword(MatchState state, StackItemState item,
        Dictionary<string, CardObjectState> cards, List<GameEvent> events, string target, string keyword)
    {
        var effect = TemporaryCombatKeywordRules.AppliedEffectId(cards[target], TemporaryCombatKeywordRules.Prefix + keyword);
        if (!TemporaryCombatKeywordRules.IsValid(effect)) throw new InvalidOperationException("Invalid triggered combat keyword grant.");
        cards[target] = cards[target] with { UntilEndOfTurnEffects = [..cards[target].UntilEndOfTurnEffects, effect] };
        events.Add(new("UNIT_STATUS_APPLIED", "触发技能给予本回合战斗关键词", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
            ["targetObjectId"] = target, ["statusEffectId"] = effect }));
    }
}
