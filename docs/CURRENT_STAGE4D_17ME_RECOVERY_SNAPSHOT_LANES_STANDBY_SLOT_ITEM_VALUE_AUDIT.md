# Stage 4D-17ME Recovery Snapshot Lanes Standby Slot Item Value Audit

Date: 2026-05-28 00:56 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Lanes["battlefields"][]["standbySlots"][]` item value validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates each nested `standbySlots` item payload object's values inside object entries of list-shaped recovered snapshot lanes `battlefields` payloads after 17MD standby slot item property-name validation.

Malformed standby slot item values now produce explicit recovered lanes battlefield item standby slot item diagnostics before later standby-slot consumers consume those payloads:

- Required `slotId` and `battlefieldObjectId` string values reject missing, blank, non-string and surrounding-whitespace values.
- Optional-present `sidePlayerId`, `controllerId` and `objectId` string values reject malformed non-string, blank-whitespace and surrounding-whitespace values.
- Required `visible` and `isFaceDown` values must be booleans.
- Required `state` must be `VISIBLE` or `HIDDEN`.
- Visible slots require a well-formed `objectId`; hidden slots must redact `objectId`.
- `state` must match the `visible` flag.

Compatibility retained:

- Missing/null/non-list lane lists keep existing recovered snapshot compatibility and 17LV/17LW/17LX diagnostics.
- Present non-list `standbySlots` payloads keep the 17MB list payload diagnostic.
- Non-object `standbySlots` entries keep the 17MC item payload diagnostic.
- Malformed standby slot item property names keep the 17MD diagnostics.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotLanesBattlefieldStandbySlotItemValueShapeDrift`
- Focused single: `1/1`
- Focused recovery: `362/362`
- Adjacent recovery/opening/store-smoke: `943/943`
- Backend full: `6308/6308`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
