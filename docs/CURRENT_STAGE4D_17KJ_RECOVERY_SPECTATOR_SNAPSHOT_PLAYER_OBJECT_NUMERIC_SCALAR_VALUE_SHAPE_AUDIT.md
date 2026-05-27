# Stage 4D-17KJ Recovery Spectator Snapshot Player Object Numeric Scalar Value Shape Audit

Date: 2026-05-27 16:01 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for present numeric scalar values inside visible player object `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KI covered spectator snapshot visible player object optional string scalar values. Stage 4D-17KJ adds explicit visible-object numeric scalar value-shape validation before spectator object scalar comparisons consume those fields. Spectator snapshot player object boolean/list scalar value-shape breadth remains open for later small slices.

## Runtime Change

- `ValidateSpectatorSnapshotPlayerObjectIntScalar` now validates numeric scalar value shape before object scalar comparisons consume those fields.
- Present `damage`, `power`, `basePower`, `effectivePower`, `untilEndOfTurnPowerModifier` and `manaCost` now reject malformed non-integer values with explicit diagnostics.
- Present `damage` and `manaCost` also reject negative values.
- Missing/null compatibility continues to surface as the existing authoritative mismatch diagnostic.
- Existing spectator visible-object scalar mismatch behavior remains unchanged for valid numeric scalar values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectNumericScalarValueShapeDrift` mutates a spectator replay-frame snapshot visible object payload and proves explicit diagnostics for negative `damage`, malformed `power`, malformed `basePower`, malformed `effectivePower`, malformed `untilEndOfTurnPowerModifier`, and negative `manaCost`.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `315/315`
- Adjacent recovery/opening/store-smoke filter: `896/896`
- Backend full: `6261/6261`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot visible player object numeric scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator object boolean/list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
