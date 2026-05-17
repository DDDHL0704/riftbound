# 4D-03GC-E Card Matrix Readiness Audit

日期：2026-05-18
结论：**ACCEPTED / ROW-LEVEL ONLY / NOT READY**

本审计记录 4D-03GC-E payment-cost Ship Monkey optional-boon targeting-stack blocker closure candidate。它只把 `FU-18d1ef92c2 / SFD·098/221 船猿 / SHIP_MONKEY_PLAY_UNIT_OPTIONAL_BOON` 这一 direct-card-behavior 代表行从 `NEEDS_ENGINE_SUPPORT` 收窄到 `IMPLEMENTED_UNTESTED`，并保留 `NEEDS_AUTOMATED_TEST_EVIDENCE`、`fullOfficial=false` 与 `ready=false`。

## Scope

- manifest: `Post03GcCardMatrixReadinessPaymentCostShipMonkeyOptionalBoonTargetingStackBlockerClosureCandidateManifest`
- classification: `post-03gb-e-card-matrix-readiness-payment-cost-ship-monkey-optional-boon-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03GB_PAYMENT_COST_SHIP_MONKEY_OPTIONAL_BOON_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- input previous closure candidate manifest: `Post03GbCardMatrixReadinessPaymentCostTinyGuardianOptionalDrawTargetingStackBlockerClosureCandidateManifest`
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-18d1ef92c2`
- selected card: `SFD·098/221 船猿`
- selected effect: `SHIP_MONKEY_PLAY_UNIT_OPTIONAL_BOON`

## Count Impact

- snapshotEntries: `1009 -> 1009`
- functionalUnits: `811 -> 811`
- payment-cost functionalUnits: `360 -> 360`
- payment-cost snapshotEntries: `446 -> 446`
- NEEDS_ENGINE_SUPPORT: `341 -> 340`
- primary residual: `197 -> 196`
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `529 -> 528`
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `238 -> 237`
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: `328`
- NEEDS_FAQ_REVIEW residual: `92`
- primary NEEDS_FAQ_REVIEW residual: `61`
- fullOfficialTrue: `0`
- ready: `false`

## Evidence Boundary

This candidate relies on existing Ship Monkey preflight, optional mana payment, source boon and fixture-runner evidence in `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `src/Riftbound.Engine/CardBehaviorRegistry.cs`, `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`, `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ship-monkey-no-optional-boon.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ship-monkey-optional-boon.fixture.json`: the evidence covers base cost 2, optional `SPEND_MANA:1`, 0-target stack timing, pass-pass source unit resolution, source `增益` and prompt/no-target boundaries. It does not claim complete LayerEngine / continuous-effect closure, complete targeting-stack closure, automated evidence closure, FAQ review closure, fullOfficial status or READY.

## Locked Scope

Runtime、frontend、Chrome / browser scripts、formal 18-step scripts、official card catalog、non-selected matrix rows、`fullOfficial`、final READY 与 `riftbound-dotnet.sln` 仍锁定。本批没有前端或浏览器脚本变更，因此 Chrome smoke 不运行；READY 前仍需要 final fresh Chrome / formal evidence。

## Non-Closure

payment-cost blocker closure remains partially open。B/D_ENGINE_SUPPORT payment-cost residual remains open。A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open。E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open。E_CARD_MATRIX_READINESS remains open。card matrix remains open。P0-005 remains open。P0-004 adjacency audit-sensitive remains open。P1 remains open。full official PaymentEngine matrix closure remains open。READY remains open。

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- focused `PaymentEngineCoverageAuditTests` 334/334 passed.
- current-head backend full `dotnet test Riftbound.slnx --no-restore` 4905/4905 passed.
- `git diff --check` passed.
