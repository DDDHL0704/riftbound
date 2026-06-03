# Stage 4D-18AE Recovery Timing Continuous Effect Keyed Known Value Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedKnownValueCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key authoritative validation for readable but non-canonical or unknown scalar/object values.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state.
- The same spectator `effectId` payload mutates `scope`, `layer`, `duration` and `layerEngineStatus` to unknown values, and mutates `targetObjectId`, `sourceObjectId`, `effectKind`, `sourceCardNo` and `sourcePath` with surrounding whitespace.
- An extra continuous effect is appended so effect-count mismatch remains active and broad ordered parity is skipped.
- Assertions require same-payload known-value/canonicality diagnostics plus keyed authoritative mismatch diagnostics for all mutated same-key fields.

## Validation

- Focused new keyed known-value/canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `144/144`.
- Focused recovery filter: `1059/1059`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1640/1640`.
- Backend full: `7005/7005`.
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
