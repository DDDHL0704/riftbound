# Stage 4D-05V Reveal Card Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server standby-reveal / hidden-card closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `REVEAL_CARD` after P1 reveals a face-down OGN Teemo standby card already in base.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `P4RevealCardCommandRejectsAcceptedBaseReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first valid `REVEAL_CARD` accepts, emits exactly `CARD_REVEALED`, keeps the source in base, flips the source face-up, preserves card number / power / mana cost / standby tags, leaves stack empty, and applies no damage to the opposing battlefield unit.
- Proves exact stale replay of the same `RevealCardCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate reveal event, no face-down / face-up drift, no base or opposing battlefield drift, no stack drift, no unintended damage, and no stale `REVEAL_CARD` action exposure after the source is already face-up.

## Non-Closure

This narrows standby-reveal / hidden-card replay and duplicate-reveal risk only. It does not close full standby-hide / reveal breadth, hidden-info random-zone breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
