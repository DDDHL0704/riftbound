# Stage 4D-06A Pass Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server runtime / generic pass replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers the generic `PASS` command surface that previously fell through `CoreRuleEngine` into `PlaceholderRuleEngine`.

The accepted runtime change makes `CoreRuleEngine` handle `PassCommand` directly:

- Stack priority windows route generic `PASS` through the existing core priority-pass path.
- Spell-duel focus windows route generic `PASS` through the existing core focus-pass path.
- Ordinary `MAIN` / `NEUTRAL_OPEN` windows accept one current-player generic `PASS`, emit the compatibility `TURN_ENDED` event, keep the turn with the same active player, and record a core-side pass guard so stale replay rejects without mutation.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Updated `src/Riftbound.Engine/CoreRuleEngine.cs` so `PassCommand` no longer relies on the placeholder fallback path.
- Added `ResolvePass(...)` to validate ordinary main-window generic pass submissions, reject non-current-player / wrong-window submissions, reject repeat generic `PASS` for the same open main state, and build core prompts from the accepted post-state.
- Added `CoreRuleEngineAcceptsGenericPassInOrdinaryMainWindow` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Added `CoreRuleEngineRejectsAcceptedGenericPassReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first ordinary main `PASS` accepts once, increments tick once, emits exactly one `TURN_ENDED`, preserves active player / turn player / phase / timing, preserves hand and battlefield zones, and does not expose `PASS` in the resulting prompt.
- Proves exact stale replay of a generic `PASS` with a new client intent rejects without events, exact `MatchStateHasher.Hash(...)` preservation, no tick drift, no turn / phase / timing drift, no prompt fork, and no duplicate `TURN_ENDED`.

## Non-Closure

This narrows generic pass / placeholder fallback / accepted-command replay risk only. It does not close full turn lifecycle breadth, priority / focus lifecycle breadth beyond the existing 05K / 05L slices, full action-window determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
