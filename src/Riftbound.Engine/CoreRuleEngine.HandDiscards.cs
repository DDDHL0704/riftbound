using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string DiscardReplacementPlayMode = "DISCARD_REPLACEMENT";
    private const string FlameChompersDiscardReplacement = "OGN_FLAME_CHOMPERS_DISCARD_ALT_PLAY_UNIT";
    private static readonly IReadOnlyDictionary<string, int> RedDiscardPlayCost = new Dictionary<string, int> { [RuneTrait.Red] = 1 };
    private static readonly Lazy<IReadOnlyDictionary<string, CardBehaviorDefinition>> DiscardReplacementPlays = new(() =>
        CardBehaviorRegistry.GetAll().Where(behavior => behavior.EffectKind == FlameChompersDiscardReplacement)
            .ToDictionary(behavior => behavior.CardNo, StringComparer.Ordinal));

    private enum HandDiscardOutcome { Invalid, Discarded, Replaced }
    private readonly record struct HandDiscardResult(string[] Discarded, int ReplacedCount);

    private static string? HandDiscardPlayer(MatchState state, IReadOnlyDictionary<string, PlayerZones> zones, string id)
        => state.ObjectLocations.TryGetValue(id, out var location)
            && location.PlayerId is { } player && zones.TryGetValue(player, out var hand) && hand.Hand.Contains(id, StringComparer.Ordinal)
            ? player : zones.FirstOrDefault(pair => pair.Value.Hand.Contains(id, StringComparer.Ordinal)).Key;

    private static HandDiscardOutcome ResolveSingleHandDiscard(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player, string id, List<GameEvent> events)
    {
        var result = ResolveHandDiscards(state, zones, cards, pools, player, [id], events);
        return result.Discarded.Length != 0 ? HandDiscardOutcome.Discarded
            : result.ReplacedCount != 0 ? HandDiscardOutcome.Replaced : HandDiscardOutcome.Invalid;
    }

    private static HandDiscardResult ResolveHandDiscards(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, string player, IReadOnlyList<string> ids, List<GameEvent> events)
    {
        if (ids.Count == 0) return new([], 0);
        return ResolveHandDiscards(state, zones, cards, pools, [(player, ids)], events)[player];
    }

    private static Dictionary<string, HandDiscardResult> ResolveHandDiscards(
        MatchState state, Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, IEnumerable<(string Player, IReadOnlyList<string> Ids)> selections,
        List<GameEvent> events)
    {
        var batches = selections.Select(selection =>
        {
            var hand = zones[selection.Player].Hand;
            return (selection.Player, Ids: selection.Ids.Where(id => (ReferenceEquals(selection.Ids, hand) || hand.Contains(id, StringComparer.Ordinal))
                && IsCardObjectControlledByPlayerOrLegacyOwned(cards, selection.Player, id))
                .Distinct(StringComparer.Ordinal).ToArray());
        }).ToArray();
        var outcomes = new Dictionary<string, HandDiscardResult>(StringComparer.Ordinal);
        // CN 373.1.a: finish replacements in turn order before moving any of
        // the unreplaced cards in the same simultaneous instruction.
        Dictionary<string, ObjectLocationState>? locations = null;
        foreach (var (player, legal) in batches.OrderBy(batch => batch.Player == state.TurnPlayerId ? 0 : 1))
        {
            var replaced = ResolveHandDiscardReplacements(state, zones, cards, pools, player, legal, events, ref locations);
            outcomes.Add(player, new(legal.Where(id => !replaced.Contains(id)).ToArray(), replaced.Count));
        }
        foreach (var (player, _) in batches)
        {
            var discarded = outcomes[player].Discarded;
            if (discarded.Length == 0) continue;
            var hand = zones[player];
            var discardedSet = discarded.ToHashSet(StringComparer.Ordinal);
            zones[player] = AddNonDeckGraveyardEntries(zones, cards, player,
                hand with { Hand = hand.Hand.Where(id => !discardedSet.Contains(id)).ToArray() }, discarded);
            foreach (var id in discarded) AdvanceNonFieldObjectIdentity(cards, id);
        }
        return outcomes;
    }

    private static HashSet<string> ResolveHandDiscardReplacements(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, string player, IReadOnlyList<string> legal, List<GameEvent> events,
        ref Dictionary<string, ObjectLocationState>? locations)
    {
        var replaced = new HashSet<string>(StringComparer.Ordinal);
        var candidates = legal.Where(id => cards.TryGetValue(id, out var card)
            && card.CardNo is { } no && DiscardReplacementPlays.Value.ContainsKey(no)).ToList();
        // CN 370.1.c.1, 373: decide replacements before moving the rest of a discard batch.
        while (candidates.Count != 0)
        {
            var context = state with { PlayerZones = zones, CardObjects = cards, RunePools = pools };
            var pool = pools.GetValueOrDefault(player, RunePool.Empty);
            var canPayFromPool = PaymentCostRules.CanPayPowerCost(pool, 0, RedDiscardPlayCost, PowerPaymentPurpose.PlayUnit);
            var redRunes = pool.HasSupportedAmounts && pool.TotalStoredPower < int.MaxValue
                && pool.PaidPowerThisTurn < long.MaxValue
                ? zones[player].Base.Where(id => IsRecyclableRuneResource(cards, player, id)
                    && TryGetRuneTrait(cards[id], out var trait) && trait == RuneTrait.Red).ToArray() : [];
            if (!canPayFromPool && redRunes.Length == 0) break;

            locations ??= ReconcileObjectLocations(state.ObjectLocations, zones);
            var preview = context with { ObjectLocations = locations };
            var id = candidates.Count == 1 ? candidates[0] : RuleChoices.Value!.Choose(player,
                "选择处理弃置替换效果的顺序。",
                candidates.Select(cardId => new RuleChoiceOption(cardId, DeckChoiceLabel(context, cardId), [cardId])).ToArray(),
                kind: "HAND_DISCARD_REPLACEMENT_ORDER", preview: preview);
            if (TryResolveDiscardReplacementPlay(preview, zones, cards, pools, locations, player, id, canPayFromPool, redRunes, events))
                replaced.Add(id);
            candidates.Remove(id);
        }

        return replaced;
    }

    private static bool TryResolveDiscardReplacementPlay(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools,
        Dictionary<string, ObjectLocationState> locations, string player, string id, bool canPayFromPool, IReadOnlyList<string> redRunes, List<GameEvent> events)
    {
        var card = cards[id];
        var behavior = DiscardReplacementPlays.Value[card.CardNo!];
        const string payFromPool = "PAY_RED";
        const string recycleAndPay = "PAY_RED:";
        var options = new List<RuleChoiceOption>();
        if (canPayFromPool) options.Add(new(payFromPool, "支付 1 点红色符能，改为打出到基地"));
        foreach (var rune in redRunes)
            options.Add(new(recycleAndPay + rune, "回收炽烈符文并支付 1 点红色符能，改为打出到基地", [rune]));
        options.Add(new(DeclinePaymentChoiceId, "放弃替代，正常弃置"));

        // Each choice is a complete, server-priced action. The existing rule
        // choice prompt sends exactly one ID, including when selecting a rune.
        var selected = RuleChoices.Value!.Choose(player, "选择是否替换弃牌。",
            options, kind: "FLAME_CHOMPERS_DISCARD_REPLACEMENT", preview: state,
            privateReason: $"弃置{behavior.DisplayName}时，你可以支付 1 点红色符能，改为打出到基地。");
        if (selected == DeclinePaymentChoiceId) return false;

        // Trigger confirmation can already be one tick into the outer
        // command. Bind the play to that command's stable confirmation tick.
        var playTick = RuleChoices.Value!.Origin.Tick + 1;
        var paymentId = PaymentCostRules.BuildPaymentId(playTick, RuleChoiceWindow, player, id,
            reason: behavior.EffectKind);
        var recycled = selected == payFromPool ? Array.Empty<string>() : [selected[recycleAndPay.Length..]];
        var resourceIds = recycled.Select(rune => RecycleRunePaymentOptionalCostPrefix + rune).ToArray();
        var plan = new PaymentCostRules.PaymentPlan(paymentId, RuleChoiceWindow, player, totalPowerCost: 1,
            powerCostByTrait: RedDiscardPlayCost, paymentResourceActionIds: resourceIds,
            reason: behavior.EffectKind, sourceObjectId: id, abilityId: behavior.EffectKind, powerPurpose: PowerPaymentPurpose.PlayUnit);
        var available = recycled.Length == 0 ? pools : ApplyRecycleRunePaymentResourceActions(pools, zones, cards,
            locations, player, recycled, events, RuleChoiceWindow, paymentId);
        var payment = PaymentCostRules.TryCommitPayment(plan, available, state.PlayerExperience, state: state);
        if (!payment.Accepted) throw new InvalidOperationException(payment.ErrorMessage);
        pools[player] = payment.RunePools[player];

        var item = new StackItemState($"STACK-{playTick}-{id}", player, id, behavior.EffectKind,
            behavior.CardNo, [], behavior.DamageAmount, 1, [], destination: MoveUnitBaseZone,
            timingContext: StackTimingContextForNewStackItem(state)) { PlayedFromHand = true };
        // The card moves straight from hand to base. Only actual discards
        // publish discard observations or advance the card into the graveyard.
        var hand = zones[player];
        zones[player] = hand with { Hand = RemoveFromZone(hand.Hand, id) };
        var played = new GameEvent("CARD_PLAYED", $"{player} 打出{behavior.DisplayName}（弃置替代）",
            new Dictionary<string, object?> {
                ["playerId"] = player, ["sourceObjectId"] = id, ["cardNo"] = behavior.CardNo,
                ["mode"] = DiscardReplacementPlayMode, ["destination"] = MoveUnitBaseZone,
                ["replacementEffectKind"] = behavior.EffectKind });
        events.Add(CaptureCardPlayHistory(played, card, player, item.StackItemId,
            resolved: false, zone: "STACK", zones: zones, cards: cards));
        events.Add(new("COST_PAID", $"{player} 支付{behavior.DisplayName}替代费用",
            PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience,
                new Dictionary<string, object?> { ["mana"] = 0, ["power"] = 1, ["powerByTrait"] = RedDiscardPlayCost,
                    ["replacement"] = true, ["recycledRuneObjectIds"] = recycled })));
        PlaySourceUnitToBase(zones, cards, behavior, item, state.TurnNumber, state.PlayerExperience,
            state.DestroyedUnitOwnerIdsThisTurn, state.UntilEndOfTurnEffects, events, state.PlayerScores);
        locations[id] = new(player, MoveUnitBaseZone);
        return true;
    }

}
