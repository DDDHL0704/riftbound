# 4D-03JY-E Card Matrix Readiness Payment-Cost Darkin Blade Destroy/Draw Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-1810c42ef7 / OGN·213/298 / 暗刃 / DARKIN_BLADE_DESTROY_BATTLEFIELD_UNIT_TARGET_CONTROLLER_DRAW_2`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·213/298` to `DARKIN_BLADE_DESTROY_BATTLEFIELD_UNIT_TARGET_CONTROLLER_DRAW_2` with base cost 2, one target, target destruction and target-controller draw-two metadata.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the Darkin Blade representative that pays 2, targets a battlefield unit, resolves the stack, destroys the target, and makes that target's controller draw 2.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-darkin-blade-destroy-target-controller-draw.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the accepted representative evidence while keeping standby/reaction and broader hidden-info timing open.
- Current status evidence: `docs/CURRENT_P2_STATUS.md` records the P2 preflight path.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `242 -> 241`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `163 -> 162`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `430 -> 429`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `151 -> 150`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Darkin Blade automated evidence disposition, standby / reaction timing breadth, complete destroy / target-controller draw hidden-info breadth, complete battle / spell-duel adjacency, cleanup / replacement / duration breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- DarkinBlade / DARKIN_BLADE / Destroy / Draw / ConformanceFixtureRunnerTests focused regression passed: 3140/3140.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / DarkinBlade / Destroy / Draw / Target / Stack adjacent regression passed: 2012/2012.
- `PaymentEngineCoverageAuditTests` passed: 536/536.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5107/5107.
- `git diff --check` passed after final doc write.
