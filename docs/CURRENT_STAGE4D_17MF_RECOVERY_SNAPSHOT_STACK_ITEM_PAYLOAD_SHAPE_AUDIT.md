# Stage 4D-17MF Recovery Snapshot Stack Item Payload Shape Audit

Date: 2026-05-28 01:04 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Stack[]` item payload shape validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates recovered player-view snapshot stack item payload shape before existing stack item property-name validation.

Malformed stack item payloads now produce explicit recovered snapshot stack diagnostics before stack item property-name or future value consumers consume those payloads:

- Non-object `Stack[]` entries fail with `snapshot for {player} stack item #{n} payload is required`.
- Object-shaped stack item entries continue through the existing 17HI stack item property-name validation.
- Missing/null stack lists keep the existing `snapshot for {player} stack is required` diagnostic.

Compatibility retained:

- Runtime change is limited to recovery frame validation.
- Command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.
- This slice does not yet validate recovered stack item scalar/list values.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotStackItemPayloadShapeDrift`
- Focused single: `1/1`
- Focused recovery: `363/363`
- Adjacent recovery/opening/store-smoke: `944/944`
- Backend full: `6309/6309`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, recovered stack item value validation, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
