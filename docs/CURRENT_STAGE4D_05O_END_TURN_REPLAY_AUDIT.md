# Stage 4D-05O End-Turn Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server turn/window closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `END_TURN` after an active player ends their main phase, turn-end cleanup runs, the next player's turn start resolves, and the game returns to an open main phase for the next player.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `CoreRuleEngineRejectsAcceptedEndTurnReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Uses the existing `p2-preflight-end-turn-advances-to-next-start.fixture.json` fixture to prove the first legal `END_TURN` accepts, advances the active / turn player from P1 to P2, resolves P2 turn start, and leaves P2 in `MAIN` / `NEUTRAL_OPEN` with the authoritative `END_TURN` prompt.
- Proves exact stale replay of `EndTurnCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no turn advancement / turn-start duplication, no prompt fork, no stale `END_TURN` action for P1, and P2's current `END_TURN` prompt remains authoritative.

## Non-Closure

This narrows P0/P1 turn-window replay and turn-start duplication risk only. It does not close full turn lifecycle breadth, full turn-end cleanup breadth, battle / spell-duel lifecycle breadth, P0/P1, hidden-info/random-zone breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
