# Stage 4D-07V Cleanup Repeat Equipment Recall Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / cleanup repeat equipment-recall audit slice. Project remains **NOT READY**.

## Scope

This slice covers the state-based cleanup loop where resolving a stack item destroys a lethal equipped host, detaches its equipment, then immediately repeats cleanup to recall the now-unattached battlefield equipment to base and return the queue to idle.

The accepted coverage proves `PRIORITY_PASSED`, `STACK_ITEM_RESOLVED`, `UNIT_DESTROYED` and `EQUIPMENT_RECALLED_TO_BASE` payload identity and ordering; the lethal host moves to graveyard and is removed from card objects; the detached equipment keeps its controller, clears `AttachedToObjectId`, moves to base with precise object location, and records `UNATTACHED_EQUIPMENT_CLEANUP`; `BF-1` remains controlled and uncontested with no occupants; and the pending task queue has no tasks, no active task and no blocking state.

No runtime behavior change was required because the existing cleanup loop already performs lethal cleanup, equipment detach, repeated unattached-equipment cleanup and idle queue reconciliation.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `CleanupLoopRepeatsWhenDestroyedHostCreatesUnattachedEquipmentCandidate`.
- Added cleanup-repeat assertions for priority pass, stack resolution, lethal destroy, equipment recall, event ordering, detached equipment state, battlefield occupant/control state and idle pending-task queue shape.

## Non-Closure

This narrows cleanup-repeat equipment-recall audit parity only. It does not close full cleanup / replacement-duration breadth, full equipment lifecycle breadth, full stack lifecycle breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
