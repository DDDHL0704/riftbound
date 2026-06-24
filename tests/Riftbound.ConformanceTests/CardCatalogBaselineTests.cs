using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CardCatalogBaselineTests
{
    [Fact]
    public async Task OfficialCatalogLoadsAllSnapshotCards()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);

        Assert.Equal("https://playloltcg.com/card.html", catalog.Source);
        Assert.Equal("2026-04-27", catalog.FetchedAt);
        Assert.Equal(1009, catalog.Total);
        Assert.Equal(1009, catalog.Cards.Count);
    }

    [Fact]
    public async Task FunctionalUnitsMatchCurrentBaselineCounts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var summary = FunctionalUnitBuilder.Summarize(units);

        Assert.Equal(1009, summary.OfficialEntries);
        Assert.Equal(811, summary.FunctionalUnits);
        Assert.Equal(113, summary.DuplicateGroups);
        Assert.Equal(311, summary.DuplicateEntries);
        Assert.Equal(198, summary.SavedLogicImplementations);
    }

    [Fact]
    public async Task OfficialCatalogSchemaValidationCoversAllSnapshotCards()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var result = OfficialCardSchemaValidator.Validate(catalog);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Violations.Take(10)));
        Assert.Equal(1009, result.OfficialEntries);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task FunctionalUnitIdsAreStableUniqueAndComplete()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var first = FunctionalUnitReporter.Build(FunctionalUnitBuilder.Build(catalog.Cards));
        var second = FunctionalUnitReporter.Build(FunctionalUnitBuilder.Build(catalog.Cards));

        Assert.Equal(1009, first.OfficialEntries);
        Assert.Equal(811, first.FunctionalUnits);
        Assert.True(first.IdsAreUnique);
        Assert.All(first.Units, unit =>
        {
            Assert.StartsWith("FU-", unit.Id, StringComparison.Ordinal);
            Assert.Equal(13, unit.Id.Length);
            Assert.All(unit.Id[3..], character => Assert.Contains(character, "0123456789abcdef"));
            Assert.False(string.IsNullOrWhiteSpace(unit.Signature));
        });
        Assert.Equal(
            first.Units.Select(unit => $"{unit.Id}|{unit.RepresentativeNo}|{unit.Signature}"),
            second.Units.Select(unit => $"{unit.Id}|{unit.RepresentativeNo}|{unit.Signature}"));
    }

    [Fact]
    public async Task BehaviorSpecsCoverEveryOfficialCardWithExplicitStatusAndReason()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var report = BehaviorSpecCatalogBuilder.BuildReport(specs);

        Assert.Equal(1009, report.OfficialEntries);
        Assert.Equal(1009, report.BehaviorSpecs);
        Assert.Empty(report.MissingReasonCardNos);
        Assert.Equal(1009, report.StatusCounts[BehaviorImplementationStatuses.Implemented]);
        Assert.False(report.StatusCounts.ContainsKey(BehaviorImplementationStatuses.ManualRuleRequired));
        Assert.False(report.StatusCounts.ContainsKey(BehaviorImplementationStatuses.Unimplemented));
        Assert.Equal(1009, report.ConformanceTierCounts[BehaviorConformanceTiers.RepresentativeRulePass]);
        Assert.False(report.ConformanceTierCounts.ContainsKey(BehaviorConformanceTiers.FullOfficialRulePass));
        Assert.Contains(BehaviorImplementationStatuses.Implemented, report.StatusCounts.Keys);
        var allowedStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            BehaviorImplementationStatuses.Implemented,
            BehaviorImplementationStatuses.ManualRuleRequired,
            BehaviorImplementationStatuses.Unimplemented
        };
        Assert.All(specs, spec =>
        {
            Assert.True(allowedStatuses.Contains(spec.Status), $"Unexpected status '{spec.Status}' for {spec.CardNo}.");
            Assert.False(string.IsNullOrWhiteSpace(spec.FunctionalUnitId));
            Assert.False(string.IsNullOrWhiteSpace(spec.Reason));
            Assert.False(string.IsNullOrWhiteSpace(spec.ConformanceTier));
            Assert.False(string.IsNullOrWhiteSpace(spec.ConformanceReason));
        });

        var drawSpec = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·087/221", StringComparison.Ordinal));
        Assert.Equal(BehaviorImplementationStatuses.Implemented, drawSpec.Status);
        Assert.Contains(drawSpec.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.Draw, StringComparison.Ordinal));

        var runeSpec = specs.First(spec => string.Equals(spec.CardCategoryName, "符文", StringComparison.Ordinal));
        Assert.Equal(BehaviorImplementationStatuses.Implemented, runeSpec.Status);
        Assert.Equal(OfficialRuleDomainBehaviorCatalog.RuneResourceDomainEffectKind, runeSpec.ImplementedEffectKind);
        Assert.Contains("P6 rune resource domain", runeSpec.Reason, StringComparison.Ordinal);

        var tokenSpec = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL·T02", StringComparison.Ordinal));
        Assert.Equal(BehaviorImplementationStatuses.Implemented, tokenSpec.Status);
        Assert.Equal(OfficialRuleDomainBehaviorCatalog.TokenFactoryDomainEffectKind, tokenSpec.ImplementedEffectKind);
        Assert.Contains("P6 token factory domain", tokenSpec.Reason, StringComparison.Ordinal);

        var legendActionSpec = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-237/219", StringComparison.Ordinal));
        Assert.Equal(BehaviorImplementationStatuses.Implemented, legendActionSpec.Status);
        Assert.Equal(OfficialRuleDomainBehaviorCatalog.LegendActionDomainEffectKind, legendActionSpec.ImplementedEffectKind);
        Assert.Contains("P7.9 legend action domain", legendActionSpec.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImplementedBehaviorSpecsReferenceOfficialCardsAndStayWithinFunctionalUnits()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var officialCardNos = catalog.Cards
            .Select(card => card.CardNo)
            .ToHashSet(StringComparer.Ordinal);
        var unitByCardNo = units
            .SelectMany(unit => unit.Cards.Select(card => new KeyValuePair<string, FunctionalUnit>(card.CardNo, unit)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var implementedSpecs = specs
            .Where(spec => string.Equals(spec.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            .OrderBy(spec => spec.CardNo, StringComparer.Ordinal)
            .ToArray();

        var missingImplementationReferences = implementedSpecs
            .Where(spec => string.IsNullOrWhiteSpace(spec.ImplementedByCardNo)
                || !officialCardNos.Contains(spec.ImplementedByCardNo!))
            .Select(spec => $"{spec.CardNo}->{(string.IsNullOrWhiteSpace(spec.ImplementedByCardNo) ? "<missing>" : spec.ImplementedByCardNo)}")
            .ToArray();
        Assert.Empty(missingImplementationReferences);

        var missingImplementedEffectKinds = implementedSpecs
            .Where(spec => string.IsNullOrWhiteSpace(spec.ImplementedEffectKind))
            .Select(spec => spec.CardNo)
            .ToArray();
        Assert.Empty(missingImplementedEffectKinds);

        var mismatchedFunctionalUnitIds = implementedSpecs
            .Where(spec => unitByCardNo.TryGetValue(spec.CardNo, out var unit)
                && !string.Equals(spec.FunctionalUnitId, unit.Id, StringComparison.Ordinal))
            .Select(spec => $"{spec.CardNo}:{spec.FunctionalUnitId}")
            .ToArray();
        Assert.Empty(mismatchedFunctionalUnitIds);

        var crossUnitDuplicateImplementationReferences = implementedSpecs
            .Where(spec => !string.IsNullOrWhiteSpace(spec.ImplementedByCardNo)
                && unitByCardNo.TryGetValue(spec.CardNo, out var specUnit)
                && specUnit.Size > 1
                && unitByCardNo.TryGetValue(spec.ImplementedByCardNo!, out var implementationUnit)
                && !string.Equals(specUnit.Id, implementationUnit.Id, StringComparison.Ordinal))
            .Select(spec => $"{spec.CardNo}->{spec.ImplementedByCardNo}")
            .ToArray();
        Assert.Empty(crossUnitDuplicateImplementationReferences);
    }

    [Fact]
    public async Task KeywordCoverageReportExposesDeferredKeywordFamilies()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var report = KeywordCoverageReporter.Build(specs);

        Assert.Equal(1009, report.BehaviorSpecs);
        Assert.Equal(6, report.Families.Count);
        Assert.True(report.CardsWithKeywordProfiles > 0);
        Assert.True(report.StatusCounts[EquipmentKeywordProfileStatuses.RecognizedDeferred] > 0);
        Assert.True(report.StatusCounts[EquipmentKeywordProfileStatuses.ImplementedRepresentative] > 0);

        var equipment = Assert.Single(report.Families, family => string.Equals(family.Family, "equipment", StringComparison.Ordinal));
        Assert.True(equipment.StatusCounts[EquipmentKeywordProfileStatuses.ImplementedRepresentative] > 0);
        Assert.True(equipment.StatusCounts[EquipmentKeywordProfileStatuses.RecognizedDeferred] > 0);
        Assert.NotEmpty(equipment.DeferredCards);
        Assert.All(
            equipment.DeferredCards,
            row => Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, row.Status));
        Assert.Contains(
            equipment.DeferredCards,
            row => row.Keywords.Contains(CardEquipmentKeywordNames.Agile, StringComparer.Ordinal)
                || row.Keywords.Contains(CardEquipmentKeywordNames.Tempered, StringComparer.Ordinal)
                || row.Keywords.Contains(CardEquipmentKeywordNames.Weapon, StringComparer.Ordinal));

        var interaction = Assert.Single(report.Families, family => string.Equals(family.Family, "interaction", StringComparison.Ordinal));
        Assert.Contains(
            interaction.StatusCounts.Keys,
            status => string.Equals(status, InteractionKeywordProfileStatuses.Implemented, StringComparison.Ordinal));
        Assert.NotEmpty(interaction.DeferredCards);

        Assert.Contains(
            report.Families,
            family => family.DeferredCards.Any(row => row.Keywords.Count > 0 && !string.IsNullOrWhiteSpace(row.Reason)));
    }

    [Fact]
    public async Task P79ProductCatalogExposesRepresentativesWithoutClaimingFullOfficialRulePass()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var implementedSpecs = specs
            .Where(spec => string.Equals(spec.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            .ToArray();
        var manualDeferredSpecs = specs
            .Where(spec => string.Equals(spec.Status, BehaviorImplementationStatuses.ManualRuleRequired, StringComparison.Ordinal))
            .ToArray();
        var blockedSpecs = specs
            .Where(spec => string.Equals(spec.Status, BehaviorImplementationStatuses.Unimplemented, StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(1009, implementedSpecs.Length);
        Assert.Equal(811, implementedSpecs.Select(spec => spec.FunctionalUnitId).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(manualDeferredSpecs);
        Assert.Empty(blockedSpecs);
        Assert.Equal(
            1009,
            specs.Count(spec => string.Equals(
                spec.ConformanceTier,
                BehaviorConformanceTiers.RepresentativeRulePass,
                StringComparison.Ordinal)));
        Assert.DoesNotContain(specs, spec => string.Equals(
            spec.ConformanceTier,
            BehaviorConformanceTiers.FullOfficialRulePass,
            StringComparison.Ordinal));
        Assert.All(implementedSpecs, spec => Assert.Contains("Representative rule pass", spec.ConformanceReason, StringComparison.Ordinal));
        Assert.Equal(106, implementedSpecs.Count(spec => string.Equals(spec.CardCategoryName, "传奇", StringComparison.Ordinal)));
        Assert.Equal(57, implementedSpecs.Count(spec => string.Equals(spec.CardCategoryName, "战场", StringComparison.Ordinal)));
        Assert.All(implementedSpecs, spec =>
        {
            Assert.False(string.IsNullOrWhiteSpace(spec.ImplementedByCardNo));
            Assert.False(string.IsNullOrWhiteSpace(spec.ImplementedEffectKind));
        });
    }

    [Fact]
    public async Task P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var runeSpecs = specs
            .Where(spec => string.Equals(spec.CardCategoryName, "符文", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(48, runeSpecs.Length);
        Assert.Equal(6, runeSpecs.Select(spec => spec.FunctionalUnitId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(runeSpecs, spec =>
        {
            Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
            Assert.Equal(OfficialRuleDomainBehaviorCatalog.RuneResourceDomainEffectKind, spec.ImplementedEffectKind);
            Assert.Equal(spec.CardNo, spec.ImplementedByCardNo);
            Assert.Contains("rune call", spec.Reason, StringComparison.Ordinal);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out _));
        });
    }

    [Fact]
    public async Task P6TokenFactoryDomainMapsAllTokenEntriesWithoutMakingTokensPlayableCards()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var tokenSpecs = specs
            .Where(spec => spec.CardCategoryName.StartsWith("指示物", StringComparison.Ordinal))
            .ToArray();
        var definitions = P6TokenFactoryCatalog.GetAll();

        Assert.Equal(13, tokenSpecs.Length);
        Assert.Equal(13, tokenSpecs.Select(spec => spec.FunctionalUnitId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(9, tokenSpecs.Count(spec => string.Equals(spec.CardCategoryName, "指示物单位", StringComparison.Ordinal)));
        Assert.Equal(2, tokenSpecs.Count(spec => string.Equals(spec.CardCategoryName, "指示物装备", StringComparison.Ordinal)));
        Assert.Equal(2, tokenSpecs.Count(spec => string.Equals(spec.CardCategoryName, "指示物战场", StringComparison.Ordinal)));
        Assert.Equal(13, definitions.Count);
        Assert.Equal(13, definitions.Select(definition => definition.CardNo).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(13, tokenSpecs.Count(spec => P6TokenFactoryCatalog.TryGetByCardNo(spec.CardNo, out _)));
        Assert.All(tokenSpecs, spec =>
        {
            Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
            Assert.Equal(OfficialRuleDomainBehaviorCatalog.TokenFactoryDomainEffectKind, spec.ImplementedEffectKind);
            Assert.Equal(spec.CardNo, spec.ImplementedByCardNo);
            Assert.Contains("token factory domain", spec.Reason, StringComparison.Ordinal);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out _));

            var officialCard = catalog.Cards.Single(card => string.Equals(card.CardNo, spec.CardNo, StringComparison.Ordinal));
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(spec.CardNo, out var definition));
            Assert.Equal(officialCard.CardName, definition.CardName);
            Assert.Equal(officialCard.CardCategoryName, definition.CategoryName);
            Assert.Equal(officialCard.Power ?? 0, definition.DefaultPower);

            var tokenObject = definition.CreateObject(
                $"TOKEN-{definition.CardNo}",
                ownerId: "P1",
                controllerId: "P1");
            Assert.Equal(definition.CardNo, tokenObject.CardNo);
            Assert.Equal(definition.DefaultPower, tokenObject.Power);
            Assert.Equal("P1", tokenObject.OwnerId);
            Assert.Equal("P1", tokenObject.ControllerId);
            Assert.Equal(definition.Tags.Order(StringComparer.Ordinal), tokenObject.Tags.Order(StringComparer.Ordinal));

            if (string.Equals(definition.CategoryName, "指示物单位", StringComparison.Ordinal))
            {
                Assert.Contains(CardObjectTags.UnitCard, definition.Tags);
            }
            else if (string.Equals(definition.CategoryName, "指示物装备", StringComparison.Ordinal))
            {
                Assert.Contains(CardObjectTags.EquipmentCard, definition.Tags);
            }
            else
            {
                Assert.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, definition.Tags);
            }
        });

        var imageDefinition = definitions.Single(definition => string.Equals(definition.CardNo, "UNL·T06", StringComparison.Ordinal));
        Assert.True(imageDefinition.RequiresCopySource);
        Assert.Contains(P6TokenFactoryCatalog.CopySourceRequiredTag, imageDefinition.Tags);
    }

    [Fact]
    public void P6TokenFactoryMarksOnlyOfficialMinionTokenFamily()
    {
        var definitions = P6TokenFactoryCatalog.GetAll();
        var minionTokenCardNos = new HashSet<string>(StringComparer.Ordinal)
        {
            "OGN·271/298",
            "OGN·272/298",
            "OGN·273/298"
        };

        Assert.All(
            definitions.Where(definition => minionTokenCardNos.Contains(definition.CardNo)),
            definition =>
            {
                Assert.Equal("随从", definition.TokenFamilyName);
                Assert.Contains(CardObjectTags.UnitCard, definition.Tags);
                Assert.Contains(CardObjectTags.MinionTokenFamily, definition.Tags);

                var tokenObject = definition.CreateObject(
                    $"TOKEN-{definition.CardNo}",
                    ownerId: "P1",
                    controllerId: "P1");
                Assert.Contains(CardObjectTags.MinionTokenFamily, tokenObject.Tags);
            });

        Assert.All(
            definitions.Where(definition => !minionTokenCardNos.Contains(definition.CardNo)),
            definition => Assert.DoesNotContain(CardObjectTags.MinionTokenFamily, definition.Tags));
    }

    [Fact]
    public async Task P6FunctionalUnitCoverageAuditsSameTextVariantsAndReprints()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var coverage = FunctionalUnitBehaviorCoverageReporter.Build(units, specs);

        Assert.Equal(811, coverage.FunctionalUnits);
        Assert.Equal(811, coverage.ImplementedUnits);
        Assert.Equal(0, coverage.ManualRuleRequiredUnits);
        Assert.Equal(0, coverage.UnimplementedUnits);
        Assert.Equal(113, coverage.DuplicateGroups);
        Assert.Equal(113, coverage.ImplementedDuplicateGroups);
        Assert.Equal(311, coverage.ImplementedDuplicateEntries);
        Assert.Equal(0, coverage.PendingDuplicateGroups);
        Assert.Equal(0, coverage.PendingDuplicateEntries);

        var implementedDuplicateRows = coverage.Units
            .Where(row => row.IsDuplicateGroup
                && string.Equals(row.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            .ToArray();
        Assert.All(implementedDuplicateRows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.ImplementedByCardNo));
            Assert.False(string.IsNullOrWhiteSpace(row.ImplementedEffectKind));
            Assert.Contains(row.ImplementedByCardNo!, row.CardNos);
        });

        var pendingDuplicateCategories = coverage.Units
            .Where(row => row.IsDuplicateGroup
                && !string.Equals(row.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal))
            .Select(row => row.Category)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(pendingDuplicateCategories);
    }

    [Fact]
    public async Task P6HighFrequencyTemplateFamiliesReportEntryAndFunctionalUnitCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var report = BehaviorTemplateFamilyCoverageReporter.Build(
            specs,
            [
                BehaviorTemplateIds.Draw,
                BehaviorTemplateIds.Damage,
                BehaviorTemplateIds.Destroy,
                BehaviorTemplateIds.Stun
            ]);

        AssertFamily(report, BehaviorTemplateIds.Draw, 131, 131, 0, 0, 114, 114, 0);
        AssertFamily(report, BehaviorTemplateIds.Damage, 148, 148, 0, 0, 129, 129, 0);
        AssertFamily(report, BehaviorTemplateIds.Destroy, 127, 127, 0, 0, 118, 118, 0);
        AssertFamily(report, BehaviorTemplateIds.Stun, 33, 33, 0, 0, 29, 29, 0);
        Assert.All(report.Families, family =>
        {
            Assert.Equal(family.Entries, family.ImplementedEntries + family.ManualRuleRequiredEntries + family.UnimplementedEntries);
            Assert.Equal(family.FunctionalUnits, family.ImplementedFunctionalUnits + family.PendingFunctionalUnits);
            Assert.True(family.ImplementedEntries > 0, $"{family.TemplateId} should have an implemented representative.");
        });
    }

    [Fact]
    public async Task P6SecondaryTemplateFamiliesReportEntryAndFunctionalUnitCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var report = BehaviorTemplateFamilyCoverageReporter.Build(
            specs,
            [
                BehaviorTemplateIds.Recall,
                BehaviorTemplateIds.Move,
                BehaviorTemplateIds.Recycle,
                BehaviorTemplateIds.Banish,
                BehaviorTemplateIds.TempMight,
                BehaviorTemplateIds.Boon
            ]);

        AssertFamily(report, BehaviorTemplateIds.Recall, 49, 49, 0, 0, 43, 43, 0);
        AssertFamily(report, BehaviorTemplateIds.Move, 136, 136, 0, 0, 111, 111, 0);
        AssertFamily(report, BehaviorTemplateIds.Recycle, 63, 63, 0, 0, 51, 51, 0);
        AssertFamily(report, BehaviorTemplateIds.Banish, 11, 11, 0, 0, 9, 9, 0);
        AssertFamily(report, BehaviorTemplateIds.TempMight, 292, 292, 0, 0, 230, 230, 0);
        AssertFamily(report, BehaviorTemplateIds.Boon, 66, 66, 0, 0, 48, 48, 0);
        Assert.All(report.Families, family =>
        {
            Assert.Equal(family.Entries, family.ImplementedEntries + family.ManualRuleRequiredEntries + family.UnimplementedEntries);
            Assert.Equal(family.FunctionalUnits, family.ImplementedFunctionalUnits + family.PendingFunctionalUnits);
            Assert.True(family.ImplementedEntries > 0, $"{family.TemplateId} should have an implemented representative.");
        });
    }

    [Fact]
    public async Task P6InteractionKeywordFamiliesReportSpecAndExecutionBoundaryCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var rows = BuildInteractionKeywordCoverageRows(
            specs,
            [
                CardInteractionKeywordNames.Standby,
                CardInteractionKeywordNames.Echo,
                CardInteractionKeywordNames.Ambush
            ]);

        AssertInteractionKeywordCoverage(
            rows,
            CardInteractionKeywordNames.Standby,
            entries: 53,
            specImplementedEntries: 53,
            functionalUnits: 43,
            specImplementedFunctionalUnits: 43,
            profileImplementedEntries: 0,
            profileDeferredEntries: 53,
            profileImplementedFunctionalUnits: 0,
            profileDeferredFunctionalUnits: 43);
        AssertInteractionKeywordCoverage(
            rows,
            CardInteractionKeywordNames.Echo,
            entries: 24,
            specImplementedEntries: 24,
            functionalUnits: 24,
            specImplementedFunctionalUnits: 24,
            profileImplementedEntries: 10,
            profileDeferredEntries: 14,
            profileImplementedFunctionalUnits: 10,
            profileDeferredFunctionalUnits: 14);
        AssertInteractionKeywordCoverage(
            rows,
            CardInteractionKeywordNames.Ambush,
            entries: 18,
            specImplementedEntries: 18,
            functionalUnits: 18,
            specImplementedFunctionalUnits: 18,
            profileImplementedEntries: 0,
            profileDeferredEntries: 18,
            profileImplementedFunctionalUnits: 0,
            profileDeferredFunctionalUnits: 18);
        Assert.All(rows, row =>
        {
            Assert.Equal(row.Entries, row.ProfileImplementedEntries + row.ProfileDeferredEntries);
            Assert.Equal(row.FunctionalUnits, row.ProfileImplementedFunctionalUnits + row.ProfileDeferredFunctionalUnits);
        });
    }

    [Fact]
    public async Task P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var rows = BuildEquipmentKeywordCoverageRows(
            specs,
            [
                CardEquipmentKeywordNames.Assemble,
                CardEquipmentKeywordNames.Agile,
                CardEquipmentKeywordNames.Tempered,
                CardEquipmentKeywordNames.Weapon
            ]);

        AssertEquipmentKeywordCoverage(
            rows,
            CardEquipmentKeywordNames.Assemble,
            entries: 32,
            specImplementedEntries: 32,
            functionalUnits: 31,
            specImplementedFunctionalUnits: 31,
            profileImplementedEntries: 32,
            profileDeferredEntries: 0,
            profileImplementedFunctionalUnits: 31,
            profileDeferredFunctionalUnits: 0);
        AssertEquipmentKeywordCoverage(
            rows,
            CardEquipmentKeywordNames.Agile,
            entries: 4,
            specImplementedEntries: 4,
            functionalUnits: 4,
            specImplementedFunctionalUnits: 4,
            profileImplementedEntries: 0,
            profileDeferredEntries: 4,
            profileImplementedFunctionalUnits: 0,
            profileDeferredFunctionalUnits: 4);
        AssertEquipmentKeywordCoverage(
            rows,
            CardEquipmentKeywordNames.Tempered,
            entries: 16,
            specImplementedEntries: 16,
            functionalUnits: 11,
            specImplementedFunctionalUnits: 11,
            profileImplementedEntries: 0,
            profileDeferredEntries: 16,
            profileImplementedFunctionalUnits: 0,
            profileDeferredFunctionalUnits: 11);

        var weaponRow = Assert.Single(rows, row => string.Equals(row.Keyword, CardEquipmentKeywordNames.Weapon, StringComparison.Ordinal));
        Assert.True(weaponRow.Entries > 0);
        Assert.Equal(0, weaponRow.ProfileImplementedEntries);
        Assert.Equal(weaponRow.Entries, weaponRow.ProfileDeferredEntries);
        Assert.Equal(0, weaponRow.ProfileImplementedFunctionalUnits);
        Assert.Equal(weaponRow.FunctionalUnits, weaponRow.ProfileDeferredFunctionalUnits);

        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·011/221", out var takeUpDefinition));
        var attachmentProfile = CardEquipmentKeywordRules.BuildAttachmentProfile(takeUpDefinition);
        Assert.True(attachmentProfile.CanAttachOrDetachWeapon);
        Assert.Equal(EquipmentAttachmentProfileStatuses.ImplementedRepresentative, attachmentProfile.Status);
        Assert.Equal(1, attachmentProfile.DrawCount);

        Assert.All(rows, row =>
        {
            Assert.Equal(row.Entries, row.ProfileImplementedEntries + row.ProfileDeferredEntries);
            Assert.Equal(row.FunctionalUnits, row.ProfileImplementedFunctionalUnits + row.ProfileDeferredFunctionalUnits);
        });
    }

    [Fact]
    public async Task P6ResourceAndExperienceFamiliesReportSpecAndExecutionBoundaryCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var rows = BuildResourceKeywordCoverageRows(
            specs,
            [
                CardResourceKeywordNames.Hunt,
                CardResourceKeywordNames.Level,
                CardResourceKeywordNames.Encourage
            ]);
        var experienceReport = BehaviorTemplateFamilyCoverageReporter.Build(
            specs,
            [BehaviorTemplateIds.GainExperience]);
        var experienceFamily = Assert.Single(experienceReport.Families);
        var experienceRows = specs
            .Where(spec => spec.TemplateIds.Contains(BehaviorTemplateIds.GainExperience, StringComparer.Ordinal))
            .Select(spec =>
            {
                CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition);
                return new
                {
                    Spec = spec,
                    Definition = definition
                };
            })
            .ToArray();
        var experienceUnitGroups = experienceRows
            .GroupBy(row => row.Spec.FunctionalUnitId, StringComparer.Ordinal)
            .ToArray();

        AssertResourceKeywordCoverage(
            rows,
            CardResourceKeywordNames.Hunt,
            entries: 14,
            specImplementedEntries: 14,
            functionalUnits: 14,
            specImplementedFunctionalUnits: 14,
            registryExecutionEntries: 14,
            registryExecutionFunctionalUnits: 14,
            profileDeferredEntries: 14,
            profileDeferredFunctionalUnits: 14);
        AssertResourceKeywordCoverage(
            rows,
            CardResourceKeywordNames.Level,
            entries: 18,
            specImplementedEntries: 18,
            functionalUnits: 17,
            specImplementedFunctionalUnits: 17,
            registryExecutionEntries: 5,
            registryExecutionFunctionalUnits: 5,
            profileDeferredEntries: 18,
            profileDeferredFunctionalUnits: 17);
        AssertResourceKeywordCoverage(
            rows,
            CardResourceKeywordNames.Encourage,
            entries: 15,
            specImplementedEntries: 15,
            functionalUnits: 10,
            specImplementedFunctionalUnits: 10,
            registryExecutionEntries: 5,
            registryExecutionFunctionalUnits: 5,
            profileDeferredEntries: 15,
            profileDeferredFunctionalUnits: 10);
        AssertFamily(
            experienceReport,
            BehaviorTemplateIds.GainExperience,
            entries: 51,
            implementedEntries: 51,
            manualRuleRequiredEntries: 0,
            unimplementedEntries: 0,
            functionalUnits: 47,
            implementedFunctionalUnits: 47,
            pendingFunctionalUnits: 0);
        Assert.Equal(6, experienceRows.Count(row => HasExperienceBehavior(row.Definition)));
        Assert.Equal(6, experienceUnitGroups.Count(group => group.Any(row => HasExperienceBehavior(row.Definition))));
        Assert.All(rows, row =>
        {
            Assert.Equal(row.Entries, row.ProfileDeferredEntries);
            Assert.Equal(row.FunctionalUnits, row.ProfileDeferredFunctionalUnits);
            Assert.True(row.RegistryExecutionEntries > 0, $"{row.Keyword} should have a P2-P5 representative boundary.");
        });
    }

    [Fact]
    public async Task P6LifecycleTriggerReplacementFamiliesReportSpecAndExecutionBoundaryCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var lifecycleRows = BuildLifecycleKeywordCoverageRows(
            specs,
            [
                CardLifecycleKeywordNames.Ephemeral,
                CardLifecycleKeywordNames.LastBreath,
                CardLifecycleKeywordNames.Predict
            ]);
        var timingRows = BuildTimingSurfaceCoverageRows(
            specs,
            [
                TimingSurfaceNames.Trigger,
                TimingSurfaceNames.Replacement
            ]);

        AssertLifecycleKeywordCoverage(
            lifecycleRows,
            CardLifecycleKeywordNames.Ephemeral,
            entries: 30,
            specImplementedEntries: 30,
            functionalUnits: 26,
            specImplementedFunctionalUnits: 26,
            profileImplementedEntries: 29,
            profileDelegatedEntries: 0,
            profileDeferredEntries: 1,
            profileImplementedFunctionalUnits: 25,
            profileDelegatedFunctionalUnits: 0,
            profileDeferredFunctionalUnits: 1);
        AssertLifecycleKeywordCoverage(
            lifecycleRows,
            CardLifecycleKeywordNames.LastBreath,
            entries: 25,
            specImplementedEntries: 25,
            functionalUnits: 25,
            specImplementedFunctionalUnits: 25,
            profileImplementedEntries: 0,
            profileDelegatedEntries: 0,
            profileDeferredEntries: 25,
            profileImplementedFunctionalUnits: 0,
            profileDelegatedFunctionalUnits: 0,
            profileDeferredFunctionalUnits: 25);
        AssertLifecycleKeywordCoverage(
            lifecycleRows,
            CardLifecycleKeywordNames.Predict,
            entries: 12,
            specImplementedEntries: 12,
            functionalUnits: 10,
            specImplementedFunctionalUnits: 10,
            profileImplementedEntries: 0,
            profileDelegatedEntries: 7,
            profileDeferredEntries: 5,
            profileImplementedFunctionalUnits: 0,
            profileDelegatedFunctionalUnits: 7,
            profileDeferredFunctionalUnits: 3);
        AssertTimingSurfaceCoverage(
            timingRows,
            TimingSurfaceNames.Trigger,
            entries: 530,
            specImplementedEntries: 530,
            manualRuleRequiredEntries: 0,
            unimplementedEntries: 0,
            functionalUnits: 423,
            specImplementedFunctionalUnits: 423,
            pendingFunctionalUnits: 0);
        AssertTimingSurfaceCoverage(
            timingRows,
            TimingSurfaceNames.Replacement,
            entries: 28,
            specImplementedEntries: 28,
            manualRuleRequiredEntries: 0,
            unimplementedEntries: 0,
            functionalUnits: 24,
            specImplementedFunctionalUnits: 24,
            pendingFunctionalUnits: 0);
        Assert.All(lifecycleRows, row =>
        {
            Assert.Equal(row.Entries, row.ProfileImplementedEntries + row.ProfileDelegatedEntries + row.ProfileDeferredEntries);
            Assert.Equal(
                row.FunctionalUnits,
                row.ProfileImplementedFunctionalUnits + row.ProfileDelegatedFunctionalUnits + row.ProfileDeferredFunctionalUnits);
        });
        Assert.All(timingRows, row =>
        {
            Assert.Equal(row.Entries, row.SpecImplementedEntries + row.ManualRuleRequiredEntries + row.UnimplementedEntries);
            Assert.Equal(row.FunctionalUnits, row.SpecImplementedFunctionalUnits + row.PendingFunctionalUnits);
        });
    }

    [Fact]
    public async Task P6LegendRuleDomainSurfacesReportManualBoundaryCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var legendSpecs = specs
            .Where(spec => string.Equals(spec.CardCategoryName, "传奇", StringComparison.Ordinal))
            .ToArray();
        var unitGroups = legendSpecs
            .GroupBy(spec => spec.FunctionalUnitId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(106, legendSpecs.Length);
        Assert.Equal(44, unitGroups.Length);
        Assert.Equal(40, legendSpecs.Select(spec => spec.CardName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            0,
            legendSpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal)));
        Assert.Equal(
            0,
            unitGroups.Count(group => group.All(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal))));
        Assert.Equal(
            106,
            legendSpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal)));
        Assert.Equal(
            44,
            unitGroups.Count(group => group.Any(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal))));
        Assert.Equal(106, legendSpecs.Count(spec => !string.IsNullOrWhiteSpace(spec.OfficialText)));
        AssertRuleDomainSurface(legendSpecs, unitGroups, spec => spec.ActivatedAbilities.Count > 0, entries: 47, functionalUnits: 18);
        AssertRuleDomainSurface(legendSpecs, unitGroups, spec => spec.Triggers.Count > 0, entries: 58, functionalUnits: 23);
        AssertRuleDomainSurface(legendSpecs, unitGroups, spec => spec.Replacements.Count > 0, entries: 3, functionalUnits: 1);
        AssertRuleDomainSurface(legendSpecs, unitGroups, spec => spec.StaticAbilities.Count > 0, entries: 48, functionalUnits: 20);
        AssertRuleDomainSurface(legendSpecs, unitGroups, spec => spec.Keywords.Count > 0, entries: 48, functionalUnits: 20);
        AssertRuleDomainSurface(legendSpecs, unitGroups, spec => spec.TemplateIds.Count > 0, entries: 71, functionalUnits: 30);

        var implementedLegendActionSpecs = legendSpecs
            .Where(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            [
                "FND-249/298",
                "FND-251/298",
                "FND-259/298",
                "FND-265/298",
                "OGN·247/298",
                "OGN·249/298",
                "OGN·251/298",
                "OGN·253/298",
                "OGN·255/298",
                "OGN·257/298",
                "OGN·259/298",
                "OGN·261/298",
                "OGN·263/298",
                "OGN·263a/298",
                "OGN·265/298",
                "OGN·267/298",
                "OGN·269/298",
                "OGN·299*/298",
                "OGN·299/298",
                "OGN·300*/298",
                "OGN·300/298",
                "OGN·301*/298",
                "OGN·301/298",
                "OGN·302*/298",
                "OGN·302/298",
                "OGN·303*/298",
                "OGN·303/298",
                "OGN·304*/298",
                "OGN·304/298",
                "OGN·305*/298",
                "OGN·305/298",
                "OGN·306*/298",
                "OGN·306/298",
                "OGN·307*/298",
                "OGN·307/298",
                "OGN·308*/298",
                "OGN·308/298",
                "OGN·309*/298",
                "OGN·309/298",
                "OGN·310*/298",
                "OGN·310/298",
                "OGS·017/024",
                "OGS·019/024",
                "OGS·021/024",
                "OGS·023/024",
                "SFD·181/221",
                "SFD·183/221",
                "SFD·185/221",
                "SFD·187/221",
                "SFD·189/221",
                "SFD·193/221",
                "SFD·195/221",
                "SFD·195a/221·P",
                "SFD·197/221",
                "SFD·199/221",
                "SFD·201/221",
                "SFD·203/221",
                "SFD·205/221",
                "SFD·240/221",
                "SFD·241/221",
                "SFD·242/221",
                "SFD·243/221",
                "SFD·244/221",
                "SFD·245/221",
                "SFD·246/221",
                "SFD·247/221",
                "SFD·248/221",
                "SFD·249/221",
                "SFD·250/221",
                "SFD·251/221",
                "UNL-181/219",
                "UNL-183/219",
                "UNL-185/219",
                "UNL-187/219",
                "UNL-189/219",
                "UNL-191/219",
                "UNL-193/219",
                "UNL-195/219",
                "UNL-197/219",
                "UNL-199/219",
                "UNL-201/219",
                "UNL-203/219",
                "UNL-226*/219",
                "UNL-226/219",
                "UNL-227*/219",
                "UNL-227/219",
                "UNL-228*/219",
                "UNL-228/219",
                "UNL-229*/219",
                "UNL-229/219",
                "UNL-230*/219",
                "UNL-230/219",
                "UNL-231*/219",
                "UNL-231/219",
                "UNL-232*/219",
                "UNL-232/219",
                "UNL-233*/219",
                "UNL-233/219",
                "UNL-234*/219",
                "UNL-234/219",
                "UNL-235*/219",
                "UNL-235/219",
                "UNL-236*/219",
                "UNL-236/219",
                "UNL-237*/219",
                "UNL-237/219"
            ],
            implementedLegendActionSpecs
                .Select(spec => spec.CardNo)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.All(implementedLegendActionSpecs, spec =>
        {
            Assert.Equal(OfficialRuleDomainBehaviorCatalog.LegendActionDomainEffectKind, spec.ImplementedEffectKind);
            Assert.False(string.IsNullOrWhiteSpace(spec.ImplementedByCardNo));
            Assert.Contains("P7.9 legend action domain", spec.Reason, StringComparison.Ordinal);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out _));
        });

        var manualLegendSpecs = legendSpecs
            .Where(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal))
            .ToArray();
        Assert.All(manualLegendSpecs, spec =>
        {
            Assert.Contains("dedicated non-PLAY_CARD rule domain", spec.Reason, StringComparison.Ordinal);
            Assert.Null(spec.ImplementedEffectKind);
            Assert.Null(spec.ImplementedByCardNo);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out _));
        });
    }

    [Fact]
    public async Task P6BattlefieldRuleDomainSurfacesReportManualBoundaryCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var battlefieldSpecs = specs
            .Where(spec => string.Equals(spec.CardCategoryName, "战场", StringComparison.Ordinal))
            .ToArray();
        var unitGroups = battlefieldSpecs
            .GroupBy(spec => spec.FunctionalUnitId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(57, battlefieldSpecs.Length);
        Assert.Equal(54, unitGroups.Length);
        Assert.Equal(54, battlefieldSpecs.Select(spec => spec.CardName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            0,
            battlefieldSpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal)));
        Assert.Equal(
            0,
            unitGroups.Count(group => group.All(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal))));
        Assert.Equal(
            57,
            battlefieldSpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal)));
        Assert.Equal(
            54,
            unitGroups.Count(group => group.Any(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal))));
        Assert.Equal(57, battlefieldSpecs.Count(spec => !string.IsNullOrWhiteSpace(spec.OfficialText)));
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.ActivatedAbilities.Count > 0, entries: 3, functionalUnits: 3);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.Triggers.Count > 0, entries: 40, functionalUnits: 39);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.Replacements.Count > 0, entries: 1, functionalUnits: 1);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.StaticAbilities.Count > 0, entries: 16, functionalUnits: 15);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.Keywords.Count > 0, entries: 11, functionalUnits: 10);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.TemplateIds.Count > 0, entries: 34, functionalUnits: 34);

        var implementedBattlefieldSpecs = battlefieldSpecs
            .Where(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal))
            .OrderBy(spec => spec.CardNo, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["OGN·275/298", "OGN·276/298", "OGN·276a/298", "OGN·277/298", "OGN·278/298", "OGN·278a/298", "OGN·279/298", "OGN·280/298", "OGN·281/298", "OGN·282/298", "OGN·283/298", "OGN·284/298", "OGN·285/298", "OGN·286/298", "OGN·287/298", "OGN·288/298", "OGN·289/298", "OGN·290/298", "OGN·291/298", "OGN·292/298", "OGN·293/298", "OGN·293a/298", "OGN·294/298", "OGN·295/298", "OGN·296/298", "OGN·297/298", "OGN·298/298", "SFD·207/221", "SFD·208/221", "SFD·209/221", "SFD·210/221", "SFD·211/221", "SFD·212/221", "SFD·213/221", "SFD·214/221", "SFD·215/221", "SFD·216/221", "SFD·217/221", "SFD·218/221", "SFD·219/221", "SFD·220/221", "SFD·221/221", "UNL-205/219", "UNL-206/219", "UNL-207/219", "UNL-208/219", "UNL-209/219", "UNL-210/219", "UNL-211/219", "UNL-212/219", "UNL-213/219", "UNL-214/219", "UNL-215/219", "UNL-216/219", "UNL-217/219", "UNL-218/219", "UNL-219/219"], implementedBattlefieldSpecs.Select(spec => spec.CardNo).ToArray());
        Assert.All(implementedBattlefieldSpecs, spec =>
        {
            Assert.Equal(OfficialRuleDomainBehaviorCatalog.BattlefieldRuleDomainEffectKind, spec.ImplementedEffectKind);
            Assert.Equal(spec.CardNo, spec.ImplementedByCardNo);
            Assert.Contains("P7.9 battlefield rule domain", spec.Reason, StringComparison.Ordinal);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out _));
        });

        var manualBattlefieldSpecs = battlefieldSpecs
            .Where(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(manualBattlefieldSpecs);
        Assert.All(manualBattlefieldSpecs, spec =>
        {
            Assert.Contains("dedicated non-PLAY_CARD rule domain", spec.Reason, StringComparison.Ordinal);
            Assert.Null(spec.ImplementedEffectKind);
            Assert.Null(spec.ImplementedByCardNo);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out _));
        });
    }

    [Fact]
    public async Task P6CompletionAuditKeepsEveryFunctionalUnitImplementedOrExplicitlyDeferred()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var coverage = FunctionalUnitBehaviorCoverageReporter.Build(units, specs);

        Assert.Equal(1009, specs.Count);
        Assert.Equal(811, coverage.FunctionalUnits);
        Assert.Equal(811, coverage.ImplementedUnits);
        Assert.Equal(0, coverage.ManualRuleRequiredUnits);
        Assert.Equal(0, coverage.UnimplementedUnits);
        Assert.DoesNotContain(specs, spec => string.Equals(
            spec.Status,
            BehaviorImplementationStatuses.Unimplemented,
            StringComparison.Ordinal));

        var manualSpecs = specs
            .Where(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal))
            .ToArray();
        var manualCategories = manualSpecs
            .Select(spec => spec.CardCategoryName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(manualCategories);
        Assert.Empty(manualSpecs);
        Assert.All(manualSpecs, spec =>
        {
            Assert.Contains("dedicated non-PLAY_CARD rule domain", spec.Reason, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(spec.OfficialText));
            Assert.Null(spec.ImplementedEffectKind);
            Assert.Null(spec.ImplementedByCardNo);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out _));
        });

        var implementedSpecs = specs
            .Where(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(1009, implementedSpecs.Length);
        Assert.All(implementedSpecs, spec =>
        {
            Assert.False(string.IsNullOrWhiteSpace(spec.ImplementedEffectKind));
            Assert.False(string.IsNullOrWhiteSpace(spec.ImplementedByCardNo));
            var isImplementedNonPlayDomain = string.Equals(
                    spec.ImplementedEffectKind,
                    OfficialRuleDomainBehaviorCatalog.RuneResourceDomainEffectKind,
                    StringComparison.Ordinal)
                || string.Equals(
                    spec.ImplementedEffectKind,
                    OfficialRuleDomainBehaviorCatalog.TokenFactoryDomainEffectKind,
                    StringComparison.Ordinal)
                || string.Equals(
                    spec.ImplementedEffectKind,
                    OfficialRuleDomainBehaviorCatalog.LegendActionDomainEffectKind,
                    StringComparison.Ordinal)
                || string.Equals(
                    spec.ImplementedEffectKind,
                    OfficialRuleDomainBehaviorCatalog.BattlefieldRuleDomainEffectKind,
                    StringComparison.Ordinal);
            Assert.True(
                isImplementedNonPlayDomain || CardBehaviorRegistry.TryGetByCardNo(spec.ImplementedByCardNo!, out _),
                $"{spec.CardNo} has implemented effect {spec.ImplementedEffectKind} without registry or non-PLAY_CARD domain coverage.");
        });

        Assert.Empty(P6LegendAbilityCatalog.GetDeferredSurfaces());
        Assert.Equal(5, P6LegendAbilityCatalog.GetImplementedLegendActionSurfaces().Count);
        Assert.Empty(P6BattlefieldEffectCatalog.GetDeferredSurfaces());
        Assert.Equal(4, P6BattlefieldEffectCatalog.GetImplementedBattlefieldRuleSurfaces().Count);
        Assert.Empty(P6TokenFactoryCatalog.GetDeferredRuleSurfaces());
        Assert.Equal(3, P6TokenFactoryCatalog.GetImplementedRuleSurfaces().Count);
    }

    [Fact]
    public async Task RuleTextParserExtractsMinimumP3Fields()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);

        var rocketBarrage = RuleTextParser.Parse(Card(catalog, "SFD·077/221"));
        Assert.Contains(rocketBarrage.Keywords, keyword => string.Equals(keyword.Keyword, "回响", StringComparison.Ordinal));
        Assert.Contains(rocketBarrage.Cost.OptionalCosts, cost => cost.StartsWith("echo", StringComparison.Ordinal));
        Assert.Contains(rocketBarrage.Targets, target => string.Equals(target.Scope, "unit", StringComparison.Ordinal));
        Assert.Contains(rocketBarrage.Targets, target => string.Equals(target.Scope, "equipment", StringComparison.Ordinal));
        Assert.Contains(rocketBarrage.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.Damage, StringComparison.Ordinal));
        Assert.Contains(rocketBarrage.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.Destroy, StringComparison.Ordinal));

        var scryingShell = RuleTextParser.Parse(Card(catalog, "UNL-161/219"));
        Assert.Contains(scryingShell.Keywords, keyword => string.Equals(keyword.Keyword, "预知", StringComparison.Ordinal));
        Assert.Contains(scryingShell.Keywords, keyword => string.Equals(keyword.Keyword, "迅捷", StringComparison.Ordinal));
        Assert.NotEmpty(scryingShell.ActivatedAbilities);
        Assert.Contains(scryingShell.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.TempMight, StringComparison.Ordinal));

        var windsongWing = RuleTextParser.Parse(Card(catalog, "SFD·138/221"));
        Assert.Contains(windsongWing.Keywords, keyword => string.Equals(keyword.Keyword, "待命", StringComparison.Ordinal));
        Assert.Contains(windsongWing.Triggers, trigger => string.Equals(trigger.Kind, "on-play", StringComparison.Ordinal));
        Assert.Contains(windsongWing.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.Recall, StringComparison.Ordinal));

        var covertSabotage = RuleTextParser.Parse(Card(catalog, "OGN·156/298"));
        Assert.Contains(covertSabotage.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.Recycle, StringComparison.Ordinal));

        var portalpalRescue = RuleTextParser.Parse(Card(catalog, "OGN·102/298"));
        Assert.Contains(portalpalRescue.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.Banish, StringComparison.Ordinal));

        var secretArtMercy = RuleTextParser.Parse(Card(catalog, "OGN·053/298"));
        Assert.Contains(secretArtMercy.Effects, effect => string.Equals(effect.TemplateId, BehaviorTemplateIds.Boon, StringComparison.Ordinal));
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        foreach (var cardNo in new[] { "SFD·085/221", "SFD·085a/221" })
        {
            var ornn = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            var aura = Assert.Single(ornn.StaticAuras);
            Assert.Equal(StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower, aura.Kind);
            Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
            Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", aura.Duration);
            Assert.Equal(StaticAuraTargetScopes.SourceObject, aura.TargetScope);
            Assert.Equal(StaticAuraParticipantScopes.FriendlyPublicFieldEquipment, aura.ParticipantScope);
            Assert.Equal(1, aura.PowerDeltaPerParticipant);
            Assert.Contains("每有一件友方装备", aura.Text, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, aura.Status);
        }

        var battlefield = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·294/298", StringComparison.Ordinal));
        var battlefieldAura = Assert.Single(battlefield.StaticAuras);
        Assert.Equal(StaticAuraKinds.BattlefieldAllUnitsPowerPlusOne, battlefieldAura.Kind);
        Assert.Equal(ContinuousEffectLayers.StaticAura, battlefieldAura.Layer);
        Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", battlefieldAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SameBattlefieldUnits, battlefieldAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldPublicUnits, battlefieldAura.ParticipantScope);
        Assert.Equal(1, battlefieldAura.PowerDeltaPerParticipant);
        Assert.Contains("此处的所有单位", battlefieldAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, battlefieldAura.Status);

        var brushBattlefield = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL·T03", StringComparison.Ordinal));
        var brushAura = Assert.Single(brushBattlefield.StaticAuras);
        Assert.Equal(StaticAuraKinds.BattlefieldFilteredUnitsPower, brushAura.Kind);
        Assert.Equal(ContinuousEffectLayers.StaticAura, brushAura.Layer);
        Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", brushAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SameBattlefieldFilteredUnits, brushAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldFilteredPublicUnits, brushAura.ParticipantScope);
        Assert.Equal(1, brushAura.PowerDeltaPerParticipant);
        Assert.Equal(
            StaticAuraTargetFilters.AnyPrefix
            + StaticAuraTargetFilters.TagPrefix + "鸟类"
            + "|"
            + StaticAuraTargetFilters.TagPrefix + "猫科"
            + "|"
            + StaticAuraTargetFilters.TagPrefix + "犬形"
            + "|"
            + StaticAuraTargetFilters.TagPrefix + "魄罗"
            + "|"
            + StaticAuraTargetFilters.CardNamePrefix + "艾翁",
            brushAura.TargetFilter);
        Assert.Contains("此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位", brushAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, brushAura.Status);

        var blackflameAltar = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-208/219", StringComparison.Ordinal));
        var blackflameAura = Assert.Single(blackflameAltar.StaticAuras);
        Assert.Equal(StaticAuraKinds.BattlefieldFilteredUnitsKeyword, blackflameAura.Kind);
        Assert.Equal(ContinuousEffectLayers.RuleText, blackflameAura.Layer);
        Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", blackflameAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SameBattlefieldFilteredUnits, blackflameAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldFilteredPublicUnits, blackflameAura.ParticipantScope);
        Assert.Equal(0, blackflameAura.PowerDeltaPerParticipant);
        Assert.Equal(StaticAuraTargetFilters.TagPrefix + "瞬息", blackflameAura.TargetFilter);
        Assert.Equal(CardCombatKeywordNames.Steadfast, blackflameAura.GrantedKeyword);
        Assert.Contains("此处拥有{{瞬息}}的单位获得{{坚守}}", blackflameAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, blackflameAura.Status);

        var windHill = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·297/298", StringComparison.Ordinal));
        var windHillAura = Assert.Single(windHill.StaticAuras);
        Assert.Equal(StaticAuraKinds.BattlefieldAllUnitsKeyword, windHillAura.Kind);
        Assert.Equal(ContinuousEffectLayers.RuleText, windHillAura.Layer);
        Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", windHillAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SameBattlefieldUnits, windHillAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldPublicUnits, windHillAura.ParticipantScope);
        Assert.Equal(0, windHillAura.PowerDeltaPerParticipant);
        Assert.Equal(CardCombatKeywordNames.Roam, windHillAura.GrantedKeyword);
        Assert.Contains("此处的单位获得{{游走}}", windHillAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, windHillAura.Status);

        var forbiddenWasteland = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-210/219", StringComparison.Ordinal));
        var forbiddenWastelandAura = Assert.Single(forbiddenWasteland.StaticAuras);
        Assert.Equal(StaticAuraKinds.BattlefieldIsolatedDefenderKeywordModifier, forbiddenWastelandAura.Kind);
        Assert.Equal(ContinuousEffectLayers.RuleText, forbiddenWastelandAura.Layer);
        Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", forbiddenWastelandAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SameBattlefieldIsolatedDefender, forbiddenWastelandAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldIsolatedDefender, forbiddenWastelandAura.ParticipantScope);
        Assert.Equal(-2, forbiddenWastelandAura.PowerDeltaPerParticipant);
        Assert.Equal(CardCombatKeywordNames.Steadfast, forbiddenWastelandAura.GrantedKeyword);
        Assert.Contains("如果防守此处的单位落单", forbiddenWastelandAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, forbiddenWastelandAura.Status);

        var ivernLegend = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-195/219", StringComparison.Ordinal));
        Assert.Empty(ivernLegend.StaticAuras);

        var petalPixie = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-076/219", StringComparison.Ordinal));
        var petalPixieAura = Assert.Single(petalPixie.StaticAuras);
        Assert.Equal(StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower, petalPixieAura.Kind);
        Assert.Equal(ContinuousEffectLayers.StaticAura, petalPixieAura.Layer);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", petalPixieAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SourceObject, petalPixieAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldFriendlyFilteredPublicUnits, petalPixieAura.ParticipantScope);
        Assert.Equal(1, petalPixieAura.PowerDeltaPerParticipant);
        Assert.Equal(StaticAuraTargetFilters.TagPrefix + "瞬息", petalPixieAura.TargetFilter);
        Assert.Contains("我所处的战场你每有一名拥有{{瞬息}}的单位", petalPixieAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, petalPixieAura.Status);

        foreach (var cardNo in new[] { "OGN·240/298", "OGN·240a/298" })
        {
            var sett = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            var aura = Assert.Single(sett.StaticAuras);
            Assert.Equal(StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower, aura.Kind);
            Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
            Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", aura.Duration);
            Assert.Equal(StaticAuraTargetScopes.SourceObject, aura.TargetScope);
            Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldFriendlyFilteredPublicUnits, aura.ParticipantScope);
            Assert.Equal(1, aura.PowerDeltaPerParticipant);
            Assert.Equal(StaticAuraTargetFilters.TagPrefix + "增益", aura.TargetFilter);
            Assert.Contains("我所处的战场每有一名拥有增益的友方单位", aura.Text, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, aura.Status);
        }

        var wiseElder = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·065/298", StringComparison.Ordinal));
        var wiseElderAura = Assert.Single(wiseElder.StaticAuras);
        Assert.Equal(StaticAuraKinds.SourceObjectFilteredPower, wiseElderAura.Kind);
        Assert.Equal(ContinuousEffectLayers.StaticAura, wiseElderAura.Layer);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", wiseElderAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SourceObject, wiseElderAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SourceObject, wiseElderAura.ParticipantScope);
        Assert.Equal(1, wiseElderAura.PowerDeltaPerParticipant);
        Assert.Equal(StaticAuraTargetFilters.TagPrefix + "增益", wiseElderAura.TargetFilter);
        Assert.Contains("如果我拥有增益", wiseElderAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, wiseElderAura.Status);

        foreach (var cardNo in new[] { "OGS·013/024", "SFD·236/221", "SFD·236*/221", "OGN·243/298", "OGN·243a/298" })
        {
            var source = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            var aura = Assert.Single(source.StaticAuras);
            Assert.Equal(StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne, aura.Kind);
            Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
            Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", aura.Duration);
            Assert.Equal(StaticAuraTargetScopes.SameBattlefieldOtherFriendlyUnits, aura.TargetScope);
            Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldOtherFriendlyPublicUnits, aura.ParticipantScope);
            Assert.Equal(1, aura.PowerDeltaPerParticipant);
            Assert.Contains("此处的其他友方单位", aura.Text, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, aura.Status);
        }

        var farronCaptain = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·015/298", StringComparison.Ordinal));
        var farronCaptainAura = Assert.Single(farronCaptain.StaticAuras);
        Assert.Equal(StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword, farronCaptainAura.Kind);
        Assert.Equal(ContinuousEffectLayers.RuleText, farronCaptainAura.Layer);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", farronCaptainAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.SameBattlefieldOtherFriendlyUnits, farronCaptainAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldOtherFriendlyPublicUnits, farronCaptainAura.ParticipantScope);
        Assert.Equal(0, farronCaptainAura.PowerDeltaPerParticipant);
        Assert.Equal(CardCombatKeywordNames.Assault, farronCaptainAura.GrantedKeyword);
        Assert.Contains("此处的其他友方单位获得{{强攻}}", farronCaptainAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, farronCaptainAura.Status);

        foreach (var cardNo in new[] { "UNL-147/219", "UNL-147a/219", "UNL-238/219" })
        {
            var source = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            var aura = Assert.Single(source.StaticAuras);
            Assert.Equal(StaticAuraKinds.OtherFriendlyUnitsPower, aura.Kind);
            Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
            Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", aura.Duration);
            Assert.Equal(StaticAuraTargetScopes.OtherFriendlyUnits, aura.TargetScope);
            Assert.Equal(StaticAuraParticipantScopes.OtherFriendlyPublicUnits, aura.ParticipantScope);
            Assert.Equal(2, aura.PowerDeltaPerParticipant);
            Assert.Contains("其他友方单位", aura.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("此处", aura.Text, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, aura.Status);
        }

        var soulShepherd = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-077/219", StringComparison.Ordinal));
        var tokenAura = Assert.Single(soulShepherd.StaticAuras);
        Assert.Equal(StaticAuraKinds.FriendlyFilteredUnitsPower, tokenAura.Kind);
        Assert.Equal(ContinuousEffectLayers.StaticAura, tokenAura.Layer);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", tokenAura.Duration);
        Assert.Equal(StaticAuraTargetScopes.FriendlyFilteredUnits, tokenAura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.FriendlyFilteredPublicUnits, tokenAura.ParticipantScope);
        Assert.Equal(1, tokenAura.PowerDeltaPerParticipant);
        Assert.Equal(StaticAuraTargetFilters.UnitToken, tokenAura.TargetFilter);
        Assert.Contains("你的指示物单位", tokenAura.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, tokenAura.Status);

        foreach (var cardNo in new[] { "SFD·089/221", "SFD·089a/221" })
        {
            var source = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            var aura = Assert.Single(source.StaticAuras);
            Assert.Equal(StaticAuraKinds.FriendlyFilteredUnitsPower, aura.Kind);
            Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
            Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", aura.Duration);
            Assert.Equal(StaticAuraTargetScopes.FriendlyFilteredUnits, aura.TargetScope);
            Assert.Equal(StaticAuraParticipantScopes.FriendlyFilteredPublicUnits, aura.ParticipantScope);
            Assert.Equal(1, aura.PowerDeltaPerParticipant);
            Assert.Equal(StaticAuraTargetFilters.TagPrefix + "机械", aura.TargetFilter);
            Assert.Contains("你的“机械”属性单位", aura.Text, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, aura.Status);
        }

        foreach (var cardNo in new[] { "OGN·151/298", "OGN·151a/298" })
        {
            var source = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            var aura = Assert.Single(source.StaticAuras);
            Assert.Equal(StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower, aura.Kind);
            Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
            Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", aura.Duration);
            Assert.Equal(StaticAuraTargetScopes.SameBattlefieldOtherFriendlyFilteredUnits, aura.TargetScope);
            Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldOtherFriendlyFilteredPublicUnits, aura.ParticipantScope);
            Assert.Equal(2, aura.PowerDeltaPerParticipant);
            Assert.Equal(StaticAuraTargetFilters.TagPrefix + "增益", aura.TargetFilter);
            Assert.Contains("我所在战场上其他拥有增益的友方单位", aura.Text, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, aura.Status);
        }

        var enthusiasticAnnouncer = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-043/219", StringComparison.Ordinal));
        Assert.Empty(enthusiasticAnnouncer.StaticAuras);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldMovedUnitPowerTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var backAlleyBar = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·277/298", StringComparison.Ordinal));
        var trigger = Assert.Single(backAlleyBar.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldUnitMovedAwayPowerModifier, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldUnitMovedAway, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.MovedUnit, trigger.TargetScope);
        Assert.Equal(1, trigger.PowerDelta);
        Assert.Equal(TriggerDurations.UntilEndOfTurn, trigger.Duration);
        Assert.Contains("每当一名单位从此处向别处移动时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("本回合内{{S}}+1", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield moved-unit power modifier parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldNextSpellEchoTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var piltoverAcademy = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-216/219", StringComparison.Ordinal));
        var trigger = Assert.Single(piltoverAcademy.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldNextSpellEcho, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(TriggerDurations.UntilEndOfTurn, trigger.Duration);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("下一个法术获得等同于其基础费用的{{回响}}", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held next-spell Echo parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldUnitCostIncreaseTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var vaultsOfHelia = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-219/219", StringComparison.Ordinal));
        var trigger = Assert.Single(vaultsOfHelia.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldUnitCostIncrease, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(TriggerDurations.UntilEndOfTurn, trigger.Duration);
        Assert.Equal(1, trigger.ManaDelta);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("非指示物单位在本回合内的打出费用增加{{1}}", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held non-token unit cost increase parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldDrawTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var hiddenValley = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·280/298", StringComparison.Ordinal));
        var trigger = Assert.Single(hiddenValley.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldDrawOne, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(1, trigger.DrawCount);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("抽一张牌", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held draw-one trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldCallRuneTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var starPeak = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·288/298", StringComparison.Ordinal));
        var trigger = Assert.Single(starPeak.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldCallRune, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(1, trigger.RuneCallCount);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("召出一枚休眠的符文", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held call-rune trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldEachPlayerCallRuneTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var paperTree = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·219/221", StringComparison.Ordinal));
        var trigger = Assert.Single(paperTree.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldEachPlayerCallRune, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.EachPlayer, trigger.TargetScope);
        Assert.Equal(1, trigger.RuneCallCount);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("每名玩家召出一枚休眠的符文", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held each-player call-rune trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldFriendlySpellDrawTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var dreamTree = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·292/298", StringComparison.Ordinal));
        var trigger = Assert.Single(dreamTree.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldFriendlySpellDraw, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldFriendlySpellTargeted, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.FriendlyUnitAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(1, trigger.DrawCount);
        Assert.Contains("每回合首次", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("对此处的友方单位使用法术时，抽一张牌", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield first friendly spell targeting trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldSpellPowerBonusTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var wasteHall = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-205/219", StringComparison.Ordinal));
        var trigger = Assert.Single(wasteHall.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldSpellPowerBonus, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldSpellPlayed, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.FriendlyUnitAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(1, trigger.PowerDelta);
        Assert.Equal(TriggerDurations.UntilEndOfTurn, trigger.Duration);
        Assert.Contains("当一名玩家打出法术时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("在本回合内{{S}}+1", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield spell-play power modifier parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHighCostSpellInsightTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var lostLibrary = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-211/219", StringComparison.Ordinal));
        var trigger = Assert.Single(lostLibrary.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHighCostSpellInsightRecycle, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldSpellPlayed, trigger.Timing);
        Assert.Equal(4, trigger.MinimumPaidMana);
        Assert.Equal(1, trigger.RecycleCount);
        Assert.Contains("当你打出一张法术牌时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("消耗了不低于{{4}}法力", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("进行{{洞察}}", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield high-cost spell insight recycle parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldPlayUnitBoonTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var idolValley = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-218/219", StringComparison.Ordinal));
        var trigger = Assert.Single(idolValley.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldPlayUnitPayBoon, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldUnitPlayed, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.PlayedUnitAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(1, trigger.ManaCost);
        Assert.Equal(1, trigger.BoonCount);
        Assert.Contains("当一名玩家在此处打出一名单位时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("支付{{1}}", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("给予该单位{{增益}}", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield unit-play pay-mana boon trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldUnitReturnedCallRuneTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var ghostBay = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-214/219", StringComparison.Ordinal));
        var trigger = Assert.Single(ghostBay.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldUnitReturnedPayCallRune, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldUnitReturned, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.ReturnedUnitAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(1, trigger.ManaCost);
        Assert.Equal(1, trigger.RuneCallCount);
        Assert.Contains("当此处的一名单位返回到一名玩家的手牌时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("支付{{1}}", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("召出一枚休眠的符文", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield returned-unit pay-mana call-rune trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var meteorSpring = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-215/219", StringComparison.Ordinal));
        var trigger = Assert.Single(meteorSpring.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldFirstUnitPlayedMoveOtherToBase, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldUnitPlayed, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.OtherControlledUnitAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(1, trigger.MoveCount);
        Assert.Equal(TriggerMoveDestinations.OwnerBase, trigger.MoveDestination);
        Assert.True(trigger.OncePerTurn);
        Assert.True(trigger.ExcludesTokens);
        Assert.Contains("每回合首次", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("打出一名非指示物单位", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("另一名单位移动到其基地", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield first non-token unit-play move-other-to-base trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldMoveUnitToBaseTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var rehearsalHall = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-207/219", StringComparison.Ordinal));
        var trigger = Assert.Single(rehearsalHall.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldMoveUnitToBase, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.UnitAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(1, trigger.MoveCount);
        Assert.Equal(TriggerMoveDestinations.OwnerBase, trigger.MoveDestination);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("战场上的一名单位移动到其基地", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held move-unit-to-base trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldGrantBoonTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var navoriArena = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·283/298", StringComparison.Ordinal));
        var trigger = Assert.Single(navoriArena.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldGrantBoon, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.UnitAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(1, trigger.BoonCount);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("给予此处的一名单位增益", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held grant-boon trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldCreateMinionTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var unitySanctum = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·275/298", StringComparison.Ordinal));
        var trigger = Assert.Single(unitySanctum.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldCreateMinion, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(1, trigger.CreatedTokenCount);
        Assert.Equal("随从", trigger.CreatedTokenName);
        Assert.Equal(1, trigger.CreatedTokenPower);
        Assert.Equal(TriggerTokenDestinations.OwnerBase, trigger.CreatedTokenDestination);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("打出一名1{{S}}的“随从”到你的基地", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held create-minion trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldReturnHeroTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var hallowedTomb = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·281/298", StringComparison.Ordinal));
        var trigger = Assert.Single(hallowedTomb.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldReturnHero, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.OwnedHeroUnitInGraveyard, trigger.TargetScope);
        Assert.Equal(1, trigger.ReturnCount);
        Assert.Equal(TriggerZones.Champion, trigger.RequiredEmptyZone);
        Assert.Equal(TriggerZones.Graveyard, trigger.ReturnOriginZone);
        Assert.Equal(TriggerZones.Champion, trigger.ReturnDestinationZone);
        Assert.Equal(TriggerCardFilters.TagPrefix + "CARD_CATEGORY:英雄单位", trigger.ReturnCardFilter);
        Assert.Contains("当你据守此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("英雄区域已无英雄单位牌", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("从废牌堆中返回英雄区域", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held return-hero trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Theory]
    [InlineData("OGN·293/298")]
    [InlineData("OGN·293a/298")]
    public async Task BehaviorSpecCatalogParsesBattlefieldHeldSevenUnitsWinTrigger(string cardNo)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var grandPlaza = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
        var trigger = Assert.Single(grandPlaza.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldHeldSevenUnitsWin, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.ControlledUnitsAtThisBattlefield, trigger.TargetScope);
        Assert.Equal(7, trigger.RequiredUnitCount);
        Assert.True(trigger.WinsGame);
        Assert.Contains("当你据守此处", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("在此拥有至少七名单位", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("赢得游戏胜利", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield held seven-units victory trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerRevealRecycleTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var candlelitSanctum = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·291/298", StringComparison.Ordinal));
        var trigger = Assert.Single(candlelitSanctum.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerRevealRecycle, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(2, trigger.RevealCount);
        Assert.Equal(2, trigger.RecycleCount);
        Assert.Equal(TriggerZones.MainDeck, trigger.RevealSourceZone);
        Assert.Equal(TriggerZones.MainDeck, trigger.RecycleDestinationZone);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("查看主牌堆顶部的两张牌", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("回收任意数量的卡牌", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered reveal/recycle trigger parsed for B4 routing; execution is available as a deterministic representative path when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerMillTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var minefield = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·212/221", StringComparison.Ordinal));
        var trigger = Assert.Single(minefield.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerMill, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(2, trigger.MillCount);
        Assert.Equal(TriggerZones.MainDeck, trigger.MillSourceZone);
        Assert.Equal(TriggerZones.Graveyard, trigger.MillDestinationZone);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("主牌堆顶部的两张牌放入废牌堆", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered mill trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerRecycleRuneTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var thunderRune = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·287/298", StringComparison.Ordinal));
        var trigger = Assert.Single(thunderRune.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerRecycleRune, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.OwnedRuneInBase, trigger.TargetScope);
        Assert.Equal(1, trigger.RecycleCount);
        Assert.Equal(TriggerZones.Base, trigger.RecycleSourceZone);
        Assert.Equal(TriggerZones.MainDeck, trigger.RecycleDestinationZone);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("回收一枚你的符文", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered recycle-rune trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerConsumeBoonDrawTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var monastery = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·282/298", StringComparison.Ordinal));
        var trigger = Assert.Single(monastery.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerConsumeBoonDraw, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.ControlledBoonUnitOnField, trigger.TargetScope);
        Assert.Equal(1, trigger.ConsumedBoonCount);
        Assert.Equal(1, trigger.DrawCount);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("消耗一个增益", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("抽一张牌", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered consume-boon draw trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerDiscardDrawTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var sump = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·298/298", StringComparison.Ordinal));
        var trigger = Assert.Single(sump.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerDiscardDraw, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.ControlledHandCard, trigger.TargetScope);
        Assert.Equal(1, trigger.DiscardCount);
        Assert.Equal(TriggerZones.Hand, trigger.DiscardSourceZone);
        Assert.Equal(TriggerZones.Graveyard, trigger.DiscardDestinationZone);
        Assert.Equal(1, trigger.DrawCount);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("弃置一张手牌", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("抽一张牌", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered discard-draw trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerDrawForOtherBattlefieldsTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var seatOfPower = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·217/221", StringComparison.Ordinal));
        var trigger = Assert.Single(seatOfPower.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerDrawForOtherBattlefields, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.OtherControlledBattlefields, trigger.TargetScope);
        Assert.Equal(1, trigger.DrawCountPerParticipant);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("每控制一处其他战场", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("抽一张牌", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered draw-for-other-battlefields trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerReadyRunesAtEndTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var mountTargon = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·289/298", StringComparison.Ordinal));
        var trigger = Assert.Single(mountTargon.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerReadyRunesAtEnd, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.OwnedRuneInBase, trigger.TargetScope);
        Assert.Equal(2, trigger.RuneReadyCount);
        Assert.Equal(TriggerReadyTimings.EndOfTurn, trigger.ReadyTiming);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("选择两枚符文", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("本回合结束时", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered ready-runes-at-end trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldConquerReadyEquipmentTrigger()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var moonveilAltar = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·221/221", StringComparison.Ordinal));
        var trigger = Assert.Single(moonveilAltar.Triggers);
        Assert.Equal(TriggerKinds.BattlefieldConquerReadyEquipment, trigger.Kind);
        Assert.Equal(TriggerTimings.BattlefieldConquered, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.FriendlyEquipment, trigger.TargetScope);
        Assert.Equal(1, trigger.EquipmentReadyCount);
        Assert.True(trigger.DetachesArmament);
        Assert.Contains("当你征服此处时", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("让一件友方装备变为活跃状态", trigger.Text, StringComparison.Ordinal);
        Assert.Contains("武装", trigger.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Battlefield conquered ready-equipment trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
            trigger.Reason);
    }

    [Fact]
    public async Task BehaviorSpecCatalogParsesBattlefieldStaticRestrictionSpecs()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var vilemawLair = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·295/298", StringComparison.Ordinal));
        var preventMoveToBase = Assert.Single(vilemawLair.StaticAbilities);
        Assert.Equal(StaticAbilityKinds.BattlefieldPreventMoveToBase, preventMoveToBase.Kind);
        Assert.Contains("单位无法从此处移动到基地", preventMoveToBase.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, preventMoveToBase.Status);

        var fallingRocks = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·216/221", StringComparison.Ordinal));
        var preventUnitPlay = Assert.Single(fallingRocks.StaticAbilities);
        Assert.Equal(StaticAbilityKinds.BattlefieldPreventUnitPlay, preventUnitPlay.Kind);
        Assert.Contains("单位无法被打出到此处", preventUnitPlay.Text, StringComparison.Ordinal);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, preventUnitPlay.Status);

        var maraiSpire = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·211/221", StringComparison.Ordinal));
        var echoCostReduction = Assert.Single(
            maraiSpire.StaticAbilities,
            ability => string.Equals(ability.Kind, StaticAbilityKinds.BattlefieldEchoCostReduction, StringComparison.Ordinal));
        Assert.Equal(StaticAbilityKinds.BattlefieldEchoCostReduction, echoCostReduction.Kind);
        Assert.Contains("友方{{回响}}的费用减少{{1}}", echoCostReduction.Text, StringComparison.Ordinal);
        Assert.Equal(1, echoCostReduction.Amount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, echoCostReduction.Status);

        var ornnForge = Assert.Single(specs, spec => string.Equals(spec.CardNo, "SFD·213/221", StringComparison.Ordinal));
        var equipmentCostReduction = Assert.Single(
            ornnForge.StaticAbilities,
            ability => string.Equals(ability.Kind, StaticAbilityKinds.BattlefieldEquipmentCostReduction, StringComparison.Ordinal));
        Assert.Equal(StaticAbilityKinds.BattlefieldEquipmentCostReduction, equipmentCostReduction.Kind);
        Assert.Contains("第一件友方装备的费用减少{{1}}", equipmentCostReduction.Text, StringComparison.Ordinal);
        Assert.Equal(1, equipmentCostReduction.Amount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, equipmentCostReduction.Status);

        var mutationGarden = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL-213/219", StringComparison.Ordinal));
        var grantedExperienceAbility = Assert.Single(mutationGarden.StaticAbilities);
        Assert.Equal(StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility, grantedExperienceAbility.Kind);
        Assert.Contains("此处的单位获得", grantedExperienceAbility.Text, StringComparison.Ordinal);
        Assert.Contains("获得1经验", grantedExperienceAbility.Text, StringComparison.Ordinal);
        Assert.Equal(1, grantedExperienceAbility.Amount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, grantedExperienceAbility.Status);

        var voidGate = Assert.Single(specs, spec => string.Equals(spec.CardNo, "OGN·296/298", StringComparison.Ordinal));
        var targetDamageBonus = Assert.Single(
            voidGate.StaticAbilities,
            ability => string.Equals(ability.Kind, StaticAbilityKinds.BattlefieldTargetSpellSkillDamageBonus, StringComparison.Ordinal));
        Assert.Equal(StaticAbilityKinds.BattlefieldTargetSpellSkillDamageBonus, targetDamageBonus.Kind);
        Assert.Contains("以此处的单位作为目标的法术或技能", targetDamageBonus.Text, StringComparison.Ordinal);
        Assert.Contains("造成的伤害+1", targetDamageBonus.Text, StringComparison.Ordinal);
        Assert.Equal(1, targetDamageBonus.Amount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, targetDamageBonus.Status);
    }

    [Fact]
    public void StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("ContinuousEffectStaticAuraCards", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldAllUnitsPowerPlusOneCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldIsolatedDefenderSteadfastMinusTwoCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldStaticRoamCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("IsPetalPixieCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WiseElderCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldAllUnitsPowerPlusOneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldIsolatedDefenderSteadfastMinusTwoCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldStaticRoamCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldMovedUnitPowerTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldMovedUnitPowerPlusOneCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldMovedUnitPowerPlusOneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldMovedUnitPowerPlusOneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldNextSpellEchoTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHeldNextSpellEchoCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHeldNextSpellEchoCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHeldNextSpellEchoCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldUnitCostIncreaseTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHeldUnitCostIncreaseCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHeldUnitCostIncreaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHeldUnitCostIncreaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldFriendlySpellDrawTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldFriendlySpellDrawCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldFriendlySpellDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldFriendlySpellDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldSpellPowerBonusTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldSpellPowerBonusCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldSpellPowerBonusCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldSpellPowerBonusCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHighCostSpellInsightTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHighCostSpellInsightCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHighCostSpellInsightCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHighCostSpellInsightCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldPlayUnitBoonTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldPlayUnitPayOneBoonCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldPlayUnitPayOneBoonCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldPlayUnitPayOneBoonCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldUnitReturnedCallRuneTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldUnitReturnedCallRuneCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldUnitReturnedCallRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldUnitReturnedCallRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldFirstUnitPlayedMoveOtherToBaseTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldFirstUnitPlayedMoveOtherToBaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldMoveUnitToBaseTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHeldMoveUnitToBaseCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHeldMoveUnitToBaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHeldMoveUnitToBaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldGrantBoonTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHoldGrantBoonCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHoldGrantBoonCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHoldGrantBoonCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldCreateMinionTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHoldCreateMinionCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHoldCreateMinionCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHoldCreateMinionCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldReturnHeroTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHeldReturnHeroCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHeldReturnHeroCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHeldReturnHeroCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldSevenUnitsWinTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHeldSevenUnitsWinCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldHeldSevenUnitsWinAltCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHeldSevenUnitsWinCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldHeldSevenUnitsWinAltCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHeldSevenUnitsWinCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerRevealRecycleTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerRevealRecycleCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerRevealRecycleCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerRevealRecycleCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerMillTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerMillTwoCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerMillTwoCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerMillTwoCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerRecycleRuneTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerRecycleRuneCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerRecycleRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerRecycleRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerConsumeBoonDrawTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerConsumeBoonDrawCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerConsumeBoonDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerConsumeBoonDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerDiscardDrawTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerDiscardDrawCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerDiscardDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerDiscardDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerDrawForOtherBattlefieldsTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerDrawForOtherBattlefieldsCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerDrawForOtherBattlefieldsCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerDrawForOtherBattlefieldsCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerReadyRunesAtEndTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerReadyTwoRunesAtEndCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerReadyTwoRunesAtEndCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerReadyTwoRunesAtEndCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldConquerReadyEquipmentTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldConquerReadyEquipmentCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldConquerReadyEquipmentCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldConquerReadyEquipmentCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldDrawTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHoldDrawCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHoldDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHoldDrawCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldCallRuneTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHoldCallRuneCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHoldCallRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHoldCallRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldHeldEachPlayerCallRuneTriggerDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldHoldEachPlayerCallRuneCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldHoldEachPlayerCallRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldHoldEachPlayerCallRuneCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldGrantUnitExperienceAbilityDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldGrantUnitExperienceCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldGrantUnitExperienceCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldGrantUnitExperienceCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldTargetSpellSkillDamageBonusDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldTargetSpellSkillDamageBonusCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldTargetSpellSkillDamageBonusCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldTargetSpellSkillDamageBonusCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldStaticRestrictionDoesNotUseCardNumberAllowList()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var source = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("BattlefieldPreventMoveToBaseCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldPreventUnitPlayCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldEchoCostReductionCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldEquipmentCostReductionCardNo", source, StringComparison.Ordinal);

        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("BattlefieldPreventMoveToBaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldPreventMoveToBaseCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldPreventUnitPlayCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldPreventUnitPlayCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldEchoCostReductionCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldEchoCostReductionCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldEquipmentCostReductionCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBattlefieldEquipmentCostReductionCardNo", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BehaviorTemplateExecutorRoutesRegisteredTemplatesWithoutReplacingP2Rules()
    {
        var requiredTemplates = new[]
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
            BehaviorTemplateIds.Boon,
            BehaviorTemplateIds.GainExperience,
            BehaviorTemplateIds.Assemble,
            BehaviorTemplateIds.Echo,
            BehaviorTemplateIds.Ambush
        };
        var registered = BehaviorTemplateRegistry.GetAll().Select(template => template.TemplateId).ToArray();
        Assert.All(requiredTemplates, templateId => Assert.Contains(templateId, registered));

        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var spec = specs.Single(candidate => string.Equals(candidate.CardNo, "SFD·087/221", StringComparison.Ordinal));
        var executor = new BehaviorTemplateExecutor();
        var plan = executor.BuildPlan(
            spec,
            new BehaviorTemplateExecutionContext("P1", "P1-SPELL-PROPHETS-OMEN", "SFD·087/221", []));

        Assert.Equal(BehaviorImplementationStatuses.Implemented, plan.Status);
        var step = Assert.Single(plan.Steps);
        Assert.Equal(BehaviorTemplateIds.Draw, step.TemplateId);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, step.Status);
        Assert.Contains("does not mutate game state", plan.Reason, StringComparison.Ordinal);

        var echoSpec = specs.Single(candidate => string.Equals(candidate.CardNo, "SFD·077/221", StringComparison.Ordinal));
        var echoPlan = executor.BuildPlan(
            echoSpec,
            new BehaviorTemplateExecutionContext("P1", "P1-SPELL-ROCKET-BARRAGE", "SFD·077/221", []));
        Assert.Equal(BehaviorImplementationStatuses.Unimplemented, echoPlan.Status);
        Assert.Contains(
            echoPlan.Steps,
            candidate => string.Equals(candidate.TemplateId, BehaviorTemplateIds.Echo, StringComparison.Ordinal)
                && string.Equals(candidate.Status, BehaviorImplementationStatuses.Unimplemented, StringComparison.Ordinal));
    }

    [Fact]
    public async Task P4BridgeDelegatesLowRiskTemplatesToExistingP2Behaviors()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var bridge = new BehaviorTemplateDelegationBridge();
        var candidates = new[]
        {
            new { CardNo = "SFD·087/221", TemplateId = BehaviorTemplateIds.Draw, EffectKind = "PROPHETS_OMEN_DRAW_3" },
            new { CardNo = "OGS·003/024", TemplateId = BehaviorTemplateIds.Damage, EffectKind = "INCINERATE_DAMAGE_2" },
            new { CardNo = "OGN·229/298", TemplateId = BehaviorTemplateIds.Destroy, EffectKind = "VENGEANCE_DESTROY_UNIT" },
            new { CardNo = "OGN·156/298", TemplateId = BehaviorTemplateIds.Recycle, EffectKind = "COVERT_SABOTAGE_RECYCLE_OPPONENT_NON_UNIT_HAND_CARD" },
            new { CardNo = "OGN·102/298", TemplateId = BehaviorTemplateIds.Banish, EffectKind = "PORTALPAL_RESCUE_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE" },
            new { CardNo = "OGN·050/298", TemplateId = BehaviorTemplateIds.Stun, EffectKind = "RUNE_PRISON_STUN_UNIT" },
            new { CardNo = "OGN·004/298", TemplateId = BehaviorTemplateIds.TempMight, EffectKind = "CLEAVE_OVERWHELM_3" },
            new { CardNo = "OGN·053/298", TemplateId = BehaviorTemplateIds.Boon, EffectKind = "SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS" }
        };

        foreach (var candidate in candidates)
        {
            var spec = specs.Single(spec => string.Equals(spec.CardNo, candidate.CardNo, StringComparison.Ordinal));
            var delegation = bridge.BuildDelegationPlan(
                spec,
                new BehaviorTemplateExecutionContext("P1", $"P1-SOURCE-{candidate.CardNo}", candidate.CardNo, []));

            Assert.Equal(BehaviorImplementationStatuses.Implemented, delegation.Status);
            Assert.NotNull(delegation.DelegatedBehavior);
            Assert.Equal(candidate.CardNo, delegation.CardNo);
            Assert.Equal(candidate.EffectKind, delegation.DelegatedBehavior.EffectKind);
            Assert.Contains(
                delegation.ExecutionPlan.Steps,
                step => string.Equals(step.TemplateId, candidate.TemplateId, StringComparison.Ordinal)
                    && string.Equals(step.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal));
            Assert.Contains("P2 hand-written behavior", delegation.Reason, StringComparison.Ordinal);

            switch (candidate.TemplateId)
            {
                case BehaviorTemplateIds.Draw:
                    Assert.Equal(3, delegation.DelegatedBehavior.DrawCount);
                    break;
                case BehaviorTemplateIds.Damage:
                    Assert.Equal(2, delegation.DelegatedBehavior.DamageAmount);
                    break;
                case BehaviorTemplateIds.Destroy:
                    Assert.True(delegation.DelegatedBehavior.DestroysTarget);
                    break;
                case BehaviorTemplateIds.Recycle:
                    Assert.True(delegation.DelegatedBehavior.RecyclesTargets);
                    break;
                case BehaviorTemplateIds.Banish:
                    Assert.True(delegation.DelegatedBehavior.BanishesTargetThenPlaysToBase);
                    break;
                case BehaviorTemplateIds.Stun:
                    Assert.Equal("STUNNED", delegation.DelegatedBehavior.StatusEffectId);
                    break;
                case BehaviorTemplateIds.TempMight:
                    Assert.Equal(3, delegation.DelegatedBehavior.PowerModifierAmount);
                    Assert.Equal(
                        CardPowerModifierConditionKinds.TargetIsAttacking,
                        delegation.DelegatedBehavior.PowerModifierConditionKind);
                    break;
                case BehaviorTemplateIds.Boon:
                    Assert.True(delegation.DelegatedBehavior.GrantsBoon);
                    break;
            }
        }
    }

    [Fact]
    public async Task P4BridgeDoesNotDelegateUnimplementedTemplateRoutes()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var bridge = new BehaviorTemplateDelegationBridge();
        var spec = specs.Single(candidate => string.Equals(candidate.CardNo, "SFD·077/221", StringComparison.Ordinal));

        var delegation = bridge.BuildDelegationPlan(
            spec,
            new BehaviorTemplateExecutionContext("P1", "P1-SPELL-ROCKET-BARRAGE", "SFD·077/221", []));

        Assert.Equal(BehaviorImplementationStatuses.Unimplemented, delegation.Status);
        Assert.Null(delegation.DelegatedBehavior);
        Assert.Contains(
            delegation.ExecutionPlan.Steps,
            step => string.Equals(step.TemplateId, BehaviorTemplateIds.Echo, StringComparison.Ordinal)
                && string.Equals(step.Status, BehaviorImplementationStatuses.Unimplemented, StringComparison.Ordinal));
    }

    [Fact]
    public async Task P4PrimitiveExecutorBuildsBasicActionPlansAndLeavesComplexRoutesDelegated()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var executor = new BehaviorTemplatePrimitiveExecutor();
        var primitiveCandidates = new[]
        {
            new
            {
                CardNo = "SFD·087/221",
                TemplateId = BehaviorTemplateIds.Draw,
                Kind = BehaviorTemplatePrimitiveKinds.DrawCards,
                Amount = 3,
                TargetScope = "",
                StatusEffectId = "",
                ConditionKind = ""
            },
            new
            {
                CardNo = "OGS·003/024",
                TemplateId = BehaviorTemplateIds.Damage,
                Kind = BehaviorTemplatePrimitiveKinds.DealDamage,
                Amount = 2,
                TargetScope = CardTargetScopes.BattlefieldUnit,
                StatusEffectId = "",
                ConditionKind = CardDamageConditionKinds.None
            },
            new
            {
                CardNo = "OGN·229/298",
                TemplateId = BehaviorTemplateIds.Destroy,
                Kind = BehaviorTemplatePrimitiveKinds.DestroyTarget,
                Amount = 0,
                TargetScope = CardTargetScopes.AnyUnit,
                StatusEffectId = "",
                ConditionKind = ""
            },
            new
            {
                CardNo = "OGN·050/298",
                TemplateId = BehaviorTemplateIds.Stun,
                Kind = BehaviorTemplatePrimitiveKinds.ApplyStatusEffect,
                Amount = 0,
                TargetScope = CardTargetScopes.AnyUnit,
                StatusEffectId = "STUNNED",
                ConditionKind = ""
            },
            new
            {
                CardNo = "OGN·004/298",
                TemplateId = BehaviorTemplateIds.TempMight,
                Kind = BehaviorTemplatePrimitiveKinds.ModifyPowerUntilEndOfTurn,
                Amount = 3,
                TargetScope = CardTargetScopes.AnyUnit,
                StatusEffectId = "",
                ConditionKind = CardPowerModifierConditionKinds.TargetIsAttacking
            }
        };

        foreach (var candidate in primitiveCandidates)
        {
            var spec = specs.Single(spec => string.Equals(spec.CardNo, candidate.CardNo, StringComparison.Ordinal));
            var plan = executor.BuildPrimitivePlan(
                spec,
                new BehaviorTemplateExecutionContext("P1", $"P1-SOURCE-{candidate.CardNo}", candidate.CardNo, []));

            Assert.True(
                string.Equals(plan.Status, BehaviorTemplatePrimitivePlanStatuses.Ready, StringComparison.Ordinal),
                $"{candidate.CardNo} produced primitive status '{plan.Status}': {plan.Reason}");
            Assert.Contains("CoreRuleEngine remains", plan.Reason, StringComparison.Ordinal);
            var primitive = Assert.Single(plan.Primitives);
            Assert.Equal(candidate.TemplateId, primitive.TemplateId);
            Assert.Equal(candidate.Kind, primitive.Kind);
            Assert.Equal(candidate.Amount, primitive.Amount);
            Assert.Equal(candidate.TargetScope, primitive.TargetScope);
            Assert.Equal(candidate.StatusEffectId, primitive.StatusEffectId);
            Assert.Equal(candidate.ConditionKind, primitive.ConditionKind);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, plan.DelegationPlan.Status);
        }

        var delegatedCandidates = new[]
        {
            new { CardNo = "OGN·188/298", TemplateId = BehaviorTemplateIds.Recall },
            new { CardNo = "OGN·043/298", TemplateId = BehaviorTemplateIds.Move },
            new { CardNo = "OGN·156/298", TemplateId = BehaviorTemplateIds.Recycle },
            new { CardNo = "OGN·102/298", TemplateId = BehaviorTemplateIds.Banish },
            new { CardNo = "OGN·053/298", TemplateId = BehaviorTemplateIds.Boon }
        };
        foreach (var candidate in delegatedCandidates)
        {
            var spec = specs.Single(spec => string.Equals(spec.CardNo, candidate.CardNo, StringComparison.Ordinal));
            var plan = executor.BuildPrimitivePlan(
                spec,
                new BehaviorTemplateExecutionContext("P1", $"P1-SOURCE-{candidate.CardNo}", candidate.CardNo, []));

            Assert.True(
                string.Equals(plan.Status, BehaviorTemplatePrimitivePlanStatuses.DelegatedToP2, StringComparison.Ordinal),
                $"{candidate.CardNo} produced primitive status '{plan.Status}': {plan.Reason}");
            Assert.Contains($"Template '{candidate.TemplateId}' remains delegated", plan.Reason, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, plan.DelegationPlan.Status);
            Assert.NotNull(plan.DelegationPlan.DelegatedBehavior);
        }
    }

    [Fact]
    public async Task P4PermissionKeywordProfilesMapOfficialTextToRegistryFlags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var swiftSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·004/298", StringComparison.Ordinal));
        Assert.Contains(swiftSpec.Keywords, keyword => string.Equals(keyword.Keyword, "迅捷", StringComparison.Ordinal));
        Assert.Contains("迅捷", swiftSpec.OfficialText, StringComparison.Ordinal);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·004/298", out var swiftDefinition));
        var swiftProfile = CardPermissionKeywordRules.BuildProfile(swiftDefinition);
        Assert.True(swiftProfile.HasSwift);
        Assert.False(swiftProfile.HasReaction);
        Assert.False(swiftProfile.HasHaste);

        var reactionSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·064/298", StringComparison.Ordinal));
        Assert.Contains(reactionSpec.Keywords, keyword => string.Equals(keyword.Keyword, "反应", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·064/298", out var reactionDefinition));
        var reactionProfile = CardPermissionKeywordRules.BuildProfile(reactionDefinition);
        Assert.False(reactionProfile.HasSwift);
        Assert.True(reactionProfile.HasReaction);
        Assert.False(reactionProfile.HasHaste);

        var hasteSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·001/298", StringComparison.Ordinal));
        Assert.Contains(hasteSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(hasteSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·001/298", out var hasteDefinition));
        var hasteProfile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.False(hasteProfile.HasSwift);
        Assert.False(hasteProfile.HasReaction);
        Assert.True(hasteProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            hasteProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, hasteProfile.HasteReadyManaCost);
        Assert.Equal(1, hasteProfile.HasteReadyPowerCost);
        Assert.Contains("P4.13/P4.18/P4.20", hasteProfile.HasteOptionalReadyBranchReason, StringComparison.OrdinalIgnoreCase);

        var babySharkSpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-006/219", StringComparison.Ordinal));
        Assert.Contains(babySharkSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(babySharkSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-006/219", out var babySharkDefinition));
        var babySharkProfile = CardPermissionKeywordRules.BuildProfile(babySharkDefinition);
        Assert.True(babySharkProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            babySharkProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, babySharkProfile.HasteReadyManaCost);
        Assert.Equal(1, babySharkProfile.HasteReadyPowerCost);

        var reksaiSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·029/221", StringComparison.Ordinal));
        Assert.Contains(reksaiSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(reksaiSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·029/221", out var reksaiDefinition));
        var reksaiProfile = CardPermissionKeywordRules.BuildProfile(reksaiDefinition);
        Assert.True(reksaiProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            reksaiProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, reksaiProfile.HasteReadyManaCost);
        Assert.Equal(1, reksaiProfile.HasteReadyPowerCost);

        var reksaiAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·029a/221", StringComparison.Ordinal));
        Assert.Contains(reksaiAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(reksaiAltASpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·029a/221", out var reksaiAltADefinition));
        var reksaiAltAProfile = CardPermissionKeywordRules.BuildProfile(reksaiAltADefinition);
        Assert.True(reksaiAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            reksaiAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, reksaiAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, reksaiAltAProfile.HasteReadyPowerCost);

        var legionRearguardSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·010/298", StringComparison.Ordinal));
        Assert.Contains(legionRearguardSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(legionRearguardSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·010/298", out var legionRearguardDefinition));
        var legionRearguardProfile = CardPermissionKeywordRules.BuildProfile(legionRearguardDefinition);
        Assert.True(legionRearguardProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            legionRearguardProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, legionRearguardProfile.HasteReadyManaCost);
        Assert.Equal(1, legionRearguardProfile.HasteReadyPowerCost);

        var mrRootSpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-127/219", StringComparison.Ordinal));
        Assert.Contains(mrRootSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(mrRootSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-127/219", out var mrRootDefinition));
        var mrRootProfile = CardPermissionKeywordRules.BuildProfile(mrRootDefinition);
        Assert.True(mrRootProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            mrRootProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, mrRootProfile.HasteReadyManaCost);
        Assert.Equal(1, mrRootProfile.HasteReadyPowerCost);

        var mechManiacSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·068/221", StringComparison.Ordinal));
        Assert.Contains(mechManiacSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(mechManiacSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·068/221", out var mechManiacDefinition));
        var mechManiacProfile = CardPermissionKeywordRules.BuildProfile(mechManiacDefinition);
        Assert.True(mechManiacProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            mechManiacProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, mechManiacProfile.HasteReadyManaCost);
        Assert.Equal(1, mechManiacProfile.HasteReadyPowerCost);

        var xersaiFishSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·103/221", StringComparison.Ordinal));
        Assert.Contains(xersaiFishSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(xersaiFishSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·103/221", out var xersaiFishDefinition));
        var xersaiFishProfile = CardPermissionKeywordRules.BuildProfile(xersaiFishDefinition);
        Assert.True(xersaiFishProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            xersaiFishProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, xersaiFishProfile.HasteReadyManaCost);
        Assert.Equal(1, xersaiFishProfile.HasteReadyPowerCost);

        var karinaVerazeSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·179/221", StringComparison.Ordinal));
        Assert.Contains(karinaVerazeSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(karinaVerazeSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·179/221", out var karinaVerazeDefinition));
        var karinaVerazeProfile = CardPermissionKeywordRules.BuildProfile(karinaVerazeDefinition);
        Assert.True(karinaVerazeProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            karinaVerazeProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, karinaVerazeProfile.HasteReadyManaCost);
        Assert.Equal(1, karinaVerazeProfile.HasteReadyPowerCost);

        var crimsonSignetTreantSpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-029/219", StringComparison.Ordinal));
        Assert.Contains(crimsonSignetTreantSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(crimsonSignetTreantSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-029/219", out var crimsonSignetTreantDefinition));
        var crimsonSignetTreantProfile = CardPermissionKeywordRules.BuildProfile(crimsonSignetTreantDefinition);
        Assert.True(crimsonSignetTreantProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            crimsonSignetTreantProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, crimsonSignetTreantProfile.HasteReadyManaCost);
        Assert.Equal(1, crimsonSignetTreantProfile.HasteReadyPowerCost);

        var crimsonSignetTreantAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-029a/219", StringComparison.Ordinal));
        Assert.Contains(crimsonSignetTreantAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains("急速", crimsonSignetTreantAltASpec.OfficialText, StringComparison.Ordinal);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-029a/219", out var crimsonSignetTreantAltADefinition));
        var crimsonSignetTreantAltAProfile = CardPermissionKeywordRules.BuildProfile(crimsonSignetTreantAltADefinition);
        Assert.True(crimsonSignetTreantAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            crimsonSignetTreantAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, crimsonSignetTreantAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, crimsonSignetTreantAltAProfile.HasteReadyPowerCost);

        var rengarSpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-024/219", StringComparison.Ordinal));
        Assert.Contains(rengarSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(rengarSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-024/219", out var rengarDefinition));
        var rengarProfile = CardPermissionKeywordRules.BuildProfile(rengarDefinition);
        Assert.True(rengarProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            rengarProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, rengarProfile.HasteReadyManaCost);
        Assert.Equal(1, rengarProfile.HasteReadyPowerCost);

        var rengarAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-024a/219", StringComparison.Ordinal));
        Assert.Contains(rengarAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains("急速", rengarAltASpec.OfficialText, StringComparison.Ordinal);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-024a/219", out var rengarAltADefinition));
        var rengarAltAProfile = CardPermissionKeywordRules.BuildProfile(rengarAltADefinition);
        Assert.True(rengarAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            rengarAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, rengarAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, rengarAltAProfile.HasteReadyPowerCost);

        var nilahSpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-115/219", StringComparison.Ordinal));
        Assert.Contains(nilahSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(nilahSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-115/219", out var nilahDefinition));
        var nilahProfile = CardPermissionKeywordRules.BuildProfile(nilahDefinition);
        Assert.True(nilahProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            nilahProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, nilahProfile.HasteReadyManaCost);
        Assert.Equal(1, nilahProfile.HasteReadyPowerCost);

        var missFortuneSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·162/298", StringComparison.Ordinal));
        Assert.Contains(missFortuneSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(missFortuneSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·162/298", out var missFortuneDefinition));
        var missFortuneProfile = CardPermissionKeywordRules.BuildProfile(missFortuneDefinition);
        Assert.True(missFortuneProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            missFortuneProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, missFortuneProfile.HasteReadyManaCost);
        Assert.Equal(1, missFortuneProfile.HasteReadyPowerCost);

        var missFortuneAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·162a/298", StringComparison.Ordinal));
        Assert.Contains(missFortuneAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(missFortuneAltASpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·162a/298", out var missFortuneAltADefinition));
        var missFortuneAltAProfile = CardPermissionKeywordRules.BuildProfile(missFortuneAltADefinition);
        Assert.True(missFortuneAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            missFortuneAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, missFortuneAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, missFortuneAltAProfile.HasteReadyPowerCost);

        var sivirSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·143/221", StringComparison.Ordinal));
        Assert.Contains(sivirSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(sivirSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·143/221", out var sivirDefinition));
        var sivirProfile = CardPermissionKeywordRules.BuildProfile(sivirDefinition);
        Assert.True(sivirProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            sivirProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, sivirProfile.HasteReadyManaCost);
        Assert.Equal(1, sivirProfile.HasteReadyPowerCost);

        var sivirAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·143a/221", StringComparison.Ordinal));
        Assert.Contains(sivirAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(sivirAltASpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·143a/221", out var sivirAltADefinition));
        var sivirAltAProfile = CardPermissionKeywordRules.BuildProfile(sivirAltADefinition);
        Assert.True(sivirAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            sivirAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, sivirAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, sivirAltAProfile.HasteReadyPowerCost);

        var lilliaSpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-082/219", StringComparison.Ordinal));
        Assert.Contains(lilliaSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(lilliaSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-082/219", out var lilliaDefinition));
        var lilliaProfile = CardPermissionKeywordRules.BuildProfile(lilliaDefinition);
        Assert.True(lilliaProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            lilliaProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, lilliaProfile.HasteReadyManaCost);
        Assert.Equal(1, lilliaProfile.HasteReadyPowerCost);

        var lilliaAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-082a/219", StringComparison.Ordinal));
        Assert.Contains(lilliaAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-082a/219", out var lilliaAltADefinition));
        var lilliaAltAProfile = CardPermissionKeywordRules.BuildProfile(lilliaAltADefinition);
        Assert.True(lilliaAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            lilliaAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, lilliaAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, lilliaAltAProfile.HasteReadyPowerCost);

        var azirSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·177/221", StringComparison.Ordinal));
        Assert.Contains(azirSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(azirSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·177/221", out var azirDefinition));
        var azirProfile = CardPermissionKeywordRules.BuildProfile(azirDefinition);
        Assert.True(azirProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            azirProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, azirProfile.HasteReadyManaCost);
        Assert.Equal(1, azirProfile.HasteReadyPowerCost);

        var azirAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·177a/221", StringComparison.Ordinal));
        Assert.Contains(azirAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(azirAltASpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·177a/221", out var azirAltADefinition));
        var azirAltAProfile = CardPermissionKeywordRules.BuildProfile(azirAltADefinition);
        Assert.True(azirAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            azirAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, azirAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, azirAltAProfile.HasteReadyPowerCost);

        var kaisaSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·039/298", StringComparison.Ordinal));
        Assert.Contains(kaisaSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(kaisaSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·039/298", out var kaisaDefinition));
        var kaisaProfile = CardPermissionKeywordRules.BuildProfile(kaisaDefinition);
        Assert.True(kaisaProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            kaisaProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, kaisaProfile.HasteReadyManaCost);
        Assert.Equal(1, kaisaProfile.HasteReadyPowerCost);

        var kaisaAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·039a/298", StringComparison.Ordinal));
        Assert.Contains(kaisaAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(kaisaAltASpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·039a/298", out var kaisaAltADefinition));
        var kaisaAltAProfile = CardPermissionKeywordRules.BuildProfile(kaisaAltADefinition);
        Assert.True(kaisaAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            kaisaAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, kaisaAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, kaisaAltAProfile.HasteReadyPowerCost);

        var tastyFaerieSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·075/298", StringComparison.Ordinal));
        Assert.Contains(tastyFaerieSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(tastyFaerieSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·075/298", out var tastyFaerieDefinition));
        var tastyFaerieProfile = CardPermissionKeywordRules.BuildProfile(tastyFaerieDefinition);
        Assert.True(tastyFaerieProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            tastyFaerieProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, tastyFaerieProfile.HasteReadyManaCost);
        Assert.Equal(1, tastyFaerieProfile.HasteReadyPowerCost);

        var ekkoSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·110/298", StringComparison.Ordinal));
        Assert.Contains(ekkoSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(ekkoSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·110/298", out var ekkoDefinition));
        var ekkoProfile = CardPermissionKeywordRules.BuildProfile(ekkoDefinition);
        Assert.True(ekkoProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            ekkoProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, ekkoProfile.HasteReadyManaCost);
        Assert.Equal(1, ekkoProfile.HasteReadyPowerCost);

        var armedAssaulterSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·002/221", StringComparison.Ordinal));
        Assert.Contains(armedAssaulterSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(armedAssaulterSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·002/221", out var armedAssaulterDefinition));
        var armedAssaulterProfile = CardPermissionKeywordRules.BuildProfile(armedAssaulterDefinition);
        Assert.True(armedAssaulterProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            armedAssaulterProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, armedAssaulterProfile.HasteReadyManaCost);
        Assert.Equal(1, armedAssaulterProfile.HasteReadyPowerCost);

        var ancientBerserkerSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·131/221", StringComparison.Ordinal));
        Assert.Contains(ancientBerserkerSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(ancientBerserkerSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·131/221", out var ancientBerserkerDefinition));
        var ancientBerserkerProfile = CardPermissionKeywordRules.BuildProfile(ancientBerserkerDefinition);
        Assert.True(ancientBerserkerProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            ancientBerserkerProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, ancientBerserkerProfile.HasteReadyManaCost);
        Assert.Equal(1, ancientBerserkerProfile.HasteReadyPowerCost);

        var krakenHunterSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·150/298", StringComparison.Ordinal));
        Assert.Contains(krakenHunterSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(krakenHunterSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·150/298", out var krakenHunterDefinition));
        var krakenHunterProfile = CardPermissionKeywordRules.BuildProfile(krakenHunterDefinition);
        Assert.True(krakenHunterProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            krakenHunterProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, krakenHunterProfile.HasteReadyManaCost);
        Assert.Equal(1, krakenHunterProfile.HasteReadyPowerCost);

        var leeSinSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·151/298", StringComparison.Ordinal));
        Assert.Contains(leeSinSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(leeSinSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·151/298", out var leeSinDefinition));
        var leeSinProfile = CardPermissionKeywordRules.BuildProfile(leeSinDefinition);
        Assert.True(leeSinProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            leeSinProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, leeSinProfile.HasteReadyManaCost);
        Assert.Equal(1, leeSinProfile.HasteReadyPowerCost);

        var leeSinAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·151a/298", StringComparison.Ordinal));
        Assert.Contains(leeSinAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(leeSinAltASpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·151a/298", out var leeSinAltADefinition));
        var leeSinAltAProfile = CardPermissionKeywordRules.BuildProfile(leeSinAltADefinition);
        Assert.True(leeSinAltAProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            leeSinAltAProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, leeSinAltAProfile.HasteReadyManaCost);
        Assert.Equal(1, leeSinAltAProfile.HasteReadyPowerCost);

        var thousandTailedWatcherSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·116/298", StringComparison.Ordinal));
        Assert.Contains(thousandTailedWatcherSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(thousandTailedWatcherSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·116/298", out var thousandTailedWatcherDefinition));
        var thousandTailedWatcherProfile = CardPermissionKeywordRules.BuildProfile(thousandTailedWatcherDefinition);
        Assert.True(thousandTailedWatcherProfile.HasHaste);
        Assert.Equal(
            HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
            thousandTailedWatcherProfile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, thousandTailedWatcherProfile.HasteReadyManaCost);
        Assert.Equal(1, thousandTailedWatcherProfile.HasteReadyPowerCost);
    }

    [Theory]
    [InlineData("OGN·001/298", RuneTrait.Red)]
    [InlineData("OGN·010/298", RuneTrait.Red)]
    [InlineData("UNL-006/219", RuneTrait.Red)]
    [InlineData("SFD·029/221", RuneTrait.Red)]
    [InlineData("SFD·029a/221", RuneTrait.Red)]
    [InlineData("OGN·039/298", RuneTrait.Red)]
    [InlineData("OGN·039a/298", RuneTrait.Red)]
    [InlineData("UNL-024/219", RuneTrait.Red)]
    [InlineData("UNL-024a/219", RuneTrait.Red)]
    [InlineData("UNL-115/219", RuneTrait.Orange)]
    [InlineData("OGN·162/298", RuneTrait.Orange)]
    [InlineData("OGN·162a/298", RuneTrait.Orange)]
    [InlineData("SFD·143/221", RuneTrait.Purple)]
    [InlineData("SFD·143a/221", RuneTrait.Purple)]
    [InlineData("UNL-082/219", RuneTrait.Blue)]
    [InlineData("UNL-082a/219", RuneTrait.Blue)]
    [InlineData("SFD·177/221", RuneTrait.Yellow)]
    [InlineData("SFD·177a/221", RuneTrait.Yellow)]
    [InlineData("UNL-127/219", RuneTrait.Purple)]
    [InlineData("SFD·068/221", RuneTrait.Blue)]
    [InlineData("SFD·103/221", RuneTrait.Orange)]
    [InlineData("SFD·179/221", RuneTrait.Yellow)]
    [InlineData("UNL-029/219", RuneTrait.Red)]
    [InlineData("UNL-029a/219", RuneTrait.Red)]
    [InlineData("OGN·075/298", RuneTrait.Green)]
    [InlineData("OGN·110/298", RuneTrait.Blue)]
    [InlineData("SFD·002/221", RuneTrait.Red)]
    [InlineData("SFD·131/221", RuneTrait.Purple)]
    [InlineData("OGN·150/298", RuneTrait.Orange)]
    [InlineData("OGN·151/298", RuneTrait.Orange)]
    [InlineData("OGN·151a/298", RuneTrait.Orange)]
    [InlineData("OGN·116/298", RuneTrait.Blue)]
    [InlineData("OGN·030/298", RuneTrait.Red)]
    [InlineData("OGN·030a/298", RuneTrait.Red)]
    public void P4HasteReadyProfilesCarryOfficialColoredPowerTrait(
        string cardNo,
        string expectedTrait)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));

        var profile = CardPermissionKeywordRules.BuildProfile(definition);

        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);
        Assert.Equal(expectedTrait, profile.HasteReadyPowerTrait);
        Assert.Equal(expectedTrait, definition.HasteReadyPowerTrait);
    }

    [Fact]
    public async Task P4PermissionKeywordProfileIncludesJinxHasteReadyDiscardBranch()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var jinxSpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·030/298", StringComparison.Ordinal));
        Assert.Contains(jinxSpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(jinxSpec.Keywords, keyword => string.Equals(keyword.Keyword, "强攻", StringComparison.Ordinal));
        Assert.Contains(jinxSpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));

        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·030/298", out var jinxDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(jinxDefinition);

        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);
        Assert.Contains("P4.56", profile.HasteOptionalReadyBranchReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P6SwiftTimingFlagsCoverSimpleOfficialSwiftSpellRepresentatives()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var simpleSwiftSpells = new[]
        {
            "OGS·003/024",
            "OGN·009/298",
            "OGN·050/298",
            "OGN·102/298",
            "OGN·172/298",
            "SFD·135/221"
        };

        foreach (var cardNo in simpleSwiftSpells)
        {
            var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            Assert.Equal("法术", spec.CardCategoryName);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
            Assert.Contains("{{迅捷}}", spec.OfficialText, StringComparison.Ordinal);
            Assert.DoesNotContain("{{迅捷>}}", spec.OfficialText, StringComparison.Ordinal);
            Assert.Contains(spec.Keywords, keyword => string.Equals(keyword.Keyword, "迅捷", StringComparison.Ordinal));

            Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
            var profile = CardPermissionKeywordRules.BuildProfile(definition);
            Assert.True(profile.HasSwift);
            Assert.False(profile.HasReaction);
            Assert.True(definition.CanPlayDuringSpellDuel);
        }
    }

    [Fact]
    public async Task P6ReactionTimingFlagsCoverSimpleOfficialReactionSpellRepresentatives()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var simpleReactionSpells = new[]
        {
            "SFD·087/221",
            "OGN·058/298",
            "OGN·093/298",
            "OGN·095/298",
            "UNL-066/219",
            "OGN·169/298"
        };

        foreach (var cardNo in simpleReactionSpells)
        {
            var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            Assert.Equal("法术", spec.CardCategoryName);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
            Assert.Contains("{{反应}}", spec.OfficialText, StringComparison.Ordinal);
            Assert.DoesNotContain("{{反应>}}", spec.OfficialText, StringComparison.Ordinal);
            Assert.Contains(spec.Keywords, keyword => string.Equals(keyword.Keyword, "反应", StringComparison.Ordinal));

            Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
            var profile = CardPermissionKeywordRules.BuildProfile(definition);
            Assert.False(profile.HasSwift);
            Assert.True(profile.HasReaction);
            Assert.True(definition.CanPlayDuringPriority);
        }
    }

    [Fact]
    public async Task P4PermissionKeywordProfileIncludesJinxAltAHasteReadyDiscardBranch()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var jinxAltASpec = specs.Single(spec => string.Equals(spec.CardNo, "OGN·030a/298", StringComparison.Ordinal));
        Assert.Contains(jinxAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "急速", StringComparison.Ordinal));
        Assert.Contains(jinxAltASpec.Keywords, keyword => string.Equals(keyword.Keyword, "强攻", StringComparison.Ordinal));
        Assert.Contains(jinxAltASpec.Cost.OptionalCosts, cost => cost.StartsWith("extra-pay", StringComparison.Ordinal));

        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·030a/298", out var jinxAltADefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(jinxAltADefinition);

        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);
        Assert.Contains("P4.57", profile.HasteOptionalReadyBranchReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P4EchoKeywordProfileMapsOfficialTextToRegistryOptionalCost()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var centerStageSpec = specs.Single(spec => string.Equals(spec.CardNo, "UNL-061/219", StringComparison.Ordinal));
        Assert.Contains(centerStageSpec.Keywords, keyword =>
            string.Equals(keyword.Keyword, CardInteractionKeywordNames.Echo, StringComparison.Ordinal));
        Assert.Contains(CardInteractionKeywordNames.Echo, centerStageSpec.OfficialText, StringComparison.Ordinal);
        Assert.Contains("echo", centerStageSpec.Cost.OptionalCosts);
        Assert.Contains(BehaviorTemplateIds.Echo, centerStageSpec.TemplateIds);

        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-061/219", out var centerStageDefinition));
        var echoProfile = CardInteractionKeywordRules.BuildEchoProfile(centerStageDefinition);
        Assert.True(echoProfile.HasEcho);
        Assert.Equal(2, echoProfile.EchoManaCost);
        Assert.Equal(EchoKeywordProfileStatuses.Implemented, echoProfile.Status);
        Assert.Contains("P2 optional cost repeat path", echoProfile.Reason, StringComparison.Ordinal);

        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-007/219", out var punishmentDefinition));
        var nonEchoProfile = CardInteractionKeywordRules.BuildEchoProfile(punishmentDefinition);
        Assert.False(nonEchoProfile.HasEcho);
        Assert.Equal(EchoKeywordProfileStatuses.NotApplicable, nonEchoProfile.Status);
    }

    [Fact]
    public async Task P4CombatKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var assaultPoro = BuildCombatProfile(specs, "OGN·210/298", CardCombatKeywordNames.Assault);
        Assert.True(assaultPoro.HasAssault);
        Assert.Equal(1, assaultPoro.AssaultAmount);
        Assert.Equal(CombatKeywordProfileStatuses.RecognizedDeferred, assaultPoro.Status);

        var mightyPoro = BuildCombatProfile(specs, "OGN·052/298", CardCombatKeywordNames.Steadfast);
        Assert.True(mightyPoro.HasSteadfast);
        Assert.Equal(1, mightyPoro.SteadfastAmount);

        var garen = BuildCombatProfile(specs, "OGS·007/024", CardCombatKeywordNames.Assault, CardCombatKeywordNames.Steadfast);
        Assert.True(garen.HasAssault);
        Assert.Equal(2, garen.AssaultAmount);
        Assert.True(garen.HasSteadfast);
        Assert.Equal(2, garen.SteadfastAmount);

        var mutantKitten = BuildCombatProfile(specs, "UNL-036/219", CardCombatKeywordNames.Steadfast, CardCombatKeywordNames.Bulwark);
        Assert.True(mutantKitten.HasSteadfast);
        Assert.Equal(2, mutantKitten.SteadfastAmount);
        Assert.True(mutantKitten.HasBulwark);

        var leblanc = BuildCombatProfile(specs, "UNL-090/219", CardCombatKeywordNames.BackRow);
        Assert.True(leblanc.HasBackRow);

        var bladeguard = BuildCombatProfile(specs, "SFD·096/221", CardCombatKeywordNames.Roam);
        Assert.True(bladeguard.HasRoam);
        Assert.Contains("deferred", bladeguard.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P4ResourceKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var gluttonousToadfrog = BuildResourceProfile(specs, "UNL-100/219", CardResourceKeywordNames.Hunt);
        Assert.True(gluttonousToadfrog.HasHunt);
        Assert.Equal(3, gluttonousToadfrog.HuntAmount);
        Assert.Equal(ResourceKeywordProfileStatuses.RecognizedDeferred, gluttonousToadfrog.Status);
        Assert.Contains("Hunt conquest/held battle experience", gluttonousToadfrog.Reason, StringComparison.Ordinal);

        var mossStepper = BuildResourceProfile(specs, "UNL-047/219", CardResourceKeywordNames.Hunt, CardResourceKeywordNames.Level);
        Assert.True(mossStepper.HasHunt);
        Assert.Equal(2, mossStepper.HuntAmount);
        Assert.True(mossStepper.HasLevel);
        Assert.Equal([3], mossStepper.LevelThresholds);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-047/219", out var mossStepperBehavior));
        Assert.Equal(3, mossStepperBehavior.LevelExperienceThreshold);
        Assert.Equal(1, mossStepperBehavior.LevelSourceUnitPowerBonus);
        Assert.Equal(CardObjectTags.Spellshield, mossStepperBehavior.LevelSourceUnitTags);

        var windrunnerFox = BuildResourceProfile(specs, "UNL-075/219", CardResourceKeywordNames.Hunt, CardResourceKeywordNames.Level);
        Assert.True(windrunnerFox.HasHunt);
        Assert.Equal(2, windrunnerFox.HuntAmount);
        Assert.True(windrunnerFox.HasLevel);
        Assert.Equal([3], windrunnerFox.LevelThresholds);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-075/219", out var windrunnerFoxBehavior));
        Assert.Equal(3, windrunnerFoxBehavior.LevelExperienceThreshold);
        Assert.Equal(1, windrunnerFoxBehavior.LevelSourceUnitPowerBonus);
        Assert.Equal("游走", windrunnerFoxBehavior.LevelSourceUnitTags);

        var wujiApprentice = BuildResourceProfile(specs, "UNL-040/219", CardResourceKeywordNames.Hunt, CardResourceKeywordNames.Level);
        Assert.True(wujiApprentice.HasHunt);
        Assert.Equal(1, wujiApprentice.HuntAmount);
        Assert.True(wujiApprentice.HasLevel);
        Assert.Equal([6], wujiApprentice.LevelThresholds);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-040/219", out var wujiApprenticeBehavior));
        Assert.Equal(6, wujiApprenticeBehavior.LevelExperienceThreshold);
        Assert.Equal(1, wujiApprenticeBehavior.LevelDrawOnPlayCount);

        var yi = BuildResourceProfile(specs, "UNL-113/219", CardResourceKeywordNames.Hunt, CardResourceKeywordNames.Level);
        Assert.True(yi.HasHunt);
        Assert.Equal(2, yi.HuntAmount);
        Assert.True(yi.HasLevel);
        Assert.Equal([6], yi.LevelThresholds);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-113/219", out var yiBehavior));
        Assert.Equal(6, yiBehavior.LevelExperienceThreshold);
        Assert.Equal(CardObjectTags.Spellshield + "|游走", yiBehavior.LevelSourceUnitTags);

        var yiAltA = BuildResourceProfile(specs, "UNL-113a/219", CardResourceKeywordNames.Hunt, CardResourceKeywordNames.Level);
        Assert.True(yiAltA.HasHunt);
        Assert.Equal(2, yiAltA.HuntAmount);
        Assert.True(yiAltA.HasLevel);
        Assert.Equal([6], yiAltA.LevelThresholds);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-113a/219", out var yiAltABehavior));
        Assert.Equal(6, yiAltABehavior.LevelExperienceThreshold);
        Assert.Equal(CardObjectTags.Spellshield + "|游走", yiAltABehavior.LevelSourceUnitTags);

        var noxianRecruit = BuildResourceProfile(specs, "OGN·012/298", CardResourceKeywordNames.Encourage);
        Assert.True(noxianRecruit.HasEncourage);
        Assert.Contains("deferred", noxianRecruit.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·012/298", out var noxianRecruitBehavior));
        Assert.Equal(
            CardCostReductionConditionKinds.ControllerPlayedAnotherCardThisTurn,
            noxianRecruitBehavior.CostReductionConditionKind);
        Assert.Equal(2, noxianRecruitBehavior.CostReductionMana);

        var dangerousDuo = BuildResourceProfile(specs, "OGN·016/298", CardResourceKeywordNames.Encourage);
        Assert.True(dangerousDuo.HasEncourage);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·016/298", out var dangerousDuoBehavior));
        Assert.Equal(1, dangerousDuoBehavior.RequiredTargetCount);
        Assert.Equal(CardTargetScopes.AnyUnit, dangerousDuoBehavior.TargetScope);
        Assert.Equal(CardObjectTags.UnitCard, dangerousDuoBehavior.TargetRequiredTag);
        Assert.Equal(2, dangerousDuoBehavior.PowerModifierAmount);
        Assert.Equal(
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn,
            dangerousDuoBehavior.TargetCountConditionKind);

        var junkyardBully = BuildResourceProfile(specs, "OGN·020/298", CardResourceKeywordNames.Encourage);
        Assert.True(junkyardBully.HasEncourage);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·020/298", out var junkyardBullyBehavior));
        Assert.Equal(2, junkyardBullyBehavior.RequiredTargetCount);
        Assert.Equal(CardTargetScopes.FriendlyHandCard, junkyardBullyBehavior.TargetScope);
        Assert.True(junkyardBullyBehavior.DiscardsTargetFromHand);
        Assert.Equal(2, junkyardBullyBehavior.DrawCount);
        Assert.Equal(
            CardDrawConditionKinds.PlayedAfterAnotherCardThisTurn,
            junkyardBullyBehavior.DrawConditionKind);
        Assert.Equal(
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn,
            junkyardBullyBehavior.TargetCountConditionKind);

        var vanguardCaptain = BuildResourceProfile(specs, "OGN·218/298", CardResourceKeywordNames.Encourage);
        Assert.True(vanguardCaptain.HasEncourage);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·218/298", out var vanguardCaptainBehavior));
        Assert.Equal(2, vanguardCaptainBehavior.CreatedBaseUnitTokenCount);
        Assert.Equal(1, vanguardCaptainBehavior.CreatedBaseUnitTokenPower);
        Assert.Equal("随从", vanguardCaptainBehavior.CreatedBaseUnitTokenName);
        Assert.Equal(CardObjectTags.UnitCard, vanguardCaptainBehavior.CreatedBaseUnitTokenTags);
        Assert.Equal(
            CardTokenCreationConditionKinds.PlayedAfterAnotherCardThisTurn,
            vanguardCaptainBehavior.CreatedBaseUnitTokenConditionKind);

        var trifarianGloryseeker = BuildResourceProfile(specs, "OGN·217/298", CardResourceKeywordNames.Encourage);
        Assert.True(trifarianGloryseeker.HasEncourage);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·217/298", out var trifarianGloryseekerBehavior));
        Assert.True(trifarianGloryseekerBehavior.GrantsBoonToSourceUnit);
        Assert.Equal(
            CardSourceBoonConditionKinds.PlayedAfterAnotherCardThisTurn,
            trifarianGloryseekerBehavior.SourceBoonConditionKind);

        var pluckyPoro = BuildResourceProfile(specs, "OGN·013/298", CardResourceKeywordNames.Spellshield);
        Assert.True(pluckyPoro.HasSpellshield);
        Assert.Equal(1, pluckyPoro.SpellshieldTax);

        var ornn = BuildResourceProfile(specs, "SFD·085/221", CardResourceKeywordNames.Spellshield);
        Assert.True(ornn.HasSpellshield);
        Assert.Equal(2, ornn.SpellshieldTax);
        Assert.Equal(
            2,
            CardResourceKeywordRules.SpellshieldTaxFromTags([CardObjectTags.UnitCard, "法盾2"]));
    }

    [Fact]
    public async Task P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var doransShield = BuildEquipmentProfile(specs, "SFD·033/221", CardEquipmentKeywordNames.Assemble);
        Assert.True(doransShield.HasAssemble);
        Assert.False(doransShield.HasAgile);
        Assert.False(doransShield.HasTempered);
        Assert.True(doransShield.HasImplementedRepresentativeAssembleBoundary);
        Assert.Equal(EquipmentKeywordProfileStatuses.ImplementedRepresentative, doransShield.Status);
        Assert.Contains("ASSEMBLE_EQUIPMENT", doransShield.Reason, StringComparison.Ordinal);

        var longSword = BuildEquipmentProfile(
            specs,
            "SFD·022/221",
            CardEquipmentKeywordNames.Agile,
            CardEquipmentKeywordNames.Assemble);
        Assert.True(longSword.HasAssemble);
        Assert.True(longSword.HasAgile);
        Assert.True(longSword.HasWeapon);
        Assert.False(longSword.HasTempered);
        Assert.True(longSword.HasImplementedRepresentativeAssembleBoundary);
        Assert.True(longSword.HasImplementedRepresentativeAgileDirectPlayAttachBoundary);
        Assert.True(longSword.HasImplementedRepresentativeEquipmentStateBoundary);
        Assert.Contains(
            "P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment",
            longSword.EquipmentStateRepresentativeVerifierTests);
        Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, longSword.Status);
        Assert.Contains("Agile direct-play attach", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("P5 equipment state representatives", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("Jax-granted Agile", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("ephemeral/static equipment breadth", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("full owner/controller breadth", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("full attach lifecycle breadth", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("LayerEngine", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("deferred", longSword.Reason, StringComparison.OrdinalIgnoreCase);

        var sentinelAdept = BuildEquipmentProfile(specs, "SFD·008/221", CardEquipmentKeywordNames.Tempered);
        Assert.False(sentinelAdept.HasAssemble);
        Assert.False(sentinelAdept.HasAgile);
        Assert.True(sentinelAdept.HasTempered);
        Assert.False(sentinelAdept.HasImplementedRepresentativeAssembleBoundary);
        Assert.False(sentinelAdept.HasImplementedRepresentativeAgileDirectPlayAttachBoundary);
        Assert.True(sentinelAdept.HasImplementedRepresentativeTemperedOptionalAttachBoundary);
        Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, sentinelAdept.Status);
        Assert.Contains("Tempered optional attach", sentinelAdept.Reason, StringComparison.Ordinal);
        Assert.Contains("full Tempered official breadth", sentinelAdept.Reason, StringComparison.Ordinal);
        Assert.Contains("deferred", sentinelAdept.Reason, StringComparison.OrdinalIgnoreCase);

        var armedAssaulter = BuildEquipmentProfile(specs, "SFD·002/221", CardEquipmentKeywordNames.Tempered);
        Assert.False(armedAssaulter.HasAssemble);
        Assert.False(armedAssaulter.HasAgile);
        Assert.True(armedAssaulter.HasTempered);
        Assert.False(armedAssaulter.HasImplementedRepresentativeAssembleBoundary);
        Assert.False(armedAssaulter.HasImplementedRepresentativeAgileDirectPlayAttachBoundary);
        Assert.True(armedAssaulter.HasImplementedRepresentativeTemperedOptionalAttachBoundary);
        Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, armedAssaulter.Status);
        Assert.Contains("Tempered optional attach", armedAssaulter.Reason, StringComparison.Ordinal);
        Assert.Contains("full Tempered official breadth", armedAssaulter.Reason, StringComparison.Ordinal);
        Assert.Contains("deferred", armedAssaulter.Reason, StringComparison.OrdinalIgnoreCase);

        foreach (var jaxCardNo in new[] { "SFD·119/221", "SFD·119a/221" })
        {
            var jax = BuildEquipmentProfile(specs, jaxCardNo, CardEquipmentKeywordNames.Tempered);
            Assert.True(jax.HasTempered);
            Assert.True(jax.HasImplementedRepresentativeTemperedOptionalAttachBoundary);
            Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, jax.Status);
            Assert.Contains("Tempered optional attach", jax.Reason, StringComparison.Ordinal);
            Assert.Contains("full Tempered official breadth", jax.Reason, StringComparison.Ordinal);
        }

        var ornn = BuildEquipmentProfile(specs, "SFD·085/221", CardEquipmentKeywordNames.Tempered);
        Assert.True(ornn.HasTempered);
        Assert.False(ornn.HasWeapon);
        Assert.True(ornn.HasImplementedRepresentativeFriendlyEquipmentStaticPowerBoundary);
        Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, ornn.Status);
        Assert.Contains("Ornn friendly-equipment static power", ornn.Reason, StringComparison.Ordinal);
        Assert.Contains("full Tempered official breadth", ornn.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task P5EquipmentStateAssembleLongSwordOwnerControllerFixtureProfileBindsExistingVerifierAnchors()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var longSword = BuildEquipmentProfile(
            specs,
            "SFD·022/221",
            CardEquipmentKeywordNames.Agile,
            CardEquipmentKeywordNames.Assemble);
        var stateRepresentative = Assert.Single(
            CardEquipmentKeywordRules.EquipmentStateRepresentatives,
            representative => string.Equals(representative.CardNo, "SFD·022/221", StringComparison.Ordinal));
        var verifierNames = typeof(ConformanceFixtureRunnerTests)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<FactAttribute>() is not null)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(CardEquipmentKeywordRules.IsEquipmentStateRepresentativeCardNo("SFD·022/221"));
        Assert.False(CardEquipmentKeywordRules.IsEquipmentStateRepresentativeCardNo("SFD·033/221"));
        Assert.True(longSword.HasImplementedRepresentativeEquipmentStateBoundary);
        Assert.Equal(stateRepresentative.VerifierTestNames, longSword.EquipmentStateRepresentativeVerifierTests);
        Assert.Equal("Long Sword", stateRepresentative.CardName);
        Assert.Contains("Long Sword owner/controller/attachment invariant", stateRepresentative.CoveredBoundaries);
        Assert.Contains("controller mismatch no-mutation rejection", stateRepresentative.CoveredBoundaries);
        Assert.Contains("controlled opponent-owned target attach", stateRepresentative.CoveredBoundaries);
        Assert.Contains("attached equipment follows host base-to-battlefield movement", stateRepresentative.CoveredBoundaries);
        Assert.Contains("attached equipment follows host battlefield-to-base movement", stateRepresentative.CoveredBoundaries);
        Assert.Contains("host destroyed detach/recall to owner base", stateRepresentative.CoveredBoundaries);
        Assert.All(stateRepresentative.VerifierTestNames, testName => Assert.Contains(testName, verifierNames));
        Assert.Contains(
            "CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed",
            stateRepresentative.VerifierTestNames);
        Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, longSword.Status);
        Assert.Contains("full owner/controller breadth", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("full attach lifecycle breadth", longSword.Reason, StringComparison.Ordinal);
        Assert.Contains("full equipment official coverage", longSword.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var takeUpSpec = specs.Single(spec => string.Equals(spec.CardNo, "SFD·011/221", StringComparison.Ordinal));
        Assert.Contains(CardPermissionKeywordNames.Reaction, takeUpSpec.OfficialText, StringComparison.Ordinal);
        Assert.Contains("武装", takeUpSpec.OfficialText, StringComparison.Ordinal);
        Assert.Contains("贴附", takeUpSpec.OfficialText, StringComparison.Ordinal);
        Assert.Contains("卸除", takeUpSpec.OfficialText, StringComparison.Ordinal);

        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·011/221", out var takeUpDefinition));
        var profile = CardEquipmentKeywordRules.BuildAttachmentProfile(takeUpDefinition);

        Assert.True(profile.CanAttachOrDetachWeapon);
        Assert.Equal(1, profile.DrawCount);
        Assert.Equal(EquipmentAttachmentProfileStatuses.ImplementedRepresentative, profile.Status);
        Assert.Contains("P4.58", profile.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deferred", profile.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P4LifecycleKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var maskedAttendant = BuildLifecycleProfile(specs, "UNL-081/219", CardLifecycleKeywordNames.Ephemeral);
        Assert.True(maskedAttendant.HasEphemeral);
        Assert.False(maskedAttendant.HasLastBreath);
        Assert.Equal(LifecycleKeywordProfileStatuses.Implemented, maskedAttendant.Status);

        var scryingShell = BuildLifecycleProfile(specs, "UNL-161/219", CardLifecycleKeywordNames.Predict);
        Assert.True(scryingShell.HasPredict);
        Assert.True(scryingShell.HasPredictRecyclePath);
        Assert.Equal(LifecycleKeywordProfileStatuses.RecognizedDelegated, scryingShell.Status);

        var kogmaw = BuildLifecycleProfile(specs, "OGN·190/298", CardLifecycleKeywordNames.LastBreath);
        Assert.True(kogmaw.HasLastBreath);
        Assert.Equal(LifecycleKeywordProfileStatuses.RecognizedDeferred, kogmaw.Status);
        Assert.Contains("deferred", kogmaw.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P4InteractionKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var centerStage = BuildInteractionProfile(specs, "UNL-061/219", CardInteractionKeywordNames.Echo);
        Assert.True(centerStage.HasEcho);
        Assert.Equal(2, centerStage.EchoManaCost);
        Assert.Equal(InteractionKeywordProfileStatuses.Implemented, centerStage.Status);

        var tidecaller = BuildInteractionProfile(specs, "OGN·199/298", CardInteractionKeywordNames.Standby);
        Assert.True(tidecaller.HasStandby);
        Assert.False(tidecaller.HasAmbush);
        Assert.Equal(InteractionKeywordProfileStatuses.RecognizedDeferred, tidecaller.Status);

        var gloomyApothecary = BuildInteractionProfile(specs, "UNL-021/219", CardInteractionKeywordNames.Ambush);
        Assert.True(gloomyApothecary.HasAmbush);
        Assert.Contains("Ambush", gloomyApothecary.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task P4BasicActionProfilesCoverPrimitiveDelegatedAndDeferredActions()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var prophetsOmen = BuildBasicActionProfile(specs, "SFD·087/221");
        Assert.True(prophetsOmen.HasDraw);
        Assert.Contains(CardBasicActionNames.Draw, prophetsOmen.PrimitiveActions);
        Assert.Equal(CardBasicActionProfileStatuses.RecognizedCovered, prophetsOmen.Status);

        var charm = BuildBasicActionProfile(specs, "OGN·043/298");
        Assert.True(charm.HasMove);
        Assert.Contains(CardBasicActionNames.Move, charm.DelegatedP2Actions);

        var zaunBouncer = BuildBasicActionProfile(specs, "OGN·188/298");
        Assert.True(zaunBouncer.HasRecall);
        Assert.Contains(CardBasicActionNames.Recall, zaunBouncer.DelegatedP2Actions);

        var disposalOrderRecycle = BuildBasicActionProfile(
            specs,
            "UNL-103/219",
            "DISPOSAL_ORDER_RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3");
        Assert.True(disposalOrderRecycle.HasRecycle);
        Assert.Contains(CardBasicActionNames.Recycle, disposalOrderRecycle.DelegatedP2Actions);

        var portalpalRescue = BuildBasicActionProfile(specs, "OGN·102/298");
        Assert.True(portalpalRescue.HasBanish);
        Assert.Contains(CardBasicActionNames.Banish, portalpalRescue.DelegatedP2Actions);

        var secretArtMercy = BuildBasicActionProfile(specs, "OGN·053/298");
        Assert.True(secretArtMercy.HasBoon);
        Assert.Contains(CardBasicActionNames.Boon, secretArtMercy.DelegatedP2Actions);

        var dangerousDuo = BuildBasicActionProfile(specs, "OGN·016/298");
        Assert.True(dangerousDuo.HasTempMight);
        Assert.Contains(CardBasicActionNames.TempMight, dangerousDuo.PrimitiveActions);
        Assert.DoesNotContain(CardBasicActionNames.TempMight, dangerousDuo.DeferredActions);

        var junkyardBully = BuildBasicActionProfile(specs, "OGN·020/298");
        Assert.True(junkyardBully.HasDraw);
        Assert.Contains(CardBasicActionNames.Draw, junkyardBully.PrimitiveActions);
        Assert.DoesNotContain(CardBasicActionNames.Draw, junkyardBully.DeferredActions);

        var trifarianGloryseeker = BuildBasicActionProfile(specs, "OGN·217/298");
        Assert.True(trifarianGloryseeker.HasBoon);
        Assert.Contains(CardBasicActionNames.Boon, trifarianGloryseeker.DelegatedP2Actions);
        Assert.DoesNotContain(CardBasicActionNames.Boon, trifarianGloryseeker.DeferredActions);

        var shepherdsHeirloom = BuildBasicActionProfile(specs, "UNL-158/219");
        Assert.True(shepherdsHeirloom.HasExperience);
        Assert.Contains(CardBasicActionNames.Experience, shepherdsHeirloom.DelegatedP2Actions);
        Assert.Equal(CardBasicActionProfileStatuses.RecognizedCovered, shepherdsHeirloom.Status);

        var poppy = BuildBasicActionProfile(specs, "UNL-178/219");
        Assert.True(poppy.HasExperience);
        Assert.Contains(CardBasicActionNames.Experience, poppy.DelegatedP2Actions);
        Assert.DoesNotContain(CardBasicActionNames.Experience, poppy.DeferredActions);
        Assert.Equal(CardBasicActionProfileStatuses.MixedDeferred, poppy.Status);

        var wujiApprentice = BuildBasicActionProfile(specs, "UNL-040/219");
        Assert.True(wujiApprentice.HasDraw);
        Assert.Contains(CardBasicActionNames.Draw, wujiApprentice.PrimitiveActions);
        Assert.Contains(CardBasicActionNames.Experience, wujiApprentice.DeferredActions);
        Assert.Equal(CardBasicActionProfileStatuses.MixedDeferred, wujiApprentice.Status);

        var sternSergeant = BuildBasicActionProfile(specs, "UNL-157/219");
        Assert.True(sternSergeant.HasExperience);
        Assert.Contains(CardBasicActionNames.Experience, sternSergeant.DelegatedP2Actions);
        Assert.DoesNotContain(CardBasicActionNames.Experience, sternSergeant.DeferredActions);
        Assert.Equal(CardBasicActionProfileStatuses.RecognizedCovered, sternSergeant.Status);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-157/219", out var sternSergeantBehavior));
        Assert.Equal(1, sternSergeantBehavior.GainExperienceOnPlayPerFriendlyFieldUnit);

        var safetyInspector = BuildBasicActionProfile(specs, "UNL-164/219");
        Assert.True(safetyInspector.HasExperience);
        Assert.DoesNotContain(CardBasicActionNames.Experience, safetyInspector.DelegatedP2Actions);
        Assert.Contains(CardBasicActionNames.Experience, safetyInspector.DeferredActions);
        Assert.Equal(CardBasicActionProfileStatuses.MixedDeferred, safetyInspector.Status);
    }

    [Fact]
    public async Task P4ObjectiveNamedSurfacesHaveRepresentativeCoverage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
        var covered = new HashSet<string>(StringComparer.Ordinal);

        void Cover(string key, bool condition)
        {
            Assert.True(condition, $"{key} does not have a P4 representative coverage artifact.");
            Assert.True(covered.Add(key), $"Duplicate P4 coverage key: {key}");
        }

        CardPermissionKeywordProfile PermissionProfile(string cardNo)
        {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
            return CardPermissionKeywordRules.BuildProfile(definition);
        }

        var swift = PermissionProfile("OGN·004/298");
        Cover("permission:迅捷", swift.HasSwift);
        var reaction = PermissionProfile("OGN·064/298");
        Cover("permission:反应", reaction.HasReaction);
        var haste = PermissionProfile("OGN·001/298");
        Cover(
            "permission:急速",
            haste.HasHaste
                && string.Equals(
                    haste.HasteOptionalReadyBranchStatus,
                    HasteOptionalReadyBranchStatuses.ImplementedRepresentative,
                    StringComparison.Ordinal));

        var assault = BuildCombatProfile(specs, "OGN·210/298", CardCombatKeywordNames.Assault);
        Cover("combat:强攻", assault.HasAssault && assault.AssaultAmount > 0);
        var steadfast = BuildCombatProfile(specs, "OGN·052/298", CardCombatKeywordNames.Steadfast);
        Cover("combat:坚守", steadfast.HasSteadfast && steadfast.SteadfastAmount > 0);
        var bulwark = BuildCombatProfile(specs, "UNL-036/219", CardCombatKeywordNames.Bulwark);
        Cover("combat:壁垒", bulwark.HasBulwark);
        var backRow = BuildCombatProfile(specs, "UNL-090/219", CardCombatKeywordNames.BackRow);
        Cover("combat:后排", backRow.HasBackRow);
        var roam = BuildCombatProfile(specs, "SFD·096/221", CardCombatKeywordNames.Roam);
        Cover("combat:游走", roam.HasRoam);

        var ephemeral = BuildLifecycleProfile(specs, "UNL-081/219", CardLifecycleKeywordNames.Ephemeral);
        Cover(
            "lifecycle:瞬息",
            ephemeral.HasEphemeral
                && string.Equals(ephemeral.Status, LifecycleKeywordProfileStatuses.Implemented, StringComparison.Ordinal));
        var lastBreath = BuildLifecycleProfile(specs, "OGN·190/298", CardLifecycleKeywordNames.LastBreath);
        Cover(
            "lifecycle:绝念",
            lastBreath.HasLastBreath
                && string.Equals(lastBreath.Status, LifecycleKeywordProfileStatuses.RecognizedDeferred, StringComparison.Ordinal));
        var predict = BuildLifecycleProfile(specs, "UNL-161/219", CardLifecycleKeywordNames.Predict);
        Cover(
            "lifecycle:预知",
            predict.HasPredict
                && string.Equals(predict.Status, LifecycleKeywordProfileStatuses.RecognizedDelegated, StringComparison.Ordinal));

        var hunt = BuildResourceProfile(specs, "UNL-100/219", CardResourceKeywordNames.Hunt);
        Cover("resource:狩猎", hunt.HasHunt && hunt.HuntAmount > 0);
        var level = BuildResourceProfile(specs, "UNL-047/219", CardResourceKeywordNames.Level);
        Cover("resource:等级", level.HasLevel && level.LevelThresholds.Count > 0);
        var encourage = BuildResourceProfile(specs, "OGN·012/298", CardResourceKeywordNames.Encourage);
        Cover("resource:鼓舞", encourage.HasEncourage);
        var spellshield = BuildResourceProfile(specs, "OGN·013/298", CardResourceKeywordNames.Spellshield);
        Cover("resource:法盾", spellshield.HasSpellshield && spellshield.SpellshieldTax == 1);

        var standby = BuildInteractionProfile(specs, "OGN·121/298", CardInteractionKeywordNames.Standby);
        Cover("interaction:待命", standby.HasStandby);
        var echo = BuildInteractionProfile(specs, "UNL-061/219", CardInteractionKeywordNames.Echo);
        Cover(
            "interaction:回响",
            echo.HasEcho
                && string.Equals(echo.Status, InteractionKeywordProfileStatuses.Implemented, StringComparison.Ordinal));
        var ambush = BuildInteractionProfile(specs, "UNL-021/219", CardInteractionKeywordNames.Ambush);
        Cover("interaction:伏击", ambush.HasAmbush);

        var assemble = BuildEquipmentProfile(specs, "SFD·033/221", CardEquipmentKeywordNames.Assemble);
        Cover("equipment:装配", assemble.HasAssemble);
        var agile = BuildEquipmentProfile(
            specs,
            "SFD·022/221",
            CardEquipmentKeywordNames.Agile,
            CardEquipmentKeywordNames.Assemble);
        Cover("equipment:灵便", agile.HasAgile && agile.HasImplementedRepresentativeAgileDirectPlayAttachBoundary);
        var tempered = BuildEquipmentProfile(specs, "SFD·008/221", CardEquipmentKeywordNames.Tempered);
        Cover("equipment:百炼", tempered.HasTempered && tempered.HasImplementedRepresentativeTemperedOptionalAttachBoundary);

        var draw = BuildBasicActionProfile(specs, "SFD·087/221");
        Cover("basic:抽牌", draw.PrimitiveActions.Contains(CardBasicActionNames.Draw, StringComparer.Ordinal));
        var damage = BuildBasicActionProfile(specs, "OGS·003/024");
        Cover("basic:伤害", damage.PrimitiveActions.Contains(CardBasicActionNames.Damage, StringComparer.Ordinal));
        var destroy = BuildBasicActionProfile(specs, "OGN·229/298");
        Cover("basic:摧毁", destroy.PrimitiveActions.Contains(CardBasicActionNames.Destroy, StringComparer.Ordinal));
        var stun = BuildBasicActionProfile(specs, "OGN·050/298");
        Cover("basic:眩晕", stun.PrimitiveActions.Contains(CardBasicActionNames.Stun, StringComparer.Ordinal));
        var tempMight = BuildBasicActionProfile(specs, "OGN·004/298");
        Cover("basic:临时战力", tempMight.PrimitiveActions.Contains(CardBasicActionNames.TempMight, StringComparer.Ordinal));
        var move = BuildBasicActionProfile(specs, "OGN·043/298");
        Cover("basic:移动", move.DelegatedP2Actions.Contains(CardBasicActionNames.Move, StringComparer.Ordinal));
        var recall = BuildBasicActionProfile(specs, "OGN·188/298");
        Cover("basic:召回", recall.DelegatedP2Actions.Contains(CardBasicActionNames.Recall, StringComparer.Ordinal));
        var recycle = BuildBasicActionProfile(specs, "OGN·156/298");
        Cover("basic:回收", recycle.DelegatedP2Actions.Contains(CardBasicActionNames.Recycle, StringComparer.Ordinal));
        var banish = BuildBasicActionProfile(specs, "OGN·102/298");
        Cover("basic:放逐", banish.DelegatedP2Actions.Contains(CardBasicActionNames.Banish, StringComparer.Ordinal));
        var boon = BuildBasicActionProfile(specs, "OGN·053/298");
        Cover("basic:增益", boon.DelegatedP2Actions.Contains(CardBasicActionNames.Boon, StringComparer.Ordinal));
        var experienceGain = BuildBasicActionProfile(specs, "UNL-158/219");
        Cover(
            "basic:经验获得",
            experienceGain.DelegatedP2Actions.Contains(CardBasicActionNames.Experience, StringComparer.Ordinal));
        var experienceSpend = BuildBasicActionProfile(specs, "UNL-178/219");
        Cover(
            "basic:经验消耗",
            experienceSpend.DelegatedP2Actions.Contains(CardBasicActionNames.Experience, StringComparer.Ordinal));

        var expected = new[]
        {
            "permission:迅捷",
            "permission:反应",
            "permission:急速",
            "combat:强攻",
            "combat:坚守",
            "combat:壁垒",
            "combat:后排",
            "combat:游走",
            "lifecycle:瞬息",
            "lifecycle:绝念",
            "lifecycle:预知",
            "resource:狩猎",
            "resource:等级",
            "resource:鼓舞",
            "resource:法盾",
            "interaction:待命",
            "interaction:回响",
            "interaction:伏击",
            "equipment:装配",
            "equipment:灵便",
            "equipment:百炼",
            "basic:抽牌",
            "basic:伤害",
            "basic:摧毁",
            "basic:眩晕",
            "basic:移动",
            "basic:召回",
            "basic:回收",
            "basic:放逐",
            "basic:临时战力",
            "basic:增益",
            "basic:经验获得",
            "basic:经验消耗"
        };

        Assert.Equal(
            expected.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            covered.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task UncoveredPlayableFunctionalUnitsAreKnownComplexP2ScopeBlocks()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var uncoveredUnits = FunctionalUnitBuilder.Build(catalog.Cards)
            .Where(unit => !unit.Cards.Any(card => CardBehaviorRegistry.TryGetByCardNo(card.CardNo, out _)))
            .ToArray();

        var uncoveredPlayableUnits = uncoveredUnits
            .Where(unit => string.Equals(unit.Category, "法术", StringComparison.Ordinal)
                || string.Equals(unit.Category, "单位", StringComparison.Ordinal)
                || string.Equals(unit.Category, "装备", StringComparison.Ordinal))
            .Select(unit => $"{unit.RepresentativeNo} {unit.Name} {unit.Category}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(uncoveredPlayableUnits);

        var uncoveredNonPlayableCategories = uncoveredUnits
            .Where(unit => !uncoveredPlayableUnits.Any(value => value.StartsWith(unit.RepresentativeNo, StringComparison.Ordinal)))
            .GroupBy(unit => unit.Category, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(6, uncoveredNonPlayableCategories["符文"]);
        Assert.Equal(44, uncoveredNonPlayableCategories["传奇"]);
        Assert.Equal(54, uncoveredNonPlayableCategories["战场"]);
        Assert.Equal(2, uncoveredNonPlayableCategories["指示物战场"]);
        Assert.Equal(9, uncoveredNonPlayableCategories["指示物单位"]);
        Assert.Equal(2, uncoveredNonPlayableCategories["指示物装备"]);
    }

    private static IReadOnlyList<ImplementedCardBehavior> ImplementedBehaviors(IReadOnlyList<OfficialCard> cards)
    {
        var playCardBehaviors = CardBehaviorRegistry.GetAll()
            .Select(definition => new ImplementedCardBehavior(
                definition.CardNo,
                definition.EffectKind,
                definition.DisplayName))
            .ToArray();

        return OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(cards, playCardBehaviors);
    }

    private static OfficialCard Card(OfficialCardCatalog catalog, string cardNo)
    {
        return catalog.Cards.Single(card => string.Equals(card.CardNo, cardNo, StringComparison.Ordinal));
    }

    private static void AssertFamily(
        BehaviorTemplateFamilyCoverageReport report,
        string templateId,
        int entries,
        int implementedEntries,
        int manualRuleRequiredEntries,
        int unimplementedEntries,
        int functionalUnits,
        int implementedFunctionalUnits,
        int pendingFunctionalUnits)
    {
        var family = Assert.Single(report.Families, candidate => string.Equals(
            candidate.TemplateId,
            templateId,
            StringComparison.Ordinal));
        Assert.Equal(entries, family.Entries);
        Assert.Equal(implementedEntries, family.ImplementedEntries);
        Assert.Equal(manualRuleRequiredEntries, family.ManualRuleRequiredEntries);
        Assert.Equal(unimplementedEntries, family.UnimplementedEntries);
        Assert.Equal(functionalUnits, family.FunctionalUnits);
        Assert.Equal(implementedFunctionalUnits, family.ImplementedFunctionalUnits);
        Assert.Equal(pendingFunctionalUnits, family.PendingFunctionalUnits);
    }

    private static IReadOnlyList<InteractionKeywordCoverageRow> BuildInteractionKeywordCoverageRows(
        IReadOnlyList<BehaviorSpec> specs,
        IReadOnlyList<string> keywords)
    {
        return keywords
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(keyword =>
            {
                var keywordSpecs = specs
                    .Where(spec => spec.Keywords.Any(candidate => string.Equals(
                        candidate.Keyword,
                        keyword,
                        StringComparison.Ordinal)))
                    .ToArray();
                var profileRows = keywordSpecs
                    .Select(spec =>
                    {
                        CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition);
                        return new
                        {
                            Spec = spec,
                            Profile = CardInteractionKeywordRules.BuildProfile(spec, definition)
                        };
                    })
                    .ToArray();
                var specUnitGroups = keywordSpecs
                    .GroupBy(spec => spec.FunctionalUnitId, StringComparer.Ordinal)
                    .ToArray();
                var profileUnitGroups = profileRows
                    .GroupBy(row => row.Spec.FunctionalUnitId, StringComparer.Ordinal)
                    .ToArray();

                return new InteractionKeywordCoverageRow(
                    keyword,
                    keywordSpecs.Length,
                    keywordSpecs.Count(spec => string.Equals(
                        spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal)),
                    specUnitGroups.Length,
                    specUnitGroups.Count(group => group.Any(spec => string.Equals(
                        spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal))),
                    profileRows.Count(row => string.Equals(
                        row.Profile.Status,
                        InteractionKeywordProfileStatuses.Implemented,
                        StringComparison.Ordinal)),
                    profileRows.Count(row => string.Equals(
                        row.Profile.Status,
                        InteractionKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal)),
                    profileUnitGroups.Count(group => group.Any(row => string.Equals(
                        row.Profile.Status,
                        InteractionKeywordProfileStatuses.Implemented,
                        StringComparison.Ordinal))),
                    profileUnitGroups.Count(group => group.All(row => string.Equals(
                        row.Profile.Status,
                        InteractionKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal))));
            })
            .ToArray();
    }

    private static void AssertInteractionKeywordCoverage(
        IReadOnlyList<InteractionKeywordCoverageRow> rows,
        string keyword,
        int entries,
        int specImplementedEntries,
        int functionalUnits,
        int specImplementedFunctionalUnits,
        int profileImplementedEntries,
        int profileDeferredEntries,
        int profileImplementedFunctionalUnits,
        int profileDeferredFunctionalUnits)
    {
        var row = Assert.Single(rows, candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
        Assert.Equal(entries, row.Entries);
        Assert.Equal(specImplementedEntries, row.SpecImplementedEntries);
        Assert.Equal(functionalUnits, row.FunctionalUnits);
        Assert.Equal(specImplementedFunctionalUnits, row.SpecImplementedFunctionalUnits);
        Assert.Equal(profileImplementedEntries, row.ProfileImplementedEntries);
        Assert.Equal(profileDeferredEntries, row.ProfileDeferredEntries);
        Assert.Equal(profileImplementedFunctionalUnits, row.ProfileImplementedFunctionalUnits);
        Assert.Equal(profileDeferredFunctionalUnits, row.ProfileDeferredFunctionalUnits);
    }

    private sealed record InteractionKeywordCoverageRow(
        string Keyword,
        int Entries,
        int SpecImplementedEntries,
        int FunctionalUnits,
        int SpecImplementedFunctionalUnits,
        int ProfileImplementedEntries,
        int ProfileDeferredEntries,
        int ProfileImplementedFunctionalUnits,
        int ProfileDeferredFunctionalUnits);

    private static IReadOnlyList<EquipmentKeywordCoverageRow> BuildEquipmentKeywordCoverageRows(
        IReadOnlyList<BehaviorSpec> specs,
        IReadOnlyList<string> keywords)
    {
        var profileRows = specs
            .Select(spec =>
            {
                CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition);
                return new
                {
                    Spec = spec,
                    Profile = CardEquipmentKeywordRules.BuildProfile(spec, definition)
                };
            })
            .ToArray();

        return keywords
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(keyword =>
            {
                var keywordRows = profileRows
                    .Where(row => HasEquipmentKeyword(row.Profile, keyword))
                    .ToArray();
                var unitGroups = keywordRows
                    .GroupBy(row => row.Spec.FunctionalUnitId, StringComparer.Ordinal)
                    .ToArray();

                return new EquipmentKeywordCoverageRow(
                    keyword,
                    keywordRows.Length,
                    keywordRows.Count(row => string.Equals(
                        row.Spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal)),
                    unitGroups.Length,
                    unitGroups.Count(group => group.Any(row => string.Equals(
                        row.Spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal))),
                    keywordRows.Count(row => string.Equals(
                        EquipmentKeywordStatus(row.Profile, keyword),
                        EquipmentKeywordProfileStatuses.ImplementedRepresentative,
                        StringComparison.Ordinal)),
                    keywordRows.Count(row => string.Equals(
                        EquipmentKeywordStatus(row.Profile, keyword),
                        EquipmentKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal)),
                    unitGroups.Count(group => group.Any(row => string.Equals(
                        EquipmentKeywordStatus(row.Profile, keyword),
                        EquipmentKeywordProfileStatuses.ImplementedRepresentative,
                        StringComparison.Ordinal))),
                    unitGroups.Count(group => group.All(row => string.Equals(
                        EquipmentKeywordStatus(row.Profile, keyword),
                        EquipmentKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal))));
            })
            .ToArray();
    }

    private static bool HasEquipmentKeyword(CardEquipmentKeywordProfile profile, string keyword)
    {
        return keyword switch
        {
            CardEquipmentKeywordNames.Assemble => profile.HasAssemble,
            CardEquipmentKeywordNames.Agile => profile.HasAgile,
            CardEquipmentKeywordNames.Tempered => profile.HasTempered,
            CardEquipmentKeywordNames.Weapon => profile.HasWeapon,
            _ => false
        };
    }

    private static string EquipmentKeywordStatus(CardEquipmentKeywordProfile profile, string keyword)
    {
        return string.Equals(keyword, CardEquipmentKeywordNames.Assemble, StringComparison.Ordinal)
            && profile.HasImplementedRepresentativeAssembleBoundary
                ? EquipmentKeywordProfileStatuses.ImplementedRepresentative
                : EquipmentKeywordProfileStatuses.RecognizedDeferred;
    }

    private static void AssertEquipmentKeywordCoverage(
        IReadOnlyList<EquipmentKeywordCoverageRow> rows,
        string keyword,
        int entries,
        int specImplementedEntries,
        int functionalUnits,
        int specImplementedFunctionalUnits,
        int profileImplementedEntries,
        int profileDeferredEntries,
        int profileImplementedFunctionalUnits,
        int profileDeferredFunctionalUnits)
    {
        var row = Assert.Single(rows, candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
        Assert.Equal(entries, row.Entries);
        Assert.Equal(specImplementedEntries, row.SpecImplementedEntries);
        Assert.Equal(functionalUnits, row.FunctionalUnits);
        Assert.Equal(specImplementedFunctionalUnits, row.SpecImplementedFunctionalUnits);
        Assert.Equal(profileImplementedEntries, row.ProfileImplementedEntries);
        Assert.Equal(profileDeferredEntries, row.ProfileDeferredEntries);
        Assert.Equal(profileImplementedFunctionalUnits, row.ProfileImplementedFunctionalUnits);
        Assert.Equal(profileDeferredFunctionalUnits, row.ProfileDeferredFunctionalUnits);
    }

    private sealed record EquipmentKeywordCoverageRow(
        string Keyword,
        int Entries,
        int SpecImplementedEntries,
        int FunctionalUnits,
        int SpecImplementedFunctionalUnits,
        int ProfileImplementedEntries,
        int ProfileDeferredEntries,
        int ProfileImplementedFunctionalUnits,
        int ProfileDeferredFunctionalUnits);

    private static IReadOnlyList<ResourceKeywordCoverageRow> BuildResourceKeywordCoverageRows(
        IReadOnlyList<BehaviorSpec> specs,
        IReadOnlyList<string> keywords)
    {
        var profileRows = specs
            .Select(spec =>
            {
                CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition);
                return new
                {
                    Spec = spec,
                    Definition = definition,
                    Profile = CardResourceKeywordRules.BuildProfile(spec, definition)
                };
            })
            .ToArray();

        return keywords
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(keyword =>
            {
                var keywordRows = profileRows
                    .Where(row => HasResourceKeyword(row.Profile, keyword))
                    .ToArray();
                var unitGroups = keywordRows
                    .GroupBy(row => row.Spec.FunctionalUnitId, StringComparer.Ordinal)
                    .ToArray();

                return new ResourceKeywordCoverageRow(
                    keyword,
                    keywordRows.Length,
                    keywordRows.Count(row => string.Equals(
                        row.Spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal)),
                    unitGroups.Length,
                    unitGroups.Count(group => group.Any(row => string.Equals(
                        row.Spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal))),
                    keywordRows.Count(row => HasResourceExecutionBoundary(keyword, row.Definition)),
                    unitGroups.Count(group => group.Any(row => HasResourceExecutionBoundary(keyword, row.Definition))),
                    keywordRows.Count(row => string.Equals(
                        row.Profile.Status,
                        ResourceKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal)),
                    unitGroups.Count(group => group.All(row => string.Equals(
                        row.Profile.Status,
                        ResourceKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal))));
            })
            .ToArray();
    }

    private static bool HasResourceKeyword(CardResourceKeywordProfile profile, string keyword)
    {
        return keyword switch
        {
            CardResourceKeywordNames.Hunt => profile.HasHunt,
            CardResourceKeywordNames.Level => profile.HasLevel,
            CardResourceKeywordNames.Encourage => profile.HasEncourage,
            CardResourceKeywordNames.Spellshield => profile.HasSpellshield,
            _ => false
        };
    }

    private static bool HasResourceExecutionBoundary(
        string keyword,
        CardBehaviorDefinition? definition)
    {
        if (definition is null)
        {
            return false;
        }

        return keyword switch
        {
            CardResourceKeywordNames.Hunt => CardResourceKeywordRules.HuntAmountFromTags(SourceTags(definition)) > 0,
            CardResourceKeywordNames.Level => definition.LevelExperienceThreshold > 0,
            CardResourceKeywordNames.Encourage => definition.CostReductionConditionKind == CardCostReductionConditionKinds.ControllerPlayedAnotherCardThisTurn
                || definition.DrawConditionKind == CardDrawConditionKinds.PlayedAfterAnotherCardThisTurn
                || definition.SourceBoonConditionKind == CardSourceBoonConditionKinds.PlayedAfterAnotherCardThisTurn
                || definition.TargetCountConditionKind == CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn
                || definition.CreatedBaseUnitTokenConditionKind == CardTokenCreationConditionKinds.PlayedAfterAnotherCardThisTurn,
            CardResourceKeywordNames.Spellshield => CardResourceKeywordRules.SpellshieldTaxFromTags(SourceTags(definition)) > 0,
            _ => false
        };
    }

    private static bool HasExperienceBehavior(CardBehaviorDefinition? definition)
    {
        return definition is not null
            && (definition.GainExperienceOnPlay > 0
                || definition.GainExperienceOnPlayPerFriendlyFieldUnit > 0
                || definition.OptionalExperienceCost > 0);
    }

    private static IReadOnlyList<string> SourceTags(CardBehaviorDefinition definition)
    {
        return ParseDelimitedValues(definition.SourceUnitTags)
            .Concat(ParseDelimitedValues(definition.SourceEquipmentTags))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
    }

    private static void AssertResourceKeywordCoverage(
        IReadOnlyList<ResourceKeywordCoverageRow> rows,
        string keyword,
        int entries,
        int specImplementedEntries,
        int functionalUnits,
        int specImplementedFunctionalUnits,
        int registryExecutionEntries,
        int registryExecutionFunctionalUnits,
        int profileDeferredEntries,
        int profileDeferredFunctionalUnits)
    {
        var row = Assert.Single(rows, candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
        Assert.Equal(entries, row.Entries);
        Assert.Equal(specImplementedEntries, row.SpecImplementedEntries);
        Assert.Equal(functionalUnits, row.FunctionalUnits);
        Assert.Equal(specImplementedFunctionalUnits, row.SpecImplementedFunctionalUnits);
        Assert.Equal(registryExecutionEntries, row.RegistryExecutionEntries);
        Assert.Equal(registryExecutionFunctionalUnits, row.RegistryExecutionFunctionalUnits);
        Assert.Equal(profileDeferredEntries, row.ProfileDeferredEntries);
        Assert.Equal(profileDeferredFunctionalUnits, row.ProfileDeferredFunctionalUnits);
    }

    private sealed record ResourceKeywordCoverageRow(
        string Keyword,
        int Entries,
        int SpecImplementedEntries,
        int FunctionalUnits,
        int SpecImplementedFunctionalUnits,
        int RegistryExecutionEntries,
        int RegistryExecutionFunctionalUnits,
        int ProfileDeferredEntries,
        int ProfileDeferredFunctionalUnits);

    private static IReadOnlyList<LifecycleKeywordCoverageRow> BuildLifecycleKeywordCoverageRows(
        IReadOnlyList<BehaviorSpec> specs,
        IReadOnlyList<string> keywords)
    {
        var profileRows = specs
            .Select(spec =>
            {
                CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition);
                return new
                {
                    Spec = spec,
                    Profile = CardLifecycleKeywordRules.BuildProfile(spec, definition)
                };
            })
            .ToArray();

        return keywords
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(keyword =>
            {
                var keywordRows = profileRows
                    .Where(row => HasLifecycleKeyword(row.Profile, keyword))
                    .ToArray();
                var unitGroups = keywordRows
                    .GroupBy(row => row.Spec.FunctionalUnitId, StringComparer.Ordinal)
                    .ToArray();

                return new LifecycleKeywordCoverageRow(
                    keyword,
                    keywordRows.Length,
                    keywordRows.Count(row => string.Equals(
                        row.Spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal)),
                    unitGroups.Length,
                    unitGroups.Count(group => group.Any(row => string.Equals(
                        row.Spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal))),
                    keywordRows.Count(row => string.Equals(
                        row.Profile.Status,
                        LifecycleKeywordProfileStatuses.Implemented,
                        StringComparison.Ordinal)),
                    keywordRows.Count(row => string.Equals(
                        row.Profile.Status,
                        LifecycleKeywordProfileStatuses.RecognizedDelegated,
                        StringComparison.Ordinal)),
                    keywordRows.Count(row => string.Equals(
                        row.Profile.Status,
                        LifecycleKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal)),
                    unitGroups.Count(group => group.Any(row => string.Equals(
                        row.Profile.Status,
                        LifecycleKeywordProfileStatuses.Implemented,
                        StringComparison.Ordinal))),
                    unitGroups.Count(group => group.Any(row => string.Equals(
                        row.Profile.Status,
                        LifecycleKeywordProfileStatuses.RecognizedDelegated,
                        StringComparison.Ordinal))),
                    unitGroups.Count(group => group.All(row => string.Equals(
                        row.Profile.Status,
                        LifecycleKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal))));
            })
            .ToArray();
    }

    private static bool HasLifecycleKeyword(CardLifecycleKeywordProfile profile, string keyword)
    {
        return keyword switch
        {
            CardLifecycleKeywordNames.Ephemeral => profile.HasEphemeral,
            CardLifecycleKeywordNames.LastBreath => profile.HasLastBreath,
            CardLifecycleKeywordNames.Predict => profile.HasPredict,
            _ => false
        };
    }

    private static void AssertLifecycleKeywordCoverage(
        IReadOnlyList<LifecycleKeywordCoverageRow> rows,
        string keyword,
        int entries,
        int specImplementedEntries,
        int functionalUnits,
        int specImplementedFunctionalUnits,
        int profileImplementedEntries,
        int profileDelegatedEntries,
        int profileDeferredEntries,
        int profileImplementedFunctionalUnits,
        int profileDelegatedFunctionalUnits,
        int profileDeferredFunctionalUnits)
    {
        var row = Assert.Single(rows, candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
        Assert.Equal(entries, row.Entries);
        Assert.Equal(specImplementedEntries, row.SpecImplementedEntries);
        Assert.Equal(functionalUnits, row.FunctionalUnits);
        Assert.Equal(specImplementedFunctionalUnits, row.SpecImplementedFunctionalUnits);
        Assert.Equal(profileImplementedEntries, row.ProfileImplementedEntries);
        Assert.Equal(profileDelegatedEntries, row.ProfileDelegatedEntries);
        Assert.Equal(profileDeferredEntries, row.ProfileDeferredEntries);
        Assert.Equal(profileImplementedFunctionalUnits, row.ProfileImplementedFunctionalUnits);
        Assert.Equal(profileDelegatedFunctionalUnits, row.ProfileDelegatedFunctionalUnits);
        Assert.Equal(profileDeferredFunctionalUnits, row.ProfileDeferredFunctionalUnits);
    }

    private sealed record LifecycleKeywordCoverageRow(
        string Keyword,
        int Entries,
        int SpecImplementedEntries,
        int FunctionalUnits,
        int SpecImplementedFunctionalUnits,
        int ProfileImplementedEntries,
        int ProfileDelegatedEntries,
        int ProfileDeferredEntries,
        int ProfileImplementedFunctionalUnits,
        int ProfileDelegatedFunctionalUnits,
        int ProfileDeferredFunctionalUnits);

    private static IReadOnlyList<TimingSurfaceCoverageRow> BuildTimingSurfaceCoverageRows(
        IReadOnlyList<BehaviorSpec> specs,
        IReadOnlyList<string> surfaces)
    {
        return surfaces
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(surface =>
            {
                var surfaceSpecs = specs
                    .Where(spec => HasTimingSurface(spec, surface))
                    .ToArray();
                var unitGroups = surfaceSpecs
                    .GroupBy(spec => spec.FunctionalUnitId, StringComparer.Ordinal)
                    .ToArray();
                var implementedUnits = unitGroups.Count(group => group.Any(spec => string.Equals(
                    spec.Status,
                    BehaviorImplementationStatuses.Implemented,
                    StringComparison.Ordinal)));

                return new TimingSurfaceCoverageRow(
                    surface,
                    surfaceSpecs.Length,
                    surfaceSpecs.Count(spec => string.Equals(
                        spec.Status,
                        BehaviorImplementationStatuses.Implemented,
                        StringComparison.Ordinal)),
                    surfaceSpecs.Count(spec => string.Equals(
                        spec.Status,
                        BehaviorImplementationStatuses.ManualRuleRequired,
                        StringComparison.Ordinal)),
                    surfaceSpecs.Count(spec => string.Equals(
                        spec.Status,
                        BehaviorImplementationStatuses.Unimplemented,
                        StringComparison.Ordinal)),
                    unitGroups.Length,
                    implementedUnits,
                    unitGroups.Length - implementedUnits);
            })
            .ToArray();
    }

    private static bool HasTimingSurface(BehaviorSpec spec, string surface)
    {
        return surface switch
        {
            TimingSurfaceNames.Trigger => spec.Triggers.Count > 0,
            TimingSurfaceNames.Replacement => spec.Replacements.Count > 0,
            _ => false
        };
    }

    private static void AssertTimingSurfaceCoverage(
        IReadOnlyList<TimingSurfaceCoverageRow> rows,
        string surface,
        int entries,
        int specImplementedEntries,
        int manualRuleRequiredEntries,
        int unimplementedEntries,
        int functionalUnits,
        int specImplementedFunctionalUnits,
        int pendingFunctionalUnits)
    {
        var row = Assert.Single(rows, candidate => string.Equals(candidate.Surface, surface, StringComparison.Ordinal));
        Assert.Equal(entries, row.Entries);
        Assert.Equal(specImplementedEntries, row.SpecImplementedEntries);
        Assert.Equal(manualRuleRequiredEntries, row.ManualRuleRequiredEntries);
        Assert.Equal(unimplementedEntries, row.UnimplementedEntries);
        Assert.Equal(functionalUnits, row.FunctionalUnits);
        Assert.Equal(specImplementedFunctionalUnits, row.SpecImplementedFunctionalUnits);
        Assert.Equal(pendingFunctionalUnits, row.PendingFunctionalUnits);
    }

    private static class TimingSurfaceNames
    {
        public const string Trigger = "trigger";
        public const string Replacement = "replacement";
    }

    private sealed record TimingSurfaceCoverageRow(
        string Surface,
        int Entries,
        int SpecImplementedEntries,
        int ManualRuleRequiredEntries,
        int UnimplementedEntries,
        int FunctionalUnits,
        int SpecImplementedFunctionalUnits,
        int PendingFunctionalUnits);

    private static void AssertRuleDomainSurface(
        IReadOnlyList<BehaviorSpec> specs,
        IReadOnlyList<IGrouping<string, BehaviorSpec>> unitGroups,
        Func<BehaviorSpec, bool> predicate,
        int entries,
        int functionalUnits)
    {
        Assert.Equal(entries, specs.Count(predicate));
        Assert.Equal(functionalUnits, unitGroups.Count(group => group.Any(predicate)));
    }

    private static CardCombatKeywordProfile BuildCombatProfile(
        IReadOnlyList<BehaviorSpec> specs,
        string cardNo,
        params string[] officialTextNeedles)
    {
        var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
        foreach (var needle in officialTextNeedles)
        {
            Assert.Contains(needle, spec.OfficialText, StringComparison.Ordinal);
            Assert.Contains(spec.Keywords, keyword => string.Equals(keyword.Keyword, needle, StringComparison.Ordinal));
        }

        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
        return CardCombatKeywordRules.BuildProfile(definition);
    }

    private static CardResourceKeywordProfile BuildResourceProfile(
        IReadOnlyList<BehaviorSpec> specs,
        string cardNo,
        params string[] officialTextNeedles)
    {
        var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
        foreach (var needle in officialTextNeedles)
        {
            Assert.Contains(needle, spec.OfficialText, StringComparison.Ordinal);
            Assert.Contains(spec.Keywords, keyword => string.Equals(keyword.Keyword, needle, StringComparison.Ordinal));
        }

        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
        return CardResourceKeywordRules.BuildProfile(spec, definition);
    }

    private static CardEquipmentKeywordProfile BuildEquipmentProfile(
        IReadOnlyList<BehaviorSpec> specs,
        string cardNo,
        params string[] officialTextNeedles)
    {
        var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
        foreach (var needle in officialTextNeedles)
        {
            Assert.Contains(needle, spec.OfficialText, StringComparison.Ordinal);
            Assert.Contains(spec.Keywords, keyword => string.Equals(keyword.Keyword, needle, StringComparison.Ordinal));
        }

        if (officialTextNeedles.Any(needle => string.Equals(needle, CardEquipmentKeywordNames.Assemble, StringComparison.Ordinal)
            || string.Equals(needle, CardEquipmentKeywordNames.Tempered, StringComparison.Ordinal)))
        {
            Assert.Contains(BehaviorTemplateIds.Assemble, spec.TemplateIds);
        }

        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
        return CardEquipmentKeywordRules.BuildProfile(spec, definition);
    }

    private static CardLifecycleKeywordProfile BuildLifecycleProfile(
        IReadOnlyList<BehaviorSpec> specs,
        string cardNo,
        params string[] officialTextNeedles)
    {
        var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
        foreach (var needle in officialTextNeedles)
        {
            Assert.Contains(needle, spec.OfficialText, StringComparison.Ordinal);
            Assert.Contains(spec.Keywords, keyword => string.Equals(keyword.Keyword, needle, StringComparison.Ordinal));
        }

        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
        return CardLifecycleKeywordRules.BuildProfile(spec, definition);
    }

    private static CardInteractionKeywordProfile BuildInteractionProfile(
        IReadOnlyList<BehaviorSpec> specs,
        string cardNo,
        params string[] officialTextNeedles)
    {
        var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
        foreach (var needle in officialTextNeedles)
        {
            Assert.Contains(needle, spec.OfficialText, StringComparison.Ordinal);
            Assert.Contains(spec.Keywords, keyword => string.Equals(keyword.Keyword, needle, StringComparison.Ordinal));
        }

        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
        return CardInteractionKeywordRules.BuildProfile(spec, definition);
    }

    private static CardBasicActionProfile BuildBasicActionProfile(
        IReadOnlyList<BehaviorSpec> specs,
        string cardNo,
        string? effectKind = null)
    {
        var spec = specs.Single(spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
        CardBehaviorDefinition definition;
        if (string.IsNullOrWhiteSpace(effectKind))
        {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out definition));
        }
        else
        {
            definition = CardBehaviorRegistry.GetAll()
                .Single(candidate => string.Equals(candidate.EffectKind, effectKind, StringComparison.Ordinal));
        }

        return CardBasicActionRules.BuildProfile(spec, definition);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }
}
