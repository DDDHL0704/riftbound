using Riftbound.Contracts;

namespace Riftbound.CardCatalog;

public sealed record ImplementedCardBehavior(
    string CardNo,
    string EffectKind,
    string DisplayName);

public sealed record BehaviorSpecCatalogReport(
    int OfficialEntries,
    int BehaviorSpecs,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyList<string> MissingReasonCardNos);

public static class BehaviorSpecCatalogBuilder
{
    private static readonly HashSet<string> SafeExistingTemplateMappings = new(StringComparer.Ordinal)
    {
        BehaviorTemplateIds.Draw,
        BehaviorTemplateIds.Damage,
        BehaviorTemplateIds.Destroy,
        BehaviorTemplateIds.Move,
        BehaviorTemplateIds.Recall,
        BehaviorTemplateIds.Stun,
        BehaviorTemplateIds.TempMight
    };

    private static readonly HashSet<string> ManualRuleRequiredCategories = new(StringComparer.Ordinal)
    {
        "符文",
        "传奇",
        "战场"
    };

    public static IReadOnlyList<BehaviorSpec> Build(
        IReadOnlyList<OfficialCard> cards,
        IReadOnlyList<FunctionalUnit> units,
        IReadOnlyList<ImplementedCardBehavior> implementedBehaviors)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(implementedBehaviors);

        var unitByCardNo = units
            .SelectMany(unit => unit.Cards.Select(card => new KeyValuePair<string, FunctionalUnit>(card.CardNo, unit)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var implementationByCardNo = implementedBehaviors
            .GroupBy(behavior => behavior.CardNo, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return cards
            .Select(card => Build(card, unitByCardNo, implementationByCardNo))
            .OrderBy(spec => spec.CardNo, StringComparer.Ordinal)
            .ToArray();
    }

    public static BehaviorSpecCatalogReport BuildReport(IReadOnlyList<BehaviorSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);

        var statusCounts = specs
            .GroupBy(spec => spec.Status, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var missingReasonCardNos = specs
            .Where(spec => string.IsNullOrWhiteSpace(spec.Reason))
            .Select(spec => spec.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new BehaviorSpecCatalogReport(
            specs.Count,
            specs.Count,
            statusCounts,
            missingReasonCardNos);
    }

    private static BehaviorSpec Build(
        OfficialCard card,
        IReadOnlyDictionary<string, FunctionalUnit> unitByCardNo,
        IReadOnlyDictionary<string, ImplementedCardBehavior> implementationByCardNo)
    {
        if (!unitByCardNo.TryGetValue(card.CardNo, out var unit))
        {
            throw new InvalidOperationException($"Card {card.CardNo} is missing a functional unit.");
        }

        var parsed = RuleTextParser.Parse(card);
        var implementation = FindImplementation(card, unit, implementationByCardNo);
        var status = DetermineStatus(card, implementation);
        var reason = DetermineReason(card, unit, status, implementation);
        var effects = ApplyEffectStatuses(parsed.Effects, status, implementation is not null);
        var activatedAbilities = ApplyActivatedAbilityStatuses(parsed.ActivatedAbilities, status);
        var staticAbilities = ApplyStaticAbilityStatuses(parsed.StaticAbilities, status, implementation is not null);
        var templateIds = effects
            .Select(effect => effect.TemplateId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new BehaviorSpec(
            card.CardNo,
            card.CardName,
            card.CardCategoryName,
            unit.Id,
            status,
            reason,
            card.CardEffect,
            parsed.Cost,
            parsed.Keywords,
            parsed.Targets,
            parsed.Triggers,
            parsed.Replacements,
            activatedAbilities,
            staticAbilities,
            effects,
            templateIds,
            implementation?.EffectKind,
            implementation?.CardNo);
    }

    private static ImplementedCardBehavior? FindImplementation(
        OfficialCard card,
        FunctionalUnit unit,
        IReadOnlyDictionary<string, ImplementedCardBehavior> implementationByCardNo)
    {
        if (implementationByCardNo.TryGetValue(card.CardNo, out var direct))
        {
            return direct;
        }

        return unit.Cards
            .Select(unitCard => implementationByCardNo.TryGetValue(unitCard.CardNo, out var implementation)
                ? implementation
                : null)
            .OfType<ImplementedCardBehavior>()
            .OrderBy(implementation => implementation.CardNo, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string DetermineStatus(OfficialCard card, ImplementedCardBehavior? implementation)
    {
        if (implementation is not null)
        {
            return BehaviorImplementationStatuses.Implemented;
        }

        if (ManualRuleRequiredCategories.Contains(card.CardCategoryName))
        {
            return BehaviorImplementationStatuses.ManualRuleRequired;
        }

        return BehaviorImplementationStatuses.Unimplemented;
    }

    private static string DetermineReason(
        OfficialCard card,
        FunctionalUnit unit,
        string status,
        ImplementedCardBehavior? implementation)
    {
        return status switch
        {
            BehaviorImplementationStatuses.Implemented when implementation is not null
                && string.Equals(implementation.CardNo, card.CardNo, StringComparison.Ordinal) =>
                $"Mapped directly to existing P2 hand-written behavior '{implementation.EffectKind}'.",
            BehaviorImplementationStatuses.Implemented =>
                $"Functional unit {unit.Id} is covered by existing P2 hand-written behavior '{implementation?.EffectKind}' from {implementation?.CardNo}.",
            BehaviorImplementationStatuses.ManualRuleRequired =>
                $"Category '{card.CardCategoryName}' requires a dedicated non-PLAY_CARD rule domain before template execution.",
            _ when card.CardCategoryName.StartsWith("指示物", StringComparison.Ordinal) =>
                $"Generated token category '{card.CardCategoryName}' needs an explicit token factory binding before it can execute.",
            _ =>
                $"Playable functional unit {unit.Id} has no existing registry mapping or template executor implementation."
        };
    }

    private static IReadOnlyList<EffectPhraseSpec> ApplyEffectStatuses(
        IReadOnlyList<EffectPhraseSpec> effects,
        string behaviorStatus,
        bool hasImplementation)
    {
        return effects
            .Select(effect =>
            {
                if (hasImplementation && SafeExistingTemplateMappings.Contains(effect.TemplateId))
                {
                    return effect with
                    {
                        Status = BehaviorImplementationStatuses.Implemented,
                        Reason = "Covered through an existing P2 hand-written behavior; the P3 template executor only records the route."
                    };
                }

                if (string.Equals(behaviorStatus, BehaviorImplementationStatuses.ManualRuleRequired, StringComparison.Ordinal))
                {
                    return effect with
                    {
                        Status = BehaviorImplementationStatuses.ManualRuleRequired,
                        Reason = "Template candidate belongs to a card type that needs a dedicated rule domain first."
                    };
                }

                return effect with
                {
                    Status = BehaviorImplementationStatuses.Unimplemented,
                    Reason = "Template route is recognized, but execution remains skeleton-only in P3."
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<ActivatedAbilitySpec> ApplyActivatedAbilityStatuses(
        IReadOnlyList<ActivatedAbilitySpec> activatedAbilities,
        string behaviorStatus)
    {
        return activatedAbilities
            .Select(ability => string.Equals(behaviorStatus, BehaviorImplementationStatuses.ManualRuleRequired, StringComparison.Ordinal)
                ? ability with
                {
                    Status = BehaviorImplementationStatuses.ManualRuleRequired,
                    Reason = "Activated ability belongs to a manual rule domain in P3."
                }
                : ability)
            .ToArray();
    }

    private static IReadOnlyList<StaticAbilitySpec> ApplyStaticAbilityStatuses(
        IReadOnlyList<StaticAbilitySpec> staticAbilities,
        string behaviorStatus,
        bool hasImplementation)
    {
        return staticAbilities
            .Select(ability =>
            {
                if (hasImplementation)
                {
                    return ability with
                    {
                        Status = BehaviorImplementationStatuses.Implemented,
                        Reason = "Static/keyword text is covered by the current functional unit mapping or tracked as a safe P2 tag/static path."
                    };
                }

                if (string.Equals(behaviorStatus, BehaviorImplementationStatuses.ManualRuleRequired, StringComparison.Ordinal))
                {
                    return ability with
                    {
                        Status = BehaviorImplementationStatuses.ManualRuleRequired,
                        Reason = "Static text belongs to a manual rule domain in P3."
                    };
                }

                return ability;
            })
            .ToArray();
    }
}

public sealed record FunctionalUnitStabilityReport(
    int OfficialEntries,
    int FunctionalUnits,
    bool IdsAreUnique,
    IReadOnlyList<FunctionalUnitStableRow> Units);

public sealed record FunctionalUnitStableRow(
    string Id,
    string RepresentativeNo,
    string Name,
    string Category,
    int Size,
    string Signature);

public static class FunctionalUnitReporter
{
    public static FunctionalUnitStabilityReport Build(IReadOnlyList<FunctionalUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        var rows = units
            .Select(unit => new FunctionalUnitStableRow(
                unit.Id,
                unit.RepresentativeNo,
                unit.Name,
                unit.Category,
                unit.Size,
                unit.Signature))
            .OrderBy(row => row.Id, StringComparer.Ordinal)
            .ToArray();

        return new FunctionalUnitStabilityReport(
            units.Sum(unit => unit.Size),
            units.Count,
            rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() == rows.Length,
            rows);
    }
}
