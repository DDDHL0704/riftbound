# 4D-03JT-E Audit: Payment-Cost Beatdown Ready Unit Boon Optional Targeting Stack Blocker Closure Candidate

## Decision

`FU-66c9db7e53 / OGN·146/298 / 痛殴` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `BEATDOWN_READY_UNIT`.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves the ordinary non-boon-consume path: paying 2, selecting a unit, entering the stack, passing priority, and readying the target unit.
- `p2-preflight-play-beatdown-ready-unit.fixture.json` proves the baseline ready-unit path.
- `rules-evidence-index.md` and `p2-rules-preflight.md` keep the accepted P2 representative path and explicitly leave the boon-consume optional-cost branch open.
- `CURRENT_STAGE4C_BATCH63_ANY_UNIT_TARGET_SCOPE_GUARD_EVIDENCE.md` records adjacent any-unit target scope guard evidence for Beatdown.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-66c9db7e53`.
- Card: `OGN·146/298 痛殴`.
- Effect: `BEATDOWN_READY_UNIT`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Beatdown|FullyQualifiedName~ReadyUnit|FullyQualifiedName~AnyUnitTargetScopeGuardTests|FullyQualifiedName~TargetScope|FullyQualifiedName~TargetGuard"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Beatdown|FullyQualifiedName~Ready|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Beatdown / ReadyUnit / AnyUnitTargetScope / TargetScope / TargetGuard focused regression passed: 32/32.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / Beatdown / Ready / Target / Stack adjacent regression passed: 2022/2022.
- `PaymentEngineCoverageAuditTests` passed: 526/526.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5097/5097.
- `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
