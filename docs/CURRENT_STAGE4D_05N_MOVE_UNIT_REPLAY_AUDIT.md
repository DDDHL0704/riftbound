# Stage 4D-05N Move-Unit Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server movement / battlefield-task closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `MOVE_UNIT` after a base unit moves into an occupied enemy battlefield and opens the battlefield task chain.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `MoveUnitIntoOccupiedEnemyBattlefieldRejectsAcceptedCommandReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`.
- Proves the first valid movement accepts, moves P2's base unit into `BATTLEFIELD:BF-CONTEST`, opens spell-duel focus for P2 and creates the contest / spell-duel / start-battle task chain.
- Proves exact replay of the same `MoveUnitCommand` by P2 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate battlefield-contested, spell-duel or start-battle side effects, no movement/task-queue fork, and no stale `MOVE_UNIT` action exposed while the spell-duel task is active.

## Non-Closure

This narrows P0/P1 movement replay and battlefield task-queue fork risk only. It does not close full movement breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, complete damage assignment, P0/P1, hidden-info/random-zone breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
