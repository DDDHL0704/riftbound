# Stage 4D-05Z Submit Deck Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server opening / deck-submission replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `SUBMIT_DECK` before a player readies. A changed decklist remains replaceable before ready, but an exact duplicate of the already submitted official decklist is now rejected without mutation.

Allowed runtime surface was limited to `MatchSession.SubmitDeckAsync` duplicate-deck handling.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Updated `src/Riftbound.Engine/MatchSession.cs` so a normalized `SUBMIT_DECK` matching the player's existing submitted official decklist rejects with `ErrorCodes.PhaseNotAllowed` and leaves state unchanged.
- Added `OfficialDecklistsEqual(...)` for value-based decklist comparison across legend, champion, main deck, rune deck and battlefields.
- Added `SubmitDeckRejectsAcceptedReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.
- Proves the first valid `SUBMIT_DECK` accepts, emits exactly one `DECK_SUBMITTED`, records the P1 decklist, keeps P1 on the `READY` prompt, and keeps P2 on the `SUBMIT_DECK` prompt.
- Proves exact stale replay of the same `SubmitDeckCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no tick drift, no duplicate deck-submitted side effect, no decklist drift, and no prompt drift.

## Non-Closure

This narrows exact duplicate deck-submission replay and duplicate `DECK_SUBMITTED` risk only. It does not close deck replacement breadth, invalid deck matrix breadth, full opening lifecycle breadth, READY / MULLIGAN breadth beyond 05X/05Y, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
