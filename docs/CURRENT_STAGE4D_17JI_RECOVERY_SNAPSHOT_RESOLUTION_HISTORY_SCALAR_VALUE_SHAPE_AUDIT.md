# Stage 4D-17JI Recovery Snapshot Resolution-History Scalar Value Shape Audit

Date: 2026-05-26 11:26 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present `Timing["battlefieldResolutions"][]` and `Timing["battleResolutions"][]` scalar and numeric item values only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

The previous recovered snapshot coverage for resolution history guarded item property names and list-valued fields. Stage 4D-17JI adds scalar/numeric value-shape validation before resolution-history field reads and parity comparison consume those values.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingResolutionHistoryScalarPayloadValues` before the existing recovered snapshot resolution-history list-value validation.
- Present recovered snapshot battlefield-resolution item payloads now validate required and optional scalar fields with the recovered snapshot label.
- Present recovered snapshot battle-resolution item payloads now validate required and optional scalar fields with the recovered snapshot label.
- Spectator and recovered snapshot resolution-history scalar validation now share helper logic while preserving existing spectator labels and diagnostics.

## Validated Fields

- Battlefield-resolution items validate required `resolutionId`, `tick`, `kind`, `reason` and `battlefieldObjectId`.
- Battlefield-resolution items validate optional-present `playerId`, `previousControllerId`, `controllerId` and `sourceObjectId`.
- Battle-resolution items validate required `resolutionId`, `tick`, `kind`, `reason` and `battlefieldId`.
- Battle-resolution items validate optional-present `attackingPlayerId`, `defendingPlayerId` and `winnerPlayerId`.
- Ticks must be valid non-negative long values, required strings must be present and non-blank, and present optional strings must be valid without surrounding-whitespace drift.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingResolutionHistoryScalarValueDrift` mutates recovered snapshot resolution-history timing payloads through `RawJson` and proves explicit diagnostics for:

- whitespace-mutated resolution ids and object/player ids
- invalid and negative tick values
- blank required kind / resolution id values
- non-string required reason values
- invalid optional scalar values
- blank optional scalar values

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `288/288`
- Adjacent recovery/opening/store-smoke filter: `869/869`
- Backend full: `6234/6234`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot resolution-history scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
