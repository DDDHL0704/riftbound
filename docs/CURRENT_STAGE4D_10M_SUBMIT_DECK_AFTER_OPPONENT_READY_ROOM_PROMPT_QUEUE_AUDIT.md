# Stage 4D-10M Submit-Deck After Opponent Ready Room Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official submit-deck-after-opponent-ready / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialSubmitDeckAfterOpponentReadyPreservesRoomPromptQueue` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing official room setup path already allowed the waiting player to submit a legal deck after the opponent had readied, without starting official opening before the waiting player also readied. This slice binds that bridge state to decklist, ready-state, prompt, snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialSubmitDeckAfterOpponentReadyPreservesRoomPromptQueue`.
- Starts from P1-submitted / P1-ready / P2-not-submitted room setup state and reuses `AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(...)`.
- Proves P2 `SUBMIT_DECK` accepts after P1 is already ready, emits exactly one `DECK_SUBMITTED`, and does not emit `OFFICIAL_OPENING_STARTED` or `MATCH_STARTED`.
- Proves both submitted decklists stay structurally unchanged after acceptance, only P1 is ready, P1 remains non-actionable on `WAIT`, P2 becomes actionable on `READY`, and `SUBMIT_DECK` is not exposed to either player.
- Proves a later P2 duplicate `SUBMIT_DECK` rejects with `ErrorCodes.PhaseNotAllowed` / `玩家已经提交相同卡组。` without events or state mutation, preserving exact hash, tick, decklists, ready state, room snapshots and idle pending-task queue.

## Non-Closure

This narrows official submit-deck-after-opponent-ready bridge, both-decks single-ready room prompt-queue and duplicate-submit no-mutation drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
