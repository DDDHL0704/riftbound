4D-03KT-E audit: payment-cost Ghost Matron spirit-revive hidden/targeting-stack blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本审计只确认 03KT 的单行矩阵 blocker-count reduction 候选边界；不关闭全量卡牌覆盖、FAQ evidence、formal 18-step E2E 或 completion audit。

Candidate identity:
- classification: `post-03ks-e-card-matrix-readiness-payment-cost-ghost-matron-spirit-revive-hidden-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03KS_PAYMENT_COST_GHOST_MATRON_SPIRIT_REVIVE_HIDDEN_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- manifest: `Post03KtCardMatrixReadinessPaymentCostGhostMatronSpiritReviveHiddenTargetingStackBlockerClosureCandidateManifest`
- matrix object: `stage4D03KtPaymentCostGhostMatronSpiritReviveHiddenTargetingStackBlockerClosureCandidate`
- previous manifest: `Post03KsCardMatrixReadinessPaymentCostColdBloodedAristocratDestroyFriendlyUnitFaqCleanupBlockerClosureCandidateManifest`

Selected row:
- functionalUnit: `FU-ab6836e360`
- card: `OGN·226/298` / `幽灵主母`
- effect: `GHOST_MATRON_SPIRIT_REVIVE_PLAY_UNIT`
- row query: `payment-cost`
- secondary row query: `payment-and-targeting-stack-timing`

Count continuity:
- snapshot entries remain 1009.
- functional units remain 811.
- payment-cost functional units remain 360.
- payment-cost snapshot entries remain 446.
- NEEDS_ENGINE_SUPPORT 221 -> 220.
- primary residual 151 -> 150.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 409 -> 408.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 138 -> 137.
- NEEDS_AUTOMATED_TEST_EVIDENCE residual remains 328.
- NEEDS_FAQ_REVIEW residual remains 92.
- primary FAQ residual remains 61.
- fullOfficialTrue remains 0.
- ready remains false.

Evidence reviewed:
- Runtime behavior registry has the Ghost Matron direct card behavior.
- P2 static play fixture covers no-target hand play and Spirit unit base entry.
- P2 graveyard-unit fixture covers optional friendly graveyard unit selection and play-to-base.
- P4 rejection fixture covers illegal target rejection, including non-graveyard and high-cost / high-current-A cases.
- `rules-evidence-index.md` and `p2-rules-preflight.md` carry the current Ghost Matron evidence anchors.

Audit boundary:
- This is not a hidden-info breadth closure.
- This is not an automated evidence disposition closure.
- This is not complete optional graveyard-unit breadth closure.
- This is not complete FEPR target / stack lifecycle closure.
- This is not complete PaymentEngine / PAY_COST closure.
- This does not change frontend behavior, Chrome smoke scripts, runtime code, protocol fields or official catalog.
- This does not change fullOfficial or readiness.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 578/578; Ghost Matron focused 3021/3021; adjacent prompt/payment/hidden/targeting-stack 1920/1920; backend full 5149/5149; git diff --check passed.
