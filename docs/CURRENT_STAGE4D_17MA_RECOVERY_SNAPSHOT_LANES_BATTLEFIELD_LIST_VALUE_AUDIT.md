# Stage 4D-17MA Recovery Snapshot Lanes Battlefield List Value Audit

Date: 2026-05-28 00:21 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Lanes["battlefields"][]` item list and list-map value validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates list values inside object entries of list-shaped recovered snapshot lanes `battlefields` payloads after scalar validation.

Covered list fields:

- `occupantObjectIds`
- `occupantControllerIds`
- `standbyObjectIds`
- `scoredThisTurnPlayerIds`
- `pendingTaskKinds`
- `unitsBySide` string-list map

Malformed non-list payloads, malformed list-map values, blank entries, surrounding-whitespace entries and duplicate normalized entries now produce explicit recovered lanes battlefield item diagnostics.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotLanesBattlefieldItemListValueShapeDrift`
- Focused single: `1/1`
- Focused recovery: `358/358`
- Adjacent recovery/opening/store-smoke: `939/939`
- Backend full: `6304/6304`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
