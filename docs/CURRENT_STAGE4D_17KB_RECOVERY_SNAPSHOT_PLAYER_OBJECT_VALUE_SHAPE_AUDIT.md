# Stage 4D-17KB Recovery Snapshot Player Object Value Shape Audit

Date: 2026-05-27 15:00 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present object-shaped nested `Players[*]["objects"][objectId]` base value shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JW covered object item payload shape. Stage 4D-17KB adds explicit value-shape validation for present object-shaped recovered snapshot object payloads before object location and later object scalar consumers/comparisons consume those fields. Full visible-object scalar/list value shape remains open for later small slices.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player object base values after object payload and property-name validation.
- Present object-shaped recovered snapshot `Players[*]["objects"][objectId]` now requires a well-formed `objectId` string.
- The normalized `objectId` value must match the containing object key.
- Present object-shaped recovered snapshot `Players[*]["objects"][objectId]` now requires a boolean `isFaceDown` flag.
- Existing object payload-shape, object property-name and object location validation behavior is unchanged for valid base object values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectValueShapeDrift` mutates recovered snapshot player object payloads and proves explicit diagnostics for surrounding-whitespace object ids, invalid face-down flags, object id/key mismatch, blank required object ids and missing/null required face-down flags.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `307/307`
- Adjacent recovery/opening/store-smoke filter: `888/888`
- Backend full: `6253/6253`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object base value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including full visible-object scalar/list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
