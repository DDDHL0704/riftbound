# 4D-03LA-E: Guerrilla Warfare Standby/Hidden Cleanup Blocker Closure Candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03KZ_PAYMENT_COST_GUERRILLA_WARFARE_STANDBY_HIDDEN_CLEANUP_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03kz-e-card-matrix-readiness-payment-cost-guerrilla-warfare-standby-hidden-cleanup-blocker-closure-candidate
- Input previous closure candidate manifest: Post03KzCardMatrixReadinessPaymentCostTeemoLegendActionDomainFaqHiddenTargetingStackBlockerClosureCandidateManifest
- Selected FU: FU-08830ca348
- Selected card: OGN·264/298 游击战
- Selected effect: GUERRILLA_WARFARE_RETURN_STANDBY_GRAVEYARD_TO_HAND

## Evidence
- Official fixed snapshot text for Guerrilla Warfare returns up to two friendly Standby cards from the discard pile and grants a same-turn free Standby hide permission.
- Server path uses GUERRILLA_WARFARE_RETURN_STANDBY_GRAVEYARD_TO_HAND as a direct card behavior, not a frontend rule decision.
- Existing focused regression proves friendly graveyard Standby target return-to-hand, non-Standby target rejection, FREE_STANDBY_HIDE until-end-of-turn permission, and STANDBY_FREE hide-card payment evidence.
- Existing hidden information coverage keeps face-down Standby placement redacted to the opponent snapshot.
- P4 status records the Guerrilla Warfare free-Standby permission and rejection fixtures as accepted interaction-keyword coverage.

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 214 -> 213.
- Primary residual: 148 -> 147.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 402 -> 401.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 133 -> 133.
- NEEDS_AUTOMATED_TEST_EVIDENCE remains 328.
- NEEDS_FAQ_REVIEW remains 92.
- Primary NEEDS_FAQ_REVIEW remains 61.
- fullOfficialTrue remains 0 and ready remains false.

## Validation
- Matrix JSON valid: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- 03LA active-goal guard passed: 1/1.
- PaymentEngineCoverageAuditTests passed: 580/580.
- CardCatalogBaselineTests/GuerrillaWarfare/FreeStandby/Standby focused regression passed: 147/147.
- Adjacent prompt/standby/hidden/cleanup/priority-stack regression passed: 860/860.
- Backend full test passed: 5151/5151.
- `git diff --check` passed.

## Non-Closure
Guerrilla Warfare automated evidence disposition, full Standby / hidden-info / cleanup replacement breadth, complete zone-control movement matrix, complete PaymentEngine / PAY_COST matrix and formal 18-step E2E remain open.
