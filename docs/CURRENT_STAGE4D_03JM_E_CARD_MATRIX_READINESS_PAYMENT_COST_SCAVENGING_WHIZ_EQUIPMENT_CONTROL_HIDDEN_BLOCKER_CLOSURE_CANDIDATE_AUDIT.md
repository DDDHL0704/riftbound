# 4D-03JM-E Audit: Payment-Cost Scavenging Whiz Equipment Control/Hidden Blocker Closure Candidate

## Decision

`FU-dcce660783 / OGN·099/298 / 拾荒小能手` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `SCAVENGING_WHIZ_PLAY_EQUIPMENT`.

## Accepted Evidence

- `CoreRuleEnginePlaysScavengingWhizEquipment` proves the card can be played as a zero-target equipment path, pays 2, enters the stack, resolves after pass/pass, and becomes a `CARD_TYPE:EQUIPMENT` object in the controller base.
- `CoreRuleEngineRejectsScavengingWhizWhenTargetsAreProvided` and `P4ScavengingWhizTargetRejectedFixture` prove target legality remains server-side and invalid targets do not mutate tick, events, payment, hand, base, or stack.
- `rules-evidence-index.md` and `p2-rules-preflight.md` record the audited fixture evidence and retain the limitation that recycle/pay/exhaust draw skill remains deferred.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-dcce660783`.
- Card: `OGN·099/298 拾荒小能手`.
- Effect: `SCAVENGING_WHIZ_PLAY_EQUIPMENT`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysScavengingWhizEquipment|FullyQualifiedName~CoreRuleEngineRejectsScavengingWhizWhenTargetsAreProvided|FullyQualifiedName~P4ScavengingWhizTargetRejectedFixture|FullyQualifiedName~ScavengingWhiz|FullyQualifiedName~Equipment"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Equipment|FullyQualifiedName~ScavengingWhiz|FullyQualifiedName~Target"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `ScavengingWhiz|Equipment` focused regression 375/375 passed; `ActionPrompt|Prompt|PaymentResource|SpendPower|RunePool|Equipment|ScavengingWhiz|Target` adjacent regression 1971/1971 passed; `PaymentEngineCoverageAuditTests` 512/512 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5083/5083 passed; `git diff --check` passed.
- Chrome smoke not run because there were no frontend or browser-script changes.
