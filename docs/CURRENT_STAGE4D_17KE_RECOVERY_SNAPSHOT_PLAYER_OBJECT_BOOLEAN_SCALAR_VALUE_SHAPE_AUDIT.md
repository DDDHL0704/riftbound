# Stage 4D-17KE Recovery Snapshot Player Object Boolean Scalar Value Shape Audit

Date: 2026-05-27 15:22 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present optional boolean scalar values inside nested `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KD covered recovered object optional numeric scalar values. Stage 4D-17KE adds explicit optional-present boolean scalar value-shape validation for present object-shaped recovered snapshot object payloads before object scalar consumers/comparisons consume those fields. Object list scalar value shape remains open for a later small slice.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player object optional boolean scalar values after object numeric scalar value validation.
- Present `isExhausted`, `isAttacking` and `isDefending` values now reject malformed non-boolean values.
- Missing or null boolean scalar values remain compatible so redacted face-down object payloads are not forced to expose visible-object state.
- Existing object payload-shape, object property-name, object base value, object string scalar, object numeric scalar and object location validation behavior is unchanged for valid optional boolean scalar values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectBooleanScalarValueShapeDrift` mutates recovered snapshot player object payloads and proves explicit diagnostics for malformed exhausted, attacking and defending boolean scalar values.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `310/310`
- Adjacent recovery/opening/store-smoke filter: `891/891`
- Backend full: `6256/6256`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object optional boolean scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including object list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
