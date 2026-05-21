# 4D-03JV-E Audit: Payment-Cost Ava Yordle Standby-Attack Hidden Targeting Stack Blocker Closure Candidate

## Decision

`FU-47beedf8a4 / OGN·107/298 / 斥候标兵 艾娃` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `OGN_AVA_STANDBY_ATTACK_PLAY_UNIT`.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves the static Ava representative path: paying 5, requiring no target selection, entering the stack, passing priority, and creating a 4-power Yordle unit in base.
- `p2-preflight-play-ogn-ava-yordle-standby-static.fixture.json` proves the baseline unit-play path for Ava's direct card behavior.
- `rules-evidence-index.md` and `p2-rules-preflight.md` keep the accepted P2 representative path and explicitly leave attack-paid standby play and timing breadth open.
- `CURRENT_P2_STATUS.md` records adjacent P2 preflight unit-play evidence for this representative.
- `CURRENT_CARD_EFFECT_STAGE3D_ORDER_TRIGGERS_EVIDENCE.md` records Ava's standby attack-trigger pressure as later trigger-ordering and timing evidence, not as this candidate's closure claim.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-47beedf8a4`.
- Card: `OGN·107/298 斥候标兵 艾娃`.
- Effect: `OGN_AVA_STANDBY_ATTACK_PLAY_UNIT`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ava|FullyQualifiedName~Yordle|FullyQualifiedName~Standby|FullyQualifiedName~ConformanceFixtureRunnerTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Ava|FullyQualifiedName~Yordle|FullyQualifiedName~Standby|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Ava / Yordle / Standby / ConformanceFixtureRunnerTests focused regression passed: 3064/3064.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / Ava / Yordle / Standby / Stack adjacent regression passed: 714/714.
- `PaymentEngineCoverageAuditTests` passed: 530/530.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5101/5101.
- `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, Ava automated evidence disposition, attack-paid standby play branch, standby / hidden-info visibility breadth, battle / spell-duel lifecycle breadth, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
