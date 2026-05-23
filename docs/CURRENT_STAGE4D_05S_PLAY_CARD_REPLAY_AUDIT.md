# Stage 4D-05S Play Card Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server play-card / stack-entry closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `PLAY_CARD` after P1 plays Punishment, pays mana, removes the card from hand, and places the spell on the stack with P1 priority.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `CoreRuleEngineRejectsAcceptedPlayCardReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the first valid `PLAY_CARD` accepts, emits exactly `CARD_PLAYED`, `COST_PAID` and `STACK_ITEM_ADDED`, removes Punishment from hand, spends P1's 2 mana, places the source in `STACK`, opens neutral-closed priority for P1, and creates exactly one stack item targeting `P2-UNIT-001`.
- Proves exact stale replay of the same `PlayCardCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate cost payment, no duplicate stack item, no source-location drift, no priority-window drift, and no stale `PLAY_CARD` action exposure while the stack priority window is active.

## Non-Closure

This narrows play-card / payment / stack-entry replay and duplicate-cost / duplicate-stack risk only. It does not close full PaymentEngine breadth, full stack lifecycle breadth, full play-card official breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
