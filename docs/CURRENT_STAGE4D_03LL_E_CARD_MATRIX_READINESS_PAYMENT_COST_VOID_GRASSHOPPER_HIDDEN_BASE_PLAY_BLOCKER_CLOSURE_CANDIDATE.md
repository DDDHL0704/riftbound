# 4D-03LL-E Card Matrix Readiness Candidate

Scope: payment-cost Void Grasshopper hidden/base-play blocker closure candidate.

Selected row:
- functionalUnit: `FU-bf85fd8432`
- card: `SFD·010/221` 虚空蜢
- effect: `VOID_GRASSHOPPER_PLAY_UNIT`
- gate: `E_CARD_MATRIX_READINESS_POST_03LK_PAYMENT_COST_VOID_GRASSHOPPER_HIDDEN_BASE_PLAY_BLOCKER_CLOSURE_CANDIDATE`
- previous manifest: `Post03LkCardMatrixReadinessPaymentCostJaggedDirkEquipmentAssembleRedLayerBlockerClosureCandidateManifest`

Matrix impact:
- payment-cost functionalUnits: 360 -> 360
- payment-cost snapshotEntries: 446 -> 446
- NEEDS_ENGINE_SUPPORT: 203 -> 202
- primary residual: 140 -> 139
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 390 -> 389
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 125 -> 125
- NEEDS_AUTOMATED_TEST_EVIDENCE: 328 -> 328
- NEEDS_FAQ_REVIEW: 92 -> 92
- primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

Evidence:
- `CardBehaviorRegistry` maps `SFD·010/221` to `VOID_GRASSHOPPER_PLAY_UNIT`.
- `p2-preflight-play-void-grasshopper-vanilla-unit.fixture.json` verifies hand-play payment, zero-target stack resolution, and base unit placement as a 3-power `CARD_TYPE:UNIT` object.
- `rules-evidence-index.md`, `p2-rules-preflight.md`, and `CURRENT_P2_STATUS.md` record official unit play evidence and explicitly leave the non-hand cost-reduction branch open.

Non-closure:
- Void Grasshopper automated evidence disposition remains open.
- Non-hand cost-reduction branch remains open.
- Hidden-info / random-zone breadth remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- `fullOfficial` remains false.
- READY remains open.

Validation passed for 4D-03LL-E: matrix JSON valid; 03LL matrix and current-state guards 2/2; PaymentEngineCoverageAuditTests 593/593; vanilla unit play/target-rejection regression 305/305; adjacent prompt/payment/hidden/visibility/vanilla regression 659/659; backend full test 5164/5164; git diff --check passed.
