# Stage 4D-18CA Recovery Timing Continuous Effect Keyed Object Reference Empty Value Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedObjectReferenceEmptyValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key PowerModifier object-reference empty string drift when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing required optional-object-id value and keyed authoritative continuous-effect parity.

## Evidence

- The test builds an authoritative PowerModifier continuous effect from real `MatchState` card-object and object-location state.
- The spectator payload starts with readable `effectId = "effect-1"`, `targetObjectId = "target-1"` and `sourceObjectId = "source-1"` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test mutates `targetObjectId` and `sourceObjectId` to empty strings, then appends `effect-extra` to force effect-count mismatch.
- Assertions require object-id required diagnostics, keyed authoritative target/source mismatch diagnostics, unknown extra-effect diagnostics and count-mismatch diagnostics.

## Validation

- Focused new keyed object-reference empty-value test: `1/1`.
- Focused `ContinuousEffect` filter: `158/158`.
- Focused recovery filter: `1107/1107`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1688/1688`.
- Backend full: not rerun for this second post-18BY test-only micro-slice; latest full remains Stage 4D-18BY at `7051/7051`.
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
