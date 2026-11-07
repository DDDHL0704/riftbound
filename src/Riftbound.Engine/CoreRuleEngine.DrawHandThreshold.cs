using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record DrawHandThresholdSpec(int MinimumDrawCount, int MinimumOtherHandCount, int OpponentScore);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrawHandThresholdContext(ObjectBinding Source, string CardNo, string ControllerId,
    int DrawnCount, int OtherHandCount, long CapturedTick, string DrawId, long FaceRevision,
    string RuleTextInstanceId, string? SourceTokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null)
{
    public string? OpponentId { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string DrawHandThresholdEffect = TriggerKinds.ControllerDrawHandThresholdDiscardDestroyScore;
    internal const string DrawHandThresholdOpponentWindow = "DRAW_HAND_THRESHOLD_OPPONENT";

    private static DrawHandThresholdSpec? DrawHandThresholdDefinition(string? cardNo)
        => cardNo is not null && CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior)
            ? behavior.ControllerDrawHandThreshold : null;

    private static DrawHandThresholdContext[] CaptureDrawHandThresholdSources(MatchState state, string player,
        int drawnCount, int otherHandCount)
    {
        if (!state.PlayerZones.TryGetValue(player, out var own)) return [];
        return own.Base.Concat(own.Battlefields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Where(id => state.CardObjects.TryGetValue(id, out var c) && !c.IsFaceDown
                && EffectiveFieldControllerId(state.PlayerZones, id, c) == player)
            .SelectMany(id => AttachedRuleTextRules.Instances(state.PlayerZones, state.CardObjects, state.CardObjects[id])
                .Where(instance => DrawHandThresholdDefinition(instance.CardNo) is { } spec
                    && drawnCount >= spec.MinimumDrawCount && otherHandCount >= spec.MinimumOtherHandCount)
                .Select(instance => new DrawHandThresholdContext(new(id, state.CardObjects[id].ObjectGeneration),
                    instance.CardNo, player, drawnCount, otherHandCount, state.Tick, "",
                    instance.Origin?.FaceRevision ?? state.CardObjects[id].CopyFaceTransitions?.Count ?? 0,
                    instance.Id, state.CardObjects[id].TokenFactoryCardNo, instance.Origin))).ToArray();
    }

    internal static bool ValidDrawHandThresholdContext(MatchState state, DrawHandThresholdContext? c,
        string effect, string source, string controller, string? cardNo)
    {
        if (c is null) return effect != DrawHandThresholdEffect;
        if (effect != DrawHandThresholdEffect || c.CardNo != cardNo || c.Source is not { Generation: >= 0 }
            || c.Source.ObjectId != source || string.IsNullOrWhiteSpace(source) || c.ControllerId != controller
            || !state.Seats.ContainsKey(controller) || c.CapturedTick <= 0 || c.CapturedTick > state.Tick
            || c.FaceRevision < 0 || string.IsNullOrWhiteSpace(c.DrawId)
            || DrawHandThresholdDefinition(c.CardNo) is not { MinimumDrawCount: > 0, MinimumOtherHandCount: > 0, OpponentScore: > 0 } spec
            || c.DrawnCount < spec.MinimumDrawCount || c.OtherHandCount < spec.MinimumOtherHandCount
            || c.OpponentId is not null && (c.OpponentId == controller || !state.Seats.ContainsKey(c.OpponentId))) return false;
        return c.Origin is null
            ? c.RuleTextInstanceId == $"PRINTED:{source}:{c.Source.Generation}"
                && PredictBindingMatches(state, c.Source, c.CardNo, c.FaceRevision, c.CapturedTick, c.SourceTokenFactoryCardNo)
            : c.Origin.Equipment is { } equipment
                && c.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{c.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, c.Origin, source, c.Source.Generation, c.CardNo);
    }

    internal static string DrawHandThresholdTriggerId(DrawHandThresholdContext c)
        => $"draw-hand-threshold-{c.DrawId}-{c.Source.ObjectId}-{c.RuleTextInstanceId}";

    internal static bool ValidDrawHandThreshold(MatchState state, TriggerQueueItemState item)
        => ValidDrawHandThresholdContext(state, item.DrawHandThreshold, item.EffectKind, item.SourceObjectId,
            item.ControllerId, item.SourceCardNo)
            && (item.DrawHandThreshold is not { } c || c.OpponentId is null
                && item.TriggerId == DrawHandThresholdTriggerId(c) && item.EnqueuedAtTick == c.CapturedTick
                && item.TriggeredByEventKind == "CARD_DRAWN");

    internal static bool ValidDrawHandThreshold(MatchState state, StackItemState item)
        => ValidDrawHandThresholdContext(state, item.DrawHandThreshold, item.EffectKind, item.SourceObjectId,
            item.ControllerId, item.CardNo)
            && (item.DrawHandThreshold is not { } c || item.StackItemId == "ordered-" + DrawHandThresholdTriggerId(c)
                && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
                && !item.SourceConfirmed && item.DamageAmount == 0 && item.EffectRepeatCount == 1);

    // Both score-producing skills confirm an opponent before opening responses.
    private static ResolutionResult PrepareCapturedOpponentChoice(ResolutionResult result, StackItemState item,
        string window, string prefix, string reason)
    {
        var state = result.State;
        var legal = SeatPlayerIds(state).Where(id => id != item.ControllerId).ToArray();
        var choice = new PendingCardChoiceState(prefix + item.StackItemId, window, item.ControllerId,
            1, 1, legal, [item.SourceObjectId], reason, item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
        state = state with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidDrawHandThresholdChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        return item is { DrawHandThreshold.OpponentId: null } && ValidDrawHandThreshold(state, item)
            && choice.ChoiceWindow == DrawHandThresholdOpponentWindow
            && choice.ChoiceId == "DRAW-HAND:" + item.StackItemId && choice.EffectKind == DrawHandThresholdEffect
            && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId
            && choice.RequiredCount == 1 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(SeatPlayerIds(state).Where(id => id != item.ControllerId))
            && choice.ContextObjectIds.SequenceEqual([item.SourceObjectId])
            && state.PriorityPlayerId is null && state.ActivePlayerId == item.ControllerId;
    }

    private static ResolutionResult ResolveDrawHandThresholdOpponentChoice(MatchState state,
        PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (!ValidDrawHandThresholdChoice(state, choice) || item?.DrawHandThreshold is not { OpponentId: null } context
            || selected.Count != 1 || !choice.LegalObjectIds.Contains(selected[0], StringComparer.Ordinal))
            return RejectWithCorePrompts(state, "抽牌技能的对手选择与原技能不匹配。", ErrorCodes.InvalidTarget);
        var confirmed = item with { DrawHandThreshold = context with { OpponentId = selected[0] } };
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null,
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? confirmed : i).ToArray(),
            ActivePlayerId = state.StackItems[^1].ControllerId, PriorityPlayerId = state.StackItems[^1].ControllerId,
            PassedPriorityPlayerIds = [] };
        return new(true, null, next, [new("DRAW_HAND_OPPONENT_CONFIRMED", "已确认抽牌技能的得分对手",
            new Dictionary<string, object?> { ["stackItemId"] = item.StackItemId, ["controllerId"] = item.ControllerId,
                ["opponentId"] = selected[0], ["sourceObjectId"] = item.SourceObjectId })],
            ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveDrawHandThreshold(MatchState state, StackItemState item)
    {
        if (!ValidDrawHandThreshold(state, item) || item.DrawHandThreshold is not { OpponentId: { } opponent } c)
            throw new InvalidOperationException("Invalid captured draw hand-threshold skill.");
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>(); var effects = state.UntilEndOfTurnEffects;
        TryDiscardPlayerHand(zones, cards, c.ControllerId, out var discarded);
        if (discarded.Count > 0)
        {
            events.Add(new("CARDS_DISCARDED", "抽牌技能弃置全部手牌", new Dictionary<string, object?> {
                ["playerId"] = c.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["count"] = discarded.Count,
                ["objectIds"] = discarded.ToArray(), ["destinationZone"] = "GRAVEYARD" }));
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, c.ControllerId, "CARDS_DISCARDED", item.SourceObjectId, discarded, events);
            effects = MarkPlayerDiscardedHandCardsThisTurn(effects, c.ControllerId, discarded, zones);
        }
        var result = NoopStackResolutionResult(state);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        if (cards.TryGetValue(c.Source.ObjectId, out var source) && source.ObjectGeneration == c.Source.Generation
            && IsObjectOnField(zones, source.ObjectId))
        {
            var current = state with { PlayerZones = zones, CardObjects = cards, UntilEndOfTurnEffects = effects, ObjectLocations = locations };
            var destruction = ResolveFieldDestructions(current, zones, cards, item, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), pools, objectLocations: locations,
                explicitDestroyObjectIds: new HashSet<string> { source.ObjectId }, explicitDestroyReason: "SKILL_EFFECT");
            events.AddRange(destruction.Events); pools = destruction.RunePools.ToDictionary(p => p.Key, p => p.Value);
            result = result with { TriggerQueue = destruction.TriggerQueue, DestroyedUnitOwnerIds = destruction.DestroyedUnitOwnerIds };
        }
        var scores = NormalizeScoresForSeats(state);
        var amount = DrawHandThresholdDefinition(c.CardNo)!.OpponentScore;
        if (!TryPreventPointGain(state, opponent, amount, item.EffectKind, item.SourceObjectId, events, zones, cards, locations))
        {
            scores[opponent] += amount;
            events.Add(new("SCORE_GAINED", "抽牌技能让选定对手获得分数", new Dictionary<string, object?> {
                ["playerId"] = opponent, ["sourceObjectId"] = item.SourceObjectId,
                ["amount"] = amount, ["score"] = scores[opponent], ["reason"] = item.EffectKind }));
        }
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = pools, PlayerScores = scores,
            UntilEndOfTurnEffects = effects, Events = events, ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}
