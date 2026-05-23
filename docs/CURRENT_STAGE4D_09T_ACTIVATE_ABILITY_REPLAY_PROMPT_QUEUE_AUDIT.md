# Stage 4D-09T Activate Ability Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 Fluft Poro activate-ability accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing Fluft Poro activated ability accepted replay and stale prompt replay tests in `FluftPoroActivatedAbilityTests`: `FluftPoroRejectsAcceptedActivationReplayWithoutMutation` and `FluftPoroActivationStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale `ACTIVATE_ABILITY` commands without mutation; this slice binds both accepted results and both rejected replay results to the stack-priority prompt, snapshot and idle queue contract after Fluft Poro exhausts and creates its Warhawk stack item.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertFluftPoroStackPriorityPromptQueueAudit(...)`.
- Reused the helper for the accepted Fluft Poro activation result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped activation result and rejected stale prompt replay result.
- The audit proves the final state remains P1 active / turn player in `MAIN` / `NEUTRAL_CLOSED`, with P1 priority, Fluft Poro exhausted on battlefield `P1-MAIN`, no Warhawk tokens created before stack resolution, no pending tasks, no battlefield tasks and one Fluft Poro Warhawk stack item with no targets.
- It proves snapshots expose P1 active / turn player, `MAIN` / `NEUTRAL_CLOSED`, the same stack item and idle pending-task queue.
- It proves P1 stays on the authoritative actionable `STACK_PRIORITY` prompt with `PASS_PRIORITY`, with no stale `ACTIVATE_ABILITY` action or Fluft source candidate for accepted and rejected replay results.
- It proves P2 remains non-actionable without `ACTIVATE_ABILITY` / `PASS_PRIORITY`.

## Non-Closure

This narrows activated-ability replay / stale prompt final prompt-queue drift risk only. It does not close full activated-ability official breadth, full resource-skill breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
