# Stage 4D-18GB Recovery Timing Continuous Effect Keyed Static Aura Condition Value Audit

Date: 2026-06-05 09:29 CST

Project status: **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraConditionValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds a real authoritative battlefield static-aura continuous effect from `MatchState` battlefield/unit object state, verifies the emitted spectator replay-frame timing payload has `effectId = "STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1"` and `condition = "SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD"`, keeps the payload keyed to the authoritative effect id, changes `condition` to the readable wrong value `WRONG_CONDITION`, then appends `effect-extra` to force effect-count mismatch. This locks the existing battlefield static-aura condition fixed-value validation and keyed authoritative validation path for spectator replay-frame timing `continuousEffects[]` when broad ordered parity is skipped by count mismatch.

## Diagnostics Locked

- `spectator replay frame timing continuous effect item battlefield static aura condition must be SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD`
- `spectator replay frame timing continuous effect item condition does not match authoritative state continuous effect condition for effect id STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`
- `spectator replay frame timing continuous effect item effect id effect-extra is not present in authoritative state continuous effects`
- `spectator replay frame timing continuous effect count 2 does not match authoritative state continuous effect count 1`

## Validation

- Focused new test: `1/1`
- Focused `ContinuousEffect` filter: `194/194`
- Focused `MatchRecoveryTests` filter: `1212/1212`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1793/1793`
- Backend full via tracked `Riftbound.slnx`: `7158/7158`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GB stale/typo scan.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
