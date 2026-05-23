# Stage 4D-11B Wrong-Player READY Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official wrong-player prompt-scoped READY retry / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `WrongPlayerPromptScopedReadyKeepsRoomPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt validation path already rejects a player carrying another player's current prompt id before room ready state changes. This slice binds that READY prompt ownership contract to the exact stale-window diagnostics, no-mutation room state, READY prompt id / snapshot tick stability and successful reuse of the original prompt by the rightful player.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `WrongPlayerPromptScopedReadyKeepsRoomPromptReusable`.
- Starts from room setup after both players have submitted legal official decks and P1 remains on actionable `READY`.
- Proves P2 submitting prompt-scoped `READY` with P1's current prompt id / snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese stale-window message.
- Proves the wrong-player READY rejection emits no events and preserves exact state hash, tick, ready players, both submitted decklists, snapshots and idle pending-task queue.
- Proves P1's original READY prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves P1 can then use the same prompt id / snapshot tick to READY successfully, emit one `PLAYER_READY`, avoid premature official opening / match start, and expose the both-decks / single-ready room prompts through `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.

## Non-Closure

This narrows official wrong-player prompt-scoped READY rejection, room prompt reuse and READY prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
