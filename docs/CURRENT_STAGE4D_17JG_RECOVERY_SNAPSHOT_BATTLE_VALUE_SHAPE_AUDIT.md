# Stage 4D-17JG Recovery Snapshot Battle Value Shape Audit

Date: 2026-05-26 10:53 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for top-level `Timing["battle"]` values only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

The previous snapshot coverage for `Timing["battle"]` guarded payload property names and participant-controller map property names. Stage 4D-17JG adds value-shape validation for the present recovered snapshot battle payload before battle field reads and parity comparison consume those values.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingBattlePayloadValues` after the existing recovered snapshot `battle` property-name validation.
- `ValidateSnapshotTimingBattlePayloadValues` reads present recovered player-view snapshot `Timing["battle"]` payloads and validates only snapshot-player payload objects.
- Spectator and recovered snapshot battle validation now share `ValidateBattlePayloadValues`, preserving the existing spectator label and diagnostics while adding the recovered snapshot label `snapshot for {playerId} timing battle`.

## Validated Fields

- Required boolean `isActive` rejects missing, null and non-boolean values.
- Optional-present strings `battleId` and `battlefieldObjectId` reject malformed present values while preserving optional-empty/null compatibility.
- Required string lists `attackerObjectIds` and `defenderObjectIds` reject missing, null and non-list payloads.
- Required string-list entries reject blank values, surrounding-whitespace values and duplicate normalized values.
- Required `participantControllerIds` rejects missing, null and non-object payloads.
- Participant-controller map values reject non-string, blank and surrounding-whitespace values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingBattleValueDrift` mutates recovered snapshot `Timing["battle"]` through `RawJson` and proves explicit diagnostics for:

- invalid `isActive`
- whitespace-mutated `battleId`
- invalid `battlefieldObjectId`
- whitespace-mutated, duplicate and blank `attackerObjectIds`
- invalid `defenderObjectIds`
- whitespace-mutated, invalid and blank `participantControllerIds` map values

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `286/286`
- Adjacent recovery/opening/store-smoke filter: `867/867`
- Backend full: `6232/6232`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot battle value shape only. Nested battle damage-assignment value breadth, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
