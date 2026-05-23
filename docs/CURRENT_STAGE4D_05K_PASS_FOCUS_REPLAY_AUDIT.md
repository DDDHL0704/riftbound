# Stage 4D-05K Pass-Focus Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server prompt closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `PASS_FOCUS` in an open spell-duel focus window.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `PassFocusRejectsAcceptedCommandReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`.
- Proves the first valid focus pass accepts and advances focus from P1 to P2.
- Proves exact replay of the same `PassFocusCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate `FOCUS_PASSED`, no focus/passed-player fork, and no stale `PASS_FOCUS` action exposed to the no-longer-focus player.

## Non-Closure

This narrows P0/P1 spell-duel prompt replay and battlefield task-queue fork risk only. It does not close full spell-duel lifecycle breadth, battle lifecycle, P0/P1, hidden-info/random-zone breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
