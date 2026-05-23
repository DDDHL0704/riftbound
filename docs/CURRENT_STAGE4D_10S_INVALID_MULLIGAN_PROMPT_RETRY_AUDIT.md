# Stage 4D-10S Invalid Mulligan Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official invalid prompt-scoped mulligan retry / mulligan prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `InvalidPromptScopedMulliganKeepsActivePromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing official mulligan rejection path already rejected an invalid prompt-scoped `MULLIGAN` without mutating opening state or consuming the active mulligan prompt. This slice binds that retry contract to prompt-scoped raw commands, opening-zone preservation, prompt id / snapshot tick stability and a successful same-window valid mulligan resubmission.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `InvalidPromptScopedMulliganKeepsActivePromptReusable`.
- Starts from official opening after both players submitted legal decks and both players readied.
- Captures the active player's actionable `MULLIGAN` prompt at the initial mulligan window.
- Proves a prompt-scoped over-selection `MULLIGAN` rejects with `ErrorCodes.InvalidTarget` and the exact Chinese message `起手调整最多可选择 2 张牌。`.
- Proves the invalid prompt-scoped rejection emits no events and preserves exact state hash, tick, RNG cursor, ready players, mulligan completion list, opening second-action player, both players' hands / main decks, snapshots and idle pending-task queue.
- Proves the active mulligan prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves the same prompt id / snapshot tick can then accept a legal `MULLIGAN`, emit one `MULLIGAN_COMPLETED`, avoid premature `MULLIGAN_PHASE_COMPLETED`, mark the active player complete and expose the second player's current mulligan prompt through `AssertOfficialMulliganSecondPlayerPromptQueueAudit(...)`.

## Non-Closure

This narrows official invalid prompt-scoped mulligan retry and mulligan prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
