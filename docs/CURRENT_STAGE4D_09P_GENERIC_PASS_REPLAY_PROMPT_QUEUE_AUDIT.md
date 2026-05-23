# Stage 4D-09P Generic Pass Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 generic pass accepted replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing ordinary main-window generic pass tests in `ConformanceFixtureRunnerTests`: `CoreRuleEngineAcceptsGenericPassInOrdinaryMainWindow` and `CoreRuleEngineRejectsAcceptedGenericPassReplayWithoutMutation`.

Runtime behavior was not changed. The existing generic `PASS` accepted replay path already rejected stale replay without mutation; this slice binds the accepted result and rejected replay result to the authoritative ordinary main-window prompt and idle queue contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertGenericPassOrdinaryMainPromptQueueAudit(...)`.
- Reused the helper for the accepted ordinary-main generic pass result.
- Reused the helper for the accepted-command replay rejection result.
- The audit proves the final state remains P1 active / turn player in `MAIN` / `NEUTRAL_OPEN`, with no pending tasks, no stack items, no battlefield tasks and no priority / focus player.
- It proves the generic pass leaves `PassedPriorityPlayerIds` as P1 while keeping the pending-task queue idle.
- It proves snapshots expose P1 active / turn player, `MAIN` / `NEUTRAL_OPEN`, empty stack and idle pending-task queue.
- It proves P1 remains on the authoritative actionable `MAIN_ACTION` prompt with `END_TURN` and without stale generic `PASS`, while P2 remains non-actionable without `PASS` or `END_TURN`.

## Non-Closure

This narrows ordinary main-window generic pass accepted replay prompt-queue drift risk only. It does not close full turn lifecycle breadth, full pass semantics breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
