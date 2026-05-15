using System.IO;
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
    private const string RepresentativeSeed = "representative-seed";
    private const string MissingOfficialRow = "missing-official-row";
    private const string PolicyDeferredRow = "policy-deferred-row";
    private const string RollbackFailureRepresentative = "rollback-failure-representative";
    private const string CrossWindowRepresentative = "cross-window-representative";
    private const string CardMatrixRepresentative = "card-matrix-representative";

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

    private static readonly PaymentEngineOfficialMatrixSeedRowCoverageEntry[] OfficialPaymentEngineMatrixSeedRowManifest =
    [
        new(
            "ROW_ACTION_WINDOWS_PLAY_CARD_TYPED_RESOURCE_SEED",
            "ACTION_WINDOWS",
            RepresentativeSeed,
            "PLAY_CARD",
            "typed power plus recycle and temporary payment resource",
            "PLAY_CARD typed-resource representative from the action-window coverage manifest.",
            "PaymentEngineUnificationTests prompt coverage for PLAY_CARD typed resource quote and recycle choices",
            "PaymentEngineUnificationTests command coverage for PLAY_CARD typed resource commit and revalidation",
            "COST_PAID / RESOURCE_RECYCLED / TEMPORARY_PAYMENT_RESOURCE_SPENT audit assertions for representative PLAY_CARD rows",
            "Wrong trait, stale prompt, invalid resource and insufficient payment must roll back with no-mutation semantics.",
            "Full official PLAY_CARD row combinations across every source, modifier, optional branch and failure branch remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md"
            ]),
        new(
            "ROW_PAYMENT_SOURCES_PAY_COST_TEMPORARY_SEED",
            "PAYMENT_SOURCES",
            RepresentativeSeed,
            "PAY_COST",
            "mana, generic power, recycle resource and temporary payment resource",
            "Pending PAY_COST representative covering temporary payment-only resource quote and commit.",
            "PaymentEngineUnificationTests prompt coverage for pending PAY_COST temporary resource metadata",
            "PaymentEngineUnificationTests command coverage for pending PAY_COST commit, spend and cleanup",
            "COST_PAID / TEMPORARY_PAYMENT_RESOURCE_SPENT / TEMPORARY_PAYMENT_RESOURCE_CLEARED audit assertions",
            "Wrong resource id, expired temporary resource, stale pending payment and insufficient source totals must roll back with no-mutation semantics.",
            "Full official payment-source mixing, generated-source lifetime and source-specific failure rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"
            ]),
        new(
            "ROW_RESOURCE_SKILLS_MALZAHAR_TARGET_AS_COST_SEED",
            "RESOURCE_SKILLS",
            RepresentativeSeed,
            "ACTIVATE_ABILITY",
            "target-as-cost generated payment-only resource skill",
            "Malzahar resource skill representative from the catalog-bound resource skill manifest.",
            "MalzaharResourceSkillTests prompt coverage for target-as-cost payment-only resource generation",
            "MalzaharResourceSkillTests command coverage for source exhaust, friendly target destruction and generated resource creation",
            "ABILITY_ACTIVATED / UNIT_DESTROYED / POWER_GAINED audit assertions for resource skill rows",
            "Invalid timing, stale target, exhausted source, duplicate source and payment-only misuse must roll back with no-mutation semantics.",
            "Full official [A] / [C] resource skill family, generated resource type and cross-window consumption rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md"
            ]),
        new(
            "ROW_TARGET_TAXES_XERATH_SPELLSHIELD_SEED",
            "TARGET_TAXES",
            RepresentativeSeed,
            "ACTIVATE_ABILITY",
            "target tax for Spellshield enemy unit target",
            "Xerath target damage representative covering Spellshield target tax quote and command commit.",
            "PaymentEngineUnificationTests prompt coverage for Xerath Spellshield target tax metadata",
            "PaymentEngineUnificationTests command coverage for Xerath target tax payment and revalidation",
            "COST_PAID spellshieldTaxMana / spellshieldTaxTargetObjectIds audit assertions",
            "Invalid target, stale target, insufficient target tax, wrong controller and target-count mismatch must roll back with no-mutation semantics.",
            "Full official target tax, dependency target, target-count and stale legality rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"
            ]),
        new(
            "ROW_KEYWORD_BRANCHES_HASTE_READY_SEED",
            "KEYWORD_BRANCHES",
            RepresentativeSeed,
            "PLAY_CARD",
            "HASTE_READY optional payment branch",
            "Rek'Sai and current registry-bound HASTE_READY representatives covering one mana plus typed power branch.",
            "HASTE_READY prompt coverage via fixture runner play prompts and RekSaiHasteReadyRedPaymentTests",
            "HASTE_READY command-side payment revalidation via p4 play Haste fixtures and RekSai command commits",
            "COST_PAID audit payload assertions for HASTE_READY mana plus typed power rows",
            "Wrong trait, insufficient cost, exhausted source and stale play must roll back with no-mutation semantics.",
            "Full official Haste, Echo, Spellshield, experience and all-window keyword parity rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_AUDIT.md"
            ]),
        new(
            "ROW_COST_MODIFIERS_BATTLEFIELD_EQUIPMENT_SEED",
            "COST_MODIFIERS",
            RepresentativeSeed,
            "PLAY_CARD",
            "cost reduction, cost increase and minimum-cost modifier representative",
            "Selected battlefield and equipment payment modifier representatives from keyword payment branch coverage.",
            "Cost modifier prompt coverage via play and equipment optional branch prompts and modifier metadata",
            "Cost modifier command-side revalidation via current reduction, increase and minimum-cost representative commits",
            "COST_PAID audit payload assertions for modified quoted cost and paid resource totals",
            "Stale modifier, insufficient modified cost, illegal zeroing, wrong resource and stacking-order mismatch must roll back with no-mutation semantics.",
            "Full official cost reduction, cost increase, minimum rule and modifier stacking rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md"
            ]),
        new(
            "ROW_OPTIONAL_EXTRA_ALTERNATIVE_AZIR_REATTACH_SEED",
            "OPTIONAL_EXTRA_ALTERNATIVE_COSTS",
            RepresentativeSeed,
            "ACTIVATE_ABILITY",
            "target-scoped optional armament reattach branch",
            "Azir optional armament reattach representative with accepted and invalid target-scoped option coverage.",
            "AzirSwiftSwapActivatedAbilityTests prompt coverage for optional armament reattach token and target choices",
            "AzirSwiftSwapActivatedAbilityTests command coverage for accepted reattach payment branch and stack revalidation",
            "COST_PAID / ABILITY_ACTIVATED / EQUIPMENT_REATTACHED audit assertions for optional branch rows",
            "Invalid option, stale option target, insufficient alternative cost, wrong branch resource and declined payment side effects must roll back with no-mutation semantics.",
            "Full official optional, extra, alternative, target-scoped and mixed-branch rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md"
            ]),
        new(
            "ROW_REPLACEMENT_PREVENTION_BATTLEFIELD_HELD_SEED",
            "REPLACEMENT_PREVENTION",
            RepresentativeSeed,
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "score prevention and battlefield replacement-adjacent payment",
            "Battlefield held score payment representative with prevention and no-effect guard coverage.",
            "ConformanceFixtureRunnerTests prompt coverage for battlefield held score payment quote",
            "ConformanceFixtureRunnerTests command coverage for battlefield held payment commit and prevention handling",
            "COST_PAID / BATTLEFIELD_HELD / SCORE_GAINED or prevented-effect audit assertions",
            "Prevented score, stale replacement source, already-scored battlefield and invalid payment resource must roll back with no-mutation semantics.",
            "Full official replacement, prevention, ordering and payment-adjacent no-effect rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md"
            ]),
        new(
            "ROW_RESOURCE_ACTIONS_TRIGGER_PAYMENT_SEED",
            "RESOURCE_ACTIONS",
            RepresentativeSeed,
            "TRIGGER_PAYMENT",
            "trigger resource-action pending payment",
            "SFD Fiora and adjacent trigger payment representatives covering pay, decline and stale guards.",
            "TriggerPaymentTests prompt coverage for TRIGGER_PAYMENT pending metadata and typed-yellow resource quote",
            "TriggerPaymentTests command coverage for accepted PayCost and declined trigger payment branches",
            "COST_PAID / TRIGGER_PAYMENT_DECLINED / BATTLEFIELD_TRIGGER_RESOLVED / PAYMENT_WINDOW_CLOSED audit assertions",
            "Stale source, stale target, invalid pending payment, declined branch, wrong resource and replacement-denied action must roll back with no-mutation semantics.",
            "Full official legend, battlefield, trigger and future resource-action rows remain open.",
            "Representative seed only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"
            ]),
        new(
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "ROLLBACK_FAILURE_BRANCHES",
            MissingOfficialRow,
            "ALL_PAYMENT_WINDOWS",
            "every illegal payment command and stale prompt / command shape",
            "No generated official row currently enumerates every illegal payment command, stale prompt, stale command, invalid resource and no-effect branch.",
            "Missing official row: future prompt schema must identify each stale or illegal payment quote before closure.",
            "Missing official row: future command schema must revalidate and reject each stale or illegal payment command before closure.",
            "Missing official row: future audit schema must prove rejected rows emit no committed COST_PAID or domain success audit.",
            "Every rejected branch must preserve hand, board, resource ledgers, pending queues, stack, score and audit state with no-mutation semantics.",
            "Full official rollback breadth, mutation boundary proof and failure branch combinations remain open.",
            "Missing official row only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md"
            ]),
        new(
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "CROSS_WINDOW_GENERATION_CONSUMPTION",
            MissingOfficialRow,
            "ALL_GENERATION_AND_CONSUMPTION_WINDOWS",
            "generated payment-only resource creation, restriction, spend, expiry and cleanup",
            "Current representatives cover selected generated and temporary resources, but no official row matrix enumerates every generation and consumption pairing.",
            "Missing official row: future prompt schema must bind generated-resource creation, payment-only restriction and consumption candidates.",
            "Missing official row: future command schema must revalidate generated resource lifetime, restriction and spend window.",
            "Missing official row: future audit schema must bind creation event, spend event, cleanup event and rejected reuse shape.",
            "Expired generated resource, wrong consumption window, duplicate spend, stale pending payment and resource restriction bypass must roll back with no-mutation semantics.",
            "Full official cross-window generation and consumption, lifetime ordering and invalid reuse rows remain open.",
            "Missing official row only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "CARD_MATRIX_ALIGNMENT",
            MissingOfficialRow,
            "ALL_CARD_PAYMENT_ROWS",
            "every official card payment branch mapped to row evidence",
            "Current representative manifests do not prove every official card payment branch or card-matrix row.",
            "Missing official row: future prompt schema must remain traceable from each card-matrix row to an executable prompt shape.",
            "Missing official row: future command schema must remain traceable from each card-matrix row to an executable command path.",
            "Missing official row: future audit schema must remain traceable from each card-matrix row to audit events and rollback guards.",
            "Unmapped card row, untested branch, stale card behavior metadata, missing rollback and mismatched audit expectation must remain no-mutation blockers.",
            "Full official card matrix alignment, all card payment branches and final P0-005 closure evidence remain open.",
            "Missing official row only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_A_MASTER_CHECKPOINT.md",
                "docs/CURRENT_COMPLETION_AUDIT.md"
            ]),
        new(
            "ROW_MOVE_UNIT_POLICY_DEFERRED",
            "ACTION_WINDOWS",
            PolicyDeferredRow,
            "MOVE_UNIT",
            "movement permission with optional-cost policy boundary",
            "MOVE_UNIT remains movement-permission / optional-cost policy today, not a rune, mana, experience or temporary-resource payment row.",
            "MOVE_UNIT prompt is policy movement permission metadata, not a PaymentEngine payment prompt.",
            "MOVE_UNIT command revalidates movement permission; no payment command row is opened unless future official rules add a resource cost.",
            "MOVE_UNIT audit is movement / permission audit today, not COST_PAID audit.",
            "Destination legality, optional movement costs, rejected source / destination shapes and future resource reclassification must preserve no-mutation semantics.",
            "Full official movement payment row remains deferred unless official rules add a payment source to MOVE_UNIT.",
            "Policy deferred row only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_AUDIT.md"
            ])
    ];

    private static readonly PaymentEngineRollbackFailureRowCoverageEntry[] RollbackFailureRowManifest =
    [
        new(
            "STALE_PROMPT_PENDING_PAYMENT",
            RollbackFailureRepresentative,
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "PAY_COST / TRIGGER_PAYMENT / generated pending payment windows",
            "Representative stale prompt and stale pending payment guards from pending payment and trigger payment rows.",
            "PaymentEngineUnificationTests prompt coverage for pending PAY_COST metadata before stale command rejection",
            "PaymentEngineUnificationTests and TriggerPaymentTests command coverage for stale pending payment rejection",
            "Rejected stale rows must not emit committed COST_PAID, RESOURCE_RECYCLED, TRIGGER_PAYMENT_DECLINED, or domain success audit entries.",
            "Stale prompt or stale pending payment rejection must preserve hand, board, ledgers, pending queues, stack, score and audit state with no-mutation semantics.",
            "Full official stale prompt, stale pending payment, duplicate command and payment-window close combinations remain open.",
            "Rollback representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"
            ]),
        new(
            "INVALID_PAYMENT_SOURCE_OR_TRAIT",
            RollbackFailureRepresentative,
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "PLAY_CARD / ACTIVATE_ABILITY / ASSEMBLE_EQUIPMENT / PAY_COST",
            "Representative invalid resource id, wrong trait, unnecessary resource and payment-only misuse guards.",
            "PaymentEngineUnificationTests prompt coverage for legal resource candidates and trait-specific payment quotes",
            "PaymentEngineUnificationTests command coverage for invalid resource id, wrong trait and unnecessary resource rejection",
            "Rejected invalid source rows must not emit committed COST_PAID, POWER_SPENT, RESOURCE_RECYCLED, or TEMPORARY_PAYMENT_RESOURCE_SPENT audit entries.",
            "Invalid resource id, wrong trait, duplicate resource or payment-only misuse rejection must preserve all mutable game state with no-mutation semantics.",
            "Full official source mixing, generated-source restriction, trait matrix and duplicate resource combinations remain open.",
            "Rollback representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "INSUFFICIENT_COST_OR_TARGET_TAX",
            RollbackFailureRepresentative,
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "PLAY_CARD / ACTIVATE_ABILITY / target-tax payment windows",
            "Representative insufficient mana, power, experience and Spellshield target tax guards.",
            "SpellshieldTaxCoverageManifest and PaymentEngineUnificationTests prompt coverage for target tax and cost quote metadata",
            "PaymentEngineUnificationTests command coverage for insufficient base cost and target-tax rejection",
            "Rejected insufficient cost rows must not emit committed COST_PAID, ABILITY_ACTIVATED, PLAY_CARD_COMMITTED, or target-tax success audit entries.",
            "Insufficient mana, typed power, experience or target-tax rejection must preserve source readiness, targets, stack, score and ledgers with no-mutation semantics.",
            "Full official insufficient-cost, tax stacking, target-count and modifier interaction combinations remain open.",
            "Rollback representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "STALE_SOURCE_TARGET_OR_OPTION",
            RollbackFailureRepresentative,
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "ACTIVATE_ABILITY / TRIGGER_PAYMENT / target-bearing payment windows",
            "Representative stale source, stale target and stale target-scoped option guards.",
            "TargetColoredActivatedAbilityCoverageManifest prompt coverage for legal source, target and target-scoped option metadata",
            "AzirSwiftSwapActivatedAbilityTests, TriggerPaymentTests and target-bearing command coverage for stale source or target rejection",
            "Rejected stale source or target rows must not emit committed COST_PAID, ABILITY_ACTIVATED, EQUIPMENT_REATTACHED, or target effect success audit entries.",
            "Stale source, stale target or stale option rejection must preserve source state, target state, attachments, ledgers, stack and audit state with no-mutation semantics.",
            "Full official target dependency, target-count, stale option and all target-bearing payment failure combinations remain open.",
            "Rollback representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "OPTIONAL_EXTRA_ALTERNATIVE_BRANCH",
            RollbackFailureRepresentative,
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "PLAY_CARD / HIDE_CARD / ASSEMBLE_EQUIPMENT / ACTIVATE_ABILITY",
            "Representative invalid optional, extra, alternative and declined payment branch guards.",
            "KeywordPaymentBranchManifest prompt coverage for optional, extra and alternative payment branch quotes",
            "PaymentEngineUnificationTests command coverage for invalid optional branch, declined branch and malformed option rejection",
            "Rejected optional or alternative branch rows must not emit committed COST_PAID, branch success, equipment attach, hide-card, or play-card audit entries.",
            "Invalid option, duplicate option, declined branch or malformed alternative rejection must preserve hand, board, attachments, resources and stack with no-mutation semantics.",
            "Full official optional, extra, alternative, target-scoped and mixed-branch failure combinations remain open.",
            "Rollback representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md"
            ]),
        new(
            "REPLACEMENT_PREVENTION_NO_EFFECT",
            RollbackFailureRepresentative,
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "BATTLEFIELD_HELD_SCORE_PAYMENT / replacement-adjacent resource action windows",
            "Representative replacement, prevention and no-effect payment-adjacent guards.",
            "LegendBattlefieldTriggerResourceActionManifest prompt coverage for battlefield held and trigger resource-action payment rows",
            "ConformanceFixtureRunnerTests and TriggerPaymentTests command coverage for prevention, no-effect and stale resource-action rejection",
            "Rejected or prevented rows must not emit committed SCORE_GAINED, BATTLEFIELD_HELD, COST_PAID, or domain success audit entries unless the representative explicitly proves the prevented result.",
            "Replacement-prevented, no-effect, already-scored or stale replacement rejection must preserve score, battlefield state, ledgers, stack and audit state with no-mutation semantics.",
            "Full official replacement ordering, prevention, declined replacement and no-effect failure combinations remain open.",
            "Rollback representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "GENERATED_RESOURCE_LIFETIME_REUSE",
            RollbackFailureRepresentative,
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "RESOURCE_SKILL / generated payment-only resource consumption windows",
            "Representative generated payment-only resource lifetime, cleanup, wrong-window and duplicate-spend guards.",
            "ResourceSkillCoverageManifest and OfficialPaymentEngineMatrixResidualManifest prompt coverage for generated resource creation and consumption candidates",
            "MalzaharResourceSkillTests and PaymentEngineUnificationTests command coverage for generated resource spend, expiry, wrong-window and duplicate-spend rejection",
            "Rejected generated-resource reuse rows must not emit committed TEMPORARY_PAYMENT_RESOURCE_SPENT, POWER_GAINED reuse success, COST_PAID, or cleanup success audit entries.",
            "Expired generated resource, wrong consumption window, duplicate spend or restriction bypass rejection must preserve ledgers, pending queues, hand, board and audit state with no-mutation semantics.",
            "Full official generated-resource lifetime, cross-window generation / consumption and invalid reuse combinations remain open.",
            "Rollback representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ])
    ];

    private static readonly PaymentEngineCrossWindowGenerationConsumptionRowCoverageEntry[] CrossWindowGenerationConsumptionRowManifest =
    [
        new(
            "RESOURCE_SKILL_GENERATION_WINDOWS",
            CrossWindowRepresentative,
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "ACTIVATE_ABILITY resource skill generation in open-main, reaction, spell-duel focus and equipment-token representatives",
            "PAY_COST / PLAY_CARD / ACTIVATE_ABILITY / ASSEMBLE_EQUIPMENT representative consumption windows",
            "Malzahar, Sigil, conversion equipment and Gold token resource skills currently create selected payment-only generated resources.",
            "ResourceSkillCoverageManifest prompt coverage for legal source, timing, generated resource amount and payment-only metadata",
            "MalzaharResourceSkillTests, resource conversion tests and Gold token command coverage for generated resource creation",
            "ABILITY_ACTIVATED / POWER_GAINED / TEMPORARY_PAYMENT_RESOURCE_CREATED audit assertions for generated resource creation",
            "Generated resources must carry payment-only lifetime metadata, and rejected misuse must preserve state with no-mutation semantics.",
            "Full official generation windows, every [A] / [C] resource skill family and all generated resource variants remain open.",
            "Cross-window representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md"
            ]),
        new(
            "INLINE_PAYMENT_CONSUMPTION_WINDOWS",
            CrossWindowRepresentative,
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "generated payment-only resources from resource skills and temporary ledgers",
            "PLAY_CARD / ACTIVATE_ABILITY / ASSEMBLE_EQUIPMENT inline payment windows",
            "4D-03K representatives consume generated or temporary payment resources in selected inline windows.",
            "PaymentEngineUnificationTests prompt coverage for inline temporary resource choices across play, activate and assemble windows",
            "PaymentEngineUnificationTests command coverage for inline generated resource spend and cleanup",
            "COST_PAID / TEMPORARY_PAYMENT_RESOURCE_SPENT / TEMPORARY_PAYMENT_RESOURCE_CLEARED audit assertions for inline consumption",
            "Inline generated resource spend must be single-use, payment-only, same-transaction safe and rejected misuse must remain no-mutation.",
            "Full official inline consumption across every legal payment window, source mix and modifier branch remain open.",
            "Cross-window representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "PENDING_PAYMENT_REUSE_AND_CLOSE",
            CrossWindowRepresentative,
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "pending PAY_COST and TRIGGER_PAYMENT windows that quote generated or temporary payment resources",
            "PAY_COST / TRIGGER_PAYMENT pending payment consumption or decline windows",
            "Pending PAY_COST and trigger payment representatives quote, spend, clear or decline selected generated payment resources.",
            "PaymentEngineUnificationTests and TriggerPaymentTests prompt coverage for pending payment resource metadata",
            "PaymentEngineUnificationTests and TriggerPaymentTests command coverage for accepted PayCost, declined trigger payment and stale pending rejection",
            "COST_PAID / TRIGGER_PAYMENT_DECLINED / PAYMENT_WINDOW_CLOSED / TEMPORARY_PAYMENT_RESOURCE_CLEARED audit assertions",
            "Pending payment close, decline, stale prompt and duplicate command paths must clear or preserve ledgers with no-mutation semantics.",
            "Full official pending-payment reuse, multi-trigger ordering, close timing and generated resource carryover combinations remain open.",
            "Cross-window representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md",
                "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"
            ]),
        new(
            "TYPED_GENERIC_CONVERSION_AND_MATCHING",
            CrossWindowRepresentative,
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "typed Sigil, conversion equipment and Gold token generated resources",
            "typed, generic and conversion payment consumption windows",
            "SFD / OGN Sigils, resource conversion equipment and Gold token representatives prove selected typed and generic generated resource paths.",
            "Sigil and conversion equipment prompt coverage for typed, generic and conversion generated resource choices",
            "Sigil, conversion equipment and Gold token command coverage for generated resource creation and matching legal spend",
            "POWER_GAINED / TEMPORARY_PAYMENT_RESOURCE_CREATED / COST_PAID audit assertions for typed and generic generated resources",
            "Wrong trait, wrong conversion direction, unnecessary generated source or duplicate spend must be rejected with no-mutation semantics.",
            "Full official typed/generic matching, conversion chaining, source mixing and trait interaction combinations remain open.",
            "Cross-window representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md",
                "docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md"
            ]),
        new(
            "EXPIRY_CLEANUP_AND_TURN_BOUNDARY",
            CrossWindowRepresentative,
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "generated and temporary payment resource ledgers across payment close, stack resolution and turn cleanup",
            "post-payment cleanup, end-turn cleanup and rejected stale consumption windows",
            "Current representatives clear temporary ledgers after spend, payment window close or turn/resource cleanup.",
            "PaymentEngineUnificationTests prompt coverage before cleanup and after generated resource candidates expire",
            "PaymentEngineUnificationTests command coverage for spend cleanup, payment close cleanup and expired resource rejection",
            "TEMPORARY_PAYMENT_RESOURCE_CLEARED / PAYMENT_WINDOW_CLOSED / COST_PAID absence audit assertions for expired resource paths",
            "Expiry, cleanup, stale resource id and turn-boundary reuse must not leak spendable state and must reject stale consumption with no-mutation semantics.",
            "Full official cleanup ordering, end-turn expiry, stack-resolution expiry and cross-window cleanup combinations remain open.",
            "Cross-window representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"
            ]),
        new(
            "PAYMENT_ONLY_RESTRICTIONS_AND_WRONG_WINDOW",
            CrossWindowRepresentative,
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "payment-only generated resources and ordinary rune pool boundaries",
            "ordinary mana/power use, wrong payment window and unsupported command paths",
            "Representative tests distinguish payment-only generated resources from ordinary rune pool resources.",
            "ResourceSkillCoverageManifest and action-window manifests prompt coverage for generated payment-only restrictions",
            "PaymentEngineUnificationTests command coverage for payment-only misuse, wrong window and unsupported generated resource rejection",
            "COST_PAID absence and no domain success audit assertions for rejected payment-only restriction bypass attempts",
            "Payment-only restriction, wrong-window consumption, ordinary-pool misuse and unsupported generated source rejection must be no-mutation.",
            "Full official restriction matrix across every generated source, payment window and ordinary resource interaction rows remain open.",
            "Cross-window representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "DUPLICATE_SPEND_AND_AUDIT_CORRELATION",
            CrossWindowRepresentative,
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "generated resource creation ids, spend ids and cleanup ids across windows",
            "duplicate spend, stale spend and audit-correlation verification windows",
            "Current representative evidence asserts selected creation, spend and cleanup events, but no full official id-correlation matrix exists.",
            "PaymentEngine coverage manifests prompt coverage for generated resource id candidates before command submit",
            "PaymentEngineUnificationTests command coverage for duplicate spend, stale generated resource id and invalid id rejection",
            "TEMPORARY_PAYMENT_RESOURCE_CREATED / SPENT / CLEARED and COST_PAID audit correlation assertions for representative rows",
            "Duplicate spend, stale id and mismatched audit correlation must reject with no-mutation semantics and without orphaned ledger entries.",
            "Full official id correlation, audit ordering, duplicate spend and stale id combinations remain open.",
            "Cross-window representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ])
    ];

    private static readonly PaymentEngineCardMatrixAlignmentRowCoverageEntry[] CardMatrixAlignmentRowManifest =
    [
        new(
            "MATRIX_ID_AND_STATUS_FIELDS",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "cardId / collectorId / oracleId / effectId / status fields",
            "Matrix row identity and status fields must stay traceable to PaymentEngine evidence before full official closure.",
            "Future prompt evidence must reference the card or effect row identity that produced each legal ActionPrompt payment branch.",
            "Future command evidence must reference the card or effect row identity that accepted or rejected each payment command.",
            "Future audit evidence must reference the card or effect row identity for COST_PAID and domain success / rejection events.",
            "Matrix alignment must preserve cardId, collectorId, oracleId, effectId, automated test status, fullOfficial flag and blocker fields.",
            "Full official identity mapping, duplicate collector variants, shared oracle rows and status-field consistency remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md",
                "docs/CURRENT_COMPLETION_AUDIT.md"
            ]),
        new(
            "PAYMENT_ROW_TO_CARD_MATRIX_MAPPING",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "PaymentEngine row schema to official card matrix rows",
            "Each PaymentEngine row family must be mappable from action window and payment profile to official card/effect rows.",
            "Future prompt evidence must map ActionPrompt action window, source and payment quote to a matrix card/effect row.",
            "Future command evidence must map accepted/rejected command paths to the same matrix card/effect row.",
            "Future audit evidence must map COST_PAID or no-success audit expectations to the same matrix card/effect row.",
            "Matrix alignment must connect action windows, payment sources, keyword branches, rollback branches and cross-window rows to card/effect blockers.",
            "Full official row-to-card mapping across every official card payment branch and payment source combination remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"
            ]),
        new(
            "REPRESENTATIVE_TEST_EVIDENCE_LINKS",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "accepted representative tests, audit docs and evidence docs",
            "Representative tests and audit docs are useful only when matrix rows identify them without upgrading full official status.",
            "Future prompt evidence links must name the focused test or fixture that proves the ActionPrompt shape for a matrix row.",
            "Future command evidence links must name the focused test or fixture that proves command commit or rejection for a matrix row.",
            "Future audit evidence links must name the focused assertion that proves audit payload or no-success behavior for a matrix row.",
            "Matrix alignment must keep representative automated evidence separate from full official automated evidence and blocker removal.",
            "Full official automated evidence coverage for every card/effect row, branch and failure case remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AT_AZIR_MATRIX_EVIDENCE_ALIGNMENT_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AS_CARD_MATRIX_READINESS_AUDIT.md"
            ]),
        new(
            "FULL_OFFICIAL_GATE_AND_COMPLETION_BLOCK",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "fullOfficial flag, blocker fields and completion audit gates",
            "Matrix rows must not promote representative PaymentEngine evidence to full official status before all official branches and blockers clear.",
            "Future prompt evidence must prove every official prompt branch before a matrix row can remove prompt-related blockers.",
            "Future command evidence must prove every official command path and no-mutation rejection before blockers are removed.",
            "Future audit evidence must prove every success, no-effect and rejection audit path before full official status changes.",
            "Matrix alignment must keep fullOfficial=false or equivalent blocker status until P0-005, full-card matrix and completion audit gates are proven.",
            "Full official gate evidence, frontend final validation, card matrix closure and completion audit remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_COMPLETION_AUDIT.md",
                "docs/CURRENT_A_MASTER_CHECKPOINT.md"
            ]),
        new(
            "FAQ_RULE_SOURCE_TRACE",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "official rules, FAQ, errata and matrix row rationale",
            "PaymentEngine matrix rows must preserve rule-source and FAQ rationale for card/effect behavior rather than relying on local inference.",
            "Future prompt evidence must cite the rule or FAQ source that justifies each legal payment prompt shape.",
            "Future command evidence must cite the rule or FAQ source that justifies each accepted or rejected payment command.",
            "Future audit evidence must cite the rule or FAQ source that justifies success, no-effect or rollback audit behavior.",
            "Matrix alignment must keep FAQ review blockers visible when official text or FAQ support is incomplete.",
            "Full official FAQ traceability, conflict resolution and errata-linked matrix evidence remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AP_CARD_MATRIX_READINESS_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md"
            ]),
        new(
            "FRONTEND_CONTRACT_AND_SNAPSHOT_TRACE",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "ActionPrompt, authoritative snapshot and frontend display contract",
            "Matrix alignment must prove frontend-visible card payment behavior is backed by server ActionPrompt and authoritative snapshot fields.",
            "Future prompt evidence must show the server ActionPrompt exposes legal payment choices for the matrix row.",
            "Future command evidence must show the frontend only submits server-supported command shapes for the matrix row.",
            "Future audit evidence must show authoritative snapshot or event payloads expose enough server-owned state for display.",
            "Matrix alignment must keep frontend from inferring legal targets, costs, payment sources, generated resources or card effects locally.",
            "Full official frontend contract coverage, final Chrome smoke and final E2E validation remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md",
                "docs/CURRENT_FRONTEND_REBUILD_PLAN.md"
            ]),
        new(
            "MATRIX_JSON_SYNC_AND_DRIFT_GUARD",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "matrix JSON, checkpoint docs and completion audit consistency",
            "Matrix alignment must keep JSON status, checkpoint summaries and completion audit blocker language synchronized.",
            "Future prompt evidence must be recorded in matrix JSON or referenced docs with a stable prompt anchor.",
            "Future command evidence must be recorded in matrix JSON or referenced docs with a stable command anchor.",
            "Future audit evidence must be recorded in matrix JSON or referenced docs with a stable audit anchor.",
            "Matrix alignment must prevent JSON status drift, stale evidence anchors and mismatched blocker counts.",
            "Full official JSON sync, blocker-count reconciliation and stale-evidence repair remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AT_AZIR_MATRIX_EVIDENCE_ALIGNMENT_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md"
            ]),
        new(
            "DEFERRED_BLOCKER_AND_STATUS_COUNTS",
            CardMatrixRepresentative,
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "NEEDS_ENGINE_SUPPORT, NEEDS_FAQ_REVIEW, shared oracle and implemented status counts",
            "Matrix alignment must keep deferred blockers and status counts visible until every row has sufficient official evidence.",
            "Future prompt evidence must explain which blockers are reduced by prompt coverage and which remain.",
            "Future command evidence must explain which blockers are reduced by command coverage and which remain.",
            "Future audit evidence must explain which blockers are reduced by audit coverage and which remain.",
            "Matrix alignment must keep blocker counts, shared oracle implementation rows and unresolved official-card gaps auditable.",
            "Full official blocker-count closure, all shared-oracle mappings and unresolved official-card evidence remain open.",
            "Card matrix representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AN_CARD_MATRIX_READINESS_AUDIT.md",
                "docs/CURRENT_STAGE4D_03AR_CARD_MATRIX_READINESS_AUDIT.md"
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
                "docs/CURRENT_STAGE4D_04G_ARMED_ASSAULTER_HASTE_TEMPERED_AUDIT.md"
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
                "docs/CURRENT_STAGE4D_02H_BATTLE_RESPONSE_BRUSH_REPLACEMENT_CONTEXT_AUDIT.md"
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
                "docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_AUDIT.md"
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
                "docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_AUDIT.md",
                "docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_AUDIT.md",
                "docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_AUDIT.md"
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
    public void PaymentEngineOfficialMatrixSeedRowManifestListsRequiredRowsExactlyOnce()
    {
        var requiredRowIds = new[]
        {
            "ROW_ACTION_WINDOWS_PLAY_CARD_TYPED_RESOURCE_SEED",
            "ROW_PAYMENT_SOURCES_PAY_COST_TEMPORARY_SEED",
            "ROW_RESOURCE_SKILLS_MALZAHAR_TARGET_AS_COST_SEED",
            "ROW_TARGET_TAXES_XERATH_SPELLSHIELD_SEED",
            "ROW_KEYWORD_BRANCHES_HASTE_READY_SEED",
            "ROW_COST_MODIFIERS_BATTLEFIELD_EQUIPMENT_SEED",
            "ROW_OPTIONAL_EXTRA_ALTERNATIVE_AZIR_REATTACH_SEED",
            "ROW_REPLACEMENT_PREVENTION_BATTLEFIELD_HELD_SEED",
            "ROW_RESOURCE_ACTIONS_TRIGGER_PAYMENT_SEED",
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "ROW_MOVE_UNIT_POLICY_DEFERRED"
        };

        Assert.Equal(
            requiredRowIds.Order(StringComparer.Ordinal),
            OfficialPaymentEngineMatrixSeedRowManifest.Select(entry => entry.RowId).Order(StringComparer.Ordinal));
        Assert.Empty(OfficialPaymentEngineMatrixSeedRowManifest
            .GroupBy(entry => entry.RowId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineOfficialMatrixSeedRowManifestRequiresSchemaFieldsAndClosureAnchors()
    {
        var allowedStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            RepresentativeSeed,
            MissingOfficialRow,
            PolicyDeferredRow
        };
        var residualAxes = OfficialPaymentEngineMatrixResidualManifest
            .Select(entry => entry.Axis)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(OfficialPaymentEngineMatrixSeedRowManifest, entry =>
        {
            Assert.Contains(entry.RowStatus, allowedStatuses);
            Assert.Contains(entry.Axis, residualAxes);
            Assert.False(string.IsNullOrWhiteSpace(entry.ActionWindow));
            Assert.False(string.IsNullOrWhiteSpace(entry.PaymentOrPolicyProfile));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeScope));
            Assert.Contains("prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit", entry.AuditAnchor, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineOfficialMatrixSeedRowsCoverEveryResidualAxis()
    {
        var residualAxes = OfficialPaymentEngineMatrixResidualManifest
            .Select(entry => entry.Axis)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var rowAxes = OfficialPaymentEngineMatrixSeedRowManifest
            .Select(entry => entry.Axis)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(residualAxes, rowAxes);
        Assert.Contains(
            OfficialPaymentEngineMatrixSeedRowManifest,
            entry => string.Equals(entry.RowStatus, PolicyDeferredRow, StringComparison.Ordinal)
                && string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineOfficialMatrixSeedRowsKeepSeedPolicyAndMissingRowsSeparate()
    {
        Assert.Contains(OfficialPaymentEngineMatrixSeedRowManifest, entry => string.Equals(entry.RowStatus, RepresentativeSeed, StringComparison.Ordinal));
        Assert.Contains(OfficialPaymentEngineMatrixSeedRowManifest, entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal));
        Assert.Contains(OfficialPaymentEngineMatrixSeedRowManifest, entry => string.Equals(entry.RowStatus, PolicyDeferredRow, StringComparison.Ordinal));

        Assert.All(
            OfficialPaymentEngineMatrixSeedRowManifest.Where(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal)),
            entry =>
            {
                Assert.Contains("Missing official row", entry.PromptAnchor, StringComparison.Ordinal);
                Assert.Contains("Missing official row", entry.CommandAnchor, StringComparison.Ordinal);
                Assert.Contains("Missing official row", entry.AuditAnchor, StringComparison.Ordinal);
                Assert.Contains("remain open", entry.RemainingOfficialBreadth, StringComparison.OrdinalIgnoreCase);
            });
        Assert.All(
            OfficialPaymentEngineMatrixSeedRowManifest.Where(entry => string.Equals(entry.RowStatus, RepresentativeSeed, StringComparison.Ordinal)),
            entry => Assert.Contains("Representative seed only", entry.ClosureStatus, StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineOfficialMatrixSeedRowsKeepConcreteRowBreadthVisible()
    {
        var combinedText = string.Join(
            " ",
            OfficialPaymentEngineMatrixSeedRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.RowId,
                    entry.Axis,
                    entry.ActionWindow,
                    entry.PaymentOrPolicyProfile,
                    entry.RepresentativeScope,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.RollbackExpectation,
                    entry.RemainingOfficialBreadth
                }.Concat(entry.DocAnchors)));

        foreach (var requiredPhrase in new[]
        {
            "PLAY_CARD",
            "PAY_COST",
            "ACTIVATE_ABILITY",
            "TRIGGER_PAYMENT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "MOVE_UNIT",
            "payment source",
            "resource skill",
            "target tax",
            "cost modifier",
            "optional",
            "replacement",
            "resource-action",
            "rollback",
            "cross-window",
            "card matrix"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineOfficialMatrixSeedRowsDoNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            OfficialPaymentEngineMatrixSeedRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.RowId,
                    entry.Axis,
                    entry.RowStatus,
                    entry.ActionWindow,
                    entry.PaymentOrPolicyProfile,
                    entry.RepresentativeScope,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.RollbackExpectation,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineRollbackFailureRowManifestListsRequiredFamiliesExactlyOnce()
    {
        var requiredFamilies = new[]
        {
            "STALE_PROMPT_PENDING_PAYMENT",
            "INVALID_PAYMENT_SOURCE_OR_TRAIT",
            "INSUFFICIENT_COST_OR_TARGET_TAX",
            "STALE_SOURCE_TARGET_OR_OPTION",
            "OPTIONAL_EXTRA_ALTERNATIVE_BRANCH",
            "REPLACEMENT_PREVENTION_NO_EFFECT",
            "GENERATED_RESOURCE_LIFETIME_REUSE"
        };

        Assert.Equal(
            requiredFamilies.Order(StringComparer.Ordinal),
            RollbackFailureRowManifest.Select(entry => entry.FailureFamily).Order(StringComparer.Ordinal));
        Assert.Empty(RollbackFailureRowManifest
            .GroupBy(entry => entry.FailureFamily, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineRollbackFailureRowManifestRequiresPromptCommandAuditRollbackAndDocAnchors()
    {
        Assert.All(RollbackFailureRowManifest, entry =>
        {
            Assert.Equal(RollbackFailureRepresentative, entry.Classification);
            Assert.Equal("ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING", entry.OfficialMatrixRowId);
            Assert.False(string.IsNullOrWhiteSpace(entry.PaymentWindowScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit", entry.AuditAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-mutation", entry.NoMutationAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("remain open", entry.RemainingOfficialBreadth, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineRollbackFailureRowsStayLinkedToOfficialMatrixMissingRow()
    {
        var missingRow = Assert.Single(
            OfficialPaymentEngineMatrixSeedRowManifest,
            entry => string.Equals(entry.RowId, "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING", StringComparison.Ordinal));
        var residualAxis = Assert.Single(
            OfficialPaymentEngineMatrixResidualManifest,
            entry => string.Equals(entry.Axis, "ROLLBACK_FAILURE_BRANCHES", StringComparison.Ordinal));

        Assert.Equal(MissingOfficialRow, missingRow.RowStatus);
        Assert.Equal(RemainingOfficialGap, residualAxis.Classification);
        Assert.Equal(missingRow.RowId, RollbackFailureRowManifest.Select(entry => entry.OfficialMatrixRowId).Distinct(StringComparer.Ordinal).Single());
        Assert.All(RollbackFailureRowManifest, entry => Assert.Contains("Rollback representative only", entry.ClosureStatus, StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineRollbackFailureRowsKeepFailureDimensionsExplicit()
    {
        var combinedText = string.Join(
            " ",
            RollbackFailureRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.FailureFamily,
                    entry.PaymentWindowScope,
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.NoMutationAnchor,
                    entry.RemainingOfficialBreadth
                }.Concat(entry.DocAnchors)));

        foreach (var requiredPhrase in new[]
        {
            "stale",
            "invalid",
            "insufficient",
            "wrong trait",
            "target",
            "optional",
            "extra",
            "alternative",
            "replacement",
            "generated",
            "duplicate",
            "cross-window",
            "no-mutation"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineRollbackFailureRowsDoNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            RollbackFailureRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.FailureFamily,
                    entry.Classification,
                    entry.OfficialMatrixRowId,
                    entry.PaymentWindowScope,
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.NoMutationAnchor,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineCrossWindowGenerationConsumptionManifestListsRequiredFamiliesExactlyOnce()
    {
        var requiredFamilies = new[]
        {
            "RESOURCE_SKILL_GENERATION_WINDOWS",
            "INLINE_PAYMENT_CONSUMPTION_WINDOWS",
            "PENDING_PAYMENT_REUSE_AND_CLOSE",
            "TYPED_GENERIC_CONVERSION_AND_MATCHING",
            "EXPIRY_CLEANUP_AND_TURN_BOUNDARY",
            "PAYMENT_ONLY_RESTRICTIONS_AND_WRONG_WINDOW",
            "DUPLICATE_SPEND_AND_AUDIT_CORRELATION"
        };

        Assert.Equal(
            requiredFamilies.Order(StringComparer.Ordinal),
            CrossWindowGenerationConsumptionRowManifest.Select(entry => entry.Family).Order(StringComparer.Ordinal));
        Assert.Empty(CrossWindowGenerationConsumptionRowManifest
            .GroupBy(entry => entry.Family, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineCrossWindowGenerationConsumptionManifestRequiresPromptCommandAuditLifetimeAndDocAnchors()
    {
        Assert.All(CrossWindowGenerationConsumptionRowManifest, entry =>
        {
            Assert.Equal(CrossWindowRepresentative, entry.Classification);
            Assert.Equal("ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING", entry.OfficialMatrixRowId);
            Assert.False(string.IsNullOrWhiteSpace(entry.GenerationScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.ConsumptionScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit", entry.AuditAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-mutation", entry.LifetimeAndRestrictionAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("remain open", entry.RemainingOfficialBreadth, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineCrossWindowGenerationConsumptionRowsStayLinkedToOfficialMatrixMissingRow()
    {
        var missingRow = Assert.Single(
            OfficialPaymentEngineMatrixSeedRowManifest,
            entry => string.Equals(entry.RowId, "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING", StringComparison.Ordinal));
        var residualAxis = Assert.Single(
            OfficialPaymentEngineMatrixResidualManifest,
            entry => string.Equals(entry.Axis, "CROSS_WINDOW_GENERATION_CONSUMPTION", StringComparison.Ordinal));

        Assert.Equal(MissingOfficialRow, missingRow.RowStatus);
        Assert.Equal(RemainingOfficialGap, residualAxis.Classification);
        Assert.Equal(missingRow.RowId, CrossWindowGenerationConsumptionRowManifest.Select(entry => entry.OfficialMatrixRowId).Distinct(StringComparer.Ordinal).Single());
        Assert.All(CrossWindowGenerationConsumptionRowManifest, entry => Assert.Contains("Cross-window representative only", entry.ClosureStatus, StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineCrossWindowGenerationConsumptionRowsKeepLifecycleDimensionsExplicit()
    {
        var combinedText = string.Join(
            " ",
            CrossWindowGenerationConsumptionRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.Family,
                    entry.GenerationScope,
                    entry.ConsumptionScope,
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.LifetimeAndRestrictionAnchor,
                    entry.RemainingOfficialBreadth
                }.Concat(entry.DocAnchors)));

        foreach (var requiredPhrase in new[]
        {
            "generation",
            "creation",
            "consumption",
            "payment-only",
            "restriction",
            "expiry",
            "cleanup",
            "typed",
            "generic",
            "pending",
            "duplicate",
            "cross-window",
            "ledger",
            "no-mutation"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineCrossWindowGenerationConsumptionRowsDoNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            CrossWindowGenerationConsumptionRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.Family,
                    entry.Classification,
                    entry.OfficialMatrixRowId,
                    entry.GenerationScope,
                    entry.ConsumptionScope,
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.LifetimeAndRestrictionAnchor,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineCardMatrixAlignmentManifestListsRequiredFamiliesExactlyOnce()
    {
        var requiredFamilies = new[]
        {
            "MATRIX_ID_AND_STATUS_FIELDS",
            "PAYMENT_ROW_TO_CARD_MATRIX_MAPPING",
            "REPRESENTATIVE_TEST_EVIDENCE_LINKS",
            "FULL_OFFICIAL_GATE_AND_COMPLETION_BLOCK",
            "FAQ_RULE_SOURCE_TRACE",
            "FRONTEND_CONTRACT_AND_SNAPSHOT_TRACE",
            "MATRIX_JSON_SYNC_AND_DRIFT_GUARD",
            "DEFERRED_BLOCKER_AND_STATUS_COUNTS"
        };

        Assert.Equal(
            requiredFamilies.Order(StringComparer.Ordinal),
            CardMatrixAlignmentRowManifest.Select(entry => entry.Family).Order(StringComparer.Ordinal));
        Assert.Empty(CardMatrixAlignmentRowManifest
            .GroupBy(entry => entry.Family, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineCardMatrixAlignmentManifestRequiresPromptCommandAuditMatrixAndDocAnchors()
    {
        Assert.All(CardMatrixAlignmentRowManifest, entry =>
        {
            Assert.Equal(CardMatrixRepresentative, entry.Classification);
            Assert.Equal("ROW_CARD_MATRIX_ALIGNMENT_MISSING", entry.OfficialMatrixRowId);
            Assert.False(string.IsNullOrWhiteSpace(entry.MatrixScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("prompt", entry.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit", entry.AuditAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("matrix", entry.MatrixAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("remain open", entry.RemainingOfficialBreadth, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineCardMatrixAlignmentRowsStayLinkedToOfficialMatrixMissingRow()
    {
        var missingRow = Assert.Single(
            OfficialPaymentEngineMatrixSeedRowManifest,
            entry => string.Equals(entry.RowId, "ROW_CARD_MATRIX_ALIGNMENT_MISSING", StringComparison.Ordinal));
        var residualAxis = Assert.Single(
            OfficialPaymentEngineMatrixResidualManifest,
            entry => string.Equals(entry.Axis, "CARD_MATRIX_ALIGNMENT", StringComparison.Ordinal));

        Assert.Equal(MissingOfficialRow, missingRow.RowStatus);
        Assert.Equal(RemainingOfficialGap, residualAxis.Classification);
        Assert.Equal(missingRow.RowId, CardMatrixAlignmentRowManifest.Select(entry => entry.OfficialMatrixRowId).Distinct(StringComparer.Ordinal).Single());
        Assert.All(CardMatrixAlignmentRowManifest, entry => Assert.Contains("Card matrix representative only", entry.ClosureStatus, StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineCardMatrixAlignmentRowsKeepMatrixDimensionsExplicit()
    {
        var combinedText = string.Join(
            " ",
            CardMatrixAlignmentRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.Family,
                    entry.MatrixScope,
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.MatrixAnchor,
                    entry.RemainingOfficialBreadth
                }.Concat(entry.DocAnchors)));

        foreach (var requiredPhrase in new[]
        {
            "cardId",
            "collectorId",
            "oracleId",
            "effectId",
            "fullOfficial",
            "prompt",
            "command",
            "audit",
            "matrix",
            "FAQ",
            "ActionPrompt",
            "snapshot",
            "frontend",
            "blocker",
            "JSON",
            "official"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineCardMatrixAlignmentRowsDoNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            CardMatrixAlignmentRowManifest.SelectMany(entry =>
                new[]
                {
                    entry.Family,
                    entry.Classification,
                    entry.OfficialMatrixRowId,
                    entry.MatrixScope,
                    entry.RepresentativeSurface,
                    entry.PromptAnchor,
                    entry.CommandAnchor,
                    entry.AuditAnchor,
                    entry.MatrixAnchor,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void PaymentEngineCoverageManifestDocAnchorsResolveToCurrentAuditDocs()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var missingAnchors = GetPaymentEngineCoverageDocAnchors()
            .Distinct(StringComparer.Ordinal)
            .Where(anchor => !File.Exists(Path.Combine(repositoryRoot, anchor)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missingAnchors);
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

    private static IEnumerable<string> GetPaymentEngineCoverageDocAnchors()
    {
        return CoverageManifest.SelectMany(entry => entry.DocAnchors)
            .Concat(SpellshieldTaxCoverageManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(TargetColoredActivatedAbilityCoverageManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(ResourceSkillCoverageManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(ResidualBlockerManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(OfficialPaymentEngineMatrixResidualManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(OfficialPaymentEngineMatrixSeedRowManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(RollbackFailureRowManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(CrossWindowGenerationConsumptionRowManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(CardMatrixAlignmentRowManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(LegendBattlefieldTriggerResourceActionManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(KeywordPaymentBranchManifest.SelectMany(entry => entry.DocAnchors));
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing Riftbound.slnx.");
    }

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

    private sealed record PaymentEngineOfficialMatrixSeedRowCoverageEntry(
        string RowId,
        string Axis,
        string RowStatus,
        string ActionWindow,
        string PaymentOrPolicyProfile,
        string RepresentativeScope,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string RollbackExpectation,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineRollbackFailureRowCoverageEntry(
        string FailureFamily,
        string Classification,
        string OfficialMatrixRowId,
        string PaymentWindowScope,
        string RepresentativeSurface,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string NoMutationAnchor,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineCrossWindowGenerationConsumptionRowCoverageEntry(
        string Family,
        string Classification,
        string OfficialMatrixRowId,
        string GenerationScope,
        string ConsumptionScope,
        string RepresentativeSurface,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string LifetimeAndRestrictionAnchor,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineCardMatrixAlignmentRowCoverageEntry(
        string Family,
        string Classification,
        string OfficialMatrixRowId,
        string MatrixScope,
        string RepresentativeSurface,
        string PromptAnchor,
        string CommandAnchor,
        string AuditAnchor,
        string MatrixAnchor,
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
