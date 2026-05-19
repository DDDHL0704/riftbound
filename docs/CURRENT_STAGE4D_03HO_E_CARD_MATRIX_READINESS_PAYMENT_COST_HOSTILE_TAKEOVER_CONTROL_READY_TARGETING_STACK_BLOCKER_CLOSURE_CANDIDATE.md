# 4D-03HO-E Payment-Cost Hostile Takeover Control-Ready Targeting-Stack Blocker Closure Candidate Evidence

4D-03HO-E records one row-level E_CARD_MATRIX_READINESS matrix blocker reduction for Hostile Takeover / 恶意收购 `SFD·202/221` / functionalUnit `FU-00ee09c2cc` / effect `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT`. It reuses existing Stage 4C-36 representative evidence for a valid enemy public battlefield unit target changing control and becoming ready, with service-authoritative guards for friendly battlefield units, enemy base units, stale units, face-down standby objects, battlefield equipment, battlefield spell objects, and battlefield rune objects.

This update removes only `NEEDS_ENGINE_SUPPORT` from the selected payment-cost row while preserving `NEEDS_FAQ_REVIEW`, `fullOfficial=false`, `ready=false`, and all broader active-goal gates.

## Matrix Delta

- input previous closure candidate manifest: `Post03HnCardMatrixReadinessPaymentCostBulletTimeDamagePowerSpentTargetingStackBlockerClosureCandidateManifest`
- classification: `post-03hn-e-card-matrix-readiness-payment-cost-hostile-takeover-control-ready-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03HN_PAYMENT_COST_HOSTILE_TAKEOVER_CONTROL_READY_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected row: `FU-00ee09c2cc` / `SFD·202/221` 恶意收购
- selected effect: `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT`
- `NEEDS_ENGINE_SUPPORT`: 303 -> 302
- primary residual: 182 -> 182
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 491 -> 490
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 201 -> 200
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328
- `NEEDS_FAQ_REVIEW`: 92 -> 92
- primary `NEEDS_FAQ_REVIEW`: 61 -> 61
- `fullOfficialTrue`: 0 -> 0
- `ready`: false -> false

## Non-Closure

- payment-cost blocker closure remains partially open
- B/D_ENGINE_SUPPORT payment-cost residual remains open
- A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open
- E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open
- E_CARD_MATRIX_READINESS remains open
- card matrix remains open
- P0-005 remains open
- P0-004 adjacency audit-sensitive remains open
- P1 remains open
- Hostile Takeover FAQ adjudication remains open
- complete control-change matrix remains open
- complete ready/exhausted/standby timing and end-turn cleanup matrix remains open
- complete battle/spell-duel lifecycle matrix remains open
- complete FEPR target order and target legality matrix remains open
- full PaymentEngine / PAY_COST matrix remains open
- FEPR / targeting-timing breadth remains open
- READY remains open

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- focused `PaymentEngineCoverageAuditTests`: 414/414 passed
- full `dotnet test Riftbound.slnx --no-restore`: 4985/4985 passed
- `git diff --check`: passed

Chrome smoke was not run because this slice has no frontend or browser-script change. The project remains **NOT READY**.
