# Stage 4D-11D Wrong-Player Submit-Deck Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official wrong-player prompt-scoped submit-deck retry / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `WrongPlayerPromptScopedSubmitDeckKeepsRoomPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt validation path already rejects a player carrying another player's current `SUBMIT_DECK` prompt id before official deck submission mutates room state. This slice binds that submit-deck prompt ownership contract to the exact stale-window diagnostics, no-mutation room state, submit prompt id / snapshot tick stability and successful reuse of the original prompt by the rightful player.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `WrongPlayerPromptScopedSubmitDeckKeepsRoomPromptReusable`.
- Starts from room setup after P2 has submitted a legal official deck and P1 remains on actionable `SUBMIT_DECK`.
- Proves P2 submitting prompt-scoped `SUBMIT_DECK` with P1's current prompt id / snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese stale-window message.
- Proves the wrong-player submit-deck rejection emits no events and preserves exact state hash, tick, ready players, P1 no-deck state, P2 submitted decklist, snapshots and idle pending-task queue.
- Proves P1's original submit-deck prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves P1 can then use the same prompt id / snapshot tick to submit a legal deck, emit one `DECK_SUBMITTED`, avoid premature official opening / match start, and expose both players' room-ready prompts through `AssertOfficialSubmitDeckBothReadyPromptQueueAudit(...)`.

## Non-Closure

This narrows official wrong-player prompt-scoped submit-deck rejection, room prompt reuse and submit-deck prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
