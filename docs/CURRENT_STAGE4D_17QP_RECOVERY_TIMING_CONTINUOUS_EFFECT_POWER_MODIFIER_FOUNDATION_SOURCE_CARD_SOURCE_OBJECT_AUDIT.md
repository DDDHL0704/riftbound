# Stage 4D-17QP Recovery Timing Continuous-Effect Power-Modifier Foundation Source-Card Source-Object Audit

Date: 2026-06-01 17:10 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `POWER_MODIFIER` payloads carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals now reject readable `sourceCardNo` when `sourceObjectId` is null.

Current stack/direct runtime builders emit source-card metadata from tracked ledger power modifiers tied to a source object. Legacy foundation remainders keep both `sourceObjectId` and `sourceCardNo` null, so they remain compatible.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationSourceCardWithoutSourceObjectDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationSourceCardWithoutSourceObjectDrift`.

## Validation

- Focused power-modifier foundation source-card/source-object tests: `2/2`.
- Focused recovery: `524/524`.
- Adjacent recovery/opening/store-smoke: `1105/1105`.
- Backend full: `6470/6470`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
