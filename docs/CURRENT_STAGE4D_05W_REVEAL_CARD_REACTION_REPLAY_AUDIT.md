# Stage 4D-05W Reveal Card Reaction Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server standby-reaction / stack-entry closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `REVEAL_CARD` after P1 reveals a face-down OGN Teemo standby card as a reaction and places it on the stack.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `P4RevealCardCommandRejectsAcceptedReactionReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first valid `REVEAL_CARD` standby reaction accepts, emits exactly `CARD_REVEALED`, `CARD_PLAYED`, `COST_PAID` and `STACK_ITEM_ADDED`, removes the source from base, flips the source face-up, records the source in `STACK`, increments P1's cards-played count once, preserves priority for P1, and appends exactly one standby stack item after the pending opposing spell.
- Proves exact stale replay of the same `RevealCardCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate reveal / play / cost / stack events, no duplicate cards-played increment, no face-down / face-up drift, no base / object-location drift, no stack-item drift, no priority-window drift, and no stale `REVEAL_CARD` action exposure after the source has left base for stack.

## Non-Closure

This narrows standby-reaction / stack-entry replay and duplicate-stack risk only. It does not close full standby-hide / reveal breadth, full reaction timing breadth, full stack lifecycle breadth, hidden-info random-zone breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
