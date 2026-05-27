# Stage 4D-17LZ Recovery Snapshot Lanes Battlefield Scalar Value Audit

Date: 2026-05-28 00:14 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Lanes["battlefields"][]` item scalar value validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates scalar values inside object entries of list-shaped recovered snapshot lanes `battlefields` payloads after list/item/property-name validation.

Covered scalar fields:

- Required strings: `battlefieldObjectId`, `zonePlayerId`, `status`
- Optional strings: `cardNo`, `controllerId`
- Required booleans: `contested`, `scoredThisTurn`
- Required non-negative counts: `standbySlotCount`, `faceDownStandbyCount`, `hiddenStandbyCount`
- Known status values: `CONTROLLED`, `UNCONTROLLED`, `CONTESTED`

Missing/null/non-list lane lists keep existing compatibility and earlier shape/property-name diagnostics.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotLanesBattlefieldItemScalarValueShapeDrift`
- Focused single: `1/1`
- Focused recovery: `357/357`
- Adjacent recovery/opening/store-smoke: `938/938`
- Backend full: `6303/6303`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
