# Stage 4D-11O Wrong-Player Submit After Ready Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official wrong-player prompt-scoped submit-deck-after-ready / opponent submit prompt reuse audit slice. Project remains **NOT READY**.

## Scope

This slice adds `WrongPlayerPromptScopedSubmitDeckAfterReadyKeepsOpponentSubmitPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt ownership path already rejects P2 carrying P1's current non-actionable WAIT prompt id / snapshot tick on `SUBMIT_DECK` after P1 has submitted and readied while P2 has not submitted yet. The existing room prompt builders preserve P1's WAIT prompt and P2's current submit prompt after the rejection. This slice binds that contract to exact stale-window diagnostics, no-mutation single-ready room state, P1 WAIT prompt stability, P2 submit prompt stability and successful reuse of P2's current submit prompt.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `WrongPlayerPromptScopedSubmitDeckAfterReadyKeepsOpponentSubmitPromptReusable`.
- Starts from room setup after P1 has submitted a legal deck, accepted READY and now holds non-actionable `WAIT`, while P2 remains actionable on `SUBMIT_DECK`.
- Proves P2 carrying P1's current WAIT prompt id / snapshot tick on `SUBMIT_DECK` rejects with `ErrorCodes.PromptExpired` and the exact Chinese stale-window message.
- Proves the rejection emits no events and preserves exact state hash, tick, ready players, P1 submitted decklist, P2 no-deck state, snapshots and idle pending-task queue.
- Proves P1's WAIT prompt and P2's submit prompt remain stable by preserving both prompt ids and snapshot ticks after rejection.
- Proves P2 can then use the same submit prompt id / snapshot tick to submit a legal deck, emit one `DECK_SUBMITTED`, avoid premature official opening / match start and expose the both-decks / single-ready room prompts through `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.

## Non-Closure

This narrows official wrong-player prompt-scoped submit-deck-after-ready rejection, opponent submit prompt reuse and room prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
