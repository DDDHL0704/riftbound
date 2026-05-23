# Stage 4D-08Q Natural No Result Idle Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 natural no-result idle prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalAssignCombatDamageEmitsNoResultWhenAllParticipantsDestroyed` in `BattleDamageAssignmentLifecycleTests`. It covers the transition where accepted natural combat damage destroys all battle participants, emits `BATTLE_NO_RESULT`, closes the battle and returns to the ordinary neutral action window.

Runtime behavior was not changed. The existing no-result path already closed the battle and cleared pending battlefield tasks; this slice makes the idle queue, snapshot queue, battlefield-task snapshot and active prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertNaturalNoResultPromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- Strengthened `NaturalAssignCombatDamageEmitsNoResultWhenAllParticipantsDestroyed`.
- The audit proves the all-participants-destroyed no-result branch returns to `NEUTRAL_OPEN`.
- It proves state pending-task queue is non-blocking `IDLE`, has no active task and no pending tasks.
- It proves P1 snapshot `pendingTaskQueue` matches the idle contract, has no state-based cleanup task kinds and exposes no battlefield tasks.
- It proves P1 is back on actionable `MAIN_ACTION` with `END_TURN` / `SURRENDER`, while `ASSIGN_COMBAT_DAMAGE`, `DECLARE_BATTLE` and `PASS_FOCUS` are absent.
- It proves P2 is non-actionable `WAIT` / `SURRENDER`.
- It proves the P1 prompt JSON does not retain the old `battle:BF-DAMAGE`, `task:start-battle:BF-DAMAGE`, stale assign-damage / declare-battle actions or destroyed participant object ids.

## Non-Closure

This narrows natural no-result battle-close idle prompt / queue drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
