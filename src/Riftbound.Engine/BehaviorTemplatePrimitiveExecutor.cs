using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class BehaviorTemplatePrimitivePlanStatuses
{
    public const string Ready = "primitive-plan-ready";
    public const string DelegatedToP2 = "delegated-to-p2";
    public const string Blocked = "blocked";
}

public static class BehaviorTemplatePrimitiveKinds
{
    public const string DrawCards = "draw-cards";
    public const string DealDamage = "deal-damage";
    public const string DestroyTarget = "destroy-target";
    public const string ApplyStatusEffect = "apply-status-effect";
    public const string ModifyPowerUntilEndOfTurn = "modify-power-until-end-of-turn";
}

public sealed record BehaviorTemplatePrimitive(
    string TemplateId,
    string Kind,
    int Amount,
    string TargetScope,
    string StatusEffectId = "",
    string ConditionKind = "",
    string Reason = "");

public sealed record BehaviorTemplatePrimitivePlan(
    string CardNo,
    string Status,
    string Reason,
    IReadOnlyList<BehaviorTemplatePrimitive> Primitives,
    BehaviorTemplateDelegationPlan DelegationPlan);

public sealed class BehaviorTemplatePrimitiveExecutor
{
    private readonly BehaviorTemplateDelegationBridge bridge = new();

    public BehaviorTemplatePrimitivePlan BuildPrimitivePlan(
        BehaviorSpec spec,
        BehaviorTemplateExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);

        var delegation = bridge.BuildDelegationPlan(spec, context);
        if (!string.Equals(delegation.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal)
            || delegation.DelegatedBehavior is null)
        {
            return new BehaviorTemplatePrimitivePlan(
                context.CardNo,
                BehaviorTemplatePrimitivePlanStatuses.Blocked,
                $"No executable primitive plan is available because delegation failed: {delegation.Reason}",
                [],
                delegation);
        }

        var primitives = new List<BehaviorTemplatePrimitive>();
        foreach (var step in delegation.ExecutionPlan.Steps)
        {
            if (!string.Equals(step.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            {
                return new BehaviorTemplatePrimitivePlan(
                    context.CardNo,
                    BehaviorTemplatePrimitivePlanStatuses.Blocked,
                    $"Template '{step.TemplateId}' is not implemented by the current BehaviorSpec route.",
                    primitives,
                    delegation);
            }

            var primitive = BuildPrimitive(step.TemplateId, delegation.DelegatedBehavior);
            if (primitive is null)
            {
                if (IsParsedReminderOnlyTemplate(step.TemplateId, delegation.DelegatedBehavior))
                {
                    continue;
                }

                return new BehaviorTemplatePrimitivePlan(
                    context.CardNo,
                    BehaviorTemplatePrimitivePlanStatuses.DelegatedToP2,
                    $"Template '{step.TemplateId}' remains delegated to the existing P2 hand-written behavior; P4.5 has no primitive executor for it yet.",
                    primitives,
                    delegation);
            }

            primitives.Add(primitive);
        }

        if (primitives.Count == 0)
        {
            return new BehaviorTemplatePrimitivePlan(
                context.CardNo,
                BehaviorTemplatePrimitivePlanStatuses.DelegatedToP2,
                "BehaviorSpec has no template steps to convert into P4.5 primitives.",
                primitives,
                delegation);
        }

        return new BehaviorTemplatePrimitivePlan(
            context.CardNo,
            BehaviorTemplatePrimitivePlanStatuses.Ready,
            "All template steps have P4.5 primitive plans; CoreRuleEngine remains the authoritative state mutator.",
            primitives,
            delegation);
    }

    private static BehaviorTemplatePrimitive? BuildPrimitive(
        string templateId,
        CardBehaviorDefinition behavior)
    {
        return templateId switch
        {
            BehaviorTemplateIds.Draw when behavior.DrawCount > 0 => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Draw,
                BehaviorTemplatePrimitiveKinds.DrawCards,
                behavior.DrawCount,
                "",
                Reason: "Draw count is supplied by the existing P2 CardBehaviorDefinition."),
            BehaviorTemplateIds.Damage when behavior.DamageAmount > 0 => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Damage,
                BehaviorTemplatePrimitiveKinds.DealDamage,
                behavior.DamageAmount,
                behavior.TargetScope,
                ConditionKind: behavior.DamageConditionKind,
                Reason: "Damage amount and target scope are supplied by the existing P2 CardBehaviorDefinition."),
            BehaviorTemplateIds.Destroy when behavior.DestroysTarget => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Destroy,
                BehaviorTemplatePrimitiveKinds.DestroyTarget,
                0,
                behavior.TargetScope,
                Reason: "Destroy target scope is supplied by the existing P2 CardBehaviorDefinition."),
            BehaviorTemplateIds.Stun when !string.IsNullOrWhiteSpace(behavior.StatusEffectId) => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Stun,
                BehaviorTemplatePrimitiveKinds.ApplyStatusEffect,
                0,
                behavior.TargetScope,
                StatusEffectId: behavior.StatusEffectId,
                Reason: "Status effect id and target scope are supplied by the existing P2 CardBehaviorDefinition."),
            BehaviorTemplateIds.TempMight when behavior.PowerModifierAmount != 0 => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.TempMight,
                BehaviorTemplatePrimitiveKinds.ModifyPowerUntilEndOfTurn,
                behavior.PowerModifierAmount,
                behavior.TargetScope,
                ConditionKind: behavior.PowerModifierConditionKind,
                Reason: "Until-end-of-turn power modifier is supplied by the existing P2 CardBehaviorDefinition."),
            _ => null
        };
    }

    private static bool IsParsedReminderOnlyTemplate(
        string templateId,
        CardBehaviorDefinition behavior)
    {
        return string.Equals(templateId, BehaviorTemplateIds.Damage, StringComparison.Ordinal)
            && behavior.DamageAmount == 0
            && string.Equals(behavior.StatusEffectId, "STUNNED", StringComparison.Ordinal);
    }
}
