# Stage 4D-17JT Recovery Snapshot Player Rune Pool Payload Shape Audit

Date: 2026-05-26 13:50 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present nested `Players[*]["runePool"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JS covered top-level pending-hand-choice object shape. Stage 4D-17JT adds an explicit object-shape guard for recovered snapshot player rune-pool payloads. Missing or null rune-pool compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates present non-null nested player `runePool` payload shape before rune-pool property-name validation.
- Present non-null recovered snapshot `Players[*]["runePool"]` now fails with `snapshot for {viewPlayerId} player {snapshotPlayerId} rune pool payload is required` when it is not an object payload.
- Existing player and rune-pool property-name validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerRunePoolPayloadShapeDrift` mutates a recovered snapshot player payload and proves explicit diagnostics for a non-object nested rune-pool payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `299/299`
- Adjacent recovery/opening/store-smoke filter: `880/880`
- Backend full: `6245/6245`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player rune-pool nested payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
