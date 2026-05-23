# Stage 4D-09N Surrender Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 surrender replay / terminal prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing surrender accepted replay and stale prompt replay tests in `ConformanceFixtureRunnerTests`: `CoreRuleEngineRejectsAcceptedSurrenderReplayWithoutMutation` and `SurrenderStalePromptReplayAfterMatchFinishedRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale `SURRENDER` commands without mutation; this slice binds both accepted results and both rejected replay results to a finished-match prompt, snapshot and idle queue contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertSurrenderFinishedPromptQueueAudit(...)`.
- Reused the helper for the accepted surrender result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped surrender result and rejected stale prompt replay result.
- The audit proves the final state remains `FINISHED` with winner P2, no pending tasks, no stack items, no priority / focus player and no blocking queue.
- It proves P1 and P2 snapshots expose winner P2, empty stack and idle pending-task queue.
- It proves both players receive non-actionable `MATCH_RESULT` prompts with only `WAIT`, and stale `SURRENDER` is absent from prompts.

## Non-Closure

This narrows terminal surrender replay / stale prompt final prompt-queue drift risk only. It does not close full win/loss breadth, full terminal-state breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
