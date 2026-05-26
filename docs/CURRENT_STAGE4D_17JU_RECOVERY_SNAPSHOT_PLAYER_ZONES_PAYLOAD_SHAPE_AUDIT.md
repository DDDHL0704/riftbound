# Stage 4D-17JU Recovery Snapshot Player Zones Payload Shape Audit

Date: 2026-05-26 14:01 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present nested `Players[*]["zones"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JT covered nested player rune-pool object shape. Stage 4D-17JU adds the same explicit object-shape guard for recovered snapshot player zones payloads. Missing or null zones compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates present non-null nested player `zones` payload shape before zones property-name validation.
- Present non-null recovered snapshot `Players[*]["zones"]` now fails with `snapshot for {viewPlayerId} player {snapshotPlayerId} zones payload is required` when it is not an object payload.
- Existing player and zones property-name validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerZonesPayloadShapeDrift` mutates a recovered snapshot player payload and proves explicit diagnostics for a non-object nested zones payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `300/300`
- Adjacent recovery/opening/store-smoke filter: `881/881`
- Backend full: `6246/6246`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player zones nested payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
