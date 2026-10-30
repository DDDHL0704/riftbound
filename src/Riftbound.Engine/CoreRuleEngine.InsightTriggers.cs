using Riftbound.Contracts;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public sealed record InsightTriggerContext(string CardNo, string ControllerId, int Count, long SourceGeneration, string Kind,
    string? BattlefieldObjectId = null, string? ReturnFocusPlayerId = null, bool PaymentAccepted = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] PredictGrantOrigin? PredictGrant = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] PrintedPredictEntryOrigin? PrintedPredict = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? SourceTokenFactoryCardNo = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? SourceFaceRevision = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? SourceCapturedTick = null);

public sealed partial class CoreRuleEngine
{
    internal const string InsightTriggerEffect = "INSIGHT_TRIGGER";
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> InsightSourceFamilies = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-211/219", "UNL-062/219", "UNL-079/219", "UNL-136/219", "VEN·047"]));

    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> HeldInsightSpecs = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Select(c => (c.CardNo, Spec: TriggerParser.Parse(c.CardEffect).SingleOrDefault(t => t.Kind == TriggerKinds.UnitBattlefieldHeldInsight)))
            .Where(c => c.Spec is not null).ToDictionary(c => c.CardNo, c => c.Spec!, StringComparer.Ordinal));

    private static TriggerSpec? HeldInsightSpec(string? cardNo) => cardNo is not null ? HeldInsightSpecs.Value.GetValueOrDefault(cardNo) : null;

    private static bool IsInsightSource(string? cardNo, string kind)
        => kind == "EXHAUSTED" ? UnitEventSpec(cardNo) is { Timing: "BECAME_EXHAUSTED", Effect: "INSIGHT" }
            : kind == "HELD" ? HeldInsightSpec(cardNo) is not null : InsightSourceFamilies.Value[kind switch { "SPELL_PLAYED" => "UNL-211/219", "LAST_BREATH" => "UNL-062/219", "DUEL" => "UNL-079/219", "EMPOWERED" => "VEN·047", _ => "UNL-136/219" }]
            .Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), StringComparer.Ordinal);

    internal static bool IsCapturedInsightKind(string? kind) => kind is "LAST_BREATH" or "DUEL" or "EMPOWERED" or "HELD" or "EXHAUSTED";

    private static IReadOnlyList<InsightTriggerContext> CaptureLastBreathInsights(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState unit, string controller)
        => CaptureRuleTextInsights(zones, cards, unit, controller, "LAST_BREATH");

    private static IReadOnlyList<InsightTriggerContext> CaptureRuleTextInsights(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState unit, string controller, string kind)
        => unit.IsFaceDown || !unit.Tags.Contains(CardObjectTags.UnitCard) ? []
            : AttachedRuleTextRules.Instances(zones, cards, unit).Where(i => IsInsightSource(i.CardNo, kind))
                .Select(i => new InsightTriggerContext(i.CardNo, controller, 2, unit.ObjectGeneration, kind,
                    Origin: i.Origin, SourceTokenFactoryCardNo: unit.TokenFactoryCardNo)).ToArray();

    internal static bool ValidInsightRuleTextSource(MatchState state, InsightTriggerContext context, string source)
        => (!IsCapturedInsightKind(context.Kind) || !string.IsNullOrWhiteSpace(source) && (state.CardObjects.TryGetValue(source, out var unit)
                ? unit.ObjectGeneration >= context.SourceGeneration && unit.TokenFactoryCardNo == context.SourceTokenFactoryCardNo
                : context.SourceTokenFactoryCardNo is not null && P6TokenFactoryCatalog.IsTokenFactory(context.SourceTokenFactoryCardNo)))
            && (context.Origin is null || IsCapturedInsightKind(context.Kind)
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.SourceGeneration, context.CardNo))
            && (context.Kind != "EXHAUSTED" || context.SourceCapturedTick is > 0 && context.SourceCapturedTick <= state.Tick
                && context.SourceFaceRevision is >= 0 && (context.Origin is not null || PredictBindingMatches(state,
                    new(source, context.SourceGeneration), context.CardNo, context.SourceFaceRevision.Value,
                    context.SourceCapturedTick.Value, context.SourceTokenFactoryCardNo)));

    internal static bool ValidCeasedInsightSource(MatchState state, InsightTriggerContext? context,
        string source, string effect, string controller, string? cardNo = null)
        => context is { SourceTokenFactoryCardNo: not null } && IsCapturedInsightKind(context.Kind)
            && !string.IsNullOrWhiteSpace(source) && !state.CardObjects.ContainsKey(source) && ValidInsightTrigger(context, effect, controller, cardNo)
            && ValidInsightRuleTextSource(state, context, source);

    internal static bool ValidInsightTrigger(InsightTriggerContext context, string effect, string controller, string? cardNo = null)
        => effect == InsightTriggerEffect && context.ControllerId == controller && context.SourceGeneration >= 0
            && (context.Kind == "EXHAUSTED" ? context.SourceFaceRevision is >= 0 && context.SourceCapturedTick is > 0
                : context.SourceFaceRevision is null && context.SourceCapturedTick is null)
            && (context.SourceTokenFactoryCardNo is null || IsCapturedInsightKind(context.Kind)
                && P6TokenFactoryCatalog.IsTokenFactory(context.SourceTokenFactoryCardNo))
            && (context.Origin is null || IsCapturedInsightKind(context.Kind) && context.Origin.CardNo == context.CardNo
                && context.Origin.Unit is { } unit && unit.Generation == context.SourceGeneration)
            && context.Kind is "SPELL_PLAYED" or "LAST_BREATH" or "DUEL" or "ACTIVATED" or "CONQUEST_LOOK" or "GRANTED_PREDICT" or "PRINTED_PREDICT" or "EMPOWERED" or "HELD" or "EXHAUSTED"
            && context.Count == (context.Kind == "EXHAUSTED" ? UnitEventSpec(context.CardNo)?.Amount ?? 0 : context.Kind == "HELD" ? HeldInsightSpec(context.CardNo)?.RevealCount ?? 0 : context.Kind=="CONQUEST_LOOK" ? ConquestLookSpec(context.CardNo)?.RevealCount ?? 0
                : context.Kind is "SPELL_PLAYED" or "DUEL" or "GRANTED_PREDICT" or "PRINTED_PREDICT" ? 1 : 2) && context.Count>0
            && (context.Kind == "DUEL" ? !string.IsNullOrEmpty(context.BattlefieldObjectId) && !string.IsNullOrEmpty(context.ReturnFocusPlayerId)
                : (context.Kind is "CONQUEST_LOOK" or "HELD" ? !string.IsNullOrWhiteSpace(context.BattlefieldObjectId) : context.BattlefieldObjectId is null)
                    && context.ReturnFocusPlayerId is null && !context.PaymentAccepted)
            && (context.Kind == "GRANTED_PREDICT" ? context.PrintedPredict is null && ValidPredictGrant(context.PredictGrant, context.CardNo)
                : context.Kind == "PRINTED_PREDICT" ? context.PredictGrant is null && ValidPrintedPredict(context.CardNo, context.PrintedPredict)
                : context.PredictGrant is null && context.PrintedPredict is null && (context.Kind=="CONQUEST_LOOK" || IsInsightSource(context.CardNo, context.Kind)))
            && (cardNo is null || cardNo == context.CardNo);

    private static TriggerSpec? ConquestLookSpec(string? cardNo)
        => BattlefieldTriggerSpecRules.TryGetTrigger(cardNo,BattlefieldTriggerSpecRules.IsBattlefieldConquerRevealRecycleTrigger,out var trigger)
            ? trigger : null;

    internal static bool ValidConquestLookSource(MatchState state,InsightTriggerContext? context,string source)
        => context?.Kind!="CONQUEST_LOOK" || context.BattlefieldObjectId==source
            && state.CardObjects.TryGetValue(source,out var card) && card.CardNo==context.CardNo
            && card.ObjectGeneration==context.SourceGeneration && card.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag);

    private static IReadOnlyList<TriggerQueueItemState> CaptureBattlefieldConquestLook(
        IReadOnlyDictionary<string,PlayerZones> zones,IReadOnlyDictionary<string,CardObjectState> cards,
        string player,string battlefield,long tick)
    {
        if(!TryGetBattlefieldCardObject(zones,cards,battlefield,out var source,out var card)
            || ConquestLookSpec(card.CardNo) is not { } spec)return [];
        return [new TriggerQueueItemState($"conquest-look-{tick}-{source}",player,source,InsightTriggerEffect,"BATTLEFIELD_CONQUERED") {
            InsightContext=new(card.CardNo!,player,spec.RevealCount!.Value,card.ObjectGeneration,"CONQUEST_LOOK",source) }];
    }

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
                    if (card.ControllerId is not { Length: > 0 } controller) continue;
                    var instances = AttachedRuleTextRules.Instances(result.State, card).Where(i => IsInsightSource(i.CardNo, "DUEL"));
                    foreach (var (instance, ordinal) in instances.Select((value, ordinal) => (value, ordinal)))
                    {
                        var duelTrigger = new TriggerQueueItemState($"insight-duel-{result.State.Tick}-{index}-{id}{(ordinal == 0 ? "" : "-" + ordinal)}", controller,
                            id, InsightTriggerEffect, "SPELL_DUEL_STARTED", TimingStates.SpellDuelOpen) {
                            InsightContext = new(instance.CardNo, controller, 1, card.ObjectGeneration, "DUEL", field, focus,
                                Origin: instance.Origin, SourceTokenFactoryCardNo: card.TokenFactoryCardNo), SourceCardNo = instance.CardNo };
                        queue.Add(duelTrigger); events.Add(BuildTriggerQueuedEvent(duelTrigger));
                    }
                }
            }
            if (ev.Kind is not ("UNIT_DESTROYED" or "OBJECT_EMPOWERED")
                || !ev.Payload.TryGetValue("insightTriggerContexts", out var raw) || raw is not IReadOnlyList<InsightTriggerContext> contexts
                || !ev.Payload.TryGetValue(ev.Kind == "OBJECT_EMPOWERED" ? "sourceObjectId" : "targetObjectId", out var target) || target is not string source) continue;
            foreach (var (context, ordinal) in contexts.Select((c, i) => (c, i)))
            {
                var prefix = ev.Kind == "OBJECT_EMPOWERED" ? "empower" : "death";
                var trigger = new TriggerQueueItemState($"insight-{prefix}-{result.State.Tick}-{index}-{source}{(ordinal == 0 ? "" : "-" + ordinal)}", context.ControllerId,
                    source, InsightTriggerEffect, ev.Kind) { InsightContext = context, SourceCardNo = context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
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
            && ValidInsightRuleTextSource(state, context, item.SourceObjectId)
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
            var committed = PaymentCostRules.TryCommitPayment(plan, pools, experience, state: state);
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
