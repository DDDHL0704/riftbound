# Stage 4D-18FX Recovery Timing Continuous Effect Keyed Static Aura Effect Kind Canonicality Audit

Date: 2026-06-05 08:49 CST

Project status: **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraEffectKindCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds a real authoritative battlefield static-aura continuous effect from `MatchState` battlefield/unit object state, verifies the emitted spectator replay-frame timing payload has `effectId = "STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1"` and `effectKind = "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE"`, keeps the payload keyed to the authoritative effect id, wraps `effectKind` in surrounding whitespace, then appends `effect-extra` to force effect-count mismatch. This locks the existing scalar canonicality and keyed authoritative validation path for spectator replay-frame timing `continuousEffects[]` when broad ordered parity is skipped by count mismatch.

## Diagnostics Locked

- `spectator replay frame timing continuous effect item effect kind BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE has surrounding whitespace`
- `spectator replay frame timing continuous effect item effect kind does not match authoritative state continuous effect effect kind for effect id STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`
- `spectator replay frame timing continuous effect item effect id effect-extra is not present in authoritative state continuous effects`
- `spectator replay frame timing continuous effect count 2 does not match authoritative state continuous effect count 1`

The static-aura metadata fixed-value check canonicalizes the readable effect-kind value for this whitespace case, so this slice intentionally does not assert a battlefield static-aura effect-kind must-be diagnostic. Value drift remains covered by Stage 4D-18FQ.

## Validation

- Focused new test: `1/1`
- Focused `ContinuousEffect` filter: `190/190`
- Focused `MatchRecoveryTests` filter: `1208/1208`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1789/1789`
- Backend full via `Riftbound.slnx`: `7154/7154`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18FX stale/typo scan.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
