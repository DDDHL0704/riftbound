# Stage 4D-11K Single READY Prompt Replay Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official prompt-scoped single-READY replay / opponent submit prompt reuse audit slice. Project remains **NOT READY**.

## Scope

This slice adds `PromptScopedSingleReadyReplayBeforeOpponentDeckKeepsSubmitPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt expiry path already rejects a replayed prompt-scoped `READY` after P1 has accepted READY and the room has moved to P1 `WAIT` plus P2 `SUBMIT_DECK`, while the existing room prompt builders preserve P2's current submit prompt. This slice binds that accepted READY prompt replay contract to exact stale-window diagnostics, no-mutation single-ready room state, P1 wait prompt stability, P2 submit prompt stability and successful reuse of P2's current submit prompt.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `PromptScopedSingleReadyReplayBeforeOpponentDeckKeepsSubmitPromptReusable`.
- Starts from room setup after P1 has submitted a legal deck and remains on actionable `READY`.
- Proves P1's prompt-scoped `READY` accepts, emits one `PLAYER_READY`, avoids premature official opening / match start and exposes P1 `WAIT` plus P2 `SUBMIT_DECK`.
- Proves replaying P1's old accepted READY prompt rejects with `ErrorCodes.PromptExpired` and the exact Chinese stale-window message.
- Proves the accepted READY prompt replay rejection emits no events and preserves exact state hash, tick, ready players, P1 submitted decklist, P2 no-deck state, snapshots and idle pending-task queue.
- Proves P1's wait prompt and P2's submit prompt remain stable by preserving both prompt ids and snapshot ticks after rejection.
- Proves P2 can then use the same submit prompt id / snapshot tick to submit a legal deck, emit one `DECK_SUBMITTED`, avoid premature official opening / match start, and expose the both-decks / single-ready room prompts through `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.

## Non-Closure

This narrows official prompt-scoped single-READY replay rejection, opponent submit prompt reuse and room prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
