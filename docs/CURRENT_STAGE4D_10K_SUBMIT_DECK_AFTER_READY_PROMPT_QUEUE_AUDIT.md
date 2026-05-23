# Stage 4D-10K Submit-Deck After Ready Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official submit-deck-after-ready rejection / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialSubmitDeckAfterReadyRejectsWithoutPromptQueueMutation` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing official room setup path already rejected deck changes after a player had readied. This slice binds that rejection to decklist, ready-state, prompt, snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialSubmitDeckAfterReadyRejectsWithoutPromptQueueMutation`.
- Starts from P1-submitted / P1-ready / P2-not-submitted room setup state and reuses `AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(...)`.
- Proves P1 `SUBMIT_DECK` after ready rejects with `ErrorCodes.PhaseNotAllowed` and exact Chinese message `玩家准备后不能更改卡组。`.
- Proves the rejection emits no events, preserves exact state hash and tick, keeps P1's ready state and submitted decklist structurally unchanged, leaves P1 non-actionable on `WAIT`, leaves P2 actionable on `SUBMIT_DECK`, and keeps room snapshots plus pending-task queue idle.

## Non-Closure

This narrows official submit-deck-after-ready rejection, single-ready room prompt-queue and no-mutation drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
