using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    public PlayCostQuoteDto PreviewPlayCard(MatchState state, string playerId, PlayCostPreviewRequestDto request)
    {
        state = RecomputeRuleDerivedKeywords(state);
        if (state.Status != MatchStatuses.InProgress || state.PendingPayment is not null
            || state.PendingHandChoice is not null || state.PendingCardChoice is not null
            || ResolutionResult.HasBlockingPendingTaskQueue(state))
            return PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.PhaseNotAllowed, "请先完成当前对局步骤。");

        if (CardBehaviorRegistry.TryGetByCardNoAndMode(request.Command.CardNo, request.Command.Mode, out var behavior)
            && CardPermissionKeywordRules.IsSpellPlayProhibited(state, playerId, behavior))
            return PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.PhaseNotAllowed,
                CardPermissionKeywordRules.SpellPlayProhibitionReason);

        var accepted = TryBuildPlayCardPlan(state, new("COST_PREVIEW", playerId, CommandTypes.PlayCard),
            request.Command, out var plan, out var rejection, includeRejectionProjections: false);
        if (plan is null)
            return PlayCostQuoteDto.Rejected(request, state.Tick, rejection.ErrorCode ?? ErrorCodes.InvalidTarget,
                "当前选择无法打出，请检查目标、位置和支付资源。");

        var pool = plan.AvailablePool;
        var availableMana = PaymentCostRules.AvailableMana(pool, plan.Payment.ManaPurpose);
        var missingMana = Math.Max(0, plan.TotalManaCost - availableMana);
        var missingPower = PaymentCostRules.PowerDeficit(pool, plan.AnyPowerCost, plan.PowerCostByTrait);
        var missingExperience = Math.Max(0, plan.TotalExperienceCost - plan.AvailableExperience);
        var remaining = accepted ? PaymentCostRules.PayPowerCost(pool, plan.AnyPowerCost, plan.PowerCostByTrait) : default;
        var adjustments = new List<PlayCostAdjustmentDto>();
        void Adjust(string label, int mana, int power = 0)
        {
            if (mana != 0 || power != 0) adjustments.Add(new(label, mana, power));
        }
        if (EffectPlayIgnoresAllCosts(state, playerId))
            Adjust("效果忽略一切费用", -plan.Behavior.ManaCost, -PrintedPowerCostRules.ForCard(plan.Behavior.CardNo).Amount);
        else if (plan.Behavior.StandbyPlayFrom is not null)
            Adjust("待命忽略基础费用", -plan.Behavior.ManaCost, -PrintedPowerCostRules.ForCard(plan.Behavior.CardNo).Amount);
        else
        {
            if (state.PendingEffectPlay is { IgnoreBaseMana: true } effect && effect.PlayerId == playerId)
                Adjust("效果忽略基础法力", -plan.Behavior.ManaCost);
            if (plan.Behavior.IgnorePrintedPowerCost)
                Adjust("效果忽略基础符能", 0, -PrintedPowerCostRules.ForCard(plan.Behavior.CardNo).Amount);
        }
        Adjust("效果替代基础费用", 0, SourceReplayPowerCost(state, playerId));
        if (plan.Behavior.AppliedFlowCost is { } flow)
            Adjust("流转替代费用", flow.Mana - plan.Behavior.ManaCost,
                flow.Power - PrintedPowerCostRules.ForCard(plan.Behavior.CardNo).Amount);
        Adjust("额外费用", plan.AdditionalManaCost, plan.OptionalPowerCost);
        Adjust("可选额外费用减免", -(plan.OptionalAdditionalReductions?.ManaReduction ?? 0),
            -(plan.OptionalAdditionalReductions?.PowerReduction ?? 0));
        Adjust("卡牌减费", -plan.CostReductionMana);
        Adjust("所选额外费用带来的减免", -plan.OptionalCostManaReduction, -plan.OptionalCostPowerReduction);
        Adjust("回响部分减费", -plan.BattlefieldEchoCostReductionMana);
        Adjust("装备减费", -plan.BattlefieldEquipmentCostReductionMana);
        Adjust("单位静态减费", -plan.DragonUnitCostReductionMana);
        Adjust("下次法术减费", -plan.NextSpellCostReductionMana);
        Adjust("战场法术减费", -plan.BattlefieldSpellCostReductionMana);
        Adjust("战场增费", plan.BattlefieldHeldUnitCostIncreaseMana);
        Adjust("法盾费用", 0, plan.SpellshieldTaxPower);
        var cost = new PlayCostBreakdownDto(plan.Behavior.ManaCost, PrintedPowerCostRules.ForCard(plan.Behavior.CardNo).Amount,
            plan.TotalManaCost, plan.AnyPowerCost, plan.PowerCostByTrait, plan.TotalExperienceCost,
            availableMana, pool.Power, pool.PowerByTrait, missingMana, missingPower, missingExperience,
            accepted ? availableMana - plan.TotalManaCost : null,
            accepted ? remaining.AnyPower : null, accepted ? remaining.PowerByTrait : null, adjustments) {
                AvailableSpellOnlyMana = plan.Payment.ManaPurpose == ManaPaymentPurpose.PlaySpell ? pool.SpellOnlyMana : 0,
                RemainingSpellOnlyMana = !accepted ? null : plan.Payment.ManaPurpose == ManaPaymentPurpose.PlaySpell
                    ? Math.Max(0, pool.SpellOnlyMana - plan.TotalManaCost) : 0 };
        return new(request.RequestId, request.PromptId, state.Tick, true, accepted,
            accepted ? "费用已核对，可以确认打出。" : "所选资源不足，请补充资源或调整额外费用。",
            accepted ? null : rejection.ErrorCode, cost);
    }
}
