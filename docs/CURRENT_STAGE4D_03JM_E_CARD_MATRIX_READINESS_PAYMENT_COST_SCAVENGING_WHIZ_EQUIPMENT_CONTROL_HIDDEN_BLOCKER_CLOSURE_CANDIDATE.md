# 4D-03JM-E Card Matrix Readiness Payment-Cost Scavenging Whiz Equipment Control/Hidden Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-dcce660783 / OGN·099/298 / 拾荒小能手 / SCAVENGING_WHIZ_PLAY_EQUIPMENT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps `SCAVENGING_WHIZ_PLAY_EQUIPMENT` as direct card behavior with cost 2, zero targets, and `PlaysSourceToBaseAsEquipment=true`.
- Existing runtime surface: `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` handle the authoritative play prompt, payment, zero-target stack item, pass/pass resolution, equipment object creation, and server snapshot.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers `CoreRuleEnginePlaysScavengingWhizEquipment`, `CoreRuleEngineRejectsScavengingWhizWhenTargetsAreProvided`, and `P4ScavengingWhizTargetRejectedFixture`.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-scavenging-whiz-equipment.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p4-play-scavenging-whiz-target-rejected.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the audited 0-target equipment play path and explicit-target rejection.
- Rules reference retained from matrix: `CORE-260330 p59`.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `254 -> 253`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `173 -> 172`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `442 -> 441`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `160 -> 160`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Scavenging Whiz automated evidence disposition, activated recycle/pay/exhaust draw skill, control-zone movement breadth, hidden-info / random-zone breadth, complete equipment lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `ScavengingWhiz|Equipment` focused regression 375/375 passed; `ActionPrompt|Prompt|PaymentResource|SpendPower|RunePool|Equipment|ScavengingWhiz|Target` adjacent regression 1971/1971 passed; `PaymentEngineCoverageAuditTests` 512/512 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5083/5083 passed; `git diff --check` passed.
- Chrome smoke not run because there were no frontend or browser-script changes.
