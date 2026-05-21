# 4D-03KZ-E: Teemo Legend Action-Domain FAQ/Hidden Targeting-Stack Blocker Closure Candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03KY_PAYMENT_COST_TEEMO_LEGEND_ACTION_DOMAIN_FAQ_HIDDEN_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03ky-e-card-matrix-readiness-payment-cost-teemo-legend-action-domain-faq-hidden-targeting-stack-blocker-closure-candidate
- Input previous closure candidate manifest: Post03KyCardMatrixReadinessPaymentCostLeeSinLegendActionDomainBlockerClosureCandidateManifest
- Selected FU: FU-ca921a56da
- Selected cards: OGN·263/298 + OGN·263a/298 + OGN·307*/298 + OGN·307/298 迅捷斥候
- Selected effect: LEGEND_ACTION_DOMAIN

## Evidence
- Official fixed snapshot text for Teemo states pay 1 and exhaust to return an owned Teemo unit from the hero zone or battlefield to hand.
- Server path uses LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT in LEGEND_ACT, not a frontend rule decision.
- Existing focused regression proves mana payment, legend exhaustion, owned Teemo unit targeting, return-to-hand resolution, and UNIT_RETURNED_TO_HAND event evidence.
- Existing Teemo standby replacement regression proves the server-authored alternate standby payment path and its hidden-info redaction boundary.
- P7.9 status records the accepted same-functional-unit OGN reprint group and the Teemo legend-action / standby-replacement slice.

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 215 -> 214.
- Primary residual: 148 -> 148.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 403 -> 402.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 134 -> 133.
- NEEDS_AUTOMATED_TEST_EVIDENCE remains 328.
- NEEDS_FAQ_REVIEW remains 92.
- Primary NEEDS_FAQ_REVIEW remains 61.
- fullOfficialTrue remains 0 and ready remains false.

## Validation
- Matrix JSON valid: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- 03KZ active-goal guard passed: 1/1.
- PaymentEngineCoverageAuditTests passed: 580/580.
- CardCatalogBaselineTests/P79LegendActTeemo/P79LegendTeemo focused regression passed: 78/78.
- Adjacent prompt/payment/Teemo/legend/standby/hidden regression passed: 885/885.
- Backend full test passed: 5151/5151.
- `git diff --check` passed.

## Non-Closure
Teemo automated evidence disposition, FAQ adjudication, hidden-info / random-zone breadth, standby replacement breadth, full legend-action official breadth, complete non-play-domain representative matrix, complete priority / stack timing matrix, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix and formal 18-step E2E remain open.
