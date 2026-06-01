# Stage 4D-17RA Recovery Timing Continuous-Effect Static-Aura Battlefield Power-Scalar Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovery-frame validation for known valid-scope battlefield `STATIC_AURA` continuous-effect power scalars.

Current runtime builder facts:

- Battlefield all-units static auras serialize `basePower` from the participant's current power before the static +1.
- They serialize fixed `powerDelta=1`.
- They serialize `effectivePower=basePower + powerDelta`.
- Object static auras are intentionally excluded because object effective power can include temporary power state in addition to friendly-equipment static-aura state.

## Runtime Change

`MatchRecoveryValidator` now rejects recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item payloads when all of these are true:

- `layer` is `STATIC_AURA`.
- `scope` is `BATTLEFIELD`.
- `duration` is `WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD`.
- readable `powerDelta`, `basePower`, and `effectivePower` are present.
- `effectivePower != basePower + powerDelta`.

The diagnostic is explicit:

```text
... battlefield static aura effective power <effectivePower> must equal base power <basePower> plus power delta <powerDelta>
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraBattlefieldPowerScalarConsistencyDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldPowerScalarConsistencyDrift`

The spectator test keeps the same count-mismatch path used by adjacent recovery slices: authoritative continuous effects are empty, the spectator frame carries one malformed battlefield static aura, and same-payload power-scalar validation still emits its diagnostic alongside the count mismatch.

## Validation

Passed:

- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter StaticAuraBattlefieldPowerScalar --no-restore` (`2/2`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter ContinuousEffectStaticAura --no-restore` (`38/38`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests --no-restore` (`546/546`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests" --no-restore` (`1127/1127`)
- `dotnet test Riftbound.slnx --no-restore` (`6492/6492`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
