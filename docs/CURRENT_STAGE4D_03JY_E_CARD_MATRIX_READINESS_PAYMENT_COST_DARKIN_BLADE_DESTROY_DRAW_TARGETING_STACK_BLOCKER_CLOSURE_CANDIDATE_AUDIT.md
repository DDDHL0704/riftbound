# 4D-03JY-E Audit: Payment-Cost Darkin Blade Destroy/Draw Targeting-Stack Blocker Closure Candidate

## Decision

`FU-1810c42ef7 / OGN·213/298 / 暗刃` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `DARKIN_BLADE_DESTROY_BATTLEFIELD_UNIT_TARGET_CONTROLLER_DRAW_2`.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves the Darkin Blade representative path: paying 2, targeting a battlefield unit, entering the stack, passing priority, destroying the target, and making the target's controller draw 2.
- `p2-preflight-play-darkin-blade-destroy-target-controller-draw.fixture.json` proves the current spell-play path and final state.
- `rules-evidence-index.md` and `p2-rules-preflight.md` keep the accepted P2 representative path and explicitly leave the standby path open.
- `CURRENT_P2_STATUS.md` records adjacent preflight evidence for this representative.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-1810c42ef7`.
- Card: `OGN·213/298 暗刃`.
- Effect: `DARKIN_BLADE_DESTROY_BATTLEFIELD_UNIT_TARGET_CONTROLLER_DRAW_2`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DarkinBlade|FullyQualifiedName~DARKIN_BLADE|FullyQualifiedName~Destroy|FullyQualifiedName~Draw|FullyQualifiedName~ConformanceFixtureRunnerTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~DarkinBlade|FullyQualifiedName~Destroy|FullyQualifiedName~Draw|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- DarkinBlade / DARKIN_BLADE / Destroy / Draw / ConformanceFixtureRunnerTests focused regression passed: 3140/3140.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / DarkinBlade / Destroy / Draw / Target / Stack adjacent regression passed: 2012/2012.
- `PaymentEngineCoverageAuditTests` passed: 536/536.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5107/5107.
- `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, Darkin Blade automated evidence disposition, standby / reaction timing breadth, complete destroy / target-controller draw hidden-info breadth, complete battle / spell-duel adjacency, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
