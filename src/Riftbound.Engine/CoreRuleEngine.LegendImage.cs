using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static IEnumerable<TriggerQueueItemState> CaptureLegendImageTriggers(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string field, long tick) => zones[player].LegendZone
        .Where(id => cards.TryGetValue(id, out var c) && c.ControllerId == player && !c.IsFaceDown
            && LegendCardHasIdentity(c.CardNo, LeblancLegendIdentityId))
        .Select(id => new TriggerQueueItemState($"conquest-image-{tick}-{field}-{id}", player, id,
            "HOLD_LEBLANC_DISCARD", "BATTLEFIELD_CONQUERED") {
            HeldContext = new(cards[id].CardNo!, field, "LEBLANC_DISCARD", 1, cards[id].ObjectGeneration) });

    private static StackResolutionResult ResolveDiscardTokenCreation(MatchState state, StackItemState item)
    {
        if (item.TriggerCost is null) return NoopStackResolutionResult(state);
        var field = item.HeldContext!.BattlefieldObjectId;
        var created = CreateUnitTokenBatch(state, item, P6TokenFactoryCatalog.ImageTokenCardNo,
            1 + AdditionalUnitTokens(item), true, field);
        var recipients = created.Events.Where(e => e.Kind == "UNIT_TOKEN_CREATED")
            .Select(e => created.CardObjects[(string)e.Payload["tokenObjectId"]!]).ToArray();
        if (recipients.Length == 0) return created;
        var events = created.Events.ToList();
        AddReflexiveCopyEvent(events, item.ControllerId, item.SourceObjectId, item.CardNo, null, recipients, field);
        return created with { Events = events };
    }

}
