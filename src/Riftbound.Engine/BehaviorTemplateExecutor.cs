using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record BehaviorTemplateDefinition(
    string TemplateId,
    string DisplayName,
    string Reason);

public sealed record BehaviorTemplateExecutionContext(
    string PlayerId,
    string SourceObjectId,
    string CardNo,
    IReadOnlyList<string> TargetObjectIds,
    string Mode = "");

public sealed record BehaviorTemplateExecutionStep(
    string TemplateId,
    string Status,
    string Reason);

public sealed record BehaviorTemplateExecutionPlan(
    string CardNo,
    string Status,
    string Reason,
    IReadOnlyList<BehaviorTemplateExecutionStep> Steps);

public static class BehaviorTemplateRegistry
{
    private static readonly BehaviorTemplateDefinition[] Definitions =
    [
        new(BehaviorTemplateIds.Draw, "Draw", "Skeleton route for drawing cards."),
        new(BehaviorTemplateIds.Damage, "Damage", "Skeleton route for dealing damage."),
        new(BehaviorTemplateIds.Destroy, "Destroy", "Skeleton route for destroying objects."),
        new(BehaviorTemplateIds.Move, "Move", "Skeleton route for moving objects between locations."),
        new(BehaviorTemplateIds.Recall, "Recall", "Skeleton route for returning objects to hand/base as required by card text."),
        new(BehaviorTemplateIds.Stun, "Stun", "Skeleton route for applying stun state."),
        new(BehaviorTemplateIds.TempMight, "Temporary Might", "Skeleton route for until-end-of-turn might modifiers."),
        new(BehaviorTemplateIds.GainExperience, "Gain Experience", "Skeleton route for experience gain/payment hooks."),
        new(BehaviorTemplateIds.Assemble, "Assemble", "Skeleton route for assemble/equipment attachment keywords."),
        new(BehaviorTemplateIds.Echo, "Echo", "Skeleton route for echo optional cost and repeat behavior."),
        new(BehaviorTemplateIds.Ambush, "Ambush", "Skeleton route for ambush timing and hidden-card behavior.")
    ];

    public static IReadOnlyList<BehaviorTemplateDefinition> GetAll()
    {
        return Definitions;
    }

    public static bool TryGet(string templateId, out BehaviorTemplateDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => string.Equals(
            candidate.TemplateId,
            templateId,
            StringComparison.Ordinal))!;
        return definition is not null;
    }
}

public sealed class BehaviorTemplateExecutor
{
    public BehaviorTemplateExecutionPlan BuildPlan(
        BehaviorSpec spec,
        BehaviorTemplateExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(spec.CardNo, context.CardNo, StringComparison.Ordinal))
        {
            return new BehaviorTemplateExecutionPlan(
                context.CardNo,
                BehaviorImplementationStatuses.Unimplemented,
                $"Template context cardNo '{context.CardNo}' does not match BehaviorSpec cardNo '{spec.CardNo}'.",
                []);
        }

        var effectStatusByTemplateId = spec.Effects
            .GroupBy(effect => effect.TemplateId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => ResolveEffectTemplateStatus(group.Select(effect => effect.Status).ToArray()),
                StringComparer.Ordinal);
        var steps = spec.TemplateIds
            .Select(templateId => BuildStep(
                templateId,
                effectStatusByTemplateId.TryGetValue(templateId, out var status)
                    ? status
                    : spec.Status))
            .ToArray();
        var status = ResolvePlanStatus(spec, steps);
        var reason = status switch
        {
            BehaviorImplementationStatuses.Implemented =>
                "All recognized template routes are currently covered by existing P2 hand-written behavior mappings; the P3 executor does not mutate game state.",
            BehaviorImplementationStatuses.ManualRuleRequired =>
                "At least one template route belongs to a manual rule domain that is outside P3 execution.",
            _ =>
                "Template routes were parsed, but execution remains skeleton-only in P3."
        };

        return new BehaviorTemplateExecutionPlan(spec.CardNo, status, reason, steps);
    }

    private static BehaviorTemplateExecutionStep BuildStep(string templateId, string templateStatus)
    {
        if (!BehaviorTemplateRegistry.TryGet(templateId, out var definition))
        {
            return new BehaviorTemplateExecutionStep(
                templateId,
                BehaviorImplementationStatuses.Unimplemented,
                $"No template registry entry exists for '{templateId}'.");
        }

        if (string.Equals(templateStatus, BehaviorImplementationStatuses.ManualRuleRequired, StringComparison.Ordinal))
        {
            return new BehaviorTemplateExecutionStep(
                templateId,
                BehaviorImplementationStatuses.ManualRuleRequired,
                $"{definition.DisplayName} requires a dedicated rule domain before execution.");
        }

        if (string.Equals(templateStatus, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
        {
            return new BehaviorTemplateExecutionStep(
                templateId,
                BehaviorImplementationStatuses.Implemented,
                $"{definition.DisplayName} route is recognized and remains delegated to the existing hand-written P2 behavior.");
        }

        return new BehaviorTemplateExecutionStep(
            templateId,
            BehaviorImplementationStatuses.Unimplemented,
            $"{definition.DisplayName} route is registered, but no executor implementation is enabled in P3.");
    }

    private static string ResolvePlanStatus(
        BehaviorSpec spec,
        IReadOnlyList<BehaviorTemplateExecutionStep> steps)
    {
        if (steps.Count == 0)
        {
            return spec.Status;
        }

        if (steps.Any(step => string.Equals(step.Status, BehaviorImplementationStatuses.Unimplemented, StringComparison.Ordinal)))
        {
            return BehaviorImplementationStatuses.Unimplemented;
        }

        if (steps.Any(step => string.Equals(step.Status, BehaviorImplementationStatuses.ManualRuleRequired, StringComparison.Ordinal)))
        {
            return BehaviorImplementationStatuses.ManualRuleRequired;
        }

        return BehaviorImplementationStatuses.Implemented;
    }

    private static string ResolveEffectTemplateStatus(IReadOnlyList<string> statuses)
    {
        if (statuses.Any(status => string.Equals(status, BehaviorImplementationStatuses.Unimplemented, StringComparison.Ordinal)))
        {
            return BehaviorImplementationStatuses.Unimplemented;
        }

        if (statuses.Any(status => string.Equals(status, BehaviorImplementationStatuses.ManualRuleRequired, StringComparison.Ordinal)))
        {
            return BehaviorImplementationStatuses.ManualRuleRequired;
        }

        return BehaviorImplementationStatuses.Implemented;
    }
}
