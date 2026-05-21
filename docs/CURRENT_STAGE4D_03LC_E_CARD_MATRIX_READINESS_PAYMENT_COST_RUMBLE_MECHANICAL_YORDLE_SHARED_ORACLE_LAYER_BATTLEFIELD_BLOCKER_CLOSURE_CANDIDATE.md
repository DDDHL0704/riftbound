# 4D-03LC-E payment-cost Rumble mechanical-yordle shared-oracle layer/battlefield blocker closure candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03LB_PAYMENT_COST_RUMBLE_MECHANICAL_YORDLE_SHARED_ORACLE_LAYER_BATTLEFIELD_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03lb-e-card-matrix-readiness-payment-cost-rumble-mechanical-yordle-shared-oracle-layer-battlefield-blocker-closure-candidate
- Previous manifest: Post03LbCardMatrixReadinessPaymentCostHiranaMonasteryBattlefieldBoonDrawHiddenLayerTargetingStackBlockerClosureCandidateManifest
- Selected functional unit: FU-1963543202
- Selected cards: SFD·026/221 + SFD·026a/221 兰博
- Selected effects: SFD_RUMBLE_ALT_A_MECHANICAL_YORDLE_PLAY_UNIT;SFD_RUMBLE_MECHANICAL_YORDLE_PLAY_UNIT

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 212 -> 211
- Primary residual: 146 -> 146
- Payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 400 -> 399
- Payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 132 -> 131
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- Primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Evidence Used
- Existing SFD Rumble source-unit behavior entries in src/Riftbound.Engine/CardBehaviorRegistry.cs.
- Existing source-unit fixture coverage for SFD·026/221 and SFD·026a/221 in tests/Riftbound.ConformanceTests/Fixtures.
- Existing ConformanceFixtureRunnerTests coverage for base play, stack resolution, zero-target rejection, source-unit power, and mechanical/yordle tags.
- Official card text remains from data/official/card-catalog.zh-CN.json; no live website data was fetched.

## Non Closure
- Rumble automated evidence disposition remains open.
- Rumble mechanical static Assault grant remains open.
- Rumble conquer recycle branch remains open.
- Rumble graveyard mechanical play / cost reduction branch remains open.
- Rumble complete LayerEngine / continuous-effect breadth remains open.
- Rumble complete battlefield / control / conquer lifecycle remains open.
- Rumble complete battle / spell-duel lifecycle remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
- validation passed: matrix JSON valid (jq empty); 03LC active-goal guard 1/1; PaymentEngineCoverageAuditTests 581/581; Rumble focused regression 462/462; adjacent prompt/payment/layer/battlefield/target/stack regression 2277/2277; backend full 5152/5152; git diff --check passed.
