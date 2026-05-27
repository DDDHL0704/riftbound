# Stage 4D-17MD Recovery Snapshot Lanes Standby Slot Item Property Name Audit

Date: 2026-05-28 00:48 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Lanes["battlefields"][]["standbySlots"][]` item property-name validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates each nested `standbySlots` item payload object's property names inside object entries of list-shaped recovered snapshot lanes `battlefields` payloads after 17MC standby slot item payload shape validation.

Malformed standby slot item keys now produce explicit recovered lanes battlefield item standby slot item property diagnostics before future standby-slot value consumers consume those payloads:

- Blank property names are rejected.
- Surrounding-whitespace property names are rejected.
- Duplicate normalized property names are rejected.

Compatibility retained:

- Missing/null/non-list lane lists keep existing recovered snapshot compatibility and 17LV/17LW/17LX diagnostics.
- Present non-list `standbySlots` payloads keep the 17MB list payload diagnostic.
- Non-object `standbySlots` entries keep the 17MC item payload diagnostic.
- This slice does not validate recovered standby slot item values.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotLanesBattlefieldStandbySlotItemPropertyNameDrift`
- Focused single: `1/1`
- Focused recovery: `361/361`
- Adjacent recovery/opening/store-smoke: `942/942`
- Backend full: `6307/6307`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, recovered standby slot item value validation, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
