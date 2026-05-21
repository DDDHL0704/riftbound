# 4D-03JX-E Card Matrix Readiness Payment-Cost Kraken Hunter HASTE_READY Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-27ad08ab01 / OGN·150/298 / 海妖猎手 / KRAKEN_HUNTER_PLAY_UNIT_NO_OPTIONAL_HASTE`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·150/298` to `KRAKEN_HUNTER_PLAY_UNIT_NO_OPTIONAL_HASTE`.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers Kraken Hunter play/target rejection and the optional HASTE_READY payment path.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-kraken-hunter-no-optional-haste.fixture.json`.
- Optional haste fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-kraken-hunter-haste-ready.fixture.json` covers the extra mana/power path that enters ready.
- Coverage audit evidence: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` records the representative OGN·150/298 payment row and the 4D-03JX matrix transition.
- Rules/evidence index: `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, `docs/CURRENT_P4_STATUS.md` and `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` record the accepted representative evidence while keeping complete Kraken Hunter official breadth open.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `243 -> 242`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `164 -> 163`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `431 -> 430`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `152 -> 151`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Kraken Hunter automated evidence disposition, complete Haste / active-entry official breadth, Assault / battle damage modifier, boon-consume cost reduction, complete layer / continuous-effect breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- KrakenHunter / KRAKEN_HUNTER / HasteReady / ConformanceFixtureRunnerTests focused regression passed: 3084/3084.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / KrakenHunter / HasteReady / Stack adjacent regression passed: 698/698.
- `PaymentEngineCoverageAuditTests` passed: 534/534.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5105/5105.
- `git diff --check` passed after final doc write.
