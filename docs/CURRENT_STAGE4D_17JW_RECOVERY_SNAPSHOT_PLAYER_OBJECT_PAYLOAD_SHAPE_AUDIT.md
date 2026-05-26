# Stage 4D-17JW Recovery Snapshot Player Object Payload Shape Audit

Date: 2026-05-26 14:28 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for nested `Players[*]["objects"][objectId]` object item payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JV covered the player `objects` map payload shape. Stage 4D-17JW adds explicit object-shape validation for each recovered snapshot object entry in that map before object property-name and object location validation consume those entries.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates nested player object item payload shape before object property-name and object location validation.
- Non-object recovered snapshot `Players[*]["objects"][objectId]` entries now fail with `snapshot for {viewPlayerId} player {snapshotPlayerId} object {objectId} payload is required`.
- Existing player, objects map, object item and object location property-name validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectPayloadShapeDrift` mutates a recovered snapshot player `objects` map entry and proves explicit diagnostics for a non-object object item payload.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `302/302`
- Adjacent recovery/opening/store-smoke filter: `883/883`
- Backend full: `6248/6248`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object item payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
