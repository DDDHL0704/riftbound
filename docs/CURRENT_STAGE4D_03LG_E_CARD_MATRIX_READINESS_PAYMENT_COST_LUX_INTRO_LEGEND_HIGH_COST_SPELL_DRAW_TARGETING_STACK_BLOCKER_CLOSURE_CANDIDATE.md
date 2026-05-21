# 4D-03LG-E payment-cost Lux intro legend high-cost-spell draw targeting-stack blocker closure candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03LF_PAYMENT_COST_LUX_INTRO_LEGEND_HIGH_COST_SPELL_DRAW_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03lf-e-card-matrix-readiness-payment-cost-lux-intro-legend-high-cost-spell-draw-targeting-stack-blocker-closure-candidate
- Previous manifest: Post03LfCardMatrixReadinessPaymentCostLuxHighCostSpellTriggerTargetingStackBlockerClosureCandidateManifest
- Selected functional unit: FU-cd03f0b576
- Selected cards: OGS·021/024 光辉女郎
- Selected effects: LEGEND_ACTION_DOMAIN

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 208 -> 207
- Primary residual: 145 -> 144
- Payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 396 -> 395
- Payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 129 -> 128
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- Primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Evidence Used
- Existing `LEGEND_ACTION_DOMAIN` mapping in src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs for OGS·021/024.
- Existing CoreRuleEngine `LuxIntroLegendCardNo` high-cost spell draw path and trigger resolution support.
- Existing ConformanceFixtureRunnerTests coverage through `P79LegendTriggerLuxDrawsWhenControllerPlaysHighCostSpell`.
- Existing CardCatalogBaselineTests implemented legend-action spec coverage.
- Official card text remains from data/official/card-catalog.zh-CN.json; no live website data was fetched.

## Non Closure
- Lux intro legend automated evidence disposition remains open.
- Lux intro legend full trigger queue / APNAP ordering remains open.
- Lux intro legend paid-cost override matrix remains open.
- Lux intro legend hidden-info / random-zone breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
- validation passed: matrix JSON valid (jq empty); 03LG matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 585/585; Lux-intro/legend focused regression 38/38; adjacent prompt/payment/legend/Lux/high-cost/draw/target/stack/hidden regression 2475/2475; backend full 5156/5156; git diff --check passed.
