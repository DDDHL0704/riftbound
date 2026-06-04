# Stage 4D-18BZ Recovery Timing Continuous Effect Keyed Object Reference Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedObjectReferenceShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key PowerModifier object-reference shape drift when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing optional-string shape and keyed authoritative continuous-effect parity.

## Evidence

- The test builds an authoritative PowerModifier continuous effect from real `MatchState` card-object and object-location state.
- The spectator payload starts with readable `effectId = "effect-1"`, `targetObjectId = "target-1"` and `sourceObjectId = "source-1"` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test mutates `targetObjectId` to a string array and `sourceObjectId` to an object payload, then appends `effect-extra` to force effect-count mismatch.
- Assertions require object-id invalid diagnostics, keyed authoritative target/source mismatch diagnostics, unknown extra-effect diagnostics and count-mismatch diagnostics.

## Validation

- Focused new keyed object-reference shape test: `1/1`.
- Focused `ContinuousEffect` filter: `157/157`.
- Focused recovery filter: `1106/1106`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1687/1687`.
- Backend full: not rerun for this first post-18BY test-only micro-slice; latest full remains Stage 4D-18BY at `7051/7051`.
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
