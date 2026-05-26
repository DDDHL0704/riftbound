# Stage 4D-17JE Recovery Snapshot Turn-Window Value Shape Audit

Date: 2026-05-26 10:28 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for `Timing["turnWindow"]` only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

The previous snapshot coverage for `Timing["turnWindow"]` guarded payload property names. Stage 4D-17JE adds value-shape validation for the present recovered snapshot payload before turn-window field reads and parity comparison consume those values.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingTurnWindowPayloadValues` after the existing recovered snapshot `turnWindow` property-name validation.
- `ValidateSnapshotTimingTurnWindowPayloadValues` reads present recovered player-view snapshot `Timing["turnWindow"]` payloads and validates only snapshot-player payload objects.
- Spectator and recovered snapshot turn-window validation now share `ValidateTurnWindowPayloadValues`, preserving the existing spectator label and diagnostics while adding the recovered snapshot label `snapshot for {playerId} timing turn window`.

## Validated Fields

- Required string `state` rejects missing, blank, non-string and surrounding-whitespace values.
- Required booleans `isSpellDuel`, `isClosed` and `hasStack` reject missing, null and non-boolean values.
- Optional-present string `actingPlayerId` rejects malformed present values while preserving optional-empty/null compatibility.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingTurnWindowValueDrift` mutates recovered snapshot `Timing["turnWindow"]` through `RawJson` and proves explicit diagnostics for:

- whitespace-mutated `state`
- invalid `isSpellDuel`
- missing/null `isClosed`
- invalid `hasStack`
- whitespace-mutated `actingPlayerId`

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `284/284`
- Adjacent recovery/opening/store-smoke filter: `865/865`
- Backend full: `6230/6230`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot turn-window value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
