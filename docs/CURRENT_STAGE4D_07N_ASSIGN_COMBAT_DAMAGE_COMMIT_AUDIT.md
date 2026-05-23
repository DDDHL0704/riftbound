# Stage 4D-07N Assign Combat Damage Commit Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / assign-combat-damage commit audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful natural `ASSIGN_COMBAT_DAMAGE` commit path after an assignment-ordering battle prompt is opened.

The accepted coverage proves the battle-damage step and assignment payloads preserve battle id, battlefield id, assigning player, attacker / defender ids, damage pool, lethal thresholds and submitted assignment DTOs. It also verifies each `DAMAGE_APPLIED` event carries source, target, damage, battle id, battlefield id, assignment reason, source damage pool and target lethal threshold.

The same audit proves lethal cleanup and battle closure occur after damage is applied: both defenders are destroyed, `BATTLE_CLOSED` records all participants, cleared attackers and removed defenders, then `BATTLEFIELD_CONTROL_RESOLVED` confirms P1 control with the expected previous / next controller, winner, resolution and occupant controller ids.

No runtime behavior change was required because the existing assign-combat-damage commit path already emitted the authoritative audit payloads and ordering.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `NaturalAssignCombatDamageCommitsSimultaneousDamageAndClosesBattle`.
- Added shared natural assignment commit assertions for battle-damage step metadata, submitted assignment metadata, per-target damage payloads, lethal cleanup, battle close and battlefield-control resolution ordering.

## Non-Closure

This narrows successful assign-combat-damage commit audit parity only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
