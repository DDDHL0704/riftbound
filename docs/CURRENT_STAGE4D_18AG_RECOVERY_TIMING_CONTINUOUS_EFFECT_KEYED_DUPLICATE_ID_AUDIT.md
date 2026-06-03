# Stage 4D-18AG Recovery Timing Continuous Effect Keyed Duplicate Id Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedDuplicateIdWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` duplicate `effectId` validation under effect-count mismatch.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state.
- The spectator payload keeps the authoritative `effectId` on both entries and appends a duplicate copy with `sequence = 2`.
- Effect-count mismatch stays active, so broad ordered parity is skipped while duplicate-id and keyed authoritative validation still run.
- Assertions require duplicate `effectId`, keyed authoritative sequence mismatch and count mismatch diagnostics.

## Validation

- Focused new keyed duplicate-id test: `1/1`.
- Focused `ContinuousEffect` filter: `146/146`.
- Focused recovery filter: `1061/1061`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1642/1642`.
- Backend full was not rerun for this second post-18AE test-only micro-slice; latest full remains Stage 4D-18AE at `7005/7005`.
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
