# Stage 4D-18GD Recovery Timing Continuous Effect Keyed Static Aura Condition Whitespace Value Audit

Date: 2026-06-05 09:55 CST

Project status: **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraConditionWhitespaceValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds a real authoritative battlefield static-aura continuous effect from `MatchState` battlefield/unit object state, verifies the emitted spectator replay-frame timing payload has `effectId = "STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1"` and `condition = "SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD"`, keeps the payload keyed to the authoritative effect id, changes `condition` to the whitespace-only string `"   "`, then appends `effect-extra` to force effect-count mismatch. This locks the existing condition required-scalar, static-aura required-condition and keyed authoritative validation path for spectator replay-frame timing `continuousEffects[]` when broad ordered parity is skipped by count mismatch.

## Diagnostics Locked

- `spectator replay frame timing continuous effect item condition is required`
- `spectator replay frame timing continuous effect item static aura condition is required`
- `spectator replay frame timing continuous effect item condition does not match authoritative state continuous effect condition for effect id STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`
- `spectator replay frame timing continuous effect item effect id effect-extra is not present in authoritative state continuous effects`
- `spectator replay frame timing continuous effect count 2 does not match authoritative state continuous effect count 1`

## Validation

- Focused new test: `1/1`
- Focused `ContinuousEffect` filter: `196/196`
- Focused `MatchRecoveryTests` filter: `1214/1214`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1795/1795`
- Backend full via tracked `Riftbound.slnx`: `7160/7160`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GD stale/typo scan.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
