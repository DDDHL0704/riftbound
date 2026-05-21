# 4D-03KL-E Card Matrix Readiness Payment-Cost Siren Equipment Control Blocker Closure Candidate

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-071b942b0a / OGN·184/298 / 塞壬号 / SIREN_PLAY_EQUIPMENT`.

## Evidence

- `data/official/card-catalog.zh-CN.json` fixed snapshot contains `OGN·184/298` 塞壬号 with equipment text that includes pay-and-tap movement from battlefield to owner base.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·184/298` to `SIREN_PLAY_EQUIPMENT`, cost 2, zero targets, and `PlaysSourceToBaseAsEquipment: true`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-siren-equipment.fixture.json` covers the current hand-play equipment path: pay 2, zero-target stack resolution, and source object entering the controller base as equipment.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-siren-target-rejected.fixture.json` covers explicit target rejection for the current zero-target play path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, and `docs/CURRENT_P4_STATUS.md` already record the rule evidence and the deferred pay-and-tap move skill.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 229 -> 228.
- Primary payment-cost residual: 157 -> 156.
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 417 -> 416.
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 141 -> 141.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

## Non-Closure

This candidate does not close Siren automated evidence disposition, pay-and-tap move skill, control-zone movement breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 562/562; Siren focused 3021/3021; adjacent prompt/payment/equipment/target/stack 2147/2147; backend full 5133/5133; git diff --check passed.
