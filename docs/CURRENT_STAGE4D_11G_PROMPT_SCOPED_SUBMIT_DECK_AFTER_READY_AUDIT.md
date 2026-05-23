# Stage 4D-11G Prompt-Scoped Submit-Deck After Ready Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official prompt-scoped submit-deck-after-ready / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `PromptScopedSubmitDeckAfterReadyKeepsOpponentSubmitPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt validation and ready-state deck-change guard already reject a readied player sending `SUBMIT_DECK` with that player's current `WAIT` room prompt before deck submission can mutate room state. This slice binds that prompt-scoped submit-deck-after-ready contract to the exact Chinese ready-state deck-change diagnostic, no-mutation single-ready room state, P1 wait prompt stability, P2 submit prompt stability and successful reuse of P2's original submit prompt.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `PromptScopedSubmitDeckAfterReadyKeepsOpponentSubmitPromptReusable`.
- Starts from room setup after P1 has submitted/readied and P2 remains on actionable `SUBMIT_DECK`.
- Proves P1 submitting prompt-scoped `SUBMIT_DECK` with P1's current non-actionable `WAIT` prompt id / snapshot tick rejects with `ErrorCodes.PhaseNotAllowed` and the exact Chinese "player cannot change deck after ready" message.
- Proves the prompt-scoped submit-deck-after-ready rejection emits no events and preserves exact state hash, tick, ready players, P1 submitted decklist, P2 no-deck state, snapshots and idle pending-task queue.
- Proves P1's current wait prompt and P2's submit prompt remain stable by preserving both prompt ids and snapshot ticks after rejection.
- Proves P2 can then use the same prompt id / snapshot tick to submit a legal deck, emit one `DECK_SUBMITTED`, avoid premature official opening / match start, and expose the both-decks / single-ready room prompts through `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.

## Non-Closure

This narrows official prompt-scoped submit-deck-after-ready rejection, room prompt reuse and submit-deck prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
