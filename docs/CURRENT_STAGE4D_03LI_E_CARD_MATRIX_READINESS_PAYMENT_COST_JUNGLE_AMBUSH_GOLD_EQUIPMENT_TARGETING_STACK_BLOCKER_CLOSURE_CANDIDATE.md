# 4D-03LI-E payment-cost Jungle Ambush Gold equipment targeting-stack blocker closure candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03LH_PAYMENT_COST_JUNGLE_AMBUSH_GOLD_EQUIPMENT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03lh-e-card-matrix-readiness-payment-cost-jungle-ambush-gold-equipment-targeting-stack-blocker-closure-candidate
- Previous manifest: Post03LhCardMatrixReadinessPaymentCostArmedAssaulterHasteTemperedTargetingStackBlockerClosureCandidateManifest
- Selected functional unit: FU-dd0c89c363
- Selected cards: SFD·004/221 丛林伏击
- Selected effects: JUNGLE_AMBUSH_CREATE_GOLD_EQUIPMENT

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 206 -> 205
- Primary residual: 143 -> 142
- Payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 394 -> 393
- Payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 127 -> 126
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- Primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Evidence Used
- Existing server-authored `JUNGLE_AMBUSH_CREATE_GOLD_EQUIPMENT` definition in src/Riftbound.Engine/CardBehaviorRegistry.cs.
- Existing CoreRuleEngine support for zero-target stack resolution and exhausted Gold equipment token creation.
- Existing ConformanceFixtureRunnerTests coverage through `CoreRuleEnginePlaysJungleAmbushCreatesGold` and `p2-preflight-play-jungle-ambush-create-gold.fixture.json`.
- Existing rules evidence in docs/rules-evidence-index.md, docs/p2-rules-preflight.md, and docs/CURRENT_P2_STATUS.md.
- Official card text remains from data/official/card-catalog.zh-CN.json; no live website data was fetched.

## Non Closure
- Jungle Ambush automated evidence disposition remains open.
- Jungle Ambush global active-entry effect remains open.
- Jungle Ambush standby / reaction timing breadth remains open.
- Jungle Ambush Gold token resource-skill lifecycle breadth remains open.
- Jungle Ambush cleanup / replacement-duration breadth remains open.
- Jungle Ambush hidden-info / random-zone breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
- validation passed: matrix JSON valid (jq empty); 03LI matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 587/587; Jungle Ambush/Gold focused regression 106/106; adjacent prompt/payment/equipment/gold/target/stack/hidden/redaction regression 2252/2252; backend full 5158/5158; git diff --check passed.
