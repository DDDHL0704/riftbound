# Stage 4D-17ML Recovery Snapshot Timing Temporary Payment Resource Id Uniqueness Audit

Date: 2026-05-28 01:49 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot timing `temporaryPaymentResources[]` item id uniqueness validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now rejects duplicate normalized `resourceId` values across object-shaped recovered snapshot timing temporary-payment-resource items.

The recovered snapshot temporary-payment-resource scalar caller now uses the existing required-string validation helper's normalized `resourceId` to detect duplicates before future temporary-payment-resource consumers can consume ambiguous recovered timing resource identity. Existing scalar, list, map and property-name diagnostics remain unchanged.

Malformed resource identity now produces an explicit recovered snapshot diagnostic:

- Duplicate normalized resource ids produce `snapshot for {playerId} timing temporary payment resource item resource id {resourceId} is duplicated`.

Compatibility retained:

- Non-object `temporaryPaymentResources[]` entries keep the existing item payload diagnostic.
- Temporary-payment-resource property-name drift keeps the existing property-name diagnostics.
- Malformed scalar, list and trait-map values keep the existing value diagnostics.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotTimingTemporaryPaymentResourceDuplicateIds`
- Focused single: `1/1`
- Focused recovery: `369/369`
- Adjacent recovery/opening/store-smoke: `950/950`
- Backend full: `6315/6315`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
