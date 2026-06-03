# Stage 4D-17ZY Recovery Timing Continuous Effect Keyed PowerModifier Metadata Audit

Date: 2026-06-04
Owner: A_MAIN
Status: Accepted test-coverage checkpoint. Project remains **NOT READY**.

## Scope

This slice adds targeted server recovery coverage for spectator replay-frame timing `continuousEffects[]` same-key authoritative parity when expected-present tracked PowerModifier metadata values drift and the spectator list count differs from authoritative state. It does not change production runtime or recovery validation code.

## Coverage Added

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierMetadataWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state.
- It mutates the same `effectId` spectator payload's `sourceCardNo` and `layerEngineStatus`.
- It adds an extra forged effect to force effect-count mismatch, proving keyed authoritative diagnostics still run before broad ordered parity is skipped.
- It also asserts the invalid LayerEngine status shape diagnostic for the forged `layerEngineStatus` value.

## Validation

- Focused new test: `1/1`
- `ContinuousEffect` filter: `138/138`
- `MatchRecoveryTests` filter: `1053/1053`
- Adjacent recovery / official opening / Postgres recovery-store filter: `1634/1634`
- Backend full: `6999/6999`
- Scoped format for `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `tests`, `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This narrows P1-004 replay/recovery determinism test coverage for continuous-effect timing parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
