# Stage 4D-17KO Recovery Spectator Snapshot Lane Battlefield List Value Shape Audit

Date: 2026-05-27 16:46 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for list and list-map values inside `Lanes["battlefields"][]` only. It does not change standby slot item values, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KN covered lane battlefield scalar value shape. Stage 4D-17KO adds explicit lane battlefield list value-shape validation before spectator lane battlefield list comparisons consume those values.

## Runtime Change

- `ValidateSpectatorSnapshotBattlefieldListPayloads` now validates required string-list values before comparison for `occupantObjectIds`, `occupantControllerIds`, `standbyObjectIds`, `scoredThisTurnPlayerIds`, and `pendingTaskKinds`.
- `unitsBySide` now validates required string-list-map values before comparison, while existing property-name validation remains unchanged.
- Malformed list payloads, blank entries, surrounding-whitespace entries, duplicate normalized entries, and malformed map list values now produce explicit lane battlefield diagnostics.
- Existing authoritative mismatch behavior remains in place for valid list and list-map values.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldListValueShapeDrift` mutates a spectator replay-frame snapshot lane battlefield payload and proves explicit diagnostics for invalid list payloads, whitespace-mutated entries, blank entries, duplicate normalized entries, and malformed `unitsBySide` map list values.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `320/320`
- Adjacent recovery/opening/store-smoke filter: `901/901`
- Backend full: `6266/6266`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot lane battlefield list value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including spectator lane standby slot value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
