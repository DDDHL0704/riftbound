# Stage 4D-18CB Recovery Timing Continuous Effect Keyed Object Reference Null Value Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedObjectReferenceNullValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key PowerModifier object-reference null-value drift when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing nullable-object-id and keyed authoritative continuous-effect parity.

## Evidence

- The test builds an authoritative PowerModifier continuous effect from real `MatchState` card-object and object-location state.
- The spectator payload starts with readable `effectId = "effect-1"`, `targetObjectId = "target-1"` and `sourceObjectId = "source-1"` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test mutates `targetObjectId` and `sourceObjectId` to `null`, then appends `effect-extra` to force effect-count mismatch.
- Assertions require keyed authoritative target/source mismatch diagnostics, unknown extra-effect diagnostics and count-mismatch diagnostics.

## Validation

- Focused new keyed object-reference null-value test: `1/1`.
- Focused `ContinuousEffect` filter: `159/159`.
- Focused recovery filter: `1108/1108`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1689/1689`.
- Backend full: `7054/7054`.
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
