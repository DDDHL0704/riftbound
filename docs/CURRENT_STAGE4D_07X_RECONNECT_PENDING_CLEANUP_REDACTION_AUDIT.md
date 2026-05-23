# Stage 4D-07X Reconnect Pending Cleanup Redaction Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / reconnect pending-cleanup redaction audit slice. Project remains **NOT READY**.

## Scope

This slice covers reconnecting an opponent while a state-based cleanup task is blocking on a hidden illegal standby card. It verifies the reconnect response, opponent snapshot and opponent prompt preserve the pending cleanup queue while redacting hidden object identity and raw cleanup internals.

The accepted coverage proves the reconnect session keeps P2 identity / seat / token stable; the opponent battlefield lane exposes no hidden standby ids but does expose `hiddenStandbyCount` and the pending task kind; the pending task queue stays `STATE_BASED_CLEANUP`, blocking and taskful; the active task id and task item redact `P1-STANDBY-ILLEGAL-001`; the task exposes hidden object kind and public reason but no `objectId`; and the opponent prompt remains non-actionable WAIT / SURRENDER without raw cleanup kind, cleanup id or hidden object id leakage.

No runtime behavior change was required because the existing reconnect, snapshot and prompt redaction paths already preserve the cleanup queue and hide opponent standby identity.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `ReconnectWithPendingCleanupTaskPreservesQueueAndOpponentRedaction`.
- Added reconnect/session assertions, opponent battlefield lane redaction assertions, pending cleanup queue identity assertions and opponent WAIT prompt raw-detail redaction assertions.

## Non-Closure

This narrows reconnect pending-cleanup hidden-info redaction audit parity only. It does not close full reconnect breadth, full hidden-info random-zone breadth, full cleanup / replacement-duration breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
