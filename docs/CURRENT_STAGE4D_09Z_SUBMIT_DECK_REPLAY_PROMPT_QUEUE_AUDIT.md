# Stage 4D-09Z Submit Deck Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official submit-deck accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing official opening submit-deck accepted replay and stale prompt replay tests in `OfficialOpeningTests`: `SubmitDeckRejectsAcceptedReplayWithoutMutation` and `SubmitDeckStalePromptReplayAfterReadyPromptStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already preserved room setup state without mutation; this slice binds the one-deck-submitted and both-decks-submitted outcomes to room setup prompts, snapshots and idle queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(...)`.
- Added `AssertOfficialSubmitDeckBothReadyPromptQueueAudit(...)`.
- Added shared room setup queue and decklist assertion helpers.
- Reused the helpers for the accepted submit result, accepted-command replay rejection result, accepted prompt-scoped submit result and rejected stale prompt replay result.
- The audit proves a one-deck-submitted room keeps `SEATING`, `ROOM` / `ROOM`, no ready players, the submitted decklist structurally unchanged, idle pending-task queue, empty stack, no battlefield tasks, no priority / focus player, submitted player actionable `READY`, and waiting player actionable `SUBMIT_DECK`.
- It proves a both-decks-submitted room keeps both decklists structurally unchanged with both players actionable `READY` and no stale `SUBMIT_DECK` action.
- It proves room setup snapshots expose room phase / timing, no stack, no priority / focus player and an idle pending-task queue.

## Non-Closure

This narrows official submit-deck replay / stale prompt final room-setup prompt-queue and decklist drift risk only. It does not close ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
