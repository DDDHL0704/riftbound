# Stage 4D-18GE Recovery Timing Continuous Effect Keyed Static Aura Lifecycle Missing Field Audit

Date: 2026-06-05 10:08 CST

Project status: **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLifecycleMissingFieldWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds a real authoritative battlefield static-aura continuous effect from `MatchState` battlefield/unit object state, verifies the emitted spectator replay-frame timing payload has `effectId = "STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1"` and `lifecycle = "DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS"`, keeps the payload keyed to the authoritative effect id, removes `lifecycle`, then appends `effect-extra` to force effect-count mismatch. This locks the existing static-aura required-lifecycle and keyed authoritative validation path for spectator replay-frame timing `continuousEffects[]` when broad ordered parity is skipped by count mismatch.

## Diagnostics Locked

- `spectator replay frame timing continuous effect item static aura lifecycle is required`
- `spectator replay frame timing continuous effect item lifecycle does not match authoritative state continuous effect lifecycle for effect id STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`
- `spectator replay frame timing continuous effect item effect id effect-extra is not present in authoritative state continuous effects`
- `spectator replay frame timing continuous effect count 2 does not match authoritative state continuous effect count 1`

## Validation

- Focused new test: `1/1`
- Focused `ContinuousEffect` filter: `197/197`
- Focused `MatchRecoveryTests` filter: `1215/1215`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1796/1796`
- Backend full via tracked `Riftbound.slnx`: `7161/7161`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GE stale/typo scan.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
