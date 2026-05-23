# Stage 4D-05Q Tap Rune Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server resource-entry closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `TAP_RUNE` after a player taps a face-up owned rune in base, gains 1 mana, and the rune becomes exhausted.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `CoreRuleEngineRejectsAcceptedTapRuneReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first valid `TAP_RUNE` accepts, emits exactly `RUNE_TAPPED` and `MANA_GAINED`, increments P1 mana to 1, keeps the rune in base, and marks the rune exhausted.
- Proves exact stale replay of the same `TapRuneCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate mana gain, no object-location drift, no exhausted-state drift, and no stale `TAP_RUNE` source exposure after the rune is exhausted.

## Non-Closure

This narrows payment/resource-entry replay and duplicate-resource risk only. It does not close full PaymentEngine breadth, full rune resource breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
