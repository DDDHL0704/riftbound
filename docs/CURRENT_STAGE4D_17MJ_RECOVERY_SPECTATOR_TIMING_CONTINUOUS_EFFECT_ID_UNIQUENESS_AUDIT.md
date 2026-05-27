# Stage 4D-17MJ Recovery Spectator Timing Continuous Effect Id Uniqueness Audit

Date: 2026-05-28 01:34 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: spectator replay-frame timing `continuousEffects[]` item id uniqueness validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects duplicate normalized `effectId` values across object-shaped spectator replay-frame timing continuous-effect items.

The shared continuous-effect scalar value helper now returns the normalized `effectId` after required string validation. The spectator timing caller uses that returned id to detect duplicates before continuous-effect parity consumers compare spectator timing identity against authoritative state. Existing recovered and spectator scalar value diagnostics remain unchanged.

Malformed continuous-effect identity now produces an explicit spectator replay diagnostic before later timing comparisons can consume ambiguous effect identity:

- Duplicate normalized effect ids produce `spectator replay frame timing continuous effect item effect id {effectId} is duplicated`.

Compatibility retained:

- Non-object `continuousEffects[]` entries keep the existing item payload diagnostic.
- Continuous-effect property-name drift keeps the existing property-name diagnostics.
- Malformed continuous-effect scalar and list values keep the existing value diagnostics.
- Continuous-effect count and authoritative parity mismatches keep their existing diagnostics.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectDuplicateIds`
- Focused single: `1/1`
- Focused recovery: `367/367`
- Adjacent recovery/opening/store-smoke: `948/948`
- Backend full: `6313/6313`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
