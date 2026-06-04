# Stage 4D-18BA Recovery Timing Continuous Effect Keyed Metadata List Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key static-aura metadata-list canonicality when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing list canonicality and keyed authoritative validation.

## Evidence

- The test builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state.
- The spectator payload keeps the authoritative `effectId` and dynamically reads the emitted participant/dependency/residual list values, then rewrites `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds`, `participantDependencyObjectIds` and `deferredLayerEngineResiduals` with whitespace-padded versions of those same values.
- A second effect with `effectId = "effect-extra"` and `sequence = 2` keeps effect-count mismatch active.
- Assertions require list-item surrounding-whitespace diagnostics, keyed authoritative mismatch diagnostics for all five metadata-list fields, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed metadata-list canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `149/149`.
- Focused recovery filter: `1081/1081`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1662/1662`.
- Backend full was not rerun for this first post-18AZ test-only micro-slice; latest backend full remains Stage 4D-18AZ at `7026/7026`.
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
