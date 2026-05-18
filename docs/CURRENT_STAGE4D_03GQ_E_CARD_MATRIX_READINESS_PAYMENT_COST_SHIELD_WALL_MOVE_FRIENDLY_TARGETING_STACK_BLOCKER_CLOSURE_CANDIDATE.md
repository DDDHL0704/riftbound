# 4D-03GQ-E Payment-Cost Shield Wall Move-Friendly Targeting-Stack Blocker Closure Candidate

Status: OK FOR THIS SLICE / NOT FINAL

Scope: `E_CARD_MATRIX_READINESS_POST_03GP_PAYMENT_COST_SHIELD_WALL_MOVE_FRIENDLY_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`

Classification: `post-03gp-e-card-matrix-readiness-payment-cost-shield-wall-move-friendly-targeting-stack-blocker-closure-candidate`

Input previous closure candidate manifest: `Post03GpCardMatrixReadinessPaymentCostSecretArtMercyBoonTargetingStackBlockerClosureCandidateManifest`

Selected row:

- functionalUnit: `FU-a7fbef72ba`
- card: `SFD·043/221` 《禁军之墙》
- effect: `SHIELD_WALL_MOVE_ANY_FRIENDLY_BATTLEFIELD_UNITS_TO_BASE`
- partition: `bd-engine-support-payment-cost`
- matrix row query: `payment-cost`
- secondary row query: `payment-and-targeting-stack-timing`

Evidence basis:

- Stage 4C-87 already records Shield Wall representative move-any-friendly-battlefield-units-to-base evidence.
- `p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base.fixture.json` covers hand play, pay 2, stack pass-pass, selected friendly battlefield units moving to owner base.
- `ConformanceFixtureRunnerTests` covers enemy/base/repeated-target rejection without mutation.
- `CardBehaviorRegistry` registers `SFD·043/221` as direct card behavior with friendly battlefield target scope, min 0 target count, dynamic friendly battlefield max target count and move-to-base resolution.

Matrix effect:

- NEEDS_ENGINE_SUPPORT 327 -> 326
- primary residual 183 -> 182
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 515 -> 514
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 224 -> 223
- NEEDS_AUTOMATED_TEST_EVIDENCE residual remains 328
- NEEDS_FAQ_REVIEW residual remains 92
- primary FAQ residual remains 61
- fullOfficialTrue remains 0
- ready remains false

Non-closure:

This candidate is a one-row E_CARD_MATRIX_READINESS blocker reduction. It does not close complete multi-battlefield movement, standby / quick timing windows, FEPR, PaymentEngine, hidden-info / redaction matrix, P0/P1, full official PaymentEngine matrix closure, card matrix closure, final frontend validation, formal 18-step rerun or READY.

Validation: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed、focused `PaymentEngineCoverageAuditTests` 366/366 passed、current-head backend full `dotnet test Riftbound.slnx --no-restore` 4937/4937 passed、`git diff --check` passed.
