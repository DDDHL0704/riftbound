# Stage 4D-05P Surrender Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server terminal-state closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `SURRENDER` after a player concedes, the match enters `FINISHED`, and the opponent is recorded as winner.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `CoreRuleEngineRejectsAcceptedSurrenderReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first valid `SURRENDER` accepts, sets `MatchStatuses.Finished`, records P2 as winner, emits exactly one `MATCH_WON`, and reduces both prompts to `WAIT`.
- Proves exact stale replay of `SurrenderCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate `MATCH_WON`, no winner/status drift, no terminal prompt fork, and no stale `SURRENDER` action exposure after match finish.

## Non-Closure

This narrows P0/P1 terminal-state replay and duplicate-win-event risk only. It does not close full win/loss matrix breadth, full replay/recovery determinism breadth, P0/P1, hidden-info/random-zone breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
