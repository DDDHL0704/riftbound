# Stage 4D-09M Assign-Damage Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 assign-combat-damage replay / prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing natural assign-combat-damage accepted replay and stale prompt replay tests in `BattleDamageAssignmentLifecycleTests`: `NaturalAssignCombatDamageRejectsAcceptedCommandReplayWithoutMutation` and `NaturalAssignCombatDamageStalePromptReplayAfterNextContestStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing replay and prompt-expiry paths already rejected stale `ASSIGN_COMBAT_DAMAGE` commands without mutation; this slice binds both accepted results and both rejected replay results to the shared BF-NEXT spell-duel pending queue, snapshot queue, battlefield-task metadata and prompt action contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Reused `AssertNaturalAssignDamageNextContestPromptQueueAudit(...)` for the accepted assign-damage result and accepted-command replay rejection result.
- Reused the same helper for the accepted prompt-scoped assign-damage result and rejected stale prompt replay result.
- The audit proves the final state remains `SPELL_DUEL_OPEN` with P1 focus and active task `task:start-spell-duel:BF-NEXT`.
- It proves deterministic BF-NEXT state and snapshot pending-task queues, active spell-duel plus waiting battle battlefield-task metadata, P1 `PASS_FOCUS` actionability, and P2 non-actionability.
- It proves stale `ASSIGN_COMBAT_DAMAGE` / `DECLARE_BATTLE`, old BF-DAMAGE battle/task ids and hidden standby ids stay out of the active prompt contract.

## Non-Closure

This narrows natural assign-combat-damage replay / stale prompt final prompt-queue drift risk only. It does not close full assign-damage legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
