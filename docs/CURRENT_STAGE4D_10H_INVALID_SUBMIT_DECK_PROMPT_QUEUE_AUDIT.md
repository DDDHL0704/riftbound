# Stage 4D-10H Invalid Submit-Deck Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official invalid submit-deck rejection / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `OfficialOpeningTests`: `SubmitDeckRejectsInvalidOfficialDeckWithChineseMessage`.

Runtime behavior was not changed. The existing official deck validation path already rejected the invalid decklist with Chinese diagnostics. This slice binds that rejection to room setup prompt, snapshot and idle pending-queue contracts so invalid `SUBMIT_DECK` does not silently mutate opening state or prompt shape.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `SubmitDeckRejectsInvalidOfficialDeckWithChineseMessage`.
- Switched the invalid submit-deck path to `CoreRuleEngine` and ensured both players are seated before rejection.
- Preserved Chinese error assertions for `卡组不合法：` and `主牌堆至少需要 40 张牌。`, while continuing to reject English fallback text.
- Proved rejection emits no events, writes no player decklists, writes no ready players and remains `SEATING`, `ROOM` / `ROOM`.
- Reused `AssertRoomSetupIdlePromptQueue(...)` to prove priority / focus, stack, battlefield tasks and pending-task queue remain idle in state and snapshots.
- Proved both P1 and P2 remain actionable on `ROOM_SETUP` with only `SUBMIT_DECK`, no stale `READY`, and prompt snapshot ticks match state tick.

## Non-Closure

This narrows official invalid submit-deck rejection, room setup prompt-queue and no-mutation drift risk only. It does not close full submit-deck breadth, ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
