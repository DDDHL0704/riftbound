# 4D-03JX-E Audit: Payment-Cost Kraken Hunter HASTE_READY Targeting-Stack Blocker Closure Candidate

## Decision

`FU-27ad08ab01 / OGN·150/298 / 海妖猎手` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `KRAKEN_HUNTER_PLAY_UNIT_NO_OPTIONAL_HASTE` plus the optional HASTE_READY payment branch.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves Kraken Hunter representative play coverage and the target-rejection path.
- `p2-preflight-play-kraken-hunter-no-optional-haste.fixture.json` proves the baseline no-optional-haste unit-play path.
- `p4-play-kraken-hunter-haste-ready.fixture.json` proves the optional HASTE_READY payment path that pays extra mana/power and enters ready.
- `PaymentEngineCoverageAuditTests.cs` records the representative OGN·150/298 payment row and the 4D-03JX blocker-count transition.
- `rules-evidence-index.md`, `p2-rules-preflight.md`, `CURRENT_P2_STATUS.md`, `CURRENT_P4_STATUS.md` and `CardCatalogBaselineTests.cs` keep the accepted representative path and leave complete Kraken Hunter official breadth open.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-27ad08ab01`.
- Card: `OGN·150/298 海妖猎手`.
- Effect: `KRAKEN_HUNTER_PLAY_UNIT_NO_OPTIONAL_HASTE`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KrakenHunter|FullyQualifiedName~KRAKEN_HUNTER|FullyQualifiedName~HasteReady|FullyQualifiedName~ConformanceFixtureRunnerTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~KrakenHunter|FullyQualifiedName~HasteReady|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- KrakenHunter / KRAKEN_HUNTER / HasteReady / ConformanceFixtureRunnerTests focused regression passed: 3084/3084.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / KrakenHunter / HasteReady / Stack adjacent regression passed: 698/698.
- `PaymentEngineCoverageAuditTests` passed: 534/534.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5105/5105.
- `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, Kraken Hunter automated evidence disposition, complete Haste / active-entry official breadth, Assault / battle damage modifier, boon-consume cost reduction, complete layer / continuous-effect breadth, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
