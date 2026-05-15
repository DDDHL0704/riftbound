using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PaymentEngineCoverageAuditTests
{
    private const string RepresentativeCovered = "representative-covered";
    private const string PolicyNonResource = "policy-non-resource";
    private const string RemainingGap = "remaining-gap";
    private const string CatalogBoundRepresentative = "catalog-bound-representative";
    private const string CoveredRepresentative = "covered-representative";
    private const string RemainingOfficialGap = "remaining-official-gap";
    private const string PolicyDeferred = "policy-deferred";

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

    private static readonly TargetColoredActivatedAbilityCoverageEntry[] TargetColoredActivatedAbilityCoverageManifest =
    [
        new(
            P4ActivatedAbilityCatalog.XerathDamageAbilityId,
            "ACTIVATE_ABILITY",
            "Xerath target damage representative",
            "1 generic power plus Spellshield target tax when the target has Spellshield",
            "one enemy unit target; RequiredTargetCount=1",
            "PaymentEngineUnificationTests: ActivateAbilityXerathPaysSpellshieldTaxAndRecyclesRunePaymentResource prompt target choices and payment resource metadata",
            "PaymentEngineUnificationTests: ActivateAbilityXerathPaysSpellshieldTaxAndRecyclesRunePaymentResource command commit",
            "PaymentEngineUnificationTests: COST_PAID / ABILITY_ACTIVATED shared paymentId and spellshieldTaxMana assertions",
            "PaymentEngineUnificationTests: insufficient cost / tax and stale target no-mutation guards",
            "Complete official damage-skill target breadth, dependency target choice, modifier/tax interactions, and all illegal target failures remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official target-bearing activated ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md",
                "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId,
            "ACTIVATE_ABILITY",
            "Renata Glasc typed-blue draw representative",
            "1 mana plus 1 blue typed power with matching blue recycle and typed temporary payment resource parity",
            "no target; RequiredTargetCount=0",
            "RenataActivatedAbilityTests: RenataOpenMainPromptExposesTypedBlueDrawRequirement and typed temporary resource prompt coverage",
            "RenataActivatedAbilityTests: RenataDrawCommandPaysTypedBlueAndCreatesStackWithoutImmediateDraw command commit",
            "RenataActivatedAbilityTests: COST_PAID / ABILITY_ACTIVATED typed-blue payment audit assertions",
            "RenataActivatedAbilityTests: wrong trait, invalid resource, target bypass, and temporary-resource no-mutation guards",
            "Complete official colored activated draw breadth, cross-window typed resource interactions, and all illegal payment failures remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official colored activated ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AJ_PAYMENT_ENGINE_RENATA_TYPED_TEMP_RESOURCE_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId,
            "ACTIVATE_ABILITY",
            "Renata Glasc typed-blue score representative",
            "4 mana plus 4 blue typed power with matching blue recycle and typed temporary payment resource parity",
            "no target; RequiredTargetCount=0",
            "RenataActivatedAbilityTests: RenataOpenMainPromptExposesTypedBlueScoreRequirement and exhausted-source prompt coverage",
            "RenataActivatedAbilityTests: RenataScoreCommandPaysTypedBlueExhaustsAndCreatesStackWithoutImmediateScore command commit",
            "RenataActivatedAbilityTests: COST_PAID / ABILITY_ACTIVATED typed-blue payment audit assertions",
            "RenataActivatedAbilityTests: wrong trait, invalid resource, target bypass, exhausted source, and temporary-resource no-mutation guards",
            "Complete official colored activated score breadth, cross-window typed resource interactions, and all illegal payment failures remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official colored activated ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AJ_PAYMENT_ENGINE_RENATA_TYPED_TEMP_RESOURCE_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
            "ACTIVATE_ABILITY",
            "Azir green swift swap representative",
            "1 green typed power with optional target-scoped armament reattach branch",
            "one controlled unit target; RequiredTargetCount=1",
            "AzirSwiftSwapActivatedAbilityTests: PromptExposesAzirSwiftSwapRequirementWithGreenCostTargetsAndOnceMetadata plus reattach prompt coverage",
            "AzirSwiftSwapActivatedAbilityTests: AzirCommandPaysGreenCreatesStackAndResolutionSwapsPreciseLocations command commit",
            "AzirSwiftSwapActivatedAbilityTests: COST_PAID / ABILITY_ACTIVATED / EQUIPMENT_REATTACHED audit assertions",
            "AzirSwiftSwapActivatedAbilityTests: stale target, once-per-turn, invalid target, and invalid reattach no-mutation guards",
            "Complete official swift timing, position-memory dependency, optional armament breadth, and all stale target interactions remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official target-bearing colored ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId,
            "ACTIVATE_ABILITY",
            "Gatekeeper Maduli purple battlefield move representative",
            "1 purple typed power",
            "one weaker enemy-controlled battlefield target; RequiredTargetCount=1",
            "GatekeeperMaduliActivatedAbilityTests: PromptExposesMaduliRequirementWithPurpleCostLegalBattlefieldTargetAndRecycleChoice prompt coverage",
            "GatekeeperMaduliActivatedAbilityTests: MaduliCommandPaysPurpleCreatesStackAndResolutionMovesToTargetBattlefield command commit",
            "GatekeeperMaduliActivatedAbilityTests: COST_PAID / ABILITY_ACTIVATED / UNIT_MOVED audit assertions",
            "GatekeeperMaduliActivatedAbilityTests: stale power condition, invalid target, wrong source, and wrong trait no-mutation guards",
            "Complete official movement target breadth, static interaction ordering, battlefield-control dependency, and all stale movement failures remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official target-bearing colored ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId,
            "ACTIVATE_ABILITY",
            "Ezreal blue swift move-to-base representative",
            "1 blue typed power",
            "no target; RequiredTargetCount=0",
            "EzrealBlueSwiftMoveToBaseActivatedAbilityTests: PromptExposesEzrealSwiftMoveRequirementWithBlueCostNoTargetsAndRecycleChoice prompt coverage",
            "EzrealBlueSwiftMoveToBaseActivatedAbilityTests: EzrealCommandPaysBlueCreatesStackAndResolutionMovesSourceToBase command commit",
            "EzrealBlueSwiftMoveToBaseActivatedAbilityTests: COST_PAID / ABILITY_ACTIVATED / UNIT_MOVED_TO_BASE audit assertions",
            "EzrealBlueSwiftMoveToBaseActivatedAbilityTests: stale source, invalid target, wrong source, and wrong trait no-mutation guards",
            "Complete official swift timing, attack/defense damage trigger, cannot-combat-damage static, and combat dependency breadth remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official colored swift ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
            "ACTIVATE_ABILITY",
            "Crimson Rose experience ready-unit representative",
            "3 experience plus Spellshield target tax when the target has Spellshield",
            "one unit target; RequiredTargetCount=1",
            "CrimsonRoseActivatedAbilityTests: CrimsonRoseOpenMainPromptExposesExperienceReadyUnitRequirement target and experience prompt coverage",
            "CrimsonRoseActivatedAbilityTests: CrimsonRoseFriendlySpellshieldTargetPaysExperienceNoTaxAndCreatesStack / enemy tax command commits",
            "CrimsonRoseActivatedAbilityTests: COST_PAID / ABILITY_ACTIVATED spellshieldTaxMana audit assertions",
            "CrimsonRoseActivatedAbilityTests: invalid target, insufficient experience, cannot-ready Maduli, and stale stack no-mutation guards",
            "Complete official experience-payment family, ready-prevention interactions, unit-play experience trigger, and all target failures remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official target-bearing experience ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"
            ]),
        new(
            P4ActivatedAbilityCatalog.ShadowStunAbilityId,
            "ACTIVATE_ABILITY",
            "Shadow swift stun representative",
            "1 mana plus 1 generic power plus Spellshield target tax when the target has Spellshield",
            "one enemy attacking unit at this battlefield target; RequiredTargetCount=1",
            "ShadowActivatedAbilityTests: ShadowBattleResponsePromptExposesSwiftStunRequirement prompt tax metadata",
            "ShadowActivatedAbilityTests: ShadowActivationPaysManaPowerExhaustsAndCreatesStackWithoutImmediateStun command commit",
            "ShadowActivatedAbilityTests: COST_PAID / ABILITY_ACTIVATED / UNIT_STUNNED spellshield tax audit assertions",
            "ShadowActivatedAbilityTests: invalid target, wrong timing, stale target, and insufficient tax no-mutation guards",
            "Complete official swift battle-response family, combat target dependency, nested stack timing, and all target failure breadth remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official target-bearing swift ability breadth.",
            [
                "docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"
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

    private static readonly PaymentEngineResidualBlockerCoverageEntry[] ResidualBlockerManifest =
    [
        new(
            "OFFICIAL_PAYMENT_ENGINE_MATRIX",
            RemainingOfficialGap,
            "No generated official matrix enumerates every action window, payment source, cost modifier, optional / extra / alternative cost, target tax, replacement interaction, payment resource action, and no-mutation failure branch.",
            "Current action-window, resource-skill, Spellshield tax, HASTE_READY, and selected target-bearing representatives are green.",
            "Full cross-window official matrix is missing; representative tests cannot prove every payment source, target, modifier, rollback, and failure branch.",
            "Future verifier must keep prompt / command / audit parity plus no-mutation rollback requirements visible for every listed family.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md"
            ]),
        new(
            "RESOURCE_SKILL_A_C_FAMILY",
            CatalogBoundRepresentative,
            "4D-03AL binds the current 19 catalog `IsResourceSkill=true` representatives to prompt, command, ABILITY_ACTIVATED audit, and rollback anchors.",
            "Malzahar, Dragon Soul Sage, SFD / OGN Sigils, resource conversion equipment, and Gold token resource skills are catalog-bound representatives.",
            "Complete official `[A]` / `[C]` resource skill breadth and cross-window resource-skill use are not proven beyond the current executable catalog.",
            "Future official closure must keep invalid timing, invalid target, wrong trait, duplicate source, payment-only misuse, and no-mutation rollback branches explicit.",
            "Catalog-bound representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md"
            ]),
        new(
            "TARGET_BEARING_COLORED_ACTIVATED_ABILITIES",
            CoveredRepresentative,
            "Selected Azir, Maduli, Ezreal, Renata, Crimson Rose, Xerath, and Shadow representatives prove prompt / command / stack / audit parity for implemented colored or target-bearing abilities.",
            "4D-03AM / 03AN / 03AO / 03AS and adjacent focused slices cover narrow target-bearing activated ability representatives.",
            "Official family breadth, dependency target choice, target-count interactions, alternative / extra costs, and all stale / illegal target failures remain open.",
            "Future verifier must keep legal target selection, command-side revalidation, stale no-effect, insufficient cost, and no-mutation rollback branches explicit.",
            "Covered representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md"
            ]),
        new(
            "LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS",
            CoveredRepresentative,
            "LEGEND_ACT, battlefield held score payment, and trigger payment representatives have action-window evidence and current prompt / command anchors.",
            "4D-03B, 03G, 03H, 03AC, 03AD, 03AE, and 03X cover current representative legend, battlefield held, and trigger payment paths.",
            "LEGEND_ACT resource-action breadth, battlefield skills, full trigger payment resource family, replacement ordering, and cross-window resource generation remain open.",
            "Future verifier must keep resource quote, command commit, decline, stale source / target, replacement, and no-mutation rollback branches explicit.",
            "Covered representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md",
                "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_AUDIT.md"
            ]),
        new(
            "KEYWORD_PAYMENT_BRANCHES",
            RemainingOfficialGap,
            "HASTE_READY, Echo, Spellshield, experience, battlefield replacement, cost reduction, cost increase, extra cost, optional cost, and temporary-resource parity have multiple focused representatives.",
            "4D-03C / 03K / 03AK / 03AQ / 03AP and battlefield cost modifier fixtures cover selected keyword payment branches.",
            "Haste, Echo, Spellshield, alternative / extra / optional, cost modifier, replacement, and all-window tax quote-command-audit parity are not full official.",
            "Future verifier must keep keyword prompt quote, command-side revalidation, COST_PAID audit payload, insufficient payment, wrong resource, and no-mutation rollback explicit.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md"
            ]),
        new(
            "MOVE_UNIT_MOVEMENT_PERMISSION_POLICY",
            PolicyDeferred,
            "MOVE_UNIT remains a server-authoritative movement-permission / optional-cost policy window today, not a rune, mana, experience, or temporary-resource payment window.",
            "4D-03AH classifies MOVE_UNIT as policy-non-resource while movement representatives cover ROAM and Baron Nest permissions.",
            "If future official rules add a resource payment to movement, MOVE_UNIT must be reclassified and joined to PaymentEngine quote / command / audit parity.",
            "Future policy change must keep destination legality, optional movement costs, rejected source / destination shapes, and no-mutation rollback explicit.",
            "Policy deferred only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_AUDIT.md"
            ])
    ];

    private static readonly PaymentEngineLegendBattlefieldTriggerResourceActionCoverageEntry[] LegendBattlefieldTriggerResourceActionManifest =
    [
        new(
            "LEGEND_ACT",
            CoveredRepresentative,
            "Legend active and reaction representatives that use LEGEND_ACT rather than deferred ACTIVATE_ABILITY surfaces.",
            "ConformanceFixtureShapeTests: LEGEND_ACT source / target prompt metadata and CardCatalogBaselineTests: implemented legend action surface audit",
            "ConformanceFixtureRunnerTests: P79LegendActLilliaCreatesFaerieWithEphemeralCostReduction and P79LegendActIreliaReadiesPendingTargetedFriendlyUnit command commits",
            "ConformanceFixtureRunnerTests: COST_PAID / LEGEND_ABILITY_ACTIVATED / CARD_DRAWN / UNIT_READIED assertions across representative LEGEND_ACT paths",
            "ConformanceFixtureRunnerTests: P79LegendActEzrealRequiresTwoEnemyTargetsThisTurn and P79LegendActIreliaRequiresPendingFriendlyUnitTarget no-mutation guards",
            "Complete LEGEND_ACT resource-action breadth, recycle / temporary resource quote parity, every timing window, and all target dependency branches remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official legend resource-action breadth.",
            [
                "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md",
                "docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_AUDIT.md"
            ]),
        new(
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            CoveredRepresentative,
            "Battlefield held pay-4-power score representatives with generic, typed, recycle, temporary, mixed, and score-prevention coverage.",
            "ConformanceFixtureRunnerTests: BATTLEFIELD_HELD_SCORE_PAYMENT prompt quote coverage via P79BattlefieldHeldScorePromptQuotesTemporaryPaymentResource and recycle plus temporary prompt cases",
            "ConformanceFixtureRunnerTests: P79BattlefieldHeldPaysPowerToGainScoreWithRecycleAndTemporaryPaymentResources command commit",
            "ConformanceFixtureRunnerTests: COST_PAID / RESOURCE_RECYCLED / TEMPORARY_PAYMENT_RESOURCE_SPENT / BATTLEFIELD_HELD audit assertions",
            "ConformanceFixtureRunnerTests: invalid temporary / recycle resource and already-scored no-mutation guards",
            "Complete battlefield skill breadth, replacement ordering, score-prevention variants, and cross-window resource generation remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official battlefield held payment breadth.",
            [
                "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_AUDIT.md"
            ]),
        new(
            "TRIGGER_PAYMENT",
            CoveredRepresentative,
            "Trigger payment representatives covering pending prompts, pay / decline resolution, typed-yellow SFD Fiora, recycle, temporary, mixed resources, and stale guards.",
            "TriggerPaymentTests: TRIGGER_PAYMENT prompt metadata coverage via BattlefieldConquerGoldOpensTriggerPaymentPrompt and SFD Fiora typed-yellow prompt cases",
            "TriggerPaymentTests: accepted PayCost command commits for Gold, Sunken Temple, Vayne, Icevale, Jax, and SFD Fiora representatives",
            "TriggerPaymentTests: COST_PAID / TRIGGER_PAYMENT_DECLINED / BATTLEFIELD_TRIGGER_RESOLVED / PAYMENT_WINDOW_CLOSED audit assertions",
            "TriggerPaymentTests: invalid choice, insufficient cost, stale source / target, and no-mutation guards",
            "Complete trigger payment resource family, multi-trigger ordering, replacement ordering, and cross-window resource generation remain open.",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official trigger payment breadth.",
            [
                "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"
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
    public void PaymentEngineResidualBlockerManifestListsRequiredFamiliesExactlyOnce()
    {
        var requiredFamilies = new[]
        {
            "OFFICIAL_PAYMENT_ENGINE_MATRIX",
            "RESOURCE_SKILL_A_C_FAMILY",
            "TARGET_BEARING_COLORED_ACTIVATED_ABILITIES",
            "LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS",
            "KEYWORD_PAYMENT_BRANCHES",
            "MOVE_UNIT_MOVEMENT_PERMISSION_POLICY"
        };

        Assert.Equal(requiredFamilies.Order(StringComparer.Ordinal), ResidualBlockerManifest.Select(entry => entry.Family).Order(StringComparer.Ordinal));
        Assert.Empty(ResidualBlockerManifest
            .GroupBy(entry => entry.Family, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineResidualBlockerManifestClassifiesEveryEntryWithEvidenceAndRollbackExpectations()
    {
        var allowedClassifications = new HashSet<string>(StringComparer.Ordinal)
        {
            CatalogBoundRepresentative,
            CoveredRepresentative,
            RemainingOfficialGap,
            PolicyDeferred
        };

        Assert.All(ResidualBlockerManifest, entry =>
        {
            Assert.Contains(entry.Classification, allowedClassifications);
            Assert.False(string.IsNullOrWhiteSpace(entry.CurrentEvidence));
            Assert.False(string.IsNullOrWhiteSpace(entry.ExistingRepresentativeEvidence));
            Assert.False(string.IsNullOrWhiteSpace(entry.MissingOfficialBreadth));
            Assert.Contains("no-mutation", entry.RollbackExpectation, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineResidualBlockerManifestKeepsOfficialGapsExplicit()
    {
        var remainingGapFamilies = ResidualBlockerManifest
            .Where(entry => string.Equals(entry.Classification, RemainingOfficialGap, StringComparison.Ordinal))
            .Select(entry => entry.Family)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "KEYWORD_PAYMENT_BRANCHES",
                "OFFICIAL_PAYMENT_ENGINE_MATRIX"
            ],
            remainingGapFamilies);

        var combinedMissingBreadth = string.Join(" ", ResidualBlockerManifest.Select(entry => entry.MissingOfficialBreadth));
        Assert.Contains("[A]", combinedMissingBreadth, StringComparison.Ordinal);
        Assert.Contains("[C]", combinedMissingBreadth, StringComparison.Ordinal);
        Assert.Contains("target", combinedMissingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Haste", combinedMissingBreadth, StringComparison.Ordinal);
        Assert.Contains("Echo", combinedMissingBreadth, StringComparison.Ordinal);
        Assert.Contains("Spellshield", combinedMissingBreadth, StringComparison.Ordinal);
        Assert.Contains("LEGEND_ACT", combinedMissingBreadth, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineResidualBlockerManifestDoesNotClaimReadyOrFullOfficialClosure()
    {
        var combinedText = string.Join(
            " ",
            ResidualBlockerManifest.SelectMany(entry =>
                new[]
                {
                    entry.Family,
                    entry.Classification,
                    entry.CurrentEvidence,
                    entry.ExistingRepresentativeEvidence,
                    entry.MissingOfficialBreadth,
                    entry.RollbackExpectation,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineLegendBattlefieldTriggerResourceActionManifestListsRequiredWindowsExactlyOnce()
    {
        var requiredWindows = new[]
        {
            "LEGEND_ACT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "TRIGGER_PAYMENT"
        };

        Assert.Equal(
            requiredWindows.Order(StringComparer.Ordinal),
            LegendBattlefieldTriggerResourceActionManifest.Select(entry => entry.ActionWindow).Order(StringComparer.Ordinal));
        Assert.Empty(LegendBattlefieldTriggerResourceActionManifest
            .GroupBy(entry => entry.ActionWindow, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));

        var actionWindowManifest = CoverageManifest.ToDictionary(entry => entry.ActionWindow, StringComparer.Ordinal);
        Assert.All(LegendBattlefieldTriggerResourceActionManifest, entry =>
        {
            Assert.True(actionWindowManifest.TryGetValue(entry.ActionWindow, out var actionWindowEntry), entry.ActionWindow);
            Assert.Equal(RepresentativeCovered, actionWindowEntry.Classification);
        });
    }

    [Fact]
    public void PaymentEngineLegendBattlefieldTriggerResourceActionManifestRequiresPromptCommandAuditAndRollbackAnchors()
    {
        Assert.All(LegendBattlefieldTriggerResourceActionManifest, entry =>
        {
            Assert.Equal(CoveredRepresentative, entry.Classification);
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains(entry.ActionWindow, entry.PromptAnchor, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(entry.CommandAnchor));
            Assert.False(string.IsNullOrWhiteSpace(entry.AuditAnchor));
            Assert.Contains("no-mutation", entry.RollbackAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(entry.RemainingOfficialBreadth));
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
    public void PaymentEngineLegendBattlefieldTriggerResourceActionManifestKeepsResidualBlockerLinked()
    {
        var residualBlocker = Assert.Single(
            ResidualBlockerManifest,
            entry => string.Equals(entry.Family, "LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS", StringComparison.Ordinal));

        Assert.Equal(CoveredRepresentative, residualBlocker.Classification);
        Assert.Contains("LEGEND_ACT", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("battlefield held", residualBlocker.CurrentEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trigger payment", residualBlocker.CurrentEvidence, StringComparison.OrdinalIgnoreCase);

        var coveredWindows = LegendBattlefieldTriggerResourceActionManifest
            .Select(entry => entry.ActionWindow)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "BATTLEFIELD_HELD_SCORE_PAYMENT",
                "LEGEND_ACT",
                "TRIGGER_PAYMENT"
            ],
            coveredWindows);
    }

    [Fact]
    public void PaymentEngineLegendBattlefieldTriggerResourceActionManifestKeepsOfficialBreadthExplicit()
    {
        var combinedRemainingBreadth = string.Join(
            " ",
            LegendBattlefieldTriggerResourceActionManifest.Select(entry => entry.RemainingOfficialBreadth));

        Assert.Contains("resource-action", combinedRemainingBreadth, StringComparison.Ordinal);
        Assert.Contains("battlefield", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trigger", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("replacement ordering", combinedRemainingBreadth, StringComparison.Ordinal);
        Assert.Contains("cross-window resource generation", combinedRemainingBreadth, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineLegendBattlefieldTriggerResourceActionManifestDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            LegendBattlefieldTriggerResourceActionManifest.SelectMany(entry =>
                new[]
                {
                    entry.ActionWindow,
                    entry.Classification,
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.RollbackAnchor,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("READY", combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineTargetColoredActivatedAbilityManifestMatchesCatalogResidualPredicate()
    {
        var catalogAbilityIds = P4ActivatedAbilityCatalog.GetAll()
            .Where(IsTargetColoredOrExperienceActivatedAbility)
            .Select(definition => definition.AbilityId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var manifestAbilityIds = TargetColoredActivatedAbilityCoverageManifest
            .Select(entry => entry.AbilityId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogAbilityIds, manifestAbilityIds);
        Assert.Equal(
            new[]
            {
                P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
                P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
                P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId,
                P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId,
                P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId,
                P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                P4ActivatedAbilityCatalog.XerathDamageAbilityId
            }.Order(StringComparer.Ordinal),
            manifestAbilityIds);
        Assert.Equal(8, manifestAbilityIds.Length);
        Assert.DoesNotContain(P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, manifestAbilityIds);
        Assert.DoesNotContain(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, manifestAbilityIds);
        Assert.DoesNotContain(P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, manifestAbilityIds);
        Assert.Empty(TargetColoredActivatedAbilityCoverageManifest
            .GroupBy(entry => entry.AbilityId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineTargetColoredActivatedAbilityManifestRequiresPromptCommandAuditAndRollbackAnchors()
    {
        Assert.All(TargetColoredActivatedAbilityCoverageManifest, entry =>
        {
            Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(entry.AbilityId, out var definition));

            Assert.Equal("ACTIVATE_ABILITY", entry.PaymentWindow);
            Assert.False(definition.IsResourceSkill);
            Assert.True(IsTargetColoredOrExperienceActivatedAbility(definition));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("Prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                entry.AuditAnchor.Contains("COST_PAID", StringComparison.Ordinal)
                || entry.AuditAnchor.Contains("ABILITY_ACTIVATED", StringComparison.Ordinal));
            Assert.Contains("no-mutation", entry.RollbackAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);

            if (definition.RequiredTargetCount > 0)
            {
                Assert.Contains("target", entry.TargetProfile, StringComparison.OrdinalIgnoreCase);
                Assert.Contains($"RequiredTargetCount={definition.RequiredTargetCount}", entry.TargetProfile, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("no target", entry.TargetProfile, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var typedPowerCost in P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(definition))
            {
                Assert.Contains("typed", entry.PaymentProfile, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(typedPowerCost.Key, entry.PaymentProfile, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(typedPowerCost.Value.ToString(), entry.PaymentProfile, StringComparison.Ordinal);
            }

            if (definition.ExperienceCost > 0)
            {
                Assert.Contains("experience", entry.PaymentProfile, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(definition.ExperienceCost.ToString(), entry.PaymentProfile, StringComparison.Ordinal);
            }

            if (definition.AppliesSpellshieldTargetTax)
            {
                Assert.Contains("Spellshield", entry.PaymentProfile, StringComparison.Ordinal);
            }

            Assert.NotEmpty(entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineTargetColoredActivatedAbilityManifestKeepsOfficialBreadthExplicit()
    {
        var combinedRemainingBreadth = string.Join(" ", TargetColoredActivatedAbilityCoverageManifest.Select(entry => entry.RemainingOfficialBreadth));

        Assert.Contains("target", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typed", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("experience", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("swift", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Spellshield", string.Join(" ", TargetColoredActivatedAbilityCoverageManifest.Select(entry => entry.PaymentProfile)), StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineTargetColoredActivatedAbilityManifestDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            TargetColoredActivatedAbilityCoverageManifest.SelectMany(entry =>
                new[]
                {
                    entry.AbilityId,
                    entry.RepresentativeSurface,
                    entry.PaymentProfile,
                    entry.TargetProfile,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.RollbackAnchor,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("CANNOT_READY", string.Empty, StringComparison.Ordinal)
                .Replace("READY_UNIT", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
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

    private sealed record TargetColoredActivatedAbilityCoverageEntry(
        string AbilityId,
        string PaymentWindow,
        string RepresentativeSurface,
        string PaymentProfile,
        string TargetProfile,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string RollbackAnchor,
        string RemainingOfficialBreadth,
        string ClosureStatus,
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

    private sealed record PaymentEngineResidualBlockerCoverageEntry(
        string Family,
        string Classification,
        string CurrentEvidence,
        string ExistingRepresentativeEvidence,
        string MissingOfficialBreadth,
        string RollbackExpectation,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineLegendBattlefieldTriggerResourceActionCoverageEntry(
        string ActionWindow,
        string Classification,
        string RepresentativeSurface,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string RollbackAnchor,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private static bool IsTargetColoredOrExperienceActivatedAbility(P4ActivatedAbilityDefinition definition)
    {
        return !definition.IsResourceSkill
            && (definition.RequiredTargetCount > 0
                || P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(definition).Count > 0
                || definition.ExperienceCost > 0
                || definition.AppliesSpellshieldTargetTax);
    }
}
