# Stage 4D-17JX Recovery Snapshot Player Object Location Payload Shape Audit

Date: 2026-05-26 14:41 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present nested `Players[*]["objects"][objectId]["location"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JW covered object item payload shape. Stage 4D-17JX adds explicit object-shape validation for present non-null recovered snapshot object `location` payloads before object location property-name validation consumes those payloads. Missing or null location compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates present non-null nested player object `location` payload shape before object location property-name validation.
- Present non-null recovered snapshot `Players[*]["objects"][objectId]["location"]` now fails with `snapshot for {viewPlayerId} player {snapshotPlayerId} object {objectId} location payload is required` when it is not an object payload.
- Existing player, objects map, object item and object location property-name validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectLocationPayloadShapeDrift` mutates a recovered snapshot player object `location` payload and proves explicit diagnostics for a non-object location payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `303/303`
- Adjacent recovery/opening/store-smoke filter: `884/884`
- Backend full: `6249/6249`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object location payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
