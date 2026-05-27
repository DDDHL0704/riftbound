# Stage 4D-17KT Recovery Spectator Snapshot Player Rune Pool Value Shape Audit

Date: 2026-05-27 17:41 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot `Players[*]["runePool"]` value shape only. It does not change player scalar validation, zone/object payload shape, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KS covered spectator snapshot top-level player scalar value shape. Stage 4D-17KT adds explicit spectator player rune-pool value-shape validation before rune-pool parity comparisons consume those values.

## Runtime Change

- Spectator player rune-pool `mana`, `power` and `untypedPower` now reject missing/null, non-integer and negative values before authoritative rune-pool parity comparison.
- Spectator player rune-pool `powerByTrait` now rejects missing/null, non-object maps and non-positive or non-integer trait entries before authoritative trait-power parity comparison.
- Existing parity diagnostics for valid-but-mismatched rune-pool values remain in place.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerRunePoolValueShapeDrift` mutates a spectator replay-frame player rune-pool payload with negative scalar values, a non-integer scalar and malformed trait-power entries, then proves explicit diagnostics for each malformed rune-pool value.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `325/325`
- Adjacent recovery/opening/store-smoke filter: `906/906`
- Backend full: `6271/6271`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot player rune-pool value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
