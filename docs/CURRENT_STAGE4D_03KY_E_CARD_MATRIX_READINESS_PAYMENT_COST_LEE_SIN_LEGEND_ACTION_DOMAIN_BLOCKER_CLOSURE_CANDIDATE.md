# 4D-03KY-E: Lee Sin Legend Action-Domain Blocker Closure Candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03KX_PAYMENT_COST_LEE_SIN_LEGEND_ACTION_DOMAIN_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03kx-e-card-matrix-readiness-payment-cost-lee-sin-legend-action-domain-blocker-closure-candidate
- Input previous closure candidate manifest: Post03KxCardMatrixReadinessPaymentCostDariusLegendResourceBridgeBlockerClosureCandidateManifest
- Selected FU: FU-9790ed5fde
- Selected cards: OGN·257/298 + OGN·304*/298 + OGN·304/298 盲僧
- Selected effect: LEGEND_ACTION_DOMAIN

## Evidence
- Official fixed snapshot text for Lee Sin states pay 1 and exhaust to grant a boon to a friendly unit.
- Server path uses LEGEND_PAY_1_EXHAUST_GRANT_BOON in LEGEND_ACT, not a frontend rule decision.
- Existing focused regression proves mana payment, legend exhaustion, friendly-unit target handling, +1 power boon state and BOON_GRANTED event.
- P7.9 status records the accepted same-functional-unit OGN reprint group.

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 216 -> 215.
- Primary residual: 148 -> 148.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 404 -> 403.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 134 -> 134.
- fullOfficialTrue remains 0 and ready remains false.

## Validation
- Matrix JSON valid: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- 03KY active-goal guard passed: 1/1.
- PaymentEngineCoverageAuditTests passed: 580/580.
- LeeSin/LegendAct/LEGEND_ACT focused regression passed: 72/72.
- Adjacent prompt/payment/legend-action regression passed: 822/822.
- Backend full test passed: 5151/5151.
- `git diff --check` passed.

## Non-Closure
Lee Sin automated evidence disposition, full legend-action official breadth, boon layer / continuous-effect breadth, complete non-play-domain representative matrix, complete priority / stack timing matrix, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix and formal 18-step E2E remain open.
