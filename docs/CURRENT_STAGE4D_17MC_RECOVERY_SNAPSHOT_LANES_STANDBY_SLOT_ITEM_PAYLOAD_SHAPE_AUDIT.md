# Stage 4D-17MC Recovery Snapshot Lanes Standby Slot Item Payload Shape Audit

Date: 2026-05-28 00:39 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Lanes["battlefields"][]["standbySlots"][]` item payload shape validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates each nested `standbySlots` item payload shape inside object entries of list-shaped recovered snapshot lanes `battlefields` payloads after 17MB standby slot list payload validation.

Malformed non-object `standbySlots` entries now produce explicit recovered lanes battlefield item standby slot item payload diagnostics before future standby-slot property or value consumers consume those payloads.

Compatibility retained:

- Missing/null/non-list lane lists keep existing recovered snapshot compatibility and 17LV/17LW/17LX diagnostics.
- Present non-list `standbySlots` payloads keep the 17MB list payload diagnostic.
- This slice does not validate recovered standby slot item property names or slot item values.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotLanesBattlefieldStandbySlotItemPayloadShapeDrift`
- Focused single: `1/1`
- Focused recovery: `360/360`
- Adjacent recovery/opening/store-smoke: `941/941`
- Backend full: `6306/6306`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, recovered standby slot item property/value validation, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
