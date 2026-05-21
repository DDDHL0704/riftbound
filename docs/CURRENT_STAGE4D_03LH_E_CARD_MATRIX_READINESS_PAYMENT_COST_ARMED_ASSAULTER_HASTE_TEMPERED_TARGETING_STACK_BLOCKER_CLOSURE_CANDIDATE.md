# 4D-03LH-E payment-cost Armed Assaulter HASTE_READY / Tempered targeting-stack blocker closure candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03LG_PAYMENT_COST_ARMED_ASSAULTER_HASTE_TEMPERED_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03lg-e-card-matrix-readiness-payment-cost-armed-assaulter-haste-tempered-targeting-stack-blocker-closure-candidate
- Previous manifest: Post03LgCardMatrixReadinessPaymentCostLuxIntroLegendHighCostSpellDrawTargetingStackBlockerClosureCandidateManifest
- Selected functional unit: FU-de319633b8
- Selected cards: SFD·002/221 武装强袭者
- Selected effects: ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 207 -> 206
- Primary residual: 144 -> 143
- Payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 395 -> 394
- Payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 128 -> 127
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- Primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Evidence Used
- Existing server-authored `ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE` definition in src/Riftbound.Engine/CardBehaviorRegistry.cs.
- Existing CoreRuleEngine and CardEquipmentKeywordRules support for Armed Assaulter HASTE_READY / Tempered representative behavior.
- Existing ConformanceFixtureRunnerTests coverage through `P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter` and `p4-play-armed-assaulter-haste-ready.fixture.json`.
- Existing ArmedAssaulterHasteTemperedTests coverage for the 4D-04G same-command HASTE_READY + Tempered attach representative.
- Existing CardCatalogBaselineTests coverage for HASTE_READY and Tempered optional attach representative profile boundaries.
- Official card text remains from data/official/card-catalog.zh-CN.json; no live website data was fetched.

## Non Closure
- Armed Assaulter automated evidence disposition remains open.
- Armed Assaulter HASTE_READY exact red-resource breadth remains open.
- Armed Assaulter full Tempered / attach lifecycle breadth remains open.
- Armed Assaulter LayerEngine / continuous-effect breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
- validation passed: matrix JSON valid (jq empty); 03LH matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 586/586; Armed Assaulter/HASTE/Tempered focused regression 121/121; adjacent prompt/payment/equipment/target/stack/layer/hidden regression 2341/2341; backend full 5157/5157; git diff --check passed.
