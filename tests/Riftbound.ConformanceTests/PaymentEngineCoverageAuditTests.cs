using System.IO;
using System.Text.Json;
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
    private const string RollbackFailureAllWindowMatrix = "rollback-failure-all-window-matrix";
    private const string CrossWindowRepresentative = "cross-window-representative";
    private const string CrossWindowAllWindowMatrix = "cross-window-all-window-matrix";
    private const string CardMatrixRepresentative = "card-matrix-representative";
    private const string CardMatrixAllWindowMatrix = "card-matrix-all-window-matrix";
    private const string OfficialMatrixDownstreamAggregate = "official-matrix-downstream-aggregate";
    private const string KeywordBranchAllWindowMatrix = "keyword-branch-all-window-matrix";
    private const string ResourceSkillAllWindowMatrix = "resource-skill-all-window-matrix";
    private const string TargetTaxActivatedAbilityMatrix = "target-tax-activated-ability-matrix";
    private const string RemainingOfficialClosureGate = "remaining-official-closure-gate";
    private const string ImplementedResourceSkillOfficialCandidate = "implemented-resource-skill-official-candidate";
    private const string DeferredResourceSkillOfficialCandidate = "deferred-resource-skill-official-candidate";
    private const string DeferredLegendResourceActionBridge = "deferred-legend-resource-action-bridge";
    private const string DeferredNonLegendResourceSkillRuntimeVerifier = "deferred-non-legend-resource-skill-runtime-verifier";
    private const string DeferredResourceSkillNextDispatchGate = "deferred-resource-skill-next-dispatch-gate";
    private const string DeferredNonLegendResourceSkillRuntimeLane = "deferred-non-legend-resource-skill-runtime-lane";
    private const string LegendResourceBridgeAggregateVerifier = "legend-resource-bridge-aggregate-verifier";
    private const string LegendResourceBridgeImplementationAcceptance = "legend-resource-bridge-implementation-acceptance";

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
            "Jhin movement-triggered mana plus payment-only power resource skill",
            [
                P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId
            ],
            "JhinMovementResourceSkillTests: prompt appears only after a server-captured Jhin movement trigger and exposes the required trigger context",
            "JhinMovementResourceSkillTests: command revalidates movement trigger identity, source state, movement context and generated-resource use",
            "JhinMovementResourceSkillTests: TRIGGER_RESOLVED, ABILITY_ACTIVATED, MANA_GAINED, POWER_GAINED, spend and cleanup audit assertions",
            "JhinMovementResourceSkillTests: wrong window, missing trigger, stale source / context, wrong resource use and handwritten command no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03CB_PAYMENT_ENGINE_JHIN_RESOURCE_SKILL_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03CA_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_LANES_AUDIT.md"
            ]),
        new(
            "Honeyfruit equipment reaction payment-only resource skill",
            [
                P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId
            ],
            "HoneyfruitResourceSkillTests: prompt exposes ready base-equipment Honeyfruit reaction source, base generated power, and level-six upgraded branch eligibility",
            "HoneyfruitResourceSkillTests: command revalidates source identity, equipment readiness, reaction timing, level-six legality and generated-resource request shape",
            "HoneyfruitResourceSkillTests: ABILITY_ACTIVATED, UNIT_EXHAUSTED, POWER_GAINED, MANA_GAINED, spend and cleanup audit assertions",
            "HoneyfruitResourceSkillTests: wrong timing, exhausted / stale / non-Honeyfruit source, illegal upgraded branch, duplicate spend and unsupported generated amount no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03CP_PAYMENT_ENGINE_HONEYFRUIT_RESOURCE_SKILL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03CP_PAYMENT_ENGINE_HONEYFRUIT_RESOURCE_SKILL_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03CC_PAYMENT_ENGINE_HONEYFRUIT_RESOURCE_SKILL_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03CA_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_LANES_AUDIT.md"
            ]),
        new(
            "Blue Sentinel held-battlefield delayed next-main payment-only resource skill",
            [
                P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId
            ],
            "BlueSentinelResourceSkillTests: prompt appears only from a server-captured held-battlefield delayed trigger in the next-main PAY_COST context",
            "BlueSentinelResourceSkillTests: command materializes payment-only generated power and revalidates trigger identity, source, battlefield, timing and generated-resource use",
            "BlueSentinelResourceSkillTests: TRIGGER_QUEUED, TRIGGER_RESOLVED, ABILITY_ACTIVATED, POWER_GAINED, spend and cleanup audit assertions",
            "BlueSentinelResourceSkillTests: wrong main phase, late next-main window, missing trigger, stale source / battlefield, duplicate spend, non-rune payment and forged temp no-mutation guards",
            "Representative coverage only; project remains NOT READY and P0-005 remains open for full-official resource skill breadth.",
            [
                "docs/CURRENT_STAGE4D_03CQ_PAYMENT_ENGINE_BLUE_SENTINEL_RESOURCE_SKILL_AUDIT.md",
                "docs/CURRENT_STAGE4D_03CQ_PAYMENT_ENGINE_BLUE_SENTINEL_RESOURCE_SKILL_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03CD_PAYMENT_ENGINE_BLUE_SENTINEL_RESOURCE_SKILL_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03CA_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_LANES_AUDIT.md"
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
            "4D-03CQ binds the current 22 catalog `IsResourceSkill=true` representatives to prompt, command, ABILITY_ACTIVATED audit, and rollback anchors.",
            "Malzahar, Dragon Soul Sage, Jhin movement-triggered resource skill, Honeyfruit equipment reaction resource skill, Blue Sentinel delayed next-main resource skill, SFD / OGN Sigils, resource conversion equipment, and Gold token resource skills are catalog-bound representatives.",
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
                "docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md"
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
                "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md"
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
                "docs/CURRENT_COMPLETION_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md"
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

    private static readonly RollbackFailureMatrixActionWindowProfile[] RollbackFailureAllWindowActionWindowProfiles =
    [
        new(
            "PLAY_CARD",
            "mana, generic power, typed power, recycle resource and temporary payment resource",
            "PLAY_CARD prompt quote: legal hand-card payment sources, optional costs and typed resource candidates remain visible before rejection.",
            "PLAY_CARD command rejection: stale or illegal play payment commands reject before the card leaves hand or enters play.",
            "PLAY_CARD no-mutation assertion: hand, board, resource ledgers, stack and audit tail remain unchanged.",
            "PLAY_CARD audit expectation: no committed COST_PAID, PLAY_CARD_COMMITTED, RESOURCE_RECYCLED or TEMPORARY_PAYMENT_RESOURCE_SPENT success audit is emitted.",
            "docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md"),
        new(
            "PAY_COST",
            "pending payment mana, generic power, recycle resource and temporary payment resource",
            "PAY_COST prompt quote: pending payment metadata exposes the exact payable resources before rejection.",
            "PAY_COST command rejection: stale, duplicate or illegal pending payment commands reject before payment-window close.",
            "PAY_COST no-mutation assertion: pending queues, resource ledgers, hand, board, stack, score and audit tail remain unchanged.",
            "PAY_COST audit expectation: no committed COST_PAID, PAYMENT_WINDOW_CLOSED, RESOURCE_RECYCLED or temporary-resource spend success audit is emitted.",
            "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md"),
        new(
            "ACTIVATE_ABILITY",
            "mana, typed power, experience, target tax and generated payment resource",
            "ACTIVATE_ABILITY prompt quote: legal ability source, target, target tax and payment-resource candidates remain visible before rejection.",
            "ACTIVATE_ABILITY command rejection: stale source, illegal target or invalid payment commands reject before stack creation.",
            "ACTIVATE_ABILITY no-mutation assertion: source readiness, targets, attachments, ledgers, stack and audit tail remain unchanged.",
            "ACTIVATE_ABILITY audit expectation: no committed COST_PAID, ABILITY_ACTIVATED, target effect or attachment success audit is emitted.",
            "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md"),
        new(
            "ASSEMBLE_EQUIPMENT",
            "typed equipment cost, recycle resource and temporary payment resource",
            "ASSEMBLE_EQUIPMENT prompt quote: equipment assembly payment resources and attach candidates remain visible before rejection.",
            "ASSEMBLE_EQUIPMENT command rejection: invalid equipment payment or attachment commands reject before equipment moves or attaches.",
            "ASSEMBLE_EQUIPMENT no-mutation assertion: hand, board, attachments, ledgers, stack and audit tail remain unchanged.",
            "ASSEMBLE_EQUIPMENT audit expectation: no committed COST_PAID, EQUIPMENT_ATTACHED, RESOURCE_RECYCLED or temporary-resource spend success audit is emitted.",
            "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md"),
        new(
            "TRIGGER_PAYMENT",
            "pending trigger payment typed power, recycle resource and generated or temporary payment resource",
            "TRIGGER_PAYMENT prompt quote: trigger payment prompt exposes accepted, declined and payable resource candidates before rejection.",
            "TRIGGER_PAYMENT command rejection: stale source, target, decline or illegal payment commands reject before trigger resolution changes state.",
            "TRIGGER_PAYMENT no-mutation assertion: trigger queues, pending payment, ledgers, stack, score and audit tail remain unchanged.",
            "TRIGGER_PAYMENT audit expectation: no committed COST_PAID, TRIGGER_PAYMENT_DECLINED, BATTLEFIELD_TRIGGER_RESOLVED or domain success audit is emitted.",
            "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"),
        new(
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "power score payment, typed power, recycle resource and temporary payment resource",
            "BATTLEFIELD_HELD_SCORE_PAYMENT prompt quote: battlefield-held payment prompt exposes score cost and legal payment resources before rejection.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT command rejection: stale, prevented, already-scored or illegal payment commands reject before score state changes.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT no-mutation assertion: score, battlefield state, ledgers, pending queues and audit tail remain unchanged.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT audit expectation: no committed COST_PAID, BATTLEFIELD_HELD, SCORE_GAINED or prevented-result success audit is emitted.",
            "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md")
    ];

    private static readonly RollbackFailureMatrixFailureProfile[] RollbackFailureAllWindowFailureProfiles =
    [
        new(
            "STALE_PROMPT_PENDING_PAYMENT",
            "stale prompt, stale pending payment, duplicate command or closed payment window",
            "pending payment id, payment window token and stale prompt correlation",
            "stale prompt quote: the verifier keeps the original prompt metadata distinct from the later rejected command.",
            "stale command rejection: command-side payment id and pending window are revalidated before any commit.",
            "stale no-mutation: duplicate or closed-window submissions preserve every mutable zone and ledger.",
            "stale audit expectation: rejected stale submissions emit no committed payment or domain success audit.",
            "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"),
        new(
            "INVALID_PAYMENT_SOURCE_OR_TRAIT",
            "invalid resource id, wrong trait, unnecessary source, duplicate source or payment-only misuse",
            "resource id, trait, generated-resource restriction and source uniqueness",
            "invalid-source prompt quote: the legal resource list is the reference for wrong-id and wrong-trait rejection.",
            "invalid-source command rejection: command-side resource ids, traits and payment-only flags are revalidated.",
            "invalid-source no-mutation: rejected source choices preserve ordinary pools, generated ledgers and paid-resource history.",
            "invalid-source audit expectation: rejected source choices emit no COST_PAID, POWER_SPENT or resource-spend success audit.",
            "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md"),
        new(
            "INSUFFICIENT_COST_OR_TARGET_TAX",
            "insufficient mana, power, experience, modified cost or target tax",
            "mana, typed or generic power, experience and Spellshield target-tax totals",
            "insufficient-cost prompt quote: quoted base cost, modified cost and target-tax metadata stay visible for rejection.",
            "insufficient-cost command rejection: command-side totals are revalidated against current cost and tax requirements.",
            "insufficient-cost no-mutation: rejected shortfalls preserve source readiness, targets, stack, score and ledgers.",
            "insufficient-cost audit expectation: rejected shortfalls emit no COST_PAID, ABILITY_ACTIVATED, play or score success audit.",
            "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"),
        new(
            "STALE_SOURCE_TARGET_OR_OPTION",
            "stale source, stale target, invalid target, stale option or target-scoped option mismatch",
            "source object id, target object id and target-scoped option token",
            "stale-target prompt quote: legal source, target and target-scoped option metadata remain the rejection reference.",
            "stale-target command rejection: command-side source, target and option ids are revalidated before commit.",
            "stale-target no-mutation: rejected stale references preserve source state, target state, attachments and stack.",
            "stale-target audit expectation: rejected stale references emit no COST_PAID, ABILITY_ACTIVATED, EQUIPMENT_REATTACHED or target-effect success audit.",
            "docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md"),
        new(
            "OPTIONAL_EXTRA_ALTERNATIVE_BRANCH",
            "optional, extra, alternative, declined, duplicate or malformed branch",
            "chosen option token, branch id, declined state and alternative payment source",
            "optional-branch prompt quote: accepted, declined, extra and alternative choices stay visible for rejection.",
            "optional-branch command rejection: command-side branch ids, accepted state and alternative resources are revalidated.",
            "optional-branch no-mutation: rejected malformed branches preserve hand, board, attachments, resources and stack.",
            "optional-branch audit expectation: rejected branches emit no COST_PAID, branch success, hide, play or equipment success audit.",
            "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md"),
        new(
            "REPLACEMENT_PREVENTION_NO_EFFECT",
            "replacement, prevention, no-effect, already-resolved action or replacement-order mismatch",
            "replacement source, prevented effect, score state and resource-action branch",
            "replacement prompt quote: replacement or prevention context remains tied to the payment prompt before rejection.",
            "replacement command rejection: command-side replacement source, prevention state and action result are revalidated.",
            "replacement no-mutation: rejected or no-effect rows preserve score, battlefield state, ledgers, stack and audit tail.",
            "replacement audit expectation: rejected rows emit no SCORE_GAINED, BATTLEFIELD_HELD, COST_PAID or domain success audit unless the representative explicitly proves prevention.",
            "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md"),
        new(
            "GENERATED_RESOURCE_LIFETIME_REUSE",
            "expired generated resource, wrong consumption window, duplicate spend, stale id or cross-window reuse",
            "generated resource id, payment-only lifetime, spend id, cleanup id and consumption window",
            "generated-resource prompt quote: generated or temporary resource candidates remain tied to lifetime metadata before rejection.",
            "generated-resource command rejection: command-side lifetime, spend id and payment-only window are revalidated before spend.",
            "generated-resource no-mutation: rejected reuse preserves generated ledgers, pending queues, hand, board and audit tail.",
            "generated-resource audit expectation: rejected reuse emits no TEMPORARY_PAYMENT_RESOURCE_SPENT, COST_PAID, POWER_GAINED reuse or cleanup success audit.",
            "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md")
    ];

    private static readonly PaymentEngineRollbackFailureAllWindowMatrixEntry[] RollbackFailureAllWindowMatrixManifest =
        BuildRollbackFailureAllWindowMatrix();

    private static PaymentEngineRollbackFailureAllWindowMatrixEntry[] BuildRollbackFailureAllWindowMatrix()
    {
        return RollbackFailureAllWindowActionWindowProfiles
            .SelectMany(window => RollbackFailureAllWindowFailureProfiles.Select(failure =>
                new PaymentEngineRollbackFailureAllWindowMatrixEntry(
                    $"ROW_ROLLBACK_FAILURE_MATRIX_{window.ActionWindow}_{failure.FailureFamily}",
                    failure.FailureFamily,
                    RollbackFailureAllWindowMatrix,
                    "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
                    window.ActionWindow,
                    failure.FailureDimension,
                    $"{window.PaymentSourceKind}; {failure.PaymentSourceKind}",
                    $"{window.PromptQuote} {failure.PromptQuote}",
                    $"{window.CommandRejection} {failure.CommandRejection}",
                    $"{window.NoMutationAssertion} {failure.NoMutationAssertion}",
                    $"{window.AuditExpectation} {failure.AuditExpectation}",
                    $"Full official rollback matrix combinations for {window.ActionWindow} / {failure.FailureFamily} remain open across all card rows, source mixes, stale commands and illegal payment combinations.",
                    "All-window rollback matrix representative only; project remains NOT READY and P0-005 remains open.",
                    [
                        "docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md",
                        "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                        failure.DocAnchor,
                        window.DocAnchor
                    ])))
            .ToArray();
    }

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

    private static readonly CrossWindowGenerationConsumptionMatrixActionWindowProfile[] CrossWindowGenerationConsumptionAllWindowActionWindowProfiles =
    [
        new(
            "PLAY_CARD",
            "resource skill or temporary payment resource generation before a legal play-card quote",
            "PLAY_CARD inline payment consumption window",
            "mana, generic power, typed power, recycle resource and temporary payment-only lifetime",
            "PLAY_CARD prompt quote: legal hand-card payment sources expose generated-resource candidates and payment-only restrictions.",
            "PLAY_CARD command commit/rejection anchor: command-side play payment either spends the generated resource once or rejects stale, wrong-window or duplicate ids before the card leaves hand.",
            "PLAY_CARD audit expectation: COST_PAID, RESOURCE_RECYCLED, TEMPORARY_PAYMENT_RESOURCE_SPENT and cleanup audit entries stay correlated by payment id.",
            "PLAY_CARD lifetime / no-mutation / restriction assertion: rejected generated-resource misuse preserves hand, board, ledgers, stack and audit tail.",
            "docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md"),
        new(
            "PAY_COST",
            "pending payment generation before PAY_COST quote reuse",
            "PAY_COST pending payment consumption, decline or close window",
            "pending payment id, temporary payment-only resource, cleanup id and stale lifetime",
            "PAY_COST prompt quote: pending payment metadata exposes generated or temporary resource candidates with lifetime and restriction data.",
            "PAY_COST command commit/rejection anchor: accepted PayCost spends or clears the generated resource, while stale pending payment and duplicate spend reject before close.",
            "PAY_COST audit expectation: COST_PAID, PAYMENT_WINDOW_CLOSED, TEMPORARY_PAYMENT_RESOURCE_SPENT and TEMPORARY_PAYMENT_RESOURCE_CLEARED entries remain correlated.",
            "PAY_COST lifetime / no-mutation / restriction assertion: rejected stale or wrong-window resources preserve pending queues, ledgers, hand, board, stack and audit tail.",
            "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md"),
        new(
            "ACTIVATE_ABILITY",
            "resource skill or temporary-resource generation before an activated ability quote",
            "ACTIVATE_ABILITY payment consumption and stack-creation window",
            "mana, typed power, experience, target tax and generated payment-only lifetime",
            "ACTIVATE_ABILITY prompt quote: legal source, target, tax and generated-resource candidates stay visible before command submit.",
            "ACTIVATE_ABILITY command commit/rejection anchor: command-side ability payment spends legal generated resources once or rejects stale source, target or resource ids before stack creation.",
            "ACTIVATE_ABILITY audit expectation: COST_PAID, ABILITY_ACTIVATED, POWER_GAINED and temporary-resource audit entries stay bound to the payment or source id.",
            "ACTIVATE_ABILITY lifetime / no-mutation / restriction assertion: rejected generated-resource misuse preserves source readiness, targets, attachments, ledgers, stack and audit tail.",
            "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md"),
        new(
            "ASSEMBLE_EQUIPMENT",
            "generated or temporary equipment-payment resource before assembly quote",
            "ASSEMBLE_EQUIPMENT inline equipment payment and attach window",
            "typed equipment cost, conversion resource, temporary payment-only resource and attachment lifetime",
            "ASSEMBLE_EQUIPMENT prompt quote: equipment payment choices expose generated-resource candidates, attach target and payment-only restrictions.",
            "ASSEMBLE_EQUIPMENT command commit/rejection anchor: assembly payment spends legal generated resources or rejects stale, wrong-trait and duplicate generated ids before equipment moves.",
            "ASSEMBLE_EQUIPMENT audit expectation: COST_PAID, EQUIPMENT_ATTACHED, RESOURCE_RECYCLED and temporary-resource audit entries stay correlated.",
            "ASSEMBLE_EQUIPMENT lifetime / no-mutation / restriction assertion: rejected generated-resource misuse preserves hand, board, attachments, ledgers, stack and audit tail.",
            "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md"),
        new(
            "TRIGGER_PAYMENT",
            "generated or temporary trigger-payment resource before trigger prompt",
            "TRIGGER_PAYMENT pending payment, pay, decline and close windows",
            "trigger pending id, typed power, temporary payment-only resource, cleanup id and declined lifetime",
            "TRIGGER_PAYMENT prompt quote: trigger payment prompt exposes pay, decline and generated-resource candidates with lifetime metadata.",
            "TRIGGER_PAYMENT command commit/rejection anchor: accepted trigger payment spends or clears the generated resource, while stale source, target, decline or duplicate ids reject before resolution.",
            "TRIGGER_PAYMENT audit expectation: COST_PAID, TRIGGER_PAYMENT_DECLINED, BATTLEFIELD_TRIGGER_RESOLVED, PAYMENT_WINDOW_CLOSED and cleanup audit entries stay correlated.",
            "TRIGGER_PAYMENT lifetime / no-mutation / restriction assertion: rejected generated-resource misuse preserves trigger queues, pending payment, ledgers, stack, score and audit tail.",
            "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"),
        new(
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "generated or temporary score-payment resource before battlefield-held prompt",
            "BATTLEFIELD_HELD_SCORE_PAYMENT score payment and no-effect window",
            "score payment power, typed power, temporary payment-only resource, prevention state and cleanup lifetime",
            "BATTLEFIELD_HELD_SCORE_PAYMENT prompt quote: battlefield-held prompt exposes score cost, generated-resource candidates and payment-only restrictions.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT command commit/rejection anchor: held-score payment spends legal generated resources or rejects stale, prevented, already-scored or duplicate ids before score mutation.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT audit expectation: COST_PAID, BATTLEFIELD_HELD, SCORE_GAINED, temporary-resource spend and cleanup audit entries stay correlated.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT lifetime / no-mutation / restriction assertion: rejected generated-resource misuse preserves score, battlefield state, ledgers, pending queues and audit tail.",
            "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md")
    ];

    private static readonly CrossWindowGenerationConsumptionMatrixFamilyProfile[] CrossWindowGenerationConsumptionAllWindowFamilyProfiles =
    [
        new(
            "RESOURCE_SKILL_GENERATION_WINDOWS",
            "generation windows that create payment-only resources before later consumption",
            "later legal payment windows that consume generated resources created by resource skills",
            "resource skill creation id, source timing, payment-only flag and generated ledger",
            "generation prompt quote: the verifier keeps source, timing, generated amount and payment-only metadata visible before creation.",
            "generation command commit/rejection anchor: resource-skill commands create a generated resource or reject illegal timing and stale source before ledger mutation.",
            "generation audit expectation: ABILITY_ACTIVATED, POWER_GAINED and TEMPORARY_PAYMENT_RESOURCE_CREATED audit entries identify the creation id.",
            "generation lifetime / no-mutation / restriction assertion: illegal generation preserves source state, targets, generated ledgers and audit tail.",
            "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md"),
        new(
            "INLINE_PAYMENT_CONSUMPTION_WINDOWS",
            "inline consumption windows that spend generated resources during immediate payment",
            "PLAY_CARD, ACTIVATE_ABILITY and ASSEMBLE_EQUIPMENT inline consumption windows",
            "single-use spend id, inline payment id, temporary ledger and same-transaction lifetime",
            "inline consumption prompt quote: legal generated-resource candidates stay attached to the quoted inline payment.",
            "inline consumption command commit/rejection anchor: command-side inline payment spends the generated resource once or rejects stale and duplicate ids before commit.",
            "inline consumption audit expectation: COST_PAID, TEMPORARY_PAYMENT_RESOURCE_SPENT and cleanup audit entries identify the payment id and spend id.",
            "inline consumption lifetime / no-mutation / restriction assertion: rejected inline misuse preserves ordinary pools, generated ledgers, hand, board and audit tail.",
            "docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md"),
        new(
            "PENDING_PAYMENT_REUSE_AND_CLOSE",
            "pending payment reuse, decline, close and stale command windows",
            "PAY_COST and TRIGGER_PAYMENT pending consumption, decline and close windows",
            "pending payment id, payment-window token, decline state, cleanup id and stale lifetime",
            "pending prompt quote: generated-resource candidates remain tied to pending payment metadata before pay or decline.",
            "pending command commit/rejection anchor: PayCost or trigger commands spend, clear, decline or reject stale generated resources before payment-window close.",
            "pending audit expectation: COST_PAID, PAYMENT_WINDOW_CLOSED, TRIGGER_PAYMENT_DECLINED and temporary-resource cleanup audit entries stay correlated.",
            "pending lifetime / no-mutation / restriction assertion: duplicate, stale or closed-window submissions preserve ledgers, queues, hand, board and audit tail.",
            "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"),
        new(
            "TYPED_GENERIC_CONVERSION_AND_MATCHING",
            "typed, generic and conversion matching across generated-resource windows",
            "typed, generic and conversion payment consumption windows",
            "trait, generic amount, conversion direction, source mix and generated-resource id",
            "typed/generic prompt quote: legal trait, generic and conversion candidates stay visible for matching.",
            "typed/generic command commit/rejection anchor: command-side payment spends only matching generated resources and rejects wrong trait, wrong conversion or unnecessary generated source.",
            "typed/generic audit expectation: POWER_GAINED, TEMPORARY_PAYMENT_RESOURCE_CREATED, COST_PAID and RESOURCE_RECYCLED audit entries preserve resource identity.",
            "typed/generic lifetime / no-mutation / restriction assertion: rejected matching failures preserve ordinary pools, generated ledgers and paid-resource history.",
            "docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md"),
        new(
            "EXPIRY_CLEANUP_AND_TURN_BOUNDARY",
            "expiry, cleanup and turn-boundary windows after generated-resource creation or spend",
            "post-cleanup and turn-boundary rejection windows for expired generated resources",
            "cleanup id, turn boundary, stack-resolution boundary, payment close and expired generated-resource id",
            "expiry prompt quote: prompts before and after cleanup distinguish spendable generated resources from expired candidates.",
            "expiry command commit/rejection anchor: command-side cleanup clears spent resources and rejects expired or stale generated ids before reuse.",
            "expiry audit expectation: TEMPORARY_PAYMENT_RESOURCE_CLEARED, PAYMENT_WINDOW_CLOSED and absence of COST_PAID for expired ids remain provable.",
            "expiry lifetime / no-mutation / restriction assertion: stale turn-boundary reuse cannot leak spendable state and rejected reuse preserves all mutable zones.",
            "docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md"),
        new(
            "PAYMENT_ONLY_RESTRICTIONS_AND_WRONG_WINDOW",
            "payment-only restriction, ordinary-pool misuse and wrong consumption windows",
            "ordinary-resource, wrong-window and unsupported-command consumption attempts",
            "payment-only flag, ordinary pool boundary, wrong-window command and unsupported generated source",
            "restriction prompt quote: generated payment-only restrictions remain explicit beside ordinary resource candidates.",
            "restriction command commit/rejection anchor: command-side payment rejects ordinary-pool misuse, wrong-window consumption and unsupported generated sources before commit.",
            "restriction audit expectation: rejected restriction bypass emits no committed COST_PAID or domain success audit.",
            "restriction lifetime / no-mutation / restriction assertion: payment-only misuse preserves generated ledgers, ordinary pools, hand, board and audit tail.",
            "docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md"),
        new(
            "DUPLICATE_SPEND_AND_AUDIT_CORRELATION",
            "duplicate spend and creation/spend/cleanup audit-correlation windows",
            "duplicate-spend, stale-id and mismatched audit-correlation consumption attempts",
            "creation id, spend id, cleanup id, payment id and stale generated-resource id",
            "audit-correlation prompt quote: generated-resource id candidates remain visible before the command submits.",
            "audit-correlation command commit/rejection anchor: command-side payment rejects duplicate spends, stale ids and mismatched correlation before ledger mutation.",
            "audit-correlation audit expectation: TEMPORARY_PAYMENT_RESOURCE_CREATED, TEMPORARY_PAYMENT_RESOURCE_SPENT, TEMPORARY_PAYMENT_RESOURCE_CLEARED and COST_PAID entries correlate by id.",
            "audit-correlation lifetime / no-mutation / restriction assertion: rejected duplicate spend preserves ledgers, pending queues, hand, board and audit tail without orphaned entries.",
            "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md")
    ];

    private static readonly PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixEntry[] CrossWindowGenerationConsumptionAllWindowMatrixManifest =
        BuildCrossWindowGenerationConsumptionAllWindowMatrix();

    private static PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixEntry[] BuildCrossWindowGenerationConsumptionAllWindowMatrix()
    {
        return CrossWindowGenerationConsumptionAllWindowActionWindowProfiles
            .SelectMany(window => CrossWindowGenerationConsumptionAllWindowFamilyProfiles.Select(family =>
                new PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixEntry(
                    $"ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_{window.ActionWindow}_{family.Family}",
                    family.Family,
                    CrossWindowAllWindowMatrix,
                    "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
                    window.ActionWindow,
                    $"{window.GenerationScope}; {family.GenerationScope}",
                    $"{window.ConsumptionScope}; {family.ConsumptionScope}",
                    $"{window.ResourceLifetimeDimension}; {family.ResourceLifetimeDimension}",
                    $"{window.PromptQuote} {family.PromptQuote}",
                    $"{window.CommandCommitOrRejectionAnchor} {family.CommandCommitOrRejectionAnchor}",
                    $"{window.AuditExpectation} {family.AuditExpectation}",
                    $"{window.LifetimeNoMutationRestrictionAssertion} {family.LifetimeNoMutationRestrictionAssertion}",
                    $"Full official cross-window generation / consumption matrix combinations for {window.ActionWindow} / {family.Family} remain open across every official card row, generated-resource source, lifetime, restriction, cleanup order and invalid reuse branch.",
                    "All-window cross-window generation / consumption matrix representative only; project remains NOT READY and P0-005 remains open.",
                    [
                        "docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md",
                        "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                        family.DocAnchor,
                        window.DocAnchor
                    ])))
            .ToArray();
    }

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

    private static readonly CardMatrixAlignmentMatrixActionWindowProfile[] CardMatrixAlignmentAllWindowActionWindowProfiles =
    [
        new(
            "PLAY_CARD",
            "PLAY_CARD official card/effect row alignment across hand-card payment rows",
            "play-card prompt and command surface",
            "PLAY_CARD prompt evidence anchor: ActionPrompt payment quote exposes cardId, collectorId, oracleId, effectId, cost, resources and generated-resource candidates.",
            "PLAY_CARD command evidence anchor: command-side play payment binds the submitted card/effect row to accepted or rejected payment paths before the card leaves hand.",
            "PLAY_CARD audit evidence anchor: COST_PAID, RESOURCE_RECYCLED, TEMPORARY_PAYMENT_RESOURCE_SPENT and play-card success/rejection audit entries stay traceable to the card matrix row.",
            "PLAY_CARD matrix sync/status anchor: JSON row status, representative evidence status, blockers and fullOfficial=false remain synchronized with 03BG/03BN docs.",
            "PLAY_CARD frontend/snapshot trace: ActionPrompt and authoritative snapshot expose server-owned hand, cost, resource and card identity fields so frontend does not infer legal payments locally.",
            "docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md"),
        new(
            "PAY_COST",
            "PAY_COST official pending-payment row alignment across pending card/effect payment rows",
            "pending payment prompt and PayCost command surface",
            "PAY_COST prompt evidence anchor: pending ActionPrompt metadata exposes paymentId, source card/effect row, payment choices, cleanup lifetime and candidate resources.",
            "PAY_COST command evidence anchor: PayCost command revalidates the same payment row, accepts legal resources and rejects stale or duplicate submissions before queue mutation.",
            "PAY_COST audit evidence anchor: COST_PAID, PAYMENT_WINDOW_CLOSED and temporary-resource cleanup audit entries remain traceable to the originating card/effect row.",
            "PAY_COST matrix sync/status anchor: pending-payment matrix row evidence, blocker status and fullOfficial=false remain synchronized with 03BG/03BN docs.",
            "PAY_COST frontend/snapshot trace: authoritative snapshot and prompt metadata expose pending payment ownership, source identity and resource choices without frontend inference.",
            "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md"),
        new(
            "ACTIVATE_ABILITY",
            "ACTIVATE_ABILITY official card/effect row alignment across activated ability payment rows",
            "activated ability prompt, target and command surface",
            "ACTIVATE_ABILITY prompt evidence anchor: ActionPrompt exposes source card/effect id, ability id, target/tax metadata, typed resource candidates and optional branches.",
            "ACTIVATE_ABILITY command evidence anchor: command-side ability payment binds source, ability, target and resource choices to the same matrix row before stack creation.",
            "ACTIVATE_ABILITY audit evidence anchor: COST_PAID, ABILITY_ACTIVATED, target-tax and optional branch audit entries stay traceable to the source card/effect row.",
            "ACTIVATE_ABILITY matrix sync/status anchor: ability row blockers, FAQ review state, representative status and fullOfficial=false remain synchronized with 03BG/03BN docs.",
            "ACTIVATE_ABILITY frontend/snapshot trace: ActionPrompt and authoritative snapshot expose legal targets, costs and source state so frontend does not synthesize ability legality.",
            "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md"),
        new(
            "ASSEMBLE_EQUIPMENT",
            "ASSEMBLE_EQUIPMENT official equipment card/effect row alignment across assemble payment rows",
            "equipment assemble prompt and command surface",
            "ASSEMBLE_EQUIPMENT prompt evidence anchor: ActionPrompt exposes equipment card identity, attach target, typed cost, resource candidates and generated-resource restrictions.",
            "ASSEMBLE_EQUIPMENT command evidence anchor: assemble command revalidates equipment row, target and payment resources before equipment attach or rejection.",
            "ASSEMBLE_EQUIPMENT audit evidence anchor: COST_PAID, EQUIPMENT_ATTACHED, RESOURCE_RECYCLED and rejection audit expectations remain traceable to the equipment matrix row.",
            "ASSEMBLE_EQUIPMENT matrix sync/status anchor: equipment row status, blocker fields, representative evidence and fullOfficial=false remain synchronized with 03BG/03BN docs.",
            "ASSEMBLE_EQUIPMENT frontend/snapshot trace: authoritative snapshot exposes attachment state and server-owned assemble options so frontend does not infer legal attach/payment rows.",
            "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md"),
        new(
            "TRIGGER_PAYMENT",
            "TRIGGER_PAYMENT official trigger card/effect row alignment across pending trigger payment rows",
            "trigger payment prompt, pay/decline and close surface",
            "TRIGGER_PAYMENT prompt evidence anchor: trigger ActionPrompt exposes source/target card row, pay/decline branch, payment id and typed/generated resource candidates.",
            "TRIGGER_PAYMENT command evidence anchor: trigger payment commands revalidate the same card/effect row for pay, decline, stale source, stale target and close paths.",
            "TRIGGER_PAYMENT audit evidence anchor: COST_PAID, TRIGGER_PAYMENT_DECLINED, BATTLEFIELD_TRIGGER_RESOLVED and PAYMENT_WINDOW_CLOSED audit entries stay row-traceable.",
            "TRIGGER_PAYMENT matrix sync/status anchor: trigger row evidence, blocker fields, representative status and fullOfficial=false remain synchronized with 03BG/03BN docs.",
            "TRIGGER_PAYMENT frontend/snapshot trace: prompt metadata and authoritative snapshot expose trigger source, target, payment window and score state without frontend inference.",
            "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"),
        new(
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT official card/effect row alignment across held-score payment rows",
            "battlefield-held score payment prompt and command surface",
            "BATTLEFIELD_HELD_SCORE_PAYMENT prompt evidence anchor: held-score ActionPrompt exposes battlefield source, score cost, typed resources, prevention state and generated-resource candidates.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT command evidence anchor: held-score command revalidates the same card/effect row before score mutation, prevention, duplicate spend or no-effect rejection.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT audit evidence anchor: COST_PAID, BATTLEFIELD_HELD, SCORE_GAINED and prevented/no-effect audit expectations remain traceable to the card matrix row.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT matrix sync/status anchor: held-score row status, blocker counts, representative evidence and fullOfficial=false remain synchronized with 03BG/03BN docs.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT frontend/snapshot trace: authoritative snapshot exposes battlefield state, score-payment options and server-owned prevention results for frontend display.",
            "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md")
    ];

    private static readonly PaymentEngineCardMatrixAlignmentAllWindowMatrixEntry[] CardMatrixAlignmentAllWindowMatrixManifest =
        BuildCardMatrixAlignmentAllWindowMatrix();

    private static PaymentEngineCardMatrixAlignmentAllWindowMatrixEntry[] BuildCardMatrixAlignmentAllWindowMatrix()
    {
        return CardMatrixAlignmentAllWindowActionWindowProfiles
            .SelectMany(window => CardMatrixAlignmentRowManifest.Select(family =>
                new PaymentEngineCardMatrixAlignmentAllWindowMatrixEntry(
                    $"ROW_CARD_MATRIX_ALIGNMENT_MATRIX_{window.ActionWindow}_{family.Family}",
                    family.Family,
                    CardMatrixAllWindowMatrix,
                    "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
                    window.ActionWindow,
                    $"{window.MatrixScope}; {family.MatrixScope}",
                    $"{window.RepresentativeSurface}; {family.RepresentativeSurface}",
                    $"{window.PromptEvidenceAnchor} {family.PromptAnchor}",
                    $"{window.CommandEvidenceAnchor} {family.CommandAnchor}",
                    $"{window.AuditEvidenceAnchor} {family.AuditAnchor}",
                    $"{window.MatrixSyncStatusAnchor} {family.MatrixAnchor}",
                    $"{window.FrontendSnapshotOrRuleSourceTrace} {family.MatrixAnchor}",
                    $"Full official card matrix alignment combinations for {window.ActionWindow} / {family.Family} remain open across every official card row, collector/oracle variant, effect row, branch, FAQ/rule-source blocker and JSON status sync path.",
                    "All-window card matrix alignment representative only; project remains NOT READY and P0-005 remains open.",
                    [
                        "docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md",
                        "docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_EVIDENCE.md",
                        "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                        window.DocAnchor,
                        .. family.DocAnchors
                    ])))
            .ToArray();
    }

    private static readonly PaymentEngineOfficialMatrixDownstreamAggregateEntry[] PaymentEngineOfficialMatrixDownstreamAggregateManifest =
    [
        new(
            "AGGREGATE_ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
            "ROLLBACK_FAILURE_BRANCHES",
            OfficialMatrixDownstreamAggregate,
            "RollbackFailureRowManifest",
            "RollbackFailureAllWindowMatrixManifest",
            RollbackFailureRepresentative,
            RollbackFailureAllWindowMatrix,
            7,
            42,
            "4D-03BC missing official rollback row bound to the 4D-03BE family manifest and 4D-03BL all-window rollback matrix.",
            "PLAY_CARD / PAY_COST / ACTIVATE_ABILITY / ASSEMBLE_EQUIPMENT / TRIGGER_PAYMENT / BATTLEFIELD_HELD_SCORE_PAYMENT",
            "MOVE_UNIT, HIDE_CARD, and LEGEND_ACT stay outside the current PaymentEngine payment surfaces aggregate.",
            "Full official rollback failure matrix breadth remains open across every card row, source mix, stale command and illegal payment combination.",
            "Downstream aggregate representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_EVIDENCE.md"
            ]),
        new(
            "AGGREGATE_ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
            "CROSS_WINDOW_GENERATION_CONSUMPTION",
            OfficialMatrixDownstreamAggregate,
            "CrossWindowGenerationConsumptionRowManifest",
            "CrossWindowGenerationConsumptionAllWindowMatrixManifest",
            CrossWindowRepresentative,
            CrossWindowAllWindowMatrix,
            7,
            42,
            "4D-03BC missing official cross-window row bound to the 4D-03BF family manifest and 4D-03BM all-window generation / consumption matrix.",
            "PLAY_CARD / PAY_COST / ACTIVATE_ABILITY / ASSEMBLE_EQUIPMENT / TRIGGER_PAYMENT / BATTLEFIELD_HELD_SCORE_PAYMENT",
            "MOVE_UNIT, HIDE_CARD, and LEGEND_ACT stay outside the current PaymentEngine payment surfaces aggregate.",
            "Full official cross-window generation / consumption breadth remains open across every generated-resource source, lifetime, restriction and invalid reuse branch.",
            "Downstream aggregate representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_EVIDENCE.md"
            ]),
        new(
            "AGGREGATE_ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
            "CARD_MATRIX_ALIGNMENT",
            OfficialMatrixDownstreamAggregate,
            "CardMatrixAlignmentRowManifest",
            "CardMatrixAlignmentAllWindowMatrixManifest",
            CardMatrixRepresentative,
            CardMatrixAllWindowMatrix,
            8,
            48,
            "4D-03BC missing official card matrix row bound to the 4D-03BG family manifest and 4D-03BN all-window card matrix alignment matrix.",
            "PLAY_CARD / PAY_COST / ACTIVATE_ABILITY / ASSEMBLE_EQUIPMENT / TRIGGER_PAYMENT / BATTLEFIELD_HELD_SCORE_PAYMENT",
            "MOVE_UNIT, HIDE_CARD, and LEGEND_ACT stay outside the current PaymentEngine payment surfaces aggregate.",
            "Full official card matrix alignment breadth remains open across every official card row, collector/oracle variant, effect row, branch, FAQ blocker and JSON status sync path.",
            "Downstream aggregate representative only; project remains NOT READY and P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_EVIDENCE.md"
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

    private static readonly KeywordPaymentBranchMatrixActionWindowProfile[] KeywordPaymentBranchAllWindowActionWindowProfiles =
    [
        new(
            "PLAY_CARD",
            "PLAY_CARD keyword payment branch surface across hand-card optional and additional costs",
            "play-card keyword branch prompt and command surface",
            "PLAY_CARD prompt quote binds branch id, base cost, optional / extra keyword choices and available rune / mana / temporary resources.",
            "PLAY_CARD command-side revalidation binds submitted branch choices to the same hand-card source before card movement or stack creation.",
            "COST_PAID audit expectation covers accepted play-card keyword branch resources, declined branches and no duplicate branch spend.",
            "PLAY_CARD rollback expectation covers stale hand source, invalid branch id, insufficient modified cost and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md"),
        new(
            "PAY_COST",
            "PAY_COST keyword payment branch surface across pending payment windows",
            "pending keyword payment prompt and PayCost command surface",
            "PAY_COST prompt quote binds payment id, branch id, pending source and current legal resource candidates.",
            "PAY_COST command-side revalidation binds branch choices to the pending payment id before queue close or resource mutation.",
            "COST_PAID audit expectation covers accepted pending keyword payment resources and PAYMENT_WINDOW_CLOSED branch cleanup.",
            "PAY_COST rollback expectation covers stale pending id, duplicate branch spend, wrong resource and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md"),
        new(
            "ACTIVATE_ABILITY",
            "ACTIVATE_ABILITY keyword payment branch surface across target, tax and colored ability costs",
            "activated ability keyword branch prompt, target and command surface",
            "ACTIVATE_ABILITY prompt quote binds ability id, branch id, target/tax metadata and legal typed resource candidates.",
            "ACTIVATE_ABILITY command-side revalidation binds source, ability, targets and branch resources before stack creation.",
            "COST_PAID audit expectation covers accepted activated ability keyword branches, target tax and experience payment metadata.",
            "ACTIVATE_ABILITY rollback expectation covers stale source, stale target, invalid branch choice, insufficient tax and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md"),
        new(
            "ASSEMBLE_EQUIPMENT",
            "ASSEMBLE_EQUIPMENT keyword payment branch surface across equipment optional, extra and modifier costs",
            "equipment assemble keyword branch prompt and command surface",
            "ASSEMBLE_EQUIPMENT prompt quote binds equipment source, attach target, branch id, typed cost and modifier choices.",
            "ASSEMBLE_EQUIPMENT command-side revalidation binds equipment source, target and branch payment resources before attach or rejection.",
            "COST_PAID audit expectation covers accepted equipment keyword branch resources and equipment attach payment traceability.",
            "ASSEMBLE_EQUIPMENT rollback expectation covers stale equipment, invalid target, stale modifier, wrong resource and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md"),
        new(
            "TRIGGER_PAYMENT",
            "TRIGGER_PAYMENT keyword payment branch surface across optional trigger pay / decline windows",
            "trigger keyword payment prompt, pay/decline and close surface",
            "TRIGGER_PAYMENT prompt quote binds trigger source, branch id, pay/decline choice and current resource candidates.",
            "TRIGGER_PAYMENT command-side revalidation binds trigger source, target and branch payment resources before trigger resolution or decline.",
            "COST_PAID audit expectation covers accepted trigger keyword resources plus declined and closed trigger payment branches.",
            "TRIGGER_PAYMENT rollback expectation covers stale trigger source, stale target, wrong branch id, invalid resource and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"),
        new(
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT keyword payment branch surface across held-score replacement and prevention costs",
            "battlefield-held keyword branch score payment prompt and command surface",
            "BATTLEFIELD_HELD_SCORE_PAYMENT prompt quote binds battlefield source, branch id, replacement / prevention state and score-payment resources.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT command-side revalidation binds battlefield state and branch payment resources before score mutation or prevention.",
            "COST_PAID audit expectation covers accepted held-score keyword resources, prevention metadata and score-payment traceability.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT rollback expectation covers stale battlefield state, duplicate score, invalid replacement branch and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md")
    ];

    private static readonly PaymentEngineKeywordPaymentBranchAllWindowMatrixEntry[] KeywordPaymentBranchAllWindowMatrixManifest =
        BuildKeywordPaymentBranchAllWindowMatrix();

    private static PaymentEngineKeywordPaymentBranchAllWindowMatrixEntry[] BuildKeywordPaymentBranchAllWindowMatrix()
    {
        return KeywordPaymentBranchAllWindowActionWindowProfiles
            .SelectMany(window => KeywordPaymentBranchManifest.Select(branch =>
                new PaymentEngineKeywordPaymentBranchAllWindowMatrixEntry(
                    $"ROW_KEYWORD_BRANCH_MATRIX_{window.ActionWindow}_{branch.Branch}",
                    branch.Branch,
                    KeywordBranchAllWindowMatrix,
                    "KEYWORD_PAYMENT_BRANCHES",
                    "KEYWORD_BRANCHES / COST_MODIFIERS / OPTIONAL_EXTRA_ALTERNATIVE_COSTS / REPLACEMENT_PREVENTION",
                    window.ActionWindow,
                    window.PaymentSurfaceScope,
                    $"{window.RepresentativeSurface}; {branch.RepresentativeSurface}",
                    $"{window.PromptQuote} {branch.PromptAnchor}",
                    $"{window.CommandRevalidation} {branch.CommandAnchor}",
                    $"{window.AuditExpectation} {branch.AuditAnchor}",
                    $"{window.RollbackExpectation} {branch.RollbackAnchor}",
                    $"Full official keyword branch parity for {window.ActionWindow} / {branch.Branch} remains open across every official card row, target/tax branch, replacement/prevention ordering, cost modifier stack, optional/extra/alternative branch and temporary resource lifetime.",
                    "All-window keyword branch representative only; project remains NOT READY and P0-005 remains open.",
                    [
                        "docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_AUDIT.md",
                        "docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_EVIDENCE.md",
                        "docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_HANDOFF.md",
                        "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                        window.DocAnchor,
                        .. branch.DocAnchors
                    ])))
            .ToArray();
    }

    private static readonly ResourceSkillMatrixActionWindowProfile[] ResourceSkillAllWindowActionWindowProfiles =
    [
        new(
            "PLAY_CARD",
            "PLAY_CARD resource-skill generated payment surface across hand-card payment windows",
            "hand-card play prompt and command surface",
            "generated payment-only resource lifetime for play-card costs",
            "PLAY_CARD prompt quote binds generated resource candidates, payment-only restrictions, trait amount and source ability metadata.",
            "PLAY_CARD command-side revalidation binds the hand-card source, selected generated resource id and payment-only lifetime before card movement or stack creation.",
            "COST_PAID plus generated-resource audit expectation covers accepted play-card spend of resource-skill outputs and source paymentId correlation.",
            "payment-only generated resources must remain restricted to payment consumption and must not become durable mana or power state.",
            "PLAY_CARD rollback expectation covers stale hand source, expired generated resource, wrong trait, duplicate spend and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md"),
        new(
            "PAY_COST",
            "PAY_COST resource-skill generated payment surface across pending payment windows",
            "pending PayCost prompt and command surface",
            "pending generated resource lifetime through payment-window close",
            "PAY_COST prompt quote binds pending payment id, generated resource candidates, payment-only restrictions and source ability metadata.",
            "PAY_COST command-side revalidation binds the pending payment id, selected generated resource id and current lifetime before window close.",
            "COST_PAID / PAYMENT_WINDOW_CLOSED audit expectation covers accepted pending spend of resource-skill outputs and generated-resource cleanup.",
            "payment-only generated resources must be consumed only by the matching pending payment window or rejected without state mutation.",
            "PAY_COST rollback expectation covers stale pending id, expired generated resource, wrong window, duplicate spend and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md"),
        new(
            "ACTIVATE_ABILITY",
            "ACTIVATE_ABILITY resource-skill generation and payment surface",
            "activated ability source prompt, resource-skill command and generated payment surface",
            "resource skill generation lifetime from activation through legal payment consumption",
            "ACTIVATE_ABILITY prompt quote binds resource-skill source, generated resource kind, legal timing, targets and payment-only metadata.",
            "ACTIVATE_ABILITY command-side revalidation binds source, ability id, target/cost shape and generated resource amount before ability activation.",
            "ABILITY_ACTIVATED plus POWER_GAINED / MANA_GAINED audit expectation covers generated resource creation and payment-only ledger metadata.",
            "generated resource lifetime must remain visible enough for later payment windows while staying payment-only and non-bankable.",
            "ACTIVATE_ABILITY rollback expectation covers stale source, invalid timing, invalid target, duplicate source, wrong trait and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md"),
        new(
            "ASSEMBLE_EQUIPMENT",
            "ASSEMBLE_EQUIPMENT resource-skill generated payment surface across equipment payment windows",
            "equipment assemble prompt and command surface",
            "generated payment-only resource lifetime for equipment costs",
            "ASSEMBLE_EQUIPMENT prompt quote binds equipment source, attach target, generated resource candidates and typed payment restrictions.",
            "ASSEMBLE_EQUIPMENT command-side revalidation binds equipment source, target and generated resource id before attach or rejection.",
            "COST_PAID audit expectation covers accepted equipment spend of resource-skill outputs and equipment payment traceability.",
            "generated resource-skill payment-only outputs must satisfy equipment costs only when trait, amount, lifetime and restrictions match.",
            "ASSEMBLE_EQUIPMENT rollback expectation covers stale equipment, invalid target, expired generated resource, wrong trait and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md"),
        new(
            "TRIGGER_PAYMENT",
            "TRIGGER_PAYMENT resource-skill generated payment surface across pay / decline windows",
            "trigger payment prompt, pay/decline and close surface",
            "generated payment-only resource lifetime through trigger pay / decline resolution",
            "TRIGGER_PAYMENT prompt quote binds trigger source, target, pay/decline choice and generated resource candidates.",
            "TRIGGER_PAYMENT command-side revalidation binds trigger source, pending payment id, target and generated resource id before trigger resolution or decline.",
            "COST_PAID / TRIGGER_PAYMENT_DECLINED audit expectation covers accepted trigger spend, declined branch and generated-resource cleanup.",
            "generated resource-skill payment-only outputs must not leak across unrelated trigger windows or survive after the payment window closes.",
            "TRIGGER_PAYMENT rollback expectation covers stale trigger source, stale target, wrong generated resource, expired resource and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"),
        new(
            "BATTLEFIELD_HELD_SCORE_PAYMENT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT resource-skill generated payment surface across held-score windows",
            "battlefield-held score payment prompt and command surface",
            "generated payment-only resource lifetime for battlefield held score payments",
            "BATTLEFIELD_HELD_SCORE_PAYMENT prompt quote binds battlefield source, score payment, generated resource candidates and replacement/prevention state.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT command-side revalidation binds battlefield state, generated resource id and score payment amount before score mutation.",
            "COST_PAID / BATTLEFIELD_HELD audit expectation covers accepted held-score spend, generated-resource cleanup and score traceability.",
            "generated resource-skill payment-only outputs must satisfy held-score costs only inside their legal lifetime and must reject stale or duplicate spend.",
            "BATTLEFIELD_HELD_SCORE_PAYMENT rollback expectation covers stale battlefield state, duplicate score, expired generated resource and no-mutation command rejection.",
            "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md")
    ];

    private static readonly PaymentEngineResourceSkillAllWindowMatrixEntry[] ResourceSkillAllWindowMatrixManifest =
        BuildResourceSkillAllWindowMatrix();

    private static PaymentEngineResourceSkillAllWindowMatrixEntry[] BuildResourceSkillAllWindowMatrix()
    {
        return ResourceSkillAllWindowActionWindowProfiles
            .SelectMany(window => ResourceSkillCoverageManifest.Select(family =>
                new PaymentEngineResourceSkillAllWindowMatrixEntry(
                    $"ROW_RESOURCE_SKILL_MATRIX_{window.ActionWindow}_{family.Family}",
                    family.Family,
                    family.AbilityIds,
                    ResourceSkillAllWindowMatrix,
                    "RESOURCE_SKILL_A_C_FAMILY",
                    "RESOURCE_SKILLS",
                    window.ActionWindow,
                    window.PaymentSurfaceScope,
                    window.ResourceLifecycleScope,
                    $"{window.RepresentativeSurface}; {family.Family}",
                    $"{window.PromptQuote} {family.PromptAnchor}",
                    $"{window.CommandRevalidation} {family.CommandAnchor}",
                    $"{window.AuditExpectation} {family.AuditAnchor}",
                    $"{window.GeneratedResourceRestriction} {family.AuditAnchor}",
                    $"{window.RollbackExpectation} {family.RollbackAnchor}",
                    $"Full official [A] / [C] resource skill breadth for {family.Family} in {window.ActionWindow} remains open across generated-resource timing, payment-only restrictions, cross-window consumption, temporary resource lifetime, conversion ordering, Gold token bonus interactions and every no-mutation failure branch.",
                    "All-window resource skill representative only; project remains NOT READY and P0-005 remains open.",
                    [
                        "docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_AUDIT.md",
                        "docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_EVIDENCE.md",
                        "docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_HANDOFF.md",
                        "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                        window.DocAnchor,
                        .. family.DocAnchors
                    ])))
            .ToArray();
    }

    private static readonly string[] ResourceSkillOfficialBreadthDocAnchors =
    [
        "docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_AUDIT.md",
        "docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_HANDOFF.md",
        "docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_BASELINE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
        "docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT.md"
    ];

    private static readonly PaymentEngineResourceSkillOfficialBreadthEntry[] ResourceSkillOfficialBreadthManifest =
    [
        ImplementedResourceSkillOfficialBreadthEntry("UNL·T05", "Gold token generic payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("UNL-022/219", "Jhin movement-triggered mana plus power generated resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("UNL-049/219", "Honeyfruit reaction resource skill plus level-six upgraded mana plus power branch"),
        ImplementedResourceSkillOfficialBreadthEntry("UNL-087/219", "Blue Sentinel held-battlefield delayed next-main generated power branch"),
        ImplementedResourceSkillOfficialBreadthEntry("UNL-093/219", "Dragon Soul Sage reaction generated mana resource skill"),
        DeferredResourceSkillOfficialBreadthEntry("UNL-197/219", "Diana spell-duel-only generated mana restriction branch"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·083/221", "Hextech Anomaly generic power to mana conversion resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·117/221", "Ancient Stele mana to generic power conversion resource skill"),
        DeferredResourceSkillOfficialBreadthEntry("SFD·189/221", "Ornn Forge equipment-only generated power restriction branch"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·222/221", "SFD Rage Sigil typed red payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·226/221", "SFD Focus Sigil typed green payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·229/221", "SFD Insight Sigil typed blue payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·231/221", "SFD Power Sigil typed orange payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·234/221", "SFD Discord Sigil typed purple payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·238/221", "SFD Unity Sigil typed yellow payment-only resource skill"),
        DeferredResourceSkillOfficialBreadthEntry("SFD·244/221", "Ornn Forge reprint equipment-only generated power restriction branch"),
        ImplementedResourceSkillOfficialBreadthEntry("SFD·T03", "SFD Gold token generic payment-only resource skill"),
        DeferredResourceSkillOfficialBreadthEntry("OGS·014/024", "Lux spell-only generated mana restriction branch"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·040/298", "OGN Rage Sigil typed red payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·081/298", "OGN Focus Sigil typed green payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·098/298", "Energy Channel generated mana payment resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·113/298", "Malzahar target-as-cost generated double power resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·120/298", "OGN Insight Sigil typed blue payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·163/298", "OGN Power Sigil typed orange payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·204/298", "OGN Discord Sigil typed purple payment-only resource skill"),
        ImplementedResourceSkillOfficialBreadthEntry("OGN·245/298", "OGN Unity Sigil typed yellow payment-only resource skill"),
        DeferredResourceSkillOfficialBreadthEntry("OGN·247/298", "KaiSa spell-only generated power restriction branch"),
        DeferredResourceSkillOfficialBreadthEntry("OGN·253/298", "Darius Inspire-gated generated mana resource skill"),
        DeferredResourceSkillOfficialBreadthEntry("OGN·299/298", "KaiSa premium spell-only generated power restriction branch"),
        DeferredResourceSkillOfficialBreadthEntry("OGN·299*/298", "KaiSa premium alternate spell-only generated power restriction branch"),
        DeferredResourceSkillOfficialBreadthEntry("OGN·302/298", "Darius premium Inspire-gated generated mana resource skill"),
        DeferredResourceSkillOfficialBreadthEntry("OGN·302*/298", "Darius premium alternate Inspire-gated generated mana resource skill")
    ];

    private static PaymentEngineResourceSkillOfficialBreadthEntry ImplementedResourceSkillOfficialBreadthEntry(
        string cardNo,
        string officialResourceProfile)
    {
        return new(
            cardNo,
            ImplementedResourceSkillOfficialCandidate,
            "RESOURCE_SKILL_A_C_FAMILY",
            "RESOURCE_SKILLS",
            officialResourceProfile,
            "Current P4ActivatedAbilityCatalog marks this fixed official catalog source as IsResourceSkill=true and keeps prompt / command / ABILITY_ACTIVATED / generated-resource / rollback representative evidence.",
            "Future official closure must keep this source in the complete resource-skill breadth verifier and prove it across every official payment-only and generated-resource branch.",
            "Implemented representative only; project remains NOT READY and P0-005 remains open for full official [A] / [C] resource skill breadth.",
            ResourceSkillOfficialBreadthDocAnchors);
    }

    private static PaymentEngineResourceSkillOfficialBreadthEntry DeferredResourceSkillOfficialBreadthEntry(
        string cardNo,
        string officialResourceProfile)
    {
        return new(
            cardNo,
            DeferredResourceSkillOfficialCandidate,
            "RESOURCE_SKILL_A_C_FAMILY",
            "RESOURCE_SKILLS",
            officialResourceProfile,
            "Fixed official catalog resource-skill candidate is not currently represented by P4ActivatedAbilityCatalog IsResourceSkill=true.",
            "Future B-side verifier / implementation must prove prompt, command, audit, generated-resource lifetime, restriction text and no-mutation rollback behavior before this source can leave deferred status.",
            "Deferred official resource skill only; project remains NOT READY and P0-005 remains open for full official [A] / [C] resource skill breadth.",
            ResourceSkillOfficialBreadthDocAnchors);
    }

    private static readonly string[] DeferredResourceSkillFamilyDocAnchors =
    [
        "docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_AUDIT.md",
        "docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BV_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_HANDOFF.md",
        "docs/CURRENT_STAGE4D_03BV_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_BASELINE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_AUDIT.md",
        "docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_EVIDENCE.md"
    ];

    private static readonly PaymentEngineDeferredResourceSkillFamilyEntry[] DeferredResourceSkillFamilyManifest =
    [
        LegendDeferredResourceSkillFamilyEntry(
            "UNL-197/219",
            "Diana spell-duel-only legend resource action",
            "Diana spell-duel-only generated mana restriction branch",
            "Existing LEGEND_ACT representative tests prove Diana gains mana during spell-duel focus, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge LEGEND_ACT timing, generated mana restriction and resource-skill closure semantics under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "SFD·189/221",
            "Ornn equipment-only legend resource action",
            "Ornn Forge equipment-only generated power restriction branch",
            "Existing LEGEND_ACT representative tests prove Ornn gains power for pending equipment, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge LEGEND_ACT equipment-only restriction, generated power lifetime and resource-skill closure semantics under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "SFD·244/221",
            "Ornn reprint equipment-only legend resource action",
            "Ornn Forge reprint equipment-only generated power restriction branch",
            "Existing LEGEND_ACT definitions include the Ornn reprint, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge reprint parity, equipment-only restriction, generated power lifetime and resource-skill closure semantics under a fresh A dispatch."),
        NonLegendDeferredResourceSkillFamilyEntry(
            "OGS·014/024",
            "spell-only tap reaction resource skill",
            "Lux spell-only generated mana restriction branch",
            "Current code has Lux play / preflight evidence, but no P4 resource-skill prompt / command / audit implementation for the official spell-only tap reaction resource text.",
            "Future B must prove tap reaction timing, spell-only generated mana consumption, invalid non-spell use and no-mutation rollback under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "OGN·247/298",
            "KaiSa spell-only legend resource action",
            "KaiSa spell-only generated power restriction branch",
            "Existing LEGEND_ACT representative tests prove KaiSa gains power in a priority window for pending spell, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge pending-spell priority timing, spell-only generated power restriction and resource-skill closure semantics under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "OGN·253/298",
            "Darius Inspire-gated legend resource action",
            "Darius Inspire-gated generated mana resource skill",
            "Existing LEGEND_ACT representative tests prove Darius gains mana after another card this turn, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge Inspire gating, generated mana lifetime and resource-skill closure semantics under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "OGN·299/298",
            "KaiSa premium spell-only legend resource action",
            "KaiSa premium spell-only generated power restriction branch",
            "Existing LEGEND_ACT definitions include the KaiSa premium source, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge premium source parity, spell-only generated power restriction and resource-skill closure semantics under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "OGN·299*/298",
            "KaiSa premium alternate spell-only legend resource action",
            "KaiSa premium alternate spell-only generated power restriction branch",
            "Existing LEGEND_ACT definitions include the KaiSa premium alternate source, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge premium alternate source parity, spell-only generated power restriction and resource-skill closure semantics under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "OGN·302/298",
            "Darius premium Inspire-gated legend resource action",
            "Darius premium Inspire-gated generated mana resource skill",
            "Existing LEGEND_ACT definitions include the Darius premium source, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge premium source parity, Inspire gating, generated mana lifetime and resource-skill closure semantics under a fresh A dispatch."),
        LegendDeferredResourceSkillFamilyEntry(
            "OGN·302*/298",
            "Darius premium alternate Inspire-gated legend resource action",
            "Darius premium alternate Inspire-gated generated mana resource skill",
            "Existing LEGEND_ACT definitions include the Darius premium alternate source, but that evidence cannot close RESOURCE_SKILLS by proxy without an explicit resource-skill closure bridge.",
            "Future B must bridge premium alternate source parity, Inspire gating, generated mana lifetime and resource-skill closure semantics under a fresh A dispatch.")
    ];

    private static PaymentEngineDeferredResourceSkillFamilyEntry LegendDeferredResourceSkillFamilyEntry(
        string cardNo,
        string familySplit,
        string officialResourceProfile,
        string currentEvidenceStatus,
        string requiredFutureWork)
    {
        return new(
            cardNo,
            DeferredLegendResourceActionBridge,
            "RESOURCE_SKILL_A_C_FAMILY",
            "RESOURCE_SKILLS",
            "LEGEND_ACT",
            familySplit,
            officialResourceProfile,
            currentEvidenceStatus,
            requiredFutureWork,
            "Deferred legend resource action bridge only; project remains NOT READY and P0-005 remains open until RESOURCE_SKILLS closure is explicit.",
            DeferredResourceSkillFamilyDocAnchors);
    }

    private static PaymentEngineDeferredResourceSkillFamilyEntry NonLegendDeferredResourceSkillFamilyEntry(
        string cardNo,
        string familySplit,
        string officialResourceProfile,
        string currentEvidenceStatus,
        string requiredFutureWork)
    {
        return new(
            cardNo,
            DeferredNonLegendResourceSkillRuntimeVerifier,
            "RESOURCE_SKILL_A_C_FAMILY",
            "RESOURCE_SKILLS",
            "FUTURE_RESOURCE_SKILL_RUNTIME_OR_VERIFIER",
            familySplit,
            officialResourceProfile,
            currentEvidenceStatus,
            requiredFutureWork,
            "Deferred non-legend resource skill runtime / verifier only; project remains NOT READY and P0-005 remains open until RESOURCE_SKILLS closure is explicit.",
            DeferredResourceSkillFamilyDocAnchors);
    }

    private static readonly string[] DeferredResourceSkillNextDispatchGateDocAnchors =
    [
        "docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_AUDIT.md",
        "docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BY_PAYMENT_ENGINE_LEGEND_RESOURCE_ACTION_BRIDGE_HANDOFF.md",
        "docs/CURRENT_STAGE4D_03BY_PAYMENT_ENGINE_LEGEND_RESOURCE_ACTION_BRIDGE_BASELINE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BX_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_HANDOFF.md",
        "docs/CURRENT_STAGE4D_03BX_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_BASELINE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_AUDIT.md",
        "docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md"
    ];

    private static readonly PaymentEngineDeferredResourceSkillNextDispatchGateEntry[] DeferredResourceSkillNextDispatchGateManifest =
    [
        new(
            "B_DEFERRED_NON_LEGEND_RESOURCE_SKILL_RUNTIME",
            "B-side PaymentEngine non-legend deferred resource skill runtime / verifier slice",
            DeferredResourceSkillNextDispatchGate,
            DeferredNonLegendResourceSkillRuntimeVerifier,
            [
                "OGS·014/024"
            ],
            "Fresh A dispatch required before modifying runtime or tests for Lux.",
            "Primary future write scope is PaymentEngine / resource-skill focused tests and, only if a real mismatch is exposed, the minimal P4ActivatedAbilityCatalog / MatchSession / CoreRuleEngine / PaymentCostRules path needed for prompt, command, audit, generated-resource lifetime and rollback semantics.",
            "Legend bridge rows, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked.",
            "Future evidence must prove spell-only tap-reaction generated resource behavior without borrowing LEGEND_ACT evidence.",
            "4D-03BX handoff, 4D-03BW split, current preflight fixtures, backend full, Chrome smoke and formal 18 are routing / representative evidence only.",
            "Project remains NOT READY and P0-005 remains open until the non-legend runtime / verifier slice is explicitly dispatched, accepted and then reconciled with RESOURCE_SKILLS closure.",
            DeferredResourceSkillNextDispatchGateDocAnchors),
        new(
            "B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER",
            "B-side PaymentEngine legend resource action bridge / verifier slice",
            DeferredResourceSkillNextDispatchGate,
            DeferredLegendResourceActionBridge,
            [
                "OGN·247/298",
                "OGN·253/298",
                "OGN·299*/298",
                "OGN·299/298",
                "OGN·302*/298",
                "OGN·302/298",
                "SFD·189/221",
                "SFD·244/221",
                "UNL-197/219"
            ],
            "Fresh A dispatch required before treating Diana, Ornn, KaiSa or Darius LEGEND_ACT resource actions as RESOURCE_SKILLS bridge evidence.",
            "Primary future write scope is a bridge verifier in PaymentEngineCoverageAuditTests and focused legend tests; runtime / catalog files are allowed only if the verifier exposes a concrete mismatch and A opens that narrower lock.",
            "The 1 remaining non-legend 03BX runtime candidate, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked.",
            "Future evidence must bind each legend row to current LEGEND_ACT ability id, source-card group, timing restriction, generated resource type, payment-only restriction and explicit RESOURCE_SKILLS closure gap.",
            "Existing Diana / Ornn / KaiSa / Darius LEGEND_ACT representative tests, 4D-03BY handoff, backend full, Chrome smoke and formal 18 are bridge inputs only, not proxy closure.",
            "Project remains NOT READY and P0-005 remains open until legend bridge semantics are explicit and RESOURCE_SKILLS closure is separately accepted.",
            DeferredResourceSkillNextDispatchGateDocAnchors)
    ];

    private static readonly string[] LegendResourceBridgeAggregateDocAnchors =
    [
        "docs/CURRENT_STAGE4D_03CJ_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_AGGREGATE_AUDIT.md",
        "docs/CURRENT_STAGE4D_03CJ_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_AGGREGATE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BY_PAYMENT_ENGINE_LEGEND_RESOURCE_ACTION_BRIDGE_HANDOFF.md",
        "docs/CURRENT_STAGE4D_03BY_PAYMENT_ENGINE_LEGEND_RESOURCE_ACTION_BRIDGE_BASELINE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_AUDIT.md",
        "docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_EVIDENCE.md"
    ];

    private static readonly PaymentEngineLegendResourceBridgeAggregateEntry[] LegendResourceBridgeAggregateManifest =
    [
        LegendResourceBridgeAggregateEntry(
            "LEGEND_BRIDGE_DIANA_SPELL_DUEL_MANA",
            "Diana",
            "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA",
            ["UNL-197/219"],
            "spell-duel focus only",
            "generated 1 mana",
            "Existing P79LegendActDiana gains-mana and outside-focus rejection evidence is bridge input only.",
            "Future B must bind ability id LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA, source-card group, spell-duel timing, generated mana consumption / lifetime, no-mutation rollback and RESOURCE_SKILLS closure gap under a fresh A dispatch.",
            [
                "docs/CURRENT_STAGE4D_03CF_PAYMENT_ENGINE_DIANA_LEGEND_RESOURCE_BRIDGE_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03CF_PAYMENT_ENGINE_DIANA_LEGEND_RESOURCE_BRIDGE_BASELINE_EVIDENCE.md"
            ],
            "Ornn / KaiSa / Darius bridge rows, non-legend lanes, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked."),
        LegendResourceBridgeAggregateEntry(
            "LEGEND_BRIDGE_ORNN_EQUIPMENT_POWER",
            "Ornn",
            "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT",
            ["SFD·189/221", "SFD·244/221"],
            "priority window with pending equipment only",
            "generated 1 power for equipment / equipment skills",
            "Existing P79LegendActOrnn pending-equipment and source-group evidence is bridge input only.",
            "Future B must bind ability id LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT, source-card group, pending-equipment timing, equipment-only generated power consumption / lifetime, no-mutation rollback and RESOURCE_SKILLS closure gap under a fresh A dispatch.",
            [
                "docs/CURRENT_STAGE4D_03CG_PAYMENT_ENGINE_ORNN_LEGEND_RESOURCE_BRIDGE_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03CG_PAYMENT_ENGINE_ORNN_LEGEND_RESOURCE_BRIDGE_BASELINE_EVIDENCE.md"
            ],
            "Diana / KaiSa / Darius bridge rows, premium rows outside Ornn, non-legend lanes, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked."),
        LegendResourceBridgeAggregateEntry(
            "LEGEND_BRIDGE_KAISA_SPELL_POWER",
            "KaiSa",
            "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL",
            ["OGN·247/298", "OGN·299/298", "OGN·299*/298"],
            "priority window with pending spell only",
            "generated 1 power for spells",
            "Existing P79LegendActKaisa pending-spell and non-spell rejection evidence is bridge input only.",
            "Future B must bind ability id LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL, source-card group, pending-spell timing, spell-only generated power consumption / lifetime, no-mutation rollback and RESOURCE_SKILLS closure gap under a fresh A dispatch.",
            [
                "docs/CURRENT_STAGE4D_03CH_PAYMENT_ENGINE_KAISA_LEGEND_RESOURCE_BRIDGE_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03CH_PAYMENT_ENGINE_KAISA_LEGEND_RESOURCE_BRIDGE_BASELINE_EVIDENCE.md"
            ],
            "Diana / Ornn / Darius bridge rows, KaiSa unit HASTE_READY / conquest draw, non-legend lanes, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked."),
        LegendResourceBridgeAggregateEntry(
            "LEGEND_BRIDGE_DARIUS_INSPIRE_MANA",
            "Darius",
            "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA",
            ["OGN·253/298", "OGN·302/298", "OGN·302*/298"],
            "Inspire / another-card-played-this-turn gate",
            "generated 1 mana",
            "Existing P79LegendActDarius gain-after-prior-card and no-prior-card rejection evidence is bridge input only.",
            "Future B must bind ability id LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA, source-card group, Inspire timing, previous-card gate, generated mana consumption / lifetime, no-mutation rollback and RESOURCE_SKILLS closure gap under a fresh A dispatch.",
            [
                "docs/CURRENT_STAGE4D_03CI_PAYMENT_ENGINE_DARIUS_LEGEND_RESOURCE_BRIDGE_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03CI_PAYMENT_ENGINE_DARIUS_LEGEND_RESOURCE_BRIDGE_BASELINE_EVIDENCE.md"
            ],
            "Diana / Ornn / KaiSa bridge rows, Darius unit HASTE_READY / non-legend Darius or Draven work, non-legend lanes, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked.")
    ];

    private static PaymentEngineLegendResourceBridgeAggregateEntry LegendResourceBridgeAggregateEntry(
        string bridgeGroupId,
        string champion,
        string abilityId,
        string[] candidateCardNos,
        string timingRestriction,
        string generatedResourceProfile,
        string currentLegendEvidence,
        string requiredFutureEvidence,
        string[] requiredHandoffDocs,
        string forbiddenScope)
    {
        return new(
            bridgeGroupId,
            champion,
            abilityId,
            candidateCardNos,
            timingRestriction,
            generatedResourceProfile,
            currentLegendEvidence,
            requiredFutureEvidence,
            requiredHandoffDocs,
            forbiddenScope,
            "Aggregate legend bridge guard only; project remains NOT READY and P0-005 remains open. Current LEGEND_ACT evidence is proxy input, not RESOURCE_SKILLS closure.",
            [.. LegendResourceBridgeAggregateDocAnchors, .. requiredHandoffDocs]);
    }

    private static readonly string[] LegendResourceBridgeImplementationAcceptanceDocAnchors =
    [
        "docs/CURRENT_STAGE4D_03CL_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_ACCEPTANCE_VERIFIER_AUDIT.md",
        "docs/CURRENT_STAGE4D_03CL_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_ACCEPTANCE_VERIFIER_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03CK_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_IMPLEMENTATION_HANDOFF.md",
        "docs/CURRENT_STAGE4D_03CK_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_IMPLEMENTATION_BASELINE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03CJ_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_AGGREGATE_AUDIT.md",
        "docs/CURRENT_STAGE4D_03CJ_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_AGGREGATE_EVIDENCE.md"
    ];

    private static readonly PaymentEngineLegendResourceBridgeImplementationAcceptanceEntry[] LegendResourceBridgeImplementationAcceptanceManifest =
    [
        LegendResourceBridgeImplementationAcceptanceEntry(
            "LEGEND_BRIDGE_DIANA_SPELL_DUEL_MANA",
            "Diana",
            "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA",
            ["UNL-197/219"],
            "Server-filtered ActionPrompt must expose Diana only during the spell-duel focus timing window, with no frontend inference.",
            "Command handling must revalidate ability id, source object, source-card group and spell-duel timing before any resource mutation.",
            "Audit must expose generated-resource source, mana type, amount, payment-only lifetime, consumption and cleanup evidence.",
            "Wrong timing, stale source, exhausted source and handwritten illegal command must reject with no-mutation rollback.",
            "Single official source card still needs exact source-card parity / binding rather than a generic LEGEND_ACT proxy.",
            "Generated resource skills cannot be targeted as responses by other spells.",
            "Future 4D-03CL-B must add or cite focused Diana bridge coverage that proves prompt, command, audit, rollback and lifetime behavior.",
            "Ornn / KaiSa / Darius bridge rows, non-legend lanes, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked."),
        LegendResourceBridgeImplementationAcceptanceEntry(
            "LEGEND_BRIDGE_ORNN_EQUIPMENT_POWER",
            "Ornn",
            "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT",
            ["SFD·189/221", "SFD·244/221"],
            "Server-filtered ActionPrompt must expose Ornn only while a pending equipment / equipment-skill payment can consume generated power, with no frontend inference.",
            "Command handling must revalidate ability id, source object, source-card group, pending equipment item and equipment-only resource action before mutation.",
            "Audit must expose generated-resource source, equipment-power type, amount, payment-only lifetime, consumption and cleanup evidence.",
            "Wrong pending item, stale source, exhausted source and handwritten illegal command must reject with no-mutation rollback.",
            "Base / reprint source-card parity must be explicit for SFD·189/221 and SFD·244/221.",
            "Generated resource skills cannot be targeted as responses by other spells.",
            "Future 4D-03CL-B must add or cite focused Ornn bridge coverage that proves prompt, command, audit, rollback and lifetime behavior.",
            "Diana / KaiSa / Darius bridge rows, non-legend lanes, Ornn static-power / equipment-look work, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked."),
        LegendResourceBridgeImplementationAcceptanceEntry(
            "LEGEND_BRIDGE_KAISA_SPELL_POWER",
            "KaiSa",
            "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL",
            ["OGN·247/298", "OGN·299/298", "OGN·299*/298"],
            "Server-filtered ActionPrompt must expose KaiSa only while a pending spell payment can consume generated power, with no frontend inference.",
            "Command handling must revalidate ability id, source object, source-card group, pending spell item and spell-only resource action before mutation.",
            "Audit must expose generated-resource source, spell-power type, amount, payment-only lifetime, consumption and cleanup evidence.",
            "Wrong pending item, stale source, exhausted source and handwritten illegal command must reject with no-mutation rollback.",
            "Base / premium / alternate source-card parity must be explicit for OGN·247/298, OGN·299/298 and OGN·299*/298.",
            "Generated resource skills cannot be targeted as responses by other spells.",
            "Future 4D-03CL-B must add or cite focused KaiSa bridge coverage that proves prompt, command, audit, rollback and lifetime behavior.",
            "Diana / Ornn / Darius bridge rows, non-legend lanes, KaiSa unit HASTE_READY / conquest draw work, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked."),
        LegendResourceBridgeImplementationAcceptanceEntry(
            "LEGEND_BRIDGE_DARIUS_INSPIRE_MANA",
            "Darius",
            "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA",
            ["OGN·253/298", "OGN·302/298", "OGN·302*/298"],
            "Server-filtered ActionPrompt must expose Darius only when Inspire / previous-card-this-turn timing is satisfied, with no frontend inference.",
            "Command handling must revalidate ability id, source object, source-card group, previous-card-this-turn gate and resource action before mutation.",
            "Audit must expose generated-resource source, mana type, amount, payment-only lifetime, consumption and cleanup evidence.",
            "Missing previous card, stale source, exhausted source and handwritten illegal command must reject with no-mutation rollback.",
            "Base / premium / alternate source-card parity must be explicit for OGN·253/298, OGN·302/298 and OGN·302*/298.",
            "Generated resource skills cannot be targeted as responses by other spells.",
            "Future 4D-03CL-B must add or cite focused Darius bridge coverage that proves prompt, command, audit, rollback and lifetime behavior.",
            "Diana / Ornn / KaiSa bridge rows, non-legend lanes, Darius unit HASTE_READY / Darius or Draven non-legend work, frontend runtime, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked.")
    ];

    private static PaymentEngineLegendResourceBridgeImplementationAcceptanceEntry LegendResourceBridgeImplementationAcceptanceEntry(
        string bridgeGroupId,
        string champion,
        string abilityId,
        string[] candidateCardNos,
        string promptContract,
        string commandContract,
        string auditAndResourceContract,
        string rollbackContract,
        string sourceParityContract,
        string reminderBoundary,
        string requiredFutureTestAnchor,
        string lockedScope)
    {
        var aggregateEntry = LegendResourceBridgeAggregateManifest.Single(entry => string.Equals(entry.BridgeGroupId, bridgeGroupId, StringComparison.Ordinal));

        return new(
            bridgeGroupId,
            champion,
            abilityId,
            candidateCardNos,
            promptContract,
            commandContract,
            auditAndResourceContract,
            rollbackContract,
            sourceParityContract,
            reminderBoundary,
            requiredFutureTestAnchor,
            lockedScope,
            "Acceptance contract only; project remains NOT READY and P0-005 remains open until future B implementation / verifier evidence explicitly closes RESOURCE_SKILLS.",
            [.. LegendResourceBridgeImplementationAcceptanceDocAnchors, .. aggregateEntry.DocAnchors]);
    }

    private static readonly string[] DeferredNonLegendResourceSkillRuntimeLaneDocAnchors =
    [
        "docs/CURRENT_STAGE4D_03CA_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_LANES_AUDIT.md",
        "docs/CURRENT_STAGE4D_03CA_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_LANES_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_AUDIT.md",
        "docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BX_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_HANDOFF.md",
        "docs/CURRENT_STAGE4D_03BX_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_BASELINE_EVIDENCE.md",
        "docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_AUDIT.md",
        "docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md"
    ];

    private static readonly PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneEntry[] DeferredNonLegendResourceSkillRuntimeLaneManifest =
    [
        new(
            "LANE_LUX_SPELL_ONLY_TAP_REACTION_RESOURCE_SKILL",
            "OGS·014/024",
            "Lux spell-only tap reaction resource skill",
            "Current evidence is play / preflight only; no P4 resource-skill prompt / command / audit path exists for the official spell-only tap reaction resource text.",
            "Prompt must be server-filtered by tap reaction timing, ready source state and a pending spell payment that can legally consume generated mana.",
            "Command must revalidate tap source, pending spell identity, generated mana request and spell-only payment restriction before mutating state.",
            "Audit must connect ABILITY_ACTIVATED, generated mana availability, spend and cleanup to Lux and the spell-only restriction.",
            "Non-spell payment, wrong timing, exhausted source, stale source and handwritten command branches must be no-mutation.",
            "New focused Lux non-legend resource-skill tests or equivalent spell-only generated-resource verifier.",
            "LEGEND_ACT bridge rows, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and riftbound-dotnet.sln remain locked.",
            "Project remains NOT READY and P0-005 remains open until this lane is implemented, accepted and reconciled with RESOURCE_SKILLS closure.",
            DeferredNonLegendResourceSkillRuntimeLaneDocAnchors)
    ];

    private static readonly TargetTaxActivatedAbilityMatrixDimensionProfile[] TargetTaxActivatedAbilityMatrixDimensionProfiles =
    [
        new(
            "SOURCE_TIMING",
            "source timing and priority permission",
            "target profile remains tied to the source timing prompt",
            "payment profile must be quoted at the same source timing",
            "target-tax / optional branch choices remain tied to the legal source window",
            "prompt quote binds legal activated source timing, swift/open-main permission and current target choices.",
            "command-side revalidation binds the submitted ability id to the same legal source timing before stack creation.",
            "COST_PAID or ABILITY_ACTIVATED audit expectation ties payment id and activated source to the resolved timing window.",
            "rollback expectation covers wrong timing, stale source, exhausted source and no-mutation command rejection.",
            "official closure trace keeps source timing representative-only and card matrix status non-full-official.",
            "docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md"),
        new(
            "TARGET_PROFILE",
            "source timing must keep target legality fresh",
            "target profile, controller relation, target count and stale-target branch",
            "payment profile must not bypass target legality",
            "target-tax / optional branch choices must follow the accepted target profile",
            "prompt quote binds legal targets, target count, controller relation and target-tax metadata where present.",
            "command-side revalidation binds source, ability and target ids before any payment or stack mutation.",
            "COST_PAID or ABILITY_ACTIVATED audit expectation keeps target ids and payment id correlated.",
            "rollback expectation covers invalid target, stale target, target-count mismatch and no-mutation command rejection.",
            "official closure trace keeps dependency target choice and target-count breadth open.",
            "docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md"),
        new(
            "PAYMENT_PROFILE",
            "source timing must expose the same payment quote used by command validation",
            "target profile must not change the legal payment source set after quote",
            "payment profile, typed power, generic power, mana, experience and recycle / temporary parity",
            "target-tax / optional branch payment deltas remain part of the same quote",
            "prompt quote binds payment profile, typed/resource candidates, experience, tax and payment-only resource metadata.",
            "command-side revalidation binds submitted payment resources to the quoted ability payment profile.",
            "COST_PAID audit expectation records paid mana, power trait, experience, tax amount and shared payment id.",
            "rollback expectation covers wrong trait, insufficient payment, invalid resource, duplicate spend and no-mutation rejection.",
            "official closure trace keeps full payment-source breadth open for every target-bearing ability family.",
            "docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md"),
        new(
            "TARGET_TAX_OR_OPTIONAL_BRANCH",
            "source timing must keep target-tax / optional branch choices available only in legal windows",
            "target profile determines whether tax or target-scoped option is legal",
            "payment profile includes target tax, typed cost, experience or optional branch deltas",
            "target-tax, SpellshieldTaxCoverageManifest or target-scoped optional branch profile",
            "prompt quote binds Spellshield target tax, optional target-scoped choices and branch metadata without local inference.",
            "command-side revalidation binds tax / optional branch choices to the same target and ability before commit.",
            "COST_PAID or ABILITY_ACTIVATED audit expectation records target-tax or optional-branch metadata and payment id.",
            "rollback expectation covers insufficient target tax, stale optional branch, invalid reattach and no-mutation rejection.",
            "official closure trace keeps TARGET_TAXES plus optional / extra / alternative branch breadth open.",
            "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"),
        new(
            "COMMAND_ROLLBACK",
            "source timing rollback must leave authoritative state unchanged",
            "target rollback must leave targets, attachments, zones and status unchanged on rejected commands",
            "payment rollback must leave mana, power, experience and generated resources unchanged on rejection",
            "target-tax / optional branch rollback must not mutate tax target or optional equipment state",
            "prompt quote supplies enough payment and target metadata for rejected commands to be audited.",
            "command-side revalidation rejects stale source, stale target, wrong branch, wrong resource and duplicate submissions.",
            "COST_PAID or ABILITY_ACTIVATED audit expectation must be absent on rejected commands and present on accepted commands.",
            "no-mutation rollback expectation covers every command rejection branch in the target/tax matrix row.",
            "official closure trace keeps all stale / illegal target failure branches open until full official matrix closure.",
            "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md"),
        new(
            "OFFICIAL_CLOSURE_TRACE",
            "source timing evidence remains representative, not full official",
            "target profile evidence remains representative, not full official",
            "payment profile evidence remains representative, not full official",
            "target-tax / optional branch evidence remains representative, not full official",
            "prompt quote trace must point to current audit docs and service-authoritative ActionPrompt evidence.",
            "command-side revalidation trace must point to focused command tests and reject local frontend inference.",
            "COST_PAID or ABILITY_ACTIVATED audit trace must connect to current manifest evidence without upgrading fullOfficial.",
            "rollback trace must preserve P0-005 open and NOT READY closure.",
            "official closure / card-matrix trace keeps fullOfficial=false and completion audit NOT READY.",
            "docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md")
    ];

    private static readonly PaymentEngineTargetTaxActivatedAbilityMatrixEntry[] TargetTaxActivatedAbilityMatrixManifest =
        BuildTargetTaxActivatedAbilityMatrix();

    private static readonly PaymentEngineRemainingOfficialClosureGateEntry[] RemainingOfficialClosureGateManifest =
    [
        new(
            "B_PAYMENT_ENGINE_OFFICIAL_BREADTH",
            "B-side PaymentEngine official breadth verifier / implementation slice",
            RemainingOfficialClosureGate,
            "Fresh A dispatch and explicit runtime / test write lock required before any B work begins.",
            "Expand one remaining official PaymentEngine family into executable prompt / command / audit / rollback tests, or minimally fix a concrete mismatch found by those tests.",
            "4D-03BR-B target/tax matrix, focused 107/107, adjacent 665/665, backend full 4544/4544, Chrome smoke and formal 18 are representative proxy evidence only.",
            "Runtime, tests, frontend, browser scripts, card matrix JSON, fullOfficial status, final readiness status and riftbound-dotnet.sln remain locked until a fresh A dispatch.",
            "Project remains NOT READY and P0-005 remains open; fullOfficial upgrade is not allowed.",
            [
                "docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BS_PAYMENT_ENGINE_REMAINING_OFFICIAL_SCOPE_HANDOFF.md",
                "docs/CURRENT_STAGE4D_03BS_PAYMENT_ENGINE_REMAINING_OFFICIAL_SCOPE_BASELINE_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md"
            ]),
        new(
            "E_CARD_MATRIX_READINESS",
            "E-side card matrix readiness slice",
            RemainingOfficialClosureGate,
            "Fresh A dispatch and explicit matrix write window required before any E matrix write begins.",
            "Map accepted PaymentEngine representatives back to affected functional units and blockers while preserving fullOfficial=false until full official proof exists.",
            "Current 1009 snapshot entries / 811 functional units matrix and all-window representative matrices are not full-card official coverage.",
            "docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json remains locked; no fullOfficial or final-readiness upgrade may be inferred from representative rows.",
            "Project remains NOT READY and full-card matrix completion remains open; P0-005 remains open.",
            [
                "docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_EVIDENCE.md",
                "docs/CURRENT_STAGE4D_03BS_PAYMENT_ENGINE_REMAINING_OFFICIAL_SCOPE_HANDOFF.md",
                "docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md",
                "docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md"
            ]),
        new(
            "D_COMPLETION_P0_AUDIT",
            "D-side completion / P0 audit slice",
            RemainingOfficialClosureGate,
            "Fresh A dispatch required before D can claim any P0/P1 closure status change.",
            "Prove which residual P0-005 items remain after the current all-window matrices and identify which require runtime work rather than more representative evidence.",
            "Backend full, Chrome smoke, formal 18 and representative PaymentEngine manifests are acceptance evidence, not completion audit proof.",
            "Completion audit, server rule audit and closure plan must keep NOT READY until P0/P1, full-card matrix and final audit gates are all proven.",
            "Project remains NOT READY; update_goal complete is forbidden until final completion audit outputs a final readiness verdict.",
            [
                "docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_EVIDENCE.md",
                "docs/CURRENT_COMPLETION_AUDIT.md",
                "docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md",
                "docs/CURRENT_SERVER_RULE_AUDIT.md"
            ])
    ];

    private static PaymentEngineTargetTaxActivatedAbilityMatrixEntry[] BuildTargetTaxActivatedAbilityMatrix()
    {
        return TargetColoredActivatedAbilityCoverageManifest
            .SelectMany(ability => TargetTaxActivatedAbilityMatrixDimensionProfiles.Select(dimension =>
                new PaymentEngineTargetTaxActivatedAbilityMatrixEntry(
                    $"ROW_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_{ability.AbilityId}_{dimension.Dimension}",
                    ability.AbilityId,
                    TargetTaxActivatedAbilityMatrix,
                    "TARGET_BEARING_COLORED_ACTIVATED_ABILITIES",
                    "TARGET_TAXES / PAYMENT_SOURCES / OPTIONAL_EXTRA_ALTERNATIVE_COSTS / CARD_MATRIX_ALIGNMENT",
                    "ACTIVATE_ABILITY",
                    dimension.Dimension,
                    $"{dimension.SourceTimingScope}; {ability.RepresentativeSurface}",
                    $"{dimension.TargetProfileScope}; {ability.TargetProfile}",
                    $"{dimension.PaymentProfileScope}; {ability.PaymentProfile}",
                    $"{dimension.TargetTaxOrOptionalBranchProfile}; {BuildTargetTaxOrOptionalBranchTrace(ability)}",
                    $"{dimension.PromptQuote} {ability.PromptAnchor}",
                    $"{dimension.CommandRevalidation} {ability.CommandAnchor}",
                    $"{dimension.AuditExpectation} {ability.AuditAnchor}",
                    $"{dimension.RollbackExpectation} {ability.RollbackAnchor}",
                    $"{dimension.OfficialClosureTrace} Card matrix trace remains representative and fullOfficial=false for {ability.AbilityId}.",
                    ability.RemainingOfficialBreadth,
                    "Target/tax activated ability matrix representative only; project remains NOT READY and P0-005 remains open.",
                    [
                        "docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_AUDIT.md",
                        "docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_EVIDENCE.md",
                        "docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_HANDOFF.md",
                        "docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md",
                        dimension.DocAnchor,
                        .. ability.DocAnchors
                    ])))
            .ToArray();
    }

    private static string BuildTargetTaxOrOptionalBranchTrace(TargetColoredActivatedAbilityCoverageEntry ability)
    {
        if (ability.PaymentProfile.Contains("Spellshield", StringComparison.Ordinal))
        {
            return "SpellshieldTaxCoverageManifest / TARGET_TAXES target-tax prompt-command-audit anchor remains required.";
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, StringComparison.Ordinal))
        {
            return "Azir target-scoped optional armament reattach branch remains linked to optional / extra / alternative branch breadth.";
        }

        if (ability.TargetProfile.Contains("no target", StringComparison.OrdinalIgnoreCase))
        {
            return "No current target-tax branch for this no-target representative; TARGET_TAXES remains visible as an open official residual axis.";
        }

        return "Target-scoped activated branch has no current Spellshield tax but must preserve target legality, stale-target rollback and future tax breadth.";
    }

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
    public void PaymentEngineOfficialMatrixMissingRowsAllHaveDownstreamRepresentativeManifests()
    {
        var missingRowIds = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal))
            .Select(entry => entry.RowId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var downstreamRowIds = RollbackFailureRowManifest.Select(entry => entry.OfficialMatrixRowId)
            .Concat(CrossWindowGenerationConsumptionRowManifest.Select(entry => entry.OfficialMatrixRowId))
            .Concat(CardMatrixAlignmentRowManifest.Select(entry => entry.OfficialMatrixRowId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
                "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
                "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING"
            },
            missingRowIds);
        Assert.Equal(missingRowIds, downstreamRowIds);
        Assert.DoesNotContain(
            OfficialPaymentEngineMatrixSeedRowManifest.Where(entry => string.Equals(entry.RowStatus, PolicyDeferredRow, StringComparison.Ordinal)),
            entry => downstreamRowIds.Contains(entry.RowId, StringComparer.Ordinal));
    }

    [Fact]
    public void PaymentEngineOfficialMatrixMissingRowCoverageKeepsDownstreamFamiliesAndDocsVisible()
    {
        var aggregateAuditDoc = "docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md";
        var missingRows = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal))
            .ToDictionary(entry => entry.RowId, StringComparer.Ordinal);
        var downstreamCoverage = new[]
        {
            new
            {
                RowId = "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
                Axis = "ROLLBACK_FAILURE_BRANCHES",
                FamilyCount = RollbackFailureRowManifest.Length,
                ExpectedAuditDoc = "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                Families = RollbackFailureRowManifest.Select(entry => entry.FailureFamily),
                Docs = RollbackFailureRowManifest.SelectMany(entry => entry.DocAnchors)
            },
            new
            {
                RowId = "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
                Axis = "CROSS_WINDOW_GENERATION_CONSUMPTION",
                FamilyCount = CrossWindowGenerationConsumptionRowManifest.Length,
                ExpectedAuditDoc = "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                Families = CrossWindowGenerationConsumptionRowManifest.Select(entry => entry.Family),
                Docs = CrossWindowGenerationConsumptionRowManifest.SelectMany(entry => entry.DocAnchors)
            },
            new
            {
                RowId = "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
                Axis = "CARD_MATRIX_ALIGNMENT",
                FamilyCount = CardMatrixAlignmentRowManifest.Length,
                ExpectedAuditDoc = "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                Families = CardMatrixAlignmentRowManifest.Select(entry => entry.Family),
                Docs = CardMatrixAlignmentRowManifest.SelectMany(entry => entry.DocAnchors)
            }
        };

        Assert.Equal(3, missingRows.Count);
        Assert.All(downstreamCoverage, coverage =>
        {
            var row = missingRows[coverage.RowId];
            Assert.Equal(coverage.Axis, row.Axis);
            Assert.True(coverage.FamilyCount > 0, $"{coverage.RowId} must keep downstream representative families.");
            Assert.Equal(coverage.FamilyCount, coverage.Families.Distinct(StringComparer.Ordinal).Count());
            Assert.Contains(aggregateAuditDoc, row.DocAnchors);
            Assert.Contains(coverage.ExpectedAuditDoc, coverage.Docs);
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixMissingRowCoverageDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            OfficialPaymentEngineMatrixSeedRowManifest
                .Where(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal)
                    || string.Equals(entry.RowStatus, PolicyDeferredRow, StringComparison.Ordinal))
                .SelectMany(entry =>
                    new[]
                    {
                        entry.RowId,
                        entry.Axis,
                        entry.RowStatus,
                        entry.ActionWindow,
                        entry.RepresentativeScope,
                        entry.PromptAnchor,
                        entry.CommandAnchor,
                        entry.AuditAnchor,
                        entry.RollbackExpectation,
                        entry.RemainingOfficialBreadth,
                        entry.ClosureStatus
                    }.Concat(entry.DocAnchors))
                .Concat(RollbackFailureRowManifest.SelectMany(entry =>
                    new[]
                    {
                        entry.OfficialMatrixRowId,
                        entry.Classification,
                        entry.RemainingOfficialBreadth,
                        entry.ClosureStatus
                    }.Concat(entry.DocAnchors)))
                .Concat(CrossWindowGenerationConsumptionRowManifest.SelectMany(entry =>
                    new[]
                    {
                        entry.OfficialMatrixRowId,
                        entry.Classification,
                        entry.RemainingOfficialBreadth,
                        entry.ClosureStatus
                    }.Concat(entry.DocAnchors)))
                .Concat(CardMatrixAlignmentRowManifest.SelectMany(entry =>
                    new[]
                    {
                        entry.OfficialMatrixRowId,
                        entry.Classification,
                        entry.RemainingOfficialBreadth,
                        entry.ClosureStatus
                    }.Concat(entry.DocAnchors))));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.Contains("ROW_MOVE_UNIT_POLICY_DEFERRED", combinedText, StringComparison.Ordinal);
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
    public void PaymentEngineOfficialMatrixDownstreamAggregatePreservesOfficialRowSchemaCounts()
    {
        Assert.Equal(9, OfficialPaymentEngineMatrixSeedRowManifest.Count(entry => string.Equals(entry.RowStatus, RepresentativeSeed, StringComparison.Ordinal)));
        Assert.Equal(3, OfficialPaymentEngineMatrixSeedRowManifest.Count(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal)));
        Assert.Equal(1, OfficialPaymentEngineMatrixSeedRowManifest.Count(entry => string.Equals(entry.RowStatus, PolicyDeferredRow, StringComparison.Ordinal)));

        var missingRows = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal))
            .ToDictionary(entry => entry.RowId, StringComparer.Ordinal);

        Assert.Equal(missingRows.Count, PaymentEngineOfficialMatrixDownstreamAggregateManifest.Length);
        Assert.All(PaymentEngineOfficialMatrixDownstreamAggregateManifest, entry =>
        {
            var officialRow = missingRows[entry.OfficialMatrixRowId];

            Assert.StartsWith("AGGREGATE_ROW_", entry.AggregateRowId, StringComparison.Ordinal);
            Assert.Contains(entry.OfficialMatrixRowId.Replace("ROW_", string.Empty, StringComparison.Ordinal), entry.AggregateRowId, StringComparison.Ordinal);
            Assert.Equal(officialRow.Axis, entry.Axis);
            Assert.Equal(OfficialMatrixDownstreamAggregate, entry.Classification);
            Assert.Contains("4D-03BC", entry.AggregateScope, StringComparison.Ordinal);
            Assert.Contains("docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md", entry.DocAnchors);
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixDownstreamAggregateRequiresExactlyOneEntryPerMissingRow()
    {
        var missingRowIds = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal))
            .Select(entry => entry.RowId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var aggregateRowIds = PaymentEngineOfficialMatrixDownstreamAggregateManifest
            .Select(entry => entry.OfficialMatrixRowId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(missingRowIds, aggregateRowIds);
        Assert.Empty(PaymentEngineOfficialMatrixDownstreamAggregateManifest
            .GroupBy(entry => entry.OfficialMatrixRowId, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key));
        Assert.DoesNotContain(
            PaymentEngineOfficialMatrixDownstreamAggregateManifest,
            entry => string.Equals(entry.OfficialMatrixRowId, "ROW_MOVE_UNIT_POLICY_DEFERRED", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineOfficialMatrixDownstreamAggregateCountsMatchCurrentDownstreamMatrices()
    {
        var expectedCounts = new[]
        {
            new
            {
                RowId = "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
                FamilyManifestName = "RollbackFailureRowManifest",
                AllWindowManifestName = "RollbackFailureAllWindowMatrixManifest",
                FamilyClassification = RollbackFailureRepresentative,
                AllWindowClassification = RollbackFailureAllWindowMatrix,
                FamilyCount = 7,
                AllWindowMatrixCount = 42,
                ActualFamilyCount = RollbackFailureRowManifest.Length,
                ActualAllWindowMatrixCount = RollbackFailureAllWindowMatrixManifest.Length
            },
            new
            {
                RowId = "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
                FamilyManifestName = "CrossWindowGenerationConsumptionRowManifest",
                AllWindowManifestName = "CrossWindowGenerationConsumptionAllWindowMatrixManifest",
                FamilyClassification = CrossWindowRepresentative,
                AllWindowClassification = CrossWindowAllWindowMatrix,
                FamilyCount = 7,
                AllWindowMatrixCount = 42,
                ActualFamilyCount = CrossWindowGenerationConsumptionRowManifest.Length,
                ActualAllWindowMatrixCount = CrossWindowGenerationConsumptionAllWindowMatrixManifest.Length
            },
            new
            {
                RowId = "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
                FamilyManifestName = "CardMatrixAlignmentRowManifest",
                AllWindowManifestName = "CardMatrixAlignmentAllWindowMatrixManifest",
                FamilyClassification = CardMatrixRepresentative,
                AllWindowClassification = CardMatrixAllWindowMatrix,
                FamilyCount = 8,
                AllWindowMatrixCount = 48,
                ActualFamilyCount = CardMatrixAlignmentRowManifest.Length,
                ActualAllWindowMatrixCount = CardMatrixAlignmentAllWindowMatrixManifest.Length
            }
        };

        Assert.All(expectedCounts, expected =>
        {
            var aggregate = Assert.Single(
                PaymentEngineOfficialMatrixDownstreamAggregateManifest,
                entry => string.Equals(entry.OfficialMatrixRowId, expected.RowId, StringComparison.Ordinal));

            Assert.Equal(expected.FamilyManifestName, aggregate.FamilyManifestName);
            Assert.Equal(expected.AllWindowManifestName, aggregate.AllWindowManifestName);
            Assert.Equal(expected.FamilyClassification, aggregate.FamilyClassification);
            Assert.Equal(expected.AllWindowClassification, aggregate.AllWindowClassification);
            Assert.Equal(expected.FamilyCount, aggregate.FamilyCount);
            Assert.Equal(expected.AllWindowMatrixCount, aggregate.AllWindowMatrixCount);
            Assert.Equal(expected.ActualFamilyCount, aggregate.FamilyCount);
            Assert.Equal(expected.ActualAllWindowMatrixCount, aggregate.AllWindowMatrixCount);
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixDownstreamAggregateLinksEveryAllWindowRowBackToOfficialRow()
    {
        var aggregateRowIds = PaymentEngineOfficialMatrixDownstreamAggregateManifest
            .Select(entry => entry.OfficialMatrixRowId)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(RollbackFailureAllWindowMatrixManifest, entry =>
        {
            Assert.Equal("ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING", entry.OfficialMatrixRowId);
            Assert.Contains(entry.OfficialMatrixRowId, aggregateRowIds);
        });
        Assert.All(CrossWindowGenerationConsumptionAllWindowMatrixManifest, entry =>
        {
            Assert.Equal("ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING", entry.OfficialMatrixRowId);
            Assert.Contains(entry.OfficialMatrixRowId, aggregateRowIds);
        });
        Assert.All(CardMatrixAlignmentAllWindowMatrixManifest, entry =>
        {
            Assert.Equal("ROW_CARD_MATRIX_ALIGNMENT_MISSING", entry.OfficialMatrixRowId);
            Assert.Contains(entry.OfficialMatrixRowId, aggregateRowIds);
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixDownstreamAggregateDocAnchorsIncludeSourceAndAggregateDocs()
    {
        var expectedDocsByRow = new[]
        {
            new
            {
                RowId = "ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING",
                SourceDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_EVIDENCE.md",
                    "docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_EVIDENCE.md"
                }
            },
            new
            {
                RowId = "ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING",
                SourceDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_EVIDENCE.md",
                    "docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_EVIDENCE.md"
                }
            },
            new
            {
                RowId = "ROW_CARD_MATRIX_ALIGNMENT_MISSING",
                SourceDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_EVIDENCE.md",
                    "docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_EVIDENCE.md"
                }
            }
        };

        Assert.All(PaymentEngineOfficialMatrixDownstreamAggregateManifest, entry =>
        {
            var expected = Assert.Single(expectedDocsByRow, row => string.Equals(row.RowId, entry.OfficialMatrixRowId, StringComparison.Ordinal));

            Assert.Contains("docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_EVIDENCE.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md", entry.DocAnchors);
            Assert.All(expected.SourceDocs, expectedDoc => Assert.Contains(expectedDoc, entry.DocAnchors));
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixDownstreamAggregateKeepsNonPaymentSurfacesOut()
    {
        var excludedActionWindows = new[] { "MOVE_UNIT", "HIDE_CARD", "LEGEND_ACT" };
        var aggregateActionWindows = RollbackFailureAllWindowMatrixManifest.Select(entry => entry.ActionWindow)
            .Concat(CrossWindowGenerationConsumptionAllWindowMatrixManifest.Select(entry => entry.ActionWindow))
            .Concat(CardMatrixAlignmentAllWindowMatrixManifest.Select(entry => entry.ActionWindow))
            .ToArray();

        Assert.All(excludedActionWindows, excluded =>
        {
            Assert.DoesNotContain(excluded, aggregateActionWindows, StringComparer.Ordinal);
            Assert.All(PaymentEngineOfficialMatrixDownstreamAggregateManifest, entry =>
            {
                Assert.DoesNotContain(excluded, entry.PaymentSurfaceScope.Split(" / "), StringComparer.Ordinal);
                Assert.Contains(excluded, entry.ExcludedPaymentSurfaces, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixDownstreamAggregateDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            PaymentEngineOfficialMatrixDownstreamAggregateManifest.SelectMany(entry =>
                new[]
                {
                    entry.AggregateRowId,
                    entry.OfficialMatrixRowId,
                    entry.Axis,
                    entry.Classification,
                    entry.FamilyManifestName,
                    entry.AllWindowManifestName,
                    entry.FamilyClassification,
                    entry.AllWindowClassification,
                    entry.AggregateScope,
                    entry.PaymentSurfaceScope,
                    entry.ExcludedPaymentSurfaces,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineOfficialMatrixRepresentativeSeedRowsAllHaveUpstreamManifestAnchors()
    {
        var representativeRows = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, RepresentativeSeed, StringComparison.Ordinal))
            .ToDictionary(entry => entry.RowId, StringComparer.Ordinal);
        var upstreamCoverage = new[]
        {
            new
            {
                RowId = "ROW_ACTION_WINDOWS_PLAY_CARD_TYPED_RESOURCE_SEED",
                Axis = "ACTION_WINDOWS",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_PAYMENT_SOURCES_PAY_COST_TEMPORARY_SEED",
                Axis = "PAYMENT_SOURCES",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_RESOURCE_SKILLS_MALZAHAR_TARGET_AS_COST_SEED",
                Axis = "RESOURCE_SKILLS",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_TARGET_TAXES_XERATH_SPELLSHIELD_SEED",
                Axis = "TARGET_TAXES",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_KEYWORD_BRANCHES_HASTE_READY_SEED",
                Axis = "KEYWORD_BRANCHES",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_COST_MODIFIERS_BATTLEFIELD_EQUIPMENT_SEED",
                Axis = "COST_MODIFIERS",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_OPTIONAL_EXTRA_ALTERNATIVE_AZIR_REATTACH_SEED",
                Axis = "OPTIONAL_EXTRA_ALTERNATIVE_COSTS",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_REPLACEMENT_PREVENTION_BATTLEFIELD_HELD_SEED",
                Axis = "REPLACEMENT_PREVENTION",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md"
                }
            },
            new
            {
                RowId = "ROW_RESOURCE_ACTIONS_TRIGGER_PAYMENT_SEED",
                Axis = "RESOURCE_ACTIONS",
                ExpectedAuditDocs = new[]
                {
                    "docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md",
                    "docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md"
                }
            }
        };

        Assert.Equal(upstreamCoverage.Length, representativeRows.Count);
        Assert.Equal(
            upstreamCoverage.Select(coverage => coverage.RowId).Order(StringComparer.Ordinal),
            representativeRows.Keys.Order(StringComparer.Ordinal));
        Assert.All(upstreamCoverage, coverage =>
        {
            var row = representativeRows[coverage.RowId];

            Assert.Equal(coverage.Axis, row.Axis);
            Assert.All(coverage.ExpectedAuditDocs, expectedDoc => Assert.Contains(expectedDoc, row.DocAnchors));
            Assert.DoesNotContain(
                row.DocAnchors,
                anchor => string.Equals(
                    anchor,
                    "docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md",
                    StringComparison.Ordinal));
            Assert.DoesNotContain("Missing official row", row.PromptAnchor, StringComparison.Ordinal);
            Assert.DoesNotContain("Missing official row", row.CommandAnchor, StringComparison.Ordinal);
            Assert.DoesNotContain("Missing official row", row.AuditAnchor, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixRepresentativeSeedRowsKeepPromptCommandAuditAndRollbackEvidenceDistinct()
    {
        var representativeRows = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, RepresentativeSeed, StringComparison.Ordinal));

        Assert.All(representativeRows, row =>
        {
            Assert.Contains("prompt", row.PromptAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", row.CommandAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit", row.AuditAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-mutation", row.RollbackExpectation, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(row.PromptAnchor, row.CommandAnchor);
            Assert.NotEqual(row.CommandAnchor, row.AuditAnchor);
            Assert.NotEqual(row.AuditAnchor, row.RollbackExpectation);
            Assert.Contains("Full official", row.RemainingOfficialBreadth, StringComparison.Ordinal);
            Assert.Contains("Representative seed only", row.ClosureStatus, StringComparison.Ordinal);
            Assert.Equal(2, row.DocAnchors.Distinct(StringComparer.Ordinal).Count());
        });
    }

    [Fact]
    public void PaymentEngineOfficialMatrixRepresentativeSeedCoverageDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            OfficialPaymentEngineMatrixSeedRowManifest
                .Where(entry => string.Equals(entry.RowStatus, RepresentativeSeed, StringComparison.Ordinal))
                .SelectMany(entry =>
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
        Assert.DoesNotContain("missing-official-row", combinedText, StringComparison.Ordinal);
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
    public void PaymentEngineOfficialMatrixPolicyDeferredRowsStaySingleMoveUnitBoundary()
    {
        var policyRow = Assert.Single(
            OfficialPaymentEngineMatrixSeedRowManifest,
            entry => string.Equals(entry.RowStatus, PolicyDeferredRow, StringComparison.Ordinal));

        Assert.Equal("ROW_MOVE_UNIT_POLICY_DEFERRED", policyRow.RowId);
        Assert.Equal("ACTION_WINDOWS", policyRow.Axis);
        Assert.Equal("MOVE_UNIT", policyRow.ActionWindow);
        Assert.Equal("movement permission with optional-cost policy boundary", policyRow.PaymentOrPolicyProfile);
        Assert.Contains("not a rune, mana, experience or temporary-resource payment row", policyRow.RepresentativeScope, StringComparison.Ordinal);
        Assert.Contains("not a PaymentEngine payment prompt", policyRow.PromptAnchor, StringComparison.Ordinal);
        Assert.Contains("no payment command row is opened", policyRow.CommandAnchor, StringComparison.Ordinal);
        Assert.Contains("not COST_PAID audit", policyRow.AuditAnchor, StringComparison.Ordinal);
        Assert.Contains("future resource reclassification", policyRow.RollbackExpectation, StringComparison.Ordinal);
        Assert.Contains("Full official movement payment row remains deferred", policyRow.RemainingOfficialBreadth, StringComparison.Ordinal);
        Assert.Contains("Policy deferred row only", policyRow.ClosureStatus, StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                "docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md",
                "docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_AUDIT.md"
            },
            policyRow.DocAnchors);
    }

    [Fact]
    public void PaymentEngineOfficialMatrixPolicyDeferredMoveUnitStaysOutOfPaymentManifests()
    {
        const string policyRowId = "ROW_MOVE_UNIT_POLICY_DEFERRED";
        var representativeSeedRowIds = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, RepresentativeSeed, StringComparison.Ordinal))
            .Select(entry => entry.RowId);
        var missingOfficialRowIds = OfficialPaymentEngineMatrixSeedRowManifest
            .Where(entry => string.Equals(entry.RowStatus, MissingOfficialRow, StringComparison.Ordinal))
            .Select(entry => entry.RowId);
        var downstreamRowIds = RollbackFailureRowManifest.Select(entry => entry.OfficialMatrixRowId)
            .Concat(CrossWindowGenerationConsumptionRowManifest.Select(entry => entry.OfficialMatrixRowId))
            .Concat(CardMatrixAlignmentRowManifest.Select(entry => entry.OfficialMatrixRowId));
        var moveUnitWindow = Assert.Single(
            CoverageManifest,
            entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        var moveUnitResidual = Assert.Single(
            ResidualBlockerManifest,
            entry => string.Equals(entry.Family, "MOVE_UNIT_MOVEMENT_PERMISSION_POLICY", StringComparison.Ordinal));

        Assert.DoesNotContain(policyRowId, representativeSeedRowIds, StringComparer.Ordinal);
        Assert.DoesNotContain(policyRowId, missingOfficialRowIds, StringComparer.Ordinal);
        Assert.DoesNotContain(policyRowId, downstreamRowIds, StringComparer.Ordinal);
        Assert.Equal(PolicyNonResource, moveUnitWindow.Classification);
        Assert.Equal(PolicyDeferred, moveUnitResidual.Classification);
        Assert.Contains("not a rune, mana, experience, or temporary-resource payment window", moveUnitResidual.CurrentEvidence, StringComparison.Ordinal);
        Assert.Contains("must be reclassified", moveUnitResidual.MissingOfficialBreadth, StringComparison.Ordinal);
        Assert.Contains("PaymentEngine quote / command / audit parity", moveUnitResidual.MissingOfficialBreadth, StringComparison.Ordinal);
        Assert.Contains("Policy deferred only", moveUnitResidual.ClosureStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineOfficialMatrixPolicyDeferredMoveUnitDoesNotClaimPaymentCoverageOrClosure()
    {
        var policyRow = Assert.Single(
            OfficialPaymentEngineMatrixSeedRowManifest,
            entry => string.Equals(entry.RowStatus, PolicyDeferredRow, StringComparison.Ordinal));
        var moveUnitWindow = Assert.Single(
            CoverageManifest,
            entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        var moveUnitResidual = Assert.Single(
            ResidualBlockerManifest,
            entry => string.Equals(entry.Family, "MOVE_UNIT_MOVEMENT_PERMISSION_POLICY", StringComparison.Ordinal));
        var combinedText = string.Join(
            " ",
            new[]
            {
                policyRow.RowId,
                policyRow.Axis,
                policyRow.RowStatus,
                policyRow.ActionWindow,
                policyRow.PaymentOrPolicyProfile,
                policyRow.RepresentativeScope,
                policyRow.PromptAnchor,
                policyRow.CommandAnchor,
                policyRow.AuditAnchor,
                policyRow.RollbackExpectation,
                policyRow.RemainingOfficialBreadth,
                policyRow.ClosureStatus,
                moveUnitWindow.Classification,
                moveUnitWindow.EvidenceSummary,
                moveUnitWindow.ClosureStatus,
                moveUnitResidual.Classification,
                moveUnitResidual.CurrentEvidence,
                moveUnitResidual.MissingOfficialBreadth,
                moveUnitResidual.RollbackExpectation,
                moveUnitResidual.ClosureStatus
            }.Concat(policyRow.DocAnchors)
                .Concat(moveUnitWindow.DocAnchors)
                .Concat(moveUnitResidual.DocAnchors));

        Assert.Contains("policy-deferred-row", combinedText, StringComparison.Ordinal);
        Assert.Contains("policy-non-resource", combinedText, StringComparison.Ordinal);
        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("representative-seed", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("missing-official-row", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal),
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
    public void PaymentEngineRollbackFailureAllWindowMatrixCoversEveryRequiredSurfaceAndFamily()
    {
        var requiredActionWindows = new[]
        {
            "PLAY_CARD",
            "PAY_COST",
            "ACTIVATE_ABILITY",
            "ASSEMBLE_EQUIPMENT",
            "TRIGGER_PAYMENT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT"
        };
        var requiredFamilies = RollbackFailureRowManifest
            .Select(entry => entry.FailureFamily)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            requiredActionWindows.Order(StringComparer.Ordinal),
            RollbackFailureAllWindowMatrixManifest.Select(entry => entry.ActionWindow).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(
            requiredFamilies,
            RollbackFailureAllWindowMatrixManifest.Select(entry => entry.FailureFamily).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(requiredActionWindows.Length * requiredFamilies.Length, RollbackFailureAllWindowMatrixManifest.Length);

        foreach (var actionWindow in requiredActionWindows)
        {
            Assert.Equal(
                requiredFamilies,
                RollbackFailureAllWindowMatrixManifest
                    .Where(entry => string.Equals(entry.ActionWindow, actionWindow, StringComparison.Ordinal))
                    .Select(entry => entry.FailureFamily)
                    .Order(StringComparer.Ordinal));
        }

        foreach (var family in requiredFamilies)
        {
            Assert.Equal(
                requiredActionWindows.Order(StringComparer.Ordinal),
                RollbackFailureAllWindowMatrixManifest
                    .Where(entry => string.Equals(entry.FailureFamily, family, StringComparison.Ordinal))
                    .Select(entry => entry.ActionWindow)
                    .Order(StringComparer.Ordinal));
        }

        Assert.DoesNotContain(RollbackFailureAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(RollbackFailureAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "HIDE_CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(RollbackFailureAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "LEGEND_ACT", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineRollbackFailureAllWindowMatrixRequiresBoundPromptCommandNoMutationAuditAndDocFields()
    {
        Assert.All(RollbackFailureAllWindowMatrixManifest, entry =>
        {
            Assert.StartsWith("ROW_ROLLBACK_FAILURE_MATRIX_", entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.ActionWindow, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.FailureFamily, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Equal(RollbackFailureAllWindowMatrix, entry.Classification);
            Assert.Equal("ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING", entry.OfficialMatrixRowId);
            Assert.False(string.IsNullOrWhiteSpace(entry.FailureDimension));
            Assert.False(string.IsNullOrWhiteSpace(entry.PaymentSourceKind));
            Assert.Contains("prompt quote", entry.PromptQuote, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command rejection", entry.CommandRejection, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-mutation", entry.NoMutationAssertion, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit expectation", entry.AuditExpectation, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineRollbackFailureAllWindowMatrixLinksBackTo03BEFamiliesAndSurfaceDocs()
    {
        var familySet = RollbackFailureRowManifest.Select(entry => entry.FailureFamily).ToHashSet(StringComparer.Ordinal);
        var actionWindowDocs = RollbackFailureAllWindowActionWindowProfiles.ToDictionary(entry => entry.ActionWindow, entry => entry.DocAnchor, StringComparer.Ordinal);

        Assert.All(RollbackFailureAllWindowMatrixManifest, entry =>
        {
            Assert.Contains(entry.FailureFamily, familySet);
            Assert.Contains("docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md", entry.DocAnchors);
            Assert.Contains(actionWindowDocs[entry.ActionWindow], entry.DocAnchors);
        });
    }

    [Fact]
    public void PaymentEngineRollbackFailureAllWindowMatrixKeepsFailureDimensionsExecutable()
    {
        var combinedText = string.Join(
            " ",
            RollbackFailureAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.FailureFamily,
                    entry.ActionWindow,
                    entry.FailureDimension,
                    entry.PaymentSourceKind,
                    entry.PromptQuote,
                    entry.CommandRejection,
                    entry.NoMutationAssertion,
                    entry.AuditExpectation,
                    entry.RemainingOfficialBreadth
                }.Concat(entry.DocAnchors)));

        foreach (var requiredPhrase in new[]
        {
            "stale",
            "invalid",
            "wrong trait",
            "insufficient",
            "target",
            "optional",
            "extra",
            "alternative",
            "replacement",
            "prevention",
            "generated",
            "duplicate",
            "cross-window",
            "prompt quote",
            "command rejection",
            "no-mutation",
            "audit expectation"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineRollbackFailureAllWindowMatrixDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            RollbackFailureAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.FailureFamily,
                    entry.Classification,
                    entry.OfficialMatrixRowId,
                    entry.ActionWindow,
                    entry.FailureDimension,
                    entry.PaymentSourceKind,
                    entry.PromptQuote,
                    entry.CommandRejection,
                    entry.NoMutationAssertion,
                    entry.AuditExpectation,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal),
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
    public void PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixCoversEveryRequiredSurfaceAndFamily()
    {
        var requiredActionWindows = new[]
        {
            "PLAY_CARD",
            "PAY_COST",
            "ACTIVATE_ABILITY",
            "ASSEMBLE_EQUIPMENT",
            "TRIGGER_PAYMENT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT"
        };
        var requiredFamilies = CrossWindowGenerationConsumptionRowManifest
            .Select(entry => entry.Family)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            requiredActionWindows.Order(StringComparer.Ordinal),
            CrossWindowGenerationConsumptionAllWindowMatrixManifest.Select(entry => entry.ActionWindow).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(
            requiredFamilies,
            CrossWindowGenerationConsumptionAllWindowMatrixManifest.Select(entry => entry.Family).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(requiredActionWindows.Length * requiredFamilies.Length, CrossWindowGenerationConsumptionAllWindowMatrixManifest.Length);

        foreach (var actionWindow in requiredActionWindows)
        {
            Assert.Equal(
                requiredFamilies,
                CrossWindowGenerationConsumptionAllWindowMatrixManifest
                    .Where(entry => string.Equals(entry.ActionWindow, actionWindow, StringComparison.Ordinal))
                    .Select(entry => entry.Family)
                    .Order(StringComparer.Ordinal));
        }

        foreach (var family in requiredFamilies)
        {
            Assert.Equal(
                requiredActionWindows.Order(StringComparer.Ordinal),
                CrossWindowGenerationConsumptionAllWindowMatrixManifest
                    .Where(entry => string.Equals(entry.Family, family, StringComparison.Ordinal))
                    .Select(entry => entry.ActionWindow)
                    .Order(StringComparer.Ordinal));
        }

        Assert.DoesNotContain(CrossWindowGenerationConsumptionAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(CrossWindowGenerationConsumptionAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "HIDE_CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(CrossWindowGenerationConsumptionAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "LEGEND_ACT", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixRequiresBoundLifecycleAndAuditFields()
    {
        Assert.All(CrossWindowGenerationConsumptionAllWindowMatrixManifest, entry =>
        {
            Assert.StartsWith("ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_", entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.ActionWindow, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.Family, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Equal(CrossWindowAllWindowMatrix, entry.Classification);
            Assert.Equal("ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING", entry.OfficialMatrixRowId);
            Assert.False(string.IsNullOrWhiteSpace(entry.GenerationScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.ConsumptionScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.ResourceLifetimeDimension));
            Assert.Contains("prompt quote", entry.PromptQuote, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandCommitOrRejectionAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit expectation", entry.AuditExpectation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("lifetime", entry.LifetimeNoMutationRestrictionAssertion, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-mutation", entry.LifetimeNoMutationRestrictionAssertion, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("restriction", entry.LifetimeNoMutationRestrictionAssertion, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixLinksBackTo03BFFamiliesAnd03BMDocs()
    {
        var familySet = CrossWindowGenerationConsumptionRowManifest.Select(entry => entry.Family).ToHashSet(StringComparer.Ordinal);
        var actionWindowDocs = CrossWindowGenerationConsumptionAllWindowActionWindowProfiles.ToDictionary(entry => entry.ActionWindow, entry => entry.DocAnchor, StringComparer.Ordinal);

        Assert.All(CrossWindowGenerationConsumptionAllWindowMatrixManifest, entry =>
        {
            Assert.Contains(entry.Family, familySet);
            Assert.Contains("docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md", entry.DocAnchors);
            Assert.Contains(actionWindowDocs[entry.ActionWindow], entry.DocAnchors);
        });
    }

    [Fact]
    public void PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixKeepsLifecycleDimensionsExecutable()
    {
        var combinedText = string.Join(
            " ",
            CrossWindowGenerationConsumptionAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Family,
                    entry.ActionWindow,
                    entry.GenerationScope,
                    entry.ConsumptionScope,
                    entry.ResourceLifetimeDimension,
                    entry.PromptQuote,
                    entry.CommandCommitOrRejectionAnchor,
                    entry.AuditExpectation,
                    entry.LifetimeNoMutationRestrictionAssertion,
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
            "lifetime",
            "prompt quote",
            "command",
            "audit expectation",
            "no-mutation"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            CrossWindowGenerationConsumptionAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Family,
                    entry.Classification,
                    entry.OfficialMatrixRowId,
                    entry.ActionWindow,
                    entry.GenerationScope,
                    entry.ConsumptionScope,
                    entry.ResourceLifetimeDimension,
                    entry.PromptQuote,
                    entry.CommandCommitOrRejectionAnchor,
                    entry.AuditExpectation,
                    entry.LifetimeNoMutationRestrictionAssertion,
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
    public void PaymentEngineCardMatrixAlignmentAllWindowMatrixCoversEveryRequiredSurfaceAndFamily()
    {
        var requiredActionWindows = new[]
        {
            "PLAY_CARD",
            "PAY_COST",
            "ACTIVATE_ABILITY",
            "ASSEMBLE_EQUIPMENT",
            "TRIGGER_PAYMENT",
            "BATTLEFIELD_HELD_SCORE_PAYMENT"
        };
        var requiredFamilies = CardMatrixAlignmentRowManifest
            .Select(entry => entry.Family)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            requiredActionWindows.Order(StringComparer.Ordinal),
            CardMatrixAlignmentAllWindowMatrixManifest.Select(entry => entry.ActionWindow).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(
            requiredFamilies,
            CardMatrixAlignmentAllWindowMatrixManifest.Select(entry => entry.Family).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(requiredActionWindows.Length * requiredFamilies.Length, CardMatrixAlignmentAllWindowMatrixManifest.Length);

        foreach (var actionWindow in requiredActionWindows)
        {
            Assert.Equal(
                requiredFamilies,
                CardMatrixAlignmentAllWindowMatrixManifest
                    .Where(entry => string.Equals(entry.ActionWindow, actionWindow, StringComparison.Ordinal))
                    .Select(entry => entry.Family)
                    .Order(StringComparer.Ordinal));
        }

        foreach (var family in requiredFamilies)
        {
            Assert.Equal(
                requiredActionWindows.Order(StringComparer.Ordinal),
                CardMatrixAlignmentAllWindowMatrixManifest
                    .Where(entry => string.Equals(entry.Family, family, StringComparison.Ordinal))
                    .Select(entry => entry.ActionWindow)
                    .Order(StringComparer.Ordinal));
        }

        Assert.DoesNotContain(CardMatrixAlignmentAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(CardMatrixAlignmentAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "HIDE_CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(CardMatrixAlignmentAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "LEGEND_ACT", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineCardMatrixAlignmentAllWindowMatrixRequiresBoundEvidenceAndTraceFields()
    {
        Assert.All(CardMatrixAlignmentAllWindowMatrixManifest, entry =>
        {
            Assert.StartsWith("ROW_CARD_MATRIX_ALIGNMENT_MATRIX_", entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.ActionWindow, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.Family, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Equal(CardMatrixAllWindowMatrix, entry.Classification);
            Assert.Equal("ROW_CARD_MATRIX_ALIGNMENT_MISSING", entry.OfficialMatrixRowId);
            Assert.False(string.IsNullOrWhiteSpace(entry.MatrixScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("prompt evidence anchor", entry.PromptEvidenceAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command evidence anchor", entry.CommandEvidenceAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("audit evidence anchor", entry.AuditEvidenceAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("matrix sync/status anchor", entry.MatrixSyncStatusAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("trace", entry.FrontendSnapshotOrRuleSourceTrace, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineCardMatrixAlignmentAllWindowMatrixLinksBackTo03BGFamiliesAnd03BNDocs()
    {
        var familySet = CardMatrixAlignmentRowManifest.Select(entry => entry.Family).ToHashSet(StringComparer.Ordinal);
        var actionWindowDocs = CardMatrixAlignmentAllWindowActionWindowProfiles.ToDictionary(entry => entry.ActionWindow, entry => entry.DocAnchor, StringComparer.Ordinal);

        Assert.All(CardMatrixAlignmentAllWindowMatrixManifest, entry =>
        {
            Assert.Contains(entry.Family, familySet);
            Assert.Contains("docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_EVIDENCE.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md", entry.DocAnchors);
            Assert.Contains(actionWindowDocs[entry.ActionWindow], entry.DocAnchors);
        });
    }

    [Fact]
    public void PaymentEngineCardMatrixAlignmentAllWindowMatrixKeepsMatrixDimensionsExecutable()
    {
        var combinedText = string.Join(
            " ",
            CardMatrixAlignmentAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Family,
                    entry.ActionWindow,
                    entry.MatrixScope,
                    entry.RepresentativeSurface,
                    entry.PromptEvidenceAnchor,
                    entry.CommandEvidenceAnchor,
                    entry.AuditEvidenceAnchor,
                    entry.MatrixSyncStatusAnchor,
                    entry.FrontendSnapshotOrRuleSourceTrace,
                    entry.RemainingOfficialBreadth
                }.Concat(entry.DocAnchors)));

        foreach (var requiredPhrase in new[]
        {
            "cardId",
            "collectorId",
            "oracleId",
            "effectId",
            "fullOfficial=false",
            "prompt",
            "command",
            "audit",
            "matrix",
            "FAQ",
            "ActionPrompt",
            "authoritative snapshot",
            "frontend",
            "blocker",
            "JSON",
            "official",
            "representative"
        })
        {
            Assert.Contains(requiredPhrase, combinedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PaymentEngineCardMatrixAlignmentAllWindowMatrixDoesNotClaimFullOfficialOrP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            CardMatrixAlignmentAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Family,
                    entry.Classification,
                    entry.OfficialMatrixRowId,
                    entry.ActionWindow,
                    entry.MatrixScope,
                    entry.RepresentativeSurface,
                    entry.PromptEvidenceAnchor,
                    entry.CommandEvidenceAnchor,
                    entry.AuditEvidenceAnchor,
                    entry.MatrixSyncStatusAnchor,
                    entry.FrontendSnapshotOrRuleSourceTrace,
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
    public void PaymentEngineKeywordPaymentBranchAllWindowMatrixCoversEveryRequiredSurfaceAndBranch()
    {
        var requiredActionWindows = KeywordPaymentBranchAllWindowActionWindowProfiles
            .Select(entry => entry.ActionWindow)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var requiredBranches = KeywordPaymentBranchManifest
            .Select(entry => entry.Branch)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            requiredActionWindows,
            KeywordPaymentBranchAllWindowMatrixManifest.Select(entry => entry.ActionWindow).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(
            requiredBranches,
            KeywordPaymentBranchAllWindowMatrixManifest.Select(entry => entry.Branch).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(requiredActionWindows.Length * requiredBranches.Length, KeywordPaymentBranchAllWindowMatrixManifest.Length);
        Assert.Equal(48, KeywordPaymentBranchAllWindowMatrixManifest.Length);
        Assert.Empty(KeywordPaymentBranchAllWindowMatrixManifest
            .GroupBy(entry => (entry.ActionWindow, entry.Branch))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));

        Assert.DoesNotContain(KeywordPaymentBranchAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(KeywordPaymentBranchAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "HIDE_CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(KeywordPaymentBranchAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "LEGEND_ACT", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineKeywordPaymentBranchAllWindowMatrixRequiresBoundQuoteCommandAuditRollbackAndDocFields()
    {
        Assert.All(KeywordPaymentBranchAllWindowMatrixManifest, entry =>
        {
            Assert.Equal(KeywordBranchAllWindowMatrix, entry.Classification);
            Assert.Equal("KEYWORD_PAYMENT_BRANCHES", entry.ResidualBlockerFamily);
            Assert.Contains("KEYWORD_BRANCHES", entry.OfficialResidualAxes, StringComparison.Ordinal);
            Assert.Contains("COST_MODIFIERS", entry.OfficialResidualAxes, StringComparison.Ordinal);
            Assert.Contains("OPTIONAL_EXTRA_ALTERNATIVE_COSTS", entry.OfficialResidualAxes, StringComparison.Ordinal);
            Assert.Contains("REPLACEMENT_PREVENTION", entry.OfficialResidualAxes, StringComparison.Ordinal);
            Assert.Contains(entry.Branch, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.ActionWindow, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(entry.PaymentSurfaceScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("prompt", entry.PromptQuote, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandRevalidation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COST_PAID", entry.AuditExpectation, StringComparison.Ordinal);
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
    public void PaymentEngineKeywordPaymentBranchAllWindowMatrixLinksBackTo03AYBranchesAnd03BPDocs()
    {
        var branchSet = KeywordPaymentBranchManifest.Select(entry => entry.Branch).ToHashSet(StringComparer.Ordinal);
        var actionWindowDocs = KeywordPaymentBranchAllWindowActionWindowProfiles.ToDictionary(entry => entry.ActionWindow, entry => entry.DocAnchor, StringComparer.Ordinal);

        Assert.All(KeywordPaymentBranchAllWindowMatrixManifest, entry =>
        {
            Assert.Contains(entry.Branch, branchSet);
            Assert.Contains("docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_EVIDENCE.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_HANDOFF.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md", entry.DocAnchors);
            Assert.Contains(actionWindowDocs[entry.ActionWindow], entry.DocAnchors);
        });
    }

    [Fact]
    public void PaymentEngineKeywordPaymentBranchAllWindowMatrixKeepsResidualAxesAndBreadthExecutable()
    {
        var combinedText = string.Join(
            " ",
            KeywordPaymentBranchAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Branch,
                    entry.ResidualBlockerFamily,
                    entry.OfficialResidualAxes,
                    entry.ActionWindow,
                    entry.PaymentSurfaceScope,
                    entry.RepresentativeSurface,
                    entry.PromptQuote,
                    entry.CommandRevalidation,
                    entry.AuditExpectation,
                    entry.RollbackExpectation,
                    entry.RemainingOfficialBreadth
                }));

        Assert.Contains("KEYWORD_PAYMENT_BRANCHES", combinedText, StringComparison.Ordinal);
        Assert.Contains("KEYWORD_BRANCHES", combinedText, StringComparison.Ordinal);
        Assert.Contains("COST_MODIFIERS", combinedText, StringComparison.Ordinal);
        Assert.Contains("OPTIONAL_EXTRA_ALTERNATIVE_COSTS", combinedText, StringComparison.Ordinal);
        Assert.Contains("REPLACEMENT_PREVENTION", combinedText, StringComparison.Ordinal);
        Assert.Contains("prompt", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("command", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COST_PAID", combinedText, StringComparison.Ordinal);
        Assert.Contains("rollback", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temporary resource lifetime", combinedText, StringComparison.Ordinal);
        Assert.Contains("replacement/prevention ordering", combinedText, StringComparison.Ordinal);
        Assert.Contains("optional/extra/alternative branch", combinedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineKeywordPaymentBranchAllWindowMatrixDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            KeywordPaymentBranchAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Branch,
                    entry.Classification,
                    entry.ResidualBlockerFamily,
                    entry.OfficialResidualAxes,
                    entry.ActionWindow,
                    entry.PaymentSurfaceScope,
                    entry.RepresentativeSurface,
                    entry.PromptQuote,
                    entry.CommandRevalidation,
                    entry.AuditExpectation,
                    entry.RollbackExpectation,
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
        Assert.Equal(22, manifestAbilityIds.Length);
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
        Assert.Contains("22", residualBlocker.CurrentEvidence, StringComparison.Ordinal);
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
            22,
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
    public void PaymentEngineResourceSkillAllWindowMatrixCoversEveryRequiredSurfaceAndFamily()
    {
        var requiredActionWindows = ResourceSkillAllWindowActionWindowProfiles
            .Select(entry => entry.ActionWindow)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var requiredFamilies = ResourceSkillCoverageManifest
            .Select(entry => entry.Family)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            requiredActionWindows,
            ResourceSkillAllWindowMatrixManifest.Select(entry => entry.ActionWindow).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(
            requiredFamilies,
            ResourceSkillAllWindowMatrixManifest.Select(entry => entry.Family).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(requiredActionWindows.Length * requiredFamilies.Length, ResourceSkillAllWindowMatrixManifest.Length);
        Assert.Equal(54, ResourceSkillAllWindowMatrixManifest.Length);
        Assert.Empty(ResourceSkillAllWindowMatrixManifest
            .GroupBy(entry => (entry.ActionWindow, entry.Family))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));

        Assert.DoesNotContain(ResourceSkillAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(ResourceSkillAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "HIDE_CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(ResourceSkillAllWindowMatrixManifest, entry => string.Equals(entry.ActionWindow, "LEGEND_ACT", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineResourceSkillAllWindowMatrixRequiresBoundGenerationAuditRollbackAndDocFields()
    {
        Assert.All(ResourceSkillAllWindowMatrixManifest, entry =>
        {
            Assert.Equal(ResourceSkillAllWindowMatrix, entry.Classification);
            Assert.Equal("RESOURCE_SKILL_A_C_FAMILY", entry.ResidualBlockerFamily);
            Assert.Equal("RESOURCE_SKILLS", entry.OfficialResidualAxis);
            Assert.Contains(entry.Family, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.ActionWindow, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.NotEmpty(entry.AbilityIds);
            Assert.False(string.IsNullOrWhiteSpace(entry.PaymentSurfaceScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.ResourceLifecycleScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.RepresentativeSurface));
            Assert.Contains("prompt", entry.PromptQuote, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandRevalidation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ABILITY_ACTIVATED", entry.AuditExpectation, StringComparison.Ordinal);
            Assert.Contains("generated", entry.GeneratedResourceRestriction, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("payment-only", entry.GeneratedResourceRestriction, StringComparison.OrdinalIgnoreCase);
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
    public void PaymentEngineResourceSkillAllWindowMatrixLinksBackTo03AZFamiliesAnd03BQDocs()
    {
        var familySet = ResourceSkillCoverageManifest.Select(entry => entry.Family).ToHashSet(StringComparer.Ordinal);
        var abilityIdsByFamily = ResourceSkillCoverageManifest.ToDictionary(entry => entry.Family, entry => entry.AbilityIds, StringComparer.Ordinal);
        var actionWindowDocs = ResourceSkillAllWindowActionWindowProfiles.ToDictionary(entry => entry.ActionWindow, entry => entry.DocAnchor, StringComparer.Ordinal);

        Assert.All(ResourceSkillAllWindowMatrixManifest, entry =>
        {
            Assert.Contains(entry.Family, familySet);
            Assert.Equal(abilityIdsByFamily[entry.Family], entry.AbilityIds);
            Assert.Contains("docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_EVIDENCE.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_HANDOFF.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md", entry.DocAnchors);
            Assert.Contains(actionWindowDocs[entry.ActionWindow], entry.DocAnchors);
        });
    }

    [Fact]
    public void PaymentEngineResourceSkillAllWindowMatrixKeepsResourceSkillAxesAndBreadthExecutable()
    {
        var combinedText = string.Join(
            " ",
            ResourceSkillAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Family,
                    entry.Classification,
                    entry.ResidualBlockerFamily,
                    entry.OfficialResidualAxis,
                    entry.ActionWindow,
                    entry.PaymentSurfaceScope,
                    entry.ResourceLifecycleScope,
                    entry.RepresentativeSurface,
                    entry.PromptQuote,
                    entry.CommandRevalidation,
                    entry.AuditExpectation,
                    entry.GeneratedResourceRestriction,
                    entry.RollbackExpectation,
                    entry.RemainingOfficialBreadth
                }));

        Assert.Contains("RESOURCE_SKILL_A_C_FAMILY", combinedText, StringComparison.Ordinal);
        Assert.Contains("RESOURCE_SKILLS", combinedText, StringComparison.Ordinal);
        Assert.Contains("prompt", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("command", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ABILITY_ACTIVATED", combinedText, StringComparison.Ordinal);
        Assert.Contains("generated", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("payment-only", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cross-window", combinedText, StringComparison.Ordinal);
        Assert.Contains("temporary resource lifetime", combinedText, StringComparison.Ordinal);
        Assert.Contains("conversion ordering", combinedText, StringComparison.Ordinal);
        Assert.Contains("Gold token", combinedText, StringComparison.Ordinal);
        Assert.Contains("[A]", combinedText, StringComparison.Ordinal);
        Assert.Contains("[C]", combinedText, StringComparison.Ordinal);
        Assert.Contains("rollback", combinedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaymentEngineResourceSkillAllWindowMatrixDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            ResourceSkillAllWindowMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Family,
                    entry.Classification,
                    entry.ResidualBlockerFamily,
                    entry.OfficialResidualAxis,
                    entry.ActionWindow,
                    entry.PaymentSurfaceScope,
                    entry.ResourceLifecycleScope,
                    entry.RepresentativeSurface,
                    entry.PromptQuote,
                    entry.CommandRevalidation,
                    entry.AuditExpectation,
                    entry.GeneratedResourceRestriction,
                    entry.RollbackExpectation,
                    entry.RemainingOfficialBreadth,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "READY",
            combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaymentEngineResourceSkillOfficialBreadthManifestMatchesFixedOfficialCatalogScan()
    {
        var officialCatalogCandidateCardNos = GetFixedOfficialResourceSkillCandidateCardNos();
        var manifestCardNos = ResourceSkillOfficialBreadthManifest
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(officialCatalogCandidateCardNos, manifestCardNos);
        Assert.Equal(32, manifestCardNos.Length);
        Assert.Empty(ResourceSkillOfficialBreadthManifest
            .GroupBy(entry => entry.CardNo, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineResourceSkillOfficialBreadthManifestSplitsImplementedAndDeferredCandidates()
    {
        var implementedCatalogSourceCardNos = P4ActivatedAbilityCatalog.GetAll()
            .Where(definition => definition.IsResourceSkill)
            .Select(definition => definition.SourceCardNo)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var implementedManifestCardNos = ResourceSkillOfficialBreadthManifest
            .Where(entry => string.Equals(entry.Classification, ImplementedResourceSkillOfficialCandidate, StringComparison.Ordinal))
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var deferredManifestCardNos = ResourceSkillOfficialBreadthManifest
            .Where(entry => string.Equals(entry.Classification, DeferredResourceSkillOfficialCandidate, StringComparison.Ordinal))
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(implementedCatalogSourceCardNos, implementedManifestCardNos);
        Assert.Equal(22, implementedManifestCardNos.Length);
        Assert.Equal(10, deferredManifestCardNos.Length);
        Assert.Empty(implementedManifestCardNos.Intersect(deferredManifestCardNos, StringComparer.Ordinal));
    }

    [Fact]
    public void PaymentEngineResourceSkillOfficialBreadthManifestRequiresEvidenceAndDocAnchors()
    {
        Assert.All(ResourceSkillOfficialBreadthManifest, entry =>
        {
            Assert.Equal("RESOURCE_SKILL_A_C_FAMILY", entry.ResidualBlockerFamily);
            Assert.Equal("RESOURCE_SKILLS", entry.OfficialResidualAxis);
            Assert.False(string.IsNullOrWhiteSpace(entry.OfficialResourceProfile));
            Assert.False(string.IsNullOrWhiteSpace(entry.CurrentEvidenceStatus));
            Assert.Contains("Future", entry.RequiredFutureEvidence, StringComparison.Ordinal);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("[A]", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("[C]", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.NotEmpty(entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_EVIDENCE.md", entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineResourceSkillOfficialBreadthManifestKeepsDeferredOfficialBreadthExplicit()
    {
        var combinedText = string.Join(
            " ",
            ResourceSkillOfficialBreadthManifest.SelectMany(entry =>
                new[]
                {
                    entry.CardNo,
                    entry.Classification,
                    entry.OfficialResourceProfile,
                    entry.CurrentEvidenceStatus,
                    entry.RequiredFutureEvidence,
                    entry.ClosureStatus
                }));

        Assert.Contains("Jhin movement-triggered", combinedText, StringComparison.Ordinal);
        Assert.Contains("Honeyfruit", combinedText, StringComparison.Ordinal);
        Assert.Contains("Blue Sentinel", combinedText, StringComparison.Ordinal);
        Assert.Contains("Diana spell-duel-only", combinedText, StringComparison.Ordinal);
        Assert.Contains("Ornn Forge equipment-only", combinedText, StringComparison.Ordinal);
        Assert.Contains("Lux spell-only", combinedText, StringComparison.Ordinal);
        Assert.Contains("KaiSa spell-only", combinedText, StringComparison.Ordinal);
        Assert.Contains("Darius Inspire-gated", combinedText, StringComparison.Ordinal);
        Assert.Contains(DeferredResourceSkillOfficialCandidate, combinedText, StringComparison.Ordinal);
        Assert.Contains("generated-resource lifetime", combinedText, StringComparison.Ordinal);
        Assert.Contains("no-mutation rollback", combinedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineResourceSkillOfficialBreadthManifestDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            ResourceSkillOfficialBreadthManifest.SelectMany(entry =>
                new[]
                {
                    entry.CardNo,
                    entry.Classification,
                    entry.OfficialResourceProfile,
                    entry.CurrentEvidenceStatus,
                    entry.RequiredFutureEvidence,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("FullOfficialRulePass", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText.Replace("NOT READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillFamilyManifestMatchesOfficialDeferredSet()
    {
        var officialDeferredCardNos = ResourceSkillOfficialBreadthManifest
            .Where(entry => string.Equals(entry.Classification, DeferredResourceSkillOfficialCandidate, StringComparison.Ordinal))
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var familyManifestCardNos = DeferredResourceSkillFamilyManifest
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(officialDeferredCardNos, familyManifestCardNos);
        Assert.Equal(10, familyManifestCardNos.Length);
        Assert.Empty(DeferredResourceSkillFamilyManifest
            .GroupBy(entry => entry.CardNo, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillFamilyManifestSplitsLegendBridgeAndNonLegendCandidates()
    {
        var legendBridgeCardNos = DeferredResourceSkillFamilyManifest
            .Where(entry => string.Equals(entry.Classification, DeferredLegendResourceActionBridge, StringComparison.Ordinal))
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var nonLegendCardNos = DeferredResourceSkillFamilyManifest
            .Where(entry => string.Equals(entry.Classification, DeferredNonLegendResourceSkillRuntimeVerifier, StringComparison.Ordinal))
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "OGN·247/298",
                "OGN·253/298",
                "OGN·299*/298",
                "OGN·299/298",
                "OGN·302*/298",
                "OGN·302/298",
                "SFD·189/221",
                "SFD·244/221",
                "UNL-197/219"
            ],
            legendBridgeCardNos);
        Assert.Equal(["OGS·014/024"], nonLegendCardNos);
        Assert.Equal(9, legendBridgeCardNos.Length);
        Assert.Single(nonLegendCardNos);
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillFamilyManifestRejectsLegendProxyClosure()
    {
        var legendBridgeEntries = DeferredResourceSkillFamilyManifest
            .Where(entry => string.Equals(entry.Classification, DeferredLegendResourceActionBridge, StringComparison.Ordinal))
            .ToArray();

        Assert.All(legendBridgeEntries, entry =>
        {
            Assert.Equal("LEGEND_ACT", entry.CurrentActionDomain);
            Assert.Contains("cannot close RESOURCE_SKILLS by proxy", entry.CurrentEvidenceStatus, StringComparison.Ordinal);
            Assert.Contains("bridge", entry.RequiredFutureWork, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fresh A dispatch", entry.RequiredFutureWork, StringComparison.Ordinal);
            Assert.Contains("RESOURCE_SKILLS closure is explicit", entry.ClosureStatus, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillFamilyManifestRequiresEvidenceAndNoReadyClosure()
    {
        Assert.All(DeferredResourceSkillFamilyManifest, entry =>
        {
            Assert.Equal("RESOURCE_SKILL_A_C_FAMILY", entry.ResidualBlockerFamily);
            Assert.Equal("RESOURCE_SKILLS", entry.OfficialResidualAxis);
            Assert.False(string.IsNullOrWhiteSpace(entry.FamilySplit));
            Assert.False(string.IsNullOrWhiteSpace(entry.OfficialResourceProfile));
            Assert.False(string.IsNullOrWhiteSpace(entry.CurrentEvidenceStatus));
            Assert.Contains("Future B", entry.RequiredFutureWork, StringComparison.Ordinal);
            Assert.Contains("fresh A dispatch", entry.RequiredFutureWork, StringComparison.Ordinal);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("RESOURCE_SKILLS closure is explicit", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_EVIDENCE.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BV_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_HANDOFF.md", entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillNextDispatchGateManifestListsTwoFreshBGates()
    {
        var requiredGateIds = new[]
        {
            "B_DEFERRED_NON_LEGEND_RESOURCE_SKILL_RUNTIME",
            "B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER"
        };

        Assert.Equal(
            requiredGateIds.Order(StringComparer.Ordinal),
            DeferredResourceSkillNextDispatchGateManifest.Select(entry => entry.GateId).Order(StringComparer.Ordinal));
        Assert.Empty(DeferredResourceSkillNextDispatchGateManifest
            .GroupBy(entry => entry.GateId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));

        Assert.All(DeferredResourceSkillNextDispatchGateManifest, entry =>
        {
            Assert.Equal(DeferredResourceSkillNextDispatchGate, entry.Classification);
            Assert.Contains("Fresh A dispatch", entry.RequiredFreshDispatch, StringComparison.Ordinal);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("riftbound-dotnet.sln", entry.ForbiddenScope, StringComparison.Ordinal);
            Assert.NotEmpty(entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillNextDispatchGateManifestMatchesDeferredFamilySplit()
    {
        var candidateNosByClassification = DeferredResourceSkillFamilyManifest
            .GroupBy(entry => entry.Classification, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.CardNo).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        Assert.All(DeferredResourceSkillNextDispatchGateManifest, entry =>
        {
            Assert.True(candidateNosByClassification.TryGetValue(entry.CandidateClassification, out var expectedCardNos));
            Assert.Equal(expectedCardNos, entry.CandidateCardNos.Order(StringComparer.Ordinal).ToArray());
        });

        var coveredByDispatchGates = DeferredResourceSkillNextDispatchGateManifest
            .SelectMany(entry => entry.CandidateCardNos)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var deferredFamilySet = DeferredResourceSkillFamilyManifest
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(deferredFamilySet, coveredByDispatchGates);
        Assert.Equal(10, coveredByDispatchGates.Length);
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillNextDispatchGateRejectsProxyClosureAndCrossSliceMixing()
    {
        var nonLegendGate = Assert.Single(
            DeferredResourceSkillNextDispatchGateManifest,
            entry => string.Equals(entry.CandidateClassification, DeferredNonLegendResourceSkillRuntimeVerifier, StringComparison.Ordinal));
        var legendGate = Assert.Single(
            DeferredResourceSkillNextDispatchGateManifest,
            entry => string.Equals(entry.CandidateClassification, DeferredLegendResourceActionBridge, StringComparison.Ordinal));

        Assert.Contains("without borrowing LEGEND_ACT evidence", nonLegendGate.RequiredFutureEvidence, StringComparison.Ordinal);
        Assert.Contains("Legend bridge rows", nonLegendGate.ForbiddenScope, StringComparison.Ordinal);
        Assert.DoesNotContain("UNL-197/219", nonLegendGate.CandidateCardNos);

        Assert.Contains("LEGEND_ACT", legendGate.RequiredFutureEvidence, StringComparison.Ordinal);
        Assert.Contains("not proxy closure", legendGate.RepresentativeProxyEvidence, StringComparison.Ordinal);
        Assert.Contains("1 remaining non-legend 03BX runtime candidate", legendGate.ForbiddenScope, StringComparison.Ordinal);
        Assert.DoesNotContain("UNL-022/219", legendGate.CandidateCardNos);
    }

    [Fact]
    public void PaymentEngineDeferredResourceSkillNextDispatchGateDoesNotClaimReadyOrFullOfficial()
    {
        var combinedText = string.Join(
            " ",
            DeferredResourceSkillNextDispatchGateManifest.SelectMany(entry =>
                new[]
                {
                    entry.GateId,
                    entry.Owner,
                    entry.Classification,
                    entry.CandidateClassification,
                    entry.RequiredFreshDispatch,
                    entry.WriteLockScope,
                    entry.ForbiddenScope,
                    entry.RequiredFutureEvidence,
                    entry.RepresentativeProxyEvidence,
                    entry.ClosureStatus
                }.Concat(entry.CandidateCardNos).Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.Contains("RESOURCE_SKILLS", combinedText, StringComparison.Ordinal);
        Assert.Contains("proxy closure", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fullOfficial / READY", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("fullOfficial / READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineLegendResourceBridgeAggregateManifestMatchesLegendBridgeGateSet()
    {
        var legendGate = Assert.Single(
            DeferredResourceSkillNextDispatchGateManifest,
            entry => string.Equals(entry.CandidateClassification, DeferredLegendResourceActionBridge, StringComparison.Ordinal));
        var aggregateCardNos = LegendResourceBridgeAggregateManifest
            .SelectMany(entry => entry.CandidateCardNos)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var familyLegendCardNos = DeferredResourceSkillFamilyManifest
            .Where(entry => string.Equals(entry.Classification, DeferredLegendResourceActionBridge, StringComparison.Ordinal))
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "OGN·247/298",
                "OGN·253/298",
                "OGN·299*/298",
                "OGN·299/298",
                "OGN·302*/298",
                "OGN·302/298",
                "SFD·189/221",
                "SFD·244/221",
                "UNL-197/219"
            ],
            aggregateCardNos);
        Assert.Equal(legendGate.CandidateCardNos.Order(StringComparer.Ordinal).ToArray(), aggregateCardNos);
        Assert.Equal(familyLegendCardNos, aggregateCardNos);
        Assert.Equal(9, aggregateCardNos.Length);
        Assert.Equal(4, LegendResourceBridgeAggregateManifest.Length);
        Assert.Empty(aggregateCardNos
            .GroupBy(cardNo => cardNo, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
    }

    [Fact]
    public void PaymentEngineLegendResourceBridgeAggregateManifestRequiresPerChampionHandoffDocs()
    {
        var repositoryRoot = ResolveRepositoryRoot();

        Assert.Equal(
            [
                "LEGEND_BRIDGE_DARIUS_INSPIRE_MANA",
                "LEGEND_BRIDGE_DIANA_SPELL_DUEL_MANA",
                "LEGEND_BRIDGE_KAISA_SPELL_POWER",
                "LEGEND_BRIDGE_ORNN_EQUIPMENT_POWER"
            ],
            LegendResourceBridgeAggregateManifest.Select(entry => entry.BridgeGroupId).Order(StringComparer.Ordinal).ToArray());

        Assert.All(LegendResourceBridgeAggregateManifest, entry =>
        {
            Assert.Equal(LegendResourceBridgeAggregateVerifier, entry.Classification);
            Assert.False(string.IsNullOrWhiteSpace(entry.Champion));
            Assert.False(string.IsNullOrWhiteSpace(entry.AbilityId));
            Assert.NotEmpty(entry.CandidateCardNos);
            Assert.False(string.IsNullOrWhiteSpace(entry.TimingRestriction));
            Assert.False(string.IsNullOrWhiteSpace(entry.GeneratedResourceProfile));
            Assert.Contains("bridge input only", entry.CurrentLegendEvidence, StringComparison.Ordinal);
            Assert.Contains(entry.AbilityId, entry.RequiredFutureEvidence, StringComparison.Ordinal);
            Assert.Contains("source-card group", entry.RequiredFutureEvidence, StringComparison.Ordinal);
            Assert.Contains("fresh A dispatch", entry.RequiredFutureEvidence, StringComparison.Ordinal);
            Assert.Contains("RESOURCE_SKILLS closure gap", entry.RequiredFutureEvidence, StringComparison.Ordinal);
            Assert.Contains("card matrix JSON", entry.ForbiddenScope, StringComparison.Ordinal);
            Assert.Contains("riftbound-dotnet.sln", entry.ForbiddenScope, StringComparison.Ordinal);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.NotEmpty(entry.RequiredHandoffDocs);
            Assert.Contains("docs/CURRENT_STAGE4D_03CJ_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_AGGREGATE_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03CJ_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_AGGREGATE_EVIDENCE.md", entry.DocAnchors);

            Assert.All(entry.RequiredHandoffDocs, handoffDoc =>
            {
                Assert.Contains(handoffDoc, entry.DocAnchors);
                Assert.True(File.Exists(Path.Combine(repositoryRoot, handoffDoc)), handoffDoc);
            });
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineLegendResourceBridgeAggregateManifestRejectsProxyClosureAndReady()
    {
        var combinedText = string.Join(
            " ",
            LegendResourceBridgeAggregateManifest.SelectMany(entry =>
                new[]
                {
                    entry.BridgeGroupId,
                    entry.Champion,
                    entry.AbilityId,
                    entry.Classification,
                    entry.TimingRestriction,
                    entry.GeneratedResourceProfile,
                    entry.CurrentLegendEvidence,
                    entry.RequiredFutureEvidence,
                    entry.ForbiddenScope,
                    entry.ClosureStatus
                }.Concat(entry.CandidateCardNos)
                    .Concat(entry.RequiredHandoffDocs)
                    .Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.Contains("proxy input", combinedText, StringComparison.Ordinal);
        Assert.Contains("RESOURCE_SKILLS closure", combinedText, StringComparison.Ordinal);
        Assert.Contains("fullOfficial / READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("riftbound-dotnet.sln", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("fullOfficial / READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineLegendResourceBridgeImplementationAcceptanceManifestMatchesAggregateInputs()
    {
        var acceptanceCardNos = LegendResourceBridgeImplementationAcceptanceManifest
            .SelectMany(entry => entry.CandidateCardNos)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var aggregateCardNos = LegendResourceBridgeAggregateManifest
            .SelectMany(entry => entry.CandidateCardNos)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(aggregateCardNos, acceptanceCardNos);
        Assert.Equal(9, acceptanceCardNos.Length);
        Assert.Equal(4, LegendResourceBridgeImplementationAcceptanceManifest.Length);
        Assert.Equal(
            LegendResourceBridgeAggregateManifest.Select(entry => entry.BridgeGroupId).Order(StringComparer.Ordinal).ToArray(),
            LegendResourceBridgeImplementationAcceptanceManifest.Select(entry => entry.BridgeGroupId).Order(StringComparer.Ordinal).ToArray());

        Assert.All(LegendResourceBridgeImplementationAcceptanceManifest, acceptanceEntry =>
        {
            var aggregateEntry = Assert.Single(
                LegendResourceBridgeAggregateManifest,
                entry => string.Equals(entry.BridgeGroupId, acceptanceEntry.BridgeGroupId, StringComparison.Ordinal));

            Assert.Equal(aggregateEntry.Champion, acceptanceEntry.Champion);
            Assert.Equal(aggregateEntry.AbilityId, acceptanceEntry.AbilityId);
            Assert.Equal(
                aggregateEntry.CandidateCardNos.Order(StringComparer.Ordinal).ToArray(),
                acceptanceEntry.CandidateCardNos.Order(StringComparer.Ordinal).ToArray());
        });
    }

    [Fact]
    public void PaymentEngineLegendResourceBridgeImplementationAcceptanceManifestRequiresPromptCommandAuditRollbackAndReminder()
    {
        var repositoryRoot = ResolveRepositoryRoot();

        Assert.All(LegendResourceBridgeImplementationAcceptanceManifest, entry =>
        {
            Assert.Equal(LegendResourceBridgeImplementationAcceptance, entry.Classification);
            Assert.Contains("Server-filtered ActionPrompt", entry.PromptContract, StringComparison.Ordinal);
            Assert.Contains("no frontend inference", entry.PromptContract, StringComparison.Ordinal);
            Assert.Contains("revalidate ability id", entry.CommandContract, StringComparison.Ordinal);
            Assert.Contains("source", entry.CommandContract, StringComparison.Ordinal);
            Assert.Contains("generated-resource source", entry.AuditAndResourceContract, StringComparison.Ordinal);
            Assert.Contains("lifetime", entry.AuditAndResourceContract, StringComparison.Ordinal);
            Assert.Contains("consumption", entry.AuditAndResourceContract, StringComparison.Ordinal);
            Assert.Contains("cleanup evidence", entry.AuditAndResourceContract, StringComparison.Ordinal);
            Assert.Contains("no-mutation rollback", entry.RollbackContract, StringComparison.Ordinal);
            Assert.Contains("source-card parity", entry.SourceParityContract, StringComparison.Ordinal);
            Assert.Contains("cannot be targeted as responses", entry.ReminderBoundary, StringComparison.Ordinal);
            Assert.Contains("4D-03CL-B", entry.RequiredFutureTestAnchor, StringComparison.Ordinal);
            Assert.Contains("focused", entry.RequiredFutureTestAnchor, StringComparison.Ordinal);
            Assert.Contains("card matrix JSON", entry.LockedScope, StringComparison.Ordinal);
            Assert.Contains("riftbound-dotnet.sln", entry.LockedScope, StringComparison.Ordinal);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("RESOURCE_SKILLS", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("docs/CURRENT_STAGE4D_03CL_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_ACCEPTANCE_VERIFIER_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03CL_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_ACCEPTANCE_VERIFIER_EVIDENCE.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03CK_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_IMPLEMENTATION_HANDOFF.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03CK_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_IMPLEMENTATION_BASELINE_EVIDENCE.md", entry.DocAnchors);

            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(repositoryRoot, anchor)), anchor);
            });
        });
    }

    [Fact]
    public void PaymentEngineLegendResourceBridgeImplementationAcceptanceManifestRejectsRuntimeAndReadyClosure()
    {
        var combinedText = string.Join(
            " ",
            LegendResourceBridgeImplementationAcceptanceManifest.SelectMany(entry =>
                new[]
                {
                    entry.BridgeGroupId,
                    entry.Champion,
                    entry.AbilityId,
                    entry.Classification,
                    entry.PromptContract,
                    entry.CommandContract,
                    entry.AuditAndResourceContract,
                    entry.RollbackContract,
                    entry.SourceParityContract,
                    entry.ReminderBoundary,
                    entry.RequiredFutureTestAnchor,
                    entry.LockedScope,
                    entry.ClosureStatus
                }.Concat(entry.CandidateCardNos)
                    .Concat(entry.DocAnchors)));

        Assert.Contains("Acceptance contract only", combinedText, StringComparison.Ordinal);
        Assert.Contains("future B implementation / verifier evidence", combinedText, StringComparison.Ordinal);
        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.Contains("RESOURCE_SKILLS", combinedText, StringComparison.Ordinal);
        Assert.Contains("frontend runtime", combinedText, StringComparison.Ordinal);
        Assert.Contains("fullOfficial / READY", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("fullOfficial / READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifestMatchesNonLegendGateSet()
    {
        var nonLegendDeferredCardNos = DeferredResourceSkillFamilyManifest
            .Where(entry => string.Equals(entry.Classification, DeferredNonLegendResourceSkillRuntimeVerifier, StringComparison.Ordinal))
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var laneCardNos = DeferredNonLegendResourceSkillRuntimeLaneManifest
            .Select(entry => entry.CardNo)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["OGS·014/024"], laneCardNos);
        Assert.Equal(nonLegendDeferredCardNos, laneCardNos);
        Assert.Single(laneCardNos);
        Assert.Empty(DeferredNonLegendResourceSkillRuntimeLaneManifest
            .GroupBy(entry => entry.CardNo, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));
        Assert.Empty(DeferredNonLegendResourceSkillRuntimeLaneManifest
            .Where(entry => entry.RequiredFutureTestAnchor.Contains("LEGEND_ACT", StringComparison.Ordinal))
            .Select(entry => entry.LaneId));
    }

    [Fact]
    public void PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifestRequiresPromptCommandAuditLifetimeAndRollback()
    {
        Assert.All(DeferredNonLegendResourceSkillRuntimeLaneManifest, entry =>
        {
            Assert.Equal(DeferredNonLegendResourceSkillRuntimeLane, entry.Classification);
            Assert.Contains(entry.CardNo, DeferredResourceSkillFamilyManifest.Select(candidate => candidate.CardNo));
            Assert.Contains("prompt", entry.RequiredPromptCondition, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("server-filtered", entry.RequiredPromptCondition, StringComparison.Ordinal);
            Assert.Contains("Command", entry.RequiredCommandGuard, StringComparison.Ordinal);
            Assert.Contains("revalidate", entry.RequiredCommandGuard, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Audit", entry.RequiredAuditAndLifetime, StringComparison.Ordinal);
            Assert.True(
                entry.RequiredAuditAndLifetime.Contains("spend", StringComparison.OrdinalIgnoreCase)
                || entry.RequiredAuditAndLifetime.Contains("cleanup", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("no-mutation", entry.RequiredRollback, StringComparison.Ordinal);
            Assert.Contains("focused", entry.RequiredFutureTestAnchor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LEGEND_ACT bridge rows", entry.ForbiddenScope, StringComparison.Ordinal);
            Assert.Contains("card matrix JSON", entry.ForbiddenScope, StringComparison.Ordinal);
            Assert.Contains("riftbound-dotnet.sln", entry.ForbiddenScope, StringComparison.Ordinal);
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("P0-005 remains open", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.Contains("RESOURCE_SKILLS closure", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.NotEmpty(entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifestKeepsOneRemainingAcceptanceLaneDistinct()
    {
        var laneIds = DeferredNonLegendResourceSkillRuntimeLaneManifest
            .Select(entry => entry.LaneId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "LANE_LUX_SPELL_ONLY_TAP_REACTION_RESOURCE_SKILL"
            ],
            laneIds);

        Assert.Contains(DeferredNonLegendResourceSkillRuntimeLaneManifest, entry =>
            entry.CardNo == "OGS·014/024"
            && entry.RequiredPromptCondition.Contains("pending spell", StringComparison.OrdinalIgnoreCase)
            && entry.RequiredRollback.Contains("Non-spell payment", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifestDoesNotClaimReadyOrFullOfficial()
    {
        var combinedText = string.Join(
            " ",
            DeferredNonLegendResourceSkillRuntimeLaneManifest.SelectMany(entry =>
                new[]
                {
                    entry.LaneId,
                    entry.CardNo,
                    entry.Classification,
                    entry.CandidateFamily,
                    entry.CurrentEvidenceStatus,
                    entry.RequiredPromptCondition,
                    entry.RequiredCommandGuard,
                    entry.RequiredAuditAndLifetime,
                    entry.RequiredRollback,
                    entry.RequiredFutureTestAnchor,
                    entry.ForbiddenScope,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("NOT READY", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.Contains("fullOfficial / READY", combinedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fullOfficial=true", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "READY",
            combinedText
                .Replace("NOT READY", string.Empty, StringComparison.Ordinal)
                .Replace("fullOfficial / READY", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineTargetTaxActivatedAbilityMatrixCoversEveryTargetAbilityAndDimension()
    {
        var requiredAbilityIds = TargetColoredActivatedAbilityCoverageManifest
            .Select(entry => entry.AbilityId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var requiredDimensions = TargetTaxActivatedAbilityMatrixDimensionProfiles
            .Select(entry => entry.Dimension)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            requiredAbilityIds,
            TargetTaxActivatedAbilityMatrixManifest.Select(entry => entry.AbilityId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(
            requiredDimensions,
            TargetTaxActivatedAbilityMatrixManifest.Select(entry => entry.Dimension).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(requiredAbilityIds.Length * requiredDimensions.Length, TargetTaxActivatedAbilityMatrixManifest.Length);
        Assert.Equal(48, TargetTaxActivatedAbilityMatrixManifest.Length);
        Assert.Empty(TargetTaxActivatedAbilityMatrixManifest
            .GroupBy(entry => (entry.AbilityId, entry.Dimension))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));

        Assert.All(TargetTaxActivatedAbilityMatrixManifest, entry => Assert.Equal("ACTIVATE_ABILITY", entry.ActionWindow));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "PLAY_CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "PAY_COST", StringComparison.Ordinal));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "TRIGGER_PAYMENT", StringComparison.Ordinal));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "BATTLEFIELD_HELD_SCORE_PAYMENT", StringComparison.Ordinal));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "HIDE_CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(TargetTaxActivatedAbilityMatrixManifest, entry => string.Equals(entry.ActionWindow, "LEGEND_ACT", StringComparison.Ordinal));
    }

    [Fact]
    public void PaymentEngineTargetTaxActivatedAbilityMatrixRequiresBoundTargetPaymentAuditRollbackAndDocFields()
    {
        Assert.All(TargetTaxActivatedAbilityMatrixManifest, entry =>
        {
            Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(entry.AbilityId, out var definition));

            Assert.Equal(TargetTaxActivatedAbilityMatrix, entry.Classification);
            Assert.Equal("TARGET_BEARING_COLORED_ACTIVATED_ABILITIES", entry.ResidualBlockerFamily);
            Assert.Contains("TARGET_TAXES", entry.OfficialResidualAxes, StringComparison.Ordinal);
            Assert.Contains(entry.AbilityId, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.Contains(entry.Dimension, entry.MatrixRowId, StringComparison.Ordinal);
            Assert.False(definition.IsResourceSkill);
            Assert.False(string.IsNullOrWhiteSpace(entry.SourceTimingScope));
            Assert.False(string.IsNullOrWhiteSpace(entry.TargetProfile));
            Assert.False(string.IsNullOrWhiteSpace(entry.PaymentProfile));
            Assert.False(string.IsNullOrWhiteSpace(entry.TargetTaxOrOptionalBranchProfile));
            Assert.Contains("prompt", entry.PromptQuote, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("command", entry.CommandRevalidation, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                entry.AuditExpectation.Contains("COST_PAID", StringComparison.Ordinal)
                || entry.AuditExpectation.Contains("ABILITY_ACTIVATED", StringComparison.Ordinal));
            Assert.Contains("no-mutation", entry.RollbackExpectation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("card matrix", entry.OfficialClosureTrace, StringComparison.OrdinalIgnoreCase);
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

            Assert.NotEmpty(entry.DocAnchors);
            Assert.All(entry.DocAnchors, anchor =>
            {
                Assert.StartsWith("docs/", anchor, StringComparison.Ordinal);
                Assert.EndsWith(".md", anchor, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public void PaymentEngineTargetTaxActivatedAbilityMatrixLinksBackToTargetManifestSpellshieldAnd03BRDocs()
    {
        var abilityIds = TargetColoredActivatedAbilityCoverageManifest.Select(entry => entry.AbilityId).ToHashSet(StringComparer.Ordinal);
        var abilityDocsById = TargetColoredActivatedAbilityCoverageManifest.ToDictionary(entry => entry.AbilityId, entry => entry.DocAnchors, StringComparer.Ordinal);
        var spellshieldTaxAbilityIds = SpellshieldTaxCoverageManifest.Select(entry => entry.AbilityId).ToHashSet(StringComparer.Ordinal);

        Assert.All(TargetTaxActivatedAbilityMatrixManifest, entry =>
        {
            Assert.Contains(entry.AbilityId, abilityIds);
            Assert.Contains("docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_AUDIT.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_EVIDENCE.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_HANDOFF.md", entry.DocAnchors);
            Assert.Contains("docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md", entry.DocAnchors);
            Assert.All(abilityDocsById[entry.AbilityId], docAnchor => Assert.Contains(docAnchor, entry.DocAnchors));

            if (spellshieldTaxAbilityIds.Contains(entry.AbilityId))
            {
                Assert.Contains("SpellshieldTaxCoverageManifest", entry.TargetTaxOrOptionalBranchProfile, StringComparison.Ordinal);
                Assert.Contains("docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md", entry.DocAnchors);
            }
        });
    }

    [Fact]
    public void PaymentEngineTargetTaxActivatedAbilityMatrixKeepsResidualAxesAndBreadthExecutable()
    {
        var combinedText = string.Join(
            " ",
            TargetTaxActivatedAbilityMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.Classification,
                    entry.ResidualBlockerFamily,
                    entry.OfficialResidualAxes,
                    entry.ActionWindow,
                    entry.Dimension,
                    entry.SourceTimingScope,
                    entry.TargetProfile,
                    entry.PaymentProfile,
                    entry.TargetTaxOrOptionalBranchProfile,
                    entry.PromptQuote,
                    entry.CommandRevalidation,
                    entry.AuditExpectation,
                    entry.RollbackExpectation,
                    entry.OfficialClosureTrace,
                    entry.RemainingOfficialBreadth
                }));

        Assert.Contains("TARGET_BEARING_COLORED_ACTIVATED_ABILITIES", combinedText, StringComparison.Ordinal);
        Assert.Contains("TARGET_TAXES", combinedText, StringComparison.Ordinal);
        Assert.Contains("PAYMENT_SOURCES", combinedText, StringComparison.Ordinal);
        Assert.Contains("OPTIONAL_EXTRA_ALTERNATIVE_COSTS", combinedText, StringComparison.Ordinal);
        Assert.Contains("prompt", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("command", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COST_PAID", combinedText, StringComparison.Ordinal);
        Assert.Contains("ABILITY_ACTIVATED", combinedText, StringComparison.Ordinal);
        Assert.Contains("rollback", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SpellshieldTaxCoverageManifest", combinedText, StringComparison.Ordinal);
        Assert.Contains("target-scoped optional", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("card matrix", combinedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaymentEngineTargetTaxActivatedAbilityMatrixDoesNotClaimP0005Closure()
    {
        var combinedText = string.Join(
            " ",
            TargetTaxActivatedAbilityMatrixManifest.SelectMany(entry =>
                new[]
                {
                    entry.MatrixRowId,
                    entry.AbilityId,
                    entry.Classification,
                    entry.ResidualBlockerFamily,
                    entry.OfficialResidualAxes,
                    entry.ActionWindow,
                    entry.Dimension,
                    entry.SourceTimingScope,
                    entry.TargetProfile,
                    entry.PaymentProfile,
                    entry.TargetTaxOrOptionalBranchProfile,
                    entry.PromptQuote,
                    entry.CommandRevalidation,
                    entry.AuditExpectation,
                    entry.RollbackExpectation,
                    entry.OfficialClosureTrace,
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
                .Replace("CANNOT_READY", string.Empty, StringComparison.Ordinal)
                .Replace("READY_UNIT", string.Empty, StringComparison.Ordinal)
                .Replace("HASTE_READY", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentEngineRemainingOfficialClosureGateManifestListsFreshDispatchGatesExactlyOnce()
    {
        var requiredGateIds = new[]
        {
            "B_PAYMENT_ENGINE_OFFICIAL_BREADTH",
            "E_CARD_MATRIX_READINESS",
            "D_COMPLETION_P0_AUDIT"
        };

        Assert.Equal(
            requiredGateIds.Order(StringComparer.Ordinal),
            RemainingOfficialClosureGateManifest.Select(entry => entry.GateId).Order(StringComparer.Ordinal));
        Assert.Empty(RemainingOfficialClosureGateManifest
            .GroupBy(entry => entry.GateId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key));

        Assert.All(RemainingOfficialClosureGateManifest, entry =>
        {
            Assert.Equal(RemainingOfficialClosureGate, entry.Classification);
            Assert.Contains("Fresh A dispatch", entry.WriteLockRequirement, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(entry.RequiredFutureEvidence));
            Assert.Contains("NOT READY", entry.ClosureStatus, StringComparison.Ordinal);
            Assert.NotEmpty(entry.DocAnchors);
        });
    }

    [Fact]
    public void PaymentEngineRemainingOfficialClosureGateRejectsRepresentativeProxyCompletion()
    {
        var combinedText = string.Join(
            " ",
            RemainingOfficialClosureGateManifest.SelectMany(entry =>
                new[]
                {
                    entry.GateId,
                    entry.Owner,
                    entry.Classification,
                    entry.WriteLockRequirement,
                    entry.RequiredFutureEvidence,
                    entry.RepresentativeProxyEvidence,
                    entry.LockedScope,
                    entry.ClosureStatus
                }.Concat(entry.DocAnchors)));

        Assert.Contains("4D-03BR-B", combinedText, StringComparison.Ordinal);
        Assert.Contains("backend full", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chrome smoke", combinedText, StringComparison.Ordinal);
        Assert.Contains("formal 18", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("representative proxy evidence only", combinedText, StringComparison.Ordinal);
        Assert.Contains("P0-005 remains open", combinedText, StringComparison.Ordinal);
        Assert.Contains("full-card matrix", combinedText, StringComparison.Ordinal);
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
    public void PaymentEngineRemainingOfficialClosureGateReadsMatrixAsNotFullOfficial()
    {
        var matrixPath = Path.Combine(ResolveRepositoryRoot(), "docs", "CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json");
        using var document = JsonDocument.Parse(File.ReadAllText(matrixPath));

        var root = document.RootElement;
        var sourceCatalog = root.GetProperty("sourceCatalog");
        Assert.Equal("data/official/card-catalog.zh-CN.json", sourceCatalog.GetProperty("path").GetString());
        Assert.Equal("2026-04-27", sourceCatalog.GetProperty("fetchedAt").GetString());
        Assert.Equal(1009, sourceCatalog.GetProperty("snapshotEntries").GetInt32());
        Assert.Equal(811, sourceCatalog.GetProperty("functionalUnits").GetInt32());

        var snapshotEntries = root.GetProperty("snapshotEntries").EnumerateArray().ToArray();
        var functionalUnits = root.GetProperty("functionalUnits").EnumerateArray().ToArray();
        Assert.Equal(1009, snapshotEntries.Length);
        Assert.Equal(811, functionalUnits.Length);

        var fullOfficialFunctionalUnits = functionalUnits
            .Count(unit => unit.GetProperty("stage4B").GetProperty("fullOfficial").GetBoolean());
        Assert.Equal(0, fullOfficialFunctionalUnits);

        var freeze = root.GetProperty("stage4BCardCoverageFreeze");
        Assert.False(freeze.GetProperty("ready").GetBoolean());
        var uncoveredSummary = freeze.GetProperty("uncoveredSummary");
        Assert.Equal(0, uncoveredSummary.GetProperty("fullOfficialFunctionalUnits").GetInt32());
        Assert.Equal(0, uncoveredSummary.GetProperty("fullOfficialSnapshotEntries").GetInt32());
        Assert.Equal(811, uncoveredSummary.GetProperty("fullOfficialUncoveredFunctionalUnitIds").GetArrayLength());
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

    private sealed record PaymentEngineResourceSkillOfficialBreadthEntry(
        string CardNo,
        string Classification,
        string ResidualBlockerFamily,
        string OfficialResidualAxis,
        string OfficialResourceProfile,
        string CurrentEvidenceStatus,
        string RequiredFutureEvidence,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineDeferredResourceSkillFamilyEntry(
        string CardNo,
        string Classification,
        string ResidualBlockerFamily,
        string OfficialResidualAxis,
        string CurrentActionDomain,
        string FamilySplit,
        string OfficialResourceProfile,
        string CurrentEvidenceStatus,
        string RequiredFutureWork,
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
            .Concat(RollbackFailureAllWindowMatrixManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(CrossWindowGenerationConsumptionRowManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(CardMatrixAlignmentRowManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(CardMatrixAlignmentAllWindowMatrixManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(PaymentEngineOfficialMatrixDownstreamAggregateManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(LegendBattlefieldTriggerResourceActionManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(KeywordPaymentBranchManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(KeywordPaymentBranchAllWindowMatrixManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(ResourceSkillAllWindowMatrixManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(ResourceSkillOfficialBreadthManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(DeferredResourceSkillFamilyManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(LegendResourceBridgeAggregateManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(LegendResourceBridgeImplementationAcceptanceManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(TargetTaxActivatedAbilityMatrixManifest.SelectMany(entry => entry.DocAnchors))
            .Concat(RemainingOfficialClosureGateManifest.SelectMany(entry => entry.DocAnchors));
    }

    private static string[] GetFixedOfficialResourceSkillCandidateCardNos()
    {
        var catalogPath = Path.Combine(ResolveRepositoryRoot(), "data", "official", "card-catalog.zh-CN.json");
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));

        return document.RootElement.GetProperty("cards").EnumerateArray()
            .Where(card =>
            {
                var effectText = card.GetProperty("cardEffect").GetString() ?? string.Empty;
                return effectText.Contains("{{获得}}", StringComparison.Ordinal)
                    && effectText.Contains("获得费用资源的技能无法成为其他法术的反应目标", StringComparison.Ordinal);
            })
            .Select(card => card.GetProperty("cardNo").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
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

    private sealed record RollbackFailureMatrixActionWindowProfile(
        string ActionWindow,
        string PaymentSourceKind,
        string PromptQuote,
        string CommandRejection,
        string NoMutationAssertion,
        string AuditExpectation,
        string DocAnchor);

    private sealed record RollbackFailureMatrixFailureProfile(
        string FailureFamily,
        string FailureDimension,
        string PaymentSourceKind,
        string PromptQuote,
        string CommandRejection,
        string NoMutationAssertion,
        string AuditExpectation,
        string DocAnchor);

    private sealed record PaymentEngineRollbackFailureAllWindowMatrixEntry(
        string MatrixRowId,
        string FailureFamily,
        string Classification,
        string OfficialMatrixRowId,
        string ActionWindow,
        string FailureDimension,
        string PaymentSourceKind,
        string PromptQuote,
        string CommandRejection,
        string NoMutationAssertion,
        string AuditExpectation,
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

    private sealed record CrossWindowGenerationConsumptionMatrixActionWindowProfile(
        string ActionWindow,
        string GenerationScope,
        string ConsumptionScope,
        string ResourceLifetimeDimension,
        string PromptQuote,
        string CommandCommitOrRejectionAnchor,
        string AuditExpectation,
        string LifetimeNoMutationRestrictionAssertion,
        string DocAnchor);

    private sealed record CrossWindowGenerationConsumptionMatrixFamilyProfile(
        string Family,
        string GenerationScope,
        string ConsumptionScope,
        string ResourceLifetimeDimension,
        string PromptQuote,
        string CommandCommitOrRejectionAnchor,
        string AuditExpectation,
        string LifetimeNoMutationRestrictionAssertion,
        string DocAnchor);

    private sealed record PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixEntry(
        string MatrixRowId,
        string Family,
        string Classification,
        string OfficialMatrixRowId,
        string ActionWindow,
        string GenerationScope,
        string ConsumptionScope,
        string ResourceLifetimeDimension,
        string PromptQuote,
        string CommandCommitOrRejectionAnchor,
        string AuditExpectation,
        string LifetimeNoMutationRestrictionAssertion,
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

    private sealed record CardMatrixAlignmentMatrixActionWindowProfile(
        string ActionWindow,
        string MatrixScope,
        string RepresentativeSurface,
        string PromptEvidenceAnchor,
        string CommandEvidenceAnchor,
        string AuditEvidenceAnchor,
        string MatrixSyncStatusAnchor,
        string FrontendSnapshotOrRuleSourceTrace,
        string DocAnchor);

    private sealed record PaymentEngineCardMatrixAlignmentAllWindowMatrixEntry(
        string MatrixRowId,
        string Family,
        string Classification,
        string OfficialMatrixRowId,
        string ActionWindow,
        string MatrixScope,
        string RepresentativeSurface,
        string PromptEvidenceAnchor,
        string CommandEvidenceAnchor,
        string AuditEvidenceAnchor,
        string MatrixSyncStatusAnchor,
        string FrontendSnapshotOrRuleSourceTrace,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineOfficialMatrixDownstreamAggregateEntry(
        string AggregateRowId,
        string OfficialMatrixRowId,
        string Axis,
        string Classification,
        string FamilyManifestName,
        string AllWindowManifestName,
        string FamilyClassification,
        string AllWindowClassification,
        int FamilyCount,
        int AllWindowMatrixCount,
        string AggregateScope,
        string PaymentSurfaceScope,
        string ExcludedPaymentSurfaces,
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

    private sealed record KeywordPaymentBranchMatrixActionWindowProfile(
        string ActionWindow,
        string PaymentSurfaceScope,
        string RepresentativeSurface,
        string PromptQuote,
        string CommandRevalidation,
        string AuditExpectation,
        string RollbackExpectation,
        string DocAnchor);

    private sealed record PaymentEngineKeywordPaymentBranchAllWindowMatrixEntry(
        string MatrixRowId,
        string Branch,
        string Classification,
        string ResidualBlockerFamily,
        string OfficialResidualAxes,
        string ActionWindow,
        string PaymentSurfaceScope,
        string RepresentativeSurface,
        string PromptQuote,
        string CommandRevalidation,
        string AuditExpectation,
        string RollbackExpectation,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record ResourceSkillMatrixActionWindowProfile(
        string ActionWindow,
        string PaymentSurfaceScope,
        string RepresentativeSurface,
        string ResourceLifecycleScope,
        string PromptQuote,
        string CommandRevalidation,
        string AuditExpectation,
        string GeneratedResourceRestriction,
        string RollbackExpectation,
        string DocAnchor);

    private sealed record PaymentEngineResourceSkillAllWindowMatrixEntry(
        string MatrixRowId,
        string Family,
        IReadOnlyList<string> AbilityIds,
        string Classification,
        string ResidualBlockerFamily,
        string OfficialResidualAxis,
        string ActionWindow,
        string PaymentSurfaceScope,
        string ResourceLifecycleScope,
        string RepresentativeSurface,
        string PromptQuote,
        string CommandRevalidation,
        string AuditExpectation,
        string GeneratedResourceRestriction,
        string RollbackExpectation,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record TargetTaxActivatedAbilityMatrixDimensionProfile(
        string Dimension,
        string SourceTimingScope,
        string TargetProfileScope,
        string PaymentProfileScope,
        string TargetTaxOrOptionalBranchProfile,
        string PromptQuote,
        string CommandRevalidation,
        string AuditExpectation,
        string RollbackExpectation,
        string OfficialClosureTrace,
        string DocAnchor);

    private sealed record PaymentEngineTargetTaxActivatedAbilityMatrixEntry(
        string MatrixRowId,
        string AbilityId,
        string Classification,
        string ResidualBlockerFamily,
        string OfficialResidualAxes,
        string ActionWindow,
        string Dimension,
        string SourceTimingScope,
        string TargetProfile,
        string PaymentProfile,
        string TargetTaxOrOptionalBranchProfile,
        string PromptQuote,
        string CommandRevalidation,
        string AuditExpectation,
        string RollbackExpectation,
        string OfficialClosureTrace,
        string RemainingOfficialBreadth,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineRemainingOfficialClosureGateEntry(
        string GateId,
        string Owner,
        string Classification,
        string WriteLockRequirement,
        string RequiredFutureEvidence,
        string RepresentativeProxyEvidence,
        string LockedScope,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineDeferredResourceSkillNextDispatchGateEntry(
        string GateId,
        string Owner,
        string Classification,
        string CandidateClassification,
        IReadOnlyList<string> CandidateCardNos,
        string RequiredFreshDispatch,
        string WriteLockScope,
        string ForbiddenScope,
        string RequiredFutureEvidence,
        string RepresentativeProxyEvidence,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors);

    private sealed record PaymentEngineLegendResourceBridgeAggregateEntry(
        string BridgeGroupId,
        string Champion,
        string AbilityId,
        IReadOnlyList<string> CandidateCardNos,
        string TimingRestriction,
        string GeneratedResourceProfile,
        string CurrentLegendEvidence,
        string RequiredFutureEvidence,
        IReadOnlyList<string> RequiredHandoffDocs,
        string ForbiddenScope,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors)
    {
        public string Classification => LegendResourceBridgeAggregateVerifier;
    }

    private sealed record PaymentEngineLegendResourceBridgeImplementationAcceptanceEntry(
        string BridgeGroupId,
        string Champion,
        string AbilityId,
        IReadOnlyList<string> CandidateCardNos,
        string PromptContract,
        string CommandContract,
        string AuditAndResourceContract,
        string RollbackContract,
        string SourceParityContract,
        string ReminderBoundary,
        string RequiredFutureTestAnchor,
        string LockedScope,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors)
    {
        public string Classification => LegendResourceBridgeImplementationAcceptance;
    }

    private sealed record PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneEntry(
        string LaneId,
        string CardNo,
        string CandidateFamily,
        string CurrentEvidenceStatus,
        string RequiredPromptCondition,
        string RequiredCommandGuard,
        string RequiredAuditAndLifetime,
        string RequiredRollback,
        string RequiredFutureTestAnchor,
        string ForbiddenScope,
        string ClosureStatus,
        IReadOnlyList<string> DocAnchors)
    {
        public string Classification => DeferredNonLegendResourceSkillRuntimeLane;
    }

    private static bool IsTargetColoredOrExperienceActivatedAbility(P4ActivatedAbilityDefinition definition)
    {
        return !definition.IsResourceSkill
            && (definition.RequiredTargetCount > 0
                || P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(definition).Count > 0
                || definition.ExperienceCost > 0
                || definition.AppliesSpellshieldTargetTax);
    }
}
