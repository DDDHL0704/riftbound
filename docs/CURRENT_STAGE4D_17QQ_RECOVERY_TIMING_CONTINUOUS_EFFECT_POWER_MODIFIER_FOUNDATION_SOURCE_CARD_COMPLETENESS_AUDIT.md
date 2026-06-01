# Stage 4D-17QQ Recovery Timing Continuous-Effect Power-Modifier Foundation Source-Card Completeness Audit

Date: 2026-06-01 17:19 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `POWER_MODIFIER` payloads carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals now reject missing or null `sourceCardNo` when `sourceObjectId` is readable.

Current stack/direct runtime builders emit source-object-bearing foundation power modifiers from tracked ledger entries, and those entries carry source object and source card metadata together. Legacy foundation remainders keep both `sourceObjectId` and `sourceCardNo` null, so they remain compatible.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationSourceCardRequiredWithSourceObjectDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationSourceCardRequiredWithSourceObjectDrift`.

## Validation

- Focused power-modifier foundation source-card tests: `4/4`.
- Focused recovery: `526/526`.
- Adjacent recovery/opening/store-smoke: `1107/1107`.
- Backend full: `6472/6472`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
