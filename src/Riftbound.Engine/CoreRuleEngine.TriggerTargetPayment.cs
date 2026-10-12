using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string TriggerTargetCostWindow = "TRIGGER_TARGET_COST";

    internal static bool ValidTriggerTargetPayment(MatchState state, PendingPaymentState payment)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == payment.ResolvingStackItemId) is { } item
            && (item.DamageDistribution is not null && ValidDistributionStack(state, item)
                || item.LocationDamage is not null && ValidLocationDamageStack(state,item)
                || item.ReflexiveDuel is not null && ValidReflexiveDuelStack(state,item)
                || item.UnitConquest is not null && ValidConquestDamageStack(state,item)
                || HasHeldTargetConfirmation(item) && ValidHeldTargetStack(state, item)
                || HasLegendConquestTarget(item) && item.TriggerCost is null && ValidLegendConquest(item.LegendConquest!, item.EffectKind, item.CardNo)
                || item.FieldContext is { } field && ValidFieldContext(field, item.EffectKind, item.CardNo)
                || item.ReflexiveCopy is { TargetConfirmed: false } copy && ValidReflexiveCopy(copy, item.EffectKind, item.CardNo))
            && (item.LocationDamage is not null || item.DamageDistribution is not null ? item.TargetObjectIds.Count > 0 : item.TargetObjectIds.Count == 1)
            && item.TargetGenerations is { } generations && generations.Count == item.TargetObjectIds.Count
            && item.TargetObjectIds.All(id => generations.TryGetValue(id, out var generation) && generation >= 0)
            && payment.PaymentId == "TRIGGER-WARD:" + item.StackItemId && payment.PlayerId == item.ControllerId
            && payment.PaymentWindow == TriggerTargetCostWindow && payment.ManaCost == 0 && payment.PowerCost > 0
            && (item.LocationDamage is null && item.DamageDistribution is null || payment.PowerCost == ResolveSpellshieldTargetTaxPower(state, item.ControllerId, item.TargetObjectIds, out _))
            && payment.PowerCostByTrait.Count == 0 && payment.PaymentResourceActionIds.Count == 0
            && payment.LegalPaymentChoiceIds.SequenceEqual(["PAY", "DECLINE"])
            && state.PriorityPlayerId is null && state.PendingCardChoice is null;

    private static ResolutionResult ResolveTriggerTargetPayment(MatchState state, PlayerIntent intent, PendingPaymentState pending,
        IReadOnlyList<string> choices, int rawCount)
    {
        if (!ValidTriggerTargetPayment(state, pending) || rawCount != 1 || choices.Count != 1 || choices[0] is not ("PAY" or "DECLINE"))
            return RejectWithCorePrompts(state, "请选择支付法盾费用或放弃技能。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == pending.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingPayment = null };
        var legal = item.DamageDistribution is not null ? DistributionTargets(state, item)
            : item.LocationDamage is not null ? LocationDamageTargets(state,item)
            : item.ReflexiveDuel is not null ? ReflexiveDuelTargets(state,item)
            : item.UnitConquest is not null ? ConquestDamageTargets(state,item)
            : HasHeldTargetConfirmation(item) ? HeldTargetChoices(state, item)
            : HasLegendConquestTarget(item) ? LegendConquestTargets(state, item)
            : item.FieldContext is not null ? FieldTriggerTargets(state, item) : ReflexiveCopyTargets(state, item);
        if (choices[0] == "DECLINE" || item.TargetObjectIds.Any(id => !legal.Contains(id)
            || item.TargetGenerations![id] != state.CardObjects.GetValueOrDefault(id)?.ObjectGeneration))
            return DiscardUnconfirmedTrigger(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        var plan = new PaymentCostRules.PaymentPlan(pending.PaymentId, pending.PaymentWindow, intent.PlayerId,
            genericPowerCost: pending.PowerCost, totalPowerCost: pending.PowerCost, sourceObjectId: item.SourceObjectId);
        var committed = PaymentCostRules.TryCommitPayment(plan, state.RunePools, state.PlayerExperience);
        if (!committed.Accepted) return RejectWithCorePrompts(state, "符能不足；可先回收符文或放弃此触发技能。", ErrorCodes.InsufficientCost);
        next = next with { RunePools = committed.RunePools, PlayerExperience = committed.PlayerExperience };
        var completed = CompleteTriggerTargetConfirmation(next, item, [new("COST_PAID", "支付法盾费用",
            PaymentCostRules.BuildCostPaidPayload(plan, committed.RunePools, committed.PlayerExperience, new Dictionary<string, object?>()))]);
        return completed.Accepted ? completed : RejectWithCorePrompts(state, completed.ErrorMessage!, ErrorCodes.InvalidTarget);
    }
}
