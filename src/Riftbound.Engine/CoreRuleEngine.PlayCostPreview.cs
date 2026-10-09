using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    public PlayCostQuoteDto PreviewPlayCard(MatchState state, string playerId, PlayCostPreviewRequestDto request)
    {
        if (state.Status != MatchStatuses.InProgress || state.PendingPayment is not null
            || state.PendingHandChoice is not null || state.PendingCardChoice is not null
            || ResolutionResult.HasBlockingPendingTaskQueue(state))
            return PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.PhaseNotAllowed, "请先完成当前对局步骤。");
        if (IsAmbushPlayMode(request.Command.Mode))
            return PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.UnsupportedCommand, "此特殊打出方式暂不支持费用预览。");

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
        var missingMana = Math.Max(0, plan.TotalManaCost - pool.Mana);
        var missingPower = PaymentCostRules.PowerDeficit(pool, plan.AnyPowerCost, plan.PowerCostByTrait);
        var missingExperience = Math.Max(0, plan.TotalExperienceCost - plan.AvailableExperience);
        var remaining = accepted ? PaymentCostRules.PayPowerCost(pool, plan.AnyPowerCost, plan.PowerCostByTrait) : default;
        var adjustments = new List<PlayCostAdjustmentDto>();
        void Adjust(string label, int mana, int power = 0)
        {
            if (mana != 0 || power != 0) adjustments.Add(new(label, mana, power));
        }
        if (state.PendingEffectPlay is { } effect && effect.IgnoreBaseMana) Adjust("效果忽略基础法力", -plan.Behavior.ManaCost);
        Adjust("额外费用", plan.AdditionalManaCost, plan.OptionalPowerCost);
        Adjust("卡牌减费", -plan.CostReductionMana);
        Adjust("所选额外费用带来的减免", -plan.OptionalCostManaReduction);
        Adjust("回响部分减费", -plan.BattlefieldEchoCostReductionMana);
        Adjust("装备减费", -plan.BattlefieldEquipmentCostReductionMana);
        Adjust("单位静态减费", -plan.DragonUnitCostReductionMana);
        Adjust("下次法术减费", -plan.NextSpellCostReductionMana);
        Adjust("战场法术减费", -plan.BattlefieldSpellCostReductionMana);
        Adjust("战场增费", plan.BattlefieldHeldUnitCostIncreaseMana);
        Adjust("法盾费用", 0, plan.SpellshieldTaxPower);
        var cost = new PlayCostBreakdownDto(plan.Behavior.ManaCost, PrintedPowerCostRules.ForCard(plan.Behavior.CardNo).Amount,
            plan.TotalManaCost, plan.AnyPowerCost, plan.PowerCostByTrait, plan.TotalExperienceCost,
            pool.Mana, pool.Power, pool.PowerByTrait, missingMana, missingPower, missingExperience,
            accepted ? pool.Mana - plan.TotalManaCost - plan.LuxSpellOnlyRemainingMana : null,
            accepted ? remaining.AnyPower : null, accepted ? remaining.PowerByTrait : null, adjustments);
        return new(request.RequestId, request.PromptId, state.Tick, true, accepted,
            accepted ? "费用已核对，可以确认打出。" : "所选资源不足，请补充资源或调整额外费用。",
            accepted ? null : rejection.ErrorCode, cost);
    }
}
