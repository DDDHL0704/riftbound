# Stage 4D-17KQ Recovery Spectator Snapshot Stack Item Value Shape Audit

Date: 2026-05-27 17:09 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot validation for `Stack[]` item payload/value shape only. It does not change timing payloads, lane payloads, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KP covered spectator lane standby slot value shape. Stage 4D-17KQ adds explicit spectator snapshot stack item payload and value-shape validation before stack item comparisons consume those values.

## Runtime Change

- `ValidateSpectatorSnapshotStackItemPayloads` now rejects non-object stack item entries with an explicit payload diagnostic.
- Stack item payload property-name validation remains in place for object entries.
- Required string values `stackItemId`, `controllerId` and `effectKind` now validate shape before comparison.
- Optional-present string values `sourceObjectId`, `cardNo` and `destination` now validate shape while preserving absent/null compatibility.
- Required string-list values `targetObjectIds` now reject malformed lists, blank entries, surrounding-whitespace entries and duplicate normalized entries.
- Required `damageAmount` now validates as a non-negative integer before comparison.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemValueShapeDrift` mutates spectator replay-frame snapshot stack item payloads and proves explicit diagnostics for a non-object item payload, whitespace-mutated required and optional strings, malformed optional strings, malformed target-object lists, duplicate normalized targets and negative damage amounts.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `322/322`
- Adjacent recovery/opening/store-smoke filter: `903/903`
- Backend full: `6268/6268`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot stack item value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
