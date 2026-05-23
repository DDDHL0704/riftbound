# Stage 4D-05R Recycle Rune Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server resource-entry closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `RECYCLE_RUNE` after a player recycles a face-up owned trait rune from base into the rune deck and gains matching trait power.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `CoreRuleEngineRejectsAcceptedRecycleRuneReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first valid `RECYCLE_RUNE` accepts, emits exactly `RUNE_RECYCLED` and `POWER_GAINED`, removes the rune from base, appends it to rune deck, clears exhaustion, records `RUNE_DECK` location, and grants 1 red power.
- Proves exact stale replay of the same `RecycleRuneCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate power gain, no duplicate rune-deck append, no object-location drift, no exhausted-state drift, and no stale `RECYCLE_RUNE` source exposure after the rune leaves base.

## Non-Closure

This narrows payment/resource-entry replay and duplicate-resource risk only. It does not close full PaymentEngine breadth, full rune resource breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
