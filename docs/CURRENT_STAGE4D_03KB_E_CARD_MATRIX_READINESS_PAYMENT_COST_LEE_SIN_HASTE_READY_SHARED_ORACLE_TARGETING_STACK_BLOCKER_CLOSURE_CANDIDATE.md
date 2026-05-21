# 4D-03KB-E Card Matrix Readiness Payment-Cost Lee Sin HASTE_READY Shared-Oracle Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-73a731583b / OGN·151/298 + OGN·151a/298 / 李青 / LEE_SIN_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE;LEE_SIN_PLAY_UNIT_NO_OPTIONAL_HASTE`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `ConformanceFixtureRunnerTests` proves the Lee Sin base play path, target rejection path and HASTE_READY optional-cost representative path.
- `p2-preflight-play-lee-sin-no-optional-haste.fixture.json` and `p2-preflight-play-lee-sin-alt-a-no-optional-haste.fixture.json` cover the shared OGN base play representatives.
- `p4-play-lee-sin-haste-ready.fixture.json` and `p4-play-lee-sin-alt-a-haste-ready.fixture.json` cover the current HASTE_READY representative optional-cost path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md` and `docs/CURRENT_P4_STATUS.md` record the accepted representative evidence and deferred official breadth.
- `CardBehaviorRegistry` binds both selected card numbers to the selected Lee Sin effect kinds.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 239 -> 238.
- Primary residual: 162 -> 162 because this row remains `SHARED_ORACLE_IMPLEMENTATION`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 427 -> 426.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 148 -> 147.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Lee Sin automated evidence disposition, precise orange-resource HASTE_READY matching, battlefield placement / active-entry official breadth, friendly-boon continuous power modifier, layer / continuous-effect breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; LeeSin/LEE_SIN/HasteReady/ConformanceFixtureRunnerTests focused regression 3088/3088 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/LeeSin/HasteReady/Stack adjacent regression 710/710 passed; PaymentEngineCoverageAuditTests 542/542 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5113/5113 passed; `git diff --check` passed after final doc write.
