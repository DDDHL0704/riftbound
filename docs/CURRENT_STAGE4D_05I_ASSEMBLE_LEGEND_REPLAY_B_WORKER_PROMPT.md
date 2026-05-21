# Stage 4D-05I B_SERVER Prompt: ASSEMBLE_EQUIPMENT / LEGEND_ACT Replay Guards

You are `B_SERVER` for one narrow Stage 4D runtime-test slice. You are not alone in the codebase: `A_MAIN` is coordinating, and `DOC_MATRIX_CURRENT` is independently dirty on matrix JSON / `PaymentEngineCoverageAuditTests.cs`. Do not revert or overwrite other agents' changes.

## Objective

Add or prove test coverage for accepted-command stale replay / no-mutation behavior on two high-risk remaining server-authoritative action windows:

- `ASSEMBLE_EQUIPMENT`
- `LEGEND_ACT`

This is P0/P1 closure narrowing only. It does not close full PaymentEngine, card matrix readiness, frontend gates, Chrome smoke, formal 18-step E2E, READY or goal completion.

## Required Evidence

For at least one existing successful `ASSEMBLE_EQUIPMENT` representative and at least one existing successful `LEGEND_ACT` representative:

1. Build or reuse a state with a valid server-authoritative prompt / candidate.
2. Submit the valid command once and assert it is accepted.
3. Capture the accepted post-state hash with `MatchStateHasher.Hash(...)`.
4. Submit the exact same command again against the accepted post-state.
5. Assert stale replay is rejected.
6. Assert replay emits no events.
7. Assert the post-replay state hash is exactly unchanged.
8. Assert no duplicate side effects:
   - no duplicate `COST_PAID`
   - no duplicate `EQUIPMENT_ATTACHED`
   - no duplicate generated mana / power resource event
   - no duplicate source exhaustion
   - no duplicated or reordered stack items
   - no reopened payment / prompt / task window
9. Assert no hidden-info leakage if the existing fixture exposes hidden / face-down / opponent-view surfaces.

## Suggested Write Scope

Allowed files:

- `tests/Riftbound.ConformanceTests/EdgeOfNightAssembleGuardTests.cs`
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`
- Optional focused helper extraction inside those same files only.
- Optional `src/Riftbound.Engine/CoreRuleEngine.cs` only if the new focused tests expose a real runtime bug.
- Optional 05I evidence docs if needed.

Locked files / scopes:

- Do not touch `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not touch `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`.
- Do not touch frontend files.
- Do not touch official catalog snapshots.
- Do not touch protocol core fields unless a real blocker is found and reported to `A_MAIN` first.
- Do not touch Chrome/browser/formal E2E scripts.
- Do not touch `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln`.

## Validation Expected From B

Run focused tests for the changed files, then an adjacent server-authority regression filter covering:

- `ASSEMBLE_EQUIPMENT`
- `LEGEND_ACT`
- `PaymentEngine`
- `ActionPrompt`
- `Prompt`
- `MatchStateHasher`

If focused or adjacent tests fail, stop and report the failure without broadening scope.

## Report Back

Report:

- changed files
- whether runtime changed
- whether protocol changed
- focused test command/result
- adjacent test command/result
- any hidden-info finding
- any blocker requiring A/user decision

Project remains **NOT READY**.
