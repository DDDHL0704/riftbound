using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PaymentEngineCoverageAuditTests
{
    private const string RepresentativeCovered = "representative-covered";
    private const string PolicyNonResource = "policy-non-resource";
    private const string RemainingGap = "remaining-gap";

    private static readonly PaymentEngineActionWindowCoverageEntry[] CoverageManifest =
    [
        new(
            "PLAY_CARD",
            RepresentativeCovered,
            "Shared PaymentPlan prompt / command / audit representatives cover mana, generic and typed rune power, recycle resources, temporary resources, optional / extra costs, and the 4D-03AG typed resource prompt parity fix.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "PaymentEngineUnificationTests: PlayCard cost plan, recycle, generic temporary, typed temporary, wrong-trait prompt and command parity",
                "GameHubJoinTests: PlayCard ActionPrompt / GameHub metadata smoke"
            ],
            [
                "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md"
            ]),
        new(
            "PAY_COST",
            RepresentativeCovered,
            "Pending PAY_COST representatives cover shared payment commit, recycle rune resources, temporary payment-only resources, aggregate prompt metadata, spent / cleared temporary ledger events, and rollback guards.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "PaymentEngineUnificationTests: pending PAY_COST recycle / temporary resource quote and commit",
                "TriggerPaymentTests: adjacent pending payment prompt metadata regression"
            ],
            [
                "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"
            ]),
        new(
            "TRIGGER_PAYMENT",
            RepresentativeCovered,
            "Trigger payment representatives cover SFD Fiora typed-yellow recycle / temporary payment resources, pending prompt aggregate parity, command commit, audit events, stale source / target rollback, and decline behavior.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "TriggerPaymentTests: SFD Fiora prompt, recycle, typed temporary, mixed resource, invalid resource, and stale guard coverage",
                "PaymentEngineUnificationTests: pending payment resource regression"
            ],
            [
                "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"
            ]),
        new(
            "ASSEMBLE_EQUIPMENT",
            RepresentativeCovered,
            "Assemble equipment representatives cover shared PaymentPlan cost commit, typed equipment costs, recycle resource actions, temporary payment-only resource inline consumption, prompt metadata, and no-mutation guards.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "ConformanceFixtureRunnerTests: P4/P7 assemble equipment typed, any-power, recycle, and rejection fixtures",
                "PaymentEngineUnificationTests: assemble equipment temporary payment resource parity"
            ],
            [
                "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md",
                "docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md"
            ]),
        new(
            "ACTIVATE_ABILITY",
            RepresentativeCovered,
            "Activated ability representatives cover Vi / Xerath payment resources, Renata typed-blue draw / score, Crimson Rose experience and target tax, Shadow swift stun, resource skills, temporary ledger usage, stack timing, and prompt / command rollback.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "PaymentEngineUnificationTests: ACTIVATE_ABILITY resource payment and temporary payment parity",
                "RenataActivatedAbilityTests / CrimsonRoseActivatedAbilityTests / ShadowActivatedAbilityTests / ResourceSkill tests"
            ],
            [
                "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md",
                "docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md",
                "docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AJ_PAYMENT_ENGINE_RENATA_TYPED_TEMP_RESOURCE_AUDIT.md"
            ]),
        new(
            "LEGEND_ACT",
            RepresentativeCovered,
            "Legend action representatives cover existing LEGEND_ACT action-domain payment and non-payment commands, while 4D-03X retired the old deferred legend ACTIVATE_ABILITY surfaces so the playable path stays LEGEND_ACT.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "ConformanceFixtureRunnerTests: P79LegendAct / LegendAction prompt, command, payment, and blocked ACTIVATE_ABILITY coverage",
                "CardCatalogBaselineTests: P6 legend implemented representative audit"
            ],
            [
                "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md",
                "docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_AUDIT.md"
            ]),
        new(
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            RepresentativeCovered,
            "Battlefield held score payment representatives cover pay-4-power score, typed power, recycle resources, temporary payment-only resources, mixed quote / command consumption, score-prevention rollback, and no-mutation guards.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "ConformanceFixtureRunnerTests: P79BattlefieldHeld score, typed power, recycle, temporary, mixed prompt, and prevention coverage",
                "PaymentEngineUnificationTests: battlefield held payment regression"
            ],
            [
                "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_AUDIT.md"
            ]),
        new(
            "HIDE_CARD",
            RepresentativeCovered,
            "Hide-card standby representatives cover shared payment plans for standard A-cost standby, Teemo mana alternative, free standby, audit payloads, and insufficient-cost rollback.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "PaymentEngineUnificationTests: HIDE_CARD standard, Teemo alternative, free standby, audit and no-mutation coverage",
                "GameHubJoinTests: HideCard ActionPrompt / GameHub metadata smoke"
            ],
            [
                "docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md"
            ]),
        new(
            "MOVE_UNIT",
            PolicyNonResource,
            "MOVE_UNIT is intentionally classified as movement-permission / optional-cost policy today: ROAM and Baron Nest choices are server-authoritative movement permissions, not rune, mana, experience, or temporary-resource payment windows.",
            "Policy coverage only; project remains NOT READY and P0-005 remains open for full-official PaymentEngine breadth.",
            [
                "ConformanceFixtureRunnerTests: P4MoveUnit, P79BattlefieldStaticRoam, BoardTaskQueue, and Baron Nest no-ROAM movement coverage",
                "GameHubJoinTests: MOVE_UNIT ActionPrompt / GameHub metadata smoke"
            ],
            [
                "docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_AUDIT.md"
            ])
    ];

    private static readonly SpellshieldTaxActivatedAbilityCoverageEntry[] SpellshieldTaxCoverageManifest =
    [
        new(
            P4ActivatedAbilityCatalog.XerathDamageAbilityId,
            "ACTIVATE_ABILITY",
            "Xerath target damage representative",
            "enemy unit target",
            "PaymentEngineUnificationTests: ActivateAbilityXerathPaysSpellshieldTaxAndRecyclesRunePaymentResource prompt target choices",
            "PaymentEngineUnificationTests: ActivateAbilityXerathPaysSpellshieldTaxAndRecyclesRunePaymentResource command commit",
            "PaymentEngineUnificationTests: COST_PAID spellshieldTaxMana / spellshieldTaxTargetObjectIds / shared paymentId assertions",
            "PaymentEngineUnificationTests: Xerath recycle / temporary resource insufficient tax mana no-mutation guards",
            [
                "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md",
                "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
            "ACTIVATE_ABILITY",
            "Crimson Rose ready-unit representative",
            "unit target",
            "CrimsonRoseActivatedAbilityTests: CrimsonRoseOpenMainPromptExposesExperienceReadyUnitRequirement target choices",
            "CrimsonRoseActivatedAbilityTests: CrimsonRoseEnemySpellshieldTargetPaysManaTax command commit",
            "CrimsonRoseActivatedAbilityTests: COST_PAID spellshieldTaxMana / spellshieldTaxTargetObjectIds assertions",
            "CrimsonRoseActivatedAbilityTests: insufficient-tax-mana no-mutation guard",
            [
                "docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.ShadowStunAbilityId,
            "ACTIVATE_ABILITY",
            "Shadow swift stun representative",
            "enemy attacking unit at this battlefield",
            "ShadowActivatedAbilityTests: ShadowBattleResponsePromptExposesSwiftStunRequirement target tax metadata",
            "ShadowActivatedAbilityTests: ShadowEnemySpellshieldTargetPaysManaTax command commit",
            "ShadowActivatedAbilityTests: COST_PAID spellshieldTaxMana / spellshieldTaxTargetObjectIds assertions",
            "ShadowActivatedAbilityTests: insufficient-tax-mana no-mutation guard",
            [
                "docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md"
            ])
    ];

    [Fact]
    public void PaymentEngineActionWindowCoverageManifestListsRequiredWindowsExactlyOnce()
    {
        var requiredWindows = new[]
        {
            "PLAY_CARD",
            "PAY_COST",
            "TRIGGER_PAYMENT",
            "ASSEMBLE_EQUIPMENT",
            "ACTIVATE_ABILITY",
            "LEGEND_ACT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "HIDE_CARD",
            "MOVE_UNIT"
        };

        Assert.Equal(requiredWindows.Order(StringComparer.Ordinal), CoverageManifest.Select(entry => entry.ActionWindow).Order(StringComparer.Ordinal));
        Assert.Empty(CoverageManifest
            .GroupBy(entry => entry.ActionWindow, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineActionWindowCoverageManifestClassifiesEveryEntryWithAuditAnchors()
    {
        var allowedClassifications = new HashSet<string>(StringComparer.Ordinal)
        {
            RepresentativeCovered,
            PolicyNonResource,
            RemainingGap
        };

        Assert.All(CoverageManifest, entry =>
        {
            Assert.Contains(entry.Classification, allowedClassifications);
            Assert.False(string.IsNullOrWhiteSpace(entry.EvidenceSummary));
            Assert.False(string.IsNullOrWhiteSpace(entry.ClosureStatus));
            Assert.NotEmpty(entry.TestAnchors);
            Assert.NotEmpty(entry.DocAnchors);
            Assert.All(entry.TestAnchors, anchor => Assert.False(string.IsNullOrWhiteSpace(anchor)));
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineActionWindowCoverageManifestKeepsMoveUnitAsPolicyNonResource()
    {
        var moveUnit = Assert.Single(CoverageManifest, entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));

        Assert.Equal(PolicyNonResource, moveUnit.Classification);
        Assert.Contains("movement-permission", moveUnit.EvidenceSummary, StringComparison.Ordinal);
        Assert.Contains("not rune, mana, experience, or temporary-resource payment windows", moveUnit.EvidenceSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("representative-covered", moveUnit.EvidenceSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineActionWindowCoverageManifestIncludesPlayCardTypedPromptParityEvidence()
    {
        var playCard = Assert.Single(CoverageManifest, entry => string.Equals(entry.ActionWindow, "PLAY_CARD", StringComparison.Ordinal));

        Assert.Equal(RepresentativeCovered, playCard.Classification);
        Assert.Contains("4D-03AG typed resource prompt parity", playCard.EvidenceSummary, StringComparison.Ordinal);
        Assert.Contains(playCard.DocAnchors, anchor => anchor.Contains("03AG", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineSpellshieldTaxCoverageManifestMatchesActivatedAbilityCatalog()
    {
        var catalogTaxAbilityIds = P4ActivatedAbilityCatalog.GetAll()
            .Where(definition => definition.AppliesSpellshieldTargetTax)
            .Select(definition => definition.AbilityId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var manifestAbilityIds = SpellshieldTaxCoverageManifest
            .Select(entry => entry.AbilityId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogTaxAbilityIds, manifestAbilityIds);
        Assert.Equal(
            [
                P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
                P4ActivatedAbilityCatalog.XerathDamageAbilityId,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId
            ],
            manifestAbilityIds);
    }

    [Fact]
    public void PaymentEngineSpellshieldTaxCoverageManifestRequiresPromptCommandAuditAndRollbackAnchors()
    {
        Assert.All(SpellshieldTaxCoverageManifest, entry =>
        {
            Assert.Equal("ACTIVATE_ABILITY", entry.PaymentWindow);
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.False(string.IsNullOrWhiteSpace(entry.TargetScope));
            Assert.Contains("Prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COST_PAID", entry.AuditAnchor, StringComparison.Ordinal);
            Assert.Contains("no-mutation", entry.RollbackAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineSpellshieldTaxCoverageManifestDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            SpellshieldTaxCoverageManifest.SelectMany(entry =>
                new[]
                {
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.RollbackAnchor
                }.Concat(entry.DocAnchors)));

        Assert.DoesNotContain("full official", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineActionWindowCoverageManifestDoesNotClaimReadyOrP0005Closure()
    {
        Assert.All(CoverageManifest, entry =>
        {
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.DoesNotContain("FullOfficialRulePass", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.DoesNotContain("READY", entry.ClosureStatus.Replace("NOT READY", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        });
    }

    private sealed record PaymentEngineActionWindowCoverageEntry(
        string ActionWindow,
        string Classification,
        string EvidenceSummary,
        string ClosureStatus,
        IReadOnlyList<string> TestAnchors,
        IReadOnlyList<string> DocAnchors);

    private sealed record SpellshieldTaxActivatedAbilityCoverageEntry(
        string AbilityId,
        string PaymentWindow,
        string RepresentativeSurface,
        string TargetScope,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string RollbackAnchor,
        IReadOnlyList<string> DocAnchors);
}
