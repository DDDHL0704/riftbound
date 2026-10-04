using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasPendingPermanentPlayAbility(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
        => OfficialPrintedTimingRules.HasPlayTrigger(behavior.CardNo)
            || (behavior.MainDeckLookCount > 0 && behavior.RecyclesSelectedMainDeckTargets)
            || (behavior.PlaysSourceToBaseAsUnit && ControllerHasRengarLegend(state, item.ControllerId));

    // CN 359.2: confirming a permanent is part of PLAY_CARD, not a pair of passes.
    private static ResolutionResult ConfirmPlayedPermanent(MatchState before, MatchState paid,
        PlayerIntent intent, IReadOnlyList<GameEvent> playEvents)
    {
        var confirmation = ResolvePassPriority(paid with { Tick = before.Tick }, intent, confirmPermanent: true);
        var events = playEvents.Where(e => e.Kind != "STACK_ITEM_ADDED").Concat(confirmation.Events).ToArray();
        return confirmation with { Events = events };
    }

    private static StackResolutionResult PendingPermanentPlayAbility(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        var ability = item with { SourceConfirmed = true };
        events.Add(new("TRIGGER_QUEUED", "入场后的打出技能等待响应", new Dictionary<string, object?>
        { ["sourceObjectId"] = item.SourceObjectId, ["controllerId"] = item.ControllerId,
            ["stackItemId"] = ability.StackItemId, ["effectKind"] = item.EffectKind }));
        return new(zones, cards, state.PlayerScores, state.PlayerExperience, state.RunePools,
            state.UntilEndOfTurnEffects, null, events, [],
            state.StackItems.Where(x => x.StackItemId != item.StackItemId).Append(ability).ToArray(),
            [], null, [], state.RngCursor,
            ObjectLocations: ReconcileObjectLocations(state.ObjectLocations, zones));
    }
}
