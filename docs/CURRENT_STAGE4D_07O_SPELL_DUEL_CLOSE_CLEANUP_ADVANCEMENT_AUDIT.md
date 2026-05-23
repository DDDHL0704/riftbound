# Stage 4D-07O Spell Duel Close Cleanup Advancement Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / spell-duel close cleanup advancement audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful `PASS_FOCUS` path that closes a spell duel, runs state-based cleanup, removes a lethal participant, skips only the matching battle task and advances to the next contested battlefield.

The accepted coverage proves `FOCUS_PASSED` preserves the passing player and active focus player, `SPELL_DUEL_CLOSED` records the turn player and completed battlefield, cleanup destroys the lethal participant with source / owner / destroyer / destination / reason metadata, and the next-contest handoff emits `BATTLEFIELD_CONTESTED` before `SPELL_DUEL_STARTED` with stable battlefield, focus, caused-by and participant payloads.

No runtime behavior change was required because the existing spell-duel close, cleanup and next-contest advancement paths already emitted the authoritative audit payloads and ordering.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `ClosingSpellDuelWithCleanupRemovedParticipantSkipsOnlyMatchingBattleAndAdvancesNextTask`.
- Added shared spell-duel close cleanup assertions for close payloads, lethal cleanup removal payloads, next-contest handoff payloads and event ordering.

## Non-Closure

This narrows spell-duel close / cleanup / next-contest advancement audit parity only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
