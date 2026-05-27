# Stage 4D-17MH Recovery Snapshot Stack Item Id Uniqueness Audit

Date: 2026-05-28 01:20 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Stack[]` item id uniqueness validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now rejects duplicate normalized `stackItemId` values across object-shaped recovered player-view snapshot stack items.

The shared stack item value helper now returns the normalized `stackItemId` after required string validation. The recovered snapshot caller uses that returned id to detect duplicates in one validation pass, while the spectator replay-frame stack validation continues to share the same field-value rule body without changing spectator identity rules.

Malformed stack identity now produces an explicit recovered snapshot diagnostic before future stack item consumers can consume ambiguous stack identity:

- Duplicate normalized stack item ids produce `snapshot for {player} stack item {stackItemId} is duplicated`.

Compatibility retained:

- Non-object `Stack[]` entries keep the 17MF payload-shape diagnostic.
- Object-shaped stack item property-name drift keeps the existing 17HI property-name diagnostics.
- Malformed stack item field values keep the 17MG value diagnostics.
- Missing/null stack lists keep the existing `snapshot for {player} stack is required` diagnostic.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotStackItemDuplicateIds`
- Focused single: `1/1`
- Focused recovery: `365/365`
- Adjacent recovery/opening/store-smoke: `946/946`
- Backend full: `6311/6311`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
