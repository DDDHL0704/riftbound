# Stage 4D-17KS Recovery Spectator Snapshot Player Scalar Value Shape Audit

Date: 2026-05-27 17:29 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot `Players[*]` top-level scalar value shape only. It does not change player map property-name validation, nested rune-pool/zone/object payload shape, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KR covered spectator snapshot top-level core scalar value shape. Stage 4D-17KS adds explicit spectator player scalar shape validation before player scalar parity comparisons consume those values.

## Runtime Change

- Spectator player `id` and `name` now reject missing/blank values and surrounding-whitespace values before matching the player key.
- Spectator player `ready`, `deckSubmitted` and `mulliganCompleted` now reject missing/null and non-boolean values before authoritative parity comparison.
- Spectator player `handSize`, `score`, `experience` and `cardsPlayedThisTurn` now reject missing/null, non-integer and negative values before authoritative parity comparison.
- Existing parity diagnostics for valid-but-mismatched player scalar values remain in place.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerScalarValueShapeDrift` mutates a spectator replay-frame player payload with whitespace-mutated ids, malformed booleans, negative counts and a non-integer scalar, then proves explicit diagnostics for each malformed player scalar value.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `324/324`
- Adjacent recovery/opening/store-smoke filter: `905/905`
- Backend full: `6270/6270`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot player scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
