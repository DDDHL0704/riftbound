# Stage 4D-07U Stack Move To Base Idle Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / stack move-to-base idle audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful stack-resolution path where `BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE` moves an opposing battlefield unit back to its owner base, removes the battlefield contest and returns the pending task queue to idle.

The accepted coverage proves `PRIORITY_PASSED`, `STACK_ITEM_RESOLVED` and `UNIT_MOVED_TO_BASE` payload identity and ordering; the target unit returns to P2 base; `BF-CONTEST` remains controlled and uncontested with only the P1 attacker as occupant; and the pending task queue has no tasks, no active task and no blocking state. It also verifies no `BATTLEFIELD_CONTESTED`, `SPELL_DUEL_STARTED` or `UNIT_DESTROYED` side effects are emitted.

No runtime behavior change was required because the existing stack resolution and battlefield-state reconciliation already satisfy this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `StackResolutionMoveToBaseCanRemoveContestAndReturnQueueToIdle`.
- Added stack-resolution assertions for priority pass, resolved stack item, move-to-base event payload, event ordering, battlefield occupant/control state, idle pending-task queue shape and absence of contest / spell-duel / destroy side effects.

## Non-Closure

This narrows stack move-to-base contest-removal idle audit parity only. It does not close full movement legality breadth, full stack lifecycle breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
