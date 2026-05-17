# 4D-03GG-E Card Matrix Readiness Audit

日期：2026-05-18
结论：**ACCEPTED / ROW-LEVEL ONLY / NOT READY**

本审计记录 4D-03GG-E payment-cost Moving Perch spellshield token targeting-stack blocker closure candidate。它只把 `FU-7c997bad02 / UNL-130/219 移动栖木 / MOVING_PERCH_SPELLSHIELD_TOKEN_STATIC` 这一 direct-card-behavior 代表行从 `NEEDS_ENGINE_SUPPORT` 收窄到 `IMPLEMENTED_UNTESTED`，并保留 `NEEDS_AUTOMATED_TEST_EVIDENCE`、`fullOfficial=false` 与 `ready=false`。

## Scope

- manifest: `Post03GgCardMatrixReadinessPaymentCostMovingPerchSpellshieldTokenTargetingStackBlockerClosureCandidateManifest`
- classification: `post-03gf-e-card-matrix-readiness-payment-cost-moving-perch-spellshield-token-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03GF_PAYMENT_COST_MOVING_PERCH_SPELLSHIELD_TOKEN_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- input previous closure candidate manifest: `Post03GfCardMatrixReadinessPaymentCostPykeOptionalReadyPowerTargetingStackBlockerClosureCandidateManifest`
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-7c997bad02`
- selected card: `UNL-130/219 移动栖木`
- selected effect: `MOVING_PERCH_SPELLSHIELD_TOKEN_STATIC`

## Count Impact

- snapshotEntries: `1009 -> 1009`
- functionalUnits: `811 -> 811`
- payment-cost functionalUnits: `360 -> 360`
- payment-cost snapshotEntries: `446 -> 446`
- NEEDS_ENGINE_SUPPORT: `337 -> 336`
- primary residual: `193 -> 192`
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `525 -> 524`
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: `234 -> 233`
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: `328`
- NEEDS_FAQ_REVIEW residual: `92`
- primary NEEDS_FAQ_REVIEW residual: `61`
- fullOfficialTrue: `0`
- ready: `false`

## Evidence Boundary

This candidate relies on existing Moving Perch preflight evidence in `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, `src/Riftbound.Engine/CardBehaviorRegistry.cs`, `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moving-perch-spellshield-token-static.fixture.json`: the evidence covers paying the base 5 cost, playing with zero targets, resolving into the controller base as a 6 power `CARD_TYPE:UNIT` object with the `法盾` tag, and direct rejection of target-bearing play. It does not claim complete Spellshield target-tax closure, opponent Warhawk token creation, token Spellshield handling, complete targeting-stack closure, automated evidence closure, FAQ review closure, fullOfficial status or READY.

## Locked Scope

Runtime、frontend、Chrome / browser scripts、formal 18-step scripts、official card catalog、non-selected matrix rows、`fullOfficial`、final READY 与 `riftbound-dotnet.sln` 仍锁定。本批没有前端或浏览器脚本变更，因此 Chrome smoke 不运行；READY 前仍需要 final fresh Chrome / formal evidence。

## Non-Closure

payment-cost blocker closure remains partially open。B/D_ENGINE_SUPPORT payment-cost residual remains open。A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open。E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open。E_CARD_MATRIX_READINESS remains open。card matrix remains open。P0-005 remains open。P0-004 adjacency audit-sensitive remains open。P1 remains open。full official PaymentEngine matrix closure remains open。READY remains open。

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- focused `PaymentEngineCoverageAuditTests` 342/342 passed.
- current-head backend full `dotnet test Riftbound.slnx --no-restore` 4913/4913 passed.
- `git diff --check` passed for this batch.
