# Stage 4D-05J Hand-Choice Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server prompt closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `CHOOSE_HAND_CARDS` using the Undercover Agent last-breath hand-choice prompt.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `UndercoverAgentHandChoiceRejectsAcceptedCommandReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/UndercoverAgentTriggerTests.cs`.
- Proves the first valid hand-choice submission accepts and closes `PendingHandChoice`.
- Proves exact replay of the same `ChooseHandCardsCommand` rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no reopened hand-choice window, no duplicate discard / draw / hand-choice-resolution side effects, and no post-closure hand-choice prompt for either player.

## Non-Closure

This narrows P0 complex prompt replay risk only. It does not close full hand-choice breadth, P0/P1, hidden-info/random-zone breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.

