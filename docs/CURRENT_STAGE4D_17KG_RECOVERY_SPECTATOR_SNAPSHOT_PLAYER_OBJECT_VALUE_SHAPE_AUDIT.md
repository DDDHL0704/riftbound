# Stage 4D-17KG Recovery Spectator Snapshot Player Object Value Shape Audit

Date: 2026-05-27 15:38 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for present player object base values inside nested `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KF covered recovered player-view snapshot player object list scalar values. Stage 4D-17KG adds explicit spectator snapshot player object base value-shape validation before spectator object identity/redaction comparisons consume those fields. Spectator snapshot player object string/numeric/boolean/list scalar value-shape breadth remains open for later small slices.

## Runtime Change

- `ValidateSpectatorSnapshotPlayerObjectPayload` now validates spectator replay-frame snapshot player object base values before object identity/redaction comparisons consume them.
- Required `objectId` now rejects missing/null, blank, non-string and surrounding-whitespace values with explicit diagnostics.
- Required `isFaceDown` now rejects missing/null and non-boolean values with explicit diagnostics.
- Existing spectator visible-object coverage, object id mismatch, face-down redaction mismatch, location and visible scalar/list comparison behavior remains unchanged for valid object base values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectValueShapeDrift` mutates a spectator replay-frame snapshot visible object payload and proves explicit diagnostics for whitespace-mutated `objectId` and malformed `isFaceDown`.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `312/312`
- Adjacent recovery/opening/store-smoke filter: `893/893`
- Backend full: `6258/6258`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot player object base value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator object scalar/list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
