# Stage 4D-09Q Play Card Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 play-card accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing Punishment play-card accepted replay and stale prompt replay tests in `ConformanceFixtureRunnerTests`: `CoreRuleEngineRejectsAcceptedPlayCardReplayWithoutMutation` and `PlayCardStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale `PLAY_CARD` commands without mutation; this slice binds both accepted results and both rejected replay results to the stack-priority prompt, snapshot and idle queue contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertPlayCardStackPriorityPromptQueueAudit(...)`.
- Reused the helper for the accepted play-card result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped play-card result and rejected stale prompt replay result.
- The audit proves the final state remains P1 active / turn player in `MAIN` / `NEUTRAL_CLOSED`, with P1 priority, no pending tasks, no battlefield tasks and no focus player.
- It proves exactly one Punishment stack item remains, sourced from `P1-SPELL-PUNISHMENT`, targeting `P2-UNIT-001`, with the source in `STACK`.
- It proves snapshots expose the same stack item, P1 priority and idle pending-task queue.
- It proves P1 has the authoritative actionable `STACK_PRIORITY` prompt for that stack item with `PASS_PRIORITY` and no stale `PLAY_CARD`, while P2 remains non-actionable without `PLAY_CARD` or `PASS_PRIORITY`.

## Non-Closure

This narrows play-card replay / stale prompt final stack-priority prompt-queue drift risk only. It does not close full play-card official breadth, full targeting-stack breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
