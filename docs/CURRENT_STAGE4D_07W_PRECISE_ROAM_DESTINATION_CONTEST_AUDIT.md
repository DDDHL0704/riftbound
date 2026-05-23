# Stage 4D-07W Precise Roam Destination Contest Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / precise ROAM destination-contest audit slice. Project remains **NOT READY**.

## Scope

This slice covers precise `MOVE_UNIT` ROAM from a mixed-case origin battlefield object id to a mixed-case destination battlefield object id that becomes contested and starts only the destination battlefield's spell-duel path.

The accepted coverage proves `UNIT_MOVED_TO_BATTLEFIELD`, `BATTLEFIELD_CONTESTED` and `SPELL_DUEL_STARTED` payload identity and ordering; preserves exact origin / destination strings; records the localized movement keyword while keeping the normalized `ROAM` optional-cost identity; keeps the moved event free of ambiguous `battlefieldObjectId`; empties and uncontests the origin battlefield; leaves the destination battlefield contested with exact participant ids; and queues only destination-scoped contest, spell-duel and battle tasks.

No runtime behavior change was required because the existing movement and battlefield-task reconciliation already preserve precise destination identity and queue only the destination contest tasks.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `PreciseRoamPreservesDestinationCasingAndQueuesOnlyDestinationContestTasks`.
- Added precise ROAM assertions for movement payload identity, destination contest payloads, spell-duel-start payloads, event ordering, origin / destination battlefield state and pending task queue shape.

## Non-Closure

This narrows precise ROAM destination-contest audit parity only. It does not close full movement legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full stack lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
