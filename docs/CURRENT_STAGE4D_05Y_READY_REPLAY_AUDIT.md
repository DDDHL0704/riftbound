# Stage 4D-05Y Ready Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server opening / ready closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `READY` after the second official player readies, official opening is built, and the match enters mulligan timing.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialReadyAcceptsAcceptedReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.
- Proves the second valid official `READY` accepts once, emits `PLAYER_READY`, `OFFICIAL_OPENING_STARTED` and `MATCH_STARTED`, moves the match to `IN_PROGRESS` / `MULLIGAN`, keeps both players ready, creates official opening hands and decks, and exposes `MULLIGAN` only to the active opening player.
- Proves exact stale replay of the same player's `READY` after match start is an accepted no-op against the official opening post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no tick / RNG drift, no duplicate official opening / match-start effects, no hand or main-deck drift for either player, no mulligan-completion drift, no second-action-player drift, and no prompt fork.

## Non-Closure

This narrows official READY replay / duplicate-opening risk only. It does not close full opening lifecycle breadth, deck submission replay breadth, mulligan breadth beyond 05X, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
