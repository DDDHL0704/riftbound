# 4D-03LE-E payment-cost Azir no-optional-haste FAQ targeting-stack blocker closure candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03LD_PAYMENT_COST_AZIR_NO_OPTIONAL_HASTE_FAQ_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03ld-e-card-matrix-readiness-payment-cost-azir-no-optional-haste-faq-targeting-stack-blocker-closure-candidate
- Previous manifest: Post03LdCardMatrixReadinessPaymentCostMissFortuneLegendActionDomainRoamSharedOracleBlockerClosureCandidateManifest
- Selected functional unit: FU-c1f964b654
- Selected cards: SFD·177/221 + SFD·177a/221 阿兹尔
- Selected effects: AZIR_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE;AZIR_PLAY_UNIT_NO_OPTIONAL_HASTE

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 210 -> 209
- Primary residual: 146 -> 146
- Payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 398 -> 397
- Payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 131 -> 130
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- Primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Evidence Used
- Existing server-authored `AZIR_PLAY_UNIT_NO_OPTIONAL_HASTE` and `AZIR_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE` definitions in src/Riftbound.Engine/CardBehaviorRegistry.cs.
- Existing ConformanceFixtureRunnerTests preflight coverage for `p2-preflight-play-azir-no-optional-haste.fixture.json` and `p2-preflight-play-azir-alt-a-no-optional-haste.fixture.json`.
- Existing HASTE_READY branch fixtures for `p4-play-azir-haste-ready.fixture.json` and `p4-play-azir-alt-a-haste-ready.fixture.json`.
- Official card text remains from data/official/card-catalog.zh-CN.json; no live website data was fetched.

## Non Closure
- Azir automated evidence disposition remains open.
- Azir FAQ adjudication remains open.
- Azir HASTE_READY exact yellow-resource breadth remains open.
- Azir attack-trigger token movement breadth remains open.
- Azir complete battle / spell-duel lifecycle remains open.
- Azir complete battlefield / control movement breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
- validation passed: matrix JSON valid (jq empty); 03LE matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 583/583; Azir/haste focused regression 116/116; adjacent prompt/payment/Azir/haste/move/target/stack/battle regression 2524/2524; backend full 5154/5154; git diff --check passed.
