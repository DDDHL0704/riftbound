# Stage 4D-05T Activate Ability Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server activated-ability / stack-entry closure slice. Project remains **NOT READY**.

## Scope

This slice covers the accepted-command replay surface for `ACTIVATE_ABILITY` after P1 activates Fluft Poro's Warhawk ability, exhausts the source as cost, and places the ability on the stack with P1 priority.

Allowed runtime surface was observation-only. No runtime change was required.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `FluftPoroRejectsAcceptedActivationReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/FluftPoroActivatedAbilityTests.cs`.
- Proves the first valid `ACTIVATE_ABILITY` accepts, emits exactly `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID` and `STACK_ITEM_ADDED`, exhausts Fluft Poro, keeps the source in battlefield, places a no-target ability item on the stack, opens neutral-closed priority for P1, and creates no Warhawk tokens before stack resolution.
- Proves exact stale replay of the same `ActivateAbilityCommand` by P1 rejects against the accepted post-state.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate exhaustion, no duplicate cost payment, no duplicate stack item, no stack-item drift, no priority-window drift, no pre-resolution token creation, and no stale `ACTIVATE_ABILITY` action exposure while the stack priority window is active.

## Non-Closure

This narrows activated-ability / payment / stack-entry replay and duplicate-exhaust / duplicate-stack risk only. It does not close full PaymentEngine breadth, full activated-ability official breadth, full stack lifecycle breadth, complete replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
