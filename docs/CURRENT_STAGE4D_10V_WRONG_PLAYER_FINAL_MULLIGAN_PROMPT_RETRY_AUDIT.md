# Stage 4D-10V Wrong-Player Final Mulligan Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official wrong-player prompt-scoped final mulligan retry / first-turn prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `WrongPlayerPromptScopedFinalMulliganKeepsSecondPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt validation path already compares prompt id / snapshot tick against the submitting player's current prompt, so after the first player completes mulligan, the wrong active player carrying the second player's final `MULLIGAN` prompt is rejected before final-mulligan execution. This slice binds that wrong-player final-prompt mismatch contract to no-mutation opening state, second prompt id / snapshot tick stability and successful second-player reuse of the original prompt.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `WrongPlayerPromptScopedFinalMulliganKeepsSecondPromptReusable`.
- Starts from official opening after both players submitted legal decks and readied.
- Lets the active first player complete the initial `MULLIGAN`, opening the second player's final mulligan prompt.
- Captures the second player's actionable final `MULLIGAN` prompt.
- Proves the wrong active player submitting a prompt-scoped final `MULLIGAN` with the second player's prompt id / snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese stale-window message.
- Proves the wrong-player final prompt-scoped rejection emits no events and preserves exact state hash, tick, RNG cursor, ready players, mulligan completion list, opening second-action player, both players' hands / main decks, snapshots and idle pending-task queue.
- Proves the second player's original final prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves the second player can then use the same prompt id / snapshot tick to submit a legal final `MULLIGAN`, emit `MULLIGAN_COMPLETED` and `MULLIGAN_PHASE_COMPLETED`, call runes, draw the active player's turn-start card, enter first-turn main action and clear stale `MULLIGAN` exposure through `AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(...)`.

## Non-Closure

This narrows official wrong-player prompt-scoped final mulligan rejection, second prompt reuse and first-turn prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
