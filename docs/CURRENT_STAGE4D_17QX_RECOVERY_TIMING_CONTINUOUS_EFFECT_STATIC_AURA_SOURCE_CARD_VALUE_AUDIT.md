# Stage 4D-17QX Recovery Timing Continuous-Effect Static-Aura Source-Card Value Audit

Date: 2026-06-01 18:27 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope battlefield `STATIC_AURA` payloads now reject readable `sourceCardNo` values that do not match the current battlefield all-units static-aura builder output.

Current battlefield static auras are built only from `ContinuousEffectStaticAuraCards.BattlefieldAllUnitsPowerPlusOneCardNo`, which serializes as `OGN·294/298`. Object static-aura source cards remain presence-only because friendly-equipment source cards vary by source object.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraSourceCardValueDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraSourceCardValueDrift`.

## Validation

- Focused static-aura source-card tests: `2/2`.
- Focused continuous-effect static-aura tests: `32/32`.
- Focused recovery: `540/540`.
- Adjacent recovery/opening/store-smoke: `1121/1121`.
- Backend full: `6486/6486`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
