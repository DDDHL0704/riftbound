using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record InsightTriggerContext(string CardNo, string ControllerId, int Count, long SourceGeneration, string Kind,
    string? BattlefieldObjectId = null, string? ReturnFocusPlayerId = null, bool PaymentAccepted = false);

public sealed partial class CoreRuleEngine
{
    internal const string InsightTriggerEffect = "INSIGHT_TRIGGER";
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> InsightSourceFamilies = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-211/219", "UNL-062/219", "UNL-079/219", "UNL-136/219"]));

    private static bool IsInsightSource(string? cardNo, string kind)
        => InsightSourceFamilies.Value[kind switch { "SPELL_PLAYED" => "UNL-211/219", "LAST_BREATH" => "UNL-062/219", "DUEL" => "UNL-079/219", _ => "UNL-136/219" }]
            .Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), StringComparer.Ordinal);

    internal static bool ValidInsightTrigger(InsightTriggerContext context, string effect, string controller, string? cardNo = null)
        => effect == InsightTriggerEffect && context.ControllerId == controller && context.SourceGeneration >= 0
            && context.Kind is "SPELL_PLAYED" or "LAST_BREATH" or "DUEL" or "ACTIVATED"
            && context.Count == (context.Kind is "SPELL_PLAYED" or "DUEL" ? 1 : 2)
            && (context.Kind == "DUEL" ? !string.IsNullOrEmpty(context.BattlefieldObjectId) && !string.IsNullOrEmpty(context.ReturnFocusPlayerId)
                : context.BattlefieldObjectId is null && context.ReturnFocusPlayerId is null && !context.PaymentAccepted)
            && IsInsightSource(context.CardNo, context.Kind) && (cardNo is null || cardNo == context.CardNo);

    private static IReadOnlyList<TriggerQueueItemState> BuildSpellPlayedInsightTriggers(MatchState state, string player,
        CardBehaviorDefinition behavior, StackItemState played, int paidMana)
    {
        if (!IsSpellPlayBehavior(behavior)) return [];
        return BattlefieldLocalRules.ControlledBy(state, player)
            .Where(card => IsInsightSource(card.CardNo, "SPELL_PLAYED")
                && BattlefieldTriggerSpecRules.TryGetTrigger(card.CardNo,
                    BattlefieldTriggerSpecRules.IsBattlefieldHighCostSpellInsightRecycleTrigger, out var trigger)
                && paidMana >= trigger.MinimumPaidMana.GetValueOrDefault())
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
            .Select(card => new TriggerQueueItemState($"insight-play-{played.StackItemId}-{card.ObjectId}", player,
                card.ObjectId, InsightTriggerEffect, "CARD_PLAYED", played.TimingContext)
                { InsightContext = new(card.CardNo!, player, 1, card.ObjectGeneration, "SPELL_PLAYED") }).ToArray();
    }

    // Destruction captures this context before resetting the card outside play.
    // This single handoff covers damage, explicit destruction and destruction costs.
    private static ResolutionResult QueueInsightEventTriggers(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var queue = result.State.TriggerQueue.ToList();
        var events = result.Events.ToList();
        for (var index = 0; index < result.Events.Count; index++)
        {
            var ev = result.Events[index];
            if (ev.Kind == "SPELL_DUEL_STARTED" && ev.Payload.TryGetValue("battlefieldObjectId", out var fieldValue)
                && fieldValue is string field && ev.Payload.TryGetValue("focusPlayerId", out var focusValue) && focusValue is string focus)
            {
                foreach (var id in HeldUnitsAt(result.State, field))
                {
                    var card = result.State.CardObjects[id];
                    if (!IsInsightSource(card.CardNo, "DUEL") || card.ControllerId is not { Length: > 0 } controller) continue;
                    var duelTrigger = new TriggerQueueItemState($"insight-duel-{result.State.Tick}-{index}-{id}", card.ControllerId,
                        id, InsightTriggerEffect, "SPELL_DUEL_STARTED", TimingStates.SpellDuelOpen) {
                        InsightContext = new(card.CardNo!, controller, 1, card.ObjectGeneration, "DUEL", field, focus) };
                    queue.Add(duelTrigger); events.Add(BuildTriggerQueuedEvent(duelTrigger));
                }
            }
            if (ev.Kind != "UNIT_DESTROYED"
                || !ev.Payload.TryGetValue("insightTriggerContext", out var raw) || raw is not InsightTriggerContext context
                || !ev.Payload.TryGetValue("targetObjectId", out var target) || target is not string source) continue;
            var trigger = new TriggerQueueItemState($"insight-death-{result.State.Tick}-{index}-{source}", context.ControllerId,
                source, InsightTriggerEffect, "UNIT_DESTROYED") { InsightContext = context };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveInsightTrigger(MatchState state, StackItemState item)
    {
        if (item.InsightCompleted) return FinishInsight(state, item);
        return BeginInsight(state, item, NoopStackResolutionResult(state), item.InsightContext!.Count);
    }

    internal static bool ValidInsightPayment(MatchState state, PendingPaymentState payment)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == payment.ResolvingStackItemId) is { InsightContext: { Kind: "DUEL", PaymentAccepted: false } context } item
            && ValidInsightTrigger(context, item.EffectKind, item.ControllerId, item.CardNo)
            && !item.InsightCompleted && payment.ResolvingStackItemId == item.StackItemId
            && payment.PaymentId == "INSIGHT-PAY:" + item.StackItemId && payment.PaymentWindow == "INSIGHT_EFFECT"
            && payment.PlayerId == item.ControllerId && payment.ManaCost == 1 && payment.PowerCost == 0
            && payment.PowerCostByTrait.Count == 0 && payment.PaymentResourceActionIds.Count == 0
            && payment.LegalPaymentChoiceIds.SequenceEqual(["PAY", "DECLINE"]);

    private static ResolutionResult ResolveInsightPayment(MatchState state, PlayerIntent intent, PendingPaymentState pending,
        IReadOnlyList<string> choices, int rawCount)
    {
        if (!ValidInsightPayment(state, pending) || choices.Count != 1 || rawCount != 1 || choices[0] is not ("PAY" or "DECLINE"))
            return RejectWithCorePrompts(state, "请选择支付或放弃。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == pending.ResolvingStackItemId);
        var events = new List<GameEvent>();
        var pools = state.RunePools;
        var experience = state.PlayerExperience;
        var accepted = choices[0] == "PAY";
        if (accepted)
        {
            var plan = new PaymentCostRules.PaymentPlan(pending.PaymentId, pending.PaymentWindow, intent.PlayerId,
                baseManaCost: 1, totalManaCost: 1, reason: "黛安娜的可选洞察费用", sourceObjectId: item.SourceObjectId);
            var committed = PaymentCostRules.TryCommitPayment(plan, pools, experience);
            if (!committed.Accepted)
                return RejectWithCorePrompts(state, "法力不足，可以先横置符文或使用反应资源技能。", ErrorCodes.InsufficientCost);
            pools = committed.RunePools; experience = committed.PlayerExperience;
            events.Add(new("COST_PAID", "支付黛安娜洞察费用", PaymentCostRules.BuildCostPaidPayload(plan, pools, experience, new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId })));
        }
        else events.Add(new("TRIGGER_PAYMENT_DECLINED", "放弃黛安娜的可选洞察", new Dictionary<string, object?> {
            ["playerId"] = intent.PlayerId, ["sourceObjectId"] = item.SourceObjectId }));
        var next = state with { Tick = state.Tick + 1, PendingPayment = null, RunePools = pools, PlayerExperience = experience,
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId
                ? i with { InsightContext = i.InsightContext! with { PaymentAccepted = true } } : i).ToArray() };
        var result = new ResolutionResult(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
        if (!accepted) return DiscardUnconfirmedTrigger(result, item);
        next = RestoreAfterTriggerConfirmation(next, item);
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }
}
