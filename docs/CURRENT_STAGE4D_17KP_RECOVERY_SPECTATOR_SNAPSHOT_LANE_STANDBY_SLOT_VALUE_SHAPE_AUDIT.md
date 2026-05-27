# Stage 4D-17KP Recovery Spectator Snapshot Lane Standby Slot Value Shape Audit

Date: 2026-05-27 16:55 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for standby slot values inside `Lanes["battlefields"][]["standbySlots"][]` only. It does not change lane battlefield list values, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KO covered lane battlefield list value shape. Stage 4D-17KP adds explicit lane standby slot value-shape validation before spectator standby slot comparisons consume those values.

## Runtime Change

- `ValidateSpectatorSnapshotStandbySlotPayload` now rejects non-object standby slot entries with an explicit payload diagnostic.
- Required string values `slotId`, `battlefieldObjectId` and `state` now validate shape before comparison.
- Optional-present string values `sidePlayerId` and `controllerId` now validate shape while preserving absent/null compatibility.
- Required boolean values `visible` and `isFaceDown` now validate shape before comparison.
- Visible standby slots now validate required `objectId` shape before comparing it to the authoritative visible standby object id.
- Hidden standby slot `objectId` redaction behavior remains unchanged.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotStandbySlotValueShapeDrift` mutates a spectator replay-frame snapshot lane standby slot payload and proves explicit diagnostics for a non-object slot payload, whitespace-mutated strings, malformed optional strings, malformed booleans and malformed visible standby object ids.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `321/321`
- Adjacent recovery/opening/store-smoke filter: `902/902`
- Backend full: `6267/6267`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot lane standby slot value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
