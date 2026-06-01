# Stage 4D-17QZ Recovery Timing Continuous-Effect Static-Aura Effect-Id Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovery-frame validation for known valid-scope `STATIC_AURA` continuous-effect identity payloads.

Current runtime builder facts:

- Friendly-equipment object static auras use effect ids shaped as `STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:{sourceObjectId}`.
- Battlefield all-units static auras use effect ids shaped as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:{sourceObjectId}:{targetObjectId}`.
- The object effect-id check only runs after object source/target identity is valid; malformed scope, duration, source or target payloads keep their existing diagnostics.

## Runtime Change

`MatchRecoveryValidator` now rejects recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item payloads when all of these are true:

- `layer` is `STATIC_AURA`.
- `scope` / `duration` match a current builder domain.
- readable `effectId`, `targetObjectId`, and `sourceObjectId` are present.
- the readable effect id differs from the current builder identity for that domain.

The diagnostics are explicit:

```text
... object static aura effect id must be STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:<sourceObjectId>
... battlefield static aura effect id must be STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:<sourceObjectId>:<targetObjectId>
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraEffectIdConsistencyDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraEffectIdConsistencyDrift`

Both tests cover object and battlefield static-aura effect-id drift. The spectator test also proves same-payload effect-id diagnostics still run when the spectator continuous-effect list count differs from authoritative state and indexed authoritative parity is skipped.

## Validation

Passed:

- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter StaticAuraEffectId --no-restore` (`2/2`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter ContinuousEffectStaticAura --no-restore` (`36/36`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests --no-restore` (`544/544`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests" --no-restore` (`1125/1125`)
- `dotnet test Riftbound.slnx --no-restore` (`6490/6490`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
