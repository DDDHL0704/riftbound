# 4D-03JO-E Audit: Payment-Cost Arena Bar Equipment Layer Blocker Closure Candidate

## Decision

`FU-52ad18b853 / OGN·124/298 / 竞技场酒吧` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `ARENA_BAR_PLAY_EQUIPMENT`.

## Accepted Evidence

- `CoreRuleEnginePlaysArenaBarEquipment` proves the card pays 3, uses zero targets, resolves through stack pass/pass, and becomes a `CARD_TYPE:EQUIPMENT` object in the controller's base.
- `CoreRuleEngineRejectsArenaBarWhenTargetsAreProvided` proves target legality remains server-side and invalid explicit targets do not mutate tick, events, payment, hand, base, or stack.
- `P4ArenaBarTargetRejectedFixture` keeps the explicit-target rejection as replayable fixture evidence.
- `rules-evidence-index.md` and `p2-rules-preflight.md` record the audited fixture evidence and retain the limitation that tap-to-grant-boon and broader equipment / layer semantics remain deferred.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-52ad18b853`.
- Card: `OGN·124/298 竞技场酒吧`.
- Effect: `ARENA_BAR_PLAY_EQUIPMENT`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysArenaBarEquipment|FullyQualifiedName~CoreRuleEngineRejectsArenaBarWhenTargetsAreProvided|FullyQualifiedName~P4ArenaBarTargetRejectedFixture|FullyQualifiedName~ArenaBar|FullyQualifiedName~Equipment"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ArenaBar|FullyQualifiedName~Equipment|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `ArenaBar|Equipment` focused prevalidation passed: `374/374`.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `ArenaBar|Equipment` focused regression passed: `377/377`.
- Adjacent prompt/payment/target/stack regression passed: `2105/2105`.
- `PaymentEngineCoverageAuditTests` passed: `516/516`.
- Backend full test passed: `5087/5087`.
- `git diff --check` passed.
- Chrome smoke was not run for 03JO because this candidate did not change frontend or browser-script files.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
