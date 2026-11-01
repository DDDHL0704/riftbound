using Riftbound.Contracts;
namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 323.6 precedes 323.8: release unsupported control before a new showdown.
    // Existing combat/showdown retains its control until its own conclusion.
    private static (MatchState State,IReadOnlyList<GameEvent> Events) ClearUnoccupiedBattlefieldControl(MatchState state)
    {
        if(state.Status!=MatchStatuses.InProgress || state.StackItems.Count>0
            || state.TimingState is not (TimingStates.NeutralOpen or TimingStates.SpellDuelOpen)) return (state,[]);
        var cards=state.CardObjects.ToDictionary(e=>e.Key,e=>e.Value);var events=new List<GameEvent>();
        var controlLossMarkers = state.UntilEndOfTurnEffects.ToHashSet(StringComparer.Ordinal);
        foreach(var field in state.BattlefieldStates.Values)
        {
            if(field.ControllerId is not { } controller || field.OccupantControllerIds.Contains(controller)
                || state.BattleState.IsActive && state.BattleState.BattlefieldObjectId==field.BattlefieldObjectId
                || state.SpellDuelState.IsActive && state.SpellDuelState.BattlefieldObjectId==field.BattlefieldObjectId) continue;
            cards[field.BattlefieldObjectId]=cards[field.BattlefieldObjectId] with {ControllerId=null};
            controlLossMarkers.RemoveWhere(effect => effect.StartsWith(
                BattlefieldTaskMarkers.ControlLostPrefix + field.BattlefieldObjectId + ":",
                StringComparison.Ordinal));
            controlLossMarkers.RemoveWhere(effect => effect.StartsWith(
                BattlefieldContestRules.InitiatorPrefix + field.BattlefieldObjectId + ":",
                StringComparison.Ordinal));
            controlLossMarkers.Add(BattlefieldTaskMarkers.ControlLost(field.BattlefieldObjectId, controller));
            events.Add(new("BATTLEFIELD_CONTROL_LOST","未留有己方单位，失去战场控制权",new Dictionary<string,object?> {
                ["playerId"]=controller,["battlefieldObjectId"]=field.BattlefieldObjectId,["previousControllerId"]=controller,
                ["reason"]="OPEN_CLEANUP_NO_CONTROLLED_UNITS" }));
        }
        if(events.Count==0) return (state,[]);
        var zones=NormalizeZonesForSeats(state);var locations=state.ObjectLocations.ToDictionary(e=>e.Key,e=>e.Value);
        var hidden=ApplyIllegalStandbyCleanup(zones,cards,locations);events.AddRange(hidden.Events);
        return (state with {
            CardObjects=cards,
            PlayerZones=zones,
            ObjectLocations=ReconcileObjectLocations(locations,zones),
            UntilEndOfTurnEffects=controlLossMarkers.Order(StringComparer.Ordinal).ToArray()
        },events);
    }
}
