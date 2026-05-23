# Stage 4D-05X Mulligan Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server opening / mulligan closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `MULLIGAN` after the active opening player completes their official mulligan and the second-action player becomes the only authoritative mulligan actor.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialMulliganRejectsAcceptedReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.
- Proves the first valid official `MULLIGAN` by the active opening player accepts, emits exactly one `MULLIGAN_COMPLETED`, keeps the match in `MULLIGAN` timing, records the active player as completed, removes the selected hand objects from hand, returns them to main deck with `MAIN_DECK` locations, and moves the actionable `MULLIGAN` prompt to the second-action player only.
- Proves exact stale replay of the same `MulliganCommand` by the completed active player rejects against the accepted post-state with `ErrorCodes.PhaseNotAllowed`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no tick / RNG drift, no duplicate draw / return / completed-player effects, no active-player hand or main-deck drift, no second-player hand drift, no prompt fork, and no stale `MULLIGAN` action exposure to the completed active player.

## Non-Closure

This narrows official opening mulligan replay and prompt-fork risk only. It does not close full opening lifecycle breadth, deck submission / READY replay breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
