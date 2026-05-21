# 4D-03JS-E Card Matrix Readiness Payment-Cost Spoils Of War Draw Two Cost Reduction Hidden Targeting Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-d4850c9eab / OGN·144/298 / 以战养战 / SPOILS_OF_WAR_DRAW_2`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·144/298` to `SPOILS_OF_WAR_DRAW_2`, and `src/Riftbound.Engine/CoreRuleEngine.cs` / `src/Riftbound.Engine/MatchSession.cs` already carry the draw and destroyed-unit-this-turn cost-reduction paths used by the fixtures.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the full-cost draw-two path and the reduced-cost path after an enemy unit is destroyed this turn.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-spoils-of-war-draw-stack.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/master-development-plan.md` record the accepted representative evidence and the remaining hidden-info / draw visibility and full reduction-matrix breadth.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `248 -> 247`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `169 -> 168`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `436 -> 435`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `156 -> 155`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Spoils of War automated evidence disposition, complete cost-reduction memory matrix, hidden-info / draw visibility breadth, cleanup / replacement / duration breadth, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Spoils of War focused regression 164/164 passed; adjacent prompt/payment/hidden/target/stack regression 1972/1972 passed; PaymentEngineCoverageAuditTests 524/524 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5095/5095 passed; `git diff --check` passed after final doc write.
