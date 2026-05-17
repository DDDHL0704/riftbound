# 4D-03GE-E Card Matrix Readiness Audit

日期：2026-05-18
结论：**ACCEPTED / ROW-LEVEL ONLY / NOT READY**

本审计记录 4D-03GE-E payment-cost Rude Pirate optional discard targeting-stack blocker closure candidate。它只把 `FU-9b8ce06dba / OGN·002/298 粗鲁的海盗 / RUDE_PIRATE_NO_OPTIONAL_DISCARD_PLAY_UNIT` 这一 direct-card-behavior 代表行从 `NEEDS_ENGINE_SUPPORT` 收窄到 `IMPLEMENTED_UNTESTED`，并保留 `NEEDS_AUTOMATED_TEST_EVIDENCE`、`fullOfficial=false` 与 `ready=false`。

## Scope

- manifest: `Post03GeCardMatrixReadinessPaymentCostRudePirateOptionalDiscardTargetingStackBlockerClosureCandidateManifest`
- classification: `post-03gd-e-card-matrix-readiness-payment-cost-rude-pirate-optional-discard-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03GD_PAYMENT_COST_RUDE_PIRATE_OPTIONAL_DISCARD_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- input previous closure candidate manifest: `Post03GdCardMatrixReadinessPaymentCostFrostcoatCubOptionalPowerMinusTwoTargetingStackBlockerClosureCandidateManifest`
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-9b8ce06dba`
- selected card: `OGN·002/298 粗鲁的海盗`
- selected effect: `RUDE_PIRATE_NO_OPTIONAL_DISCARD_PLAY_UNIT`

## Count Impact

- snapshotEntries: `1009 -> 1009`
- functionalUnits: `811 -> 811`
- payment-cost functionalUnits: `360 -> 360`
- payment-cost snapshotEntries: `446 -> 446`
- NEEDS_ENGINE_SUPPORT: `339 -> 338`
- primary residual: `195 -> 194`
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `527 -> 526`
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `236 -> 235`
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: `328`
- NEEDS_FAQ_REVIEW residual: `92`
- primary NEEDS_FAQ_REVIEW residual: `61`
- fullOfficialTrue: `0`
- ready: `false`

## Evidence Boundary

This candidate relies on existing Rude Pirate preflight, optional discard cost reduction prompt, payment/discard settlement and self-discard rejection evidence in `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `src/Riftbound.Engine/CardBehaviorRegistry.cs`, `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rude-pirate-no-optional-discard.fixture.json`: the evidence covers base cost 6, no-target unit resolution, optional `DISCARD_HAND_CARD:<objectId>` cost reduction to 4, authoritative discard/payment settlement and rejecting the source card as its own optional discard. It does not claim complete hidden-info/random-zone closure, complete targeting-stack closure, automated evidence closure, FAQ review closure, fullOfficial status or READY.

## Locked Scope

Runtime、frontend、Chrome / browser scripts、formal 18-step scripts、official card catalog、non-selected matrix rows、`fullOfficial`、final READY 与 `riftbound-dotnet.sln` 仍锁定。本批没有前端或浏览器脚本变更，因此 Chrome smoke 不运行；READY 前仍需要 final fresh Chrome / formal evidence。

## Non-Closure

payment-cost blocker closure remains partially open。B/D_ENGINE_SUPPORT payment-cost residual remains open。A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open。E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open。E_CARD_MATRIX_READINESS remains open。card matrix remains open。P0-005 remains open。P0-004 adjacency audit-sensitive remains open。P1 remains open。full official PaymentEngine matrix closure remains open。READY remains open。

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- focused `PaymentEngineCoverageAuditTests` 338/338 passed.
- current-head backend full `dotnet test Riftbound.slnx --no-restore` 4909/4909 passed.
- `git diff --check` passed.
