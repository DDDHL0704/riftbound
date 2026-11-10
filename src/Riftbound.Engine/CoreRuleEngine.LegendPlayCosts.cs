using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record CardBehaviorDefinition
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool MayExhaustLegendAdditionalCost { get; init; }
}

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresSourcePlayLegendCost { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendPlayCostReceipt(string PlayId, ObjectBinding Legend, string CardNo,
    long FaceRevision, string PlayerId, long PaidTick);

public sealed partial record StackItemState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegendPlayCostReceipt? LegendPlayCost { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? UnitEventMovementBattlefield { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegendPlayCostReceipt? PaidLegendPlayCost { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string ExhaustLegendPlayCostPrefix = "EXHAUST_LEGEND:";
    internal const string MoveFriendlyGroupToOpenBattlefieldEffect = "MOVE_FRIENDLY_GROUP_TO_OPEN_BATTLEFIELD";

    private static bool CanExhaustLegendForPlay(MatchState state, string player, string id)
        => state.PlayerZones.TryGetValue(player, out var zones) && zones.LegendZone.Contains(id)
            && state.ObjectLocations.TryGetValue(id, out var at) && at.PlayerId == player && at.Zone == "LEGEND"
            && state.CardObjects.TryGetValue(id, out var legend) && !legend.IsFaceDown && !legend.IsExhausted
            && legend.Tags.Contains("CARD_TYPE:LEGEND")
            && SourceObjectControlledByPlayerOrLegacyOwned(legend, player);

    internal static IReadOnlyList<string> OptionalLegendPlayCostChoices(MatchState state, string player,
        CardBehaviorDefinition behavior)
        => !behavior.MayExhaustLegendAdditionalCost || EffectPlayIgnoresAllCosts(state, player) ? []
            : state.PlayerZones.GetValueOrDefault(player, PlayerZones.Empty).LegendZone
                .Where(id => CanExhaustLegendForPlay(state, player, id)).Order(StringComparer.Ordinal)
                .Select(id => ExhaustLegendPlayCostPrefix + id).ToArray();

    private static LegendPlayCostReceipt? SourcePaidLegendPlayCost(CardObjectState entered, string player)
        => RuleChoices.Value?.ResolutionItem is { LegendPlayCost: { } receipt } play
            && play.SourceObjectId == entered.ObjectId && play.ControllerId == player
            && receipt.PlayId == play.StackItemId && receipt.PlayerId == player ? receipt : null;

    internal static bool ValidLegendPlayCostReceipt(MatchState state, LegendPlayCostReceipt? receipt)
        => receipt is { PlayId.Length: > 0, Legend: { ObjectId.Length: > 0, Generation: >= 0 },
                CardNo.Length: > 0, PlayerId.Length: > 0, FaceRevision: >= 0, PaidTick: > 0 }
            && receipt.PaidTick <= state.Tick && state.Seats.ContainsKey(receipt.PlayerId)
            && PredictBindingMatches(state, receipt.Legend, receipt.CardNo, receipt.FaceRevision, receipt.PaidTick, null);

    private static bool ValidPaidLegendPlayCondition(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec)
        => spec.RequiresSourcePlayLegendCost
            ? spec.Timing == "PLAY" && ValidLegendPlayCostReceipt(state, context.PaidLegendPlayCost)
                && context.PaidLegendPlayCost!.PaidTick <= context.CapturedTick
                && context.SourcePlayCondition?.PlayingPlayerId == context.PaidLegendPlayCost.PlayerId
            : context.PaidLegendPlayCost is null;

    private static StackResolutionResult ResolveFriendlyGroupToOpenBattlefield(MatchState state, StackItemState item)
    {
        if (item.UnitEventMovementBattlefield is not { } field
            || item.Destination != "BATTLEFIELD:" + field.ObjectId
            || !state.CardObjects.TryGetValue(field.ObjectId, out var battlefield)
            || battlefield.ObjectGeneration != field.Generation || !IsOpenBattlefield(state, field.ObjectId))
            return NoopStackResolutionResult(state);
        var legal = UnitEventTargets(state, item).ToHashSet(StringComparer.Ordinal);
        var ids = item.TargetObjectIds.Where(id => legal.Contains(id)
            && StackObjectRules.ReferencesCurrentObject(state, item, id)).ToArray();
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var pools = state.RunePools.ToDictionary();
        var events = new List<GameEvent>();
        MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, ids, item.Destination, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools, Events = events };
    }
}
