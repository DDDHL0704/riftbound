# Stage 4D-09K Declare-Battle Stale Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 declare-battle stale prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing prompt-scoped declare-battle stale replay test in `BattlefieldContestBattleTaskGuardTests`: `DeclareBattleStalePromptReplayAfterNextSpellDuelStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing prompt-expiry path already rejected the stale `DECLARE_BATTLE` command without mutation; this slice binds both the accepted result and the rejected stale replay result to the same final BF-2 spell-duel pending queue, snapshot queue, battlefield-task metadata and prompt action contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Reused `AssertImmediateDeclareBattleNextSpellDuelPromptQueueAudit(...)` for both the accepted declare-battle result and the rejected stale prompt replay result.
- Extended the shared helper to assert the active P1 spell-duel prompt does not expose stale BF-1 battle task id, old `battle:BF-1`, old defender id or stale `DECLARE_BATTLE`.
- The audit proves the final state remains `SPELL_DUEL_OPEN` with P1 focus and active task `task:start-spell-duel:BF-2`.
- It proves the state pending-task queue and P1 snapshot queue keep deterministic `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` order for BF-2 only.
- It proves P1 snapshot `battlefieldTasks` exposes active spell-duel plus waiting battle metadata for BF-2.
- It proves P1 remains actionable on `PASS_FOCUS`, while stale `DECLARE_BATTLE` is absent.
- It proves P2 remains non-actionable and cannot pass focus or declare battle from the stale BF-1 battle prompt.

## Non-Closure

This narrows declare-battle stale prompt final prompt-queue drift risk only. It does not close full declare-battle legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
