# Stage 4D-17ZV Recovery Timing Continuous Effect Keyed Optional Field Absence Audit

Date: 2026-06-04
Owner: A_MAIN
Status: Accepted test-coverage checkpoint. Project remains **NOT READY**.

## Scope

This slice adds targeted server recovery coverage for spectator replay-frame timing `continuousEffects[]` same-key authoritative parity when expected-present optional PowerModifier fields are omitted and the spectator list count differs from authoritative state. It does not change production runtime or recovery validation code.

## Coverage Added

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedOptionalFieldAbsenceWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The test builds an authoritative `PowerModifierLedgerEntry` continuous effect from real `MatchState` object state.
- It removes the same `effectId` spectator payload's `effectKind`, `sourceCardNo`, `sourcePath`, `layerEngineStatus`, `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower` and `appliedOrder`.
- It adds an extra forged effect to force effect-count mismatch, proving keyed authoritative diagnostics still run before broad ordered parity is skipped.

## Validation

- Focused new test: `1/1`
- `ContinuousEffect` filter: `135/135`
- `MatchRecoveryTests` filter: `1050/1050`
- Adjacent recovery / official opening / Postgres recovery-store filter: `1631/1631`
- Backend full: `6996/6996`
- Scoped format for `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `tests`, `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This narrows P1-004 replay/recovery determinism test coverage for continuous-effect timing parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
