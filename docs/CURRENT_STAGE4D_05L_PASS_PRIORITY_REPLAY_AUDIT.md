# Stage 4D-05L Pass-Priority Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server prompt closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `PASS_PRIORITY` in an open stack-priority window.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `PassPriorityRejectsAcceptedCommandReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`.
- Proves the first valid priority pass accepts, resolves the pending stack exactly once, moves the contested defender to base and returns the pending task queue to idle.
- Proves exact replay of the same `PassPriorityCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate `PRIORITY_PASSED`, `STACK_ITEM_RESOLVED` or movement side effects, no stack/task-queue fork, and no stale `PASS_PRIORITY` action exposed to the previous priority player.

## Non-Closure

This narrows P0/P1 stack-priority prompt replay and task-queue fork risk only. It does not close full priority/stack lifecycle breadth, battle lifecycle, P0/P1, hidden-info/random-zone breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
