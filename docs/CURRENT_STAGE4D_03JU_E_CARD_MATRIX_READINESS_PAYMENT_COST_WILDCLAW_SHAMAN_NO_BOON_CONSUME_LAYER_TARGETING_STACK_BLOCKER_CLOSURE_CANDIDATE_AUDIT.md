# 4D-03JU-E Audit: Payment-Cost Wildclaw Shaman No-Boon-Consume Layer Targeting Stack Blocker Closure Candidate

## Decision

`FU-b55baa6b03 / OGN·147/298 / 野爪萨满` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `WILDCLAW_SHAMAN_NO_BOON_CONSUME_PLAY_UNIT`.

This is not a full-official closure. The row moves from primary `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` proves the ordinary no-boon-consume path: paying 4, requiring no target selection, entering the stack, passing priority, and creating Wildclaw Shaman as a 3-power unit in base.
- `p2-preflight-play-wildclaw-shaman-no-boon-consume-static.fixture.json` proves the baseline no-boon-consume unit-play path.
- `rules-evidence-index.md` and `p2-rules-preflight.md` keep the accepted P2 representative path and explicitly leave the optional boon-consume self-buff / ready branch open.
- `CURRENT_P2_STATUS.md` records adjacent P2 preflight unit-play evidence for this representative.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-b55baa6b03`.
- Card: `OGN·147/298 野爪萨满`.
- Effect: `WILDCLAW_SHAMAN_NO_BOON_CONSUME_PLAY_UNIT`.
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Wildclaw|FullyQualifiedName~NoBoonConsume|FullyQualifiedName~PlayUnit|FullyQualifiedName~ConformanceFixtureRunnerTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Wildclaw|FullyQualifiedName~PlayCard|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Wildclaw / NoBoonConsume / PlayUnit / ConformanceFixtureRunnerTests focused regression passed: 3042/3042.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / Wildclaw / PlayCard / Stack adjacent regression passed: 740/740.
- `PaymentEngineCoverageAuditTests` passed: 528/528.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5099/5099.
- `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
