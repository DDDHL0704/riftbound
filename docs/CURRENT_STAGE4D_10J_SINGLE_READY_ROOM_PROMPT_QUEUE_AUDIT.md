# Stage 4D-10J Single-Ready Room Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official single-ready / waiting-submit room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialSingleReadyBeforeOpponentDeckReplayPreservesRoomPromptQueue` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing official room setup path already allowed a player with a submitted legal deck to ready while the opponent still needed to submit a deck. This slice binds that accepted `READY` and accepted replay to room setup prompt, snapshot, decklist and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialSingleReadyBeforeOpponentDeckReplayPreservesRoomPromptQueue`.
- Starts from P1-submitted / P2-not-submitted room setup state and reuses `AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(...)`.
- Proves accepted P1 `READY` emits one `PLAYER_READY` event and does not emit `OFFICIAL_OPENING_STARTED` or `MATCH_STARTED` before P2 submits a legal deck.
- Proves the accepted single-ready state keeps P1's decklist structurally unchanged, records only P1 as ready, leaves P1 non-actionable on `WAIT`, leaves P2 actionable on `SUBMIT_DECK`, and keeps room snapshots plus pending-task queue idle.
- Proves a later P1 `READY` replay accepts without events or mutation, preserving state hash, tick, ready players, decklist, prompt shape and idle queue.

## Non-Closure

This narrows official single-ready / waiting-submit room setup, accepted READY replay and no-mutation drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
