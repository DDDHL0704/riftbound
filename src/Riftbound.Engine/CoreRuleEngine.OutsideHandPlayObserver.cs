using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OutsideHandPlayObserverContext(ObjectBinding Source, ObjectBinding Played, string CardNo,
    string ControllerId, string PlayId, long CapturedTick, long FaceRevision, string RuleTextInstanceId,
    string? SourceTokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TurnPlayerId = null);

public sealed partial class CoreRuleEngine
{
    internal const string OutsideHandPlayObserverEffect = "CONTROLLER_OUTSIDE_HAND_PLAY_EMPOWER_SOURCE";
    internal const string OpponentTurnPlayTokenEffect = "CONTROLLER_OPPONENT_TURN_PLAY_CREATE_MINION";

    private sealed record OutsideHandPlayObserverCandidate(ObjectBinding Source, string CardNo, string ControllerId,
        long FaceRevision, string RuleTextInstanceId, string? SourceTokenFactoryCardNo, RuleTextInstanceBinding? Origin,
        string Effect)
    {
        internal OutsideHandPlayObserverContext ToContext(CardPlayHistoryEntry play, long tick, string turnPlayer)
            => new(Source, play.Source, CardNo, ControllerId, play.PlayId, tick, FaceRevision,
                RuleTextInstanceId, SourceTokenFactoryCardNo, Origin,
                Effect == OpponentTurnPlayTokenEffect ? turnPlayer : null);
    }

    private static readonly Lazy<HashSet<string>> OutsideHandPlayObserverFaces = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => RuleTextParser.Parse(card).Triggers.Any(t => t.Kind == TriggerKinds.ControllerOutsideHandPlayEmpowerSource))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));
    private static readonly Lazy<HashSet<string>> OpponentTurnPlayObserverFaces = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => RuleTextParser.Parse(card).Triggers.Any(t => t.Kind == TriggerKinds.ControllerOpponentTurnPlayCreateMinion))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    // Capture when the play completes, not at declaration or after the rest of
    // the enclosing effect: either can change which rule-text instances exist.
    private static OutsideHandPlayObserverCandidate[] CaptureOutsideHandPlayObservers(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, string player)
    {
        if (!zones.TryGetValue(player, out var own)) return [];
        return own.Base.Concat(own.Battlefields).Concat(own.LegendZone).Distinct(StringComparer.Ordinal)
            .Where(id => cards.TryGetValue(id, out var source) && !source.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(source, player))
            .Order(StringComparer.Ordinal)
            .SelectMany(id => AttachedRuleTextRules.Instances(zones, cards, cards[id])
                .SelectMany(instance => new[] {
                    OutsideHandPlayObserverFaces.Value.Contains(instance.CardNo) ? OutsideHandPlayObserverEffect : null,
                    OpponentTurnPlayObserverFaces.Value.Contains(instance.CardNo) && cards[id].Tags.Contains(CardObjectTags.UnitCard)
                        && (own.Base.Contains(id) || own.Battlefields.Contains(id)) ? OpponentTurnPlayTokenEffect : null }
                    .Where(effect => effect is not null)
                    .Select(effect => new OutsideHandPlayObserverCandidate(new(id, cards[id].ObjectGeneration),
                        instance.CardNo, player, instance.Origin?.FaceRevision ?? cards[id].CopyFaceTransitions?.Count ?? 0,
                        instance.Id, cards[id].TokenFactoryCardNo, instance.Origin, effect!))))
            .ToArray();
    }

    internal static bool ValidOutsideHandPlayObserver(MatchState state, OutsideHandPlayObserverContext? context,
        string effect, string sourceId, string controller, string? cardNo)
    {
        if (context is null) return effect is not (OutsideHandPlayObserverEffect or OpponentTurnPlayTokenEffect);
        var opponentTurn = effect == OpponentTurnPlayTokenEffect;
        if (effect is not (OutsideHandPlayObserverEffect or OpponentTurnPlayTokenEffect) || context.CardNo != cardNo
            || !(opponentTurn ? OpponentTurnPlayObserverFaces.Value : OutsideHandPlayObserverFaces.Value).Contains(context.CardNo)
            || context.Source is null || context.Played is null || context.Source.ObjectId != sourceId
            || context.Source.Generation < 0 || context.Played.Generation < 0
            || string.IsNullOrWhiteSpace(context.Played.ObjectId) || context.ControllerId != controller
            || !state.Seats.ContainsKey(controller) || context.CapturedTick < 0 || context.CapturedTick > state.Tick
            || context.FaceRevision < 0 || string.IsNullOrWhiteSpace(context.PlayId)
            || (opponentTurn ? context.TurnPlayerId != state.TurnPlayerId || controller == context.TurnPlayerId
                : context.TurnPlayerId is not null)
            || !state.CardPlays.Resolved.Any(play => play.PlayId == context.PlayId && play.PlayerId == controller
                && play.Source == context.Played && play.Tick == context.CapturedTick
                && (opponentTurn || CompletedPlayFromOutsideHand(state.CardPlays.Finalized.FirstOrDefault(p => p.PlayId == play.PlayId), play)))) return false;
        return context.Origin is null
            ? context.RuleTextInstanceId == $"PRINTED:{context.Source.ObjectId}:{context.Source.Generation}"
                && PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision,
                    context.CapturedTick, context.SourceTokenFactoryCardNo)
            : context.Origin.Equipment is { } equipment
                && context.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{context.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.Source.Generation, context.CardNo);
    }

    internal static bool ValidOutsideHandPlayTrigger(MatchState state, TriggerQueueItemState item)
        => ValidOutsideHandPlayObserver(state, item.OutsideHandPlayObserver, item.EffectKind,
            item.SourceObjectId, item.ControllerId, item.SourceCardNo)
            && (item.OutsideHandPlayObserver is null || item.TriggeredByEventKind == "CARD_PLAY_COMPLETED"
                && item.TriggerId.StartsWith($"outside-hand-play-{item.OutsideHandPlayObserver.CapturedTick}-", StringComparison.Ordinal));

    internal static bool ValidOutsideHandPlayStack(MatchState state, StackItemState item)
        => ValidOutsideHandPlayObserver(state, item.OutsideHandPlayObserver, item.EffectKind,
            item.SourceObjectId, item.ControllerId, item.CardNo)
            && (item.OutsideHandPlayObserver is null || item.StackItemId.StartsWith($"ordered-outside-hand-play-{item.OutsideHandPlayObserver.CapturedTick}-", StringComparison.Ordinal)
                && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
                && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed);

    private static StackResolutionResult ResolveOutsideHandPlayObserver(MatchState state, StackItemState item)
    {
        if (!ValidOutsideHandPlayStack(state, item)) throw new InvalidOperationException("Invalid captured card play observer.");
        if (item.EffectKind == OpponentTurnPlayTokenEffect)
            return CreateUnitTokenBatch(state, item, P6TokenFactoryCatalog.ZaunMinionTokenCardNo,
                1 + AdditionalUnitTokens(item), false, abilityId: OpponentTurnPlayTokenEffect);
        return ApplyEmpowerAction(state, item.OutsideHandPlayObserver!.Source, item.ControllerId);
    }
}
