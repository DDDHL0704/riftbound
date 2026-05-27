# Stage 4D-17JY Recovery Snapshot Player Rune-Pool Value Shape Audit

Date: 2026-05-27 14:34 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present object-shaped nested `Players[*]["runePool"]` value shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JT covered rune-pool payload shape. Stage 4D-17JY adds explicit value-shape validation for present object-shaped recovered snapshot rune-pool payloads before rune-pool consumers/comparisons consume those fields. Missing or null rune-pool compatibility for recovered snapshots is unchanged because value validation only runs after the rune-pool payload is present and object-shaped.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player rune-pool values after rune-pool payload and property-name validation.
- Present object-shaped recovered snapshot `Players[*]["runePool"]` now requires non-negative integer `mana`, `power` and `untypedPower` values.
- Present object-shaped recovered snapshot `Players[*]["runePool"]["powerByTrait"]` now requires an object map with positive integer entries.
- Existing player payload, rune-pool payload-shape and rune-pool property-name validation behavior is unchanged for valid value payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerRunePoolValueShapeDrift` mutates a recovered snapshot player rune-pool payload and proves explicit diagnostics for negative scalar values, non-integer scalar values, non-positive trait-map entries and non-integer trait-map entries.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `304/304`
- Adjacent recovery/opening/store-smoke filter: `885/885`
- Backend full: `6250/6250`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player rune-pool value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
