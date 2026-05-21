# 4D-03LK-E Card Matrix Readiness Candidate

Scope: payment-cost Jagged Dirk equipment / ASSEMBLE_RED layer blocker closure candidate.

Selected row:
- functionalUnit: `FU-9f4d9817c1`
- card: `SFD·009/221` 锯齿短匕
- effect: `JAGGED_DIRK_PLAY_EQUIPMENT`
- gate: `E_CARD_MATRIX_READINESS_POST_03LJ_PAYMENT_COST_JAGGED_DIRK_EQUIPMENT_ASSEMBLE_RED_LAYER_BLOCKER_CLOSURE_CANDIDATE`
- previous manifest: `Post03LjCardMatrixReadinessPaymentCostSentinelAdeptNoOptionalAssembleTemperedTargetingStackBlockerClosureCandidateManifest`

Matrix impact:
- payment-cost functionalUnits: 360 -> 360
- payment-cost snapshotEntries: 446 -> 446
- NEEDS_ENGINE_SUPPORT: 204 -> 203
- primary residual: 141 -> 140
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 391 -> 390
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 125 -> 125
- NEEDS_AUTOMATED_TEST_EVIDENCE: 328 -> 328
- NEEDS_FAQ_REVIEW: 92 -> 92
- primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

Evidence:
- `CardBehaviorRegistry` maps `SFD·009/221` to `JAGGED_DIRK_PLAY_EQUIPMENT`.
- `p2-preflight-play-jagged-dirk-equipment.fixture.json` verifies zero-target play, payment, stack resolution, and base equipment placement.
- `p4-play-jagged-dirk-target-rejected.fixture.json` verifies explicit-target play rejection with no payment, movement, stack item, or mutation.
- `rules-evidence-index.md`, `p2-rules-preflight.md`, `CURRENT_P2_STATUS.md`, and `CURRENT_P4_STATUS.md` record official equipment play and ASSEMBLE_RED representative evidence.

Non-closure:
- Jagged Dirk automated evidence disposition remains open.
- Full equipment / attach lifecycle breadth remains open.
- LayerEngine / continuous-effect breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- `fullOfficial` remains false.
- READY remains open.

Validation passed for 4D-03LK-E: matrix JSON valid; 03LK matrix and current-state guards 2/2; PaymentEngineCoverageAuditTests 591/591; Jagged/Equipment/Assemble regression 420/420; adjacent prompt/payment/equipment/assemble/target/stack/layer regression 2247/2247; backend full test 5162/5162; git diff --check passed.
