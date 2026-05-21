# 4D-03LF-E payment-cost Lux high-cost-spell trigger targeting-stack blocker closure candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03LE_PAYMENT_COST_LUX_HIGH_COST_SPELL_TRIGGER_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03le-e-card-matrix-readiness-payment-cost-lux-high-cost-spell-trigger-targeting-stack-blocker-closure-candidate
- Previous manifest: Post03LeCardMatrixReadinessPaymentCostAzirNoOptionalHasteFaqTargetingStackBlockerClosureCandidateManifest
- Selected functional unit: FU-f18a49e06d
- Selected cards: OGS·006/024 拉克丝
- Selected effects: OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 209 -> 208
- Primary residual: 146 -> 145
- Payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 397 -> 396
- Payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 130 -> 129
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- Primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Evidence Used
- Existing server-authored `OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT` definition in src/Riftbound.Engine/CardBehaviorRegistry.cs.
- Existing CoreRuleEngine Lux high-cost spell trigger and temporary power resolution support.
- Existing RealTriggerQueueTests coverage for Lux high-cost spell queue / resolution / until-end-of-turn behavior.
- Existing ConformanceFixtureRunnerTests fixture and source-unit target rejection coverage for `p2-preflight-play-ogs-lux-high-cost-spell-static.fixture.json`.
- Official card text remains from data/official/card-catalog.zh-CN.json; no live website data was fetched.

## Non Closure
- Lux automated evidence disposition remains open.
- Lux full trigger queue / APNAP ordering remains open.
- Lux paid-cost override matrix remains open.
- Lux until-end-of-turn cleanup / replacement-duration breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
- validation passed: matrix JSON valid (jq empty); 03LF matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 584/584; Lux/high-cost focused regression 250/250; adjacent prompt/payment/Lux/high-cost/trigger/cleanup/target/stack/battle regression 2457/2457; backend full 5155/5155; git diff --check passed.
