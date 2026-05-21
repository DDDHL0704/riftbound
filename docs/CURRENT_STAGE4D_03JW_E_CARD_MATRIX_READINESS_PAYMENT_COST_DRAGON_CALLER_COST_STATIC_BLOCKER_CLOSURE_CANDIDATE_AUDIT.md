# 4D-03JW-E Audit: Payment-Cost Dragon Caller Cost-Static Blocker Closure Candidate

## Decision

`FU-f45bfb57e3 / OGN·140/298 / 唤龙使者` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `DRAGON_CALLER_COST_STATIC_PLAY_UNIT`.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves the static Dragon Caller representative path: paying 4, requiring no target selection, entering the stack, passing priority, and creating a 3-power unit in base.
- `p2-preflight-play-dragon-caller-cost-static.fixture.json` proves the baseline unit-play path for Dragon Caller's direct card behavior.
- `p4-play-dragon-caller-target-rejected.fixture.json` proves that explicit target submission for the current zero-target play path is rejected before payment, stack creation or unit entry.
- `rules-evidence-index.md` and `p2-rules-preflight.md` keep the accepted P2 representative path and explicitly leave the Dragon-unit cost-reduction static path open.
- `CURRENT_P2_STATUS.md`, `CURRENT_P4_STATUS.md` and `conformance-fixture-format.md` record adjacent preflight and fixture-format evidence for this representative.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-f45bfb57e3`.
- Card: `OGN·140/298 唤龙使者`.
- Effect: `DRAGON_CALLER_COST_STATIC_PLAY_UNIT`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonCaller|FullyQualifiedName~Dragon Caller|FullyQualifiedName~DRAGON_CALLER|FullyQualifiedName~ConformanceFixtureRunnerTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~DragonCaller|FullyQualifiedName~Dragon|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- DragonCaller / DRAGON_CALLER / ConformanceFixtureRunnerTests focused regression passed: 3022/3022.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / DragonCaller / Dragon / Stack adjacent regression passed: 671/671.
- `PaymentEngineCoverageAuditTests` passed: 532/532.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5103/5103.
- `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, Dragon Caller automated evidence disposition, Dragon-unit cost-reduction static path, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
