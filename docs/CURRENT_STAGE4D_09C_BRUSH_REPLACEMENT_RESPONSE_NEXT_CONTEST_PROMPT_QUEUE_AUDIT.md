# Stage 4D-09C Brush Replacement Response Next-Contest Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 Brush replacement battle-response next-contest prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalBattleResponseActivationBrushReplacementAdvancesNextContestedBattlefieldTask` in `BattleDamageAssignmentLifecycleTests`. It covers the path where battle-response context preserves a Brush replacement optional cost through Shadow stack resolution, applies the replacement battlefield for held-score resolution, closes the battle, then advances BF-NEXT into active spell-duel focus.

Runtime behavior was not changed. The existing Brush replacement next-contest path already advanced BF-NEXT correctly; this slice binds that path to the same state queue, snapshot queue, battlefield-task snapshot and active prompt invariants used by the natural battle-response next-contest audit helpers.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Reused `AssertNaturalAssignDamageNextContestPromptQueueAudit(...)` in `NaturalBattleResponseActivationBrushReplacementAdvancesNextContestedBattlefieldTask`.
- The audit preserves the existing Brush replacement evidence: replacement battlefield choice, held-score trigger on the effective replacement battlefield, `COST_PAID`, score gain and event ordering before battle close.
- It proves the final post-close state advances to `SPELL_DUEL_OPEN` with P1 focus and active task `task:start-spell-duel:BF-NEXT`.
- It proves the state pending-task queue contains only the BF-NEXT `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` tasks in deterministic order.
- It proves P1 snapshot `pendingTaskQueue` and `battlefieldTasks` mirror the BF-NEXT active spell-duel contract.
- It proves P1 is actionable on BF-NEXT `SPELL_DUEL_FOCUS` with `PASS_FOCUS` / `SURRENDER`, while stale `ASSIGN_COMBAT_DAMAGE` and `DECLARE_BATTLE` are absent.
- It proves P2 is non-actionable on the same BF-NEXT spell-duel prompt with `WAIT` / `SURRENDER`.
- It proves the P1 prompt JSON does not retain the old `battle:BF-DAMAGE`, `task:start-battle:BF-DAMAGE`, stale assign-damage or declare-battle actions.

## Non-Closure

This narrows Brush replacement battle-response close to next-contest prompt / queue drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
