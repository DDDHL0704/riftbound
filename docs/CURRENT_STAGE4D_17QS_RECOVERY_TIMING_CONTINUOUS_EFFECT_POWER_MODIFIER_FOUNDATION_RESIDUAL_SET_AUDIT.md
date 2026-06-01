# Stage 4D-17QS Recovery Timing Continuous-Effect Power-Modifier Foundation Residual-Set Audit

Date: 2026-06-01 17:37 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `POWER_MODIFIER` payloads carrying `FOUNDATION_ONLY` LayerEngine status now reject readable `deferredLayerEngineResiduals` lists that do not exactly match the current foundation residual labels emitted by `MatchState.ContinuousEffects`.

Current tracked power-modifier ledger entries and legacy untracked remainders both serialize the same `LayerEngineFoundationResiduals()` list when they are foundation-only. Non-foundation power modifiers remain outside this canonicality check, and existing malformed or missing residual diagnostics keep their prior wording.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationResidualCanonicalityDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationResidualCanonicalityDrift`.

## Validation

- Focused residual-canonicality tests: `2/2`.
- Focused power-modifier foundation tests: `18/18`.
- Focused recovery: `530/530`.
- Adjacent recovery/opening/store-smoke: `1111/1111`.
- Backend full: `6476/6476`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
