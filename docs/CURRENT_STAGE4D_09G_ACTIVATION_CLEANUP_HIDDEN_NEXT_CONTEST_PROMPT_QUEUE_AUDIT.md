# Stage 4D-09G Activation Cleanup Hidden Next-Contest Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 activation cleanup hidden next-contest prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalBattleResponseActivationAssignmentCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask` in `BattleDamageAssignmentLifecycleTests`. It covers the path where battle response opens, Shadow activation resolves before assignment damage, assignment damage changes battlefield control, the hidden illegal standby is removed by control cleanup, and BF-NEXT advances into active spell-duel focus.

Runtime behavior was not changed. The existing activation cleanup next-contest path already removed the hidden standby and advanced BF-NEXT correctly; this slice binds that path to the same state queue, snapshot queue, battlefield-task snapshot, active prompt and hidden-info redaction invariants used by the natural control-cleanup next-contest audit helper.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Reused `AssertNaturalControlCleanupNextContestPromptQueueAudit(...)` in `NaturalBattleResponseActivationAssignmentCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask`.
- The audit preserves the existing hidden cleanup evidence: `BATTLEFIELD_STANDBY_REMOVED`, hidden standby movement to graveyard, face-up cleanup, controller retention, object-location cleanup and absence of residual cleanup tasks.
- It proves the final post-cleanup state advances to `SPELL_DUEL_OPEN` with P1 focus and active task `task:start-spell-duel:BF-NEXT`.
- It proves the state pending-task queue contains only the BF-NEXT `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` tasks in deterministic order.
- It proves P1 snapshot `pendingTaskQueue` and `battlefieldTasks` mirror the BF-NEXT active spell-duel contract.
- It proves P1 is actionable on BF-NEXT `SPELL_DUEL_FOCUS` with `PASS_FOCUS` / `SURRENDER`, while stale `ASSIGN_COMBAT_DAMAGE` and `DECLARE_BATTLE` are absent.
- It proves P2 is non-actionable on the same BF-NEXT spell-duel prompt with `WAIT` / `SURRENDER`.
- It proves the hidden standby id and raw cleanup internals do not leak into active prompts or snapshot queue metadata.

## Non-Closure

This narrows activation cleanup hidden to next-contest prompt / queue drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
