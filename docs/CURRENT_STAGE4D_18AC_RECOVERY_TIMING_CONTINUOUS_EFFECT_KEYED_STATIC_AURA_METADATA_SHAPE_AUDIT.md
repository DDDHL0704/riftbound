# Stage 4D-18AC Recovery Timing Continuous Effect Keyed Static-Aura Metadata Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraMetadataShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key authoritative validation for expected-present battlefield static-aura scalar metadata fields with unreadable shapes.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative battlefield static-aura continuous effect from real `MatchState` public-field and battlefield state.
- The same spectator `effectId` payload mutates `sourceCardNo`, `layerEngineStatus`, `sourceOrder`, `condition` and `lifecycle` to unreadable shapes.
- An extra continuous effect is appended so effect-count mismatch remains active and broad ordered parity is skipped.
- Assertions require same-payload scalar metadata shape diagnostics plus keyed authoritative mismatch diagnostics for all mutated scalar metadata fields.

## Validation

- Focused new keyed static-aura scalar metadata shape test: `1/1`.
- Focused `ContinuousEffect` filter: `142/142`.
- Focused recovery filter: `1057/1057`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1638/1638`.
- Backend full was not rerun for this first post-18AB test-only micro-slice; latest full remains Stage 4D-18AB at `7002/7002`.
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
