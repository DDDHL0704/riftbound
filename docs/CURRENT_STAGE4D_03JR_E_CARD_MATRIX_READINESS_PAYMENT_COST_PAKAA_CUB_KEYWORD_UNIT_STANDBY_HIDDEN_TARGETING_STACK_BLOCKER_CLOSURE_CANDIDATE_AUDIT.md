# 4D-03JR-E Audit: Payment-Cost Pakaa Cub Keyword-Unit Standby Hidden Targeting Stack Blocker Closure Candidate

## Decision

`FU-913f287ca6 / OGN·135/298 / 帕卡幼崽` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `PAKAA_CUB_PLAY_KEYWORD_UNIT`.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves ordinary play, no-target play-card shape, base 3-cost payment, controller-base unit creation, `待命` and `猫科` tag projection, and target rejection for the selected card row.
- `p2-preflight-play-pakaa-cub-keyword-unit.fixture.json` proves the baseline play path.
- `rules-evidence-index.md`, `p2-rules-preflight.md`, and `CURRENT_P2_STATUS.md` keep the accepted P2 representative path and explicitly leave standby face-down / reaction play official breadth open.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-913f287ca6`.
- Card: `OGN·135/298 帕卡幼崽`.
- Effect: `PAKAA_CUB_PLAY_KEYWORD_UNIT`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Pakaa|FullyQualifiedName~Paka|FullyQualifiedName~KeywordUnit|FullyQualifiedName~Standby|FullyQualifiedName~p2-preflight-play-pakaa-cub-keyword-unit"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Pakaa|FullyQualifiedName~Paka|FullyQualifiedName~KeywordUnit|FullyQualifiedName~Standby|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Pakaa Cub focused regression 164/164 passed; adjacent prompt/payment/target/stack regression 1956/1956 passed; PaymentEngineCoverageAuditTests 522/522 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5093/5093 passed; `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
