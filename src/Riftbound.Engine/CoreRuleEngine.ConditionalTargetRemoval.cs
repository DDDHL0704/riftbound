using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static void ApplyConditionalRemovalEmpowerInstruction(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        if (behavior.ConditionalTargetRemoval?.RemoveEmpowermentFirst != true) return;
        foreach (var id in item.TargetObjectIds)
        {
            if (!cards.TryGetValue(id, out var card) || !IsFaceUpNonStandbyUnit(card)
                || !IsObjectOnField(state.PlayerZones, id)
                || item.TargetGenerations?.GetValueOrDefault(id, -1) != card.ObjectGeneration) continue;
            var changed = ApplyEmpowerStatusAction(state with { CardObjects = cards }, new(id, card.ObjectGeneration), false);
            cards[id] = changed.CardObjects[id];
            events.AddRange(changed.Events);
        }
    }

    private static string ConditionalRemovalAction(MatchState state, CardBehaviorDefinition behavior, string id)
    {
        if (behavior.ConditionalTargetRemoval is not { } spec
            || !state.CardObjects.TryGetValue(id, out var card) || !IsFaceUpNonStandbyUnit(card)
            || !IsObjectOnField(state.PlayerZones, id)) return "";
        return ResolveCurrentFieldUnitPower(state, card) <= spec.MaximumMight ? spec.Removal : spec.OtherwiseRemoval;
    }
}
