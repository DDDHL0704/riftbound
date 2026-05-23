# Stage 4D-09O End Turn Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 end-turn accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing end-turn accepted replay and stale prompt replay tests in `ConformanceFixtureRunnerTests`: `CoreRuleEngineRejectsAcceptedEndTurnReplayWithoutMutation` and `EndTurnStalePromptReplayAfterNextPlayerStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale `END_TURN` commands without mutation; this slice binds both accepted results and both rejected replay results to the next-player main-window prompt and idle queue contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertEndTurnNextPlayerPromptQueueAudit(...)`.
- Reused the helper for the accepted end-turn fixture-runner result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped end-turn result and rejected stale prompt replay result.
- The audit proves the final state remains P2 active / turn player in `MAIN` / `NEUTRAL_OPEN`, with no pending tasks, no stack items, no battlefield tasks and no priority / focus player.
- It proves ResolutionResult snapshots expose P2 active player, P2 turn player, `MAIN` / `NEUTRAL_OPEN`, empty stack and idle pending-task queue.
- It proves P1 has no actionable stale `END_TURN` prompt while P2 retains the authoritative actionable `MAIN_ACTION` prompt with `END_TURN`.

## Non-Closure

This narrows end-turn replay / stale prompt final prompt-queue drift risk only. It does not close full turn lifecycle breadth, turn-end cleanup breadth, turn-start trigger breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
