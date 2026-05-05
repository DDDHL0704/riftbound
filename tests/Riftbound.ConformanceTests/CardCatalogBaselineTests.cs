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
        Assert.Equal(981, report.StatusCounts[BehaviorImplementationStatuses.Implemented]);
        Assert.Equal(28, report.StatusCounts[BehaviorImplementationStatuses.ManualRuleRequired]);
        Assert.False(report.StatusCounts.ContainsKey(BehaviorImplementationStatuses.Unimplemented));
        Assert.Contains(BehaviorImplementationStatuses.Implemented, report.StatusCounts.Keys);
        Assert.Contains(BehaviorImplementationStatuses.ManualRuleRequired, report.StatusCounts.Keys);
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
    public async Task P6FunctionalUnitCoverageAuditsSameTextVariantsAndReprints()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var coverage = FunctionalUnitBehaviorCoverageReporter.Build(units, specs);

        Assert.Equal(811, coverage.FunctionalUnits);
        Assert.Equal(784, coverage.ImplementedUnits);
        Assert.Equal(27, coverage.ManualRuleRequiredUnits);
        Assert.Equal(0, coverage.UnimplementedUnits);
        Assert.Equal(113, coverage.DuplicateGroups);
        Assert.Equal(112, coverage.ImplementedDuplicateGroups);
        Assert.Equal(309, coverage.ImplementedDuplicateEntries);
        Assert.Equal(1, coverage.PendingDuplicateGroups);
        Assert.Equal(2, coverage.PendingDuplicateEntries);

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
        Assert.Equal(["战场"], pendingDuplicateCategories);
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

        AssertFamily(report, BehaviorTemplateIds.Draw, 131, 129, 2, 0, 114, 112, 2);
        AssertFamily(report, BehaviorTemplateIds.Damage, 148, 145, 3, 0, 129, 126, 3);
        AssertFamily(report, BehaviorTemplateIds.Destroy, 127, 125, 2, 0, 118, 116, 2);
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

        AssertFamily(report, BehaviorTemplateIds.Recall, 49, 45, 4, 0, 43, 39, 4);
        AssertFamily(report, BehaviorTemplateIds.Move, 136, 132, 4, 0, 111, 107, 4);
        AssertFamily(report, BehaviorTemplateIds.Recycle, 63, 61, 2, 0, 51, 49, 2);
        AssertFamily(report, BehaviorTemplateIds.Banish, 11, 11, 0, 0, 9, 9, 0);
        AssertFamily(report, BehaviorTemplateIds.TempMight, 292, 289, 3, 0, 230, 227, 3);
        AssertFamily(report, BehaviorTemplateIds.Boon, 66, 65, 1, 0, 48, 47, 1);
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
            specImplementedEntries: 51,
            functionalUnits: 43,
            specImplementedFunctionalUnits: 42,
            profileImplementedEntries: 0,
            profileDeferredEntries: 53,
            profileImplementedFunctionalUnits: 0,
            profileDeferredFunctionalUnits: 43);
        AssertInteractionKeywordCoverage(
            rows,
            CardInteractionKeywordNames.Echo,
            entries: 24,
            specImplementedEntries: 22,
            functionalUnits: 24,
            specImplementedFunctionalUnits: 22,
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
                CardEquipmentKeywordNames.Tempered
            ]);

        AssertEquipmentKeywordCoverage(
            rows,
            CardEquipmentKeywordNames.Assemble,
            entries: 32,
            specImplementedEntries: 32,
            functionalUnits: 31,
            specImplementedFunctionalUnits: 31,
            profileDeferredEntries: 32,
            profileDeferredFunctionalUnits: 31);
        AssertEquipmentKeywordCoverage(
            rows,
            CardEquipmentKeywordNames.Agile,
            entries: 4,
            specImplementedEntries: 4,
            functionalUnits: 4,
            specImplementedFunctionalUnits: 4,
            profileDeferredEntries: 4,
            profileDeferredFunctionalUnits: 4);
        AssertEquipmentKeywordCoverage(
            rows,
            CardEquipmentKeywordNames.Tempered,
            entries: 16,
            specImplementedEntries: 16,
            functionalUnits: 11,
            specImplementedFunctionalUnits: 11,
            profileDeferredEntries: 16,
            profileDeferredFunctionalUnits: 11);

        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·011/221", out var takeUpDefinition));
        var attachmentProfile = CardEquipmentKeywordRules.BuildAttachmentProfile(takeUpDefinition);
        Assert.True(attachmentProfile.CanAttachOrDetachWeapon);
        Assert.Equal(EquipmentAttachmentProfileStatuses.ImplementedRepresentative, attachmentProfile.Status);
        Assert.Equal(1, attachmentProfile.DrawCount);

        Assert.All(rows, row =>
        {
            Assert.Equal(row.Entries, row.ProfileDeferredEntries);
            Assert.Equal(row.FunctionalUnits, row.ProfileDeferredFunctionalUnits);
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
            implementedEntries: 50,
            manualRuleRequiredEntries: 1,
            unimplementedEntries: 0,
            functionalUnits: 47,
            implementedFunctionalUnits: 46,
            pendingFunctionalUnits: 1);
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
            specImplementedEntries: 514,
            manualRuleRequiredEntries: 16,
            unimplementedEntries: 0,
            functionalUnits: 423,
            specImplementedFunctionalUnits: 407,
            pendingFunctionalUnits: 16);
        AssertTimingSurfaceCoverage(
            timingRows,
            TimingSurfaceNames.Replacement,
            entries: 28,
            specImplementedEntries: 27,
            manualRuleRequiredEntries: 1,
            unimplementedEntries: 0,
            functionalUnits: 24,
            specImplementedFunctionalUnits: 23,
            pendingFunctionalUnits: 1);
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
            28,
            battlefieldSpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal)));
        Assert.Equal(
            27,
            unitGroups.Count(group => group.All(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.ManualRuleRequired,
                StringComparison.Ordinal))));
        Assert.Equal(
            29,
            battlefieldSpecs.Count(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal)));
        Assert.Equal(
            27,
            unitGroups.Count(group => group.Any(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal))));
        Assert.Equal(57, battlefieldSpecs.Count(spec => !string.IsNullOrWhiteSpace(spec.OfficialText)));
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.ActivatedAbilities.Count > 0, entries: 3, functionalUnits: 3);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.Triggers.Count > 0, entries: 40, functionalUnits: 39);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.Replacements.Count > 0, entries: 1, functionalUnits: 1);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.StaticAbilities.Count > 0, entries: 11, functionalUnits: 10);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.Keywords.Count > 0, entries: 11, functionalUnits: 10);
        AssertRuleDomainSurface(battlefieldSpecs, unitGroups, spec => spec.TemplateIds.Count > 0, entries: 34, functionalUnits: 34);

        var implementedBattlefieldSpecs = battlefieldSpecs
            .Where(spec => string.Equals(
                spec.Status,
                BehaviorImplementationStatuses.Implemented,
                StringComparison.Ordinal))
            .OrderBy(spec => spec.CardNo, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["OGN·275/298", "OGN·276/298", "OGN·276a/298", "OGN·279/298", "OGN·280/298", "OGN·282/298", "OGN·283/298", "OGN·284/298", "OGN·285/298", "OGN·287/298", "OGN·288/298", "OGN·290/298", "OGN·293/298", "OGN·293a/298", "OGN·294/298", "OGN·298/298", "SFD·210/221", "SFD·212/221", "SFD·214/221", "SFD·215/221", "SFD·217/221", "SFD·218/221", "SFD·219/221", "SFD·220/221", "SFD·221/221", "UNL-207/219", "UNL-208/219", "UNL-210/219", "UNL-217/219"], implementedBattlefieldSpecs.Select(spec => spec.CardNo).ToArray());
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
        Assert.Equal(784, coverage.ImplementedUnits);
        Assert.Equal(27, coverage.ManualRuleRequiredUnits);
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
        Assert.Equal(["战场"], manualCategories);
        Assert.Equal(28, manualSpecs.Length);
        Assert.Equal(
            27,
            manualSpecs.Select(spec => spec.FunctionalUnitId).Distinct(StringComparer.Ordinal).Count());
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
        Assert.Equal(981, implementedSpecs.Length);
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

        Assert.Equal(5, P6LegendAbilityCatalog.GetDeferredSurfaces().Count);
        Assert.Equal(5, P6BattlefieldEffectCatalog.GetDeferredSurfaces().Count);
        Assert.Equal(5, P6TokenFactoryCatalog.GetDeferredRuleSurfaces().Count);
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
        Assert.Equal(EquipmentKeywordProfileStatuses.RecognizedDeferred, doransShield.Status);

        var longSword = BuildEquipmentProfile(
            specs,
            "SFD·022/221",
            CardEquipmentKeywordNames.Agile,
            CardEquipmentKeywordNames.Assemble);
        Assert.True(longSword.HasAssemble);
        Assert.True(longSword.HasAgile);
        Assert.True(longSword.HasWeapon);
        Assert.False(longSword.HasTempered);

        var sentinelAdept = BuildEquipmentProfile(specs, "SFD·008/221", CardEquipmentKeywordNames.Tempered);
        Assert.False(sentinelAdept.HasAssemble);
        Assert.False(sentinelAdept.HasAgile);
        Assert.True(sentinelAdept.HasTempered);
        Assert.Contains("deferred", sentinelAdept.Reason, StringComparison.OrdinalIgnoreCase);

        var ornn = BuildEquipmentProfile(specs, "SFD·085/221", CardEquipmentKeywordNames.Tempered);
        Assert.True(ornn.HasTempered);
        Assert.False(ornn.HasWeapon);
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
        Cover("equipment:灵便", agile.HasAgile);
        var tempered = BuildEquipmentProfile(specs, "SFD·008/221", CardEquipmentKeywordNames.Tempered);
        Cover("equipment:百炼", tempered.HasTempered);

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
                        row.Profile.Status,
                        EquipmentKeywordProfileStatuses.RecognizedDeferred,
                        StringComparison.Ordinal)),
                    unitGroups.Count(group => group.All(row => string.Equals(
                        row.Profile.Status,
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
            _ => false
        };
    }

    private static void AssertEquipmentKeywordCoverage(
        IReadOnlyList<EquipmentKeywordCoverageRow> rows,
        string keyword,
        int entries,
        int specImplementedEntries,
        int functionalUnits,
        int specImplementedFunctionalUnits,
        int profileDeferredEntries,
        int profileDeferredFunctionalUnits)
    {
        var row = Assert.Single(rows, candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
        Assert.Equal(entries, row.Entries);
        Assert.Equal(specImplementedEntries, row.SpecImplementedEntries);
        Assert.Equal(functionalUnits, row.FunctionalUnits);
        Assert.Equal(specImplementedFunctionalUnits, row.SpecImplementedFunctionalUnits);
        Assert.Equal(profileDeferredEntries, row.ProfileDeferredEntries);
        Assert.Equal(profileDeferredFunctionalUnits, row.ProfileDeferredFunctionalUnits);
    }

    private sealed record EquipmentKeywordCoverageRow(
        string Keyword,
        int Entries,
        int SpecImplementedEntries,
        int FunctionalUnits,
        int SpecImplementedFunctionalUnits,
        int ProfileDeferredEntries,
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
}
