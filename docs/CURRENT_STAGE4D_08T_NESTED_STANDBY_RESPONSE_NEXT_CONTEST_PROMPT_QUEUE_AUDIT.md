# Stage 4D-08T Nested Standby Response Next-Contest Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 nested standby battle-response next-contest prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalBattleResponseActivationAllowsNestedStandbyReactionStackBeforeReturningToResponse` in `BattleDamageAssignmentLifecycleTests`. It covers the path where a natural battle opens a battle-response priority window, P2 activates Shadow, P1 reveals a standby reaction into the stack, the nested standby stack item resolves first, Shadow then resolves, both players pass response priority, the battle closes without opening `ASSIGN_COMBAT_DAMAGE`, and the follow-up contested battlefield advances into active spell-duel focus.

Runtime behavior was not changed. The existing nested standby response path already returned to response priority before closing the battle and advancing the next contested battlefield; this slice makes the final post-close queue, snapshot queue, battlefield-task snapshot and active prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Reused `AssertNaturalAssignDamageNextContestPromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- Strengthened `NaturalBattleResponseActivationAllowsNestedStandbyReactionStackBeforeReturningToResponse`.
- The audit proves the nested standby reaction resolves before Shadow and does not prematurely close battle response priority.
- It proves the final battle-response priority close does not open a battle-damage assignment prompt.
- It proves `BATTLE_RESPONSE_PRIORITY_CLOSED`, `BATTLE_CLOSED`, `BATTLEFIELD_CONTROL_RESOLVED`, next `BATTLEFIELD_CONTESTED` and next `SPELL_DUEL_STARTED` events keep the expected ordering.
- It proves the post-close state advances to `SPELL_DUEL_OPEN` with P1 focus and active task `task:start-spell-duel:BF-NEXT`.
- It proves the state pending-task queue contains only the BF-NEXT `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` tasks in deterministic order.
- It proves P1 snapshot `pendingTaskQueue` and `battlefieldTasks` mirror the BF-NEXT active spell-duel contract.
- It proves P1 is actionable on BF-NEXT `SPELL_DUEL_FOCUS` with `PASS_FOCUS` / `SURRENDER`, while stale `ASSIGN_COMBAT_DAMAGE` and `DECLARE_BATTLE` are absent.
- It proves P2 is non-actionable on the same BF-NEXT spell-duel prompt with `WAIT` / `SURRENDER`.
- It proves the P1 prompt JSON does not retain the old `battle:BF-DAMAGE`, `task:start-battle:BF-DAMAGE`, stale assign-damage or declare-battle actions.

## Non-Closure

This narrows nested standby battle-response close to next-contest prompt / queue drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
