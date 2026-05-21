# 4D-03JN-E Audit: Payment-Cost Portalpal Rescue Banish Play-Base Targeting-Stack Blocker Closure Candidate

## Decision

`FU-289695c5bf / OGN·102/298 / 传送门大营救` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `PORTALPAL_RESCUE_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE`.

## Accepted Evidence

- `CoreRuleEnginePlaysPortalpalRescueBanishPlayBase` proves the card pays 3, targets a friendly battlefield unit, resolves through stack pass/pass, banishes the target, plays it to its owner's base, and clears damage plus until-end-of-turn effects.
- `CoreRuleEngineRejectsPortalpalRescueAgainstEnemyUnit` proves target legality remains server-side and invalid enemy targets do not mutate tick, events, payment, hand, battlefield, or stack.
- `CoreRuleEnginePortalpalRescueResolutionSkipsOpponentControlledFriendlyZoneTarget` proves restored / dirty stack targets that sit in a friendly zone but are opponent-controlled do not get banished or replayed.
- `rules-evidence-index.md` and `p2-rules-preflight.md` record the audited fixture evidence and retain the limitation that broader banish / replacement / play-to-base semantics remain deferred.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-289695c5bf`.
- Card: `OGN·102/298 传送门大营救`.
- Effect: `PORTALPAL_RESCUE_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysPortalpalRescueBanishPlayBase|FullyQualifiedName~CoreRuleEngineRejectsPortalpalRescueAgainstEnemyUnit|FullyQualifiedName~CoreRuleEnginePortalpalRescueResolutionSkipsOpponentControlledFriendlyZoneTarget|FullyQualifiedName~PortalpalRescue|FullyQualifiedName~Banish|FullyQualifiedName~PlayCard"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~PortalpalRescue|FullyQualifiedName~Banish|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `PortalpalRescue|Banish|PlayCard` focused regression passed: `165/165`.
- `ActionPrompt|Prompt|PaymentResource|SpendPower|RunePool|PortalpalRescue|Banish|Target|Stack` adjacent regression passed: `1828/1828`.
- `PaymentEngineCoverageAuditTests` passed: `514/514`.
- Backend full test passed: `dotnet test Riftbound.slnx --no-restore`, `5085/5085`.
- `git diff --check` passed.
- Chrome smoke was not run for 03JN because this batch did not change frontend or browser-script files.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
