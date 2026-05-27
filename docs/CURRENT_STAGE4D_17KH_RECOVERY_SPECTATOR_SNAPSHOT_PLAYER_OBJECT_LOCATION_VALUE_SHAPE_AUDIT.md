# Stage 4D-17KH Recovery Spectator Snapshot Player Object Location Value Shape Audit

Date: 2026-05-27 15:46 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for present player object location values inside nested `Players[*]["objects"][objectId]["location"]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KG covered spectator snapshot player object base values. Stage 4D-17KH adds explicit spectator snapshot player object location value-shape validation before spectator object-location comparisons consume those fields. Spectator snapshot player object string/numeric/boolean/list scalar value-shape breadth remains open for later small slices.

## Runtime Change

- `ValidateSpectatorSnapshotPlayerObjectLocation` now validates spectator replay-frame snapshot player object location values before object-location comparisons consume them.
- Required location `playerId` now rejects missing/null, blank, non-string and surrounding-whitespace values with explicit diagnostics.
- Required location `zone` now rejects missing/null, blank, non-string, surrounding-whitespace and unknown object-location zone values with explicit diagnostics.
- Optional-present `battlefieldObjectId` now rejects malformed non-string and surrounding-whitespace values with explicit diagnostics while absent/null/empty compatibility remains unchanged.
- Existing spectator object-location mismatch behavior remains unchanged for valid location values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectLocationValueShapeDrift` mutates a spectator replay-frame snapshot visible object location payload and proves explicit diagnostics for whitespace-mutated `playerId`, whitespace/unknown `zone`, and malformed optional-present `battlefieldObjectId`.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `313/313`
- Adjacent recovery/opening/store-smoke filter: `894/894`
- Backend full: `6259/6259`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot player object location value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator object scalar/list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
