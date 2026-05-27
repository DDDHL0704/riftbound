# Stage 4D-17MB Recovery Snapshot Lanes Standby Slot List Payload Shape Audit

Date: 2026-05-28 00:31 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Lanes["battlefields"][]["standbySlots"]` object-list payload shape validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates the nested `standbySlots` object-list payload shape inside object entries of list-shaped recovered snapshot lanes `battlefields` payloads after battlefield item list value validation.

Malformed present non-list `standbySlots` payloads now produce explicit recovered lanes battlefield item standby slot list payload diagnostics before future standby-slot item consumers consume those payloads.

Compatibility retained:

- Missing/null/non-list lane lists keep existing recovered snapshot compatibility and 17LV/17LW/17LX diagnostics.
- This slice does not validate recovered standby slot item property names or slot item values.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotLanesBattlefieldStandbySlotListPayloadShapeDrift`
- Focused single: `1/1`
- Focused recovery: `359/359`
- Adjacent recovery/opening/store-smoke: `940/940`
- Backend full: `6305/6305`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, recovered standby slot item property/value validation, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
