# Stage 4D-09I Reconnect Natural Assign-Damage Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 reconnect natural assign-damage prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `ReconnectDuringNaturalAssignCombatDamagePreservesBattleTaskMetadataAndRedaction` in `BattleDamageAssignmentLifecycleTests`. It covers reconnecting as P1 while natural combat damage assignment is open on BF-DAMAGE and an opponent hidden standby remains present elsewhere.

Runtime behavior was not changed. The existing reconnect snapshot and prompt paths already preserved the assignment battle task and hidden-info redaction; this slice binds that path to explicit pending-task queue, battlefield-task metadata, prompt action and hidden-id redaction assertions.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertNaturalAssignDamageReconnectBattleTaskMetadataAudit(...)` in `BattleDamageAssignmentLifecycleTests`.
- Strengthened `ReconnectDuringNaturalAssignCombatDamagePreservesBattleTaskMetadataAndRedaction`.
- The audit proves reconnect preserves P1 identity and reconnect-token presence without over-binding the session seat value.
- It proves P1 snapshot `pendingTaskQueue` remains blocking `BATTLE_TASKS`, active on `task:start-battle:BF-DAMAGE`.
- It proves the snapshot queue keeps deterministic BF-DAMAGE `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` task order.
- It proves P1 snapshot `battlefieldTasks` exposes completed spell-duel plus active start-battle metadata, `battle:BF-DAMAGE`, participant controllers and participant object ids.
- It proves P1 remains actionable on `ASSIGN_COMBAT_DAMAGE` / `SURRENDER` with the BF-DAMAGE battle prompt scope.
- It proves the hidden standby id does not leak through pending queue metadata, battlefield-task metadata or the active prompt.

## Non-Closure

This narrows reconnect natural assign-damage prompt / queue drift and hidden-info redaction risk only. It does not close full reconnect breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
