# 4D-03JW-E Card Matrix Readiness Payment-Cost Dragon Caller Cost-Static Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-f45bfb57e3 / OGN·140/298 / 唤龙使者 / DRAGON_CALLER_COST_STATIC_PLAY_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·140/298` to `DRAGON_CALLER_COST_STATIC_PLAY_UNIT` with fixed base-cost, zero target count and source-to-base unit creation metadata.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the static Dragon Caller representative that pays 4, requires no target selection, resolves the stack, and creates the 3-power unit in base.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-dragon-caller-cost-static.fixture.json`.
- Rejection fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-dragon-caller-target-rejected.fixture.json` proves that providing an explicit target for the current zero-target play path is rejected before payment, stack creation or unit entry.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the accepted representative evidence while keeping the Dragon-unit cost-reduction static path open.
- P2/P4 status evidence: `docs/CURRENT_P2_STATUS.md`, `docs/CURRENT_P4_STATUS.md` and `docs/conformance-fixture-format.md` record the current representative path and target-rejection conformance fixture.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `244 -> 243`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `165 -> 164`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `432 -> 431`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `152 -> 152`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Dragon Caller automated evidence disposition, Dragon-unit cost-reduction static path, complete PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- DragonCaller / DRAGON_CALLER / ConformanceFixtureRunnerTests focused regression passed: 3022/3022.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / DragonCaller / Dragon / Stack adjacent regression passed: 671/671.
- `PaymentEngineCoverageAuditTests` passed: 532/532.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5103/5103.
- `git diff --check` passed after final doc write.
