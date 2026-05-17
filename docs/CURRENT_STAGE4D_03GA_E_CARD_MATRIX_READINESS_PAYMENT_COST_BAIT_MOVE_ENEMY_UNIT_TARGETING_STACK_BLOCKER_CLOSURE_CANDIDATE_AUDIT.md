# 4D-03GA-E Card Matrix Readiness Audit

日期：2026-05-18
结论：**ACCEPTED / ROW-LEVEL ONLY / NOT READY**

本审计记录 4D-03GA-E payment-cost Bait move-enemy-unit targeting-stack blocker closure candidate。它只把 `FU-6bcef271ca / SFD·129/221 诱饵 / BAIT_MOVE_ENEMY_UNIT_TO_ANOTHER_ENEMY_UNIT_LOCATION_NO_ECHO` 这一 direct-card-behavior 代表行从 `NEEDS_ENGINE_SUPPORT` 收窄到 `IMPLEMENTED_UNTESTED`，并保留 `NEEDS_AUTOMATED_TEST_EVIDENCE`、`fullOfficial=false` 与 `ready=false`。

## Scope

- manifest: `Post03GaCardMatrixReadinessPaymentCostBaitMoveEnemyUnitTargetingStackBlockerClosureCandidateManifest`
- classification: `post-03fz-e-card-matrix-readiness-payment-cost-bait-move-enemy-unit-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03FZ_PAYMENT_COST_BAIT_MOVE_ENEMY_UNIT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- input previous closure candidate manifest: `Post03FzCardMatrixReadinessPaymentCostScarletRoseEquipmentReadyUnitTargetingStackBlockerClosureCandidateManifest`
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-6bcef271ca`
- selected card: `SFD·129/221 诱饵`
- selected effect: `BAIT_MOVE_ENEMY_UNIT_TO_ANOTHER_ENEMY_UNIT_LOCATION_NO_ECHO`

## Count Impact

- snapshotEntries: `1009 -> 1009`
- functionalUnits: `811 -> 811`
- payment-cost functionalUnits: `360 -> 360`
- payment-cost snapshotEntries: `446 -> 446`
- NEEDS_ENGINE_SUPPORT: `343 -> 342`
- primary residual: `199 -> 198`
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `531 -> 530`
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `240 -> 239`
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: `328`
- NEEDS_FAQ_REVIEW residual: `92`
- primary NEEDS_FAQ_REVIEW residual: `61`
- fullOfficialTrue: `0`
- ready: `false`

## Evidence Boundary

This candidate relies on existing Bait preflight and fixture-runner evidence in `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bait-move-enemy-unit-to-another-location.fixture.json`: the evidence covers pay 2, two enemy unit targets, stack timing, pass-pass resolution and the move-to-another-unit-location outcome. It does not claim complete control-zone-movement closure, complete targeting-stack closure, automated evidence closure, FAQ review closure, fullOfficial status or READY.

## Locked Scope

Runtime、frontend、Chrome / browser scripts、formal 18-step scripts、official card catalog、non-selected matrix rows、`fullOfficial`、final READY 与 `riftbound-dotnet.sln` 仍锁定。本批没有前端或浏览器脚本变更，因此 Chrome smoke 不运行；READY 前仍需要 final fresh Chrome / formal evidence。

## Non-Closure

payment-cost blocker closure remains partially open。B/D_ENGINE_SUPPORT payment-cost residual remains open。A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open。E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open。E_CARD_MATRIX_READINESS remains open。card matrix remains open。P0-005 remains open。P0-004 adjacency audit-sensitive remains open。P1 remains open。full official PaymentEngine matrix closure remains open。READY remains open。

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- focused `PaymentEngineCoverageAuditTests` 330/330 passed.
- current-head backend full `dotnet test Riftbound.slnx --no-restore` 4901/4901 passed.
- `git diff --check` passed.
