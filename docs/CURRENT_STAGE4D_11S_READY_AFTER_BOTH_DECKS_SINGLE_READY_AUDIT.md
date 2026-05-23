# Stage 4D-11S Ready After Both Decks Single Ready Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official prompt-scoped READY-after-both-decks single-ready / opponent final READY prompt reuse audit slice. Project remains **NOT READY**.

## Scope

This slice adds `PromptScopedReadyAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing READY idempotency path already accepts P1 carrying P1's current non-actionable WAIT prompt id / snapshot tick on `READY` after both players have submitted legal decks and P1 is already ready while P2 remains ready-able. The existing room prompt builders preserve P1's WAIT prompt and P2's current READY prompt after the idempotent replay. This slice binds that contract to no-mutation single-ready room state, prompt stability and successful reuse of P2's current READY prompt into the official mulligan opening.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `PromptScopedReadyAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable`.
- Starts from room setup after P1 and P2 have both submitted legal decks, P1 has accepted READY and now holds non-actionable `WAIT`, while P2 remains actionable on `READY`.
- Proves P1 carrying P1's current WAIT prompt id / snapshot tick on `READY` accepts as an idempotent no-op.
- Proves the replay emits no events and preserves exact state hash, tick, ready players, both submitted decklists, snapshots and idle pending-task queue.
- Proves P1's WAIT prompt and P2's READY prompt remain stable by preserving both prompt ids and snapshot ticks after replay.
- Proves P2 can then use the same READY prompt id / snapshot tick to accept final READY, enter `IN_PROGRESS` / `MULLIGAN`, emit `PLAYER_READY`, `OFFICIAL_OPENING_STARTED` and `MATCH_STARTED`, expose the official mulligan prompt queue through `AssertOfficialReadyMulliganPromptQueueAudit(...)`, and clear stale `READY` / `SUBMIT_DECK` room actions and candidates from all prompts.

## Non-Closure

This narrows official prompt-scoped READY-after-both-decks single-ready idempotent replay, opponent final READY prompt reuse and room-to-mulligan prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
