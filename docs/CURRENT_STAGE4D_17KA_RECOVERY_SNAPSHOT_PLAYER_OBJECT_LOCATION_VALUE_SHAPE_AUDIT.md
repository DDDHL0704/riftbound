# Stage 4D-17KA Recovery Snapshot Player Object Location Value Shape Audit

Date: 2026-05-27 14:53 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present object-shaped nested `Players[*]["objects"][objectId]["location"]` value shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JX covered object location payload shape. Stage 4D-17KA adds explicit value-shape validation for present object-shaped recovered snapshot object location payloads before object location consumers/comparisons consume those fields. Missing or null location compatibility for recovered snapshots is unchanged because value validation only runs after the location payload is present and object-shaped.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player object location values after location payload and property-name validation.
- Present object-shaped recovered snapshot `Players[*]["objects"][objectId]["location"]` now requires a well-formed `playerId` string.
- Present object-shaped recovered snapshot `Players[*]["objects"][objectId]["location"]` now requires a well-formed `zone` string from the shared object-location zone whitelist.
- Present optional `battlefieldObjectId` now rejects malformed non-string and surrounding-whitespace values while preserving absent, null and empty-string compatibility.
- Existing player object payload, object location payload-shape and object location property-name validation behavior is unchanged for valid value payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectLocationValueShapeDrift` mutates recovered snapshot player object location payloads and proves explicit diagnostics for surrounding-whitespace player ids, invalid zones, non-string optional battlefield object ids, missing/blank required player ids, surrounding-whitespace zones and surrounding-whitespace optional battlefield object ids.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `306/306`
- Adjacent recovery/opening/store-smoke filter: `887/887`
- Backend full: `6252/6252`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object location value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
