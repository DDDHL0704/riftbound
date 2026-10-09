using Riftbound.Contracts;

namespace Riftbound.Engine;

public enum RevealedHandAction { Recycle, DiscardDraw, RevealOnly }
public sealed record RevealedHandChoiceSpec(RevealedHandAction Action, string ForbiddenTag = "", int ExperienceCost = 0, bool Optional = false);
public sealed record RevealedHandChoiceContext(string StackItemId, int ExecutionIndex, string OwnerId, IReadOnlyDictionary<string, long> Cards);

public sealed partial class CoreRuleEngine
{
    // Revealing a hand grants temporary identity access, never access to unrelated zones.
    internal static bool IsPubliclyRevealedHandCard(MatchState state, string id)
    {
        var owner = state.PendingCardChoice?.HandContext?.OwnerId ?? state.PendingEffectPlay?.RevealedHand?.OwnerId;
        var cards = state.PendingCardChoice?.HandContext?.Cards ?? state.PendingEffectPlay?.RevealedHand?.Cards;
        return owner is not null && cards is not null && cards.TryGetValue(id, out var generation)
            && state.PlayerZones.TryGetValue(owner, out var zones) && zones.Hand.Contains(id)
            && state.CardObjects.TryGetValue(id, out var card) && card.ObjectGeneration == generation;
    }

    private static string[] RevealedHandChoices(MatchState state, string controller, RevealedHandChoiceSpec spec, IEnumerable<string> hand)
        => spec.Action == RevealedHandAction.RevealOnly || state.PlayerExperience.GetValueOrDefault(controller) < spec.ExperienceCost ? []
            : hand.Where(id => string.IsNullOrEmpty(spec.ForbiddenTag) || !state.CardObjects[id].Tags.Contains(spec.ForbiddenTag)).ToArray();

    private static StackResolutionResult BeginRevealedHandChoice(MatchState state, StackItemState item, RevealedHandChoiceSpec spec)
    {
        // The current platform has exactly two seats, so the opponent is unambiguous.
        var owner = state.Seats.Keys.Single(id => id != item.ControllerId);
        var hand = state.PlayerZones[owner].Hand.Order(StringComparer.Ordinal).ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration);
        var legal = RevealedHandChoices(state, item.ControllerId, spec, hand.Keys);
        var reason = spec.Action == RevealedHandAction.RevealOnly ? "对手已展示手牌。确认后，本回合可查看该对手场上正面朝下的卡牌，并获得1经验。手牌随后恢复隐藏。"
            : spec.Action == RevealedHandAction.Recycle ? "对手已展示手牌：必须选择一张非单位卡牌让其回收；没有可选牌时继续。"
            : $"对手已展示手牌：选择一张牌并确认，将支付 {spec.ExperienceCost} 经验，让对手弃置该牌并抽一张；也可以不选放弃。";
        if (state.PlayerExperience.GetValueOrDefault(item.ControllerId) < spec.ExperienceCost) reason += " 当前经验不足，只能继续。";
        var choice = new PendingCardChoiceState($"HAND-{item.StackItemId}-{item.CompletedHandExecutions}", "REVEALED_HAND_EFFECT", item.ControllerId,
            !spec.Optional && legal.Length > 0 ? 1 : 0, legal.Length > 0 ? 1 : 0, legal, hand.Keys.ToArray(), reason, item.SourceObjectId, item.EffectKind)
            { HandContext = new(item.StackItemId, item.CompletedHandExecutions, owner, hand) };
        return NoopStackResolutionResult(state) with { StackItems = state.StackItems, PendingCardChoice = choice,
            Events = [new("HAND_REVEALED", "对手展示手牌，等待结算中的选择", new Dictionary<string, object?> {
                ["playerId"] = owner, ["sourceObjectId"] = item.SourceObjectId,
                ["cards"] = hand.Keys.Select(id => new { objectId = id, cardNo = state.CardObjects[id].CardNo }).ToArray() })] };
    }

    internal static bool ValidRevealedHandChoice(MatchState state, PendingCardChoiceState choice)
    {
        var top = state.StackItems.LastOrDefault();
        if (choice.ChoiceWindow != "REVEALED_HAND_EFFECT" || choice.HandContext is not { } context || top is null
            || choice.DeckContext is not null || choice.ResolvingStackItemId is not null
            || context.Cards is null || context.StackItemId != top.StackItemId || context.ExecutionIndex != top.CompletedHandExecutions
            || top.CompletedHandExecutions >= top.EffectRepeatCount || top.CompletedHandExecutions < 0
            || choice.PlayerId != top.ControllerId || choice.SourceObjectId != top.SourceObjectId || choice.EffectKind != top.EffectKind
            || choice.ChoiceId != $"HAND-{top.StackItemId}-{top.CompletedHandExecutions}"
            || !TryGetRevealedHandChoiceSpec(top, out var spec)
            || !state.Seats.ContainsKey(context.OwnerId) || context.OwnerId == choice.PlayerId
            || !state.PlayerZones.TryGetValue(context.OwnerId, out var zones)
            || !context.Cards.Keys.Order(StringComparer.Ordinal).SequenceEqual(zones.Hand.Order(StringComparer.Ordinal))
            || !context.Cards.Keys.SequenceEqual(choice.ContextObjectIds)
            || context.Cards.Any(kv => !state.CardObjects.TryGetValue(kv.Key, out var card) || card.ObjectGeneration != kv.Value)) return false;
        var legal = RevealedHandChoices(state, choice.PlayerId, spec, choice.ContextObjectIds);
        return legal.SequenceEqual(choice.LegalObjectIds) && choice.RequiredCount == (!spec.Optional && legal.Length > 0 ? 1 : 0)
            && choice.MaxCount == (legal.Length > 0 ? 1 : 0);
    }

    private static ResolutionResult ResolveRevealedHandChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidRevealedHandChoice(state, choice))
            return RejectWithCorePrompts(state, "展示手牌的选择上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Last();
        TryGetRevealedHandChoiceSpec(item, out var spec);
        var owner = choice.HandContext!.OwnerId;
        var zones = NormalizeZonesForSeats(state);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        var experience = NormalizeExperienceForSeats(state);
        var effects = state.UntilEndOfTurnEffects;
        var scores = state.PlayerScores;
        var rng = state.RngCursor;
        var winner = state.WinnerPlayerId;
        var events = new List<GameEvent>();
        if (selected.Count > 0)
        {
            if (spec.ExperienceCost > 0)
            {
                experience[item.ControllerId] -= spec.ExperienceCost;
                events.Add(new("EXPERIENCE_SPENT", "结算中支付经验", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["amount"] = spec.ExperienceCost, ["sourceObjectId"] = item.SourceObjectId }));
            }
            if (spec.Action == RevealedHandAction.Recycle)
            {
                var recycled = RecycleTargetCards(state, zones, item.ControllerId, item.SourceObjectId, selected);
                events.AddRange(recycled.Events); rng = recycled.RngCursor;
            }
            else
            {
                if (!TryDiscardCardFromHand(zones, cards, owner, selected[0]))
                    return RejectWithCorePrompts(state, "所选卡牌已不在对手手牌中。", ErrorCodes.InvalidTarget);
                events.Add(new("CARD_DISCARDED", "对手弃置展示的卡牌", new Dictionary<string, object?> {
                    ["playerId"] = owner, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = selected[0],
                    ["discardedByPlayerId"] = item.ControllerId, ["destinationZone"] = "GRAVEYARD" }));
                ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, owner, "CARD_DISCARDED", item.SourceObjectId, selected, events);
                effects = MarkPlayerDiscardedHandCardsThisTurn(effects, owner, selected);
                var drawn = ApplyDrawToPlayer(state, zones, scores, owner, 1, rng, events);
                scores = drawn.PlayerScores; rng = drawn.RngCursor; winner = drawn.WinnerPlayerId ?? winner;
            }
        }
        events.Add(new("CARD_CHOICE_RESOLVED", "展示手牌选择完成", new Dictionary<string, object?> {
            ["choiceId"] = choice.ChoiceId, ["playerId"] = item.ControllerId, ["chosenCount"] = selected.Count }));
        var next = state with { PendingCardChoice = null, PlayerZones = zones, CardObjects = cards, PlayerExperience = experience,
            UntilEndOfTurnEffects = effects, PlayerScores = scores, WinnerPlayerId = winner, RngCursor = rng,
            Status = winner is null ? state.Status : MatchStatuses.Finished,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(item with { CompletedHandExecutions = item.CompletedHandExecutions + 1 }).ToArray() };
        if (item.DeathRevealContext is not null) next = GrantFaceDownLook(next, item, owner, events);
        var resumed = ResolvePassPriority(next, new("hand-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }
}
