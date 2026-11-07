using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // This is a fact of this source's actual play, including token plays. A
    // later move is not a new play, and another permanent's play is irrelevant.
    private static UnitEventObservation[] CaptureConditionalSourcePlayDestination(string entered,
        string destination, UnitEventObservation[] observations)
        => observations.Select(o => o.Source == entered
            && UnitEventSpec(o.Context.CardNo)?.BoonsFriendlyUnitsHereIfPlayedToBattlefield == true
                ? o with { Context = o.Context with { SourcePlayDestinationZone = destination } } : o).ToArray();

    private static bool ValidConditionalSourcePlayDestination(UnitEventContext context, UnitEventAbilitySpec spec)
        => spec.BoonsFriendlyUnitsHereIfPlayedToBattlefield
            ? spec.Timing == "PLAY" && spec.Effect == "BOON_SELF"
                && context.SourcePlayDestinationZone is "BASE" or "BATTLEFIELD"
            : context.SourcePlayDestinationZone is null;

    private static void GrantFriendlyUnitBoonsAtSourceLocation(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        if (!locations.TryGetValue(item.SourceObjectId, out var here) || here.Zone is not ("BASE" or "BATTLEFIELD")) return;
        var targets = cards.Values.Where(card => IsFaceUpNonStandbyUnit(card) && IsObjectOnField(zones, card.ObjectId)
                && EffectiveFieldControllerId(zones, card.ObjectId, card) == item.ControllerId
                && locations.TryGetValue(card.ObjectId, out var at) && at.Zone == here.Zone
                && (here.Zone == "BASE" ? at.PlayerId == here.PlayerId : at.BattlefieldObjectId == here.BattlefieldObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
        var behavior = new CardBehaviorDefinition(item.CardNo, "此处友方单位增益", 0, item.EffectKind, 0, 0);
        // CN359.3.f.2: the instruction's "here" is the source's current location.
        // Its original battlefield-play condition remains the captured play fact.
        using var instruction = new MightInstructionScope(state, zones, cards, events);
        foreach (var id in targets)
        {
            cards[id] = ApplyBoon(cards[id], behavior, item, id, out var granted, zones, cards);
            events.AddRange(granted);
        }
    }
}
