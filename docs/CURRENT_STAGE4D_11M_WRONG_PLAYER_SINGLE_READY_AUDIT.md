# Stage 4D-11M Wrong-Player Single READY Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official wrong-player prompt-scoped single-READY / room prompt reuse audit slice. Project remains **NOT READY**.

## Scope

This slice adds `WrongPlayerPromptScopedSingleReadyKeepsReadyPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt ownership path already rejects P2 carrying P1's current READY prompt id / snapshot tick after P1 has submitted a legal deck and P2 has not submitted yet. The existing room prompt builders preserve P1's current READY prompt and P2's current submit prompt after the rejection. This slice binds that contract to exact stale-window diagnostics, no-mutation one-deck-submitted room state, P1 READY prompt stability, P2 submit prompt stability and successful reuse of P1's current READY prompt.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `WrongPlayerPromptScopedSingleReadyKeepsReadyPromptReusable`.
- Starts from room setup after P1 has submitted a legal deck and remains on actionable `READY` while P2 remains on actionable `SUBMIT_DECK`.
- Proves P2 carrying P1's current READY prompt id / snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese stale-window message.
- Proves the rejection emits no events and preserves exact state hash, tick, ready players, P1 submitted decklist, P2 no-deck state, snapshots and idle pending-task queue.
- Proves P1's READY prompt and P2's submit prompt remain stable by preserving both prompt ids and snapshot ticks after rejection.
- Proves P1 can then use the same READY prompt id / snapshot tick to accept READY, emit one `PLAYER_READY`, avoid premature official opening / match start and expose P1 `WAIT` plus P2 `SUBMIT_DECK` through `AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(...)`.

## Non-Closure

This narrows official wrong-player prompt-scoped single-READY rejection, current READY prompt reuse and room prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
