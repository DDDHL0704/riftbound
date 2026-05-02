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
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());
        var report = BehaviorSpecCatalogBuilder.BuildReport(specs);

        Assert.Equal(1009, report.OfficialEntries);
        Assert.Equal(1009, report.BehaviorSpecs);
        Assert.Empty(report.MissingReasonCardNos);
        Assert.Contains(BehaviorImplementationStatuses.Implemented, report.StatusCounts.Keys);
        Assert.Contains(BehaviorImplementationStatuses.ManualRuleRequired, report.StatusCounts.Keys);
        Assert.Contains(BehaviorImplementationStatuses.Unimplemented, report.StatusCounts.Keys);
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
        Assert.Equal(BehaviorImplementationStatuses.ManualRuleRequired, runeSpec.Status);

        var tokenSpec = Assert.Single(specs, spec => string.Equals(spec.CardNo, "UNL·T02", StringComparison.Ordinal));
        Assert.Equal(BehaviorImplementationStatuses.Unimplemented, tokenSpec.Status);
        Assert.Contains("token", tokenSpec.Reason, StringComparison.OrdinalIgnoreCase);
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
            BehaviorTemplateIds.Stun,
            BehaviorTemplateIds.TempMight,
            BehaviorTemplateIds.GainExperience,
            BehaviorTemplateIds.Assemble,
            BehaviorTemplateIds.Echo,
            BehaviorTemplateIds.Ambush
        };
        var registered = BehaviorTemplateRegistry.GetAll().Select(template => template.TemplateId).ToArray();
        Assert.All(requiredTemplates, templateId => Assert.Contains(templateId, registered));

        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());
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
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());
        var bridge = new BehaviorTemplateDelegationBridge();
        var candidates = new[]
        {
            new { CardNo = "SFD·087/221", TemplateId = BehaviorTemplateIds.Draw, EffectKind = "PROPHETS_OMEN_DRAW_3" },
            new { CardNo = "OGS·003/024", TemplateId = BehaviorTemplateIds.Damage, EffectKind = "INCINERATE_DAMAGE_2" },
            new { CardNo = "OGN·229/298", TemplateId = BehaviorTemplateIds.Destroy, EffectKind = "VENGEANCE_DESTROY_UNIT" },
            new { CardNo = "OGN·050/298", TemplateId = BehaviorTemplateIds.Stun, EffectKind = "RUNE_PRISON_STUN_UNIT" },
            new { CardNo = "OGN·004/298", TemplateId = BehaviorTemplateIds.TempMight, EffectKind = "CLEAVE_OVERWHELM_3" }
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
                case BehaviorTemplateIds.Stun:
                    Assert.Equal("STUNNED", delegation.DelegatedBehavior.StatusEffectId);
                    break;
                case BehaviorTemplateIds.TempMight:
                    Assert.Equal(3, delegation.DelegatedBehavior.PowerModifierAmount);
                    Assert.Equal(
                        CardPowerModifierConditionKinds.TargetIsAttacking,
                        delegation.DelegatedBehavior.PowerModifierConditionKind);
                    break;
            }
        }
    }

    [Fact]
    public async Task P4BridgeDoesNotDelegateUnimplementedTemplateRoutes()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());
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
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());
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
            new { CardNo = "OGN·043/298", TemplateId = BehaviorTemplateIds.Move }
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
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

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
        Assert.Equal(HasteOptionalReadyBranchStatuses.RecognizedDeferred, hasteProfile.HasteOptionalReadyBranchStatus);
        Assert.Contains("deferred", hasteProfile.HasteOptionalReadyBranchReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P4EchoKeywordProfileMapsOfficialTextToRegistryOptionalCost()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

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

    private static IReadOnlyList<ImplementedCardBehavior> ImplementedBehaviors()
    {
        return CardBehaviorRegistry.GetAll()
            .Select(definition => new ImplementedCardBehavior(
                definition.CardNo,
                definition.EffectKind,
                definition.DisplayName))
            .ToArray();
    }

    private static OfficialCard Card(OfficialCardCatalog catalog, string cardNo)
    {
        return catalog.Cards.Single(card => string.Equals(card.CardNo, cardNo, StringComparison.Ordinal));
    }
}
