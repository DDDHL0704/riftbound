# Stage 4D-09Y Ready Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official ready accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing official opening ready accepted replay and stale prompt replay tests in `OfficialOpeningTests`: `OfficialReadyAcceptsAcceptedReplayWithoutMutation` and `OfficialReadyStalePromptReplayAfterMulliganStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already preserved the started mulligan state without mutation; this slice binds both accepted ready results and both replay results to the initial active-player mulligan prompt, snapshot and idle queue contract after both players are ready and official opening begins.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertOfficialReadyMulliganPromptQueueAudit(...)`.
- Reused the helper for the accepted ready result and accepted ready replay result.
- Reused the helper for the accepted prompt-scoped ready result and rejected stale prompt replay result.
- The audit proves the final state remains `IN_PROGRESS`, `MULLIGAN` / `MULLIGAN`, with both players ready, no completed mulligan players, and stable active / second action player identity.
- It proves both players keep official opening zones: thirty-five cards in main deck, twelve runes, four-card hand, one battlefield, one legend and one champion.
- It proves no stack items, no priority / focus player, no battlefield tasks and an idle pending-task queue.
- It proves snapshots expose the same active player, turn player, mulligan phase / timing and idle pending-task queue.
- It proves the active player's prompt is actionable `MULLIGAN` with min/max selection metadata and source candidates exactly matching the active player's current hand, while the second player's prompt is non-actionable without `MULLIGAN`.

## Non-Closure

This narrows official ready replay / stale prompt final prompt-queue and opening-zone drift risk only. It does not close submit-deck breadth, mulligan completion breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
