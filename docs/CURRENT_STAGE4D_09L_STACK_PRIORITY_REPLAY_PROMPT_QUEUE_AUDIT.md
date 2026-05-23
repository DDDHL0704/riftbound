# Stage 4D-09L Stack Priority Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 stack-priority replay / prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing stack-priority accepted replay and stale prompt replay tests in `BoardTaskQueueFoundationTests`: `PassPriorityRejectsAcceptedCommandReplayWithoutMutation` and `StackPriorityStalePromptReplayAfterNextStackItemStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing replay and prompt-expiry paths already rejected stale `PASS_PRIORITY` commands without mutation; this slice binds the accepted and rejected results to explicit stack, pending-task queue, snapshot queue and prompt contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertStackPriorityResolvedIdlePromptQueueAudit(...)` for the accepted pass-priority stack resolution and accepted-command replay result.
- Added `AssertStackPriorityNextItemPromptQueueAudit(...)` for the accepted prompt-scoped stack-priority pass and rejected stale prompt replay result.
- The idle helper proves the resolved stack is empty, priority is cleared, pending-task queues are idle in state and snapshots, battlefield tasks are absent, P2 defender moved to base, and no stack-priority prompt remains.
- The next-item helper proves exactly `STACK-FOLLOWUP-NOOP` remains on stack, P1 keeps priority, pending-task queues stay idle, P1 remains actionable on `PASS_PRIORITY`, P2 remains non-actionable, and the active prompt excludes stale `STACK-BATTLE-OR-FLIGHT` / old target metadata.

## Non-Closure

This narrows stack-priority replay / stale prompt final prompt-queue drift risk only. It does not close full priority/stack lifecycle breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
