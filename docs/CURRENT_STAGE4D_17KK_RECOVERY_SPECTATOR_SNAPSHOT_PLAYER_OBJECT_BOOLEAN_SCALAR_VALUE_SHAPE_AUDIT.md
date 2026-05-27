# Stage 4D-17KK Recovery Spectator Snapshot Player Object Boolean Scalar Value Shape Audit

Date: 2026-05-27 16:09 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for present boolean scalar values inside visible player object `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KJ covered spectator snapshot visible player object numeric scalar values. Stage 4D-17KK adds explicit visible-object boolean scalar value-shape validation before spectator object scalar comparisons consume those fields. Spectator snapshot player object list scalar value-shape breadth remains open for a later small slice.

## Runtime Change

- `ValidateSpectatorSnapshotPlayerObjectBoolScalar` now validates boolean scalar value shape before object scalar comparisons consume those fields.
- Present `isExhausted`, `isAttacking` and `isDefending` now reject malformed non-boolean values with explicit diagnostics.
- Missing/null compatibility continues to surface as the existing authoritative mismatch diagnostic.
- Existing spectator visible-object scalar mismatch behavior remains unchanged for valid boolean scalar values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectBooleanScalarValueShapeDrift` mutates a spectator replay-frame snapshot visible object payload and proves explicit diagnostics for malformed `isExhausted`, `isAttacking` and `isDefending` values.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `316/316`
- Adjacent recovery/opening/store-smoke filter: `897/897`
- Backend full: `6262/6262`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot visible player object boolean scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator object list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
