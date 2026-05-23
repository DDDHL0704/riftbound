# Stage 4D-09H Cleanup Prompt Queue Helper Reconciliation Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 cleanup prompt / queue helper reconciliation slice. Project remains **NOT READY**.

## Scope

This slice reconciles a current code/doc drift in `BattleDamageAssignmentLifecycleTests`: the 08P natural control-cleanup and 09G activation cleanup audit docs both describe the shared cleanup prompt / queue helper contract, but the current test bodies did not call `AssertNaturalControlCleanupNextContestPromptQueueAudit(...)` at their final post-cleanup next-contest checkpoints.

Runtime behavior was not changed. The two cleanup paths already removed the hidden illegal standby and advanced BF-NEXT correctly; this slice binds both paths to the same final state queue, snapshot queue, battlefield-task snapshot, active prompt and hidden-info redaction helper.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertNaturalControlCleanupNextContestPromptQueueAudit(...)` to `NaturalBattlefieldControlCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask`.
- Added `AssertNaturalControlCleanupNextContestPromptQueueAudit(...)` to `NaturalBattleResponseActivationAssignmentCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask`.
- The shared helper proves the final post-cleanup state advances to `SPELL_DUEL_OPEN` with P1 focus and active task `task:start-spell-duel:BF-NEXT`.
- It proves the state pending-task queue contains only the BF-NEXT `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` tasks in deterministic order.
- It proves P1 snapshot `pendingTaskQueue` and `battlefieldTasks` mirror the BF-NEXT active spell-duel contract.
- It proves P1 is actionable on BF-NEXT `SPELL_DUEL_FOCUS` with `PASS_FOCUS` / `SURRENDER`, while stale `ASSIGN_COMBAT_DAMAGE` and `DECLARE_BATTLE` are absent.
- It proves P2 is non-actionable on the same BF-NEXT spell-duel prompt with `WAIT` / `SURRENDER`.
- It proves hidden standby ids, raw cleanup task ids and cleanup internals do not leak into active prompts or snapshot queue metadata.

## Non-Closure

This narrows cleanup-to-next-contest prompt / queue drift and code/doc alignment risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
