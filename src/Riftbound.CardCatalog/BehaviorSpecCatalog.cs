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

public sealed record FunctionalUnitBehaviorCoverageReport(
    int FunctionalUnits,
    int ImplementedUnits,
    int ManualRuleRequiredUnits,
    int UnimplementedUnits,
    int DuplicateGroups,
    int ImplementedDuplicateGroups,
    int ImplementedDuplicateEntries,
    int PendingDuplicateGroups,
    int PendingDuplicateEntries,
    IReadOnlyList<FunctionalUnitBehaviorCoverageRow> Units);

public sealed record FunctionalUnitBehaviorCoverageRow(
    string FunctionalUnitId,
    string RepresentativeNo,
    string Name,
    string Category,
    int Size,
    string Status,
    IReadOnlyList<string> CardNos,
    string? ImplementedByCardNo,
    string? ImplementedEffectKind)
{
    public bool IsDuplicateGroup => Size > 1;
}

public sealed record BehaviorTemplateFamilyCoverageReport(
    IReadOnlyList<BehaviorTemplateFamilyCoverageRow> Families);

public sealed record BehaviorTemplateFamilyCoverageRow(
    string TemplateId,
    int Entries,
    int ImplementedEntries,
    int ManualRuleRequiredEntries,
    int UnimplementedEntries,
    int FunctionalUnits,
    int ImplementedFunctionalUnits,
    int PendingFunctionalUnits,
    IReadOnlyList<string> PendingCategories);

public static class BehaviorTemplateFamilyCoverageReporter
{
    public static BehaviorTemplateFamilyCoverageReport Build(
        IReadOnlyList<BehaviorSpec> specs,
        IReadOnlyList<string> templateIds)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(templateIds);

        var rows = templateIds
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(templateId => BuildRow(specs, templateId))
            .ToArray();

        return new BehaviorTemplateFamilyCoverageReport(rows);
    }

    private static BehaviorTemplateFamilyCoverageRow BuildRow(
        IReadOnlyList<BehaviorSpec> specs,
        string templateId)
    {
        var familySpecs = specs
            .Where(spec => spec.TemplateIds.Contains(templateId, StringComparer.Ordinal))
            .ToArray();
        var unitGroups = familySpecs
            .GroupBy(spec => spec.FunctionalUnitId, StringComparer.Ordinal)
            .ToArray();
        var implementedUnitCount = unitGroups.Count(group => group.Any(spec => string.Equals(
            spec.Status,
            BehaviorImplementationStatuses.Implemented,
            StringComparison.Ordinal)));
        var pendingUnitCount = unitGroups.Length - implementedUnitCount;
        var pendingCategories = familySpecs
            .Where(spec => !string.Equals(spec.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            .Select(spec => spec.CardCategoryName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new BehaviorTemplateFamilyCoverageRow(
            templateId,
            familySpecs.Length,
            familySpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal)),
            familySpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal)),
            familySpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Unimplemented,
                StringComparison.Ordinal)),
            unitGroups.Length,
            implementedUnitCount,
            pendingUnitCount,
            pendingCategories);
    }
}

public static class FunctionalUnitBehaviorCoverageReporter
{
    public static FunctionalUnitBehaviorCoverageReport Build(
        IReadOnlyList<FunctionalUnit> units,
        IReadOnlyList<BehaviorSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(specs);

        var specsByCardNo = specs.ToDictionary(spec => spec.CardNo, StringComparer.Ordinal);
        var rows = units
            .Select(unit => BuildRow(unit, specsByCardNo))
            .OrderBy(row => row.FunctionalUnitId, StringComparer.Ordinal)
            .ToArray();

        var implementedUnits = rows.Count(row => string.Equals(
            row.Status,
            BehaviorImplementationStatuses.Implemented,
            StringComparison.Ordinal));
        var manualRuleRequiredUnits = rows.Count(row => string.Equals(
            row.Status,
            BehaviorImplementationStatuses.ManualRuleRequired,
            StringComparison.Ordinal));
        var unimplementedUnits = rows.Count(row => string.Equals(
            row.Status,
            BehaviorImplementationStatuses.Unimplemented,
            StringComparison.Ordinal));
        var duplicateGroups = rows.Where(row => row.IsDuplicateGroup).ToArray();
        var implementedDuplicateGroups = duplicateGroups
            .Where(row => string.Equals(row.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            .ToArray();
        var pendingDuplicateGroups = duplicateGroups
            .Where(row => !string.Equals(row.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            .ToArray();

        return new FunctionalUnitBehaviorCoverageReport(
            rows.Length,
            implementedUnits,
            manualRuleRequiredUnits,
            unimplementedUnits,
            duplicateGroups.Length,
            implementedDuplicateGroups.Length,
            implementedDuplicateGroups.Sum(row => row.Size),
            pendingDuplicateGroups.Length,
            pendingDuplicateGroups.Sum(row => row.Size),
            rows);
    }

    private static FunctionalUnitBehaviorCoverageRow BuildRow(
        FunctionalUnit unit,
        IReadOnlyDictionary<string, BehaviorSpec> specsByCardNo)
    {
        var unitSpecs = unit.Cards
            .Select(card => specsByCardNo.TryGetValue(card.CardNo, out var spec)
                ? spec
                : throw new InvalidOperationException($"Card {card.CardNo} is missing a BehaviorSpec."))
            .ToArray();
        var implementedSpec = unitSpecs
            .FirstOrDefault(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal));
        var status = implementedSpec is not null
            ? BehaviorImplementationStatuses.Implemented
            : unitSpecs.Any(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal))
                ? BehaviorImplementationStatuses.ManualRuleRequired
                : BehaviorImplementationStatuses.Unimplemented;

        return new FunctionalUnitBehaviorCoverageRow(
            unit.Id,
            unit.RepresentativeNo,
            unit.Name,
            unit.Category,
            unit.Size,
            status,
            unit.Cards.Select(card => card.CardNo).ToArray(),
            implementedSpec?.ImplementedByCardNo,
            implementedSpec?.ImplementedEffectKind);
    }
}

public static class OfficialRuleDomainBehaviorCatalog
{
    public const string RuneResourceDomainEffectKind = "RUNE_RESOURCE_DOMAIN";
    public const string TokenFactoryDomainEffectKind = "TOKEN_FACTORY_DOMAIN";
    public const string LegendActionDomainEffectKind = "LEGEND_ACTION_DOMAIN";
    public const string BattlefieldRuleDomainEffectKind = "BATTLEFIELD_RULE_DOMAIN";

    public static IReadOnlyList<ImplementedCardBehavior> MergeWithNonPlayCardDomains(
        IReadOnlyList<OfficialCard> cards,
        IReadOnlyList<ImplementedCardBehavior> playCardBehaviors)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(playCardBehaviors);

        var runeBehaviors = cards
            .Where(IsRuneCard)
            .Select(card => new ImplementedCardBehavior(
                card.CardNo,
                RuneResourceDomainEffectKind,
                card.CardName))
            .ToArray();
        var tokenBehaviors = cards
            .Where(IsTokenCard)
            .Select(card => new ImplementedCardBehavior(
                card.CardNo,
                TokenFactoryDomainEffectKind,
                card.CardName))
            .ToArray();
        var legendActionBehaviors = cards
            .Where(IsImplementedLegendActionCard)
            .Select(card => new ImplementedCardBehavior(
                card.CardNo,
                LegendActionDomainEffectKind,
                card.CardName))
            .ToArray();
        var battlefieldRuleBehaviors = cards
            .Where(IsImplementedBattlefieldRuleCard)
            .Select(card => new ImplementedCardBehavior(
                card.CardNo,
                BattlefieldRuleDomainEffectKind,
                card.CardName))
            .ToArray();

        return playCardBehaviors
            .Concat(runeBehaviors)
            .Concat(tokenBehaviors)
            .Concat(legendActionBehaviors)
            .Concat(battlefieldRuleBehaviors)
            .ToArray();
    }

    public static bool IsRuneCard(OfficialCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal);
    }

    public static bool IsTokenCard(OfficialCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card.CardCategoryName.StartsWith("指示物", StringComparison.Ordinal);
    }

    public static bool IsImplementedLegendActionCard(OfficialCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card.CardNo is "FND-259/298"
            or "OGN·259/298"
            or "OGN·305*/298"
            or "OGN·305/298"
            or "OGN·257/298"
            or "OGN·304*/298"
            or "OGN·304/298"
            or "UNL-203/219"
            or "UNL-237*/219"
            or "UNL-237/219"
            or "FND-265/298"
            or "OGN·265/298"
            or "OGN·308*/298"
            or "OGN·308/298"
            or "OGN·267/298"
            or "OGN·309/298"
            or "OGN·309*/298"
            or "UNL-201/219"
            or "UNL-236/219"
            or "UNL-236*/219"
            or "UNL-185/219"
            or "UNL-228/219"
            or "UNL-228*/219"
            or "SFD·193/221"
            or "SFD·245/221"
            or "OGN·253/298"
            or "OGN·302/298"
            or "OGN·302*/298"
            or "OGN·263/298"
            or "OGN·263a/298"
            or "OGN·307/298"
            or "OGN·307*/298"
            or "OGN·261/298"
            or "OGN·306/298"
            or "OGN·306*/298"
            or "SFD·197/221"
            or "SFD·247/221"
            or "SFD·203/221"
            or "SFD·250/221"
            or "UNL-181/219"
            or "UNL-226/219"
            or "UNL-226*/219"
            or "UNL-197/219"
            or "UNL-234/219"
            or "UNL-234*/219"
            or "OGN·247/298"
            or "OGN·299/298"
            or "OGN·299*/298"
            or "SFD·189/221"
            or "SFD·244/221"
            or "SFD·199/221"
            or "SFD·248/221"
            or "SFD·195/221"
            or "SFD·195a/221·P"
            or "SFD·246/221"
            or "SFD·201/221"
            or "SFD·249/221"
            or "SFD·187/221"
            or "SFD·243/221"
            or "UNL-199/219"
            or "UNL-235/219"
            or "UNL-235*/219"
            or "UNL-195/219"
            or "UNL-233/219"
            or "UNL-233*/219"
            or "UNL-183/219"
            or "UNL-227/219"
            or "UNL-227*/219"
            or "UNL-187/219"
            or "UNL-229/219"
            or "UNL-229*/219"
            or "UNL-193/219"
            or "UNL-232/219"
            or "UNL-232*/219"
            or "FND-251/298"
            or "OGN·251/298"
            or "OGN·301*/298"
            or "OGN·301/298"
            or "UNL-189/219"
            or "UNL-230*/219"
            or "UNL-230/219"
            or "SFD·181/221"
            or "SFD·240/221"
            or "SFD·183/221"
            or "SFD·241/221"
            or "OGS·019/024"
            or "OGN·255/298"
            or "OGN·303/298"
            or "OGN·303*/298"
            or "SFD·185/221"
            or "SFD·242/221"
            or "OGS·023/024"
            or "OGS·021/024"
            or "OGS·017/024"
            or "FND-249/298"
            or "OGN·249/298"
            or "OGN·300/298"
            or "OGN·300*/298"
            or "SFD·205/221"
            or "SFD·251/221"
            or "OGN·269/298"
            or "OGN·310/298"
            or "OGN·310*/298"
            or "UNL-191/219"
            or "UNL-231/219"
            or "UNL-231*/219";
    }

    public static bool IsImplementedBattlefieldRuleCard(OfficialCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card.CardNo is "UNL-207/219"
            or "UNL-208/219"
            or "UNL-209/219"
            or "UNL-210/219"
            or "UNL-212/219"
            or "OGN·276/298"
            or "OGN·276a/298"
            or "OGN·275/298"
            or "OGN·279/298"
            or "OGN·280/298"
            or "OGN·281/298"
            or "OGN·282/298"
            or "OGN·283/298"
            or "OGN·284/298"
            or "OGN·285/298"
            or "OGN·287/298"
            or "OGN·288/298"
            or "OGN·290/298"
            or "OGN·291/298"
            or "OGN·293/298"
            or "OGN·293a/298"
            or "OGN·294/298"
            or "OGN·298/298"
            or "SFD·210/221"
            or "SFD·212/221"
            or "SFD·214/221"
            or "SFD·215/221"
            or "UNL-217/219"
            or "SFD·217/221"
            or "SFD·218/221"
            or "SFD·219/221"
            or "SFD·220/221"
            or "SFD·221/221";
    }
}

public static class BehaviorSpecCatalogBuilder
{
    private static readonly HashSet<string> SafeExistingTemplateMappings = new(StringComparer.Ordinal)
    {
        BehaviorTemplateIds.Draw,
        BehaviorTemplateIds.Damage,
        BehaviorTemplateIds.Destroy,
        BehaviorTemplateIds.Move,
        BehaviorTemplateIds.Recall,
        BehaviorTemplateIds.Recycle,
        BehaviorTemplateIds.Banish,
        BehaviorTemplateIds.Stun,
        BehaviorTemplateIds.TempMight,
        BehaviorTemplateIds.Boon
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
                && string.Equals(
                    implementation.EffectKind,
                    OfficialRuleDomainBehaviorCatalog.RuneResourceDomainEffectKind,
                    StringComparison.Ordinal) =>
                "Mapped to the P6 rune resource domain: rune call, rune pool payment, and end-turn clearing are covered by P2 core rules conformance fixtures; rune cards remain outside PLAY_CARD.",
            BehaviorImplementationStatuses.Implemented when implementation is not null
                && string.Equals(
                    implementation.EffectKind,
                    OfficialRuleDomainBehaviorCatalog.TokenFactoryDomainEffectKind,
                    StringComparison.Ordinal) =>
                $"Mapped to the P6 token factory domain: generated token category '{card.CardCategoryName}' has an explicit official token identity binding; token cards remain outside PLAY_CARD.",
            BehaviorImplementationStatuses.Implemented when implementation is not null
                && string.Equals(
                    implementation.EffectKind,
                    OfficialRuleDomainBehaviorCatalog.LegendActionDomainEffectKind,
                    StringComparison.Ordinal) =>
                "Mapped to the P7.9 legend action domain: LEGEND_ACT validates source legend, exhaustion, costs, targets, and server-authoritative effects; legend cards remain outside PLAY_CARD.",
            BehaviorImplementationStatuses.Implemented when implementation is not null
                && string.Equals(
                    implementation.EffectKind,
                    OfficialRuleDomainBehaviorCatalog.BattlefieldRuleDomainEffectKind,
                    StringComparison.Ordinal) =>
                "Mapped to the P7.9 battlefield rule domain: DECLARE_BATTLE accepts server-known battlefield objects and resolves hold/conquer battlefield effects from authoritative combat events; battlefield cards remain outside PLAY_CARD.",
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
