# Stage 4D-10P Submit-Deck After Ready Stale Prompt Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official submit-deck-after-opponent-ready stale prompt / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialSubmitDeckAfterOpponentReadyStalePromptReplayRejectsWithoutMutation` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt-expiry path already rejected a replay of the prompt-scoped P2 `SUBMIT_DECK` command after P1 had submitted/readied, P2 used that prompt to submit late, and the room moved to the both-decks / single-ready prompt state. This slice binds that rejection to room prompt-queue determinism, decklist preservation and exact no-mutation assertions.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialSubmitDeckAfterOpponentReadyStalePromptReplayRejectsWithoutMutation`.
- Starts from P1-submitted / P1-ready / P2-not-submitted room setup state and reuses `AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(...)`.
- Captures P2's prompt-scoped `SUBMIT_DECK` command from the actionable room prompt.
- Proves P2 prompt-scoped `SUBMIT_DECK` accepts with one `DECK_SUBMITTED`, does not emit official opening or match start events, and preserves the both-decks / single-ready room prompt contract through `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.
- Proves replaying the same prompt-scoped P2 `SUBMIT_DECK` rejects with `ErrorCodes.PromptExpired` and `行动窗口已过期，请按最新提示重新提交。`.
- Proves stale-prompt rejection emits no events and preserves exact state hash, tick, ready players, both player decklists, snapshots and idle pending-task queue.
- Proves stale-prompt rejection keeps P1 on non-actionable `WAIT`, P2 on actionable `READY`, and does not reintroduce stale `SUBMIT_DECK` room actions.

## Non-Closure

This narrows official submit-deck-after-opponent-ready stale prompt rejection and room prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
