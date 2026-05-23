# Stage 4D-10B Short Main Deck Mulligan Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official short-main-deck mulligan completion prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing official opening short-main-deck mulligan test in `OfficialOpeningTests`: `OfficialMulliganWithShortMainDeckDrawsAvailableCardsAndReturnsSetAside`.

Runtime behavior was not changed. The existing short-main-deck mulligan completion path already drew only available replacement cards, returned set-aside cards to main deck and avoided burnout; this slice binds that success path to event payload, zone / object-location, prompt / snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertOfficialShortMainDeckMulliganPromptQueueAudit(...)`.
- Bound the helper to `OfficialMulliganWithShortMainDeckDrawsAvailableCardsAndReturnsSetAside`.
- The audit proves accepted short-deck `MULLIGAN` remains `IN_PROGRESS`, `MULLIGAN` / `MULLIGAN`, increments tick / RNG cursor as expected, records P1 completed and leaves P2 waiting.
- It proves the selected P1 hand cards leave hand, return to P1 main deck with authoritative `MAIN_DECK` object locations, and the only available main-deck card moves to P1 hand with authoritative `HAND` object location.
- It proves `MULLIGAN_COMPLETED` carries stable `playerId`, `setAsideCount`, `drawnCount` and `returnedCount`, while no `MULLIGAN_PHASE_COMPLETED` or `BURNOUT_APPLIED` event is emitted.
- It proves snapshots keep mulligan timing, empty stack, no priority / focus player and an idle pending-task queue.
- It proves P1 becomes non-actionable without `MULLIGAN`, while P2 remains actionable for `MULLIGAN` with source candidates exactly matching P2's current hand and no leaked P1 selected or drawn object ids.

## Non-Closure

This narrows official mulligan short-main-deck completion prompt-queue, draw/return and no-burnout drift risk only. It does not close submit-deck breadth, ready breadth, remaining mulligan completion breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
