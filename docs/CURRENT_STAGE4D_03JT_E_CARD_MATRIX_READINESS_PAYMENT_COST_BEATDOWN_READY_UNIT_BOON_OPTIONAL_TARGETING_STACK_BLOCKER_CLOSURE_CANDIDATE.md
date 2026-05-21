# 4D-03JT-E Card Matrix Readiness Payment-Cost Beatdown Ready Unit Boon Optional Targeting Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-66c9db7e53 / OGN·146/298 / 痛殴 / BEATDOWN_READY_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·146/298` to `BEATDOWN_READY_UNIT`, and `src/Riftbound.Engine/CoreRuleEngine.cs` resolves the payment, target and stack path used by the fixture.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the non-boon-consume baseline path that pays 2, targets a unit, resolves the stack, and readies that unit.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-beatdown-ready-unit.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the accepted representative evidence and the remaining optional boon-consume breadth.
- Target scope guard evidence: `docs/CURRENT_STAGE4C_BATCH63_ANY_UNIT_TARGET_SCOPE_GUARD_EVIDENCE.md` keeps adjacent any-unit targeting scope evidence for Beatdown.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `247 -> 246`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `168 -> 167`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `435 -> 434`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `155 -> 154`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Beatdown automated evidence disposition, optional boon-consume cost branch, complete ready / layer official breadth, battle / spell-duel adjacency, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Beatdown / ReadyUnit / AnyUnitTargetScope / TargetScope / TargetGuard focused regression passed: 32/32.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / Beatdown / Ready / Target / Stack adjacent regression passed: 2022/2022.
- `PaymentEngineCoverageAuditTests` passed: 526/526.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5097/5097.
- `git diff --check` passed after final doc write.
