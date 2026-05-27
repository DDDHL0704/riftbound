# Stage 4D-17KC Recovery Snapshot Player Object String Scalar Value Shape Audit

Date: 2026-05-27 15:06 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present optional string scalar values inside nested `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KB covered recovered object base values. Stage 4D-17KC adds explicit optional-present string scalar value-shape validation for present object-shaped recovered snapshot object payloads before object scalar consumers/comparisons consume those fields. Numeric, boolean and list object scalar value shape remain open for later small slices.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player object optional string scalar values after object base value validation.
- Present `cardNo` values now reject malformed non-string and whitespace-only/surrounding-whitespace values.
- Present `ownerId` and `controllerId` values now reject malformed non-string and whitespace-only/surrounding-whitespace values.
- Present `attachedToObjectId` values now reject malformed non-string and surrounding-whitespace values while preserving absent, null and empty-string compatibility.
- Existing object payload-shape, object property-name, object base value and object location validation behavior is unchanged for valid optional string scalar values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectStringScalarValueShapeDrift` mutates recovered snapshot player object payloads and proves explicit diagnostics for malformed `cardNo`, surrounding-whitespace `ownerId`, whitespace-only `controllerId`, malformed `attachedToObjectId` and surrounding-whitespace `attachedToObjectId` values.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `308/308`
- Adjacent recovery/opening/store-smoke filter: `889/889`
- Backend full: `6254/6254`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object optional string scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including object numeric/boolean/list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
