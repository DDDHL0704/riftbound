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
    public async Task P4CombatKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

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
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

        var gluttonousToadfrog = BuildResourceProfile(specs, "UNL-100/219", CardResourceKeywordNames.Hunt);
        Assert.True(gluttonousToadfrog.HasHunt);
        Assert.Equal(3, gluttonousToadfrog.HuntAmount);
        Assert.Equal(ResourceKeywordProfileStatuses.RecognizedDeferred, gluttonousToadfrog.Status);

        var mossStepper = BuildResourceProfile(specs, "UNL-047/219", CardResourceKeywordNames.Hunt, CardResourceKeywordNames.Level);
        Assert.True(mossStepper.HasHunt);
        Assert.Equal(2, mossStepper.HuntAmount);
        Assert.True(mossStepper.HasLevel);
        Assert.Equal([3], mossStepper.LevelThresholds);

        var noxianRecruit = BuildResourceProfile(specs, "OGN·012/298", CardResourceKeywordNames.Encourage);
        Assert.True(noxianRecruit.HasEncourage);
        Assert.Contains("deferred", noxianRecruit.Reason, StringComparison.OrdinalIgnoreCase);

        var pluckyPoro = BuildResourceProfile(specs, "OGN·013/298", CardResourceKeywordNames.Spellshield);
        Assert.True(pluckyPoro.HasSpellshield);
        Assert.Equal(1, pluckyPoro.SpellshieldTax);

        var ornn = BuildResourceProfile(specs, "SFD·085/221", CardResourceKeywordNames.Spellshield);
        Assert.True(ornn.HasSpellshield);
        Assert.Equal(2, ornn.SpellshieldTax);
    }

    [Fact]
    public async Task P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

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
    public async Task P4LifecycleKeywordProfilesMapOfficialTextToRegistryTags()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

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
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

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
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors());

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

        var shepherdsHeirloom = BuildBasicActionProfile(specs, "UNL-158/219");
        Assert.True(shepherdsHeirloom.HasExperience);
        Assert.Contains(CardBasicActionNames.Experience, shepherdsHeirloom.DelegatedP2Actions);
        Assert.Equal(CardBasicActionProfileStatuses.RecognizedCovered, shepherdsHeirloom.Status);

        var poppy = BuildBasicActionProfile(specs, "UNL-178/219");
        Assert.True(poppy.HasExperience);
        Assert.Contains(CardBasicActionNames.Experience, poppy.DelegatedP2Actions);
        Assert.DoesNotContain(CardBasicActionNames.Experience, poppy.DeferredActions);
        Assert.Equal(CardBasicActionProfileStatuses.MixedDeferred, poppy.Status);

        var sternSergeant = BuildBasicActionProfile(specs, "UNL-157/219");
        Assert.True(sternSergeant.HasExperience);
        Assert.DoesNotContain(CardBasicActionNames.Experience, sternSergeant.DelegatedP2Actions);
        Assert.Contains(CardBasicActionNames.Experience, sternSergeant.DeferredActions);
        Assert.Equal(CardBasicActionProfileStatuses.MixedDeferred, sternSergeant.Status);

        var safetyInspector = BuildBasicActionProfile(specs, "UNL-164/219");
        Assert.True(safetyInspector.HasExperience);
        Assert.DoesNotContain(CardBasicActionNames.Experience, safetyInspector.DelegatedP2Actions);
        Assert.Contains(CardBasicActionNames.Experience, safetyInspector.DeferredActions);
        Assert.Equal(CardBasicActionProfileStatuses.MixedDeferred, safetyInspector.Status);
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
