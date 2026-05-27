# Stage 4D-17KF Recovery Snapshot Player Object List Scalar Value Shape Audit

Date: 2026-05-27 15:30 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present optional string-list scalar values inside nested `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KE covered recovered object optional boolean scalar values. Stage 4D-17KF adds explicit optional-present string-list scalar value-shape validation for present object-shaped recovered snapshot object payloads before object list consumers/comparisons consume those fields.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player object optional string-list scalar values after object boolean scalar value validation.
- Present `tags` and `untilEndOfTurnEffects` values now reject malformed non-list values.
- Present list entries now reject blank values, surrounding-whitespace values and duplicate normalized values.
- Missing or null list scalar values remain compatible so redacted face-down object payloads are not forced to expose visible-object metadata.
- Existing object payload-shape, object property-name, object base value, object string scalar, object numeric scalar, object boolean scalar and object location validation behavior is unchanged for valid optional list scalar values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectListScalarValueShapeDrift` mutates recovered snapshot player object payloads and proves explicit diagnostics for malformed `tags` list shape plus blank, surrounding-whitespace and duplicate normalized `untilEndOfTurnEffects` values.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `311/311`
- Adjacent recovery/opening/store-smoke filter: `892/892`
- Backend full: `6257/6257`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object optional list scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
