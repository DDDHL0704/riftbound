# Stage 4D-17KD Recovery Snapshot Player Object Numeric Scalar Value Shape Audit

Date: 2026-05-27 15:13 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present optional numeric scalar values inside nested `Players[*]["objects"][objectId]` payloads only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17KC covered recovered object optional string scalar values. Stage 4D-17KD adds explicit optional-present numeric scalar value-shape validation for present object-shaped recovered snapshot object payloads before object scalar consumers/comparisons consume those fields. Boolean and list object scalar value shape remain open for later small slices.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates recovered player-view snapshot player object optional numeric scalar values after object string scalar value validation.
- Present `damage`, `power`, `basePower`, `effectivePower`, `untilEndOfTurnPowerModifier` and `manaCost` values now reject malformed non-integer values.
- Present `damage` and `manaCost` values now reject negative values, matching authoritative card-object value constraints.
- Missing or null numeric scalar values remain compatible so redacted face-down object payloads are not forced to expose visible-object metadata.
- Existing object payload-shape, object property-name, object base value, object string scalar and object location validation behavior is unchanged for valid optional numeric scalar values.

## Test Coverage

`RecoveryValidatorRejectsSnapshotPlayerObjectNumericScalarValueShapeDrift` mutates recovered snapshot player object payloads and proves explicit diagnostics for negative damage, malformed power/base/effective power, malformed until-end-of-turn power modifier and negative mana cost values.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `309/309`
- Adjacent recovery/opening/store-smoke filter: `890/890`
- Backend full: `6255/6255`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot player object optional numeric scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth including object boolean/list value shape, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
