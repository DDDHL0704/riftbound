# 4D-03KM-E Card Matrix Readiness Payment-Cost Ownerless Treasure Equipment Hidden/Cleanup Blocker Closure Candidate

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-c85e993e85 / OGN·186/298 / 无主宝藏 / OWNERLESS_TREASURE_PLAY_EQUIPMENT`.

## Evidence

- `data/official/card-catalog.zh-CN.json` fixed snapshot contains `OGN·186/298` 无主宝藏 with leaves-play draw/call dormant rune text and a pay-and-tap self-destroy activated skill.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·186/298` to `OWNERLESS_TREASURE_PLAY_EQUIPMENT`, cost 2, zero targets, and `PlaysSourceToBaseAsEquipment: true`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ownerless-treasure-equipment.fixture.json` covers the current hand-play equipment path: pay 2, zero-target stack resolution, and source object entering the controller base as equipment.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-ownerless-treasure-target-rejected.fixture.json` covers explicit target rejection for the current zero-target play path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, and `docs/CURRENT_P4_STATUS.md` already record the rule evidence and deferred leaves-play trigger / self-destroy skill boundaries.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 228 -> 227.
- Primary payment-cost residual: 156 -> 155.
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 416 -> 415.
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 141 -> 141.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

## Non-Closure

This candidate does not close Ownerless Treasure automated evidence disposition, leaves-play draw/call dormant rune trigger, pay-and-tap self-destroy skill, cleanup / replacement duration breadth, hidden-info / random-zone breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 564/564; Ownerless Treasure focused 3021/3021; adjacent prompt/payment/equipment/target/stack 2149/2149; backend full 5135/5135; git diff --check passed.
