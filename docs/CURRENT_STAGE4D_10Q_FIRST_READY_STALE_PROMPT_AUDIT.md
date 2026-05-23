# Stage 4D-10Q First Ready Stale Prompt Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official first-ready stale prompt / mulligan prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialFirstReadyBothDecksPromptReplayAfterFinalReadyRejectsWithoutMutation` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt-expiry path already rejected a replay of P1's prompt-scoped first `READY` after both decks had been submitted, P1 used that prompt to enter the single-ready room state, and P2 final `READY` advanced the match into official opening / mulligan. This slice binds that rejection to room-to-mulligan transition determinism, exact no-mutation assertions, and absence of stale room actions in mulligan prompts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialFirstReadyBothDecksPromptReplayAfterFinalReadyRejectsWithoutMutation`.
- Starts from both players submitted legal decks and reuses `AssertOfficialSubmitDeckBothReadyPromptQueueAudit(...)`.
- Captures P1's prompt-scoped `READY` command from the both-decks room setup prompt.
- Proves P1 prompt-scoped `READY` accepts with one `PLAYER_READY`, does not start official opening or match start, and preserves the both-decks / single-ready room prompt contract through `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.
- Proves P2 final `READY` accepts, records `["P1", "P2"]`, enters `IN_PROGRESS`, `MULLIGAN` / `MULLIGAN`, emits official opening / match start events, and exposes only mulligan prompts with no stale `READY` or `SUBMIT_DECK` room actions or candidates.
- Proves replaying the original P1 prompt-scoped `READY` after final-ready opening rejects with `ErrorCodes.PromptExpired` and `行动窗口已过期，请按最新提示重新提交。`.
- Proves stale-prompt rejection emits no events and preserves exact state hash, tick, RNG cursor, ready players, hands, main decks, mulligan completion list, second-action player id, snapshots and idle pending-task queue.
- Proves stale-prompt rejection keeps the same mulligan prompt queue and does not reintroduce stale room actions or candidates.

## Non-Closure

This narrows official first-ready stale prompt rejection, room-to-mulligan prompt-queue and stale room-action cleanup drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
