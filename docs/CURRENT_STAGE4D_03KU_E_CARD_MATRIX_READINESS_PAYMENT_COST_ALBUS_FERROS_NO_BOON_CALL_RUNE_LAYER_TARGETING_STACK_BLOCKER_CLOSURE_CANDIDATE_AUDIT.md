4D-03KU-E audit: payment-cost Albus Ferros no-boon call-rune layer/targeting-stack blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本审计只确认 03KU 的单行矩阵 blocker-count reduction 候选边界；不关闭全量卡牌覆盖、FAQ evidence、formal 18-step E2E 或 completion audit。

Candidate identity:
- classification: `post-03kt-e-card-matrix-readiness-payment-cost-albus-ferros-no-boon-call-rune-layer-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03KT_PAYMENT_COST_ALBUS_FERROS_NO_BOON_CALL_RUNE_LAYER_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- manifest: `Post03KuCardMatrixReadinessPaymentCostAlbusFerrosNoBoonCallRuneLayerTargetingStackBlockerClosureCandidateManifest`
- matrix object: `stage4D03KuPaymentCostAlbusFerrosNoBoonCallRuneLayerTargetingStackBlockerClosureCandidate`
- previous manifest: `Post03KtCardMatrixReadinessPaymentCostGhostMatronSpiritReviveHiddenTargetingStackBlockerClosureCandidateManifest`

Selected row:
- functionalUnit: `FU-cead48a12d`
- card: `OGN·230/298` / `阿不思·菲罗斯`
- effect: `ALBUS_FERROS_NO_BOON_CALL_RUNE_PLAY_UNIT`
- row query: `payment-cost`
- secondary row query: `payment-and-targeting-stack-timing`

Count continuity:
- snapshot entries remain 1009.
- functional units remain 811.
- payment-cost functional units remain 360.
- payment-cost snapshot entries remain 446.
- NEEDS_ENGINE_SUPPORT 220 -> 219.
- primary residual 150 -> 149.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 408 -> 407.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 137 -> 136.
- NEEDS_AUTOMATED_TEST_EVIDENCE residual remains 328.
- NEEDS_FAQ_REVIEW residual remains 92.
- primary FAQ residual remains 61.
- fullOfficialTrue remains 0.
- ready remains false.

Evidence reviewed:
- Runtime behavior registry has the Albus Ferros direct card behavior.
- P2 static play fixture covers no-target hand play, 4-cost payment and 3-power unit base entry.
- P4 rejection fixture covers explicit-target rejection with no payment, no stack item and no unit entry.
- `rules-evidence-index.md` and `p2-rules-preflight.md` carry the current Albus Ferros evidence anchors.

Audit boundary:
- This is not a boon-consuming dormant-rune call branch closure.
- This is not an automated evidence disposition closure.
- This is not complete layer / continuous-effect closure.
- This is not complete FEPR target / stack lifecycle closure.
- This is not complete PaymentEngine / PAY_COST closure.
- This does not change frontend behavior, Chrome smoke scripts, runtime code, protocol fields or official catalog.
- This does not change fullOfficial or readiness.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 580/580; Albus Ferros focused 3021/3021; adjacent prompt/payment/layer/targeting-stack 1976/1976; backend full 5151/5151; git diff --check passed.
