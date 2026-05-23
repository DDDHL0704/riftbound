# Stage 4D-08P Natural Control Cleanup Next Contest Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 battlefield-control cleanup to next-contest prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalBattlefieldControlCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask` in `BattleDamageAssignmentLifecycleTests`. It covers the transition where accepted natural combat damage changes battlefield control, removes an illegal hidden standby during control cleanup, then advances the next contested battlefield into spell-duel focus.

Runtime behavior was not changed. The existing transition already removed the illegal standby before BF-NEXT advancement; this slice makes the cleanup residue, state queue, snapshot queue, battlefield-task snapshot and prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertNaturalControlCleanupNextContestPromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- Strengthened `NaturalBattlefieldControlCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask`.
- The audit proves BF-DAMAGE control cleanup removes the hidden illegal standby before BF-NEXT contest and spell-duel start events.
- It proves state and P1 snapshot queue contain only BF-NEXT contest / spell-duel / battle follow-up tasks, with no BF-DAMAGE cleanup task or hidden standby object residue.
- It proves BF-NEXT advances to active spell-duel focus with `task:start-spell-duel:BF-NEXT`, deterministic task order and matching battlefield-task snapshot metadata.
- It proves P1 is actionable with `PASS_FOCUS`, P2 is non-actionable with `WAIT`, both prompts are scoped to `BF-NEXT` / `spell-duel:BF-NEXT`, and stale assign-damage / battle-declaration actions are removed.
- It proves hidden standby id, `REMOVE_ILLEGAL_STANDBY`, `BATTLEFIELD_CONTROL_CLEANUP` and raw `cleanup:` internals do not remain in either active prompt.

## Non-Closure

This narrows battlefield-control cleanup-to-next-contest prompt / queue transition drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
