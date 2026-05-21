# 4D-03JO-E Card Matrix Readiness Payment-Cost Arena Bar Equipment Layer Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-52ad18b853 / OGN·124/298 / 竞技场酒吧 / ARENA_BAR_PLAY_EQUIPMENT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps `ARENA_BAR_PLAY_EQUIPMENT` as direct card behavior for `OGN·124/298`.
- Existing runtime surface: `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` handle the authoritative play prompt, payment, zero-target equipment play, stack resolution, equipment object creation, and server snapshot.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers `CoreRuleEnginePlaysArenaBarEquipment`, `CoreRuleEngineRejectsArenaBarWhenTargetsAreProvided`, and `P4ArenaBarTargetRejectedFixture`.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-arena-bar-equipment.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p4-play-arena-bar-target-rejected.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the audited equipment play path and explicit-target rejection.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `252 -> 251`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `171 -> 170`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `440 -> 439`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `159 -> 159`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Arena Bar automated evidence disposition, tap-to-grant-boon equipment skill, complete layer / continuous-effect matrix, complete equipment lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `ArenaBar|Equipment` focused prevalidation passed: `374/374`.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `ArenaBar|Equipment` focused regression passed: `377/377`.
- Adjacent prompt/payment/target/stack regression passed: `2105/2105`.
- `PaymentEngineCoverageAuditTests` passed: `516/516`.
- Backend full test passed: `5087/5087`.
- `git diff --check` passed.
- Chrome smoke was not run for 03JO because this candidate did not change frontend or browser-script files.
