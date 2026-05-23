# Stage 4D-07S Base Move Contest Spell Duel Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / base move contest spell-duel audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful `MOVE_UNIT` path where a unit moves from base into an occupied enemy battlefield and immediately opens the battlefield-contest spell-duel task chain.

The accepted coverage proves `UNIT_MOVED_TO_BATTLEFIELD` records player, source / target object, origin, destination and battlefield metadata; `BATTLEFIELD_CONTESTED` records the focus player, caused-by player and participant metadata; and `SPELL_DUEL_STARTED` records the task id, reason, focus, caused-by and participant payloads. It also verifies event ordering and the queued `BATTLEFIELD_CONTESTED` / `START_SPELL_DUEL` / `START_BATTLE` task identities.

No runtime behavior change was required because the existing base-to-contested-battlefield move path already emits the authoritative audit payloads and task queue shape.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `BaseToBattlefieldMoveIntoOccupiedEnemyBattlefieldStartsSpellDuelAfterCleanupGate`.
- Added base-move contest assertions for movement, contested battlefield, spell-duel start, event ordering and queued task identity.

## Non-Closure

This narrows base-move to battlefield-contest spell-duel handoff audit parity only. It does not close full movement legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
