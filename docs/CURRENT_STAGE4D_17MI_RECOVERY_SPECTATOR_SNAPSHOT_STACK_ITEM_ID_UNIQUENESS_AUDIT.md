# Stage 4D-17MI Recovery Spectator Snapshot Stack Item Id Uniqueness Audit

Date: 2026-05-28 01:27 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: spectator replay-frame snapshot `Stack[]` item id uniqueness validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects duplicate normalized `stackItemId` values across object-shaped spectator replay-frame snapshot stack items.

The spectator stack item value helper now returns the normalized `stackItemId` after required string validation. The spectator snapshot caller uses that returned id to detect duplicates before stack parity consumers compare spectator stack item ids against authoritative state. This mirrors the recovered player-view stack identity rule added in 17MH while keeping the existing spectator stack field-value validation semantics.

Malformed stack identity now produces an explicit spectator replay diagnostic before later stack comparisons can consume ambiguous stack identity:

- Duplicate normalized stack item ids produce `spectator replay frame snapshot stack item {stackItemId} is duplicated`.

Compatibility retained:

- Non-object spectator `Stack[]` entries keep the existing payload-shape diagnostic.
- Object-shaped stack item property-name drift keeps the existing property-name diagnostics.
- Malformed stack item field values keep the 17MG value diagnostics.
- Stack count and authoritative stack parity mismatches keep their existing diagnostics.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemDuplicateIds`
- Focused single: `1/1`
- Focused recovery: `366/366`
- Adjacent recovery/opening/store-smoke: `947/947`
- Backend full: `6312/6312`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
