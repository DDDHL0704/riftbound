# Stage 4D-17JV Recovery Snapshot Player Objects Payload Shape Audit

Date: 2026-05-26 14:14 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present nested `Players[*]["objects"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JU covered nested player zones object shape. Stage 4D-17JV adds the same explicit object-shape guard for recovered snapshot player objects payloads. Missing or null objects compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates present non-null nested player `objects` payload shape before objects property-name and object item/location validation.
- Present non-null recovered snapshot `Players[*]["objects"]` now fails with `snapshot for {viewPlayerId} player {snapshotPlayerId} objects payload is required` when it is not an object payload.
- Existing player, objects map, object item and object location property-name validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectsPayloadShapeDrift` mutates a recovered snapshot player payload and proves explicit diagnostics for a non-object nested objects payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `301/301`
- Adjacent recovery/opening/store-smoke filter: `882/882`
- Backend full: `6247/6247`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player objects nested payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
