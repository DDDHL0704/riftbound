using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RequiredFriendlyUnitTagsForScore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RequiredFriendlyUnitTagsAtTrigger { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool TargetsHere { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string FriendlyUnitTraitScoreEffect = "SCORE_IF_FRIENDLY_UNITS_HAVE_ALL_TAGS";

    private static bool IsFriendlyUnitTraitScoreSpec(UnitEventAbilitySpec? spec)
        => spec is { Effect: FriendlyUnitTraitScoreEffect, Amount: > 0,
            RequiredFriendlyUnitTagsForScore: { Count: > 0 } tags }
            && tags.All(tag => !string.IsNullOrWhiteSpace(tag))
            && tags.Distinct(StringComparer.Ordinal).Count() == tags.Count;

    private static IReadOnlySet<string> ControlledFriendlyUnitTags(MatchState state, string controller)
        => GetControlledFieldUnitObjectIds(state.PlayerZones, state.CardObjects, controller)
            .Select(id => state.CardObjects[id]).Where(card => IsFaceUpNonStandbyUnit(card))
            .SelectMany(card => card.Tags).ToHashSet(StringComparer.Ordinal);

    private static int CountControlledFriendlyUnitTags(MatchState state, string controller,
        IReadOnlyList<string> required)
    {
        var tags = ControlledFriendlyUnitTags(state, controller);
        return required.Distinct(StringComparer.Ordinal).Count(tags.Contains);
    }

    private static bool HasRequiredFriendlyUnitTags(MatchState state, string controller,
        IReadOnlyList<string> required)
        => CountControlledFriendlyUnitTags(state, controller, required)
            == required.Distinct(StringComparer.Ordinal).Count();

    // The Chinese UNL judge FAQ260511 section4 explicitly makes this a
    // resolution condition. Do not capture its truth at conquest or hold.
    private static StackResolutionResult ResolveFriendlyUnitTraitScore(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        if (!IsFriendlyUnitTraitScoreSpec(spec) || !ValidFriendlyUnitTraitScoreStack(item))
            throw new InvalidOperationException("Invalid conditional unit trait score skill.");
        return HasRequiredFriendlyUnitTags(state, item.ControllerId, spec.RequiredFriendlyUnitTagsForScore!)
            ? ResolveUnitEventPointGain(state, item, spec.Amount)
            : NoopStackResolutionResult(state) with {
                Events = [new("UNIT_TRIGGER_RESOLVED", "友方单位属性未齐备，得分技能结算但不获得分数",
                    new Dictionary<string, object?> {
                        ["playerId"] = item.ControllerId, ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(
                            state, item.SourceObjectId, ProjectionViewers.Spectator), ["trigger"] = spec.Effect })] };
    }

    private static bool ValidFriendlyUnitTraitScoreStack(StackItemState item)
        => item.UnitEvent is { } context && IsFriendlyUnitTraitScoreSpec(SelectedUnitEventSpec(context))
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
            && item.TriggerCost is null && item.DamageAmount == 0 && item.EffectRepeatCount == 1
            && item.RepeatExecutions is null && !item.SourceConfirmed && item.Destination.Length == 0;

    private static StackResolutionResult ResolveUnitEventPointGain(MatchState state, StackItemState item, int amount)
    {
        var events = new List<GameEvent>();
        if (TryPreventPointGain(state, item.ControllerId, amount, item.EffectKind, item.SourceObjectId, events))
            return NoopStackResolutionResult(state) with { Events = events };
        var scores = NormalizeScoresForSeats(state).ToDictionary(); scores[item.ControllerId] += amount;
        events.Add(new("SCORE_GAINED", "触发技能获得分数", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(
                state, item.SourceObjectId, ProjectionViewers.Spectator),
            ["amount"] = amount, ["score"] = scores[item.ControllerId] }));
        return NoopStackResolutionResult(state) with { PlayerScores = scores,
            WinnerPlayerId = scores[item.ControllerId] >= EffectiveWinningScore(state) ? item.ControllerId : null, Events = events };
    }
}
