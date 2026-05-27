# Stage 4D-17KU Recovery Spectator Snapshot Player Zones Value Shape Audit

Date: 2026-05-27 17:50 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot `Players[*]["zones"]` value shape only. It does not change player scalar/rune-pool validation, object payload shape, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KT covered spectator snapshot player rune-pool value shape. Stage 4D-17KU adds explicit spectator player zones value-shape validation before zone parity and redaction checks consume those values.

## Runtime Change

- Spectator player zones `mainDeckCount`, `runeDeckCount`, `handHidden` and `battlefieldHiddenStandbyCount` now reject missing/null, non-integer and negative values before authoritative zone count comparisons.
- Spectator player zones `hand`, `base`, `battlefields`, `graveyard`, `banished`, `legendZone` and `championZone` now reject missing/null, non-list payloads, blank entries, surrounding-whitespace entries and duplicate normalized entries before redaction or authoritative zone-list comparisons.
- Existing spectator hand redaction and valid-but-mismatched zone parity diagnostics remain in place.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerZonesValueShapeDrift` mutates a spectator replay-frame player zones payload with negative count scalars, malformed count scalars, malformed lists, blank entries, surrounding-whitespace entries and duplicate normalized entries, then proves explicit spectator zone value-shape diagnostics.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `326/326`
- Adjacent recovery/opening/store-smoke filter: `907/907`
- Backend full: `6272/6272`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot player zones value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
