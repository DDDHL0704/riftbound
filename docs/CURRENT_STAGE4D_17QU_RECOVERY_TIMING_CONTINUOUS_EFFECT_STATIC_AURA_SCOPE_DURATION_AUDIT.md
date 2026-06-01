# Stage 4D-17QU Recovery Timing Continuous-Effect Static-Aura Scope-Duration Audit

Date: 2026-06-01 17:57 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `STATIC_AURA` payloads now reject `OBJECT` scope paired with any duration other than `WHILE_SOURCE_ON_PUBLIC_FIELD`, and `BATTLEFIELD` scope paired with any duration other than `WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD`.

Current friendly-equipment object static-aura builders and battlefield all-units static-aura builders serialize those canonical scope/duration pairs. Unknown or invalid scalar values, static-aura layer/scope consistency, and static-aura layer/duration consistency remain separate diagnostics.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraScopeDurationConsistencyDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraScopeDurationConsistencyDrift`.

## Validation

- Focused static-aura scope-duration tests: `2/2`.
- Focused continuous-effect static-aura tests: `26/26`.
- Focused recovery: `534/534`.
- Adjacent recovery/opening/store-smoke: `1115/1115`.
- Backend full: `6480/6480`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
