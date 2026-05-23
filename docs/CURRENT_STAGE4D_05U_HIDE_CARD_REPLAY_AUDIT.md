# Stage 4D-05U Hide Card Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server standby-hide / hidden-card closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `HIDE_CARD` after P1 pays the standard standby cost for OGN Teemo, removes the source from hand, and places it face-down in base.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `P4HideCardCommandRejectsAcceptedReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first valid `HIDE_CARD` accepts, emits exactly `COST_PAID` and `CARD_HIDDEN`, spends P1's 1 mana, removes OGN Teemo from hand, places it face-down in base, leaves stack empty, and keeps hidden card details out of the public `CARD_HIDDEN` payload.
- Proves exact stale replay of the same `HideCardCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate mana payment, no duplicate hide event, no hand/base/source-state drift, no stack drift, and no stale `HIDE_CARD` action exposure after the source leaves hand.

## Non-Closure

This narrows standby-hide / payment / hidden-card replay and duplicate-hide risk only. It does not close full PaymentEngine breadth, full standby/reveal breadth, hidden-info random-zone breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
