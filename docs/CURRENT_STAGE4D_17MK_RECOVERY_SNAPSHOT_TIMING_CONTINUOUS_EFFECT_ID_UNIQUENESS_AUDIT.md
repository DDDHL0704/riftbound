# Stage 4D-17MK Recovery Snapshot Timing Continuous Effect Id Uniqueness Audit

Date: 2026-05-28 01:42 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot timing `continuousEffects[]` item id uniqueness validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now rejects duplicate normalized `effectId` values across object-shaped recovered snapshot timing continuous-effect items.

The shared continuous-effect scalar value helper already returns the normalized `effectId` after required string validation. The recovered snapshot timing caller now uses that returned id to detect duplicates before future continuous-effect consumers can consume ambiguous recovered timing identity. Existing scalar, list and property-name diagnostics remain unchanged.

Malformed continuous-effect identity now produces an explicit recovered snapshot diagnostic:

- Duplicate normalized effect ids produce `snapshot for {playerId} timing continuous effect item effect id {effectId} is duplicated`.

Compatibility retained:

- Non-object `continuousEffects[]` entries keep the existing item payload diagnostic.
- Continuous-effect property-name drift keeps the existing property-name diagnostics.
- Malformed continuous-effect scalar and list values keep the existing value diagnostics.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotTimingContinuousEffectDuplicateIds`
- Focused single: `1/1`
- Focused recovery: `368/368`
- Adjacent recovery/opening/store-smoke: `949/949`
- Backend full: `6314/6314`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
