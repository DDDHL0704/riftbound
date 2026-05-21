# 4D-03JQ-E Audit: Payment-Cost Thousand Tailed Watcher Power-Minus-Three Haste FAQ Targeting Stack Blocker Closure Candidate

## Decision

`FU-500b9dad14 / OGN·116/298 / 千尾监视者` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime, layer and automated evidence already prove the narrow representative path for `THOUSAND_TAILED_WATCHER_PLAY_UNIT_ALL_ENEMY_UNITS_MINUS_3`.

This is not a full-official closure. The row remains `NEEDS_FAQ_REVIEW` and still keeps `NEEDS_FAQ_REVIEW` plus `NEEDS_AUTOMATED_TEST_EVIDENCE` as full official blockers.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves ordinary play, no-target play-card shape, all-enemy unit power-minus-three resolution, optional haste-ready payment and invalid-target / payment guard coverage for the selected card row.
- `p2-preflight-play-thousand-tailed-watcher-all-enemy-units-minus-3.fixture.json` proves the baseline play path and temporary all-enemy-unit power modifier.
- `p4-play-thousand-tailed-watcher-haste-ready.fixture.json` proves the haste-ready optional payment representative branch.
- `CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_BASELINE_EVIDENCE.md` and `CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_AUDIT.md` keep the minimum-power floor and layer-ordering adjacency for this power modifier family covered.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-500b9dad14`.
- Card: `OGN·116/298 千尾监视者`.
- Effect: `THOUSAND_TAILED_WATCHER_PLAY_UNIT_ALL_ENEMY_UNITS_MINUS_3`.
- `freezeStatus`: `NEEDS_FAQ_REVIEW -> NEEDS_FAQ_REVIEW`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW -> IMPLEMENTED_UNTESTED + NEEDS_FAQ_REVIEW`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysThousandTailedWatcherAllEnemyUnitsMinus3|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~ThousandTailedWatcher|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~LayerEngine"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ThousandTailed|FullyQualifiedName~Haste|FullyQualifiedName~PowerModifier|FullyQualifiedName~LayerEngine|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Thousand Tailed focused regression 13/13 passed; adjacent prompt/payment/target/stack regression 1948/1948 passed; PaymentEngineCoverageAuditTests 520/520 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5091/5091 passed; `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
