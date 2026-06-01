# Stage 4D-17QR Recovery Timing Continuous-Effect Power-Modifier Foundation Legacy-Remainder Metadata Audit

Date: 2026-06-01 17:28 CST

## Scope

A_MAIN tightened `MatchRecoveryValidator` continuous-effect validation for recovered player-view snapshot timing and spectator replay-frame timing. Known valid-scope `POWER_MODIFIER` payloads carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals now reject no-source, no-tracked-scalar legacy-remainder metadata drift when readable `effectKind` or `sourcePath` differs from current builder output.

Current `MatchState.ContinuousEffects` emits that no-source foundation branch only for untracked legacy remainders with `effectKind` `LEGACY_UNTRACKED_POWER_MODIFIER` and `sourcePath` `MatchState.ContinuousEffects.LegacyRemainder`. Stack/direct tracked power modifiers carry source objects plus tracked scalar/order metadata, so they remain outside this legacy-remainder check.

## Runtime And Tests

- Runtime changed: `src/Riftbound.Engine/MatchRecovery.cs`.
- Tests changed: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Added recovered snapshot coverage: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationLegacyRemainderMetadataDrift`.
- Added spectator replay coverage: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationLegacyRemainderMetadataDrift`.

## Validation

- Focused legacy-remainder tests: `2/2`.
- Focused power-modifier foundation tests: `16/16`.
- Focused recovery: `528/528`.
- Adjacent recovery/opening/store-smoke: `1109/1109`.
- Backend full: `6474/6474`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

## Non-Closure

This narrows P1-004 replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, matrix readiness and final project status remain open. Project remains **NOT READY**.
