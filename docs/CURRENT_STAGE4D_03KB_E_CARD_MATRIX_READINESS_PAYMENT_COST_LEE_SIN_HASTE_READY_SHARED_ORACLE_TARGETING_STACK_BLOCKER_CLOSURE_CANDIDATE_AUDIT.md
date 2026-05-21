# 4D-03KB-E Audit: Payment-Cost Lee Sin HASTE_READY Shared-Oracle Targeting-Stack Blocker Closure Candidate

## Decision

`FU-73a731583b / OGN·151/298 + OGN·151a/298 / 李青` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative base play, target rejection and HASTE_READY optional-cost paths for the shared Lee Sin oracle implementation.

This is not a full-official closure. The row remains `SHARED_ORACLE_IMPLEMENTATION` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` covers the base no-optional-haste play path, explicit target rejection and HASTE_READY optional-cost representative path.
- `p2-preflight-play-lee-sin-no-optional-haste.fixture.json` and `p2-preflight-play-lee-sin-alt-a-no-optional-haste.fixture.json` prove both shared OGN source entries in the no-optional-cost branch.
- `p4-play-lee-sin-haste-ready.fixture.json` and `p4-play-lee-sin-alt-a-haste-ready.fixture.json` prove the current HASTE_READY representative optional-cost branch.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md` and `docs/CURRENT_P4_STATUS.md` record the evidence and deferred official breadth.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds the selected OGN card numbers to the selected effect kinds.

## Write Locks

Runtime, frontend, Chrome / browser scripts, formal 18-step scripts, protocol core fields, official catalog, fullOfficial status, final readiness status and `riftbound-dotnet.sln` remain locked.

## Non-Closure

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; LeeSin/LEE_SIN/HasteReady/ConformanceFixtureRunnerTests focused regression 3088/3088 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/LeeSin/HasteReady/Stack adjacent regression 710/710 passed; PaymentEngineCoverageAuditTests 542/542 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5113/5113 passed; `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, Lee Sin automated evidence disposition, precise orange-resource HASTE_READY matching, battlefield placement / active-entry official breadth, friendly-boon continuous power modifier, layer / continuous-effect breadth, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
