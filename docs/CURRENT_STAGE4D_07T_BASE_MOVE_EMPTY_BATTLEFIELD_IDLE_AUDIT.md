# Stage 4D-07T Base Move Empty Battlefield Idle Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / base move empty-battlefield idle audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful `MOVE_UNIT` path where a unit moves from base into an empty friendly battlefield and must leave the battlefield task queue idle.

The accepted coverage proves `UNIT_MOVED_TO_BATTLEFIELD` records player, source / target object, origin, destination, battlefield and optional-cost metadata; the destination battlefield remains controlled and uncontested with only the moved unit as occupant; and the pending task queue has no tasks, no active task and no blocking state. It also verifies no `BATTLEFIELD_CONTESTED`, `SPELL_DUEL_STARTED` or `UNIT_DESTROYED` events are emitted.

No runtime behavior change was required because the existing base-to-empty-battlefield move path already emits the authoritative movement payload and preserves the idle queue shape.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `BaseToBattlefieldMoveIntoEmptyBattlefieldKeepsTaskQueueIdle`.
- Added base-move empty-battlefield assertions for movement payload, battlefield occupant/control state, idle pending-task queue shape and absence of contest / spell-duel / destroy side effects.

## Non-Closure

This narrows base-move empty-battlefield idle audit parity only. It does not close full movement legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
