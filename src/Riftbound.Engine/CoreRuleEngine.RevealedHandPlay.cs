using Riftbound.Contracts;

namespace Riftbound.Engine;

// Selection belongs to the effect controller; the actual play belongs to the hand owner.
public sealed record RevealedHandPlayContext(string OwnerId, string BattlefieldId, long BattlefieldGeneration,
    IReadOnlyDictionary<string, long> Cards, bool Choosing);

public sealed partial class CoreRuleEngine
{
    internal static bool EffectPlayIgnoresAllCosts(MatchState state, string playerId)
        => state.PendingEffectPlay is { IgnoreAllCosts: true } p && p.PlayerId == playerId;

    internal static bool IsRevealedEffectPlayCard(MatchState state, string id)
        => state.PendingEffectPlay?.RevealedHand is { } hand && hand.Cards.TryGetValue(id, out var generation)
            && state.PlayerZones.TryGetValue(hand.OwnerId, out var ownerZones) && ownerZones.Hand.Contains(id)
            && state.CardObjects.TryGetValue(id, out var card) && card.ObjectGeneration == generation;

    internal static bool IsForcedEffectPlayDestination(MatchState state, string playerId, string destination)
        => state.PendingEffectPlay is { RevealedHand: { Choosing: false } hand } p && p.PlayerId == playerId
            && destination == "BATTLEFIELD:" + hand.BattlefieldId
            && BattlefieldLocalRules.Battlefield(state, hand.BattlefieldId) is { } battlefield
            && battlefield.ObjectGeneration == hand.BattlefieldGeneration;

    private static bool IsRevealedHandUnit(MatchState state, string owner, string id)
        => state.CardObjects.TryGetValue(id, out var card) && card.OwnerId == owner && card.ControllerId == owner
            && card.Tags.Contains(CardObjectTags.UnitCard)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var definition) && definition.PlaysSourceToBaseAsUnit;

    private static StackResolutionResult BeginRevealedHandPlay(MatchState state, StackItemState parent, CardBehaviorDefinition behavior)
    {
        var target = parent.TargetObjectIds.SingleOrDefault();
        if (BattlefieldLocalRules.Battlefield(state, target) is not { } battlefield)
            return ResolveStackItemEffect(state, parent with { EffectPlayCompleted = true });
        var owner = state.Seats.Keys.Single(id => id != parent.ControllerId);
        var hand = state.PlayerZones[owner].Hand.Order(StringComparer.Ordinal).ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration);
        if (hand.Count == 0) return ResolveStackItemEffect(state, parent with { EffectPlayCompleted = true });
        var sources = hand.Where(kv => IsRevealedHandUnit(state, owner, kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var pending = new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{parent.StackItemId}", parent.ControllerId,
            parent, "HAND", sources, true, true, 0, "SPECIFIED_BATTLEFIELD", true,
            IgnoreAllCosts: true, RevealedHand: new(owner, target!, battlefield.ObjectGeneration, hand, true));
        return NoopStackResolutionResult(state) with { PendingEffectPlay = pending, Events = [new("HAND_REVEALED", "对手展示手牌，等待效果控制者选择单位",
            new Dictionary<string,object?> { ["playerId"] = owner, ["sourceObjectId"] = parent.SourceObjectId,
                ["cards"] = hand.Keys.Select(id => new { objectId = id, cardNo = state.CardObjects[id].CardNo }).ToArray() })] };
    }

    private static IReadOnlyDictionary<string, ActionPromptDto> BuildRevealedHandPlayPrompts(MatchState state, PendingEffectPlayState pending)
    {
        var hand = pending.RevealedHand!;
        var choice = new PendingCardChoiceState(pending.ChoiceId, "REVEALED_HAND_PLAY", pending.PlayerId, 0,
            pending.Sources.Count > 0 ? 1 : 0, pending.Sources.Keys.ToArray(), hand.Cards.Keys.ToArray(),
            "对手已展示手牌：选择一名单位，让对手打到指定战场；也可以放弃。", pending.Parent.SourceObjectId, pending.Parent.EffectKind);
        var choiceState = state with { PendingCardChoice = choice };
        var own = ActionPromptBuilder.Build(choiceState, pending.PlayerId, true, choice.Reason, [CommandTypes.ChooseCards, CommandTypes.Surrender]);
        own = own with { Candidates = own.Candidates!.Select(c => c.Action != CommandTypes.ChooseCards ? c : c with {
            Label = "选择展示的单位 / 放弃",
            Metadata = new Dictionary<string,object?>(c.Metadata!) {
                ["viewedCardsPublic"] = true,
                ["viewedCards"] = hand.Cards.Keys.Select(id => new ActionPromptChoiceDto(id, DeckChoiceLabel(state,id), "已公开展示")).ToArray()
            } }).ToArray() };
        return state.Seats.Keys.ToDictionary(id => id, id => id == pending.PlayerId ? own
            : ActionPromptBuilder.Build(state, id, false, "手牌已展示，等待对手选择单位或放弃", ["WAIT", CommandTypes.Surrender]));
    }

    private static ResolutionResult ResolveRevealedHandSelection(MatchState state, PlayerIntent intent, GameCommand command, PendingEffectPlayState pending)
    {
        var hand = pending.RevealedHand!;
        if (command is not ChooseCardsCommand choice || choice.ChoiceId != pending.ChoiceId || choice.ChoiceWindow != "REVEALED_HAND_PLAY"
            || choice.ChosenObjectIds is null || choice.ChosenObjectIds.Count > 1
            || choice.ChosenObjectIds.Any(id => !pending.Sources.ContainsKey(id) || !IsRevealedEffectPlayCard(state, id) || !IsRevealedHandUnit(state, hand.OwnerId, id)))
            return RejectWithCorePrompts(state, "请选择本次展示手牌中的合法单位，或不选以放弃。", ErrorCodes.InvalidTarget);
        if (choice.ChosenObjectIds.Count == 0)
            return FinishEffectPlay(state with { Tick = state.Tick + 1 }, intent, pending, []);
        var id = choice.ChosenObjectIds[0];
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = hand.OwnerId, PendingEffectPlay = pending with {
            PlayerId = hand.OwnerId, Optional = false, Sources = new Dictionary<string,long>{{id,pending.Sources[id]}},
            RevealedHand = hand with { Choosing = false } } };
        return new(true, null, next, [new("EFFECT_PLAY_SELECTED", "已选定单位，等待其拥有者正式打出",
            new Dictionary<string,object?> { ["choosingPlayerId"] = intent.PlayerId, ["playerId"] = hand.OwnerId,
                ["sourceObjectId"] = id, ["battlefieldObjectId"] = hand.BattlefieldId })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static ResolutionResult ApplyEffectPlayFollowup(ResolutionResult result, PendingEffectPlayState pending, PlayCardCommand play)
    {
        if (pending.RevealedHand is not { } hand
            || !CardBehaviorRegistry.TryGetByEffectKind(pending.Parent.EffectKind, out var behavior)
            || string.IsNullOrEmpty(behavior.EffectPlayedUnitStatus)
            || BattlefieldLocalRules.AtUnit(result.State, play.SourceObjectId)?.ObjectId != hand.BattlefieldId) return result;
        var cards = new Dictionary<string,CardObjectState>(result.State.CardObjects);
        cards[play.SourceObjectId] = cards[play.SourceObjectId] with {
            UntilEndOfTurnEffects = cards[play.SourceObjectId].UntilEndOfTurnEffects.Append(behavior.EffectPlayedUnitStatus).Distinct().ToArray() };
        return result with { State = result.State with { CardObjects = cards }, Events = result.Events.Append(new GameEvent("STATUS_EFFECT_APPLIED", "效果要求打出的单位获得眩晕",
            new Dictionary<string,object?> { ["sourceObjectId"] = pending.Parent.SourceObjectId, ["targetObjectId"] = play.SourceObjectId, ["effectId"] = behavior.EffectPlayedUnitStatus })).ToArray() };
    }

    internal static bool ValidRevealedHandPlay(MatchState state, PendingEffectPlayState p)
        => p.RevealedHand is { } hand && CardBehaviorRegistry.TryGetByEffectKind(p.Parent.EffectKind, out var definition)
            && definition.CardNo == p.Parent.CardNo && definition.EffectPlaySourceZone == "OPPONENT_HAND"
            && state.Seats.ContainsKey(hand.OwnerId) && state.PlayerZones.TryGetValue(hand.OwnerId, out var ownerZones) && hand.OwnerId != p.Parent.ControllerId
            && state.Seats.ContainsKey(p.Parent.ControllerId) && p.PlayerId == (hand.Choosing ? p.Parent.ControllerId : hand.OwnerId)
            && p.SourceZone == "HAND" && p.IgnoreAllCosts && p.IgnoreBaseMana && p.IgnoreBasePower && p.ManaReduction == 0
            && p.DestinationPolicy == "SPECIFIED_BATTLEFIELD" && p.Optional == hand.Choosing && p.ViewedCardIds is null
            && !p.Parent.EffectPlayCompleted && p.Parent.TargetObjectIds.SequenceEqual([hand.BattlefieldId])
            && BattlefieldLocalRules.Battlefield(state, hand.BattlefieldId)?.ObjectGeneration == hand.BattlefieldGeneration
            && hand.Cards.Count > 0 && hand.Cards.Keys.Order().SequenceEqual(ownerZones.Hand.Order())
            && hand.Cards.All(kv => state.CardObjects.TryGetValue(kv.Key, out var card) && card.ObjectGeneration == kv.Value)
            && (hand.Choosing ? p.Sources.Keys.Order().SequenceEqual(hand.Cards.Keys.Where(id => IsRevealedHandUnit(state, hand.OwnerId,id)).Order()) : p.Sources.Count == 1)
            && p.Sources.All(kv => hand.Cards.GetValueOrDefault(kv.Key, -1) == kv.Value && IsRevealedHandUnit(state, hand.OwnerId,kv.Key));
}
