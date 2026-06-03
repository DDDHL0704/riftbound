# Stage 4D-18AB Recovery Timing Continuous Effect Keyed Static-Aura List Metadata Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key authoritative validation for expected-present battlefield static-aura list metadata fields with unreadable shapes.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative battlefield static-aura continuous effect from real `MatchState` public-field and battlefield state.
- The same spectator `effectId` payload mutates `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds`, `participantDependencyObjectIds` and `deferredLayerEngineResiduals` to non-list or non-string-list shapes.
- An extra continuous effect is appended so effect-count mismatch remains active and broad ordered parity is skipped.
- Assertions require same-payload list-shape diagnostics plus keyed authoritative mismatch diagnostics for all mutated list metadata fields.

## Validation

- Focused new keyed static-aura list metadata shape test: `1/1`.
- Focused `ContinuousEffect` filter: `141/141`.
- Focused recovery filter: `1056/1056`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1637/1637`.
- Backend full: `7002/7002`.
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
