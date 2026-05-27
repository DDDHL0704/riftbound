# Stage 4D-17MG Recovery Snapshot Stack Item Value Audit

Date: 2026-05-28 01:12 CST

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

- Runtime file: `src/Riftbound.Engine/MatchRecovery.cs`
- Test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Slice: recovered player-view snapshot `Stack[]` item value validation only.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now validates object-shaped recovered player-view snapshot stack item values after 17MF stack item payload-shape validation and existing stack item property-name validation.

Malformed stack item values now produce explicit recovered snapshot stack diagnostics before future stack item consumers consume those payloads:

- Required `stackItemId`, `controllerId` and `effectKind` string values reject missing, blank, non-string and surrounding-whitespace values.
- Optional-present `sourceObjectId`, `cardNo` and `destination` string values reject malformed non-string, blank-whitespace and surrounding-whitespace values.
- Required `targetObjectIds` string-list values reject missing, malformed non-list payloads, blank entries, surrounding-whitespace entries and duplicate normalized entries.
- Required `damageAmount` values must be integer and non-negative.
- The stack item value rule body is shared with the existing spectator replay-frame snapshot stack validator.

Compatibility retained:

- Non-object `Stack[]` entries keep the 17MF payload-shape diagnostic.
- Object-shaped stack item property-name drift keeps the existing 17HI property-name diagnostics.
- Missing/null stack lists keep the existing `snapshot for {player} stack is required` diagnostic.
- Runtime change is limited to recovery frame validation; command resolution, protocol, frontend, matrix JSON and official catalog are unchanged.

## Test Evidence

- `RecoveryValidatorRejectsSnapshotStackItemValueShapeDrift`
- Focused single: `1/1`
- Focused recovery: `364/364`
- Adjacent recovery/opening/store-smoke: `945/945`
- Backend full: `6310/6310`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse

## Remaining Gates

This narrows P1-004 recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
