# Stage 4D-17MM Recovery Spectator Timing Temporary Payment Resource Id Uniqueness Audit

Date: 2026-05-28 01:55 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: spectator replay-frame timing `temporaryPaymentResources[]` item id uniqueness validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects duplicate normalized `resourceId` values across object-shaped spectator replay-frame timing temporary-payment-resource items.

The spectator temporary-payment-resource value helper now returns the normalized `resourceId` after required string validation. The spectator timing caller uses that returned id to detect duplicates before temporary-payment-resource parity consumers compare spectator timing resource identity against authoritative state. Existing scalar, list, map and property-name diagnostics remain unchanged.

Malformed resource identity now produces an explicit spectator replay diagnostic:

- Duplicate normalized resource ids produce `spectator replay frame timing temporary payment resource item resource id {resourceId} is duplicated`.

Compatibility retained:

- Non-object `temporaryPaymentResources[]` entries keep the existing item payload diagnostic.
- Temporary-payment-resource property-name drift keeps the existing property-name diagnostics.
- Malformed scalar, list and trait-map values keep the existing value diagnostics.
- Temporary-payment-resource count and authoritative parity mismatches keep their existing diagnostics.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceDuplicateIds`
- Focused single: `1/1`
- Focused recovery: `370/370`
- Adjacent recovery/opening/store-smoke: `951/951`
- Backend full: `6316/6316`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
