# 4D-03JS-E Audit: Payment-Cost Spoils Of War Draw Two Cost Reduction Hidden Targeting Stack Blocker Closure Candidate

## Decision

`FU-d4850c9eab / OGN·144/298 / 以战养战` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative paths for `SPOILS_OF_WAR_DRAW_2`.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves the ordinary full-cost path: paying 4, entering the stack with 0 targets, passing priority, and drawing 2.
- `p2-preflight-play-spoils-of-war-draw-stack.fixture.json` proves the baseline draw-two path.
- `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json` proves the representative cost-reduction path after an enemy unit is destroyed this turn.
- `rules-evidence-index.md`, `p2-rules-preflight.md`, and `master-development-plan.md` keep the accepted P2 representative path and explicitly leave broader cost-reduction / hidden draw visibility / cleanup-duration breadth open.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-d4850c9eab`.
- Card: `OGN·144/298 以战养战`.
- Effect: `SPOILS_OF_WAR_DRAW_2`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpoilsOfWar|FullyQualifiedName~spoils-of-war|FullyQualifiedName~Draw|FullyQualifiedName~DestroyedUnit"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Spoils|FullyQualifiedName~Draw|FullyQualifiedName~Hidden|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Spoils of War focused regression 164/164 passed; adjacent prompt/payment/hidden/target/stack regression 1972/1972 passed; PaymentEngineCoverageAuditTests 524/524 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5095/5095 passed; `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
