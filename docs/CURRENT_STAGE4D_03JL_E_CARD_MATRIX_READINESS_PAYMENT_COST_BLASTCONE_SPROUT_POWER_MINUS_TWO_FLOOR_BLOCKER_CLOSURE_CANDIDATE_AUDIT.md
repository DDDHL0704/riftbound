# 4D-03JL-E Audit: Payment-Cost Blastcone Sprout Power-Minus-Two Floor Blocker Closure Candidate

## Decision

`FU-3fd9d79377 / OGN·097/298 / 爆裂球果仙灵` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `BLASTCONE_SPROUT_PLAY_UNIT_POWER_MINUS_2_FLOOR`.

## Accepted Evidence

- `CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor` proves the unit enters base, the target receives requested `-2`, applied floor metadata records the minimum `1`, and authoritative snapshot / continuous-effect metadata expose the resolved public result.
- `CoreRuleEngineRejectsBlastconeSproutWhenTargetIsNotUnit` proves target legality remains server-side.
- LayerEngine 04M / 04P evidence records minimum-power ledger and ordering baseline coverage without upgrading to full official breadth.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-3fd9d79377`.
- Card: `OGN·097/298 爆裂球果仙灵`.
- Effect: `BLASTCONE_SPROUT_PLAY_UNIT_POWER_MINUS_2_FLOOR`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~CoreRuleEngineRejectsBlastconeSproutWhenTargetIsNotUnit|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~LayerEngine"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|CoreRuleEngineRejectsBlastconeSproutWhenTargetIsNotUnit|PowerModifier|MinimumPower|Blastcone|ContinuousEffect|LayerEngine` focused regression 16/16 passed; `ActionPrompt|Prompt|PaymentResource|SpendPower|RunePool|Priority|Stack|Blastcone|Target` adjacent regression 1838/1838 passed; `PaymentEngineCoverageAuditTests` 510/510 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5081/5081 passed; `git diff --check` passed.
- Chrome smoke not run because there were no frontend or browser-script changes.
