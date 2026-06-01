# Stage 4D-17SJ Recovery Timing Battle Damage Assignment Identity Consistency Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battle damage-assignment identity consistency:

- Recovered player-view snapshot `Timing["battle"]["damageAssignment"]["battleId"]` now must match the enclosing `Timing["battle"]["battleId"]` when damage-assignment pending fields are present.
- Recovered player-view snapshot `Timing["battle"]["damageAssignment"]["battlefieldId"]` now must match the enclosing `Timing["battle"]["battlefieldObjectId"]` when damage-assignment pending fields are present.
- Spectator replay-frame timing battle damage-assignment payloads now run the same same-payload identity checks before authoritative battle parity comparison.

This follows Stage 4D-17SG, 17SH and 17SI by covering the remaining near-field consistency seam in the same `battle` / `damageAssignment` payload pair. Forged nested damage-assignment identity values now emit explicit consistency diagnostics instead of relying only on the later spectator `battle` parity mismatch.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Added battle damage-assignment identity consistency validation for pending damage-assignment payloads.
- Wired the check into recovered snapshot and spectator replay-frame battle damage-assignment validation.
- Kept existing required string, object-reference registry, player-reference membership and spectator authoritative parity checks intact.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentIdentityInconsistentWithBattle`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentIdentityInconsistentWithBattle`

These cover recovered snapshot and spectator replay-frame nested `battleId` / `battlefieldId` drift against the enclosing battle payload.

## Validation

Passed:

- Focused identity-consistency tests: `2/2`
- `BattleDamageAssignment` filter: `66/66`
- `Battle` filter: `679/679`
- `MatchRecoveryTests` filter: `594/594`
- Adjacent recovery/opening/store-smoke filter: `1194/1194`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6540/6540`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battle damage-assignment identity consistency only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
