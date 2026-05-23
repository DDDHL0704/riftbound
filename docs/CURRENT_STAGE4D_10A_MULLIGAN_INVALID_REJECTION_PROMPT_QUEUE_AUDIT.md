# Stage 4D-10A Mulligan Invalid Rejection Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official mulligan invalid rejection prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing official opening invalid mulligan test in `OfficialOpeningTests`: `OfficialMulliganRejectsInvalidSelectionsAndWrongPlayer`.

Runtime behavior was not changed. The existing invalid mulligan paths already rejected wrong-player, too-many, duplicate and non-hand selections without mutation; this slice binds all four rejected results to exact state-hash preservation, empty event output and the still-current active-player mulligan prompt / snapshot / idle queue contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertOfficialMulliganInvalidRejectionPromptQueueAudit(...)`.
- Bound the helper to wrong-player `MULLIGAN`, too-many selections, duplicate selections and non-hand selections.
- The audit proves every rejected result is non-accepted, emits no events, preserves `MatchStateHasher.Hash(...)`, tick, RNG cursor, ready players, mulligan completion list, second-action player id and both players' hand / main-deck zones.
- It proves the state remains `IN_PROGRESS`, `MULLIGAN` / `MULLIGAN`, with no completed mulligan players, no stack items, no priority / focus player, no battlefield tasks and an idle pending-task queue.
- It proves snapshots expose the same active player, turn player, mulligan phase / timing and idle pending-task queue.
- It proves the active player's prompt remains actionable `MULLIGAN` with source candidates exactly matching the active player's current hand, while the second player's prompt remains non-actionable without `MULLIGAN`.

## Non-Closure

This narrows official mulligan invalid-rejection prompt-queue and no-mutation drift risk only. It does not close submit-deck breadth, ready breadth, mulligan completion breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
