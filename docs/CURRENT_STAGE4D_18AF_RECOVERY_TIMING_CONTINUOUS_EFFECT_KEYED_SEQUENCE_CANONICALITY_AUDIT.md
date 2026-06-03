# Stage 4D-18AF Recovery Timing Continuous Effect Keyed Sequence Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedSequenceCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key authoritative validation for readable but non-canonical `sequence` values.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state.
- The same spectator `effectId` payload keeps a readable `sequence` field but changes it from `1` to `0`.
- An extra continuous effect with sequence `3` is appended so effect-count mismatch remains active and broad ordered parity is skipped.
- Assertions require same-payload positive/contiguous sequence diagnostics, keyed authoritative sequence mismatch diagnostics and the count mismatch diagnostic.

## Validation

- Focused new keyed sequence canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `145/145`.
- Focused recovery filter: `1060/1060`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1641/1641`.
- Backend full was not rerun for this first post-18AE test-only micro-slice; latest full remains Stage 4D-18AE at `7005/7005`.
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
