# Stage 4D-17KN Recovery Spectator Snapshot Lane Battlefield Scalar Value Shape Audit

Date: 2026-05-27 16:37 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for lane battlefield scalar values only: `Lanes["battlefieldCount"]` and scalar fields inside `Lanes["battlefields"][]`. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KM covered `Lanes["battlefieldObjectIds"][]` item payload and required item string value shape. Stage 4D-17KN adds explicit lane battlefield scalar value-shape validation before spectator lane battlefield comparisons consume those values.

## Runtime Change

- `ValidateSpectatorSnapshotLanePayloads` now validates `battlefieldCount` as a required non-negative integer before comparing it to authoritative battlefield-object count.
- `ValidateSpectatorSnapshotBattlefieldScalarPayloads` now rejects non-object battlefield entries with an explicit payload diagnostic.
- Battlefield scalar fields now validate value shape before comparison: required strings (`battlefieldObjectId`, `zonePlayerId`, `status`), optional strings (`cardNo`, `controllerId`), required boolean (`contested`), and required non-negative integers (`standbySlotCount`, `faceDownStandbyCount`, `hiddenStandbyCount`).
- Existing authoritative mismatch behavior remains in place for valid scalar values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotLaneBattlefieldScalarValueShapeDrift` mutates a spectator replay-frame snapshot lane payload and proves explicit diagnostics for an invalid `battlefieldCount`, whitespace-mutated required strings, blank required strings, invalid optional string, invalid boolean, invalid integer, and negative count scalars.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `319/319`
- Adjacent recovery/opening/store-smoke filter: `900/900`
- Backend full: `6265/6265`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot lane battlefield scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator lane battlefield list and standby value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
