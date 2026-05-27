# Stage 4D-17KL Recovery Spectator Snapshot Player Object List Scalar Value Shape Audit

Date: 2026-05-27 16:16 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for present string-list scalar values inside visible player object `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KK covered spectator snapshot visible player object boolean scalar values. Stage 4D-17KL adds explicit visible-object string-list scalar value-shape validation before spectator object list comparisons consume those fields.

## Runtime Change

- `ValidateSpectatorSnapshotPlayerObjectStringListScalar` now validates string-list scalar value shape before object list comparisons consume those fields.
- Present `tags` and `untilEndOfTurnEffects` now reject malformed non-list values with explicit diagnostics.
- Present list entries now reject blank values, surrounding-whitespace values and duplicate normalized values.
- Missing/null compatibility continues to surface as the existing authoritative mismatch diagnostic.
- Existing spectator visible-object list mismatch behavior remains unchanged for valid string-list scalar values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectListScalarValueShapeDrift` mutates a spectator replay-frame snapshot visible object payload and proves explicit diagnostics for malformed `tags` list shape plus whitespace, blank and duplicate `untilEndOfTurnEffects` entries.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `317/317`
- Adjacent recovery/opening/store-smoke filter: `898/898`
- Backend full: `6263/6263`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot visible player object string-list scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
