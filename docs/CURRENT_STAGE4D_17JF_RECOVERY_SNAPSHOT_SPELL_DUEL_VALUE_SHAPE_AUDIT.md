# Stage 4D-17JF Recovery Snapshot Spell-Duel Value Shape Audit

Date: 2026-05-26 10:44 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for `Timing["spellDuel"]` only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

The previous snapshot coverage for `Timing["spellDuel"]` guarded payload property names. Stage 4D-17JF adds value-shape validation for the present recovered snapshot payload before spell-duel field reads and parity comparison consume those values.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingSpellDuelPayloadValues` after the existing recovered snapshot `spellDuel` property-name validation.
- `ValidateSnapshotTimingSpellDuelPayloadValues` reads present recovered player-view snapshot `Timing["spellDuel"]` payloads and validates only snapshot-player payload objects.
- Spectator and recovered snapshot spell-duel validation now share `ValidateSpellDuelPayloadValues`, preserving the existing spectator label and diagnostics while adding the recovered snapshot label `snapshot for {playerId} timing spell duel`.

## Validated Fields

- Required booleans `isActive` and `isClosed` reject missing, null and non-boolean values.
- Optional-present strings `spellDuelId`, `battlefieldObjectId` and `focusPlayerId` reject malformed present values while preserving optional-empty/null compatibility.
- Required string lists `passedFocusPlayerIds`, `stackItemIds` and `stackControllerIds` reject missing, null and non-list payloads.
- Required string-list entries reject blank values, surrounding-whitespace values and duplicate normalized values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingSpellDuelValueDrift` mutates recovered snapshot `Timing["spellDuel"]` through `RawJson` and proves explicit diagnostics for:

- invalid `isActive`
- missing/null `isClosed`
- whitespace-mutated `spellDuelId`
- invalid `battlefieldObjectId`
- whitespace-mutated `focusPlayerId`
- whitespace-mutated, duplicate and blank `passedFocusPlayerIds`
- invalid `stackItemIds`
- missing/null `stackControllerIds`

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `285/285`
- Adjacent recovery/opening/store-smoke filter: `866/866`
- Backend full: `6231/6231`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot spell-duel value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
