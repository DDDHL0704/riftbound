using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool SimpleActivationTargetCountValid(P4ActivatedAbilityDefinition ability, int count)
        => count >= ability.RequiredTargetCount && count <= (ability.MaximumTargetCount ?? ability.RequiredTargetCount);

    private static bool IsAnyGraveyardCard(MatchState state, string id)
        => state.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown && !TokenObjectRules.IsToken(card)
            && state.PlayerZones.Any(pair => pair.Value.Graveyard.Contains(id, StringComparer.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, pair.Key, id));

    private static StackResolutionResult ResolveActivatedGraveyardRecycle(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state);
        var selected = item.TargetObjectIds.Where(id => IsAnyGraveyardCard(state, id)
            && state.CardObjects[id].ObjectGeneration == item.TargetGenerations!.GetValueOrDefault(id, -1))
            .ToHashSet(StringComparer.Ordinal);
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "回收已选择且仍合法的废牌",
            new Dictionary<string, object?> { ["sourceObjectId"] = visibleSource, ["effectKind"] = item.EffectKind }) };
        var cursor = state.RngCursor;
        foreach (var player in zones.Keys.Order(StringComparer.Ordinal))
        {
            var ids = zones[player].Graveyard.Where(selected.Contains).Order(StringComparer.Ordinal).ToArray();
            var recycled = RecycleGraveyardCards(state with { RngCursor = cursor }, zones, state.CardObjects,
                player, item.SourceObjectId, ids, "ACTIVATED_ABILITY_EFFECT");
            events.AddRange(recycled.Events.Select(entry => entry with { Payload = new Dictionary<string, object?>(entry.Payload) {
                ["sourceObjectId"] = visibleSource } }));
            cursor = recycled.RngCursor;
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, Events = events, RngCursor = cursor };
    }
}
