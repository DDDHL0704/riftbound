# Stage 4D-10I Ready Missing-Deck Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official missing-deck READY rejection / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialReadyRejectsMissingSubmittedDeckWithoutPromptQueueMutation` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing official room setup path already rejected `READY` when another player had submitted a legal deck but the acting player had not. This slice binds that rejection to decklist, prompt, snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialReadyRejectsMissingSubmittedDeckWithoutPromptQueueMutation`.
- Starts from P2-submitted / P1-not-submitted room setup state and reuses `AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(...)`.
- Proves P1 `READY` rejects with `ErrorCodes.InvalidDeck` and exact Chinese message `正式卡组房间需要先提交合法卡组才能准备。`.
- Proves the rejection emits no events, preserves exact state hash and tick, and keeps P2's submitted decklist structurally unchanged.
- Proves P2 remains actionable on `READY`, P1 remains actionable on `SUBMIT_DECK`, snapshots stay at room timing, and the pending-task queue remains idle.

## Non-Closure

This narrows official missing-deck READY rejection, room setup prompt-queue and no-mutation drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
