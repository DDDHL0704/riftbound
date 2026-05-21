# 4D-03JR-E Card Matrix Readiness Payment-Cost Pakaa Cub Keyword-Unit Standby Hidden Targeting Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-913f287ca6 / OGN·135/298 / 帕卡幼崽 / PAKAA_CUB_PLAY_KEYWORD_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·135/298` to `PAKAA_CUB_PLAY_KEYWORD_UNIT` with cost 3, 0 targets, a play-unit resolution into the controller base, and `待命` plus `猫科` unit tags.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers ordinary play, base payment, 0 target entry, resulting unit power/tag shape, and target rejection for the selected card row.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-pakaa-cub-keyword-unit.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_P2_STATUS.md` record the accepted representative evidence and the remaining standby face-down / reaction and hidden-info breadth.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `249 -> 248`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `170 -> 169`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `437 -> 436`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `157 -> 156`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Pakaa Cub automated evidence disposition, standby face-down / reaction play official breadth, hidden-info / standby visibility matrix, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Pakaa Cub focused regression 164/164 passed; adjacent prompt/payment/target/stack regression 1956/1956 passed; PaymentEngineCoverageAuditTests 522/522 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5093/5093 passed; `git diff --check` passed after final doc write.
