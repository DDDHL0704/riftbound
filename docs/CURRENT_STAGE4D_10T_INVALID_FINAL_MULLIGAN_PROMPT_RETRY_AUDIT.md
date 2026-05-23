# Stage 4D-10T Invalid Final Mulligan Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official invalid prompt-scoped final mulligan retry / first-turn prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `InvalidPromptScopedFinalMulliganKeepsSecondPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing second-player official mulligan rejection path already rejected an invalid prompt-scoped `MULLIGAN` without mutating the first-player-completed mulligan state or consuming the second player's prompt. This slice binds that retry contract to prompt-scoped raw commands, final-mulligan transition determinism and successful same-window first-turn advancement.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `InvalidPromptScopedFinalMulliganKeepsSecondPromptReusable`.
- Starts from official opening after both players submitted legal decks and readied.
- Completes the first player's legal mulligan and captures the second player's actionable `MULLIGAN` prompt.
- Proves a second-player prompt-scoped over-selection `MULLIGAN` rejects with `ErrorCodes.InvalidTarget` and the exact Chinese message `起手调整最多可选择 2 张牌。`.
- Proves the invalid prompt-scoped rejection emits no events and preserves exact state hash, tick, RNG cursor, ready players, mulligan completion list, opening second-action player, both players' hands / main decks, snapshots and idle pending-task queue.
- Proves the second player's mulligan prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves the same prompt id / snapshot tick can then accept a legal final `MULLIGAN`, emit `MULLIGAN_COMPLETED` and `MULLIGAN_PHASE_COMPLETED`, call runes, draw the active player's turn-start card, enter first-turn `MAIN_ACTION` and clear stale `MULLIGAN` exposure through `AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(...)`.

## Non-Closure

This narrows official invalid prompt-scoped final mulligan retry, first-turn advancement and mulligan prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
