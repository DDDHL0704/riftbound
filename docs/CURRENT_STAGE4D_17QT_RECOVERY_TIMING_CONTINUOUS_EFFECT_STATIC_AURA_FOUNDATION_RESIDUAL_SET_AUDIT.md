# Stage 4D-17QT Recovery Timing Continuous-Effect Static-Aura Foundation Residual-Set Audit

Date: 2026-06-01 17:47 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `STATIC_AURA` payloads carrying `FOUNDATION_ONLY` LayerEngine status now reject readable `deferredLayerEngineResiduals` lists that do not exactly match the current foundation residual labels emitted by `MatchState.ContinuousEffects`.

Current friendly-equipment and battlefield all-units static-aura builders serialize the same `LayerEngineFoundationResiduals()` list when they are foundation-only. Malformed or missing residual diagnostics and static-aura foundation-status diagnostics remain separate, and power-modifier residual-set canonicality remains covered by Stage 4D-17QS.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraFoundationResidualCanonicalityDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraFoundationResidualCanonicalityDrift`.

## Validation

- Focused static-aura foundation residual-canonicality tests: `2/2`.
- Focused continuous-effect static-aura tests: `24/24`.
- Focused recovery: `532/532`.
- Adjacent recovery/opening/store-smoke: `1113/1113`.
- Backend full: `6478/6478`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
