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

    private static readonly ResourceSkillCoverageFamilyEntry[] ResourceSkillCoverageManifest =
    [
        new(
            "Malzahar target-as-cost payment-only resource skill",
            [
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId
            ],
            "MalzaharResourceSkillTests: prompt exposes target-as-cost, generated power, payment-only resource restriction, and spell-duel focus timing",
            "MalzaharResourceSkillTests: command destroys friendly target, exhausts source, creates temporary payment resource, and rejects invalid target / timing / duplicate shapes",
            "MalzaharResourceSkillTests: ABILITY_ACTIVATED, UNIT_DESTROYED, POWER_GAINED, and payment-only ledger metadata assertions",
            "MalzaharResourceSkillTests: stale target, exhausted source, invalid target, and temporary-resource misuse no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md"
            ]),
        new(
            "Dragon Soul Sage reaction mana resource skill",
            [
                P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId
            ],
            "ReactionResourceSkillTests: prompt exposes reaction-speed Dragon Soul Sage source, generated mana, and no-target shape",
            "ReactionResourceSkillTests: command exhausts source, gains mana, and rejects wrong phase / target / exhausted shapes",
            "ReactionResourceSkillTests: ABILITY_ACTIVATED and MANA_GAINED generated-mana audit assertions",
            "ReactionResourceSkillTests: invalid target, exhausted source, and unsupported timing no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_AUDIT.md"
            ]),
        new(
            "SFD Sigil typed payment-only resource skill family",
            [
                P4ActivatedAbilityCatalog.RageSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.FocusSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.InsightSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.PowerSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.DiscordSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.UnitySigilResourceAbilityId
            ],
            "RageSigilResourceSkillTests / SfdSigilResourceSkillTests: prompts expose SFD typed temporary payment-only resource skills with per-trait generated power",
            "RageSigilResourceSkillTests / SfdSigilResourceSkillTests: commands exhaust source, create typed temporary payment resources, and keep SFD family parity",
            "RageSigilResourceSkillTests / SfdSigilResourceSkillTests: ABILITY_ACTIVATED and POWER_GAINED typed resource audit metadata assertions",
            "RageSigilResourceSkillTests / SfdSigilResourceSkillTests: wrong source, exhausted source, duplicate, wrong trait, and payment misuse no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md"
            ]),
        new(
            "OGN Sigil typed payment-only resource skill family",
            [
                P4ActivatedAbilityCatalog.OgnRageSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.OgnFocusSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.OgnInsightSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.OgnPowerSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.OgnDiscordSigilResourceAbilityId,
                P4ActivatedAbilityCatalog.OgnUnitySigilResourceAbilityId
            ],
            "OgnSigilResourceSkillTests: prompts expose OGN typed temporary payment-only resource skills with per-trait generated power",
            "OgnSigilResourceSkillTests: commands exhaust source, create typed temporary payment resources, and keep OGN reprint parity",
            "OgnSigilResourceSkillTests: ABILITY_ACTIVATED and POWER_GAINED typed resource audit metadata assertions",
            "OgnSigilResourceSkillTests: wrong source, exhausted source, duplicate, wrong trait, and payment misuse no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md"
            ]),
        new(
            "Resource conversion equipment skill family",
            [
                P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId,
                P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
                P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId
            ],
            "ResourceConversionEquipmentSkillTests: prompts expose Energy Channel, Ancient Stele, and Hextech Anomaly conversion choices",
            "ResourceConversionEquipmentSkillTests: commands gain mana, convert mana to temporary generic power, and convert generic power to mana",
            "ResourceConversionEquipmentSkillTests: ABILITY_ACTIVATED, MANA_GAINED, POWER_GAINED, resource restriction, and conversion audit assertions",
            "ResourceConversionEquipmentSkillTests: missing / invalid conversion, wrong card, exhausted source, target, and temporary-resource misuse no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md"
            ]),
        new(
            "Gold token payment-only resource skill family",
            [
                P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId,
                P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId
            ],
            "GoldTokenResourceSkillTests: prompts expose UNL / SFD Gold token resource skills and Renata bonus metadata",
            "GoldTokenResourceSkillTests: commands destroy Gold token, create generic payment-only temporary resource, and apply Renata bonus mana when marked",
            "GoldTokenResourceSkillTests: ABILITY_ACTIVATED, POWER_GAINED, MANA_GAINED, token destruction, and bonus audit assertions",
            "GoldTokenResourceSkillTests: wrong owner, wrong zone, exhausted / invalid source, and mana-only payment misuse no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_AUDIT.md"
            ])
    ];

    private static readonly HasteReadyCoverageEntry[] HasteReadyCoverageManifest =
    [
        new("OGN·001/298", RuneTrait.Red, "p4-play-blazing-drake-haste-ready.fixture.json", "Blazing Drake HASTE_READY play representative"),
        new("UNL-006/219", RuneTrait.Red, "p4-play-baby-shark-haste-ready.fixture.json", "Baby Shark HASTE_READY play representative"),
        new("SFD·029/221", RuneTrait.Red, "p4-play-reksai-haste-ready.fixture.json", "Rek'Sai HASTE_READY play representative"),
        new("SFD·029a/221", RuneTrait.Red, "p4-play-reksai-alt-a-haste-ready.fixture.json", "Rek'Sai alt-a HASTE_READY play representative"),
        new("OGN·010/298", RuneTrait.Red, "p4-play-legion-rearguard-haste-ready.fixture.json", "Legion Rearguard HASTE_READY play representative"),
        new("UNL-127/219", RuneTrait.Purple, "p4-play-mr-root-haste-ready.fixture.json", "Mr. Root HASTE_READY play representative"),
        new("SFD·068/221", RuneTrait.Blue, "p4-play-mech-maniac-haste-ready.fixture.json", "Mech Maniac HASTE_READY play representative"),
        new("SFD·103/221", RuneTrait.Orange, "p4-play-xersai-fish-haste-ready.fixture.json", "Xer'sai Fish HASTE_READY play representative"),
        new("SFD·179/221", RuneTrait.Yellow, "p4-play-karina-veraze-haste-ready.fixture.json", "Karina Veraze HASTE_READY play representative"),
        new("UNL-029/219", RuneTrait.Red, "p4-play-crimson-signet-treant-haste-ready.fixture.json", "Crimson Signet Treant HASTE_READY play representative"),
        new("UNL-029a/219", RuneTrait.Red, "p4-play-crimson-signet-treant-alt-a-haste-ready.fixture.json", "Crimson Signet Treant alt-a HASTE_READY play representative"),
        new("UNL-024/219", RuneTrait.Red, "p4-play-rengar-haste-ready.fixture.json", "Rengar HASTE_READY play representative"),
        new("UNL-024a/219", RuneTrait.Red, "p4-play-rengar-alt-a-haste-ready.fixture.json", "Rengar alt-a HASTE_READY play representative"),
        new("UNL-115/219", RuneTrait.Orange, "p4-play-nilah-haste-ready.fixture.json", "Nilah HASTE_READY play representative"),
        new("OGN·162/298", RuneTrait.Orange, "p4-play-miss-fortune-haste-ready.fixture.json", "Miss Fortune HASTE_READY play representative"),
        new("OGN·162a/298", RuneTrait.Orange, "p4-play-miss-fortune-alt-a-haste-ready.fixture.json", "Miss Fortune alt-a HASTE_READY play representative"),
        new("SFD·143/221", RuneTrait.Purple, "p4-play-sivir-haste-ready.fixture.json", "Sivir HASTE_READY play representative"),
        new("SFD·143a/221", RuneTrait.Purple, "p4-play-sivir-alt-a-haste-ready.fixture.json", "Sivir alt-a HASTE_READY play representative"),
        new("UNL-082/219", RuneTrait.Blue, "p4-play-lillia-haste-ready.fixture.json", "Lillia HASTE_READY play representative"),
        new("UNL-082a/219", RuneTrait.Blue, "p4-play-lillia-alt-a-haste-ready.fixture.json", "Lillia alt-a HASTE_READY play representative"),
        new("SFD·177/221", RuneTrait.Yellow, "p4-play-azir-haste-ready.fixture.json", "Azir HASTE_READY play representative"),
        new("SFD·177a/221", RuneTrait.Yellow, "p4-play-azir-alt-a-haste-ready.fixture.json", "Azir alt-a HASTE_READY play representative"),
        new("OGN·039/298", RuneTrait.Red, "p4-play-kaisa-haste-ready.fixture.json", "Kai'Sa HASTE_READY play representative"),
        new("OGN·039a/298", RuneTrait.Red, "p4-play-kaisa-alt-a-haste-ready.fixture.json", "Kai'Sa alt-a HASTE_READY play representative"),
        new("OGN·075/298", RuneTrait.Green, "p4-play-tasty-faerie-haste-ready.fixture.json", "Tasty Faerie HASTE_READY play representative"),
        new("OGN·110/298", RuneTrait.Blue, "p4-play-ekko-haste-ready.fixture.json", "Ekko HASTE_READY play representative"),
        new("SFD·002/221", RuneTrait.Red, "p4-play-armed-assaulter-haste-ready.fixture.json", "Armed Assaulter HASTE_READY play representative"),
        new("SFD·131/221", RuneTrait.Purple, "p4-play-ancient-berserker-haste-ready.fixture.json", "Ancient Berserker HASTE_READY play representative"),
        new("OGN·150/298", RuneTrait.Orange, "p4-play-kraken-hunter-haste-ready.fixture.json", "Kraken Hunter HASTE_READY play representative"),
        new("OGN·151/298", RuneTrait.Orange, "p4-play-lee-sin-haste-ready.fixture.json", "Lee Sin HASTE_READY play representative"),
        new("OGN·151a/298", RuneTrait.Orange, "p4-play-lee-sin-alt-a-haste-ready.fixture.json", "Lee Sin alt-a HASTE_READY play representative"),
        new("OGN·116/298", RuneTrait.Blue, "p4-play-thousand-tailed-watcher-haste-ready.fixture.json", "Thousand-Tailed Watcher HASTE_READY play representative"),
        new("OGN·030/298", RuneTrait.Red, "p4-play-jinx-haste-ready.fixture.json", "Jinx HASTE_READY discard play representative"),
        new("OGN·030a/298", RuneTrait.Red, "p4-play-jinx-alt-a-haste-ready.fixture.json", "Jinx alt-a HASTE_READY discard play representative")
    ];

    private const string HasteReadyCoverageClosureStatus =
        "Representative HASTE_READY play coverage only; project remains NOT READY and P0-005 remains open for remaining official Haste and PaymentEngine breadth.";

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
    public void PaymentEngineResourceSkillCoverageManifestMatchesActivatedAbilityCatalog()
    {
        var catalogResourceSkillAbilityIds = P4ActivatedAbilityCatalog.GetAll()
            .Where(definition => definition.IsResourceSkill)
            .Select(definition => definition.AbilityId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var manifestAbilityIds = ResourceSkillCoverageManifest
            .SelectMany(entry => entry.AbilityIds)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogResourceSkillAbilityIds, manifestAbilityIds);
        Assert.Equal(19, manifestAbilityIds.Length);
        Assert.Empty(ResourceSkillCoverageManifest
            .SelectMany(entry => entry.AbilityIds)
            .GroupBy(abilityId => abilityId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineResourceSkillCoverageManifestRequiresPromptCommandAuditAndRollbackAnchors()
    {
        Assert.All(ResourceSkillCoverageManifest, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Family));
            Assert.NotEmpty(entry.AbilityIds);
            Assert.Contains("Prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ABILITY_ACTIVATED", entry.AuditAnchor, StringComparison.Ordinal);
            Assert.Contains("no-mutation", entry.RollbackAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.NotEmpty(entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineResourceSkillCoverageManifestDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            ResourceSkillCoverageManifest.SelectMany(entry =>
                new[]
                {
                    entry.Family,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.RollbackAnchor,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("READY", combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineHasteReadyCoverageManifestMatchesImplementedRegistryProfiles()
    {
        var registryCardNos = CardBehaviorRegistry.GetAll()
            .Where(definition => CardPermissionKeywordRules.BuildProfile(definition).HasteOptionalReadyBranchStatus == HasteOptionalReadyBranchStatuses.ImplementedRepresentative)
            .Select(definition => definition.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var manifestCardNos = HasteReadyCoverageManifest
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(registryCardNos, manifestCardNos);
        Assert.Equal(34, manifestCardNos.Length);
        Assert.Empty(HasteReadyCoverageManifest
            .GroupBy(entry => entry.CardNo, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineHasteReadyCoverageManifestRequiresOfficialOneManaOnePowerTraitAndFixtureEvidence()
    {
        Assert.All(HasteReadyCoverageManifest, entry =>
        {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(entry.CardNo, out var definition));

            var profile = CardPermissionKeywordRules.BuildProfile(definition);

            Assert.True(profile.HasHaste);
            Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
            Assert.Equal(1, definition.HasteReadyManaCost);
            Assert.Equal(1, definition.HasteReadyPowerCost);
            Assert.False(string.IsNullOrWhiteSpace(entry.ExpectedPowerTrait));
            Assert.Equal(entry.ExpectedPowerTrait, definition.HasteReadyPowerTrait);
            Assert.Equal(1, profile.HasteReadyManaCost);
            Assert.Equal(1, profile.HasteReadyPowerCost);
            Assert.Equal(entry.ExpectedPowerTrait, profile.HasteReadyPowerTrait);
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeAnchor));
            Assert.StartsWith("p4-play-", entry.FixtureFileName, StringComparison.Ordinal);
            Assert.EndsWith("-haste-ready.fixture.json", entry.FixtureFileName, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Fixtures", entry.FixtureFileName)));
        });
    }

    [Fact]
    public void PaymentEngineHasteReadyCoverageManifestKeepsRepresentativeNotReadyClosure()
    {
        var combinedText = string.Join(
            " ",
            HasteReadyCoverageManifest.Select(entry => string.Join(
                " ",
                entry.CardNo,
                entry.ExpectedPowerTrait,
                entry.FixtureFileName,
                entry.RepresentativeAnchor,
                HasteReadyCoverageClosureStatus)));

        Assert.Contains("NOT READY", HasteReadyCoverageClosureStatus, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", HasteReadyCoverageClosureStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("full official", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("full-official", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
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

    private sealed record ResourceSkillCoverageFamilyEntry(
        string Family,
        IReadOnlyList<string> AbilityIds,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string RollbackAnchor,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record HasteReadyCoverageEntry(
        string CardNo,
        string ExpectedPowerTrait,
        string FixtureFileName,
        string RepresentativeAnchor);
}
