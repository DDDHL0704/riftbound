# Stage 4D-17KM Recovery Spectator Snapshot Lane Battlefield Object Id Item Value Shape Audit

Date: 2026-05-27 16:25 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for `Lanes["battlefieldObjectIds"][]` item payload and required string values only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KL covered spectator snapshot visible player object string-list scalar values. Stage 4D-17KM adds explicit lane battlefield-object-id item value-shape validation before spectator lane battlefield-object-id comparisons consume those entries.

## Runtime Change

- `ValidateSpectatorSnapshotLanePayloads` now rejects non-object `battlefieldObjectIds[]` entries with an explicit item payload diagnostic.
- Each item now validates required `playerId` and `objectId` string values before adding the pair to lane comparison input.
- Required item strings now reject missing, blank and surrounding-whitespace values with explicit diagnostics.
- Existing lane battlefield-object-id authoritative mismatch behavior remains unchanged for valid item values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotLaneBattlefieldObjectIdItemValueShapeDrift` mutates a spectator replay-frame snapshot lane payload and proves explicit diagnostics for a non-object `battlefieldObjectIds[]` item, a whitespace-mutated item `playerId`, and a blank item `objectId`.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `318/318`
- Adjacent recovery/opening/store-smoke filter: `899/899`
- Backend full: `6264/6264`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot lane battlefield-object-id item value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator lane battlefield scalar/list/standby value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
