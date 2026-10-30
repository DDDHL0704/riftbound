using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string TriggerTargetCostWindow = "TRIGGER_TARGET_COST";

    internal static bool ValidTriggerTargetPayment(MatchState state, PendingPaymentState payment)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == payment.ResolvingStackItemId) is { } item
            && (NeedsLastBreathHereTarget(item) && ValidLastBreathBattlefieldDamageStack(state, item)
                || HasMovementDamage(item) && ValidMovementDamageStack(state, item)
                || item.HighCostPlayObserver is { Paid: false } && ValidHighCostStack(state, item)
                || item.UnitPlayMight is not null && RengarPlayRecoveryRules.ValidStack(state, item)
                || item.CreatedTokenReady is not null && !CreatedTokenReadyHasPayment(item) && ValidCreatedTokenReadyStack(state, item)
                || item.DamageDistribution is not null && ValidDistributionStack(state, item)
                || item.LocationDamage is not null && ValidLocationDamageStack(state,item)
                || item.ReflexiveDuel is not null && ValidReflexiveDuelStack(state,item)
                || item.UnitConquest is not null && ValidConquestDamageStack(state,item)
                || HasHeldTargetConfirmation(item) && ValidHeldTargetStack(state, item)
                || HasLegendConquestTarget(item) && item.TriggerCost is null && ValidLegendConquest(item.LegendConquest!, item.EffectKind, item.CardNo)
                || item.FieldContext is { } field && (field.Kind is BombDestructionKind or EmpoweredBanishKind or AttackMightDamageKind or CombatMightDamageKind or EmpoweredAttackDestroyKind or AttackWeaponDamageKind or DisarmKind or PaidUnitStunKind or PaidUnitDestroyKind || IsStunObserverKind(field.Kind)
                    ? ValidFieldContextState(state, field, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
                    : ValidFieldContext(field, item.EffectKind, item.CardNo))
                || item.ReflexiveCopy is { TargetConfirmed: false } copy && ValidReflexiveCopy(copy, item.EffectKind, item.CardNo)
                || item.PermanentPlayTargetsPending && ValidPermanentPlayTargetStack(state, item))
            && (item.HighCostPlayObserver is not null ? item.TargetObjectIds.Count <= (HighCostPlaySpec(item.CardNo!)!.RuneReadyCount ?? 0)
                : item.CreatedTokenReady is not null || item.LocationDamage is not null || item.DamageDistribution is not null || item.PermanentPlayTargetsPending ? item.TargetObjectIds.Count > 0 : item.TargetObjectIds.Count == 1)
            && item.TargetGenerations is { } generations && generations.Count == item.TargetObjectIds.Count
            && item.TargetObjectIds.All(id => generations.TryGetValue(id, out var generation) && generation >= 0)
            && payment.PaymentId == "TRIGGER-WARD:" + item.StackItemId && payment.PlayerId == item.ControllerId
            && payment.PaymentWindow == TriggerTargetCostWindow
            && payment.ExperienceCost == (item.FieldContext?.Kind == AttackMightDamageKind ? AttackMightDamageSpec(item.FieldContext.CardNo)!.ExperienceCost : 0)
            && (item.FieldContext?.Kind != AttackMightDamageKind || item.FieldContext.AttackMightDamage is { CostPaid: false })
            && (item.FieldContext is { Kind: PaidUnitStunKind, PaidUnitTrigger.CostPaid: false } paid
                ? payment.ManaCost == PaidUnitStunSpec(paid.CardNo)!.ManaCost
                    && payment.PowerCost == ResolveSpellshieldTargetTaxPower(state, item.ControllerId, item.TargetObjectIds, out _)
                : item.FieldContext is { Kind: PaidUnitDestroyKind, PaidUnitTrigger: { CostPaid: false, DiscardCost: not null } }
                    ? payment.ManaCost == 0 && payment.PowerCost > 0
                        && payment.PowerCost == ResolveSpellshieldTargetTaxPower(state, item.ControllerId, item.TargetObjectIds, out _)
                    : payment.ManaCost == 0 && (item.HighCostPlayObserver is not null ? payment.PowerCost >= 0 : payment.PowerCost > 0 || payment.ExperienceCost > 0))
            && (item.HighCostPlayObserver is null && item.CreatedTokenReady is null && item.LocationDamage is null && item.DamageDistribution is null && !item.PermanentPlayTargetsPending && item.UnitPlayMight is null && !HasMovementDamage(item) && !NeedsLastBreathHereTarget(item) && item.FieldContext?.Kind is not (EmpoweredBanishKind or AttackMightDamageKind or CombatMightDamageKind or EmpoweredAttackDestroyKind or AttackWeaponDamageKind or DisarmKind) || payment.PowerCost == ResolveSpellshieldTargetTaxPower(state, item.ControllerId, item.TargetObjectIds, out _))
            && payment.PowerCostByTrait.SequenceEqual(item.HighCostPlayObserver is { } highCost
                && HighCostPlaysSourceFromGraveyard(highCost.CardNo)
                ? new Dictionary<string, int>(StringComparer.Ordinal) { [RuneTrait.Yellow] = 1 }
                : new Dictionary<string, int>(StringComparer.Ordinal))
            && payment.PaymentResourceActionIds.Count == 0
            && payment.LegalPaymentChoiceIds.SequenceEqual(["PAY", "DECLINE"])
            && state.PriorityPlayerId is null && state.PendingCardChoice is null;

    private static ResolutionResult ResolveTriggerTargetPayment(MatchState state, PlayerIntent intent, PendingPaymentState pending,
        IReadOnlyList<string> choices, int rawCount)
    {
        if (!ValidTriggerTargetPayment(state, pending) || rawCount != 1 || choices.Count != 1 || choices[0] is not ("PAY" or "DECLINE"))
            return RejectWithCorePrompts(state, "请选择支付法盾费用或放弃技能。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == pending.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingPayment = null };
        var legal = item.HighCostPlayObserver is not null ? HighCostRuneTargets(state, item)
            : NeedsLastBreathHereTarget(item) ? LastBreathHereTargets(state, item)
            : HasMovementDamage(item) ? MovementDamageTargets(state, item)
            : item.UnitPlayMight is not null ? UnitPlayMightTargets(state, item.ControllerId)
            : item.CreatedTokenReady is not null ? CreatedTokenReadyTargets(state, item)
            : item.PermanentPlayTargetsPending ? PermanentPlayTargetChoices(state, item)
            : item.DamageDistribution is not null ? DistributionTargets(state, item)
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
            baseManaCost: pending.ManaCost, totalManaCost: pending.ManaCost, genericPowerCost: pending.PowerCost,
            totalPowerCost: pending.PowerCost + pending.PowerCostByTrait.Values.Sum(),
            powerCostByTrait: pending.PowerCostByTrait, experienceCost: pending.ExperienceCost, sourceObjectId: item.SourceObjectId);
        var committed = PaymentCostRules.TryCommitPayment(plan, state.RunePools, state.PlayerExperience, state: state);
        if (!committed.Accepted) return RejectWithCorePrompts(state, "资源不足；可先使用资源技能或放弃此触发技能。", ErrorCodes.InsufficientCost);
        next = next with { RunePools = committed.RunePools, PlayerExperience = committed.PlayerExperience };
        if (item.FieldContext is { Kind: PaidUnitStunKind, PaidUnitTrigger: { } paid })
            item = item with { FieldContext = item.FieldContext with { PaidUnitTrigger = paid with { CostPaid = true } } };
        var completed = CompleteTriggerTargetConfirmation(next, item, [new("COST_PAID", "支付触发技能费用",
            PaymentCostRules.BuildCostPaidPayload(plan, committed.RunePools, committed.PlayerExperience,
                item.FieldContext?.Kind is PaidUnitStunKind or PaidUnitDestroyKind ? new Dictionary<string, object?> {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(next, item.SourceObjectId, "__spectator__") }
                    : new Dictionary<string, object?>()))]);
        return completed.Accepted ? completed : RejectWithCorePrompts(state, completed.ErrorMessage!, ErrorCodes.InvalidTarget);
    }
}
