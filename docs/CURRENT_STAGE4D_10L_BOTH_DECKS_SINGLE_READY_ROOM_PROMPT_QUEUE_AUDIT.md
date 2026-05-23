# Stage 4D-10L Both-Decks Single-Ready Room Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official both-decks-submitted single-ready / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialSingleReadyAfterBothDecksSubmittedPreservesRoomPromptQueue` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing official room setup path already kept the match in room setup after one player readied while both decks were submitted and the other player was not ready. This slice binds that waiting state to decklist, ready-state, prompt, snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialSingleReadyAfterBothDecksSubmittedPreservesRoomPromptQueue`.
- Starts from P1-submitted / P2-submitted room setup state and reuses `AssertOfficialSubmitDeckBothReadyPromptQueueAudit(...)`.
- Proves P1 `READY` accepts with exactly one `PLAYER_READY`.
- Proves single-player ready does not emit `OFFICIAL_OPENING_STARTED` or `MATCH_STARTED`.
- Proves both submitted decklists stay structurally unchanged, only P1 is ready, P1 becomes non-actionable on `WAIT`, P2 remains actionable on `READY`, and `SUBMIT_DECK` is not exposed to either player.
- Proves a later P1 `READY` replay accepts without events and preserves exact state hash, tick, ready state, decklists, room snapshots and idle pending-task queue.

## Non-Closure

This narrows official both-decks-submitted single-ready room prompt-queue and READY replay drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
