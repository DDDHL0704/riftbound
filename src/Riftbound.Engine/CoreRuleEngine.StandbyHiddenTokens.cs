using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string StandbyHiddenTokenEffect = "STANDBY_HIDDEN_CREATE_EQUIPMENT_TOKEN";

    private static TriggerSpec? StandbyHiddenTokenSpec(string? cardNo)
        => BattlefieldTriggerSpecRules.TriggersForCard(cardNo).SingleOrDefault(t =>
            t.Kind == TriggerKinds.BattlefieldStandbyHiddenCreateEquipmentToken && t.Timing == TriggerTimings.StandbyHidden
            && t.CreatedTokenCount is > 0 && t.CreatedTokenDestination == TriggerTokenDestinations.OwnerBase
            && P6TokenFactoryCatalog.GetAll().Any(f => f.TokenFamilyName == t.CreatedTokenName
                && f.Tags.Contains(CardObjectTags.EquipmentCard)));

    // Hiding is a completed game action. Its earned skill opens a response
    // window before a token is created; the face-down card is never its source.
    private static ResolutionResult QueueStandbyHiddenTokens(ResolutionResult result, string player, string battlefield)
    {
        var field = BattlefieldLocalRules.Battlefield(result.State, battlefield);
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress || StandbyHiddenTokenSpec(field?.CardNo) is null)
            return result;
        var trigger = new TriggerQueueItemState($"standby-hidden-{result.State.Tick}-{battlefield}", player,
            field!.ObjectId, StandbyHiddenTokenEffect, "CARD_HIDDEN") {
            SourceCardNo = field.CardNo,
            StandbyObserver = new(field.CardNo!, new(field.ObjectId, field.ObjectGeneration), BattlefieldId: battlefield) };
        var next = result.State with { TriggerQueue = result.State.TriggerQueue.Append(trigger).ToArray() };
        return result with { State = next, Events = result.Events.Append(BuildTriggerQueuedEvent(trigger)).ToArray(),
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static StackResolutionResult ResolveStandbyHiddenTokens(MatchState state, StackItemState item)
    {
        var spec = StandbyHiddenTokenSpec(item.StandbyObserver!.CardNo)!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        for (var i = 0; i < spec.CreatedTokenCount.GetValueOrDefault(); i++)
            CreateLegendEquipmentToken(zones, cards, item.ControllerId, item.SourceObjectId, spec.Kind,
                spec.CreatedTokenName!, [CardObjectTags.EquipmentCard, spec.CreatedTokenName!],
                spec.CreatedTokenExhausted == true, events, item.StandbyObserver.CardNo);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
