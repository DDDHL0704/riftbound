# Stage 4D-11F Snapshot-Mismatch Late Submit-Deck Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official snapshot-mismatch prompt-scoped late submit-deck retry / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `SnapshotMismatchPromptScopedSubmitDeckAfterOpponentReadyKeepsRoomPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt validation path already rejects a rightful player carrying the current late `SUBMIT_DECK` prompt id with a stale snapshot tick before ready-state deck-change guards or deck submission can mutate room state. This slice binds that late submit-deck snapshot freshness contract to the exact snapshot-expired diagnostics, no-mutation single-ready room state, late submit prompt id / snapshot tick stability and successful reuse of the original prompt with the current snapshot tick.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `SnapshotMismatchPromptScopedSubmitDeckAfterOpponentReadyKeepsRoomPromptReusable`.
- Starts from room setup after P1 has submitted/readied and P2 remains on actionable `SUBMIT_DECK`.
- Proves P2 submitting prompt-scoped `SUBMIT_DECK` with P2's current prompt id but stale snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese snapshot-expired message instead of mutating or changing decks.
- Proves the snapshot-mismatch late submit-deck rejection emits no events and preserves exact state hash, tick, ready players, P1 submitted decklist, P2 no-deck state, snapshots and idle pending-task queue.
- Proves P2's original late submit-deck prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves P2 can then use the same prompt id with the current snapshot tick to submit a legal deck, emit one `DECK_SUBMITTED`, avoid premature official opening / match start, and expose the both-decks / single-ready room prompts through `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.

## Non-Closure

This narrows official snapshot-mismatch prompt-scoped late submit-deck rejection, room prompt reuse and submit-deck prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
