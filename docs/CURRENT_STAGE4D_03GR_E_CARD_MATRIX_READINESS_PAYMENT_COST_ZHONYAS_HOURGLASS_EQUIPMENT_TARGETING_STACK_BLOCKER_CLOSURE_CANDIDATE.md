# 4D-03GR-E Card Matrix Readiness Payment-Cost Zhonyas Hourglass Equipment Targeting-Stack Blocker Closure Candidate

Status: NOT READY / row-level blocker reduction only.

This candidate records the thirty-fifth payment-cost blocker closure candidate after 4D-03GQ-E. `Post03GrCardMatrixReadinessPaymentCostZhonyasHourglassEquipmentTargetingStackBlockerClosureCandidateManifest` uses `Post03GqCardMatrixReadinessPaymentCostShieldWallMoveFriendlyTargetingStackBlockerClosureCandidateManifest` as the input previous closure candidate manifest.

Selected row:

- partition: `bd-engine-support-payment-cost`
- matrix row query: `payment-cost`
- secondary matrix row query: `payment-and-targeting-stack-timing`
- functionalUnit: `FU-fb79eea7fc`
- card: `OGN·077/298` 中娅沙漏
- effect: `ZHONYAS_HOURGLASS_PLAY_EQUIPMENT`
- classification: `post-03gq-e-card-matrix-readiness-payment-cost-zhonyas-hourglass-equipment-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03GQ_PAYMENT_COST_ZHONYAS_HOURGLASS_EQUIPMENT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`

Count impact:

- payment-cost functionalUnits: `360 -> 360`
- payment-cost snapshotEntries: `446 -> 446`
- payment-cost `NEEDS_ENGINE_SUPPORT`: `326 -> 325`
- primary residual: `182 -> 182`
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `514 -> 513`
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `223 -> 222`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: `328 -> 328`
- `NEEDS_FAQ_REVIEW`: `92 -> 92`
- primary FAQ residual: `61 -> 61`
- `fullOfficialTrue=0`
- `ready=false`

Evidence basis:

- Stage 4C-39 Zhonyas Hourglass representative play-equipment guard.
- `ZhonyasHourglassGuardTests`.
- `p2-preflight-play-zhonyas-hourglass-equipment.fixture.json`.
- `p4-play-zhonyas-hourglass-target-rejected.fixture.json`.

Non-closure:

This does not close payment-cost blocker closure, B/D engine-support residual, automated evidence residual, FAQ review residual, `E_CARD_MATRIX_READINESS`, card matrix, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, or READY.
