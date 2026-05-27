# Stage 4D-17KI Recovery Spectator Snapshot Player Object String Scalar Value Shape Audit

Date: 2026-05-27 15:52 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for present optional string scalar values inside visible player object `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KH covered spectator snapshot player object location values. Stage 4D-17KI adds explicit visible-object string scalar value-shape validation before spectator object scalar comparisons consume those fields. Spectator snapshot player object numeric/boolean/list scalar value-shape breadth remains open for later small slices.

## Runtime Change

- `ValidateSpectatorSnapshotPlayerObjectOptionalStringScalar` now validates optional string scalar value shape before object scalar comparisons consume those fields.
- Present `cardNo`, `ownerId`, `controllerId` and `attachedToObjectId` now reject malformed non-string values with explicit diagnostics.
- Present string scalars now reject blank-whitespace and surrounding-whitespace values with explicit diagnostics.
- Absent/null optional compatibility and empty optional attachment id compatibility remain unchanged.
- Existing spectator visible-object scalar mismatch behavior remains unchanged for valid string scalar values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectStringScalarValueShapeDrift` mutates a spectator replay-frame snapshot visible object payload and proves explicit diagnostics for malformed `cardNo`, whitespace-mutated `ownerId`, blank-whitespace `controllerId`, and whitespace-mutated `attachedToObjectId`.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `314/314`
- Adjacent recovery/opening/store-smoke filter: `895/895`
- Backend full: `6260/6260`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot visible player object optional string scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator object numeric/boolean/list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
