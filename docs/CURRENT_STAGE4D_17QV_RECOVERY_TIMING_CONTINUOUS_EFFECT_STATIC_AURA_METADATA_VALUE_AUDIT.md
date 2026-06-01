# Stage 4D-17QV Recovery Timing Continuous-Effect Static-Aura Metadata-Value Audit

Date: 2026-06-01 18:09 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `STATIC_AURA` payloads now reject readable `effectKind`, `sourcePath`, `condition` or `lifecycle` values that do not match the current runtime builder tuple for their canonical scope/duration pair.

Current object static auras serialize friendly-equipment metadata from `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute`. Current battlefield static auras serialize all-units battlefield metadata from `CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus`. `sourceCardNo` remains presence-validated, not exact-value canonicalized, because friendly-equipment source card numbers vary by source card.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraMetadataValueDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraMetadataValueDrift`.

## Validation

- Focused static-aura metadata-value tests: `2/2`.
- Focused continuous-effect static-aura tests: `28/28`.
- Focused recovery: `536/536`.
- Adjacent recovery/opening/store-smoke: `1117/1117`.
- Backend full: `6482/6482`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
