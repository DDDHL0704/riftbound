# Stage 4D-09S Recycle Rune Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 recycle-rune accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing recycle-rune accepted replay and stale prompt replay tests in `ConformanceFixtureRunnerTests`: `CoreRuleEngineRejectsAcceptedRecycleRuneReplayWithoutMutation` and `RecycleRuneStalePromptReplayAfterRuneMovesToRuneDeckRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale `RECYCLE_RUNE` commands without mutation; this slice binds both accepted results and both rejected replay results to the ordinary main-window prompt, snapshot and idle queue contract after the rune moves to the rune deck.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertRecycleRuneOrdinaryMainPromptQueueAudit(...)`.
- Reused the helper for the accepted recycle-rune result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped recycle-rune result and rejected stale prompt replay result.
- The audit proves the final state remains P1 active / turn player in `MAIN` / `NEUTRAL_OPEN`, with red power 1, the recycled rune moved from base to rune deck, no pending tasks, no stack items, no battlefield tasks and no priority / focus player.
- It proves snapshots expose P1 active / turn player, `MAIN` / `NEUTRAL_OPEN`, empty stack and idle pending-task queue.
- It proves P1 stays on the authoritative actionable `MAIN_ACTION` prompt with `END_TURN`, and the recycled rune is absent from `RECYCLE_RUNE` candidate sources for accepted and rejected replay results.
- It proves P2 remains non-actionable without `RECYCLE_RUNE`.

## Non-Closure

This narrows recycle-rune replay / stale prompt final prompt-queue drift risk only. It does not close full rune-resource breadth, tap-rune breadth, full payment-resource breadth, full turn lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
