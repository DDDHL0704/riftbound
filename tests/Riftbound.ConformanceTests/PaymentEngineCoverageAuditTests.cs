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

    private static readonly PaymentEngineOfficialMatrixResidualCoverageEntry[] OfficialPaymentEngineMatrixResidualManifest =
    [
        new(
            "ACTION_WINDOWS",
            RemainingOfficialGap,
            "CoverageManifest has representative PLAY_CARD, PAY_COST, TRIGGER_PAYMENT, ASSEMBLE_EQUIPMENT, ACTIVATE_ABILITY, LEGEND_ACT, BATTLEFIELD_HELD_SCORE_PAYMENT, HIDE_CARD, and policy MOVE_UNIT evidence.",
            "Generated official matrix must enumerate every playable payment action window plus future official window additions before P0-005 closure.",
            "Prompt quote, command-side revalidation, and COST_PAID or domain audit parity are required for every action window.",
            "Unsupported window, stale pending payment, wrong action source, and insufficient payment must roll back with no-mutation semantics.",
            "Full official action-window breadth, window-specific payment source reuse, and all window-specific failure branches remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md"
            ]),
        new(
            "PAYMENT_SOURCES",
            RemainingOfficialGap,
            "Current representatives cover mana, generic power, typed power, experience, recycle resources, generated payment-only resources, and temporary payment resources in selected windows.",
            "Generated official matrix must enumerate every legal payment source against every legal window and resource lifetime.",
            "Prompt quote, command-side revalidation, and COST_PAID audit parity must identify the source type, resource id, trait, amount, and payment id.",
            "Wrong trait, wrong resource id, exhausted generated resource, expired temporary resource, and insufficient source totals must roll back with no-mutation semantics.",
            "Full official payment source breadth, source mixing, generated-source lifetime, and source-specific failure branches remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md"
            ]),
        new(
            "RESOURCE_SKILLS",
            RemainingOfficialGap,
            "4D-03AZ keeps current resource skill representatives catalog-bound, including target-as-cost, reaction mana, typed Sigils, conversion equipment, and Gold token branches.",
            "Generated official matrix must prove every official [A] and [C] resource skill family, generated resource type, timing permission, and payment-only consumption branch.",
            "Prompt quote, command-side revalidation, and ABILITY_ACTIVATED plus generated-resource audit parity must remain visible for each resource skill family.",
            "Invalid timing, invalid target, duplicate source, wrong trait, and payment-only misuse must roll back with no-mutation semantics.",
            "Full official resource skill breadth, cross-window generated resource consumption, and all invalid generated-resource branches remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT.md"
            ]),
        new(
            "TARGET_TAXES",
            RemainingOfficialGap,
            "Spellshield and selected target-bearing activated ability representatives cover current target tax prompts, command commits, audit payloads, and stale target guards.",
            "Generated official matrix must enumerate every target tax, dependency target, target count, target controller, and stale legality branch.",
            "Prompt quote, command-side revalidation, and COST_PAID audit parity must bind target ids, tax amount, and target-tax metadata.",
            "Invalid target, stale target, insufficient target tax, wrong controller, and target-count mismatch must roll back with no-mutation semantics.",
            "Full official target tax breadth, target dependency interactions, and all stale target failure branches remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"
            ]),
        new(
            "KEYWORD_BRANCHES",
            RemainingOfficialGap,
            "4D-03AY keeps Haste, Echo, Spellshield, experience, battlefield replacement, cost modifier, optional / extra / alternative, and temporary-resource branches explicit as representatives.",
            "Generated official matrix must enumerate every keyword payment branch across every legal window, including declined optional choices and replacement ordering.",
            "Prompt quote, command-side revalidation, and COST_PAID audit parity must bind keyword source, chosen branch, paid resource totals, and payment id.",
            "Declined branch, stale keyword source, insufficient branch cost, wrong resource, and replacement-denied payment must roll back with no-mutation semantics.",
            "Full official keyword branch breadth, all-window keyword parity, and branch-specific failure coverage remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md"
            ]),
        new(
            "COST_MODIFIERS",
            RemainingOfficialGap,
            "Current representatives include selected reductions, increases, minimum-cost behavior, battlefield/equipment modifiers, and modified COST_PAID assertions.",
            "Generated official matrix must enumerate every official cost reduction, cost increase, minimum rule, modifier stacking order, and modified quote branch.",
            "Prompt quote, command-side revalidation, and COST_PAID audit parity must expose unmodified cost, modified cost, paid amount, and modifier source.",
            "Stale modifier, insufficient modified cost, illegal zeroing, wrong resource, and stacking-order mismatch must roll back with no-mutation semantics.",
            "Full official cost modifier breadth, modifier layering, minimum interactions, and all modified-cost failure branches remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md"
            ]),
        new(
            "OPTIONAL_EXTRA_ALTERNATIVE_COSTS",
            RemainingOfficialGap,
            "Current representatives cover selected play optional costs, hide-card alternative payment, equipment branches, and Azir target-scoped optional reattach payment.",
            "Generated official matrix must enumerate every optional, extra, alternative, target-scoped, accept, decline, and mixed-branch payment path.",
            "Prompt quote, command-side revalidation, and COST_PAID audit parity must bind the chosen option, accepted or declined state, branch target, and paid resource totals.",
            "Invalid option, stale option target, insufficient alternative cost, wrong branch resource, and declined payment side effects must roll back with no-mutation semantics.",
            "Full official optional, extra, alternative, target-scoped branch, and all-window payment parity remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md"
            ]),
        new(
            "REPLACEMENT_PREVENTION",
            RemainingOfficialGap,
            "Current representatives include battlefield held score prevention, battlefield replacement-adjacent branches, and selected no-effect command paths.",
            "Generated official matrix must enumerate every payment-adjacent replacement, prevention, declined replacement, and replacement ordering branch.",
            "Prompt quote, command-side revalidation, and audit parity must bind replacement source, prevented effect, paid cost, and resulting no-effect state.",
            "Prevented score, stale replacement source, illegal replacement order, declined replacement, and insufficient replacement cost must roll back with no-mutation semantics.",
            "Full official replacement and prevention breadth, ordering interactions, and all payment-adjacent no-effect branches remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md"
            ]),
        new(
            "RESOURCE_ACTIONS",
            RemainingOfficialGap,
            "4D-03AX keeps LEGEND_ACT, battlefield held score payment, and TRIGGER_PAYMENT resource-action representatives tied to prompt, command, audit, and rollback anchors.",
            "Generated official matrix must enumerate every official legend, battlefield, trigger, and resource-action payment branch plus future resource-action windows.",
            "Prompt quote, command-side revalidation, and COST_PAID or domain audit parity must identify source, target, pending payment, branch, and payment id.",
            "Stale source, stale target, invalid pending payment, declined branch, wrong resource, and replacement-denied action must roll back with no-mutation semantics.",
            "Full official resource action breadth, battlefield and trigger resource families, and cross-window resource generation remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md"
            ]),
        new(
            "ROLLBACK_FAILURE_BRANCHES",
            RemainingOfficialGap,
            "Current representative tests include many insufficient-cost, wrong-resource, stale-source, stale-target, unsupported-timing, and invalid-pending no-mutation guards.",
            "Generated official matrix must enumerate every illegal payment command, stale prompt, stale command, invalid resource, and no-effect failure branch.",
            "Prompt quote, command-side revalidation, and audit parity must prove rejected branches emit no committed COST_PAID or domain success audit.",
            "Every rejected branch must preserve hand, board, resource ledgers, pending queues, stack, score, and audit state with no-mutation semantics.",
            "Full official rollback breadth, mutation boundary proof, and all failure branch combinations remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md"
            ]),
        new(
            "CROSS_WINDOW_GENERATION_CONSUMPTION",
            RemainingOfficialGap,
            "Current representatives cover selected generated payment-only resources, temporary resource spending, resource skill generation, pending payment reuse, and ledger clearing.",
            "Generated official matrix must enumerate every legal generation window, consumption window, expiry point, cross-window reuse, and generated-resource restriction.",
            "Prompt quote, command-side revalidation, and generated-resource audit parity must bind creation event, payment-only restriction, spend event, and cleanup event.",
            "Expired generated resource, wrong consumption window, duplicate spend, stale pending payment, and resource restriction bypass must roll back with no-mutation semantics.",
            "Full official cross-window generation and consumption breadth, resource lifetime ordering, and all invalid reuse branches remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md"
            ]),
        new(
            "CARD_MATRIX_ALIGNMENT",
            RemainingOfficialGap,
            "Current representative manifests are useful audit evidence, but they do not prove every official card payment branch or card-matrix row.",
            "Generated official matrix must align every official card row, implemented card behavior, payment branch, prompt shape, command path, audit event, and rollback guard.",
            "Prompt quote, command-side revalidation, and audit parity must remain traceable from each card-matrix row to executable tests and docs.",
            "Unmapped card row, untested branch, stale card behavior metadata, missing rollback, and mismatched audit expectation must remain no-mutation blockers.",
            "Full official card matrix alignment, all card payment branches, and final P0-005 closure evidence remain open.",
            "Remaining official gap only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_A_MASTER_CHECKPOINT.md",
                "docs/CURRENT_COMPLETION_AUDIT.md"
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

    private static readonly PaymentEngineKeywordPaymentBranchCoverageEntry[] KeywordPaymentBranchManifest =
    [
        new(
            "HASTE_READY",
            CoveredRepresentative,
            "Haste optional ready representatives cover current registry/profile bound HASTE_READY play fixtures plus Rek'Sai red exactness.",
            "HASTE_READY prompt quote coverage via fixture runner play prompts and RekSaiHasteReadyRedPaymentTests",
            "HASTE_READY command-side payment revalidation via p4 play Haste fixtures and RekSai command commits",
            "COST_PAID audit payload assertions for Haste mana plus typed power representatives",
            "Haste wrong trait, insufficient cost, exhausted source, and stale play no-mutation rollback guards",
            "Complete Haste official breadth, non-hand granting, strong/overflow interactions, and all-window keyword parity remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md",
                "docs/CURRENT_STAGE4D_04G_TEMPERED_HASTE_EQUIPMENT_AUDIT.md"
            ]),
        new(
            "ECHO_OPTIONAL_PAYMENT",
            CoveredRepresentative,
            "Echo and optional payment representatives cover selected spell / play branches with prompt, commit, and audit parity.",
            "Echo optional payment prompt quote coverage via 4D-03C play optional branch representatives",
            "Echo optional payment command-side commit coverage via current optional-cost fixture representatives",
            "COST_PAID audit payload assertions for optional Echo payment representatives",
            "Echo declined payment, insufficient optional cost, wrong resource, and stale source no-mutation rollback guards",
            "Complete Echo timing, repeated spell windows, replacement ordering, and all-window optional payment parity remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md"
            ]),
        new(
            "SPELLSHIELD_TARGET_TAX",
            CoveredRepresentative,
            "Spellshield target tax representatives cover Xerath, Crimson Rose, and Shadow target tax payment branches.",
            "Spellshield target tax prompt metadata coverage via activated ability target choices and tax quotes",
            "Spellshield target tax command-side revalidation via Xerath, Crimson Rose, and Shadow command commits",
            "COST_PAID audit payload assertions for spellshieldTaxMana and spellshieldTaxTargetObjectIds",
            "Spellshield insufficient tax, invalid target, stale target, and wrong resource no-mutation rollback guards",
            "Complete Spellshield official tax breadth, cross-window target taxes, dependency targets, and all-window audit parity remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md",
                "docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md"
            ]),
        new(
            "EXPERIENCE_PAYMENT",
            CoveredRepresentative,
            "Experience payment representatives cover Crimson Rose ready-unit activation and adjacent target tax interactions.",
            "Experience payment prompt quote coverage via CrimsonRoseOpenMainPromptExposesExperienceReadyUnitRequirement",
            "Experience payment command-side revalidation via Crimson Rose friendly and enemy target command commits",
            "COST_PAID audit payload assertions for experience spend plus adjacent Spellshield tax metadata",
            "Experience insufficient cost, cannot-ready target, invalid target, and stale stack no-mutation rollback guards",
            "Complete experience-payment official family, unit-play experience generation, prevention ordering, and all-window parity remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"
            ]),
        new(
            "BATTLEFIELD_REPLACEMENT_COSTS",
            CoveredRepresentative,
            "Battlefield replacement representatives cover selected held-score payment, Brush context, and score-prevention payment branches.",
            "Battlefield replacement prompt quote coverage via held-score payment and Brush-context payment representatives",
            "Battlefield replacement command-side revalidation via held-score payment commit and score-prevention command paths",
            "COST_PAID audit payload assertions across battlefield held payment and replacement-adjacent representatives",
            "Battlefield replacement stale battlefield, already scored, invalid payment resource, and prevented score no-mutation rollback guards",
            "Complete battlefield replacement ordering, prevention variants, response timing, and cross-window resource generation remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AI_PAYMENT_ENGINE_BATTLEFIELD_BRUSH_CONTEXT_AUDIT.md"
            ]),
        new(
            "COST_MODIFIER_PAYMENTS",
            CoveredRepresentative,
            "Cost modifier representatives cover selected reduction, increase, minimum-cost, and battlefield/equipment payment modifier branches.",
            "Cost modifier prompt quote coverage via play/equipment optional branch prompts and modifier metadata",
            "Cost modifier command-side revalidation via current reduction, increase, and minimum-cost representative commits",
            "COST_PAID audit payload assertions for modified quoted cost and paid resource totals",
            "Cost modifier stale modifier, insufficient modified cost, wrong resource, and minimum-cost no-mutation rollback guards",
            "Complete cost reduction, cost increase, minimum replacement ordering, and all official modifier stacking branches remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md",
                "docs/CURRENT_STAGE4D_04P_STATIC_AURA_COST_MODIFIER_LAYERING_AUDIT.md"
            ]),
        new(
            "OPTIONAL_EXTRA_ALTERNATIVE_COSTS",
            CoveredRepresentative,
            "Optional, extra, and alternative payment representatives cover selected play, standby, equipment, and Azir reattach branches.",
            "Optional / extra / alternative prompt quote coverage via play optional, hide-card alternative, equipment optional, and Azir reattach prompts",
            "Optional / extra / alternative command-side revalidation via selected play, hide-card, equipment, and Azir command commits",
            "COST_PAID audit payload assertions for accepted optional, extra, and alternative payment branches",
            "Optional decline, invalid reattach, insufficient alternative cost, wrong resource, and stale option no-mutation rollback guards",
            "Complete official optional, extra, alternative, target-scoped branch, and all-window payment parity remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md",
                "docs/CURRENT_STAGE4D_04D_EQUIPMENT_OPTIONAL_TARGET_BRANCH_AUDIT.md",
                "docs/CURRENT_STAGE4D_04E_EQUIPMENT_EXTRA_PAY_BRANCH_AUDIT.md"
            ]),
        new(
            "TEMPORARY_RESOURCE_PARITY",
            CoveredRepresentative,
            "Temporary-resource parity representatives cover play, activated ability, battlefield held, trigger payment, and pending PAY_COST windows.",
            "Temporary-resource parity prompt quote coverage via inline play, Renata typed temporary, battlefield held, trigger, and pending prompts",
            "Temporary-resource parity command-side revalidation via temporary payment resource commit paths across representative windows",
            "COST_PAID audit payload assertions plus temporary payment resource spent / cleared ledger assertions",
            "Temporary-resource wrong window, wrong trait, expired resource, invalid resource id, and stale pending no-mutation rollback guards",
            "Complete temporary-resource official matrix, cross-window generation/consumption ordering, and all invalid resource failures remain open.",
            "Representative keyword branch coverage only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AJ_PAYMENT_ENGINE_RENATA_TYPED_TEMP_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_AUDIT.md",
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
    public void PaymentEngineOfficialMatrixResidualManifestListsRequiredAxesExactlyOnce()
    {
        var requiredAxes = new[]
        {
            "ACTION_WINDOWS",
            "PAYMENT_SOURCES",
            "RESOURCE_SKILLS",
            "TARGET_TAXES",
            "KEYWORD_BRANCHES",
            "COST_MODIFIERS",
            "OPTIONAL_EXTRA_ALTERNATIVE_COSTS",
            "REPLACEMENT_PREVENTION",
            "RESOURCE_ACTIONS",
            "ROLLBACK_FAILURE_BRANCHES",
            "CROSS_WINDOW_GENERATION_CONSUMPTION",
            "CARD_MATRIX_ALIGNMENT"
        };

        Assert.Equal(
            requiredAxes.Order(StringComparer.Ordinal),
            OfficialPaymentEngineMatrixResidualManifest.Select(entry => entry.Axis).Order(StringComparer.Ordinal));
        Assert.Empty(OfficialPaymentEngineMatrixResidualManifest
            .GroupBy(entry => entry.Axis, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineOfficialMatrixResidualManifestRequiresPromptCommandAuditRollbackAndDocAnchors()
    {
        Assert.All(OfficialPaymentEngineMatrixResidualManifest, entry =>
        {
            Assert.Equal(RemainingOfficialGap, entry.Classification);
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeEvidence));
            Assert.False(string.IsNullOrWhiteSpace(entry.RequiredFutureVerifier));
            Assert.Contains("prompt", entry.PromptCommandAuditExpectation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.PromptCommandAuditExpectation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit", entry.PromptCommandAuditExpectation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-mutation", entry.RollbackExpectation, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineOfficialMatrixResidualManifestKeepsResidualBlockerAsRemainingOfficialGap()
    {
        var residualBlocker = Assert.Single(
            ResidualBlockerManifest,
            entry => string.Equals(entry.Family, "OFFICIAL_PAYMENT_ENGINE_MATRIX", StringComparison.Ordinal));

        Assert.Equal(RemainingOfficialGap, residualBlocker.Classification);
        Assert.Contains("No generated official matrix", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("Full cross-window official matrix is missing", residualBlocker.MissingOfficialBreadth, StringComparison.Ordinal);
        Assert.Contains("prompt / command / audit parity", residualBlocker.RollbackExpectation, StringComparison.Ordinal);
        Assert.Contains("no-mutation", residualBlocker.RollbackExpectation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            residualBlocker.DocAnchors,
            anchor => anchor.Contains("03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF", StringComparison.Ordinal));

        Assert.All(OfficialPaymentEngineMatrixResidualManifest, entry => Assert.Equal(RemainingOfficialGap, entry.Classification));
    }

    [Fact]
    public void PaymentEngineOfficialMatrixResidualManifestKeepsOfficialBreadthExplicit()
    {
        var combinedText = string.Join(
            " ",
            OfficialPaymentEngineMatrixResidualManifest.SelectMany(entry =>
                new[]
                {
                    entry.Axis,
                    entry.RepresentativeEvidence,
                    entry.RequiredFutureVerifier,
                    entry.PromptCommandAuditExpectation,
                    entry.RollbackExpectation,
                    entry.RemainingOfficialBreadth
                }.Concat(entry.DocAnchors)));

        foreach (var requiredPhrase in new[]
        {
            "action-window",
            "payment source",
            "resource skill",
            "target tax",
            "keyword",
            "cost modifier",
            "optional",
            "extra",
            "alternative",
            "replacement",
            "resource action",
            "no-mutation",
            "cross-window",
            "card matrix"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineOfficialMatrixResidualManifestDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            OfficialPaymentEngineMatrixResidualManifest.SelectMany(entry =>
                new[]
                {
                    entry.Axis,
                    entry.Classification,
                    entry.RepresentativeEvidence,
                    entry.RequiredFutureVerifier,
                    entry.PromptCommandAuditExpectation,
                    entry.RollbackExpectation,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("READY", combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
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
    public void PaymentEngineKeywordPaymentBranchManifestListsRequiredBranchesExactlyOnce()
    {
        var requiredBranches = new[]
        {
            "HASTE_READY",
            "ECHO_OPTIONAL_PAYMENT",
            "SPELLSHIELD_TARGET_TAX",
            "EXPERIENCE_PAYMENT",
            "BATTLEFIELD_REPLACEMENT_COSTS",
            "COST_MODIFIER_PAYMENTS",
            "OPTIONAL_EXTRA_ALTERNATIVE_COSTS",
            "TEMPORARY_RESOURCE_PARITY"
        };

        Assert.Equal(
            requiredBranches.Order(StringComparer.Ordinal),
            KeywordPaymentBranchManifest.Select(entry => entry.Branch).Order(StringComparer.Ordinal));
        Assert.Empty(KeywordPaymentBranchManifest
            .GroupBy(entry => entry.Branch, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineKeywordPaymentBranchManifestRequiresPromptCommandAuditAndRollbackAnchors()
    {
        Assert.All(KeywordPaymentBranchManifest, entry =>
        {
            Assert.Equal(CoveredRepresentative, entry.Classification);
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COST_PAID", entry.AuditAnchor, StringComparison.Ordinal);
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
    public void PaymentEngineKeywordPaymentBranchManifestKeepsResidualBlockerAsRemainingOfficialGap()
    {
        var residualBlocker = Assert.Single(
            ResidualBlockerManifest,
            entry => string.Equals(entry.Family, "KEYWORD_PAYMENT_BRANCHES", StringComparison.Ordinal));

        Assert.Equal(RemainingOfficialGap, residualBlocker.Classification);
        Assert.Contains("HASTE_READY", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("Echo", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("Spellshield", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("experience", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("temporary-resource", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("no-mutation", residualBlocker.RollbackExpectation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaymentEngineKeywordPaymentBranchManifestKeepsOfficialBreadthExplicit()
    {
        var combinedRemainingBreadth = string.Join(
            " ",
            KeywordPaymentBranchManifest.Select(entry => entry.RemainingOfficialBreadth));

        Assert.Contains("Haste", combinedRemainingBreadth, StringComparison.Ordinal);
        Assert.Contains("Echo", combinedRemainingBreadth, StringComparison.Ordinal);
        Assert.Contains("Spellshield", combinedRemainingBreadth, StringComparison.Ordinal);
        Assert.Contains("experience", combinedRemainingBreadth, StringComparison.Ordinal);
        Assert.Contains("replacement", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("optional", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extra", combinedRemainingBreadth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temporary-resource", combinedRemainingBreadth, StringComparison.Ordinal);
        Assert.Contains("all-window", combinedRemainingBreadth, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineKeywordPaymentBranchManifestDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            KeywordPaymentBranchManifest.SelectMany(entry =>
                new[]
                {
                    entry.Branch,
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
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal)
                .Replace("READY_UNIT", string.Empty, StringComparison.Ordinal)
                .Replace("CRIMSON_ROSE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
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
    public void PaymentEngineResourceSkillCoverageManifestKeepsResidualBlockerCatalogBound()
    {
        var residualBlocker = Assert.Single(
            ResidualBlockerManifest,
            entry => string.Equals(entry.Family, "RESOURCE_SKILL_A_C_FAMILY", StringComparison.Ordinal));

        Assert.Equal(CatalogBoundRepresentative, residualBlocker.Classification);
        Assert.Contains("19", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("IsResourceSkill=true", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("[A]", residualBlocker.MissingOfficialBreadth, StringComparison.Ordinal);
        Assert.Contains("[C]", residualBlocker.MissingOfficialBreadth, StringComparison.Ordinal);
        Assert.Contains("cross-window", residualBlocker.MissingOfficialBreadth, StringComparison.Ordinal);
        Assert.Contains("no-mutation", residualBlocker.RollbackExpectation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT READY", residualBlocker.ClosureStatus, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", residualBlocker.ClosureStatus, StringComparison.Ordinal);
        Assert.Contains(
            residualBlocker.DocAnchors,
            anchor => anchor.Contains("03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT", StringComparison.Ordinal));

        Assert.Equal(
            19,
            ResourceSkillCoverageManifest.SelectMany(entry => entry.AbilityIds).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PaymentEngineResourceSkillCoverageManifestKeepsOfficialBreadthExplicit()
    {
        var residualBlocker = Assert.Single(
            ResidualBlockerManifest,
            entry => string.Equals(entry.Family, "RESOURCE_SKILL_A_C_FAMILY", StringComparison.Ordinal));
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
                }.Concat(entry.DocAnchors)).Concat(
                    [
                        residualBlocker.CurrentEvidence,
                        residualBlocker.ExistingRepresentativeEvidence,
                        residualBlocker.MissingOfficialBreadth,
                        residualBlocker.RollbackExpectation
                    ]));

        Assert.Contains("Malzahar", combinedText, StringComparison.Ordinal);
        Assert.Contains("target-as-cost", combinedText, StringComparison.Ordinal);
        Assert.Contains("Dragon Soul Sage", combinedText, StringComparison.Ordinal);
        Assert.Contains("reaction", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sigil", combinedText, StringComparison.Ordinal);
        Assert.Contains("typed", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temporary", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conversion", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Gold token", combinedText, StringComparison.Ordinal);
        Assert.Contains("payment-only", combinedText, StringComparison.Ordinal);
        Assert.Contains("generated", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[A]", combinedText, StringComparison.Ordinal);
        Assert.Contains("[C]", combinedText, StringComparison.Ordinal);
        Assert.Contains("cross-window", combinedText, StringComparison.Ordinal);
        Assert.All(ResourceSkillCoverageManifest, entry =>
            Assert.Contains("full-official resource skill breadth", entry.ClosureStatus, StringComparison.Ordinal));
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
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
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

    private sealed record PaymentEngineOfficialMatrixResidualCoverageEntry(
        string Axis,
        string Classification,
        string RepresentativeEvidence,
        string RequiredFutureVerifier,
        string PromptCommandAuditExpectation,
        string RollbackExpectation,
        string RemainingOfficialBreadth,
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

    private sealed record PaymentEngineKeywordPaymentBranchCoverageEntry(
        string Branch,
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
