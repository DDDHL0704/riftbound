# Stage 4D-07Y State-Based Cleanup Ordinary Action Block Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / state-based cleanup ordinary-action block audit slice. Project remains **NOT READY**.

## Scope

This slice covers a blocking state-based cleanup queue that has lethal cleanup ahead of battlefield contest, spell-duel and battle tasks, and rejects an ordinary `END_TURN` action before those server tasks are processed.

The accepted coverage proves the initial queue is blocking `STATE_BASED_CLEANUP` with stable active lethal cleanup task, stable queued task ids / kinds, and a WAIT / SURRENDER prompt that exposes the localized cleanup reason without raw task kind, cleanup id or object id leakage. It also proves the rejected ordinary action returns `PHASE_NOT_ALLOWED`, emits no events, preserves the exact state hash, preserves active task id and queued task ids / kinds, keeps the queue blocking, and returns a localized error without raw cleanup internals.

No runtime behavior change was required because the existing blocking pending-task guard already rejects ordinary actions before battlefield task advancement.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `StateBasedCleanupBlocksOrdinaryActionsBeforeBattlefieldTasks`.
- Added state-based cleanup block assertions for queue identity, prompt redaction, rejected action no-mutation behavior, event absence, state-hash preservation and error-message redaction.

## Non-Closure

This narrows state-based cleanup ordinary-action blocking audit parity only. It does not close full cleanup / replacement-duration breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
