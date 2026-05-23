# Stage 4D-08L Declare Battle Cleanup Block Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 declare-battle cleanup-block prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `ImmediateDeclareBattleDoesNotAdvanceNextContestedBattlefieldWhenCleanupBlocks` in `BattlefieldContestBattleTaskGuardTests`. It covers the transition where resolving the current BF-1 battle creates an unattached-equipment cleanup task and therefore must not advance BF-2 into an active spell-duel prompt yet.

Runtime behavior was not changed. The existing cleanup-block transition already held BF-2 behind the state-based cleanup task; this slice makes the state queue, snapshot queue and prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `AssertImmediateDeclareBattleCleanupBlockAudit(...)` in `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`.
- Strengthened `ImmediateDeclareBattleDoesNotAdvanceNextContestedBattlefieldWhenCleanupBlocks`.
- The audit proves the result stays in `STATE_BASED_CLEANUP` with active cleanup task `cleanup:unattached-equipment:BF-1:P2-BATTLEFIELD-EQUIPMENT`.
- It proves the deterministic pending-task order is cleanup first, then BF-2 `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE`.
- It proves BF-2 does not emit `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` events and no BF-2 spell-duel or battle task becomes active while cleanup is pending.
- It proves the P1 snapshot exposes the same blocking queue identity, state-based task metadata and BF-2 queued follow-up tasks.
- It proves both players receive non-actionable task-queue prompts with `WAIT` / `SURRENDER`, without `PASS_FOCUS`, stale `DECLARE_BATTLE`, raw cleanup ids, raw cleanup reasons or BF-2 spell-duel prompt leakage.

## Non-Closure

This narrows declare-battle cleanup-block prompt / queue transition drift risk only. It does not close full declare-battle legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
