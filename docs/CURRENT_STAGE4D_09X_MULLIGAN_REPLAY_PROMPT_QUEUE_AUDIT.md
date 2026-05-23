# Stage 4D-09X Mulligan Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official mulligan accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing official opening mulligan accepted replay and stale prompt replay tests in `OfficialOpeningTests`: `OfficialMulliganRejectsAcceptedReplayWithoutMutation` and `OfficialMulliganStalePromptReplayAfterSecondPlayerWindowStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale first-player `MULLIGAN` commands without mutation; this slice binds both accepted results and both rejected replay results to the second-player mulligan prompt, snapshot and idle queue contract after the first mulligan player completes and the opening window moves to the second player.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertOfficialMulliganSecondPlayerPromptQueueAudit(...)`.
- Reused the helper for the accepted first-player mulligan result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped first-player mulligan result and rejected stale prompt replay result.
- The audit proves the final state remains `IN_PROGRESS`, `MULLIGAN` / `MULLIGAN`, with the first player still active / turn player, the first player completed, and the second player still waiting to mulligan.
- It proves both players keep four-card hands and thirty-five-card main decks after opening draw / first mulligan, and every selected first-player hand object moved from hand to that player's main deck with authoritative `MAIN_DECK` object location.
- It proves no stack items, no priority / focus player, no battlefield tasks and an idle pending-task queue.
- It proves snapshots expose the same mulligan phase / timing, active / turn player and idle pending-task queue.
- It proves the completed player's prompt is non-actionable without `MULLIGAN`, while the waiting player's prompt is actionable `MULLIGAN` with min/max selection metadata and source candidates exactly matching the waiting player's current hand.

## Non-Closure

This narrows official mulligan replay / stale prompt final prompt-queue and object-location drift risk only. It does not close submit-deck breadth, ready breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
