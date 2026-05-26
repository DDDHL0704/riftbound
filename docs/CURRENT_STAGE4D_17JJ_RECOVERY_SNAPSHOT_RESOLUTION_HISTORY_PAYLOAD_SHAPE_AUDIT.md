# Stage 4D-17JJ Recovery Snapshot Resolution-History Payload Shape Audit

Date: 2026-05-26 11:43 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present `Timing["battlefieldResolutions"][]` and `Timing["battleResolutions"][]` item payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

The previous recovered snapshot coverage for resolution history guarded item property names, list-valued fields and scalar/numeric fields, but non-object array entries were skipped by those deeper validators. Stage 4D-17JJ adds explicit object-shape validation before those item payloads are consumed.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingResolutionHistoryItemPayloads` before recovered snapshot resolution-history property-name, scalar-value and list-value validation.
- Present recovered snapshot battlefield-resolution entries now fail with `snapshot for {playerId} timing battlefield resolution payload is required` when an array item is not an object payload.
- Present recovered snapshot battle-resolution entries now fail with `snapshot for {playerId} timing battle resolution payload is required` when an array item is not an object payload.
- The change is recovered snapshot validation only; spectator replay-frame resolution-history validation already rejects non-object item payloads with its own spectator labels.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingResolutionHistoryItemPayloadShapeDrift` mutates recovered snapshot resolution-history timing payloads through `RawJson` and proves explicit diagnostics for:

- non-object battlefield-resolution array entries
- non-object battle-resolution array entries
- mixed arrays where valid object entries continue through the existing property-name, scalar-value and list-value validators

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `289/289`
- Adjacent recovery/opening/store-smoke filter: `870/870`
- Backend full: `6235/6235`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot resolution-history item payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
