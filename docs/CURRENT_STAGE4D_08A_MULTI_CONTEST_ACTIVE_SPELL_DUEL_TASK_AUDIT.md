# Stage 4D-08A Multi Contest Active Spell Duel Task Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / multi-contest active spell-duel task ordering audit slice. Project remains **NOT READY**.

## Scope

This slice covers the deterministic active spell-duel task surface when two battlefields are contested at the same time and the first battlefield has the active spell-duel focus.

The accepted coverage proves the authoritative pending task queue keeps the full cleanup / battlefield-task order: `BATTLEFIELD_CONTESTED` markers for `BF-A` and `BF-B`, then `START_SPELL_DUEL` tasks for `BF-A` and `BF-B`, then `START_BATTLE` tasks for `BF-A` and `BF-B`. It also proves the active task remains `task:start-spell-duel:BF-A`, the snapshot pending-task queue exposes the same deterministic task ids and battlefield ids, the battlefield-task view exposes only the four spell-duel / battle tasks with `BF-A` active and `BF-B` pending, and the active P1 prompt is the exact spell-duel focus prompt for `BF-A` / `spell-duel:BF-A`.

No runtime behavior change was required because the existing pending-task and snapshot builders already preserve the authoritative ordering and active-task focus.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `MultipleContestedBattlefieldsExposeOneActiveSpellDuelTaskInDeterministicOrder`.
- Added assertions for authoritative pending queue kind / battlefield / task-id order.
- Added assertions for snapshot pending queue order and battlefield-task view order.
- Added assertions for active spell-duel focus prompt identity and available actions.

## Non-Closure

This narrows multi-contest active spell-duel task ordering audit parity only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial` or READY.
