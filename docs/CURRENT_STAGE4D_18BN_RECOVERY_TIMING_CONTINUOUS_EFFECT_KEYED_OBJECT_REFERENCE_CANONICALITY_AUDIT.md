# Stage 4D-18BN Recovery Timing Continuous Effect Keyed Object Reference Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedObjectReferenceCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key PowerModifier object-reference canonicality when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar canonicality and keyed authoritative continuous-effect parity.

## Evidence

- The test builds authoritative continuous effects from real `MatchState` PowerModifier ledger state.
- The spectator payload starts with canonical `targetObjectId = "target-1"` and `sourceObjectId = "source-1"`.
- The test mutates those fields to `" target-1 "` and `" source-1 "` while keeping the authoritative `effectId` readable for keyed lookup.
- The test appends `effect-extra` to force effect-count mismatch.
- Assertions require target/source object-reference surrounding-whitespace diagnostics, keyed authoritative target/source mismatch diagnostics, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed object-reference canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `153/153`.
- Focused recovery filter: `1094/1094`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1675/1675`.
- Backend full was not rerun for this first post-18BM test-only micro-slice; latest backend full remains Stage 4D-18BM at `7039/7039`.
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
