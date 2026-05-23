# Stage 4D-08R Natural Immediate Battle Idle Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 natural immediate battle-close idle prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalStartBattleOneOnOneImmediateBattleRemainsStable` in `BattleDamageAssignmentLifecycleTests`. It covers the one-on-one natural `DECLARE_BATTLE` path where battle closes immediately without opening an `ASSIGN_COMBAT_DAMAGE` window and without advancing a follow-up contested battlefield.

Runtime behavior was not changed. The existing immediate battle-close path already returned to the ordinary neutral action window; this slice makes the idle queue, snapshot queue, battlefield-task snapshot and prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertNaturalImmediateBattleClosedIdlePromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- Refactored the 08Q idle prompt / queue assertions into `AssertNaturalBattleClosedIdlePromptQueueAudit(...)` and kept `AssertNaturalNoResultPromptQueueAudit(...)` as the no-result wrapper.
- Strengthened `NaturalStartBattleOneOnOneImmediateBattleRemainsStable`.
- The audit proves the immediate one-on-one battle close does not open a battle-damage assignment prompt.
- It proves the post-close state returns to `NEUTRAL_OPEN`, with non-blocking `IDLE` pending-task queue, no active task, no pending tasks and no battlefield tasks.
- It proves P1 snapshot `pendingTaskQueue` matches the idle contract and exposes no battlefield-task snapshot entries.
- It proves P1 is actionable on `MAIN_ACTION` with `END_TURN` / `SURRENDER`, while stale `ASSIGN_COMBAT_DAMAGE`, `DECLARE_BATTLE` and `PASS_FOCUS` are absent.
- It proves P2 is non-actionable `WAIT` / `SURRENDER`.
- It proves the P1 prompt JSON does not retain the old `battle:BF-DAMAGE`, `task:start-battle:BF-DAMAGE`, stale assign-damage / declare-battle actions or the destroyed defender object id.

## Non-Closure

This narrows natural immediate battle-close idle prompt / queue drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
