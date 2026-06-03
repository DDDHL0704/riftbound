# Stage 4D-18AD Recovery Timing Continuous Effect Keyed Required Field Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedRequiredFieldShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key authoritative validation for expected-present required scalar/object fields with unreadable shapes.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state.
- The same spectator `effectId` payload mutates `scope`, `layer`, `duration`, `targetObjectId`, `sourceObjectId`, `powerDelta`, `basePower`, `effectivePower` and `sequence` to unreadable shapes.
- An extra continuous effect is appended so effect-count mismatch remains active and broad ordered parity is skipped.
- Assertions require same-payload required-field shape diagnostics plus keyed authoritative mismatch diagnostics for all mutated required fields.

## Validation

- Focused new keyed required field shape test: `1/1`.
- Focused `ContinuousEffect` filter: `143/143`.
- Focused recovery filter: `1058/1058`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1639/1639`.
- Backend full was not rerun for this second post-18AB test-only micro-slice; latest full remains Stage 4D-18AB at `7002/7002`.
- Touched-file scoped whitespace format passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src` passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Path typo scan for `tests\.Riftbound` over `docs`, `tests` and `src` passed.

## Open

- Broader command/recovery/random determinism remains open.
- Remaining recovered/spectator/authoritative nested payload shape/value breadth remains open.
- Full LayerEngine breadth remains open.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
