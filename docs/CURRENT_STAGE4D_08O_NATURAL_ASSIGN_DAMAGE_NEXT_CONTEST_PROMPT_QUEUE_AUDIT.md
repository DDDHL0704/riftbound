# Stage 4D-08O Natural Assign Damage Next Contest Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 natural combat-damage to next-contest prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalAssignCombatDamageAdvancesNextContestedBattlefieldTask` in `BattleDamageAssignmentLifecycleTests`. It covers the transition where accepted natural combat damage closes the current battle and advances the next contested battlefield into spell-duel focus.

Runtime behavior was not changed. The existing transition already advanced BF-NEXT; this slice makes the state queue, snapshot queue, battlefield-task snapshot and prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertNaturalAssignDamageNextContestPromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- Strengthened `NaturalAssignCombatDamageAdvancesNextContestedBattlefieldTask`.
- The audit proves accepted natural combat damage closes BF-DAMAGE and removes BF-DAMAGE pending battle-task residue.
- It proves BF-NEXT advances to active spell-duel focus with state queue phase `SPELL_DUEL_TASKS`, active task `task:start-spell-duel:BF-NEXT` and deterministic `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL`, `START_BATTLE` order.
- It proves the P1 snapshot queue exposes the same BF-NEXT-only task order and queue metadata.
- It proves snapshot battlefield tasks expose active `START_SPELL_DUEL` and waiting `START_BATTLE` metadata for BF-NEXT.
- It proves P1 is actionable with `PASS_FOCUS`, P2 is non-actionable with `WAIT`, both prompts are scoped to `BF-NEXT` / `spell-duel:BF-NEXT`, and stale assign-damage / battle-declaration actions are removed.
- It proves P1 prompt JSON does not retain the old BF-DAMAGE battle id, old BF-DAMAGE battle task id, `ASSIGN_COMBAT_DAMAGE` or `DECLARE_BATTLE`.

## Non-Closure

This narrows natural combat-damage-to-next-contest prompt / queue transition drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
