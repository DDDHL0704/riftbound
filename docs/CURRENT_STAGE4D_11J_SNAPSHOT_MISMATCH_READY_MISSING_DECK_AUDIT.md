# Stage 4D-11J Snapshot-Mismatch READY Missing Deck Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official snapshot-mismatch prompt-scoped READY missing-deck / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `SnapshotMismatchPromptScopedReadyMissingDeckKeepsSubmitPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing snapshot freshness validation already rejects P1 sending `READY` with P1's current actionable `SUBMIT_DECK` prompt id but a stale snapshot tick before the missing-deck guard or room state mutation can consume the prompt. This slice binds that snapshot-mismatch READY missing-deck contract to the exact snapshot-expired diagnostic, no-mutation one-deck-submitted room state, P1 submit prompt stability, P2 ready prompt stability and successful reuse of P1's original submit prompt with the current snapshot tick.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `SnapshotMismatchPromptScopedReadyMissingDeckKeepsSubmitPromptReusable`.
- Starts from room setup after P2 has submitted a legal deck and P1 remains on actionable `SUBMIT_DECK`.
- Proves P1 submitting prompt-scoped `READY` with P1's current `SUBMIT_DECK` prompt id but stale snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese snapshot-expired message.
- Proves the snapshot-mismatch READY missing-deck rejection emits no events and preserves exact state hash, tick, ready players, P2 submitted decklist, P1 no-deck state, snapshots and idle pending-task queue.
- Proves P1's submit prompt and P2's ready prompt remain stable by preserving both prompt ids and snapshot ticks after rejection.
- Proves P1 can then use the same submit prompt id / current snapshot tick to submit a legal deck, emit one `DECK_SUBMITTED`, avoid premature official opening / match start, and expose both players' room-ready prompts through `AssertOfficialSubmitDeckBothReadyPromptQueueAudit(...)`.

## Non-Closure

This narrows official snapshot-mismatch READY missing-deck rejection, room prompt reuse and READY prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
