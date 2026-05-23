# Stage 4D-05M Declare-Battle Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server battle-task closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `DECLARE_BATTLE` while an active `START_BATTLE` task is open.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `ActiveStartBattleDeclareBattleRejectsAcceptedCommandReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`.
- Proves the first valid battle declaration accepts, closes battle, clears the active battle task, resolves battlefield control and moves the defender to graveyard.
- Proves exact replay of the same `DeclareBattleCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate battle declaration, battle close, control resolution or unit-destruction side effects, no battle/task-queue fork, and no stale `DECLARE_BATTLE` action exposed after task closure.

## Non-Closure

This narrows P0/P1 battle-task prompt replay and task-queue fork risk only. It does not close full battle lifecycle breadth, spell-duel lifecycle breadth, complete damage assignment, P0/P1, hidden-info/random-zone breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
