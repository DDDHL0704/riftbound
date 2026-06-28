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
    public const string BanishThenPlayTarget = "banish-then-play-target";
    public const string ReturnTargetToHand = "return-target-to-hand";
    public const string GrantBoon = "grant-boon";
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
    string PlayDestinationZone = "",
    bool IgnoreCosts = false,
    string ReturnDestinationZone = "",
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

            var primitive = BuildPrimitive(step.TemplateId, spec, delegation.DelegatedBehavior);
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
        BehaviorSpec spec,
        CardBehaviorDefinition behavior)
    {
        var effect = spec.Effects.FirstOrDefault(candidate => string.Equals(
            candidate.TemplateId,
            templateId,
            StringComparison.Ordinal));
        var specPrimitive = BuildPrimitiveFromEffect(effect);
        if (specPrimitive is not null)
        {
            return specPrimitive;
        }

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
            BehaviorTemplateIds.Banish when behavior.BanishesTargetThenPlaysToBase => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Banish,
                BehaviorTemplatePrimitiveKinds.BanishThenPlayTarget,
                0,
                behavior.TargetScope,
                PlayDestinationZone: "BASE",
                IgnoreCosts: true,
                Reason: "Banish/play destination is supplied by the existing P2 CardBehaviorDefinition."),
            BehaviorTemplateIds.Recall when behavior.ReturnsTargetToHand => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Recall,
                BehaviorTemplatePrimitiveKinds.ReturnTargetToHand,
                0,
                behavior.TargetScope,
                ReturnDestinationZone: "HAND",
                Reason: "Return destination is supplied by the existing P2 CardBehaviorDefinition."),
            BehaviorTemplateIds.Boon when behavior.GrantsBoon => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Boon,
                BehaviorTemplatePrimitiveKinds.GrantBoon,
                1,
                behavior.TargetScope,
                Reason: "Boon target scope is supplied by the existing P2 CardBehaviorDefinition."),
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

    private static BehaviorTemplatePrimitive? BuildPrimitiveFromEffect(EffectPhraseSpec? effect)
    {
        if (effect is null)
        {
            return null;
        }

        return effect.TemplateId switch
        {
            BehaviorTemplateIds.Damage when effect.DamageAmount is > 0 => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Damage,
                BehaviorTemplatePrimitiveKinds.DealDamage,
                effect.DamageAmount.Value,
                effect.TargetScope ?? string.Empty,
                ConditionKind: effect.ConditionKind ?? CardDamageConditionKinds.None,
                Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
            BehaviorTemplateIds.Destroy when effect.DestroysTarget is true => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Destroy,
                BehaviorTemplatePrimitiveKinds.DestroyTarget,
                0,
                effect.TargetScope ?? string.Empty,
                Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
            BehaviorTemplateIds.Banish when effect.BanishesTarget is true
                && !string.IsNullOrWhiteSpace(effect.PlayDestinationZone) => new BehaviorTemplatePrimitive(
                    BehaviorTemplateIds.Banish,
                    BehaviorTemplatePrimitiveKinds.BanishThenPlayTarget,
                    0,
                    effect.TargetScope ?? string.Empty,
                    PlayDestinationZone: effect.PlayDestinationZone,
                    IgnoreCosts: effect.IgnoreCosts is true,
                    Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
            BehaviorTemplateIds.Recall when effect.ReturnsTargetToHand is true
                && !string.IsNullOrWhiteSpace(effect.ReturnDestinationZone) => new BehaviorTemplatePrimitive(
                    BehaviorTemplateIds.Recall,
                    BehaviorTemplatePrimitiveKinds.ReturnTargetToHand,
                    0,
                    effect.TargetScope ?? string.Empty,
                    ReturnDestinationZone: effect.ReturnDestinationZone,
                    Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
            BehaviorTemplateIds.Boon when effect.GrantsBoon is true => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Boon,
                BehaviorTemplatePrimitiveKinds.GrantBoon,
                effect.BoonPowerBonusAmount ?? 0,
                effect.TargetScope ?? string.Empty,
                Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
            BehaviorTemplateIds.Draw when effect.DrawCount is > 0 => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Draw,
                BehaviorTemplatePrimitiveKinds.DrawCards,
                effect.DrawCount.Value,
                effect.TargetScope ?? string.Empty,
                ConditionKind: effect.ConditionKind ?? string.Empty,
                Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
            BehaviorTemplateIds.Stun when !string.IsNullOrWhiteSpace(effect.StatusEffectId) => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.Stun,
                BehaviorTemplatePrimitiveKinds.ApplyStatusEffect,
                0,
                effect.TargetScope ?? string.Empty,
                StatusEffectId: effect.StatusEffectId,
                ConditionKind: effect.ConditionKind ?? string.Empty,
                Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
            BehaviorTemplateIds.TempMight when effect.PowerModifierAmount is not null and not 0 => new BehaviorTemplatePrimitive(
                BehaviorTemplateIds.TempMight,
                BehaviorTemplatePrimitiveKinds.ModifyPowerUntilEndOfTurn,
                effect.PowerModifierAmount.Value,
                effect.TargetScope ?? string.Empty,
                ConditionKind: effect.ConditionKind ?? string.Empty,
                Reason: "Primitive metadata is supplied by BehaviorSpec.Effects parsed from official text."),
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
