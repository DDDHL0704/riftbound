# Stage 4D-17JZ Recovery Snapshot Player Zones Value Shape Audit

Date: 2026-05-27 14:42 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present object-shaped nested `Players[*]["zones"]` value shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JU covered zones payload shape. Stage 4D-17JZ adds explicit value-shape validation for present object-shaped recovered snapshot zones payloads before zone consumers/comparisons consume those fields. Missing or null zones compatibility for recovered snapshots is unchanged because value validation only runs after the zones payload is present and object-shaped.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player zones values after zones payload and property-name validation.
- Present object-shaped recovered snapshot `Players[*]["zones"]` now requires non-negative integer `mainDeckCount`, `runeDeckCount`, `handHidden` and `battlefieldHiddenStandbyCount` values.
- Present object-shaped recovered snapshot `Players[*]["zones"]` now requires `hand`, `base`, `battlefields`, `graveyard`, `banished`, `legendZone` and `championZone` string-list payloads.
- Zone list values now reject blank entries, surrounding-whitespace entries and duplicate normalized entries with explicit recovered snapshot diagnostics.
- Existing player payload, zones payload-shape and zones property-name validation behavior is unchanged for valid value payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerZonesValueShapeDrift` mutates a recovered snapshot player zones payload and proves explicit diagnostics for negative count values, non-integer count values, invalid list payloads, null required list payloads, blank list entries, surrounding-whitespace list entries and duplicate normalized list entries.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `305/305`
- Adjacent recovery/opening/store-smoke filter: `886/886`
- Backend full: `6251/6251`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player zones value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
