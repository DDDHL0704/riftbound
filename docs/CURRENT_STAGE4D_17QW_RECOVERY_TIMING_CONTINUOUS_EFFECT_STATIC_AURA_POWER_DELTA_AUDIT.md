# Stage 4D-17QW Recovery Timing Continuous-Effect Static-Aura Power-Delta Audit

Date: 2026-06-01 18:17 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `STATIC_AURA` payloads now reject readable `powerDelta` values that do not match current runtime builder output.

Current object static auras derive `powerDelta` from friendly equipment participants: without a readable `participantObjectIds` list the value must be `0`, and with a readable participant list it must equal the participant object count. Current battlefield static auras derive from the all-units battlefield bonus and require `powerDelta` `1`. Malformed participant-list payloads keep their existing list-shape diagnostics.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraPowerDeltaConsistencyDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraPowerDeltaConsistencyDrift`.

## Validation

- Focused static-aura power-delta tests: `2/2`.
- Focused continuous-effect static-aura tests: `30/30`.
- Focused recovery: `538/538`.
- Adjacent recovery/opening/store-smoke: `1119/1119`.
- Backend full: `6484/6484`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
