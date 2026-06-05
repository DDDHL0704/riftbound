# Stage 4D-18GA Recovery Timing Continuous Effect Keyed Static Aura Condition Shape Audit

Date: 2026-06-05 09:19 CST

Project status: **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraConditionShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds a real authoritative battlefield static-aura continuous effect from `MatchState` battlefield/unit object state, verifies the emitted spectator replay-frame timing payload has `effectId = "STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1"` and `condition = "SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD"`, keeps the payload keyed to the authoritative effect id, changes `condition` to an unreadable dictionary payload, then appends `effect-extra` to force effect-count mismatch. This locks the existing condition payload-shape, static-aura required-condition and keyed authoritative validation path for spectator replay-frame timing `continuousEffects[]` when broad ordered parity is skipped by count mismatch.

## Diagnostics Locked

- `spectator replay frame timing continuous effect item condition is invalid`
- `spectator replay frame timing continuous effect item static aura condition is required`
- `spectator replay frame timing continuous effect item condition does not match authoritative state continuous effect condition for effect id STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`
- `spectator replay frame timing continuous effect item effect id effect-extra is not present in authoritative state continuous effects`
- `spectator replay frame timing continuous effect count 2 does not match authoritative state continuous effect count 1`

## Validation

- Focused new test: `1/1`
- Focused `ContinuousEffect` filter: `193/193`
- Focused `MatchRecoveryTests` filter: `1211/1211`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1792/1792`
- Backend full via `Riftbound.slnx`: `7157/7157`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GA stale/typo scan.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
