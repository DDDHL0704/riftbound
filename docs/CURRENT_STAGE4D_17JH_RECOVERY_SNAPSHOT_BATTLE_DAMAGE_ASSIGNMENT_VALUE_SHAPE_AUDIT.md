# Stage 4D-17JH Recovery Snapshot Battle Damage-Assignment Value Shape Audit

Date: 2026-05-26 11:13 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present nested `Timing["battle"]["damageAssignment"]` values only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Missing or null recovered snapshot damage-assignment payloads remain compatible. Stage 4D-17JH adds value-shape validation only when the payload is present, before battle field reads and parity comparison consume those nested values.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingBattleDamageAssignmentPayloadValues` after the existing recovered snapshot damage-assignment property-name validation.
- Present non-object damage-assignment payloads now emit an explicit recovered snapshot diagnostic.
- The recovered snapshot wrapper validates the required `isPending` flag, then validates pending-window fields when `isPending` is true or any pending-window field is present.
- Spectator and recovered snapshot damage-assignment pending-field validation now share helper logic for scalar, map, list-map and required-assignment item values while preserving existing spectator labels.

## Validated Fields

- Required boolean `isPending` rejects missing, null and non-boolean values.
- Pending-window strings `phase`, `battleId`, `battlefieldId` and `assigningPlayerId` reject malformed required values when pending fields are active.
- `damagePool`, `existingDamage` and `lethalDamageThreshold` reject missing, null, non-map, non-integer and negative values when pending fields are active.
- `legalTargets` rejects missing, null and non-map payloads, plus malformed legal-target lists.
- `requiredAssignments` rejects missing, null and non-list payloads.
- Required-assignment item payloads reject non-object items and malformed `sourceObjectId`, `damage` and `legalTargetObjectIds` values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentValueDrift` mutates recovered snapshot `Timing["battle"]["damageAssignment"]` through `RawJson` and proves explicit diagnostics for:

- invalid `isPending`
- whitespace-mutated `phase` and `assigningPlayerId`
- missing required pending-window scalar values
- invalid and negative non-negative integer map values
- invalid legal-target map/list values, blank entries, whitespace entries and duplicate normalized entries
- invalid required-assignment item damage
- invalid required-assignment item payloads
- blank, whitespace and duplicate required-assignment legal target ids

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `287/287`
- Adjacent recovery/opening/store-smoke filter: `868/868`
- Backend full: `6233/6233`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot battle damage-assignment value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
